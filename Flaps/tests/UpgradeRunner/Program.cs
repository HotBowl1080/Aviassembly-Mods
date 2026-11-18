using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
class Program {
 const WingUpgradeOptions F=WingUpgradeOptions.Flaps,S=WingUpgradeOptions.Spoilers;
 static int assertions;
 static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 static void Near(float a,float b,string message){Check(Math.Abs(a-b)<.0001f,message+": "+a+" != "+b);}
 sealed class Context {
  internal GameObject root;
  internal PlaneStorage storage;
  internal Wing wing;
  internal BuildingPart part;
  internal WingUpgrades upgrades;
  internal Context(string type="BigWing",float price=400f,float cash=1000f,bool placed=true,bool creative=false){
   GameManager.Instance=new GameManager();GameManager.Instance.gameModeData.creativeMode=creative;
   MoneyManager.Instance=new MoneyManager{money=cash};FlapMarker.FailGeometry=false;FlapMarker.FailOnName=null;PartPlacer.mirrorMode=false;
   root=new GameObject("Plane");root.AddComponent<PlaneContainer>();storage=root.AddComponent<PlaneStorage>();PlaneStorage.Instance=storage;
   var o=new GameObject(type);o.Parent(root);part=o.AddComponent<BuildingPart>();part.price=price;part.hasBeenPlaced=placed;
   o.AddComponent<MeshFilter>();wing=o.AddComponent<Wing>();upgrades=WingUpgrades.Ensure(wing);
  }
  internal bool Set(WingUpgradeOptions option,bool value){string error;return upgrades.TrySet(option,value,out error);}
 }
 static void Patch(Type type,string method,params object[] args){type.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);}
 static void Main(){try{Transactions();Mirroring();Snapshots();CodecSafety();Console.WriteLine("PASS "+assertions+" upgrade assertions: per-wing 25% prices, independent options, atomic mirrored purchases/refunds, campaign debits/refunds, exact affordability, creative mode, placement/copy/resale, undo, old/new save round-trips, cursor/prefix preservation, malformed metadata rejection.");}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Receipts(){
  using(var stream=new MemoryStream()){
   stream.Write(new byte[20],0,20);Check(WingUpgradeReceipt.Read(stream)==null,"Stock design gets no receipt");
   var pair=new List<WingUpgradeSaveCodec.Record>{
    new WingUpgradeSaveCodec.Record{partIndex=0,wingIndex=0,wingType=1,basePrice=2500,options=F|S},
    new WingUpgradeSaveCodec.Record{partIndex=1,wingIndex=0,wingType=2,basePrice=500,options=F}};
   WingUpgradeSaveCodec.Append(stream,pair);stream.Position=3;
   var text=WingUpgradeReceipt.Read(stream);Check(stream.Position==3,"Inspection preserves stream position");
   Check(text.Contains("Flaps (2):  $750"),"Independent flap prices");Check(text.Contains("Spoilers (1):  $625"),"Spoilers counted separately");
   Check(text.EndsWith(1375d.ToString("#,0.##")),"Combined value includes both options");
  }
 }
 static void DesignLoads(){
  Receipts();
  var ctx=new Context("SmallRecreationalWing");Check(ctx.Set(F,true),"Install recreational flaps");Check(!ctx.Set(S,true),"Reject recreational spoilers");
  var records=WingUpgradePersistence.Capture(ctx.storage);Check(records.Single().wingType==3,"Distinct saved wing identity");
  var copy=new Context("SmallRecreationalWing");WingUpgradePersistence.Restore(copy.storage,records);Near(copy.part.price,500,"Restored flap price");
  var plane=copy.storage;plane.value=400;MoneyManager.Instance.money=125;
  foreach(var target in new[]{500f,500f,600f,400f}){
   float priorValue=plane.value,priorCash=MoneyManager.Instance.money;
   var capture=new object[]{plane,null};Patch(typeof(ReconcilePurchasedDesign),"Prefix",capture);
   plane.value=target;MoneyManager.Instance.money=priorCash+priorValue-400;
   var finish=new object[]{plane,true,capture[1]};Patch(typeof(ReconcilePurchasedDesign),"Postfix",finish);
   bool affordable=priorCash+priorValue>=target;
   Check((bool)finish[1]==affordable,"Design affordability");
   Near(plane.value,affordable?target:priorValue,"Design restored or rolled back");
   Near(MoneyManager.Instance.money,affordable?priorCash+priorValue-target:priorCash,"Design money conserved");
   Patch(typeof(ReconcilePurchasedDesign),"Finalizer",null,plane,capture[1]);
  }
 }
 static void Transactions(){
  DesignLoads();
  var c=new Context();Check(c.Set(F,true),"Buy flaps");Near(MoneyManager.Instance.money,900,"Flaps debit");Near(c.part.price,500,"Wing value includes flaps");
  Check(c.Set(F,true),"Repeated enable succeeds");Near(MoneyManager.Instance.money,900,"Repeated enable is not charged");Check(c.storage.histories==1,"Repeated enable has no history step");
  Check(c.Set(S,true),"Buy spoilers independently");Near(MoneyManager.Instance.money,800,"Spoilers debit");Near(c.part.price,600,"Both cost 50% of BASE wing");
  Check(c.Set(F,false),"Remove flaps");Near(MoneyManager.Instance.money,900,"Flap refund");Near(c.part.price,500,"Spoilers remain valued");Check(!c.upgrades.HasFlaps && c.upgrades.HasSpoilers,"Spoilers operate without flap ownership");
  Check(c.Set(F,false),"Repeated removal");Near(MoneyManager.Instance.money,900,"No duplicate refund");
  Check(c.Set(S,false),"Remove spoilers");Near(MoneyManager.Instance.money,1000,"Complete cycle returns money");Near(c.part.price,400,"Base value restored");Check(!c.wing.GetComponent<FlapMarker>().enabled,"All upgrades off disables runtime");
  c=new Context("StraightWing");Check(!c.Set(S,true),"Straight Wing cannot buy spoilers");Near(c.part.price,400,"Unsupported option preserves price");Near(MoneyManager.Instance.money,1000,"Unsupported option preserves cash");
  c=new Context(cash:99f);Check(!c.Set(F,true),"Reject unaffordable purchase");Near(MoneyManager.Instance.money,99,"Failed purchase cash unchanged");Near(c.part.price,400,"Failed purchase price unchanged");Check(c.storage.histories==0,"No history on failure");
  c=new Context(price:101f,cash:25.249f);Check(!c.Set(F,true),"Fractional amount cannot overdraft via rounded affordability");
  MoneyManager.Instance.money=25.25f;Check(c.Set(F,true),"Exact fractional balance accepted");Near(MoneyManager.Instance.money,0,"Exact charge");Near(c.part.price,126.25f,"Fractional wing value");Check(c.Set(F,false),"Fractional refund");Near(MoneyManager.Instance.money,25.25f,"Exact refund");
  c=new Context(cash:0,creative:true);Check(c.Set(F,true)&&c.Set(S,true),"Creative buys both");Near(MoneyManager.Instance.money,0,"Creative never debits");Near(c.part.price,600,"Creative still records aircraft value");Check(c.Set(F,false)&&c.Set(S,false),"Creative removal");Near(MoneyManager.Instance.money,0,"Creative cannot farm refund money");
  c=new Context(placed:false);Check(c.Set(F,true)&&c.Set(S,true),"Configure unplaced wing");Near(MoneyManager.Instance.money,1000,"Unplaced options not charged twice");Near(c.part.price,600,"Placement quote contains both options");
  Check(c.Set(S,false),"Remove unplaced option");Near(MoneyManager.Instance.money,1000,"Unplaced cancellation never refunds unpaid money");Check(c.Set(S,true),"Restore preview option");
  MoneyManager.Instance.ChangeMoneyAmount(-c.part.price);c.part.hasBeenPlaced=true;Near(MoneyManager.Instance.money,400,"Native placement charges total once");
  Check(c.Set(F,false),"Remove purchased option after placement");Near(MoneyManager.Instance.money,500,"Only actual option price refunded");MoneyManager.Instance.ChangeMoneyAmount(c.part.price);Near(MoneyManager.Instance.money,1000,"Native resale recovers remaining wing value once");
  c=new Context();FlapMarker.FailGeometry=true;Check(!c.Set(F,true),"Mesh failure rejects purchase");Near(c.part.price,400,"Mesh failure price unchanged");Near(MoneyManager.Instance.money,1000,"Mesh failure not charged");
  c=new Context();c.root.GetComponent<PlaneContainer>().FlightModeInitialized=true;Check(!c.Set(F,true),"Cannot buy/remove in flight");
  c=new Context();Check(c.Set(F,true)&&c.Set(S,true),"Copy source configured");var target=new GameObject("BigWing");target.Parent(c.root);var p=target.AddComponent<BuildingPart>();p.price=400;p.hasBeenPlaced=false;target.AddComponent<MeshFilter>();target.AddComponent<Wing>();
  Patch(typeof(CopyWingUpgrades),"Postfix",c.part,p);Near(p.price,600,"Mirror copy includes upgrade price");Near(MoneyManager.Instance.money,800,"Copy not charged before placement");Check(target.GetComponent<WingUpgrades>().Options==(F|S),"Mirror copied both options");
 }
 static WingUpgrades Partner(Context c,float price=400f,bool placed=true){
  var o=new GameObject("BigWingMirror");o.Parent(c.root);var p=o.AddComponent<BuildingPart>();p.price=price;p.hasBeenPlaced=placed;o.AddComponent<MeshFilter>();return WingUpgrades.Ensure(o.AddComponent<Wing>());
 }
 static void Mirroring(){
  var c=new Context(cash:150f);var peer=Partner(c);var placer=c.root.AddComponent<PartPlacer>();placer.currentSelectedPart=c.part;placer.partner=peer.Part;placer.currentMirrorPart=peer.Part;PartPlacer.mirrorMode=true;
  var group=WingUpgradeMirroring.Group(c.upgrades,placer);Check(group.Count==2,"Find mirrored settings partner");string error;
  Check(!WingUpgrades.TrySetGroup(group,F,true,out error),"Combined affordability checked first");Near(MoneyManager.Instance.money,150,"Rejected pair cash unchanged");Check(group.All(s=>s.Options==WingUpgradeOptions.None),"Rejected pair leaves both options off");Near(c.part.price+peer.Part.price,800,"Rejected pair leaves both prices unchanged");
  MoneyManager.Instance.money=1000;Check(WingUpgrades.TrySetGroup(group,F,true,out error),"Buy mirrored flaps");Near(MoneyManager.Instance.money,800,"Charge 25% for EACH wing");Check(group.All(s=>s.HasFlaps),"Both wings enabled");Check(c.storage.histories==1,"Pair is one undo operation");
  Check(WingUpgrades.TrySetGroup(group,F,true,out error),"Repeated pair enable");Near(MoneyManager.Instance.money,800,"No repeat pair charge");
  Check(WingUpgrades.TrySetGroup(group,S,true,out error),"Buy mirrored spoilers");Near(MoneyManager.Instance.money,600,"Both independent upgrades on both wings");Near(c.part.price+peer.Part.price,1200,"Pair asset value matches total spending");
  Check(WingUpgrades.TrySetGroup(group,F,false,out error),"Remove mirrored flaps");Near(MoneyManager.Instance.money,800,"Pair flap refund only");Check(group.All(s=>!s.HasFlaps&&s.HasSpoilers),"Both spoilers retained");
  PartPlacer.mirrorMode=false;Check(WingUpgradeMirroring.Group(c.upgrades,placer).Count==1,"Mirror-off affects only selected wing");Check(c.Set(S,false),"Remove selected spoiler only");Near(MoneyManager.Instance.money,900,"Only selected spoiler refunded");Check(!c.upgrades.HasSpoilers&&peer.HasSpoilers,"Partner left alone with mirror off");
  PartPlacer.mirrorMode=true;Check(WingUpgrades.TrySetGroup(group,S,true,out error),"Align mismatched ownership");Near(MoneyManager.Instance.money,800,"Only missing upgrade is charged");Check(WingUpgrades.TrySetGroup(group,S,false,out error),"Remove aligned pair");Near(MoneyManager.Instance.money,1000,"Exact pair refund after mixed state");
  placer.partner=c.part;Check(WingUpgradeMirroring.Group(c.upgrades,placer).Count==1,"Centreline part never charged twice");placer.partner=null;placer.currentMirrorPart=null;Check(WingUpgradeMirroring.Group(c.upgrades,placer).Count==1,"Missing partner remains single-wing");
  c=new Context();peer=Partner(c);FlapMarker.FailOnName="BigWingMirror";group=new List<WingUpgrades>{c.upgrades,peer};Check(!WingUpgrades.TrySetGroup(group,F,true,out error),"Second mesh failure rejects entire pair");Near(MoneyManager.Instance.money,1000,"Pair geometry failure has no debit");Check(group.All(s=>s.Options==WingUpgradeOptions.None),"Pair geometry failure leaves ownership untouched");Check(!c.wing.GetComponent<FlapMarker>().enabled,"Prepared first wing returned to stock on partner failure");
  c=new Context(cash:300);peer=Partner(c,800);group=new List<WingUpgrades>{c.upgrades,peer};Check(WingUpgrades.TrySetGroup(group,F,true,out error),"Different base costs priced separately");Near(MoneyManager.Instance.money,0,"400/800 pair costs 100+200");Near(c.part.price,500,"First wing value");Near(peer.Part.price,1000,"Second wing value");
  var records=WingUpgradePersistence.Capture(c.storage);Check(records.Count==2,"Both mirrored options saved independently");var restored=new Context(cash:777);var restoredPeer=Partner(restored,800);WingUpgradePersistence.Restore(restored.storage,records);Check(restored.upgrades.HasFlaps&&restoredPeer.HasFlaps,"Both mirrored settings restored");Near(MoneyManager.Instance.money,777,"Pair load never double-charges");
  c=new Context();peer=Partner(c,placed:false);group=new List<WingUpgrades>{c.upgrades,peer};Check(WingUpgrades.TrySetGroup(group,F,true,out error),"Mixed placed/preview pair");Near(MoneyManager.Instance.money,900,"Only placed wing charged now");Check(WingUpgrades.TrySetGroup(group,F,false,out error),"Cancel mixed pair");Near(MoneyManager.Instance.money,1000,"Unpaid mirror preview cannot generate refunds");
  c=new Context(cash:0,creative:true);peer=Partner(c);group=new List<WingUpgrades>{c.upgrades,peer};Check(WingUpgrades.TrySetGroup(group,F|S,true,out error),"Creative mirrored upgrades");Near(MoneyManager.Instance.money,0,"Creative pair never changes money");
 }
 static void Snapshots(){
  var c=new Context();Check(c.Set(F,true)&&c.Set(S,true),"Prepare snapshot");var records=WingUpgradePersistence.Capture(c.storage);Check(records.Count==1 && records[0].options==(F|S),"Snapshot captures per-wing options");
  var prefix=Enumerable.Range(0,200).Select(i=>(byte)i).ToArray();var stream=new MemoryStream();stream.Write(prefix,0,prefix.Length);Patch(typeof(SaveWingUpgradeSnapshot),"Postfix",c.storage,stream);
  Check(stream.ToArray().Take(prefix.Length).SequenceEqual(prefix),"Save preserves all native bytes");
  var loaded=new Context(cash:123f);stream.Position=64;Patch(typeof(LoadWingUpgradeSnapshot),"Postfix",loaded.storage,new GameDataReader(stream));
  Check(stream.Position==64,"Load preserves campaign reader cursor");Near(MoneyManager.Instance.money,123,"Restore never charges a second time");Near(loaded.part.price,600,"Restored part value includes purchased options");Check(loaded.upgrades.Options==(F|S),"Flags restored");
  // Native Undo refunds old aircraft cost minus restored aircraft cost.
  var beforeOnlyFlaps=new List<WingUpgradeSaveCodec.Record>{new WingUpgradeSaveCodec.Record{partIndex=0,wingIndex=0,wingType=1,basePrice=400,options=F}};
  float oldValue=loaded.part.price;WingUpgradePersistence.Restore(loaded.storage,beforeOnlyFlaps);Near(MoneyManager.Instance.money,123,"Undo restore itself has no money side effect");
  MoneyManager.Instance.ChangeMoneyAmount(oldValue-loaded.part.price);Near(MoneyManager.Instance.money,223,"Native undo refunds option once");
  float previous=loaded.part.price;WingUpgradePersistence.Restore(loaded.storage,records);MoneyManager.Instance.ChangeMoneyAmount(previous-loaded.part.price);Near(MoneyManager.Instance.money,123,"Restoring higher-value history recharges once");
  string file=Path.Combine(Path.GetTempPath(),"flap-mod-save-test-"+Guid.NewGuid().ToString("N")+".tmp");
  try{File.WriteAllBytes(file,prefix);Patch(typeof(SaveCampaignWingUpgrades),"Postfix",file);var bytes=File.ReadAllBytes(file);Check(bytes.Take(prefix.Length).SequenceEqual(prefix),"Campaign footer preserves native save prefix");using(var read=new MemoryStream(bytes)){List<WingUpgradeSaveCodec.Record> restored;Check(WingUpgradeSaveCodec.TryRead(read,out restored)&&restored[0].options==(F|S),"Campaign file carries choices");}}
  finally{if(File.Exists(file))File.Delete(file);}
  loaded=new Context();var old=new MemoryStream(prefix);old.Position=31;Patch(typeof(LoadWingUpgradeSnapshot),"Postfix",loaded.storage,new GameDataReader(old));Check(old.Position==31 && loaded.upgrades.Options==WingUpgradeOptions.None,"Legacy saves keep stock defaults and cursor");Near(MoneyManager.Instance.money,1000,"Legacy load does not spend money");
  var empty=new MemoryStream();empty.Write(prefix,0,prefix.Length);WingUpgradeSaveCodec.Append(empty,new List<WingUpgradeSaveCodec.Record>());Check(empty.ToArray().SequenceEqual(prefix),"No choices produces no save-format change");
  var straight=new Context("StraightWing");WingUpgradePersistence.Restore(straight.storage,new List<WingUpgradeSaveCodec.Record>{new WingUpgradeSaveCodec.Record{partIndex=0,wingIndex=0,wingType=2,basePrice=400,options=F}});Check(straight.upgrades.HasFlaps&&!straight.upgrades.HasSpoilers,"Straight Wing persisted separately");
 }
 static void CodecSafety(){
  var good=new WingUpgradeSaveCodec.Record{partIndex=0,wingIndex=0,wingType=1,basePrice=400,options=F|S};
  for(int flags=1;flags<=3;flags++)foreach(float price in new[]{0f,1f,101f,123456f}){
   good.options=(WingUpgradeOptions)flags;good.basePrice=price;using(var s=new MemoryStream()){s.WriteByte(99);WingUpgradeSaveCodec.Append(s,new[]{good});s.Position=1;List<WingUpgradeSaveCodec.Record> restored;Check(WingUpgradeSaveCodec.TryRead(s,out restored),"Footer roundtrip");Check(restored[0].options==good.options&&restored[0].basePrice==price&&s.Position==1,"Footer values/cursor roundtrip");}
  }
  good.basePrice=400;good.options=F;
  using(var s=new MemoryStream()){
   WingUpgradeSaveCodec.Append(s,new[]{good});var bytes=s.ToArray();bytes[0]=99;var bad=new MemoryStream(bytes);bad.Position=3;bool rejected=false;try{List<WingUpgradeSaveCodec.Record> ignored;WingUpgradeSaveCodec.TryRead(bad,out ignored);}catch(InvalidDataException){rejected=true;}Check(rejected&&bad.Position==3,"Malformed version rejected without moving reader");
   var c=new Context(cash:321);bad.Position=2;Patch(typeof(LoadWingUpgradeSnapshot),"Postfix",c.storage,new GameDataReader(bad));Check(bad.Position==2 && c.upgrades.Options==WingUpgradeOptions.None,"Rejected optional metadata does not abort or desynchronize native load");Near(MoneyManager.Instance.money,321,"Rejected metadata leaves native balance alone");
  }
  foreach(var kind in new[]{"duplicate","straight-spoilers","nan","invalid-flags","negative-index"}){
   var bad=good;if(kind=="straight-spoilers"){bad.wingType=2;bad.options=S;}if(kind=="nan")bad.basePrice=float.NaN;if(kind=="invalid-flags")bad.options=(WingUpgradeOptions)8;if(kind=="negative-index")bad.partIndex=-1;
   bool rejected=false;using(var s=new MemoryStream()){s.WriteByte(13);try{WingUpgradeSaveCodec.Append(s,kind=="duplicate"?new[]{good,bad}:new[]{bad});}catch(InvalidDataException){rejected=true;}Check(rejected&&s.Length==1,"Reject invalid record before changing native stream: "+kind);}
  }
 }
}

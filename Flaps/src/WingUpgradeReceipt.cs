using System.Collections.Generic;
using System.Globalization;
using System.IO;

internal static class WingUpgradeReceipt
{
    internal static string Read(Stream stream)
    {
        List<WingUpgradeSaveCodec.Record> records;
        if(!WingUpgradeSaveCodec.TryRead(stream,out records) || records.Count==0)return null;
        int flaps=0,spoilers=0;
        double flapValue=0,spoilerValue=0;
        foreach(var record in records){
            float unit=WingUpgradeEconomy.UnitPrice(record.basePrice);
            if((record.options&WingUpgradeOptions.Flaps)!=0){flaps++;flapValue+=unit;}
            if((record.options&WingUpgradeOptions.Spoilers)!=0){spoilers++;spoilerValue+=unit;}
        }
        string result="Saved wing upgrades";
        if(flaps>0)result+="\nFlaps ("+flaps+"):  "+Amount(flapValue);
        if(spoilers>0)result+="\nSpoilers ("+spoilers+"):  "+Amount(spoilerValue);
        return result+"\nUpgrade value:  "+Amount(flapValue+spoilerValue);
    }
    static string Amount(double value){return "$"+value.ToString("#,0.##",CultureInfo.CurrentCulture);}
}

using System;
using System.IO;
using System.Collections.Generic;

// Optional EOF extension: the native bytes remain an unchanged prefix.
// Reads restore the caller's cursor, including campaign fields after the plane.
internal static class WingUpgradeSaveCodec
{
    const ulong Magic=0x3130444F4D504C46UL; // FLPMOD01
    const int Version=1,RecordSize=14,MaxRecords=100000;
    internal struct Record {
        internal int partIndex,wingIndex;
        internal byte wingType; // 1 = Big Wing; 2 = Straight Wing
        internal float basePrice;
        internal WingUpgradeOptions options;
    }
    static void Validate(Record record,HashSet<string> seen)
    {
        if(record.partIndex<0 || record.wingIndex<0 || record.wingType<1 || record.wingType>8 ||
           record.options==WingUpgradeOptions.None || (record.options&~(WingUpgradeOptions.Flaps|WingUpgradeOptions.Spoilers))!=0 ||
           (record.wingType!=1 && (record.options&WingUpgradeOptions.Spoilers)!=0) ||
           !WingUpgradeEconomy.Finite(record.basePrice) || record.basePrice<0f ||
           !WingUpgradeEconomy.Finite(WingUpgradeEconomy.TotalPrice(record.basePrice,record.options)) ||
           !seen.Add(record.partIndex+":"+record.wingIndex))
            throw new InvalidDataException("Invalid Flap Mod upgrade record");
    }
    internal static void Append(Stream destination,IList<Record> records)
    {
        if(records.Count==0)return;
        if(records.Count>MaxRecords)throw new InvalidDataException("Too many upgrade records");
        var seen=new HashSet<string>();
        using(var payload=new MemoryStream()){
            var writer=new BinaryWriter(payload);
            writer.Write(Version);writer.Write(records.Count);
            foreach(var record in records){
                Validate(record,seen);
                writer.Write(record.partIndex);writer.Write(record.wingIndex);writer.Write(record.wingType);
                writer.Write(record.basePrice);writer.Write((byte)record.options);
            }
            writer.Flush();destination.Position=destination.Length;payload.Position=0;payload.CopyTo(destination);
            var footer=new BinaryWriter(destination);footer.Write((int)payload.Length);footer.Write(Magic);footer.Flush();
        }
    }
    internal static bool TryRead(Stream source,out List<Record> records)
    {
        records=new List<Record>();
        if(!source.CanRead || !source.CanSeek || source.Length<12)return false;
        long position=source.Position;
        try{
            source.Position=source.Length-12;
            var reader=new BinaryReader(source);int length=reader.ReadInt32();
            if(reader.ReadUInt64()!=Magic)return false;
            if(length<8 || length>8+MaxRecords*RecordSize || length>source.Length-12)
                throw new InvalidDataException("Invalid Flap Mod extension length");
            source.Position=source.Length-12-length;
            if(reader.ReadInt32()!=Version)throw new InvalidDataException("Unsupported Flap Mod save version");
            int count=reader.ReadInt32();
            if(count<0 || count>MaxRecords || length!=8+count*RecordSize)throw new InvalidDataException("Invalid Flap Mod record count");
            var seen=new HashSet<string>();
            for(int i=0;i<count;i++){
                var record=new Record{partIndex=reader.ReadInt32(),wingIndex=reader.ReadInt32(),wingType=reader.ReadByte(),basePrice=reader.ReadSingle(),options=(WingUpgradeOptions)reader.ReadByte()};
                Validate(record,seen);records.Add(record);
            }
            return true;
        }finally{source.Position=position;}
    }
}

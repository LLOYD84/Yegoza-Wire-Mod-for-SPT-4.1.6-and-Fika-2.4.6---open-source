using Fika.Core.Networking.LiteNetLib.Utils;

namespace YegozaWire.Net
{
    /// <summary>Установка (Action=0) или удаление (Action=1) проволоки между точками A и B.</summary>
    public struct WirePacket : INetSerializable
    {
        public byte Action;
        public string Id;
        public float Ax, Ay, Az;
        public float Bx, By, Bz;

        public void Serialize(NetDataWriter w)
        {
            w.Put(Action);
            w.Put(Id ?? "");
            w.Put(Ax); w.Put(Ay); w.Put(Az);
            w.Put(Bx); w.Put(By); w.Put(Bz);
        }

        public void Deserialize(NetDataReader r)
        {
            Action = r.GetByte();
            Id = r.GetString();
            Ax = r.GetFloat(); Ay = r.GetFloat(); Az = r.GetFloat();
            Bx = r.GetFloat(); By = r.GetFloat(); Bz = r.GetFloat();
        }
    }

    /// <summary>Клиент просит хоста прислать всю уже стоящую проволоку (вошёл в рейд позже).</summary>
    public struct WireSyncRequestPacket : INetSerializable
    {
        public byte Dummy;
        public void Serialize(NetDataWriter w) { w.Put(Dummy); }
        public void Deserialize(NetDataReader r) { Dummy = r.GetByte(); }
    }
}

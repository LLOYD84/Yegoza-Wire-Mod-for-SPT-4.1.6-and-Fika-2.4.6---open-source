using Comfort.Common;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using UnityEngine;

namespace YegozaWire.Net
{
    /// <summary>
    /// Синхронизация через Fika. Схема: любой игрок -> хост -> все остальные.
    /// Пакеты идемпотентны (повторная установка/удаление с тем же id игнорируется),
    /// поэтому дублирование при ретрансляции безопасно.
    /// Если не собирается из-за namespace Fika - поправить using'и сверху (см. исходники Fika.Core).
    /// </summary>
    internal sealed class FikaBackend : INetBackend
    {
        private readonly WireWorld _world;

        public FikaBackend(WireWorld world)
        {
            _world = world;
            FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnManagerCreated);
        }

        public bool IsBotAuthority
        {
            get
            {
                var m = Singleton<IFikaNetworkManager>.Instance;
                return m == null || m is FikaServer;
            }
        }

        private void OnManagerCreated(FikaNetworkManagerCreatedEvent e)
        {
            var m = e.Manager;
            if (m is FikaServer)
            {
                m.RegisterPacket<WirePacket, NetPeer>(ServerOnWire);
                m.RegisterPacket<WireSyncRequestPacket, NetPeer>(ServerOnSyncRequest);
            }
            else
            {
                m.RegisterPacket<WirePacket>(ClientOnWire);
                m.RegisterPacket<WireSyncRequestPacket>(_ => { });
            }
        }

        // ---- приём ----
        private void Apply(WirePacket p)
        {
            if (p.Action == 0)
                _world.ApplyPlace(p.Id, new Vector3(p.Ax, p.Ay, p.Az), new Vector3(p.Bx, p.By, p.Bz));
            else
                _world.ApplyRemove(p.Id);
        }

        private void ClientOnWire(WirePacket p) { Apply(p); }

        private void ServerOnWire(WirePacket p, NetPeer peer)
        {
            Apply(p);
            var copy = p;
            Singleton<IFikaNetworkManager>.Instance.SendData(ref copy, DeliveryMethod.ReliableOrdered, true);
        }

        private void ServerOnSyncRequest(WireSyncRequestPacket req, NetPeer peer)
        {
            var m = Singleton<IFikaNetworkManager>.Instance;
            foreach (var w in _world.Wires.Values)
            {
                var pk = MakePlace(w.Id, w.A, w.B);
                m.SendDataToPeer(ref pk, DeliveryMethod.ReliableOrdered, peer);
            }
        }

        // ---- отправка ----
        public void OnRaidStart()
        {
            var m = Singleton<IFikaNetworkManager>.Instance;
            if (m is FikaClient)
            {
                var req = new WireSyncRequestPacket();
                m.SendData(ref req, DeliveryMethod.ReliableOrdered, true);
            }
        }

        public void SendPlace(string id, Vector3 a, Vector3 b)
        {
            var m = Singleton<IFikaNetworkManager>.Instance;
            if (m == null) return;
            var pk = MakePlace(id, a, b);
            m.SendData(ref pk, DeliveryMethod.ReliableOrdered, true);
        }

        public void SendRemove(string id)
        {
            var m = Singleton<IFikaNetworkManager>.Instance;
            if (m == null) return;
            var pk = new WirePacket { Action = 1, Id = id };
            m.SendData(ref pk, DeliveryMethod.ReliableOrdered, true);
        }

        private static WirePacket MakePlace(string id, Vector3 a, Vector3 b) => new WirePacket
        {
            Action = 0, Id = id,
            Ax = a.x, Ay = a.y, Az = a.z,
            Bx = b.x, By = b.y, Bz = b.z
        };
    }
}

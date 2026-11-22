using UnityEngine;

namespace YegozaWire.Net
{
    /// <summary>Сетевой слой без привязки к Fika, чтобы мод работал и в обычном SPT.</summary>
    internal interface INetBackend
    {
        /// <summary>true - эта машина управляет ботами (хост Fika или одиночная игра).</summary>
        bool IsBotAuthority { get; }
        void OnRaidStart();
        void SendPlace(string id, Vector3 a, Vector3 b);
        void SendRemove(string id);
    }

    internal sealed class OfflineBackend : INetBackend
    {
        public bool IsBotAuthority => true;
        public void OnRaidStart() { }
        public void SendPlace(string id, Vector3 a, Vector3 b) { }
        public void SendRemove(string id) { }
    }
}

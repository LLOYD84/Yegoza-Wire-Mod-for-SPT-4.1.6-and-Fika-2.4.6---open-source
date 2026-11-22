using System.Collections.Generic;
using EFT;
using UnityEngine;
using YegozaWire.Net;

namespace YegozaWire
{
    /// <summary>Проволока, натянутая между точками A и B (как растяжка).</summary>
    internal sealed class Wire
    {
        public string Id;
        public Vector3 A, B;
        public GameObject Go;

        public Vector3 Center => (A + B) * 0.5f;
        public float Length => Vector3.Distance(A, B);
        public Quaternion Rot => Frame(A, B);

        /// <summary>Система координат проволоки: X вдоль A-B, Y вверх, Z поперёк.</summary>
        public static Quaternion Frame(Vector3 a, Vector3 b)
        {
            var x = b - a;
            if (x.sqrMagnitude < 1e-4f) return Quaternion.identity;
            x.Normalize();
            var y = Vector3.up - x * Vector3.Dot(Vector3.up, x);
            if (y.sqrMagnitude < 1e-4f) y = Vector3.forward; // вертикальная линия - не должна случаться
            y.Normalize();
            return Quaternion.LookRotation(Vector3.Cross(x, y), y);
        }

        private Vector3 ToLocal(Vector3 p) => Quaternion.Inverse(Rot) * (p - Center);

        public bool Contains(Vector3 p, float r)
        {
            var l = ToLocal(p);
            return Mathf.Abs(l.x) <= Length * 0.5f + r
                && Mathf.Abs(l.z) <= Cfg.DepthM.Value * 0.5f + r
                && l.y >= -0.3f && l.y <= Cfg.HeightM.Value + 0.3f;
        }

        private static readonly float[] BodyHeights = { 0.1f, 0.6f, 1.1f, 1.6f };

        /// <summary>Проверка по всему телу (ноги, живот, грудь, голова), а не только по ногам:
        /// работает для проволоки, поставленной на высоте, наклонно или подвешенной.</summary>
        public bool ContainsBody(Vector3 feet, float r, float scale)
        {
            for (int i = 0; i < BodyHeights.Length; i++)
                if (Contains(feet + Vector3.up * (BodyHeights[i] * scale), r)) return true;
            return false;
        }

        public float Distance(Vector3 p)
        {
            var l = ToLocal(p);
            var c = new Vector3(
                Mathf.Clamp(l.x, -Length * 0.5f, Length * 0.5f),
                Mathf.Clamp(l.y, 0, Cfg.HeightM.Value),
                Mathf.Clamp(l.z, -Cfg.DepthM.Value * 0.5f, Cfg.DepthM.Value * 0.5f));
            return Vector3.Distance(l, c);
        }
    }

    /// <summary>Реестр расставленной проволоки + её эффекты (замедление/урон). Выполняется на каждой машине локально.</summary>
    internal sealed class WireWorld
    {
        public INetBackend Net;
        public readonly Dictionary<string, Wire> Wires = new Dictionary<string, Wire>();

        private GameWorld _last;
        private int _enterLogs;
        private float _nextTick, _prevTick;
        private readonly Dictionary<Player, bool> _slowed = new Dictionary<Player, bool>();
        private readonly Dictionary<Player, float> _nextHit = new Dictionary<Player, float>();
        private readonly Dictionary<Player, float> _lastSpeed = new Dictionary<Player, float>();
        private readonly Dictionary<Player, Vector3> _lastPos = new Dictionary<Player, Vector3>();

        // ---- изменение мира (идемпотентно: повторный пакет ничего не ломает) ----
        public bool ApplyPlace(string id, Vector3 a, Vector3 b)
        {
            if (Wires.ContainsKey(id)) return false;
            var w = new Wire { Id = id, A = a, B = b };
            w.Go = WireVisual.Build(w.Center, w.Rot, w.Length);
            Wires[id] = w;
            return true;
        }

        public bool ApplyRemove(string id)
        {
            if (!Wires.TryGetValue(id, out var w)) return false;
            if (w.Go != null) Object.Destroy(w.Go);
            Wires.Remove(id);
            return true;
        }

        public void Clear()
        {
            foreach (var w in Wires.Values) if (w.Go != null) Object.Destroy(w.Go);
            Wires.Clear();
            _slowed.Clear(); _nextHit.Clear(); _lastPos.Clear(); _lastSpeed.Clear();
            WireVisual.ResetCache();
        }

        public Wire Nearest(Vector3 p, float maxDist)
        {
            Wire best = null; float bd = maxDist;
            foreach (var w in Wires.Values)
            {
                float d = w.Distance(p);
                if (d <= bd) { bd = d; best = w; }
            }
            return best;
        }

        // ---- тени: у дальних проволок отключаем, чтобы не грузить видеокарту ----
        private float _nextShadow;
        private readonly List<KeyValuePair<float, Wire>> _sorted = new List<KeyValuePair<float, Wire>>();

        private void UpdateShadows(Player me)
        {
            if (Time.time < _nextShadow || Wires.Count == 0) return;
            _nextShadow = Time.time + 1f;
            string mode = Cfg.ShadowMode.Value;
            _sorted.Clear();
            foreach (var w in Wires.Values) _sorted.Add(new KeyValuePair<float, Wire>(w.Distance(me.Position), w));
            _sorted.Sort((a, b) => a.Key.CompareTo(b.Key));
            for (int i = 0; i < _sorted.Count; i++)
            {
                var w = _sorted[i].Value;
                if (w.Go == null) continue;
                bool on = mode == "Always" || (mode == "Near" && i < Cfg.ShadowMax.Value && _sorted[i].Key <= Cfg.ShadowDist.Value);
                var mode2 = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (var r in w.Go.GetComponentsInChildren<MeshRenderer>())
                    if (r.shadowCastingMode != mode2) r.shadowCastingMode = mode2;
            }
        }

        // ---- эффекты ----
        public void Tick(GameWorld world, Player me)
        {
            if (world != _last)
            {
                Clear();
                _last = world;
                Net.OnRaidStart();
            }
            UpdateShadows(me);
            if (Time.time < _nextTick) return;
            float dt = Time.time - _prevTick;
            _prevTick = Time.time;
            _nextTick = Time.time + 0.033f;
            if (dt <= 0f || dt > 1f) dt = 0.033f;
            if (dt < 0.02f) dt = 0.02f;
            if (Wires.Count == 0 && _slowed.Count == 0) return;

            var list = world.AllAlivePlayersList;
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p == null) continue;
                bool isMe = p == me;
                bool bot = GameApi.IsBot(p);
                // Каждая машина обрабатывает: себя; ботов - только хост (боты живут на нём)
                if (!isMe && !(bot && Cfg.AffectBots.Value && Net.IsBotAuthority)) continue;

                var pos = p.Position;
                float speed = 0f;
                if (_lastPos.TryGetValue(p, out var lp)) speed = Vector3.Distance(pos, lp) / dt;
                _lastPos[p] = pos;

                // при быстром беге захватываем с запасом по скорости, чтобы не проскочить между тиками
                _lastSpeed.TryGetValue(p, out float prevSpeed);
                _lastSpeed[p] = speed * 0.5f + prevSpeed * 0.5f;
                float margin = 0.25f + Mathf.Min(_lastSpeed[p], 8f) * 0.1f;
                bool inside = false;
                float scale = 1f;
                if (!bot && p.Transform != null && p.Transform.Original != null)
                    scale = Mathf.Clamp(p.Transform.Original.lossyScale.y, 0.5f, 3f);
                foreach (var w in Wires.Values) if (w.ContainsBody(pos, margin, scale)) { inside = true; break; }

                _slowed.TryGetValue(p, out bool wasSlowed);
                if (inside != wasSlowed)
                {
                    GameApi.SetSlow(p, inside, Cfg.SlowSpeed.Value);
                    _slowed[p] = inside;
                    if (inside)
                    {
                        WireSound.Play(pos);
                        if (_enterLogs++ < 5) Plugin.Log.LogInfo("Enter wire: " + (bot ? "bot" : "player") + " speed=" + speed.ToString("0.0"));
                    }
                }
                else if (inside) GameApi.SetSlow(p, true, Cfg.SlowSpeed.Value); // переустанавливаем, если другой мод сбросил

                if (inside && speed > 0.25f)
                {
                    _nextHit.TryGetValue(p, out float next);
                    if (Time.time >= next)
                    {
                        GameApi.Hurt(p, Cfg.Damage.Value);
                        if (UnityEngine.Random.value < Cfg.BleedChance.Value) GameApi.Bleed(p);
                        WireSound.Play(pos);
                        _nextHit[p] = Time.time + Cfg.DamageInterval.Value;
                    }
                }
            }
        }
    }
}

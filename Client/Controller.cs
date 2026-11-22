using System;
using EFT;
using UnityEngine;

namespace YegozaWire
{
    /// <summary>
    /// Ввод игрока.
    ///   Y (нажать) - режим "призрака": Y ставит начало, Y ставит конец, затем ~5 секунд развёртывания.
    ///   U (нажать) - отмена в режиме установки.
    ///   U (удерживать рядом с проволокой) - забрать.
    /// </summary>
    internal sealed class Controller : MonoBehaviour
    {
        private enum Mode { None, Aim, Deploy, Pickup }

        private const float MinLength = 1f;

        private WireWorld _world;
        private Mode _mode;
        private int _step;                 // 0 - выбираем начало, 1 - выбираем конец
        private Vector3 _a, _b;
        private bool _valid;
        private Vector3 _startPos;
        private float _t, _duration;
        private Wire _target;
        private GameObject _ghost;
        private string _msg = "";
        private float _msgUntil;

        public void Init(WireWorld w) { _world = w; }

        private void Update()
        {
            var world = GameApi.World;
            var me = world != null ? world.MainPlayer : null;
            if (world == null || me == null || me.HealthController == null || !me.HealthController.IsAlive)
            {
                if (_mode != Mode.None) Reset();
                return;
            }

            _world.Tick(world, me);

            if (_mode == Mode.None)
            {
                if (Cursor.lockState != CursorLockMode.Locked) return; // открыт инвентарь/меню
                if (Input.GetKeyDown(Cfg.DeployKey.Value)) StartAim(me);
                else if (Input.GetKeyDown(Cfg.PickupKey.Value)) StartPickup(me);
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked) { Cancel(Loc.T("cancelled")); return; }

            if (_mode == Mode.Aim) UpdateAim(me);
            else UpdateTimer(me);
        }

        // ---------- выбор точек ----------
        private void StartAim(Player me)
        {
            if (_world.Wires.Count >= Cfg.MaxWires.Value) { Say(Loc.T("limit")); return; }
            if (GameApi.FindItem(me, Cfg.ItemTpl) == null) { Say(Loc.T("noKit")); return; }
            _mode = Mode.Aim;
            _step = 0;
            _valid = false;
            _ghost = WireVisual.BuildGhost();
        }

        private void UpdateAim(Player me)
        {
            if (Input.GetKeyDown(Cfg.PickupKey.Value)) { Cancel(Loc.T("cancelled")); return; }

            Vector3 right = Vector3.ProjectOnPlane(me.Transform.Original.right, Vector3.up).normalized;
            Vector3 hitP, hitN;
            bool hit = AimHit(me, _step == 0 ? 4f : Mathf.Max(9f, Cfg.LengthM.Value + 3f), out hitP, out hitN);
            bool ok = hit && hitN.y > 0.5f;

            if (_step == 0)
            {
                _a = ok ? hitP : me.Position;
                _b = _a + right * 0.8f;
                _valid = ok;
            }
            else
            {
                float max = Cfg.LengthM.Value;
                var end = ok ? hitP : _a + right * 0.8f;
                var d = end - _a;
                if (ok && d.magnitude > max)
                {
                    end = _a + d.normalized * max;
                    ok = GroundAt(end, out end);
                }
                _b = end;
                _valid = ok && Vector3.Distance(_a, _b) >= MinLength
                        && Vector3.Distance(me.Position, _a) <= Mathf.Max(6f, Cfg.LengthM.Value + 1f);
            }

            WireVisual.SetGhost(_ghost, _a, _b, _valid);

            if (Input.GetKeyDown(Cfg.DeployKey.Value))
            {
                if (!_valid) { Say(Loc.T("cannotHere")); return; }
                if (_step == 0) { _step = 1; return; }

                // обе точки выбраны - начинаем развёртывание
                _mode = Mode.Deploy;
                _t = 0;
                _duration = Cfg.DeploySeconds.Value;
                _startPos = me.Position;
            }
        }

        // ---------- таймеры (развёртывание / сворачивание) ----------
        private void UpdateTimer(Player me)
        {
            if (_mode == Mode.Deploy && Input.GetKeyDown(Cfg.PickupKey.Value)) { Cancel(Loc.T("cancelled")); return; }
            if (_mode == Mode.Pickup && !Input.GetKey(Cfg.PickupKey.Value)) { Cancel(Loc.T("interrupted")); return; }
            if (Vector3.Distance(me.Position, _startPos) > 1f) { Cancel(Loc.T("moved")); return; }

            _t += Time.deltaTime;
            if (_t < _duration) return;

            if (_mode == Mode.Deploy) FinishDeploy(me); else FinishPickup(me);
            Reset();
        }

        private void FinishDeploy(Player me)
        {
            var item = GameApi.FindItem(me, Cfg.ItemTpl);
            if (item == null) { Say(Loc.T("noKit")); return; }
            if (!GameApi.Consume(me, item)) { Say(Loc.T("useFail")); return; }

            string id = Guid.NewGuid().ToString("N");
            _world.ApplyPlace(id, _a, _b);
            _world.Net.SendPlace(id, _a, _b);
            Say(Loc.T("deployed"));
        }

        private void StartPickup(Player me)
        {
            _target = _world.Nearest(me.Position, 4f);
            if (_target == null) { Say(Loc.T("noWireNear")); return; }
            _mode = Mode.Pickup; _t = 0; _duration = Cfg.PickupSeconds.Value; _startPos = me.Position;
        }

        private void FinishPickup(Player me)
        {
            if (_target == null || !_world.Wires.ContainsKey(_target.Id)) { Say(Loc.T("alreadyGone")); return; }
            if (!GameApi.Give(me, Cfg.ItemTpl)) { Say(Loc.T("packFail")); return; }
            string id = _target.Id;
            _world.ApplyRemove(id);
            _world.Net.SendRemove(id);
            Say(Loc.T("packed"));
        }

        private void Cancel(string msg) { Reset(); Say(msg); }

        private void Reset()
        {
            _mode = Mode.None;
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }
        }

        // ---------- лучи ----------
        private static bool AimHit(Player me, float range, out Vector3 point, out Vector3 normal)
        {
            point = default; normal = Vector3.up;
            var cam = Camera.main;
            Vector3 o = cam != null ? cam.transform.position : me.Position + Vector3.up * 1.6f;
            Vector3 d = cam != null ? cam.transform.forward : me.Transform.Original.forward;
            if (!Cast(o, d, range, out var h)) return false;
            point = h.point + h.normal * 0.02f;
            normal = h.normal;
            return true;
        }

        private static bool GroundAt(Vector3 p, out Vector3 ground)
        {
            ground = p;
            if (!Cast(p + Vector3.up * 2f, Vector3.down, 4f, out var h) || h.normal.y <= 0.5f) return false;
            ground = h.point + h.normal * 0.02f;
            return true;
        }

        private static bool Cast(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<Player>() != null) continue;
                best = h;
                return true;
            }
            return false;
        }

        // ---------- HUD ----------
        private void Say(string s) { _msg = s; _msgUntil = Time.time + 3f; }

        private void OnGUI()
        {
            float w = 380f, h = 18f;
            float x = (Screen.width - w) * 0.5f, y = Screen.height * 0.72f;

            if (_mode == Mode.Aim)
            {
                string hint = _step == 0
                    ? string.Format(Loc.T("hintStart"), Cfg.DeployKey.Value, Cfg.PickupKey.Value)
                    : string.Format(Loc.T("hintEnd"), Cfg.LengthM.Value.ToString("0.#"), Cfg.DeployKey.Value, Cfg.PickupKey.Value);
                GUI.Label(new Rect(x, y, w, 22f), hint);
            }
            else if (_mode == Mode.Deploy || _mode == Mode.Pickup)
            {
                var label = _mode == Mode.Deploy ? Loc.T("deploying") : Loc.T("packing");
                GUI.Label(new Rect(x, y - 24f, w, 22f), label);
                GUI.color = new Color(0, 0, 0, 0.6f);
                GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
                GUI.color = new Color(0.9f, 0.75f, 0.2f, 0.95f);
                GUI.DrawTexture(new Rect(x + 2, y + 2, (w - 4) * Mathf.Clamp01(_t / _duration), h - 4), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else if (Time.time < _msgUntil)
            {
                GUI.Label(new Rect(x, y, w, 22f), _msg);
            }
        }
    }
}

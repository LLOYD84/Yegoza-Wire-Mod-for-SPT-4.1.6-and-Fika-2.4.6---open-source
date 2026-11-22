using System.Collections.Generic;
using UnityEngine;

namespace YegozaWire
{
    /// <summary>
    /// Внешний вид расставленной проволоки. Сначала пробуем клонировать ванильную колючку с карты
    /// (объект с "barbed"/"razor" в имени), если такой на карте нет - строим спираль процедурно.
    /// </summary>
    internal static class WireVisual
    {
        private static GameObject _vanillaSource;
        private static bool _searched;
        private static Material _fallbackMat;

        public static void ResetCache() { _vanillaSource = null; _searched = false; }

        public static GameObject Build(Vector3 pos, Quaternion rot, float L)
        {
            var root = new GameObject("YegozaWire");
            float H = Cfg.HeightM.Value, D = Cfg.DepthM.Value;

            bool ok = false;
            try { ok = TryVanilla(root, L, H, D); }
            catch (System.Exception e) { Plugin.Log.LogWarning("Vanilla wire clone failed: " + e.Message); }
            if (!ok) BuildCoil(root, L, H);
            Plugin.Log.LogInfo("Wire built: " + (ok ? "vanilla" : "procedural") + " L=" + L.ToString("0.0") + " at " + pos);

            root.transform.SetPositionAndRotation(pos, rot);
            return root;
        }


        // ---------- сложенная егоза на земле (подмена модели выброшенного предмета) ----------
        public static void DecorateLoot(GameObject loot)
        {
            if (loot.GetComponent<LootDecorator>() == null) loot.AddComponent<LootDecorator>();
        }

        /// <summary>
        /// Следит за выброшенной егозой: прячет ванильную модель (она может подгрузиться позже), ставит свёрнутую колючку
        /// и подгоняет её по земле, чтобы она не "плавала" и не уходила в пол.
        /// </summary>
        private sealed class LootDecorator : MonoBehaviour
        {
            private float _next;
            private Transform _vis;
            private MeshRenderer _visRenderer;

            private void Update()
            {
                if (Time.time < _next) return;
                _next = Time.time + 0.3f;

                if (_vis == null)
                {
                    if (GetComponentsInChildren<Renderer>(true).Length == 0) return; // модель ещё не загрузилась
                    var vis = new GameObject("YegozaFolded");
                    vis.transform.SetParent(transform, false);
                    vis.layer = gameObject.layer;
                    vis.AddComponent<MeshFilter>().sharedMesh = CoilMesh(0.4f, 0.2f, 0.006f, 8, 10);
                    _visRenderer = vis.AddComponent<MeshRenderer>();
                    _visRenderer.sharedMaterial = FallbackMaterial();
                    _vis = vis.transform;

                    var col = new GameObject("YegozaPickup");
                    col.transform.SetParent(transform, false);
                    col.layer = gameObject.layer;
                    var bc = col.AddComponent<BoxCollider>();
                    bc.size = new Vector3(0.45f, 0.4f, 0.45f);
                    bc.center = new Vector3(0, 0.1f, 0);
                }

                // прячем всё чужое (в т.ч. то, что подгрузилось позже)
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    if (r != _visRenderer && r.enabled && r.transform != _vis) r.enabled = false;

                // подгоняем модель по земле
                var hits = Physics.RaycastAll(transform.position + Vector3.up * 0.3f, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue; bool found = false; float gy = 0f;
                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(transform)) continue;
                    if (h.distance < best) { best = h.distance; gy = h.point.y; found = true; }
                }
                if (found)
                {
                    float bottom = _visRenderer.bounds.min.y;
                    _vis.position += new Vector3(0, gy + 0.01f - bottom, 0);
                }
                else _vis.localPosition = new Vector3(0, -0.1f, 0);
            }
        }

        // ---------- "призрак" перед установкой ----------
        public static GameObject BuildGhost()
        {
            var go = new GameObject("YegozaWireGhost");
            var coil = new GameObject("coil");
            coil.transform.SetParent(go.transform, false);
            coil.AddComponent<MeshFilter>().sharedMesh = CoilMesh(1f, Cfg.HeightM.Value * 0.5f, 0.012f, 7, 10);
            var mr = coil.AddComponent<MeshRenderer>();
            mr.material = new Material(FallbackMaterial());
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Растягивает призрак между A и B. Зелёный - можно ставить, красный - нельзя.</summary>
        public static void SetGhost(GameObject g, Vector3 a, Vector3 b, bool valid)
        {
            if (g == null) return;
            float len = Mathf.Max(Vector3.Distance(a, b), 0.3f);
            g.transform.SetPositionAndRotation((a + b) * 0.5f, Wire.Frame(a, b));
            g.transform.localScale = new Vector3(len, 1f, 1f);
            var r = g.GetComponentInChildren<Renderer>();
            if (r != null) r.material.color = valid ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.2f, 0.2f);
        }

        // ---------- ванильная модель ----------
        private static readonly string[] Bad =
            { "scope", "vortex", "weapon", "tripwire", "mod_", "sight", "handguard", "muzzle", "magazine", "item", "icon" };

        private static bool NameOk(string n)
        {
            n = n.ToLowerInvariant();
            if (!(n.Contains("barb") || n.Contains("razor"))) return false;
            foreach (var b in Bad) if (n.Contains(b)) return false;
            return true;
        }

        private static GameObject FindVanilla()
        {
            if (_searched) return _vanillaSource;
            _searched = true;
            if (!Cfg.UseVanilla.Value) return null;
            var seen = new HashSet<string>();
            foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (r == null || !NameOk(r.gameObject.name)) continue;
                if (r.GetComponentInParent<EFT.Player>() != null) continue;
                var t = r.transform;
                while (t.parent != null && NameOk(t.parent.name)) t = t.parent;
                var go = t.gameObject;
                var bs = GetBounds(go);
                float big = Mathf.Max(bs.size.x, Mathf.Max(bs.size.y, bs.size.z));
                if (seen.Add(go.name))
                    Plugin.Log.LogInfo("Wire model candidate: " + go.name + " size=" + bs.size);
                if (_vanillaSource == null && big >= 1.2f && big <= 30f)
                {
                    _vanillaSource = go;
                    Plugin.Log.LogInfo("Vanilla barbed wire model chosen: " + go.name);
                }
            }
            return _vanillaSource;
        }

        private static bool TryVanilla(GameObject root, float L, float H, float D)
        {
            var src = FindVanilla();
            if (src == null) return false;

            var holder = new GameObject("holder");
            holder.transform.SetParent(root.transform, false);
            var go = Object.Instantiate(src, holder.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.SetActive(true);

            // убираем скрипты (чтобы не дублировать ванильный урон) и коллайдеры
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(mb);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);

            var b = GetBounds(go);
            if (b.size.sqrMagnitude < 0.0001f) { Object.Destroy(holder); return false; }
            if (b.size.z > b.size.x) { go.transform.localRotation = Quaternion.Euler(0, 90, 0); b = GetBounds(go); }

            holder.transform.localScale = new Vector3(
                L / Mathf.Max(b.size.x, 0.01f),
                H / Mathf.Max(b.size.y, 0.01f),
                Mathf.Clamp(D / Mathf.Max(b.size.z, 0.01f), 0.5f, 3f));

            b = GetBounds(go);
            go.transform.position += root.transform.position - new Vector3(b.center.x, b.min.y, b.center.z);
            return true;
        }

        private static Bounds GetBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds();
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // ---------- процедурная спираль ----------
        private static void BuildCoil(GameObject root, float L, float H)
        {
            var go = new GameObject("coil");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = CoilMesh(L, H * 0.5f, 0.006f, Mathf.Max(6, Mathf.RoundToInt(L / 0.14f)), 10);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = FallbackMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        private static Mesh CoilMesh(float length, float radius, float tube, int turns, int perTurn)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            // несколько неровных витков разного шага - "клубок", как у ванильной егозы
            var rnd = new System.Random(11);
            AddHelix(verts, tris, length, radius, tube, turns, perTurn, 0f, true, rnd);
            AddHelix(verts, tris, length, radius * 0.9f, tube, Mathf.Max(3, (int)(turns * 0.7f)), perTurn, 2.1f, true, rnd);
            AddHelix(verts, tris, length, radius * 0.8f, tube, (int)(turns * 1.35f), perTurn, 4.2f, false, rnd);
            AddLine(verts, tris, length, 0.03f, tube * 1.2f);

            var m = new Mesh { name = "YegozaCoil" };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static void AddRing(List<Vector3> verts, Vector3 c, float tube, int sides)
        {
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides;
                verts.Add(c + new Vector3(0, Mathf.Cos(a) * tube, Mathf.Sin(a) * tube));
            }
        }

        private static void Strip(List<int> tris, int start, int count, int sides)
        {
            for (int i = 0; i < count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + i * sides + s, b = start + i * sides + (s + 1) % sides;
                    int c = start + (i + 1) * sides + s, d = start + (i + 1) * sides + (s + 1) % sides;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
        }

        private static void AddHelix(List<Vector3> verts, List<int> tris, float length, float radius, float tube,
                                     int turns, int perTurn, float phase, bool barbs, System.Random rnd)
        {
            const int sides = 4;
            int n = turns * perTurn + 1;
            int start = verts.Count;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float ang = t * turns * Mathf.PI * 2f + phase;
                float wob = 0.75f + 0.25f * (float)rnd.NextDouble();
                var c = new Vector3((t - 0.5f) * length + (float)(rnd.NextDouble() - 0.5) * 0.02f,
                                    radius + Mathf.Sin(ang) * radius * wob + (float)(rnd.NextDouble() - 0.5) * 0.03f,
                                    Mathf.Cos(ang) * radius * wob + (float)(rnd.NextDouble() - 0.5) * 0.03f);
                AddRing(verts, c, tube, sides);
            }
            Strip(tris, start, n, sides);

            if (!barbs) return;
            // колючки: маленькие пирамидки наружу от оси витка
            for (int i = 0; i < n; i += 2)
            {
                float t = i / (float)(n - 1);
                float ang = t * turns * Mathf.PI * 2f + phase;
                var c = (verts[start + i * sides] + verts[start + i * sides + 2]) * 0.5f;
                var dir = new Vector3((float)(rnd.NextDouble() - 0.5) * 0.8f, Mathf.Sin(ang), Mathf.Cos(ang)).normalized;
                var side = Vector3.Cross(dir, Vector3.right).normalized;
                float w = tube * 1.6f, len = 0.05f + 0.05f * (float)rnd.NextDouble();
                int v0 = verts.Count;
                verts.Add(c + Vector3.right * w);
                verts.Add(c - Vector3.right * w);
                verts.Add(c + side * w);
                verts.Add(c - side * w);
                verts.Add(c + dir * len);
                tris.AddRange(new[] { v0, v0 + 2, v0 + 4, v0 + 2, v0 + 1, v0 + 4, v0 + 1, v0 + 3, v0 + 4, v0 + 3, v0, v0 + 4 });
                // обратная сторона, чтобы было видно при любом culling
                tris.AddRange(new[] { v0, v0 + 4, v0 + 2, v0 + 2, v0 + 4, v0 + 1, v0 + 1, v0 + 4, v0 + 3, v0 + 3, v0 + 4, v0 });
            }
        }

        private static void AddLine(List<Vector3> verts, List<int> tris, float length, float y, float tube)
        {
            const int sides = 4;
            int start = verts.Count;
            AddRing(verts, new Vector3(-length * 0.5f, y, 0), tube, sides);
            AddRing(verts, new Vector3(length * 0.5f, y, 0), tube, sides);
            Strip(tris, start, 2, sides);
        }

        private static Material FallbackMaterial()
        {
            if (_fallbackMat != null) return _fallbackMat;
            Shader sh = null;
            foreach (var name in new[] { "Standard", "Unlit/Color", "Sprites/Default", "Hidden/Internal-Colored" })
            {
                sh = Shader.Find(name);
                if (sh != null) break;
            }
            _fallbackMat = new Material(sh != null ? sh : Shader.Find("Diffuse"));
            _fallbackMat.color = new Color(0.22f, 0.2f, 0.19f);
            return _fallbackMat;
        }
    }
}

using UnityEngine;

namespace YegozaWire
{
    /// <summary>Лязг металла при входе в проволоку. Клип генерируется кодом (шум + звон с затуханием).</summary>
    internal static class WireSound
    {
        private static AudioClip _clip;
        private static readonly System.Collections.Generic.List<AudioClip> _clips = new System.Collections.Generic.List<AudioClip>();
        private static float _next;

        public static void Play(Vector3 pos)
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.35f;
            try
            {
                if (_clip == null) _clip = FindVanilla() ?? Make();
                var clip = _clips.Count > 0 ? _clips[Random.Range(0, _clips.Count)] : _clip;
                var go = new GameObject("YegozaWireSfx");
                go.transform.position = pos;
                var src = go.AddComponent<AudioSource>();
                src.clip = clip;
                src.spatialBlend = 1f;
                src.minDistance = 3f;
                src.maxDistance = 40f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.volume = 1f;
                src.pitch = Random.Range(0.9f, 1.15f);
                src.Play();
                Object.Destroy(go, clip.length + 0.2f);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Sound: " + e.Message); }
        }

        // Ищем уже загруженный в память ванильный звук колючки (в имени barb/wire)
        private static AudioClip FindVanilla()
        {
            AudioClip best = null;
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var c in Resources.FindObjectsOfTypeAll<AudioClip>())
            {
                if (c == null) continue;
                var n = c.name.ToLowerInvariant();
                if (n.Contains("tripwire") || n.Contains("grenade")) continue;
                if (n.Contains("barb") || n.Contains("wire") || n.Contains("fence"))
                {
                    if (seen.Add(n)) Plugin.Log.LogInfo("Audio candidate: " + c.name + " " + c.length.ToString("0.0") + "s");
                    if (n.Contains("barb") && c.length < 4f) { if (best == null) best = c; if (!_clips.Contains(c)) _clips.Add(c); }
                }
            }
            if (best != null) Plugin.Log.LogInfo("Using vanilla sound: " + best.name);
            return best;
        }

        private static AudioClip Make()
        {
            const int rate = 44100;
            int n = (int)(rate * 0.6f);
            var d = new float[n];
            var rnd = new System.Random(7);
            float[] freqs = { 1800f, 2650f, 3900f, 5200f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * 9f);
                float noise = (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 30f);
                float ring = 0f;
                for (int k = 0; k < freqs.Length; k++)
                    ring += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * (0.25f / (k + 1));
                // несколько "щелчков" подряд, как будто задевает колючку
                float gate = (t % 0.12f) < 0.05f ? 1f : 0.35f;
                d[i] = Mathf.Clamp((noise * 0.8f + ring * env) * gate * 0.8f, -1f, 1f);
            }
            var c = AudioClip.Create("YegozaWireRattle", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}

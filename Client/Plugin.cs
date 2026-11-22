using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;
using YegozaWire.Net;

namespace YegozaWire
{
    /// <summary>Все настройки (F12 в игре → YegozaWire). У хоста и друзей лучше держать одинаковые.</summary>
    internal static class Cfg
    {
        // Должен совпадать с ItemId в серверном моде
        public const string ItemTpl = "674f1a2b3c4d5e6f7a8b9c0d";

        public static ConfigEntry<KeyCode> DeployKey, PickupKey;
        public static ConfigEntry<float> DeploySeconds, PickupSeconds, LengthM, HeightM, DepthM;
        public static ConfigEntry<float> SlowSpeed, Damage, DamageInterval;
        public static ConfigEntry<int> MaxWires;
        public static ConfigEntry<bool> AffectBots, UseVanilla;
        public static ConfigEntry<float> BleedChance, ShadowDist;
        public static ConfigEntry<string> ShadowMode;
        public static ConfigEntry<int> ShadowMax;
        

        public static ConfigEntry<string> Language;

        public static void Bind(ConfigFile c)
        {
            Language = c.Bind("General", "Language", "En",
                new ConfigDescription("Interface language for on-screen messages / Язык сообщений на экране",
                    new AcceptableValueList<string>("En", "Ru")));
            DeployKey = c.Bind("Controls", "Deploy key", KeyCode.Y, "Press to start placing the wire; press again to set each point");
            PickupKey = c.Bind("Controls", "Pickup key", KeyCode.U, "Hold near a wire to pack it up; also cancels placing");
            DeploySeconds = c.Bind("Wire", "Deploy seconds", 5.5f, "Time needed to deploy a wire (stand still)");
            PickupSeconds = c.Bind("Wire", "Pickup seconds", 3f, "Time needed to pack a wire up");
            LengthM = c.Bind("Wire", "Max length (m)", 5f, "Maximum length of one wire (stretched between two points)");
            HeightM = c.Bind("Wire", "Height (m)", 0.9f, "Height of the wire coil");
            DepthM = c.Bind("Wire", "Depth (m)", 0.9f, "Width of the damage zone");
            MaxWires = c.Bind("Wire", "Max wires per raid", 30, "Limit on deployed wires per raid");
            UseVanilla = c.Bind("Wire", "Use vanilla model", false, "Try to reuse a barbed-wire model from the map; false = built-in model");
            SlowSpeed = c.Bind("Effects", "Slow factor", 0.2f, "Speed limit inside the wire as a fraction of max speed (0.1 = almost stopped, 1 = no slow)");
            Damage = c.Bind("Effects", "Damage per tick", 12f, "Damage dealt to a random limb per tick");
            DamageInterval = c.Bind("Effects", "Damage interval (s)", 0.5f, "Seconds between damage ticks while moving inside the wire");
            ShadowMode = c.Bind("Performance", "Wire shadows", "Near",
                new ConfigDescription("Always = all wires cast shadows; Near = only the closest ones; Off = no shadows",
                    new AcceptableValueList<string>("Always", "Near", "Off")));
            ShadowDist = c.Bind("Performance", "Shadow distance (m)", 20f, "Near mode: max distance at which a wire casts a shadow");
            ShadowMax = c.Bind("Performance", "Max shadowed wires", 3, "Near mode: how many of the closest wires cast shadows");
            BleedChance = c.Bind("Effects", "Bleed chance", 0.35f, "Chance of a light bleed on each damage tick (0-1)");
            AffectBots = c.Bind("Effects", "Affect bots", true, "Slow down and hurt bots as well");
        }
    }

    [BepInPlugin("com.yegoza.wire", "Yegoza Wire", "1.0.0")]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Cfg.Bind(Config);
            LootHook.Install();

            var world = new WireWorld();
            INetBackend backend = Chainloader.PluginInfos.ContainsKey("com.fika.core")
                ? CreateFikaBackend(world)
                : new OfflineBackend();
            world.Net = backend;

            gameObject.AddComponent<Controller>().Init(world);
            Log.LogInfo("Yegoza Wire loaded. Net backend: " + backend.GetType().Name);
        }

        // Отдельный метод, чтобы типы Fika не подгружались, когда Fika нет
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static INetBackend CreateFikaBackend(WireWorld world) => new FikaBackend(world);
    }
}

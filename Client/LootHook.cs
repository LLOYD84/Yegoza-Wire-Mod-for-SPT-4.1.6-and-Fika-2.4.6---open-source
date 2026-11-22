using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace YegozaWire
{
    /// <summary>
    /// Ловит появление любого предмета-лута с нашим шаблоном (выброс из инвентаря, сворачивание, синхронизация Fika)
    /// и подменяет его модель на свёрнутую колючку.
    /// </summary>
    internal static class LootHook
    {
        public static void Install()
        {
            try
            {
                var asm = typeof(EFT.Player).Assembly;
                var lootT = asm.GetType("EFT.Interactive.LootItem");
                var worldT = typeof(EFT.GameWorld);
                if (lootT == null) { Plugin.Log.LogWarning("LootItem не найден - подмена модели только для сворачивания"); return; }

                var harmony = new Harmony("com.yegoza.wire.loot");
                var post = new HarmonyMethod(typeof(LootHook).GetMethod("Post", BindingFlags.Static | BindingFlags.NonPublic));
                int n = 0;
                foreach (var t in new[] { lootT, worldT })
                {
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (!lootT.IsAssignableFrom(m.ReturnType) || m.IsGenericMethodDefinition || m.IsAbstract) continue;
                        var name = m.Name;
                        if (!(name.Contains("Throw") || name.Contains("Create") || name.Contains("Spawn") || name.Contains("Drop"))) continue;
                        try { harmony.Patch(m, postfix: post); n++; Plugin.Log.LogInfo("Loot hook: " + t.Name + "." + m); }
                        catch (Exception e) { Plugin.Log.LogWarning("Loot hook failed " + m + ": " + e.Message); }
                    }
                }
                Plugin.Log.LogInfo("Loot hooks installed: " + n);
            }
            catch (Exception e) { Plugin.Log.LogError("LootHook.Install: " + e); }
        }

        private static void Post(object __result)
        {
            try
            {
                var comp = __result as Component;
                if (comp == null) return;
                var item = comp.GetType().GetProperty("Item")?.GetValue(comp, null) as EFT.InventoryLogic.Item;
                if (item == null || item.TemplateId.ToString() != Cfg.ItemTpl) return;
                WireVisual.DecorateLoot(comp.gameObject);
            }
            catch (Exception e) { Plugin.Log.LogWarning("LootHook.Post: " + e.Message); }
        }
    }
}

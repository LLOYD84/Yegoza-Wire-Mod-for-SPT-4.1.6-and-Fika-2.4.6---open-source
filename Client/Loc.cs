using System.Collections.Generic;

namespace YegozaWire
{
    /// <summary>Тексты интерфейса. Язык выбирается в F12 (Language: En / Ru), по умолчанию английский.</summary>
    internal static class Loc
    {
        private static readonly Dictionary<string, string[]> D = new Dictionary<string, string[]>
        {
            // ключ -> { English, Русский }
            ["limit"]       = new[] { "Wire limit for this raid reached", "Достигнут лимит проволоки в рейде" },
            ["noKit"]       = new[] { "No Egoza wire coil in your inventory", "Нет «Егозы» в инвентаре" },
            ["cannotHere"]  = new[] { "Can't place it here", "Сюда нельзя" },
            ["useFail"]     = new[] { "Couldn't use the item (see BepInEx log)", "Не удалось использовать предмет (см. лог BepInEx)" },
            ["deployed"]    = new[] { "Wire deployed", "Проволока развёрнута" },
            ["noWireNear"]  = new[] { "No wire nearby", "Рядом нет проволоки" },
            ["alreadyGone"] = new[] { "That wire is already gone", "Проволока уже убрана" },
            ["packFail"]    = new[] { "Couldn't pack it up (see BepInEx log)", "Не удалось свернуть (см. лог BepInEx)" },
            ["packed"]      = new[] { "Wire packed up - pick it up from the ground", "Проволока свёрнута - подберите её с земли" },
            ["cancelled"]   = new[] { "Cancelled", "Отменено" },
            ["interrupted"] = new[] { "Interrupted", "Прервано" },
            ["moved"]       = new[] { "Interrupted: you moved", "Прервано: вы сдвинулись" },
            ["deploying"]   = new[] { "Deploying wire...", "Развёртывание проволоки…" },
            ["packing"]     = new[] { "Packing wire up...", "Сворачивание проволоки…" },
            ["hintStart"]   = new[] { "Aim at the wire start and press {0}  ({1} - cancel)", "Целься в начало проволоки и нажми {0}  ({1} - отмена)" },
            ["hintEnd"]     = new[] { "Aim at the wire end (max {0} m) and press {1}  ({2} - cancel)", "Целься в конец (макс. {0} м) и нажми {1}  ({2} - отмена)" },
        };

        public static string T(string key)
        {
            if (!D.TryGetValue(key, out var v)) return key;
            return v[Cfg.Language != null && Cfg.Language.Value == "Ru" ? 1 : 0];
        }
    }
}

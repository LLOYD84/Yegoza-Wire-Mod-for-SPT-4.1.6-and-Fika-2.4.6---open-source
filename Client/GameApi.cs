using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace YegozaWire
{
    /// <summary>
    /// ВСЕ обращения к внутренностям EFT собраны здесь. Если после обновления игры/SPT что-то
    /// не собирается или не работает - чинить нужно, скорее всего, только этот файл.
    /// </summary>
    internal static class GameApi
    {
        public static GameWorld World => Singleton<GameWorld>.Instance;

        public static bool IsBot(Player p) => p != null && p.IsAI;

        // ---------------- Замедление ----------------
        private static MethodInfo _addLimit, _removeLimit;
        private static object _barbedCause;
        private static bool _limitInit, _limitOk;

        private static void InitLimit()
        {
            if (_limitInit) return;
            _limitInit = true;
            try
            {
                var enumType = typeof(Player).GetNestedType("ESpeedLimit", BindingFlags.Public | BindingFlags.NonPublic);
                _barbedCause = Enum.Parse(enumType, "BarbedWire");
                foreach (var m in typeof(MovementContext).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    var ps = m.GetParameters();
                    if (m.Name == "AddStateSpeedLimit" && ps.Length == 2 && ps[0].ParameterType == typeof(float)
                        && ps[1].ParameterType == enumType) _addLimit = m;
                    else if (m.Name == "RemoveStateSpeedLimit" && ps.Length == 1 && ps[0].ParameterType == enumType)
                        _removeLimit = m;
                }
                if (_addLimit == null || _removeLimit == null)
                    foreach (var m in typeof(MovementContext).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        if (m.Name.Contains("SpeedLimit"))
                            Plugin.Log.LogInfo("SpeedLimit overload: " + m);
                _limitOk = _addLimit != null && _removeLimit != null;
            }
            catch (Exception e) { Plugin.Log.LogError("Speed limit API not found: " + e.Message); }
            if (!_limitOk) Plugin.Log.LogError("Замедление недоступно: не найдены AddStateSpeedLimit/RemoveStateSpeedLimit");
        }

        public static void SetSlow(Player p, bool on, float limit)
        {
            InitLimit();
            if (!_limitOk || p == null || p.MovementContext == null) return;
            try
            {
                if (on) _addLimit.Invoke(p.MovementContext, new object[] { limit, _barbedCause });
                else _removeLimit.Invoke(p.MovementContext, new object[] { _barbedCause });
            }
            catch (Exception e) { Plugin.Log.LogError("SetSlow: " + e.Message); }
            LimitSprint(p, on, limit);
        }

        // Лимит скорости не действует на бег, поэтому запрещаем спринт отдельно (игрок и бот).
        private static MethodInfo _enableSprint, _botSprint, _botSpeed;
        private static bool _sprintInit;

        private static void LimitSprint(Player p, bool on, float limit)
        {
            try
            {
                if (!_sprintInit)
                {
                    _sprintInit = true;
                    foreach (var m in typeof(MovementContext).GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (!(m.Name.Contains("Sprint") || m.Name.Contains("MovementSpeed"))) continue;
                        Plugin.Log.LogInfo("MovementContext method: " + m);
                        var ps = m.GetParameters();
                        if (m.Name == "EnableSprint" && ps.Length == 1 && ps[0].ParameterType == typeof(bool)) _enableSprint = m;
                    }
                    if (_enableSprint == null) Plugin.Log.LogWarning("EnableSprint(bool) не найден - бег в проволоке не блокируется");
                }
                if (p.IsAI)
                {
                    var owner = Member(Member(p, "AIData"), "BotOwner");
                    var mover = Member(owner, "Mover");
                    if (mover == null) return;
                    if (_botSprint == null || _botSpeed == null)
                    {
                        foreach (var m in mover.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                        {
                            var ps = m.GetParameters();
                            if (m.Name == "Sprint" && ps.Length >= 1 && ps[0].ParameterType == typeof(bool)) _botSprint = m;
                            else if (m.Name == "SetTargetMoveSpeed" && ps.Length == 1 && ps[0].ParameterType == typeof(float)) _botSpeed = m;
                        }
                        Plugin.Log.LogInfo("Bot mover: Sprint=" + (_botSprint != null) + " SetTargetMoveSpeed=" + (_botSpeed != null));
                    }
                    if (on)
                    {
                        if (_botSprint != null) _botSprint.Invoke(mover, BotArgs(_botSprint, false));
                        if (_botSpeed != null) _botSpeed.Invoke(mover, new object[] { limit });
                    }
                    return;
                }
                if (_enableSprint != null && p.MovementContext != null)
                    _enableSprint.Invoke(p.MovementContext, new object[] { !on });
            }
            catch (Exception e) { Plugin.Log.LogError("LimitSprint: " + e.Message); }
        }

        private static object[] BotArgs(MethodInfo m, bool first)
        {
            var ps = m.GetParameters();
            var a = new object[ps.Length];
            a[0] = first;
            for (int i = 1; i < ps.Length; i++) a[i] = ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
            return a;
        }

        // ---------------- Урон ----------------
        private static MethodInfo _applyDamage;
        private static bool _dmgInit;
        private static int _hurtLogged;
        private static readonly EBodyPart[] LimbParts =
            { EBodyPart.LeftLeg, EBodyPart.RightLeg, EBodyPart.LeftArm, EBodyPart.RightArm };

        public static void Hurt(Player p, float dmg)
        {
            if (p == null || p.ActiveHealthController == null) return;
            try
            {
                var hc = p.ActiveHealthController;
                if (!_dmgInit)
                {
                    _dmgInit = true;
                    foreach (var m in hc.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (m.Name != "ApplyDamage") continue;
                        var ps = m.GetParameters();
                        if (ps.Length == 3 && ps[0].ParameterType == typeof(EBodyPart) && ps[1].ParameterType == typeof(float))
                        { _applyDamage = m; break; }
                    }
                    if (_applyDamage == null) Plugin.Log.LogError("Урон недоступен: ApplyDamage(EBodyPart,float,info) не найден");
                }
                if (_applyDamage == null) return;

                var infoType = _applyDamage.GetParameters()[2].ParameterType;
                object info = Activator.CreateInstance(infoType);
                SetMember(info, "Damage", dmg);
                SetMember(info, "DamageType", ParseEnum(infoType, "DamageType", "Barbed"));
                SetMember(info, "Direction", Vector3.down);

                var part = LimbParts[UnityEngine.Random.Range(0, LimbParts.Length)];
                _applyDamage.Invoke(hc, new object[] { part, dmg, info });
                if (_hurtLogged < 5)
                {
                    _hurtLogged++;
                    Plugin.Log.LogInfo("Hurt " + (p.IsAI ? "bot" : "player") + " " + part + " " + dmg);
                }
            }
            catch (Exception e) { Plugin.Log.LogError("Hurt: " + e.Message); }
        }


        private static MethodInfo _doBleed;
        private static bool _bleedInit;
        public static void Bleed(Player p)
        {
            if (p == null || p.ActiveHealthController == null) return;
            try
            {
                var hc = p.ActiveHealthController;
                if (!_bleedInit)
                {
                    _bleedInit = true;
                    foreach (var m in hc.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (!m.Name.Contains("Bleed")) continue;
                        Plugin.Log.LogInfo("Bleed method: " + m);
                        if (m.Name == "DoBleed" && !m.IsGenericMethodDefinition && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(EBodyPart)) _doBleed = m;
                    }
                    if (_doBleed == null) Plugin.Log.LogWarning("DoBleed(bool, EBodyPart) не найден - кровотечение не накладывается");
                }
                if (_doBleed != null) _doBleed.Invoke(hc, new object[] { false, LimbParts[UnityEngine.Random.Range(0, LimbParts.Length)] });
            }
            catch (Exception e) { Plugin.Log.LogError("Bleed: " + e.Message); }
        }

        private static object ParseEnum(Type owner, string member, string value)
        {
            var f = owner.GetField(member);
            var t = f != null ? f.FieldType : owner.GetProperty(member)?.PropertyType;
            return t != null && t.IsEnum ? Enum.Parse(t, value) : null;
        }

        private static void SetMember(object boxed, string name, object value)
        {
            if (value == null) return;
            var t = boxed.GetType();
            var f = t.GetField(name);
            if (f != null) { f.SetValue(boxed, value); return; }
            var pr = t.GetProperty(name);
            if (pr != null && pr.CanWrite) pr.SetValue(boxed, value, null);
        }

        // ---------------- Инвентарь ----------------
        // ВНИМАНИЕ: два метода ниже - самые вероятные кандидаты на подгонку под вашу сборку 4.1.6.
        // Все операции идут через InventoryController, чтобы Fika их синхронизировал.

        public static Item FindItem(Player p, string tpl)
        {
            foreach (var it in p.Profile.Inventory.AllRealPlayerItems)
                if (it.TemplateId.ToString() == tpl) return it;
            return null;
        }

        /// <summary>Убрать предмет из инвентаря (для развёртывания).</summary>
        public static bool Consume(Player p, Item item)
        {
            try
            {
                var ctrl = p.InventoryController;
                var handler = FindHandler();
                if (handler == null) { Plugin.Log.LogError("Не найден класс с QuickFindAppropriatePlace/Remove"); return false; }

                // Ищем Remove(Item, контроллер, bool) среди перегрузок
                MethodInfo m = null;
                foreach (var cand in handler.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (cand.Name != "Remove") continue;
                    var ps = cand.GetParameters();
                    if (ps.Length == 3 && ps[0].ParameterType.IsInstanceOfType(item)
                        && ps[1].ParameterType.IsInstanceOfType(ctrl) && ps[2].ParameterType == typeof(bool))
                    { m = cand; break; }
                }
                if (m == null) { Plugin.Log.LogError("Не найден метод Remove(item, controller, simulate) в " + handler.Name); return false; }

                // simulate=true: Remove только готовит операцию, выполняет её сетевая транзакция.
                // (с false предмет убирается сразу, и транзакция падает: у предмета уже нет родителя)
                var res = m.Invoke(null, new object[] { item, ctrl, true });
                if (Failed(res))
                {
                    Plugin.Log.LogError("Remove вернул неудачу: " + (Member(res, "Error") ?? "(без описания)"));
                    return false;
                }
                return RunOp(ctrl, res);
            }
            catch (Exception e) { Plugin.Log.LogError("Consume: " + e); return false; }
        }

        /// <summary>Создать предмет и положить в инвентарь (для сворачивания). false - нет места.</summary>
        public static bool Give(Player p, string tpl)
        {
            try
            {
                var ctrl = p.InventoryController;

                // 1. создать предмет через фабрику
                if (!FindFactory(out var factory, out var create))
                { Plugin.Log.LogError("Не найдена фабрика предметов (CreateItem)"); return false; }

                var idType = create.GetParameters()[0].ParameterType;
                object id = idType == typeof(string)
                    ? (object)Guid.NewGuid().ToString("N").Substring(0, 24)
                    : Activator.CreateInstance(idType, new object[] { true });
                var item = create.Invoke(factory, new object[] { id, tpl, null }) as Item;
                if (item == null) return false;

                // Предмет без родителя нельзя добавить в инвентарь (игра отвергает транзакцию Add, а прямой Add
                // даёт "неизвестный предмет"). Поэтому выбрасываем его на землю у ног - как гранату:
                // игрок подбирает его обычным способом, Fika показывает его остальным.
                return Drop(p, item);
            }
            catch (Exception e) { Plugin.Log.LogError("Give: " + e); return false; }
        }


        /// <summary>Ищем свободное место в сетках рюкзака/жилета/карманов и добавляем туда предмет.</summary>
        private static bool GiveManual(Player p, object ctrl, Item item)
        {
            try
            {
                var handler = FindHandler();
                if (handler == null) return false;
                var asm = typeof(Item).Assembly;
                var addrT = asm.GetType("EFT.InventoryLogic.GridItemAddress");
                if (addrT == null) { Plugin.Log.LogWarning("GridItemAddress не найден"); return false; }

                MethodInfo add = null;
                foreach (var m in handler.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "Add") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 4 && ps[0].ParameterType.IsInstanceOfType(item)
                        && ps[1].ParameterType.IsAssignableFrom(addrT)
                        && ps[2].ParameterType.IsInstanceOfType(ctrl) && ps[3].ParameterType == typeof(bool))
                    { add = m; break; }
                }
                if (add == null)
                {
                    foreach (var m in handler.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        Plugin.Log.LogInfo("Handler method: " + m);
                    Plugin.Log.LogWarning("Add(item, address, controller, simulate) не найден");
                    return false;
                }

                foreach (var c in p.Profile.Inventory.Equipment.GetAllItems())
                {
                    var grids = Member(c, "Grids") as System.Collections.IEnumerable;
                    if (grids == null) continue;
                    var parent = Member(c, "Parent");
                    var cont = Member(parent, "Container");
                    var cid = Member(cont, "ID") as string;
                    if (cid == "SecuredContainer") continue;

                    foreach (var grid in grids)
                    {
                        MethodInfo ffs = null;
                        foreach (var m in grid.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            if (m.Name == "FindFreeSpace" && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType.IsInstanceOfType(item)) { ffs = m; break; }
                        if (ffs == null) continue;
                        var loc = ffs.Invoke(grid, new object[] { item });
                        if (loc == null || Failed2(loc)) continue;

                        object addr = null;
                        foreach (var ct in ConcreteAddressTypes(addrT))
                        {
                            foreach (var ctor in ct.GetConstructors())
                            {
                                var cp = ctor.GetParameters();
                                if (cp.Length == 2 && cp[0].ParameterType.IsInstanceOfType(grid) && cp[1].ParameterType.IsInstanceOfType(loc))
                                { addr = ctor.Invoke(new object[] { grid, loc }); break; }
                            }
                            if (addr != null) break;
                        }
                        if (addr == null) continue;

                        // Add не поддерживается сетевой транзакцией (AddResult отвергается), поэтому выполняем сразу (simulate=false)
                        var res = add.Invoke(null, new object[] { item, addr, ctrl, false });
                        if (Failed(res)) continue;
                        Plugin.Log.LogInfo("Give: предмет добавлен в " + c.TemplateId.ToString());
                        return true;
                    }
                }
                return false;
            }
            catch (Exception e) { Plugin.Log.LogError("GiveManual: " + e); return false; }
        }


        private static bool Drop(Player p, Item item)
        {
            var gw = Singleton<GameWorld>.Instance;
            if (gw == null) return false;
            var pos = p.Position + p.Transform.Original.forward * 0.6f + Vector3.up * 0.4f;

            MethodInfo best = null;
            foreach (var m in gw.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name != "ThrowItem") continue;
                var ps = m.GetParameters();
                if (ps.Length > 0 && ps[0].ParameterType.IsInstanceOfType(item)) { best = m; break; }
            }
            if (best == null)
            {
                foreach (var m in gw.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    if (m.Name.Contains("Throw") || m.Name.Contains("Loot") || m.Name.Contains("Drop"))
                        Plugin.Log.LogInfo("GameWorld method: " + m);
                Plugin.Log.LogError("GameWorld.ThrowItem не найден");
                return false;
            }

            var pars = best.GetParameters();
            var args = new object[pars.Length];
            bool posSet = false;
            for (int i = 0; i < pars.Length; i++)
            {
                var t = pars[i].ParameterType;
                if (i == 0) args[i] = item;
                else if (t.IsInstanceOfType(p)) args[i] = p;
                else if (t == typeof(Vector3)) { args[i] = posSet ? Vector3.zero : pos; posSet = true; }
                else if (t == typeof(Quaternion)) args[i] = Quaternion.identity;
                else if (t == typeof(bool)) args[i] = true;
                else args[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
            }
            Plugin.Log.LogInfo("Drop via " + best);
            var loot = best.Invoke(gw, args) as Component;
            if (loot != null) WireVisual.DecorateLoot(loot.gameObject);
            else Plugin.Log.LogWarning("ThrowItem не вернул LootItem - модель на земле не заменена");
            return true;
        }

        private static List<Type> _addrTypes;
        private static List<Type> ConcreteAddressTypes(Type baseT)
        {
            if (_addrTypes != null) return _addrTypes;
            _addrTypes = new List<Type>();
            foreach (var t in typeof(Item).Assembly.GetTypes())
                if (!t.IsAbstract && baseT.IsAssignableFrom(t)) _addrTypes.Add(t);
            Plugin.Log.LogInfo("Grid address types: " + string.Join(", ", _addrTypes.ConvertAll(x => x.FullName).ToArray()));
            return _addrTypes;
        }

        // FindFreeSpace иногда возвращает результат-обёртку с признаком неудачи
        private static bool Failed2(object o)
        {
            var v = Member(o, "Failed");
            return v is bool b && b;
        }

        // ---- вспомогательные методы (рефлексия) ----

        // ---- поиск классов игры по их методам (имена в 4.1 разные, по имени не найти) ----

        private static IEnumerable<Type> AllTypes()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var an = asm.GetName().Name;
                if (an.StartsWith("System") || an.StartsWith("mscorlib") || an.StartsWith("Unity")
                    || an.StartsWith("Mono") || an.StartsWith("BepInEx") || an.StartsWith("0Harmony")
                    || an.StartsWith("Newtonsoft")) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                catch { continue; }
                foreach (var t in types) if (t != null) yield return t;
            }
        }

        private static Type _handler;
        /// <summary>Статический класс, в котором есть и Remove, и QuickFindAppropriatePlace.</summary>
        private static Type FindHandler()
        {
            if (_handler != null) return _handler;
            foreach (var t in AllTypes())
            {
                MethodInfo[] ms;
                try { ms = t.GetMethods(BindingFlags.Public | BindingFlags.Static); } catch { continue; }
                bool quick = false, remove = false;
                foreach (var m in ms)
                {
                    if (m.Name == "QuickFindAppropriatePlace") quick = true;
                    else if (m.Name == "Remove") remove = true;
                }
                if (quick && remove)
                {
                    _handler = t;
                    Plugin.Log.LogInfo("Inventory handler type: " + t.FullName);
                    return t;
                }
            }
            return null;
        }

        private static object _factory;
        private static MethodInfo _createItem;
        /// <summary>Фабрика предметов: класс с CreateItem(id, tpl, ...) и живым Singleton Instance.</summary>
        private static bool FindFactory(out object factory, out MethodInfo create)
        {
            if (_factory == null)
            {
                foreach (var t in AllTypes())
                {
                    MethodInfo cm = null;
                    try
                    {
                        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                            if (m.Name == "CreateItem" && m.GetParameters().Length == 3) { cm = m; break; }
                    }
                    catch { continue; }
                    if (cm == null) continue;
                    try
                    {
                        var inst = typeof(Singleton<>).MakeGenericType(t).GetProperty("Instance").GetValue(null, null);
                        if (inst == null) continue;
                        _factory = inst; _createItem = cm;
                        Plugin.Log.LogInfo("Item factory type: " + t.FullName);
                        break;
                    }
                    catch { }
                }
            }
            factory = _factory; create = _createItem;
            return _factory != null;
        }

        private static Type FindType(params string[] names)
        {
            foreach (var t in typeof(Player).Assembly.GetTypes())
                foreach (var n in names)
                    if (t.Name == n) return t;
            return null;
        }

        private static MethodInfo FindStatic(Type t, string name, int paramCount)
        {
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (m.Name == name && m.GetParameters().Length == paramCount) return m;
            return null;
        }

        private static object Member(object o, string name)
        {
            if (o == null) return null;
            var t = o.GetType();
            var pr = t.GetProperty(name);
            if (pr != null) return pr.GetValue(o, null);
            var f = t.GetField(name);
            return f != null ? f.GetValue(o) : null;
        }

        private static bool Failed(object result)
        {
            if (result == null) return true;
            var v = Member(result, "Failed");
            if (v is bool b) return b;
            var s = Member(result, "Succeeded");
            if (s is bool ok) return !ok;
            // Признак успеха не нашли - считаем неудачей только если есть ошибка
            var err = Member(result, "Error");
            if (err != null) Plugin.Log.LogError("Операция инвентаря вернула ошибку: " + err);
            Plugin.Log.LogWarning("Нет Failed/Succeeded у " + result.GetType().Name + ", члены: " + string.Join(", ",
                Array.ConvertAll(result.GetType().GetMembers(), m => m.Name)));
            return err != null;
        }

        /// <summary>Выполнить операцию через InventoryController (сетевая транзакция, её видит Fika).</summary>
        private static bool RunOp(object ctrl, object result)
        {
            var value = Member(result, "Value");
            Plugin.Log.LogInfo("RunOp: result=" + result.GetType().FullName + " value=" + (value != null ? value.GetType().FullName : "null"));
            var candidates = new List<object> { result };
            if (value != null) candidates.Add(value);
            foreach (var name in new[] { "TryRunNetworkTransaction", "RunNetworkTransaction" })
            {
                foreach (var m in ctrl.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (m.Name != name) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 0) continue;

                    foreach (var arg in candidates)
                    {
                        if (!ps[0].ParameterType.IsInstanceOfType(arg)) continue;
                        var args = new object[ps.Length];
                        args[0] = arg;
                        for (int i = 1; i < ps.Length; i++)
                            args[i] = ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
                        try { m.Invoke(ctrl, args); return true; }
                        catch (Exception e)
                        {
                            Plugin.Log.LogWarning("RunOp " + m + " arg=" + arg.GetType().Name + ": " + (e.InnerException ?? e).Message);
                        }
                    }
                }
            }
            Plugin.Log.LogError("Не найден подходящий TryRunNetworkTransaction для " + result.GetType().Name);
            return false;
        }
    }
}

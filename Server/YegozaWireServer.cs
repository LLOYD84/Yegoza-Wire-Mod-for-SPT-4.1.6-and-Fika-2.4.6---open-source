using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace YegozaWire.Server;

/// <summary>
/// Добавляет предмет «Егоза» (клон «Bundle of wires», размер 2x2, вес 3.5 кг)
/// и продаёт его у Прапора на 2 уровне лояльности.
/// ВАЖНО: Id предмета должен совпадать с константой в клиентском плагине (Cfg.ItemTpl).
/// </summary>
// Часть 1: создание предмета. SPT требует делать это ДО загрузки профилей (Preload).
[Injectable(TypePriority = OnLoadOrder.Preload)]
public class YegozaWireServer(
    CustomItemService customItemService,
    ISptLogger<YegozaWireServer> logger) : IOnLoad
{
    // ---- Настройки (меняй тут и пересобирай) ----
    internal const string ItemId = "674f1a2b3c4d5e6f7a8b9c0d";      // ID нового предмета
    internal const string AssortId = "674f1a2b3c4d5e6f7a8b9c0e";    // ID позиции в ассортименте
    private const string CloneFrom = "5c06779c86f77426e00dd782";    // Bundle of wires (модель мотка проволоки)
    internal const double WeightKg = 3.5;                           // ~5 м армейской спирали
    internal const int PriceRoubles = 28000;
    internal const int TraderLoyaltyLevel = 2;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var details = new NewItemFromCloneDetails
        {
            NewItemName = "yegoza_wire_coil",
            ItemTplToClone = CloneFrom,
            ParentId = "5448eb774bdc2d0a728b4567",         // Barter item
            NewId = new MongoId(ItemId),
            FleaPriceRoubles = PriceRoubles,
            HandbookPriceRoubles = PriceRoubles,
            HandbookParentId = "5b47574386f77428ca22b2ee", // Building materials
            OverrideProperties = new TemplateItemProperties
            {
                Width = 2,
                Height = 2,
                Weight = WeightKg,
                StackMaxSize = 1,
                ExamineExperience = 5,
            },
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["ru"] = new LocaleDetails
                {
                    Name = "Спиральный барьер «Егоза»",
                    ShortName = "Егоза",
                    Description = "Армейская колючая проволока в бухте (около 5 м). Можно развернуть на полу или лестнице: " +
                                  "замедляет и ранит всех, кто проходит через неё. Нажмите Y в рейде, наведите на начало и конец, дождитесь ~5 секунд. " +
                                  "Забрать обратно: удерживать U рядом с проволокой."
                },
                ["en"] = new LocaleDetails
                {
                    Name = "Egoza razor-wire coil",
                    ShortName = "Egoza",
                    Description = "Military barbed-wire coil (about 5 m). Deploy it on a floor or stairs: it slows and hurts " +
                                  "anyone crossing it. In a raid press Y, aim at the start and end points and wait ~5 seconds. " +
                                  "To pick it back up, hold U next to the wire."
                }
            }
        };

        var result = customItemService.CreateItemFromClone(details);
        if (!result.Success)
        {
            logger.Error($"[YegozaWire] Failed to create the item: {string.Join("; ", result.Errors ?? [])}");
            return Task.CompletedTask;
        }

        logger.Success("[YegozaWire] Egoza item created");
        return Task.CompletedTask;
    }
}

// Часть 2: продажа у Прапора. Выполняется позже, когда данные торговцев уже загружены.
[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public class YegozaWireTrader(
    TradersTable tradersTable,
    ISptLogger<YegozaWireTrader> logger) : IOnLoad
{
    private const string ItemId = YegozaWireServer.ItemId;
    private const string AssortId = YegozaWireServer.AssortId;
    private const int PriceRoubles = YegozaWireServer.PriceRoubles;
    private const int TraderLoyaltyLevel = YegozaWireServer.TraderLoyaltyLevel;
    private const double WeightKg = YegozaWireServer.WeightKg;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        // В 4.1 DatabaseService удалён, торговцы приходят через TradersTable.
        // Точный вид TradersTable мне не известен (словарь или объект со словарём), поэтому dynamic.
        dynamic table = tradersTable;
        var praporId = new MongoId("54cb50c76803fa8b248b4571");
        dynamic prapor;
        try { prapor = table[praporId]; }
        catch { prapor = table.Traders[praporId]; }
        var assortId = new MongoId(AssortId);

        prapor.Assort.Items.Add(new Item
        {
            Id = assortId,
            Template = new MongoId(ItemId),
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd { UnlimitedCount = true, StackObjectsCount = 999999 }
        });
        prapor.Assort.BarterScheme[assortId] = new List<List<BarterScheme>>
        {
            new List<BarterScheme> { new BarterScheme { Template = Money.ROUBLES, Count = PriceRoubles } }
        };
        prapor.Assort.LoyalLevelItems[assortId] = TraderLoyaltyLevel;

        logger.Success($"[YegozaWire] Egoza added to Prapor: LL{TraderLoyaltyLevel}, {PriceRoubles} RUB, {WeightKg} kg");
        return Task.CompletedTask;
    }
}

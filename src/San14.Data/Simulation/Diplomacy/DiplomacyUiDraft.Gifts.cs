using San14.Core;

namespace San14.Data;

public sealed record DiplomacyPreparationGiftChoice(int Id, string Name);

public sealed partial class DiplomacyUiDraft
{
    public int? GiftItemId { get; private set; }
    public int? GiftAreaId { get; private set; }
    public string? GiftItemName => GiftItemId is int id ? ItemChoices.Value?.FirstOrDefault(row => row.Id == id)?.Name : null;
    public string? GiftAreaName => GiftAreaId is int id ? AreaChoices.Value?.FirstOrDefault(row => row.Id == id)?.Name : null;
    public bool? CanChooseGold { get; private set; }
    public bool? CanChooseSupplies { get; private set; }
    public bool? CanChooseItem { get; private set; }
    public bool? CanChooseArea { get; private set; }
    public string? GiftUnresolvedInput { get; private set; }
    public AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>> ItemChoices => source.Items();
    public AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>> AreaChoices { get; private set; } =
        AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Known(Array.Empty<DiplomacyPreparationGiftChoice>());
    public AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> GoldSelection { get; private set; } = UnopenedAmount();
    public AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> SuppliesSelection { get; private set; } = UnopenedAmount();

    //74A640/7488D0 evaluate their three presets when the child opens. These
    //event methods do not capture another world; drawing only reads the cache.
    public AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> OpenGoldSelection() =>
        GoldSelection = ReadAmount(false);
    public AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> OpenSuppliesSelection() =>
        SuppliesSelection = ReadAmount(true);

    public void SelectGiftItem(int itemId)
    {
        RequireChoice(ItemChoices, itemId, CanChooseItem);
        GiftItemId = itemId; GiftAreaId = null; GiftGold = GiftSupplies = 0;
        RefreshSubmission(); RefreshAdvice();
    }
    public void SelectGiftArea(int areaId)
    {
        RequireChoice(AreaChoices, areaId, CanChooseArea);
        GiftAreaId = areaId; GiftItemId = null; GiftGold = GiftSupplies = 0;
        RefreshSubmission(); RefreshAdvice();
    }
    //745120 ->67 ->7560D0 ->6DB4F0 installs the current default again.
    public void ResetGift()
    {
        ResetGiftCore(); RefreshGiftAdmission(); RefreshSubmission(); RefreshAdvice();
    }
    private void ResetGiftCore()
    {
        GiftGold = GiftSupplies = 0; GiftItemId = GiftAreaId = null;
        GiftUnresolvedInput = null;
        if (Kind == DiplomaticMissionKind.CancelAlliance) return;
        if (MaxGold >= 1000) GiftGold = 1000;
        else if (MaxSupplies >= 10000) GiftSupplies = 10000;
        else
        {
            try { GiftItemId = source.DefaultItem(); }
            catch (InvalidDataException missing) { GiftUnresolvedInput = missing.Message; }
        }
        if (SelectedTargetForceId is >= 46 and <= 50) GiftItemId = null;
    }
    private void RefreshGiftAdmission()
    {
        InvalidateAmountSelections();
        CanChooseGold = CanChooseSupplies = CanChooseItem = CanChooseArea = false;
        AreaChoices = AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Known(Array.Empty<DiplomacyPreparationGiftChoice>());
        if (Kind == DiplomaticMissionKind.CancelAlliance || SelectedExecutorOfficerId is null ||
            SelectedTargetForceId is not int recipient || Kind == DiplomaticMissionKind.RequestAttack && SelectedTargetCityId is null) return;
        CanChooseGold = MaxGold >= 1000; CanChooseSupplies = MaxSupplies >= 10000;
        if (Kind == DiplomaticMissionKind.Goodwill && recipient is >= 46 and <= 50) return;
        var items = source.Items();
        CanChooseItem = source.ItemButtonAvailable;
        AreaChoices = source.Areas(recipient);
        CanChooseArea = AreaChoices.Value is { } areas ? areas.Count != 0 : null;
        GiftUnresolvedInput = items.UnresolvedInput ?? AreaChoices.UnresolvedInput;
    }
    private AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> ReadAmount(bool supplies)
    {
        bool? allowed = supplies ? CanChooseSupplies : CanChooseGold;
        if (allowed is null) return AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Unknown(GiftUnresolvedInput ?? "diplomacy.ui.gift_admission");
        if (!allowed.Value) return AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Known(
            new(false, supplies ? 10000 : 1000, supplies ? MaxSupplies : MaxGold, Array.Empty<int>()));
        return source.Amount(SelectedExecutorOfficerId!.Value, SelectedTargetForceId!.Value,
            AllianceMonths, SelectedTargetCityId, supplies);
    }
    private void InvalidateAmountSelections() { GoldSelection = UnopenedAmount(); SuppliesSelection = UnopenedAmount(); }
    private static AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> UnopenedAmount() =>
        AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Unknown("diplomacy.ui.amount_selector_not_open");
    private static void RequireChoice(AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>> choices,
        int id, bool? allowed)
    {
        if (choices.Value is not { } rows) throw new InvalidOperationException(choices.UnresolvedInput);
        if (allowed != true || !rows.Any(row => row.Id == id)) throw new ArgumentOutOfRangeException(nameof(id));
    }
}

internal sealed partial class DiplomacyUiSource
{
    private AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>? itemChoices;
    private readonly Dictionary<int, string> areaNames = [];
    private readonly Dictionary<int, AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>> areaChoices = [];
    internal bool? ItemButtonAvailable { get; private set; }

    internal AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>> Items()
    {
        if (itemChoices is not null) return itemChoices;
        try
        {
            var rows = new List<DiplomacyPreparationGiftChoice>();
            int ruler = Need(current.ReadPersonIdentity(qualification.Ruler(ForceId)), "gift_ruler_ID10");
            bool anyRetained = false;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item) ?? throw Missing("gift_Item_registry"))
            {
                int physical = Need(entry?.PhysicalSlot, "gift_Item_pointer");
                if (Need(current.ReadCaptureItemOwner4A(physical), "gift_Item4A") != ruler) continue;
                var item = State.Items?.GetValueOrDefault(physical) ?? throw Missing("gift_Item_owner");
                if ((Need(item.Lifecycle?.RetainedFlags, "gift_Item4F") & 1) == 0) continue;
                //78FD40's radio gate only inspects4F;6DD0F0 additionally
                //uses334070. An enabled selector can genuinely be empty.
                anyRetained = true; ItemButtonAvailable = true;
                if (!Need(current.ReadForceNativeValidity(new(ForceId)), "gift_Force_validity") ||
                    !Need(current.ReadRegistryObjectNativeValidity(entry!), "gift_Item_validity") ||
                    Need(current.ReadRegistryItemRawField50(entry!), "gift_Item50") != 1) continue;
                string name = State.ItemDefinitions.GetValueOrDefault(item.DefinitionId)?.Name ?? throw Missing("gift_Item_name");
                rows.Add(new(physical, name));
            }
            ItemButtonAvailable = anyRetained;
            return itemChoices = AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Known(rows.AsReadOnly());
        }
        catch (InvalidDataException missing)
        {
            return itemChoices = AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Unknown(missing.Message);
        }
    }
    internal int? DefaultItem()
    {
        var items = Items();
        if (items.Value is not { } rows) throw new InvalidDataException(items.UnresolvedInput);
        int? selected = null; int lowest = int.MaxValue;
        foreach (var row in rows)
        {
            var item = State.Items![row.Id];
            int value = Math.Min(Need(State.ItemDefinitions.GetValueOrDefault(item.DefinitionId)?.LoyaltyValue, "default_Item49"), 50);
            if (value < lowest) { selected = row.Id; lowest = value; }
        }
        return selected;
    }
    internal AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>> Areas(int recipient)
    {
        if (areaChoices.TryGetValue(recipient, out var cached)) return cached;
        try
        {
            var rows = new List<DiplomacyPreparationGiftChoice>();
            foreach (var area in current.ReadRegisteredAreaReferences() ?? throw Missing("gift_Area_registry"))
            {
                int physical = Need(area?.PhysicalSlot, "gift_Area_pointer");
                if (!adapter.ReadDiplomacyAreaOfferEligibilityCurrent(State, current, physical, ForceId, recipient)) continue;
                string name = areaNames.GetValueOrDefault(physical) ?? throw Missing("gift_Area_name");
                rows.Add(new(physical, name));
            }
            return areaChoices[recipient] = AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Known(rows.AsReadOnly());
        }
        catch (InvalidDataException missing)
        { return areaChoices[recipient] = AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Unknown(missing.Message); }
    }
    internal AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> Amount(int executor, int target,
        int months, int? targetCity, bool supplies) => adapter.ReadDiplomacyPreparationAmountCurrent(
            State, current, executor, target, Kind, months, targetCity, supplies, supplies ? MaxSupplies : MaxGold);
}

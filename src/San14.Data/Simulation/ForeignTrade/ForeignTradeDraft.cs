using San14.Core;

namespace San14.Data;

public sealed record ForeignTradeCountryChoice(int Id, string Name, bool? Enabled, string? UnresolvedOwner);
public sealed record ForeignTradeExecutorChoice(int OfficerId, string Name, string LocationName,
    int? Intelligence, int? Politics, int? Charisma, int? TravelDays, bool? Enabled, string? UnresolvedOwner);
public sealed record ForeignTradeItemChoice(int ItemId, string Name, bool? Enabled, string? UnresolvedOwner);
internal sealed record ForeignTradePageSelection(IReadOnlyList<ForeignTradeCountryChoice> Countries,
    IReadOnlyList<ForeignTradeExecutorChoice> Executors, IReadOnlyList<ForeignTradeItemChoice> Items,
    int? GoldMaximum, int? SuppliesMaximum, int? TroopsAvailable, int? BooksRequired, int? BooksAvailable, int? TravelDays,
    bool CanConfirm, string? UnresolvedOwner);

/// <summary>A page-owned selection of the ordinary caravan packet. Queries
/// capture the current source once and do not publish missions or spend cargo.</summary>
public sealed class ForeignTradeDraft
{
    private readonly Func<int?, IReadOnlyList<int>, int, int, int, int?, ForeignTradePageSelection>? read;
    private ForeignTradePageSelection? selection;
    private readonly Func<ForeignTradeSelectorKind, ForeignTradeDraft, ForeignTradeSelectorPublication>? selector;
    private readonly Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>>? card;
    private readonly Func<int?, ForeignTradeSummary>? summary;
    private readonly Func<int, AreaAdministrationQuery<string?>>? traitTooltip;
    private readonly Func<GameState, GameState, bool>? adoptPresentationWorld;
    internal ForeignTradeDraft(int forceId, string title,
        Func<int?, IReadOnlyList<int>, int, int, int, int?, ForeignTradePageSelection> read,
        Func<GameState, GameState, bool> adoptPresentationWorld,
        Func<ForeignTradeSelectorKind, ForeignTradeDraft, ForeignTradeSelectorPublication> selector,
        Func<int, AreaAdministrationQuery<AreaAdministrationOfficerCard>> card,
        Func<int?, ForeignTradeSummary> summary, Func<int, AreaAdministrationQuery<string?>> traitTooltip)
    { ForceId = forceId; Title = title; this.read = read; this.adoptPresentationWorld = adoptPresentationWorld;
        this.selector = selector; this.card = card; this.summary = summary; this.traitTooltip = traitTooltip; Refresh(); }
    internal ForeignTradeDraft(int forceId, string unresolved,
        Func<GameState, GameState, bool> adoptPresentationWorld)
    { ForceId = forceId; UnresolvedOwner = unresolved; this.adoptPresentationWorld = adoptPresentationWorld; }
    /// <summary>Retains selections and cached quotes across the exact
    /// presentation-only successor established by the owning client.</summary>
    public bool TryAdoptPresentationWorld(GameState previous, GameState replacement)
        => adoptPresentationWorld?.Invoke(previous, replacement) == true;
    public int ForceId { get; }
    public string Title { get; } = "";
    public int? CountryId { get; private set; }
    public IReadOnlyList<int> ExecutorIds { get; private set; } = [];
    public int Gold { get; private set; } = 1000;
    public int Supplies { get; private set; } = 10000;
    public int Troops { get; private set; } = 500;
    public int? ItemId { get; private set; }
    public int MaximumExecutors => 5;
    public int GoldMinimum => 1000;
    public int SuppliesMinimum => 10000;
    public IReadOnlyList<int> TroopOptions { get; } = new[] { 500, 1000, 1500, 2000 };
    public int? GoldMaximum => selection?.GoldMaximum;
    public int? SuppliesMaximum => selection?.SuppliesMaximum;
    public int? TroopsAvailable => selection?.TroopsAvailable;
    public int? BooksRequired => selection?.BooksRequired;
    public int? BooksAvailable => selection?.BooksAvailable;
    public int? TravelDays => selection?.TravelDays;
    public int? LeadExecutorOfficerId
    {
        get
        {
            int? selected = null; int longest = 0;
            foreach (int id in ExecutorIds)
                if (Executors.FirstOrDefault(row => row.OfficerId == id)?.TravelDays is int days && days > longest)
                { longest = days; selected = id; }
            return selected;
        }
    }
    public IReadOnlyList<ForeignTradeCountryChoice> Countries => selection?.Countries ?? [];
    public IReadOnlyList<ForeignTradeExecutorChoice> Executors => selection?.Executors ?? [];
    public IReadOnlyList<ForeignTradeItemChoice> Items => selection?.Items ?? [];
    public bool CanConfirm => selection?.CanConfirm == true && UnresolvedOwner is null;
    public string? UnresolvedOwner { get; private set; }
    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard(int officerId) =>
        card?.Invoke(officerId) ?? AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown("foreign-trade.page.officer_card_context17");
    public AreaAdministrationQuery<string?> QueryOfficerTraitTooltip(int lookup) => traitTooltip?.Invoke(lookup)
        ?? AreaAdministrationQuery<string?>.Unknown("foreign-trade.page.trait_tooltip");
    public ForeignTradeSummary QuerySummary() => summary?.Invoke(CountryId) ?? new(null, null, UnresolvedOwner);
    public AreaAdministrationQuery<ForeignTradeSelectorPublication> PrepareSelector(ForeignTradeSelectorKind kind)
    {
        try { return selector is null ? AreaAdministrationQuery<ForeignTradeSelectorPublication>.Unknown(UnresolvedOwner ?? "foreign-trade.page.selector")
            : AreaAdministrationQuery<ForeignTradeSelectorPublication>.Known(selector(kind, this)); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<ForeignTradeSelectorPublication>.Unknown(error.Message); }
    }
    public bool SelectCountry(int country)
    {
        if (!Countries.Any(row => row.Id == country && row.Enabled == true)) return false;
        CountryId = country; Refresh(); return true;
    }
    public bool SelectExecutors(IReadOnlyList<int> officers)
    {
        ArgumentNullException.ThrowIfNull(officers);
        if (officers.Count > MaximumExecutors || officers.Distinct().Count() != officers.Count ||
            officers.Any(id => !Executors.Any(row => row.OfficerId == id && row.Enabled == true))) return false;
        ExecutorIds = officers.ToArray(); Refresh(); return true;
    }
    public bool SetGold(int amount)
    {
        if (amount < GoldMinimum || GoldMaximum is not int maximum || amount > maximum) return false;
        Gold = amount; Refresh(); return true;
    }
    public bool SetSupplies(int amount)
    {
        if (amount < SuppliesMinimum || SuppliesMaximum is not int maximum || amount > maximum) return false;
        Supplies = amount; Refresh(); return true;
    }
    public bool SetTroops(int amount)
    {
        if (!TroopOptions.Contains(amount)) return false;
        Troops = amount; Refresh(); return true;
    }
    public bool SelectItem(int item)
    {
        if (!Items.Any(row => row.ItemId == item && row.Enabled == true)) return false;
        ItemId = item; Refresh(); return true;
    }
    public void ResetItem() { ItemId = null; Refresh(); }
    public GameOrder? BuildOrder() => CanConfirm ? new DispatchForeignTradeOrder(ForceId,
        CountryId!.Value, ExecutorIds.ToArray(), Gold, Supplies, Troops, ItemId) : null;
    private void Refresh()
    {
        if (read is null) return;
        try
        {
            selection = read(CountryId, ExecutorIds, Gold, Supplies, Troops, ItemId);
            UnresolvedOwner = selection.UnresolvedOwner;
        }
        catch (InvalidDataException error) { UnresolvedOwner = error.Message; }
    }
}

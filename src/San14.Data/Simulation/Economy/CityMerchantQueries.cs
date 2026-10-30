using San14.Core;

namespace San14.Data;

public sealed record CityMerchantCandidate(int OfficerId, int PhysicalSlot, string Name,
    int? Intelligence, int? Politics, int? CapacityWeight)
{
    public string? NameSortKey { get; init; }
}

public sealed record CityMerchantAmountOptions(CityMerchantMode Mode, IReadOnlyList<int> PresetAmounts,
    int Minimum, int Maximum, int Current);

public sealed record CityMerchantPage(int CityId, string CityName,
    IReadOnlyList<CityMerchantCandidate> Candidates, IReadOnlyList<int> SelectedOfficerIds,
    CityMerchantMode Mode, int Amount, int? Gold, int? Food, int? Books,
    int? BuyPrice, int? SellPrice, int? GoldCapacity, int? FoodCapacity,
    int? MaxAmount, int? GoldDelta, int? FoodDelta, int? AfterGold, int? AfterFood,
    int? AfterBooks, bool? HasTraded, bool? CanConfirm, string? UnresolvedInput)
{
    public int MaximumOfficers => CityMerchantSource.MaximumOfficers;
    public int CommandCost => CityMerchantSource.CommandCost;
    public bool? CanBuy { get; init; }
    public bool? CanSell { get; init; }
}

/// <summary>One page owns its captured current. Editing only changes page
/// choices; the formal order always re-reads the active campaign.</summary>
public sealed class CityMerchantDraft : IDisposable
{
    private readonly CityMerchantSource source;
    private int[] selected = [];
    private CityMerchantMode mode = CityMerchantMode.Buy;
    private int amount;
    private bool closed;
    internal CityMerchantDraft(CityMerchantSource source) => this.source = source;
    internal CityMerchantSource Source => !closed ? source : throw new InvalidDataException("city-merchant.closed");
    internal IReadOnlyList<int> SelectedOfficerIds => Array.AsReadOnly(selected.ToArray());

    public AreaAdministrationQuery<CityMerchantPage> QueryPage()
    {
        if (closed) return AreaAdministrationQuery<CityMerchantPage>.Unknown("city-merchant.closed");
        return AreaAdministrationQuery<CityMerchantPage>.Known(source.Page(selected, mode, amount));
    }

    public AreaAdministrationQuery<AreaAdministrationOfficerCard> QueryOfficerCard()
        => closed ? AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown("city-merchant.closed")
            : source.OfficerCard(selected.FirstOrDefault());

    public AreaAdministrationQuery<CityMerchantAmountOptions> QueryAmountOptions(CityMerchantMode requested)
    {
        if (closed) return AreaAdministrationQuery<CityMerchantAmountOptions>.Unknown("city-merchant.closed");
        if (!Enum.IsDefined(requested)) return AreaAdministrationQuery<CityMerchantAmountOptions>.Unknown("city-merchant.mode");
        var page = source.Page(selected, requested, amount);
        if (page.MaxAmount is not int maximum || page.SellPrice is not int sellPrice)
            return AreaAdministrationQuery<CityMerchantAmountOptions>.Unknown(page.UnresolvedInput ?? "city-merchant.amount_inputs");
        int[] presets = maximum < 1000 ? [maximum, 0, 0] :
            [unchecked(maximum * 25) / 100 / 10 * 10, unchecked(maximum * 50) / 100 / 10 * 10, maximum / 10 * 10];
        return AreaAdministrationQuery<CityMerchantAmountOptions>.Known(new(requested, Array.AsReadOnly(presets),
            requested == CityMerchantMode.Buy ? 0 : unchecked(sellPrice + 9) / 10, maximum, amount));
    }

    public AreaAdministrationQuery<string> QueryDefaultHint() => closed
        ? AreaAdministrationQuery<string>.Unknown("city-merchant.closed") : source.DefaultHint();

    public AreaAdministrationQuery<CityDomesticNarration> PrepareNarration(string locale, Func<int, int> appRandom)
        => closed ? AreaAdministrationQuery<CityDomesticNarration>.Unknown("city-merchant.closed")
            : source.PrepareNarration(selected, mode, amount, locale, appRandom);

    public AreaAdministrationDraftResult SelectOfficers(IReadOnlyList<int> officerIds)
    {
        if (closed) return new(false, "city-merchant.closed");
        if (officerIds is null || officerIds.Count > CityMerchantSource.MaximumOfficers ||
            officerIds.Distinct().Count() != officerIds.Count || officerIds.Any(id => !source.IsCandidate(id)))
            return new(false, "city-merchant.officer_selection");
        bool same = selected.Length == officerIds.Count && selected.Order().SequenceEqual(officerIds.Order());
        selected = officerIds.ToArray();
        if (!same) { mode = CityMerchantMode.Buy; amount = 0; }
        return new(true);
    }

    public AreaAdministrationDraftResult SetMode(CityMerchantMode value)
    {
        if (closed) return new(false, "city-merchant.closed");
        if (!Enum.IsDefined(value)) return new(false, "city-merchant.mode");
        mode = value; amount = 0;
        return new(true);
    }

    public AreaAdministrationDraftResult SetAmount(int value)
    {
        if (closed) return new(false, "city-merchant.closed");
        var page = source.Page(selected, mode, value);
        if (value < 0 || page.MaxAmount is not int maximum || value > maximum)
            return new(false, page.UnresolvedInput ?? "city-merchant.amount");
        amount = value;
        return new(true);
    }

    public AreaAdministrationQuery<CityMerchantOrder> BuildOrder(int forceId)
    {
        var page = QueryPage();
        if (!page.Available || page.Value?.CanConfirm != true)
            return AreaAdministrationQuery<CityMerchantOrder>.Unknown(page.UnresolvedInput ??
                page.Value?.UnresolvedInput ?? "city-merchant.cannot_confirm");
        return AreaAdministrationQuery<CityMerchantOrder>.Known(new(forceId, source.CityId,
            Array.AsReadOnly(selected.ToArray()), mode, amount));
    }
    public void Abandon() => closed = true;
    public void Dispose() => Abandon();
}

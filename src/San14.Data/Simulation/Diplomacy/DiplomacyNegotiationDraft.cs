using San14.Core;

namespace San14.Data;

public enum DiplomacyNegotiationConditionKind { Gold, Supplies, Item, Area }
public sealed record DiplomacyNegotiationChoice(int Id, string Name);
public sealed record DiplomacyNegotiationAmountSelection(bool Allowed, int Minimum, int Maximum,
    IReadOnlyList<int> Presets);

/// <summary>The real Spy condition editor. One page owns its current inputs;
/// selectors change only this proposal, and confirmation submits the real order.</summary>
public sealed class DiplomacyNegotiationDraft
{
    private readonly DiplomacyNegotiationSource source;
    private readonly DiplomacyOffer entryProposal;
    private int? submittedIntelligence;
    private int? probabilityBeforeExpense;
    public DiplomacyNegotiationRequest Request => source.Request;
    public DiplomaticMissionKind Kind => Request.Kind;
    public DiplomaticNegotiationStage Stage => Request.Negotiation?.Stage ??
        (source.PlayerForceId == Request.ForceId ? DiplomaticNegotiationStage.SenderPersuasion : DiplomaticNegotiationStage.RecipientDecision);
    public bool CanEditConditions => Request.Negotiation?.CanEditConditions == true;
    public DiplomacyNegotiationPage Page { get; private set; }
    public DiplomacyOffer Offer { get; private set; }
    public int AllianceMonths => Request.AllianceMonths;
    public int? TargetCityId => Request.TargetCityId;
    public CampaignAppRandomState? AppRandomState => source.State.CampaignAppRandom32;
    public DiplomacyNegotiationConditionKind ConditionKind => Offer.ItemId is > 0 ? DiplomacyNegotiationConditionKind.Item :
        Offer.AreaId is > 0 ? DiplomacyNegotiationConditionKind.Area : Offer.Supplies != 0 ?
        DiplomacyNegotiationConditionKind.Supplies : DiplomacyNegotiationConditionKind.Gold;
    public AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> GoldSelection { get; private set; }
    public AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> SuppliesSelection { get; private set; }
    public AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>> ItemChoices { get; }
    public AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>> AreaChoices { get; }
    public bool? CanChooseGold => Allowed(GoldSelection);
    public bool? CanChooseSupplies => Allowed(SuppliesSelection);
    public bool? CanChooseItem => Allowed(ItemChoices);
    public bool? CanChooseArea => Allowed(AreaChoices);
    public int? MilitaryIntelligenceMinimum { get; private set; }
    public int? MilitaryIntelligenceMaximum { get; private set; }
    public int? MilitaryIntelligenceValue { get; private set; }
    public int? RequiredMilitaryIntelligence { get; private set; }
    public int? Probability { get; private set; }
    public string? MilitaryIntelligenceUnresolvedInput { get; private set; }
    public string? ProbabilityUnresolvedInput { get; private set; }
    public bool? CanChooseMilitaryIntelligence { get; private set; }
    public int? MilitaryIntelligenceCalculatorMaximum => MilitaryIntelligenceMaximum is int maximum &&
        RequiredMilitaryIntelligence is int required ? Math.Min(maximum, required) : null;

    internal DiplomacyNegotiationDraft(DiplomacyNegotiationSource source, DiplomacyOffer proposal)
    {
        this.source = source; Offer = proposal; entryProposal = proposal;
        Page = source.ReadPage(proposal);
        if (CanEditConditions)
        {
            ItemChoices = source.Items(); AreaChoices = source.Areas();
            GoldSelection = source.Amount(proposal, false); SuppliesSelection = source.Amount(proposal, true);
        }
        else
        {
            //57(0) does not enumerate the disabled condition selectors.
            ItemChoices = AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>>.Known(Array.Empty<DiplomacyNegotiationChoice>());
            AreaChoices = AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>>.Known(Array.Empty<DiplomacyNegotiationChoice>());
            GoldSelection = AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Known(new(false, 1000, 1000, Array.Empty<int>()));
            SuppliesSelection = AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Known(new(false, 10000, 10000, Array.Empty<int>()));
        }
        if (Stage == DiplomaticNegotiationStage.RecipientDecision)
        {
            Probability = Request.Negotiation?.Probability;
            CanChooseMilitaryIntelligence = false;
            return;
        }
        try { MilitaryIntelligenceMaximum = source.IntelligenceMaximum(); }
        catch (InvalidDataException missing) { MilitaryIntelligenceUnresolvedInput = missing.Message; }
        try { MilitaryIntelligenceMinimum = source.IntelligenceMinimum(); MilitaryIntelligenceValue = MilitaryIntelligenceMinimum; }
        catch (InvalidDataException missing) { MilitaryIntelligenceUnresolvedInput = missing.Message; }
        RefreshProbability(recomputeBase: true);
    }

    public void SetGold(int amount)
    {
        RequireEdit(); RequireAmount(GoldSelection, amount);
        if (Offer.Gold != 0 && amount == 0) return;
        SetOffer(new(amount, 0));
    }
    public void SetSupplies(int amount)
    {
        RequireEdit(); RequireAmount(SuppliesSelection, amount);
        if (Offer.Supplies != 0 && amount == 0) return;
        SetOffer(new(0, amount));
    }
    public void SelectItem(int id)
    {
        RequireEdit(); RequireChoice(ItemChoices, id);
        SetOffer(new(0, 0, ItemId: id));
    }
    public void SelectArea(int id)
    {
        RequireEdit(); RequireChoice(AreaChoices, id);
        SetOffer(new(0, 0, AreaId: id));
    }
    //8255F0 restores the proposal retained when this negotiation round began.
    //The source's029F caption does not mean an invented all-zero transfer.
    public void ClearCondition() { RequireEdit(); SetOffer(entryProposal); }
    public void SetMilitaryIntelligence(int amount)
    {
        if (CanChooseMilitaryIntelligence != true ||
            MilitaryIntelligenceMinimum is not int minimum || MilitaryIntelligenceMaximum is not int maximum)
            throw new InvalidOperationException(MilitaryIntelligenceUnresolvedInput ?? "Military intelligence is unavailable in this negotiation stage.");
        if (amount < minimum || amount > maximum) throw new ArgumentOutOfRangeException(nameof(amount));
        if (MilitaryIntelligenceValue == amount) return;
        MilitaryIntelligenceValue = amount; submittedIntelligence = amount; RefreshProbability();
    }
    public ConfirmDiplomaticNegotiationOrder BuildOrder(bool accepted) =>
        new(source.PlayerForceId, Request.TaskId, Offer, accepted) { MilitaryIntelligence = submittedIntelligence };

    private void SetOffer(DiplomacyOffer offer)
    {
        if (Offer == offer) return;
        var page = source.ReadPage(offer);
        Offer = offer; Page = page;
        //82DBB0 replaces214 and clears210 after an actual proposal change.
        MilitaryIntelligenceValue = 0; submittedIntelligence = null;
        GoldSelection = source.Amount(offer, false); SuppliesSelection = source.Amount(offer, true);
        RefreshProbability(recomputeBase: true);
    }
    private void RefreshProbability(bool recomputeBase = false)
    {
        Probability = null; ProbabilityUnresolvedInput = null; RequiredMilitaryIntelligence = null;
        try
        {
            if (recomputeBase) probabilityBeforeExpense = source.Probability(Offer, null);
            int before = probabilityBeforeExpense ?? throw new InvalidDataException(ProbabilityUnresolvedInput ?? "diplomacy.negotiation.probability");
            Probability = MilitaryIntelligenceValue is int amount ? source.AdjustedProbability(before, amount) : before;
            bool locked = Stage == DiplomaticNegotiationStage.RecipientDecision ||
                Request.Negotiation is { ForcedResult: not null } or { PersuasionLocked: true } ||
                (source.PlayerForceId == Request.ForceId ? before >= 100 : before <= 0) ||
                MilitaryIntelligenceMinimum is int lowerBound && MilitaryIntelligenceMaximum is int upperBound && lowerBound >= upperBound;
            if (locked) RequiredMilitaryIntelligence = 0;
            else
            {
                try { RequiredMilitaryIntelligence = source.RequiredIntelligence(before); }
                catch (InvalidDataException missing) { MilitaryIntelligenceUnresolvedInput ??= missing.Message; }
            }
        }
        catch (InvalidDataException missing) { ProbabilityUnresolvedInput = missing.Message; }
        CanChooseMilitaryIntelligence = Stage == DiplomaticNegotiationStage.RecipientDecision ||
            Request.Negotiation is { ForcedResult: not null } or { PersuasionLocked: true } ? false :
            MilitaryIntelligenceMinimum is not int minimum || MilitaryIntelligenceMaximum is not int maximum || Probability is null ? null :
            probabilityBeforeExpense is int baseline && minimum < maximum &&
                (source.PlayerForceId == Request.ForceId ? baseline < 100 : baseline > 0);
    }
    private void RequireEdit()
    { if (!CanEditConditions) throw new InvalidOperationException("The current diplomatic stage does not allow editing conditions."); }
    private bool? Allowed(AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> result) =>
        !CanEditConditions ? false : result.Available ? result.Value!.Allowed : null;
    private bool? Allowed(AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>> result) =>
        !CanEditConditions ? false : result.Available ? result.Value!.Count != 0 : null;
    private static void RequireAmount(AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> result, int amount)
    {
        if (!result.Available || result.Value is not { Allowed: true } range)
            throw new InvalidOperationException(result.UnresolvedInput ?? "The payment selector is unavailable.");
        if (amount < range.Minimum || amount > range.Maximum) throw new ArgumentOutOfRangeException(nameof(amount));
    }
    private static void RequireChoice(AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>> result, int id)
    {
        if (!result.Available) throw new InvalidOperationException(result.UnresolvedInput);
        if (!result.Value!.Any(choice => choice.Id == id)) throw new ArgumentOutOfRangeException(nameof(id));
    }
}

internal sealed class DiplomacyNegotiationSource(ArmyCampaignRuleAdapter adapter, GameState state,
    SourceWorldContext current, DiplomacyNegotiationRequest request, PersonnelDiplomacyTask task, int playerForceId)
{
    internal GameState State => state;
    internal DiplomacyNegotiationRequest Request => request;
    internal int PlayerForceId => playerForceId;
    internal DiplomacyNegotiationPage ReadPage(DiplomacyOffer offer) =>
        adapter.ReadDiplomacyNegotiationPageCurrent(state, current, request, playerForceId, offer);
    internal AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>> Items() =>
        adapter.ReadDiplomacyNegotiationItemChoicesCurrent(state, current, task);
    internal AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>> Areas() =>
        adapter.ReadDiplomacyNegotiationAreaChoicesCurrent(state, current, task);
    internal AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> Amount(DiplomacyOffer offer, bool supplies) =>
        adapter.ReadDiplomacyNegotiationAmountCurrent(state, current, task, offer, supplies, playerForceId);
    internal int IntelligenceMinimum() => adapter.ReadDiplomacyNegotiationIntelligenceMinimumCurrent(state);
    internal int IntelligenceMaximum() => adapter.ReadDiplomacyNegotiationIntelligenceBudgetCurrent(state, current, playerForceId);
    internal int RequiredIntelligence(int probability) =>
        adapter.ReadDiplomacyNegotiationRequiredIntelligenceCurrent(state, current, task, probability, playerForceId == task.TargetForceId);
    internal int Probability(DiplomacyOffer offer, int? expense)
        => adapter.ReadDiplomacyNegotiationDisplayedProbabilityCurrent(state, current, task, request, offer, expense, playerForceId);
    internal int AdjustedProbability(int before, int amount) =>
        adapter.ReadDiplomacyNegotiationAdjustedProbabilityCurrent(state, current, task, before, amount, playerForceId == task.TargetForceId);
}

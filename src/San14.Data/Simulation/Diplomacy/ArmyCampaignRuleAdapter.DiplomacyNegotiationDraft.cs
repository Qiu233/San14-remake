using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public DiplomacyNegotiationDraft OpenDiplomacyNegotiationDraft(GameState state,
        DiplomacyNegotiationRequest request, int playerForceId, CampaignAppRandomState? appRandom = null,
        DiplomacyOffer? proposal = null)
    {
        var task = state.PersonnelCommands?.Diplomacies?.GetValueOrDefault(request.TaskId);
        if (task is not { AwaitingNegotiation: true, IsRetiring: false } ||
            playerForceId != task.ForceId && playerForceId != task.TargetForceId)
            throw new ArgumentException("There is no current diplomatic negotiation for this force.");
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        PersonReturnCommands.BindCurrent(state, current);
        task = task with { Negotiation = request.Negotiation ?? task.Negotiation };
        var working = appRandom is null ? state : state with { CampaignAppRandom32 = appRandom };
        return new(new(this, working, current, request, task, playerForceId),
            proposal ?? request.Negotiation?.Proposal ?? task.Negotiation?.Proposal ?? request.InitialOffer);
    }

    internal AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>> ReadDiplomacyNegotiationItemChoicesCurrent(
        GameState state, SourceWorldContext current, PersonnelDiplomacyTask task)
    {
        try
        {
            var q = new DiplomacyQuery(this, state, current, task);
            int ruler = q.Id(q.Ruler(q.Own));
            var choices = new List<DiplomacyNegotiationChoice>();
            //807F90: the actual ruler's item iterator, preserving its list
            //order, then Item4F.bit0. Escrow is not an invented second item.
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item) ?? throw DiplomacyNumericMissing("selector_Item_registry110"))
            {
                int physical = DNeed(entry?.PhysicalSlot, "selector_Item_pointer");
                if (DNeed(current.ReadCaptureItemOwner4A(physical), "selector_Item4A") != ruler) continue;
                var item = state.Items?.GetValueOrDefault(physical) ?? throw DiplomacyNumericMissing("selector_Item_owner");
                if ((DNeed(item.Lifecycle?.RetainedFlags, "selector_Item4F") & 1) == 0) continue;
                string name = state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)?.Name
                    ?? throw DiplomacyNumericMissing("selector_Item_name");
                choices.Add(new(physical, name));
            }
            return AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>>.Known(choices.AsReadOnly());
        }
        catch (InvalidDataException missing)
        { return AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>>.Unknown(missing.Message); }
    }

    internal AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>> ReadDiplomacyNegotiationAreaChoicesCurrent(
        GameState state, SourceWorldContext current, PersonnelDiplomacyTask task)
    {
        try
        {
            var q = new DiplomacyQuery(this, state, current, task);
            var choices = new List<DiplomacyNegotiationChoice>();
            foreach (var area in current.ReadRegisteredAreaReferences() ?? throw DiplomacyNumericMissing("selector_Area_registryB0"))
            {
                int physical = DNeed(area?.PhysicalSlot, "selector_Area_pointer");
                if (!ReadDiplomacyAreaOfferEligibilityCurrent(state, current, physical, q.OwnOrdinal, q.OtherOrdinal)) continue;
                string name = _scenario.Snapshot.Areas.FirstOrDefault(value => value.Slot == physical)?.Name
                    ?? throw DiplomacyNumericMissing("selector_Area_name");
                choices.Add(new(physical, name));
            }
            return AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>>.Known(choices.AsReadOnly());
        }
        catch (InvalidDataException missing)
        { return AreaAdministrationQuery<IReadOnlyList<DiplomacyNegotiationChoice>>.Unknown(missing.Message); }
    }

    internal AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> ReadDiplomacyNegotiationAmountCurrent(
        GameState state, SourceWorldContext current, PersonnelDiplomacyTask task, DiplomacyOffer proposal,
        bool supplies, int playerForceId)
    {
        try
        {
            //Current CE_SC pages/callees and matched offline data prove the
            //same calculator, threshold and amount constants as CE_TC.
            if (Seed.Language is not ("TC" or "SC"))
                throw DiplomacyNumericMissing("selector_amount_locale_" + Seed.Language);
            var q = new DiplomacyQuery(this, state, current, task);
            int home = DNeed(current.ReadPersonFirstLocation(q.Ruler(q.Own)), "selector_ruler_HomeE2");
            var location = BaseLookup.Find(home, current);
            int stock = DNeed(current.ReadBaseResource(location, supplies ? BaseResourceKind.Supplies : BaseResourceKind.Gold), "selector_capital_resource");
            int escrow = supplies ? task.InitialOffer.Supplies : task.InitialOffer.Gold;
            int budget = unchecked(stock + escrow), minimum = supplies ? 10000 : 1000;
            //19's8068A0 uses a calculator, never the Spy probability presets.
            if(task.NativeKind==19)
                return AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Known(
                    new(budget>=minimum && (supplies?task.RequestedOffer.Supplies:task.RequestedOffer.Gold)==0,
                        minimum,budget,Array.Empty<int>()));
            bool allowed = supplies ? unchecked(stock + proposal.Supplies) >= minimum : budget >= minimum;
            //823F10/824E30 call808110/808200 only on actual selection.
            //Binary search the first amount reaching90; no random trial.
            int lower = 0, upper = budget, threshold = -1;
            while (lower <= upper)
            {
                int value = lower + (upper - lower) / 2;
                int rate = ReadDiplomacyNegotiationProposalProbability(q, task,
                    supplies ? new(0, value) : new(value, 0), playerForceId);
                if (rate >= 90) { threshold = value; upper = value - 1; }
                else lower = value + 1;
            }
            int target = threshold >= 0 ? threshold : unchecked(budget - (supplies ? 10000 : 5000));
            //Phase8 preserves the value already selected on the conditions
            //page. This also includes the actual first three return-captives
            //receivers when no money, item or Area takes precedence.
            if (task.Negotiation?.Stage == DiplomaticNegotiationStage.RecipientCounterOffer)
                minimum = Math.Max(minimum, q.NegotiationSelectedValueMinimum(proposal, supplies));
            int middle = 0, high = 0;
            if (minimum < target)
            {
                if (target < unchecked(minimum * 150) / 100)
                    middle = target; //The source leaves the high slot empty.
                else
                {
                    middle = unchecked(unchecked(target - minimum) * 50) / 100 + minimum;
                    high = target;
                }
            }
            int maximum = Math.Max(minimum, threshold >= 0 ? Math.Min(threshold, budget) : budget);
            return AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Known(
                new(allowed, minimum, maximum, Array.AsReadOnly(new[] { minimum, middle, high })));
        }
        catch (InvalidDataException missing)
        { return AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Unknown(missing.Message); }
    }

    internal int ReadDiplomacyNegotiationIntelligenceMinimumCurrent(GameState state) =>
        ReadDiplomacyStaticRuleCurrent(state, "negotiation-intelligence-minimum");
    internal int ReadDiplomacyNegotiationIntelligenceBudgetCurrent(GameState state, SourceWorldContext current, int playerForceId)
    {
        var force = current.ReadForceTableReference(playerForceId) ?? throw DiplomacyNumericMissing("negotiation_player_Force");
        int district = DNeed(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "negotiation_primary_District");
        return ReadDiplomacyDistrictBudget(current, district);
    }
    internal int ReadDiplomacyNegotiationRequiredIntelligenceCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, int probability, bool counterPersuasion) =>
        new DiplomacyQuery(this, state, current, task).NegotiationRequiredIntelligence(probability, counterPersuasion);

    internal int ReadDiplomacyNegotiationProbabilityCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer offer, int militaryIntelligence, bool counterPersuasion)
    {
        var q = new DiplomacyQuery(this, state, current, task);
        return q.NegotiationAdjustedProbability(q.Probability(offer), militaryIntelligence, counterPersuasion);
    }
    internal int ReadDiplomacyNegotiationAdjustedProbabilityCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, int before, int amount, bool counterPersuasion) =>
        new DiplomacyQuery(this, state, current, task).NegotiationAdjustedProbability(before, amount, counterPersuasion);
    internal int ReadDiplomacyNegotiationDisplayedProbabilityCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyNegotiationRequest request, DiplomacyOffer offer, int? expense, int playerForceId)
    {
        var q = new DiplomacyQuery(this, state, current, task);
        int probability = ReadDiplomacyNegotiationProposalProbability(q, task, offer, playerForceId);
        return expense is int amount ? q.NegotiationAdjustedProbability(probability, amount, playerForceId == task.TargetForceId) : probability;
    }

    //806480 and the actual prior expenditure passes used by804830/804A00.
    //An unchanged proposal retains the reached probability; editing uses
    //the native baseline and reapplies the already-consumed phase expenses.
    private int ReadDiplomacyNegotiationProposalProbability(DiplomacyQuery q, PersonnelDiplomacyTask task,
        DiplomacyOffer offer, int playerForceId)
    {
        var reached = task.Negotiation;
        if (reached is not null && reached.Proposal == offer) return reached.Probability;
        int rate;
        if (task.TargetForceId != playerForceId && task.NativeKind == 4 && q.OtherOrdinal is >= 46 and <= 50) rate = 100;
        else if (task.TargetForceId != playerForceId && task.NativeKind == 9 && q.AttackBlockReason() != 0) rate = 0;
        else if (task.TargetForceId != playerForceId && q.Level(q.OwnOrdinal, q.OtherOrdinal) >= 5) rate = 100;
        else rate = q.Probability(offer);
        if (reached?.Stage is DiplomaticNegotiationStage.RecipientCounterOffer or DiplomaticNegotiationStage.SenderFinalOffer)
            rate = q.NegotiationAdjustedProbability(rate, reached.SenderFirstExpense, false);
        if (reached?.Stage == DiplomaticNegotiationStage.SenderFinalOffer)
            rate = q.NegotiationAdjustedProbability(rate, reached.RecipientExpense, true);
        return rate;
    }
    private sealed partial class DiplomacyQuery
    {
        internal int NegotiationSelectedValueMinimum(DiplomacyOffer proposal, bool supplies)
        {
            //322960/CE_SC322BA0: gold, supplies, Item, Area, then captives.
            int value = OfferValue(proposal);
            if (NativeKind == 8 && proposal.Gold <= 0 && proposal.Supplies <= 0 &&
                proposal.ItemId is not (>= 1 and <= 201) && proposal.AreaId is not (>= 1 and <= 500))
            {
                foreach (int id in RequestedCaptives.Take(3))
                {
                    int person = DiplomacyLookupPerson(Current, id);
                    if (Valid(person)) value = unchecked(value + package.ReadDiplomacyCaptiveRansomCurrent(State, Current, person));
                }
            }
            if (value <= 0) return value;
            //322910/CE_SC322B50 receives (value,0) for gold and
            //(0,value) for supplies; these are the established typed rules.
            return supplies ? unchecked((int)((long)Parameter("supplies-value-divisor") * value / 10)) :
                unchecked((int)((long)value * 100 / Parameter("gold-value-percent")));
        }

        internal int NegotiationRequiredIntelligence(int probability, bool counterPersuasion) =>
            NpcRequiredExpense(probability, counterPersuasion);
        internal int NegotiationAdjustedProbability(int probability, int amount, bool counterPersuasion) =>
            NpcAdjustedProbability(probability, amount, counterPersuasion);
    }
}

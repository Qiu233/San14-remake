using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //818770/81BDC0: opening the first real window evaluates its probability,
    //but neither trials nor spends a persuasion budget.
    private DiplomaticNegotiationState ReadInitialDiplomacyNegotiationCurrent(GameState state,
        SourceWorldContext current, PersonnelDiplomacyTask task)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw DiplomacyMissing("negotiation_player");
        bool recipient = player.PhysicalSlot == task.TargetForceId;
        if(task.NativeKind is 7 or 19)throw DiplomacyMissing("direct_reply_saved_negotiation");
        var q = new DiplomacyQuery(this, state, current, task);
        bool? forced = null;
        int probability;
        if (!recipient && task.NativeKind == 4 && q.OtherOrdinal is >= 46 and <= 50)
            { probability = 100; forced = true; }
        else if (!recipient && task.NativeKind == 9 && q.AttackBlockReason() != 0)
            { probability = 0; forced = false; }
        else if (!recipient && q.Level(q.OwnOrdinal, q.OtherOrdinal) >= 5)
            { probability = 100; forced = true; }
        else probability = q.Probability(task.InitialOffer);
        return new(recipient ? DiplomaticNegotiationStage.RecipientDecision : DiplomaticNegotiationStage.SenderPersuasion,
            task.InitialOffer, probability, false, forced, PersuasionLocked: forced.HasValue);
    }

    //815D20: rejecting the recipient's first readonly window invites the
    //sender's actual budget pass, then opens the editable counteroffer window.
    private (GameState State, DiplomaticNegotiationState Negotiation) OpenDiplomacyRecipientCounterofferCurrent(
        GameState state, SourceWorldContext current, PersonnelDiplomacyTask task, DiplomaticNegotiationState negotiation,
        List<CampaignEffectRequest> effects)
    {
        int probability = negotiation.Probability, expense = 0;
        var q = new DiplomacyQuery(this, state, current, task);
        if (!negotiation.PersuasionLocked && probability < 100)
            expense = q.NpcPersuasion(7, 0, ref probability);
        if (expense > 0)
        {
            state = SpendDiplomacyDistrictCurrent(state, current, ForcePrimaryDistrictSource.Resolve(current, q.Own), expense);
            state = WriteDiplomacyNegotiationResponseCurrent(state, current, 0x3507, q.OwnOrdinal, effects);
        }
        else probability = negotiation.Probability; //815D20's no-expense path retains0594.
        return (state, negotiation with { Stage = DiplomaticNegotiationStage.RecipientCounterOffer,
            CanEditConditions = true, PersuasionLocked = false, Probability = probability,
            SenderFirstExpense = expense });
    }

    private (GameState State, DiplomaticNegotiationState Negotiation) SpendDiplomacyPlayerIntelligenceCurrent(
        GameState state, SourceWorldContext current, PersonnelDiplomacyTask task,
        DiplomaticNegotiationState negotiation, DiplomacyOffer offer, int? requested)
    {
        if (!requested.HasValue) return (state, negotiation);
        int amount = requested.Value;
        int minimum = ReadDiplomacyNegotiationIntelligenceMinimumCurrent(state);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw DiplomacyMissing("negotiation_expense_player");
        var district = ForcePrimaryDistrictSource.Resolve(current, player);
        int budget = ReadDiplomacyDistrictBudget(current, DNeed(district.PhysicalSlot, "negotiation_expense_District"));
        if (amount < minimum || amount < 0 || amount > budget)
            throw new ArgumentException("The military-intelligence expenditure is outside the current permitted range.");
        if (negotiation.ForcedResult.HasValue || negotiation.Stage == DiplomaticNegotiationStage.RecipientDecision)
            throw new ArgumentException("This negotiation stage does not admit a persuasion expenditure.");
        bool counter = player.PhysicalSlot == task.TargetForceId;
        int probability = new DiplomacyQuery(this, state, current, task)
            .NegotiationAdjustedProbability(negotiation.Probability, amount, counter);
        if (amount > 0) state = SpendDiplomacyDistrictCurrent(state, current, district, amount);
        negotiation = negotiation with { Probability = probability };
        if (counter) negotiation = negotiation with { RecipientExpense = amount };
        else if (negotiation.Stage == DiplomaticNegotiationStage.SenderPersuasion)
            negotiation = negotiation with { SenderFirstExpense = amount };
        else negotiation = negotiation with { SenderFinalExpense = amount };
        return (state, negotiation);
    }
}

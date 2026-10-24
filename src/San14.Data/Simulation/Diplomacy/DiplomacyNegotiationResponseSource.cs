using San14.Core;

namespace San14.Data;

internal sealed record DiplomacyNegotiationResponseResult(GameState State, DiplomacyOffer Proposal,
    int Probability, bool ProposalChanged, int SenderFirstExpense, int RecipientExpense,
    int SenderFinalExpense, int? NextPlayerStage, DiplomacyTrialResult? Trial,
    int ResponseForceId, int? ResponseMessageRoot);

public sealed partial class ArmyCampaignRuleAdapter
{
    //recipientCounter=false is816990/phase9; true is817840/phase11.
    //The caller persists the actual negotiation and owns presentation. These
    //results are neither a player choice nor a presentation acknowledgement.
    internal DiplomacyNegotiationResponseResult ResolveDiplomacyNegotiationResponseCurrent(
        GameState state, SourceWorldContext current, PersonnelDiplomacyTask task, DiplomacyOffer offer,
        int probability, bool recipientCounter, int senderFirstExpense, int recipientExpense,
        bool persuasionLocked)
    {
        var query = new DiplomacyQuery(this, state, current, task);
        int operation = recipientCounter ? 11 : 9;
        var senderDistrict = ForcePrimaryDistrictSource.Resolve(current, query.Own);
        var recipientDistrict = ForcePrimaryDistrictSource.Resolve(current, query.Other);
        if (recipientCounter)
        {
            //817840 debits the envoy's current E0 District.806110's budget
            //query still independently uses the Force primary District.
            byte raw = DNeed(current.ReadPersonDistrictSlot(query.Executor), "response_executor_DistrictE0");
            senderDistrict = current.ReadDistrictTableReference(raw < 52 ? raw : 0)
                ?? throw DiplomacyNumericMissing("response_executor_District_pointer");
        }
        //805970 precedes persuasion, including when persuasion is locked.
        var candidateOffer = query.CounterProposalCurrent(operation, task.InitialOffer, offer,
            probability, out bool changed);
        int candidateProbability = probability;
        if (changed)
        {
            candidateProbability = query.CounterBaseline(candidateOffer, !recipientCounter);
            candidateProbability = query.NegotiationAdjustedProbability(candidateProbability, senderFirstExpense, false);
            if (recipientCounter)
                candidateProbability = query.NegotiationAdjustedProbability(candidateProbability, recipientExpense, true);
        }
        int expense = 0;
        if (!persuasionLocked && (task.NativeKind is 4 or 5 or 9) &&
            (recipientCounter ? candidateProbability < 100 : candidateProbability > 0))
            expense = query.NpcPersuasion(operation, recipientCounter ? senderFirstExpense : 0,
                ref candidateProbability);

        GameState working = state;
        int finalExpense = 0;
        int respondingForce = recipientCounter ? query.OwnOrdinal : query.OtherOrdinal;
        int? responseRoot = null;
        bool adoptedChange = false;
        //Both phases disregard a candidate rate-only clamp when no actual
        //proposal/expense was produced. The saved0594 remains the old value.
        if (candidateProbability != probability && (changed || expense > 0))
        {
            if (changed) { offer = candidateOffer; adoptedChange = true; }
            if (expense > 0)
                working = SpendDiplomacyDistrictCurrent(working, current,
                    recipientCounter ? senderDistrict : recipientDistrict, expense);
            probability = candidateProbability;
            responseRoot = changed ? (expense > 0 ? 0x3509 : 0x3508) : 0x3507;
            if (expense > 0)
            {
                if (recipientCounter) finalExpense = expense;
                else recipientExpense = expense;
            }
            if (!recipientCounter)
                return new(working, offer, probability, adoptedChange, senderFirstExpense, recipientExpense,
                    0, 10, null, respondingForce, responseRoot);
        }
        //Phase9 with no effective counter goes straight to3254D0. Phase11
        //does so after either branch, using the retained current probability.
        var trial = DrawDiplomacyFinalProbabilityCurrent(working, current, task, probability,
            () => ReadArmyDestructionSampler(working, current)
                ?? throw DiplomacyNumericMissing("negotiation_response_trial_sampler"));
        return new(working, offer, probability, adoptedChange, senderFirstExpense, recipientExpense,
            finalExpense, null, trial, respondingForce, responseRoot);
    }

    private sealed partial class DiplomacyQuery
    {
        internal DiplomacyOffer CounterProposalCurrent(int operation, DiplomacyOffer originalOffer,
            DiplomacyOffer offer, int probability, out bool changed)
        {
            changed = false;
            if (NativeKind is not (4 or 5 or 9)) return offer;
            //The source resolves both real primary Districts before the
            //proposal comparisons. They are shared with later expense writes.
            _ = ForcePrimaryDistrictSource.Resolve(Current, Own);
            _ = ForcePrimaryDistrictSource.Resolve(Current, Other);
            bool itemValid = CounterItemValid(offer.ItemId);
            bool areaValid = CounterAreaValid(offer.AreaId);
            if (operation == 9)
            {
                int low = NpcParameter("low-probability-threshold");
                int other = OtherOrdinal;
                int relation = other < 52 ? Relation(OwnOrdinal, other) : 0;
                if (relation < NpcParameter("low-probability-relation-threshold") ||
                    probability <= low || probability >= 100 || itemValid || areaValid) return offer;
                var ownHome = BaseLookup.Find(DNeed(Current.ReadPersonFirstLocation(Ruler(Own)),
                    "counter_sender_ruler_homeE2"), Current);
                var otherHome = BaseLookup.Find(DNeed(Current.ReadPersonFirstLocation(Ruler(Other)),
                    "counter_recipient_ruler_homeE2"), Current);
                int value = OfferValue(originalOffer);
                int min = CounterPercent(value, "minimum-value-percent");
                int max = CounterPercent(value, "maximum-value-percent");
                int lowValue = CounterBound(min, max, CounterPercent(value, "ratio-low-value-percent"));
                int middleValue = CounterBound(min, max, CounterPercent(value, "ratio-middle-value-percent"));
                int highValue = CounterBound(min, max, CounterPercent(value, "ratio-high-value-percent"));
                //Both ratios are evaluated, with the second recipient getter
                //after the sender getter. Escrow is not added to these stocks.
                float goldRatio = CounterResource(otherHome, true) < 1 ? float.MaxValue :
                    (float)CounterResource(ownHome, true) / (float)CounterResource(otherHome, true);
                float suppliesRatio = CounterResource(otherHome, false) < 1 ? float.MaxValue :
                    (float)CounterResource(ownHome, false) / (float)CounterResource(otherHome, false);
                int selected;
                if (goldRatio >= 2.5f || suppliesRatio >= 2.5f) selected = highValue;
                else if (goldRatio >= 2f || suppliesRatio >= 2f) selected = middleValue;
                else if (goldRatio >= 1.5f || suppliesRatio >= 1.5f) selected = lowValue;
                else return offer;
                selected = CounterBound(min, max, selected);
                int gold = offer.Gold, supplies = offer.Supplies;
                if (originalOffer.Gold > 0)
                    gold = selected > 0 ? unchecked((int)((long)selected * 100 / Parameter("gold-value-percent"))) : 0;
                if (originalOffer.Supplies > 0)
                    supplies = selected > 0 ? unchecked((int)((long)Parameter("supplies-value-divisor") * selected / 10)) : 0;
                changed = true;
                return offer with { Gold = gold, Supplies = supplies };
            }
            //Initial offer value is read before the candidate offer value.
            //The latter uses the identities obtained from actual valid pointers.
            int originalValue = OfferValue(originalOffer);
            var candidate = offer with { ItemId = itemValid ? offer.ItemId : null,
                AreaId = areaValid ? offer.AreaId : null };
            int candidateValue = OfferValue(candidate);
            if (candidateValue == originalValue ||
                !(CounterPercent(originalValue, "restore-upper-value-percent") < candidateValue ||
                    probability <= CounterParameter("restore-upper-probability-threshold")) ||
                !(CounterPercent(originalValue, "restore-lower-value-percent") < candidateValue ||
                    probability <= CounterParameter("restore-lower-probability-threshold"))) return offer;
            changed = true;
            if ((originalOffer.Gold != 0 || originalOffer.Supplies != 0) && (itemValid || areaValid))
                return offer with { Gold = originalOffer.Gold, Supplies = originalOffer.Supplies,
                    ItemId = null, AreaId = null };
            return originalOffer;
        }

        internal int CounterBaseline(DiplomacyOffer offer, bool shortcuts)
        {
            //806480(selector1) shortcuts belong only to phase9's requery.
            if (shortcuts)
            {
                if (NativeKind == 4 && OtherOrdinal is >= 46 and <= 50) return 100;
                if (NativeKind == 9 && AttackBlockReason() != 0) return 0;
                if (Level(OwnOrdinal, OtherOrdinal) >= 5) return 100;
            }
            return Probability(offer);
        }
        private int CounterPercent(int value, string name) => unchecked(value * CounterParameter(name)) / 100;
        private int CounterParameter(string name) => ReadDiplomacyCounterParameterCurrent(State, name);
        private static int CounterBound(int min, int max, int value) => min > value ? min : value > max ? max : value;
        private int CounterResource(BaseReference home, bool gold)
        {
            return ReadDiplomacyBaseResourceCurrent(Current, home,
                gold ? BaseResourceKind.Gold : BaseResourceKind.Supplies);
        }

        private bool CounterItemValid(int? identity)
        {
            if (identity is null) return false;
            var item = Current.ReadRegistryTableReference(RegistryKind.Item, identity is >= 0 and < 202 ? identity.Value : 0)
                ?? throw DiplomacyNumericMissing("counter_proposal_Item_pointer");
            return DNeed(Current.ReadRegistryObjectNativeValidity(item), "counter_proposal_Item_validity");
        }
        private bool CounterAreaValid(int? identity)
        {
            if (identity is null) return false;
            var area = Current.ReadAreaTableReference(identity is >= 0 and < 501 ? identity.Value : 0)
                ?? throw DiplomacyNumericMissing("counter_proposal_Area_pointer");
            return DNeed(Current.ReadAreaNativeValidity(area), "counter_proposal_Area_validity");
        }
    }
}

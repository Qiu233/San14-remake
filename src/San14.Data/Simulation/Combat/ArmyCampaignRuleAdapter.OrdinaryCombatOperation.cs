using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal PersonNormalArmyRewardResult ApplyBorrowedNormalArmyReward(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int personId, int kind, int amount)
        => _personReward.TryApplyBorrowedCurrent(state, current, context, this, personId, kind, amount,
            (reached, person, dimension, requested, report) => _growthSource.TryApplyWithinArmyOperation(
                reached, current, _growthQueries, person, dimension, requested, report),
            (reached, person, requested) => _pendingMerit.TryApplyWithinArmyOperation(reached, current, person, requested));

    internal ArmyMoraleAdjustmentResult ApplyBorrowedNormalArmyMorale(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int armyId, int adjustment)
    {
        _baseCapacityInputs.BindCurrent(state, current);
        var result = new ArmyMoraleAdjustmentSource(context, current, Revision)
            .TryApplyWithinArmyOperation(state, armyId, adjustment);
        if (result.Outcome == ArmyMoraleAdjustmentOutcome.Committed)
        {
            var army = result.State.Units[armyId];
            current.WriteArmyReturnMorale(army.NativeArmyOrigin!.NativeSlotIndex!.Value, result.Receipt!.After1A);
        }
        return result;
    }

    internal NormalArmyForceResultApplication ApplyBorrowedNormalForceResult(GameState state,
        SourceWorldContext current, int attackerForce, int targetForce, int cappedLoss, bool killed,
        INormalArmyForceResultDefinitionQueries registry)
        => NormalArmyForceResultSource.ApplyWithinArmyOperation(state, attackerForce, targetForce,
            cappedLoss, killed, current, registry, _forceResultSeed, Revision);

    internal NormalArmyPersonalityApplication ApplyBorrowedNormalPersonality(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int targetArmyId)
    {
        if (!state.Units.TryGetValue(targetArmyId, out var target))
            return new(NormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved, state, null, [], "current.target_Commander");
        var result = NormalArmyPersonalityApplicationSource.TryApply(state, target, context, current, Revision);
        if (result.Outcome == NormalArmyPersonalityApplicationOutcome.Committed)
        {
            int person = current.ResolveOfficerSource(target.OfficerId)
                ?? throw new InvalidDataException("ordinary_army_combat_cut.personality_Person_identity");
            OfficerTraitActivationSourceAdapter.Write(current, person, result.State.Officers[target.OfficerId].TraitActivation);
        }
        return result;
    }

    internal NormalArmyPairAftermathResult ApplyBorrowedNormalAftermath(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, NormalArmyCombatPair pair)
        => NormalArmyPairAftermath.TryApply(state, pair, current, context, _normalAftermathInputs);
}

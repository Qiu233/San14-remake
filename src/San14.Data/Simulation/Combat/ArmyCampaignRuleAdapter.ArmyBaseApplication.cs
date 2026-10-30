using San14.Core;

namespace San14.Data;

internal sealed record BorrowedMixedPairApplication(GameState State, ushort? SuspendedWord20 = null,
    AutomaticTacticExecutionContinuation? AutomaticExecution = null)
{
    internal BaseAutomaticTacticExecutionContinuation? BaseAutomaticExecution { get; init; }
    internal bool PendingAutomaticFatalFeedback { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One actual Army-to-Base pair in169CE0's mixed ordering. The
    /// phase cache stays frozen, while every eligibility and quantity read uses
    /// the same current after earlier pairs. Unknown discards this operation.</summary>
    private BorrowedMixedPairApplication CompleteBorrowedArmyBasePair(GameState state, SourceWorldContext current,
        NormalCombatMixedPair pair, byte? baseDivisor,
        TacticRemakeCurrentBattleQueries descriptorQueries, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate operationDate, long operationRevision, List<CampaignEffectRequest> effects)
    {
        var armyPointer = current.ReadArmyUnitTableReference(pair.Actor.PhysicalSlot)
            ?? throw Unresolved("Army_lookup");
        if (current.ReadArmyUnitNativeValidity(armyPointer) is not bool actorValid)
            throw Unresolved("Army_validity");
        if (!actorValid) return new(state);
        if (OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, pair.Actor.PhysicalSlot)
            is not bool actorExcluded) throw Unresolved("Army_manager233560");
        if (actorExcluded) return new(state);
        if (pair.Actor.UnitId is not int attackerId || !state.Units.TryGetValue(attackerId, out var attacker) ||
            attacker.NativeArmyOrigin?.NativeSlotIndex != pair.Actor.PhysicalSlot)
            throw Unresolved("Army_identity");
        var target = (pair.Target.Kind == 5
            ? current.ReadCityTableReference(pair.Target.PhysicalSlot <= 51 ? pair.Target.PhysicalSlot : 0)
            : current.ReadGateTableReference(pair.Target.PhysicalSlot <= 10 ? pair.Target.PhysicalSlot : 0))
            ?? throw Unresolved("Base_lookup");
        if (current.ReadBaseNativeValidity(target) is not bool baseValid)
            throw Unresolved("Base_validity");
        int objectIndex = pair.Target.Kind == 5
            ? pair.Target.PhysicalSlot is >= 0 and <= 51 ? pair.Target.PhysicalSlot : 0
            : pair.Target.PhysicalSlot is >= 1 and <= 10 ? pair.Target.PhysicalSlot + 51 : 0;
        if (baseValid)
        {
            if (OwnedArmyDestructionCurrent.ReadBaseExcluded2334B0(state, current, objectIndex)
                is not bool baseExcluded) throw Unresolved("Base_manager2334B0");
            if (baseExcluded) return new(state);
        }
        //An invalid Base receiver does not itself reject169CE0. The separate
        //Object allocation still decides its native eligibility and Force13.
        var targetObject = current.ReadObjectTableReference(objectIndex)
            ?? throw Unresolved("Base_Object_lookup");
        if (current.ReadObjectNativeValidity(targetObject) is not bool objectValid)
            throw Unresolved("Base_Object_validity");
        if (!objectValid) return new(state);
        var pool = current.ReadOwnedCurrentListPool() ?? throw Unresolved("current_pool");
        var directory = current.CurrentMapObjectDirectory ?? throw Unresolved("Object_directory");
        int objectPhysical = targetObject.PhysicalSlot ?? throw Unresolved("Object_receiver");
        var receiver = directory.ReceiverAtPhysical(objectPhysical);
        var pending = pool.PendingObjectRetirements ?? throw Unresolved("pending_F0");
        foreach (var retired in pending)
            if (retired == receiver) return new(state);
        int actorForce = current.ReadArmyUnitRawForceSlot(armyPointer);
        int targetForce = current.ReadObjectForceSlot13(targetObject) ?? throw Unresolved("Object_Force13");
        if (ArmyLogisticsPreparation.ReadNativeRelationGate(state, actorForce, targetForce) is not bool hostile)
            throw Unresolved("relation2CA650");
        if (!hostile) return new(state);
        if (current.ArmyReturnTransferMode is not { Present: false })
            throw Unresolved("challenge_pair_gate497F20");

        //174C50 is independent of the Common/Defense frame. Its return8
        //bypasses stages2..7 but still enters174EC0 below, just like169CE0.
        var wall = ApplyBorrowedArmyBaseWall(state, current, pair, sampler, effects);
        var working = wall.State;
        //Both City report channels identify the selected Base location. The
        //actual linked Object physical receiver remains in the wall receipt.
        if (wall.Receipt is { } wallDamage)
            working = working with { NativeSubdayReports = [.. working.NativeSubdayReports ?? [],
                new("normal_city_wall_damage", working.NativeClock!.UpdateOrdinal, attackerId,
                    pair.Actor.ForceSlot, objectIndex, wallDamage.AppliedLoss)] };
        if (!wall.SkipTroopStages)
        {
            if (!working.Units.TryGetValue(attackerId, out attacker))
                throw Unresolved("post_wall_Army_receiver");
            byte? armyDivisor = descriptorQueries.ReadFinalDescriptorDivisor(working, attacker);
            _baseCapacityInputs.BindCurrent(working, current);
            var resolved = ResolveBorrowedArmyBaseDamage(working, current, attackerId, target,
                armyDivisor, baseDivisor, sampler);
            var damage = resolved.Receipt ?? throw Unresolved(resolved.UnresolvedOwner ?? "damage");
            if (TacticRemakeCurrentBattleControl.Read(working, 0x48) is not bool tutorial)
                throw Unresolved("automatic_control48");
            bool activated = false;
            ushort tacticWord22 = 0;
            if (!tutorial && damage.Damage.NativeFrameWord20 < pair.Target.Troops)
            {
                var context = Context(current, sampler);
                bool? automatic = context.ResolveAutomaticArmyBaseGate(working,
                    attacker, pair.Target.Kind, checked((ushort)pair.Target.PhysicalSlot));
                if (automatic is null) throw Unresolved("automatic_Army_Base_permission");
                if (automatic.Value)
                {
                    var activation = CompleteBorrowedArmyBaseAutomatic(working, current, context,
                        pair, sampler, operationDate, operationRevision, effects);
                    working = activation.State; activated = activation.Activated;
                    if (HasNativeCampaignStop(working)) return new(working);
                    if (activation.Continuation is { } pendingActivation)
                        return new(working, damage.Damage.NativeFrameWord20, pendingActivation);
                    tacticWord22 = activation.PrimaryTroopDamageWord22
                        ?? throw Unresolved("automatic_completed_frame22");
                }
            }
            return CompleteBorrowedArmyBaseApplicationTail(working, current, pair,
                damage.Damage.NativeFrameWord20, tacticWord22, activated, sampler,
                operationDate, operationRevision, effects);
        }
        return new(CompleteBorrowedArmyBaseAftermath(working, current, pair, sampler));

        InvalidDataException Unresolved(string owner) => ArmyBasePairMissing(pair, owner);
    }

    private BorrowedMixedPairApplication ResumeBorrowedArmyBasePairAfterAutomatic(GameState state,
        SourceWorldContext current, NormalCombatMixedPair pair, ushort frame20,
        AutomaticTacticExecutionContinuation saved, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate operationDate, long revision, List<CampaignEffectRequest> effects)
    {
        var activation = ResumeBorrowedSelectedAutomatic(state, current, Context(current, sampler),
            pair.Target, saved, sampler, operationDate, revision, effects);
        if (HasNativeCampaignStop(activation.State)) return new(activation.State);
        if (activation.Continuation is { } pending)
            return new(activation.State, frame20, pending);
        return CompleteBorrowedArmyBaseApplicationTail(activation.State, current, pair, frame20,
            activation.Frame22?.Word22After ?? throw ArmyBasePairMissing(pair, "resume_frame22"), true, sampler,
            operationDate, revision, effects);
    }

    private BorrowedMixedPairApplication CompleteBorrowedArmyBaseApplicationTail(GameState working, SourceWorldContext current,
        NormalCombatMixedPair pair, ushort frame20, ushort tacticWord22, bool activated,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate operationDate,
        long operationRevision, List<CampaignEffectRequest> effects)
    {
        int attackerId = pair.Actor.UnitId ?? throw ArmyBasePairMissing(pair, "tail_Army_identity");
        int objectIndex = pair.Target.Kind == 5 ? pair.Target.PhysicalSlot : pair.Target.PhysicalSlot + 51;
        var target = (pair.Target.Kind == 5 ? current.ReadCityTableReference(pair.Target.PhysicalSlot)
            : current.ReadGateTableReference(pair.Target.PhysicalSlot))
            ?? throw ArmyBasePairMissing(pair, "tail_Base_pointer");
        if (Stage7PermitsBaseApplication(working))
        {
            var request = NormalArmyCombatApplicationSourceMath.BuildNormalRequestedLoss(frame20, tacticWord22);
            var applied = ApplyBorrowedNormalBaseFatalApplication(working, attackerId, objectIndex,
                request.RequestedLoss, sampler, current, operationDate: operationDate,
                operationEffects: effects, immediateRevision: operationRevision);
            if (applied.Outcome == NormalCityApplicationOutcome.CurrentInputsUnresolved || applied.Receipt is null)
                throw Unresolved(applied.UnresolvedOwner ?? "application178EC0");
            working = applied.State;
            //174A30 updates fatal feedback and its report only after178EC0
            //returns. A suspended Full capture has not reached either write.
            //The original automatic frame24 is retained independently of the
            //now-completed automatic execution, without replaying frame22.
            if (working.BaseCaptureContinuation is not null || AutomaticEmperorCaptureLifecycle.HasCaptureTail(working))
                return new(working, request.FrameWord20After)
                    { PendingAutomaticFatalFeedback = activated && applied.Receipt.NativeReturn };
            if (HasNativeCampaignStop(working)) return new(working);
            working = CompleteBorrowedArmyBasePostApplication(working, pair,
                request.FrameWord20After, activated && applied.Receipt.NativeReturn);
        }
        return new(CompleteBorrowedArmyBaseAftermath(working, current, pair, sampler));

        bool Stage7PermitsBaseApplication(GameState reached)
        {
            if (pair.Target.Kind != 5) return true;
            if (current.ReadConditionBaseSpecialGovernor(target) is not bool special)
                throw Unresolved("application_special_Governor232950");
            if (!special) return true;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(reached, current)
                ?? throw Unresolved("application_current_player_pointer");
            if (current.ReadForceNativeValidity(player) is not bool valid)
                throw Unresolved("application_current_player_validity");
            if (!valid) return false;
            int playerId = current.ReadForcePhysicalSlot(player)
                ?? throw Unresolved("application_current_player_identity");
            //174A30 compares the captured descriptor Force10. This differs
            //from16A260's fresh Army getter and must precede minimum-one.
            return pair.Actor.ForceSlot == playerId;
        }

        InvalidDataException Unresolved(string owner) => ArmyBasePairMissing(pair, owner);
    }

    private GameState CompleteBorrowedArmyBasePairAfterCapture(GameState state, SourceWorldContext current,
        NormalCombatMixedPair pair, ushort reportWord20, bool pendingAutomaticFatalFeedback,
        NativeArmyCombatCycleSampler sampler)
    {
        state = CompleteBorrowedArmyBasePostApplication(state, pair, reportWord20, pendingAutomaticFatalFeedback);
        return CompleteBorrowedArmyBaseAftermath(state, current, pair, sampler);
    }

    private GameState CompleteBorrowedArmyBasePostApplication(GameState state, NormalCombatMixedPair pair,
        ushort reportWord20, bool automaticFatalFeedback)
    {
        if (automaticFatalFeedback) state = MarkBorrowedAutomaticTacticFatalFeedback(state);
        int attackerId = pair.Actor.UnitId ?? throw ArmyBasePairMissing(pair, "post_application_Army_identity");
        int objectIndex = pair.Target.Kind == 5 ? pair.Target.PhysicalSlot : pair.Target.PhysicalSlot + 51;
        return state with { NativeSubdayReports = [.. state.NativeSubdayReports ?? [],
            new("normal_city_damage", state.NativeClock!.UpdateOrdinal, attackerId,
                pair.Actor.ForceSlot, objectIndex, reportWord20)] };
    }

    private GameState CompleteBorrowedArmyBaseAftermath(GameState state, SourceWorldContext current,
        NormalCombatMixedPair pair, NativeArmyCombatCycleSampler sampler)
    {
        var aftermath = NormalArmyPairAftermath.TryApplyMixed(state, pair.Actor, pair.Target,
            current, Context(current, sampler), _normalAftermathInputs);
        if (aftermath.Outcome == NormalArmyPairAftermathOutcome.CurrentInputsUnresolved)
            throw ArmyBasePairMissing(pair, aftermath.MissingOwner ?? "aftermath174EC0");
        return aftermath.State;
    }

    private static InvalidDataException ArmyBasePairMissing(NormalCombatMixedPair pair, string owner) => new(
        $"ordinary_army_combat_cut.Base_target.{owner}.source27:{pair.Actor.PhysicalSlot}.target{pair.Target.Kind}:{pair.Target.PhysicalSlot}");
}

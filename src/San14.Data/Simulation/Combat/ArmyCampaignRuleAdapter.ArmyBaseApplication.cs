using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One actual Army-to-Base pair in169CE0's mixed ordering. The
    /// phase cache stays frozen, while every eligibility and quantity read uses
    /// the same current after earlier pairs. Unknown discards this operation.</summary>
    private GameState CompleteBorrowedArmyBasePair(GameState state, SourceWorldContext current,
        NormalCombatMixedPair pair, byte? baseDivisor,
        TacticRemakeCurrentBattleQueries descriptorQueries, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate operationDate, List<CampaignEffectRequest> effects)
    {
        var armyPointer = current.ReadArmyUnitTableReference(pair.Actor.PhysicalSlot)
            ?? throw Unresolved("Army_lookup");
        if (current.ReadArmyUnitNativeValidity(armyPointer) is not bool actorValid)
            throw Unresolved("Army_validity");
        if (!actorValid) return state;
        if (OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, pair.Actor.PhysicalSlot)
            is not bool actorExcluded) throw Unresolved("Army_manager233560");
        if (actorExcluded) return state;
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
            if (baseExcluded) return state;
        }
        //An invalid Base receiver does not itself reject169CE0. The separate
        //Object allocation still decides its native eligibility and Force13.
        var targetObject = current.ReadObjectTableReference(objectIndex)
            ?? throw Unresolved("Base_Object_lookup");
        if (current.ReadObjectNativeValidity(targetObject) is not bool objectValid)
            throw Unresolved("Base_Object_validity");
        if (!objectValid) return state;
        var pool = current.ReadOwnedCurrentListPool() ?? throw Unresolved("current_pool");
        var directory = current.CurrentMapObjectDirectory ?? throw Unresolved("Object_directory");
        int objectPhysical = targetObject.PhysicalSlot ?? throw Unresolved("Object_receiver");
        var receiver = directory.ReceiverAtPhysical(objectPhysical);
        var pending = pool.PendingObjectRetirements ?? throw Unresolved("pending_F0");
        foreach (var retired in pending)
            if (retired == receiver) return state;
        int actorForce = current.ReadArmyUnitRawForceSlot(armyPointer);
        int targetForce = current.ReadObjectForceSlot13(targetObject) ?? throw Unresolved("Object_Force13");
        if (ArmyLogisticsPreparation.ReadNativeRelationGate(state, actorForce, targetForce) is not bool hostile)
            throw Unresolved("relation2CA650");
        if (!hostile) return state;
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
                        pair, sampler, operationDate, effects);
                    working = activation.State; activated = activation.Activated;
                    tacticWord22 = activation.PrimaryTroopDamageWord22;
                }
            }
            if (Stage7PermitsBaseApplication(working))
            {
                //16FE60 consumes the real C8 Base tree after activation. A
                //deferred primary opcode1 request enters stage7 exactly once;
                //wall opcode18 has no troop-tree contribution.
                var request = NormalArmyCombatApplicationSourceMath.BuildNormalRequestedLoss(
                    damage.Damage.NativeFrameWord20, tacticWord22);
                var applied = ApplyBorrowedNormalBaseFatalApplication(working, attackerId, objectIndex,
                    request.RequestedLoss, sampler, current);
                if (applied.Outcome == NormalCityApplicationOutcome.CurrentInputsUnresolved || applied.Receipt is null)
                    throw Unresolved(applied.UnresolvedOwner ?? "application178EC0");
                working = applied.State;
                if (activated && applied.Receipt.NativeReturn)
                    working = MarkBorrowedAutomaticTacticFatalFeedback(working);
                //174A30 accumulates the word after its minimum-one write,
                //independently from the signed Base troop cap and wall loss.
                working = working with { NativeSubdayReports = [.. working.NativeSubdayReports ?? [],
                    new("normal_city_damage", working.NativeClock!.UpdateOrdinal, attackerId,
                        pair.Actor.ForceSlot, objectIndex, request.FrameWord20After)] };
            }
        }
        var aftermath = NormalArmyPairAftermath.TryApplyMixed(working, pair.Actor, pair.Target,
            current, Context(current, sampler), _normalAftermathInputs);
        if (aftermath.Outcome == NormalArmyPairAftermathOutcome.CurrentInputsUnresolved)
            throw Unresolved(aftermath.MissingOwner ?? "aftermath174EC0");
        return aftermath.State;

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

        InvalidDataException Unresolved(string owner) => new(
            $"ordinary_army_combat_cut.Base_target.{owner}.source27:{pair.Actor.PhysicalSlot}.target{pair.Target.Kind}:{pair.Target.PhysicalSlot}");
    }
}

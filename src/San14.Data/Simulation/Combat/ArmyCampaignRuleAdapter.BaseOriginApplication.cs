using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One reached Base-origin/Army-target pair in the mixed order.
    /// All writes borrow the same private operation; Unknown rejects that
    /// operation, including reached numeric and presentation prefixes.</summary>
    private GameState CompleteBorrowedBaseArmyPair(GameState state, SourceWorldContext current,
        NormalCombatMixedPair pair, byte? baseDivisor, TacticRemakeCurrentBattleQueries descriptorQueries,
        NativeArmyCombatCycleSampler sampler, List<CampaignEffectRequest> effects)
    {
        var origin = (pair.Actor.Kind == 5
            ? current.ReadCityTableReference(pair.Actor.PhysicalSlot <= 51 ? pair.Actor.PhysicalSlot : 0)
            : current.ReadGateTableReference(pair.Actor.PhysicalSlot <= 10 ? pair.Actor.PhysicalSlot : 0))
            ?? throw Missing("Base_lookup");
        if (current.ReadBaseNativeValidity(origin) is not bool baseValid) throw Missing("Base_validity");
        //169CE0 only reads2334B0 after a valid Base pointer. Its next
        //independent admission uses the descriptor's Object lookup, not4E.
        int objectIndex = pair.Actor.Kind == 5
            ? pair.Actor.PhysicalSlot is >= 0 and <= 51 ? pair.Actor.PhysicalSlot : 0
            : pair.Actor.PhysicalSlot is >= 1 and <= 10 ? pair.Actor.PhysicalSlot + 51 : 0;
        if (baseValid)
        {
            if (OwnedArmyDestructionCurrent.ReadBaseExcluded2334B0(state, current, objectIndex) is not bool baseExcluded)
                throw Missing("Base_manager");
            if (baseExcluded) return state;
        }
        var sourceObject = current.ReadObjectTableReference(objectIndex) ?? throw Missing("Base_Object_lookup");
        if (current.ReadObjectNativeValidity(sourceObject) is not bool objectValid) throw Missing("Base_Object_validity");
        if (!objectValid) return state;
        var pendingObjects = current.ReadOwnedCurrentListPool()?.PendingObjectRetirements ?? throw Missing("pending_F0");
        var objectDirectory = current.CurrentMapObjectDirectory ?? throw Missing("Object_directory");
        int objectPhysical = sourceObject.PhysicalSlot ?? throw Missing("Object_receiver");
        var objectReceiver = objectDirectory.ReceiverAtPhysical(objectPhysical);
        foreach (var pending in pendingObjects)
            if (pending == objectReceiver) return state;
        int sourceForce = current.ReadObjectForceSlot13(sourceObject) ?? throw Missing("Base_Object_Force13");
        var targetReference = new ArmyUnitReference(pair.Target.PhysicalSlot);
        if (current.ReadArmyUnitNativeValidity(targetReference) is not bool armyValid) throw Missing("Army_validity");
        if (!armyValid) return state;
        if (pair.Target.UnitId is not int targetId || !state.Units.TryGetValue(targetId, out var target) ||
            target.NativeArmyOrigin?.NativeSlotIndex != pair.Target.PhysicalSlot)
            throw Missing("Army_identity");
        if (OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, pair.Target.PhysicalSlot) is not bool excluded)
            throw Missing("Army_manager");
        if (excluded) return state;
        int targetForce = current.ReadArmyUnitRawForceSlot(targetReference);
        if (ArmyLogisticsPreparation.ReadNativeRelationGate(state, sourceForce, targetForce) is not bool hostile)
            throw Missing("relation");
        if (!hostile) return state;
        //The real absent570850 service bypasses497F20. A challenge-present
        //pair needs that service's own fields; tutorial flags are no substitute.
        if (current.ArmyReturnTransferMode is not { Present: false })
            throw Missing("challenge_pair_gate497F20");

        byte? targetDivisor = descriptorQueries.ReadFinalDescriptorDivisor(state, target);
        _baseCapacityInputs.BindCurrent(state, current);
        var resolved = ResolveBorrowedBaseOriginDamage(state, current, origin,
            targetId, baseDivisor, targetDivisor, sampler);
        if (resolved.Receipt is not { } damage) throw Missing(resolved.UnresolvedOwner ?? "damage");
        //16FE60 keeps the fresh frame22 zero only through its real skip
        //branches. A Base is not automatically a no-tactic actor.
        if (TacticRemakeCurrentBattleControl.Read(state, 0x48) is not bool blockTactic)
            throw Missing("automatic_control48");
        if (!blockTactic && damage.Damage.NativeFrameWord20 < pair.Target.Troops)
        {
            var fields = current.ReadCaptureBaseThreatFields(origin) ?? throw Missing("automatic_Base_fields");
            if (fields.Byte48 is not byte enabled) throw Missing("automatic_Base48");
            if (enabled != 0)
            {
                ushort governorId = current.ReadArmyReturnBaseGovernor2E(origin) ?? throw Missing("automatic_governor2E");
                var governor = current.ReadPersonLookupReference(governorId <= 6000 ? governorId : 0)
                    ?? throw Missing("automatic_governor_lookup");
                bool governorValid = governor.PhysicalSlot is int governorPhysical
                    ? current.ReadPersonNativeValidity(governorPhysical) ?? throw Missing("automatic_governor_validity") : false;
                if (governorValid)
                {
                    int person = governor.PhysicalSlot!.Value;
                    var position = PersonPositionRepair.DecodeSecondLocation(current.ReadPersonSecondLocation(person)
                        ?? throw Missing("automatic_governor_E4"));
                    ushort link = current.ReadBaseObjectLink(origin) ?? throw Missing("automatic_Base4E");
                    bool matchingPosition = link <= 51 ? position == new NativeLocation(5, link)
                        : link <= 61 ? position == new NativeLocation(6, link - 51) : true;
                    if (matchingPosition)
                        throw Missing("automatic_Base_governor161_and_selection");
                }
            }
        }
        var request = NormalArmyCombatApplicationSourceMath.BuildNormalRequestedLoss(damage.Damage.NativeFrameWord20, 0);
        var context = Context(current, sampler);
        TacticRemakeCurrentContext ReadLatest(GameState reached)
        {
            OwnedArmyDestructionCurrent.BindOwnedCurrentContext(reached, current);
            return context;
        }
        var application = TryApplyBorrowedBaseArmyNumbers(state, origin, targetId, request.RequestedLoss,
            ReadLatest, current);
        if (application.Outcome == NormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved)
            throw Missing(application.MissingPhase ?? "application");
        if (application.Outcome == NormalArmyApplicationPrefixOutcome.ZeroTroopsCleanupRequired)
            throw Missing("deferred_destruction_with_Base_winner");
        var working = application.State;
        //178EC0 rewards and updates Force results only for a real Army
        //source. Target personality remains independent of source kind.
        if (application.Outcome == NormalArmyApplicationPrefixOutcome.AwaitingForceAndAftermath)
        {
            var personality = ApplyBorrowedNormalPersonality(working, current, ReadLatest(working), targetId);
            if (personality.Outcome == NormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved)
                throw Missing("target_personality");
            working = personality.State;
        }
        working = working with { NativeSubdayReports = [.. working.NativeSubdayReports ?? [],
            new("normal_base_damage", working.NativeClock!.UpdateOrdinal, targetId,
                pair.Actor.ForceSlot, pair.Actor.PhysicalSlot, damage.Damage.NativeFrameWord20)] };
        //The actual view chooses whether presentation148 exists. Keep this
        //as pending work; no playback or target-view receipt is manufactured.
        effects.Add(new(null, null) { NormalCombatTargetReaction = new(targetId, 1) });
        _baseCapacityInputs.BindCurrent(working, current);
        var aftermath = NormalArmyPairAftermath.TryApplyMixed(working, pair.Actor, pair.Target,
            current, ReadLatest(working), _normalAftermathInputs);
        if (aftermath.Outcome == NormalArmyPairAftermathOutcome.CurrentInputsUnresolved)
            throw Missing(aftermath.MissingOwner ?? "aftermath");
        return aftermath.State;

        InvalidDataException Missing(string owner) => new($"ordinary_army_combat_cut.Base_origin.{owner}.source{pair.Actor.Kind}:{pair.Actor.PhysicalSlot}.target27:{pair.Target.PhysicalSlot}");
    }
}

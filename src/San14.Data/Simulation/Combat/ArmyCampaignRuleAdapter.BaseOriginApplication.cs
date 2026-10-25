using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One reached Base-origin/Army-target pair in the mixed order.
    /// All writes borrow the same private operation; Unknown rejects that
    /// operation, including reached numeric and presentation prefixes.</summary>
    private BorrowedMixedPairApplication CompleteBorrowedBaseArmyPair(GameState state, SourceWorldContext current,
        NormalCombatMixedPair pair, byte? baseDivisor, TacticRemakeCurrentBattleQueries descriptorQueries,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate operationDate,
        long operationRevision, List<CampaignEffectRequest> effects)
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
            if (baseExcluded) return new(state);
        }
        var sourceObject = current.ReadObjectTableReference(objectIndex) ?? throw Missing("Base_Object_lookup");
        if (current.ReadObjectNativeValidity(sourceObject) is not bool objectValid) throw Missing("Base_Object_validity");
        if (!objectValid) return new(state);
        var pendingObjects = current.ReadOwnedCurrentListPool()?.PendingObjectRetirements ?? throw Missing("pending_F0");
        var objectDirectory = current.CurrentMapObjectDirectory ?? throw Missing("Object_directory");
        int objectPhysical = sourceObject.PhysicalSlot ?? throw Missing("Object_receiver");
        var objectReceiver = objectDirectory.ReceiverAtPhysical(objectPhysical);
        foreach (var pending in pendingObjects)
            if (pending == objectReceiver) return new(state);
        int sourceForce = current.ReadObjectForceSlot13(sourceObject) ?? throw Missing("Base_Object_Force13");
        var targetReference = new ArmyUnitReference(pair.Target.PhysicalSlot);
        if (current.ReadArmyUnitNativeValidity(targetReference) is not bool armyValid) throw Missing("Army_validity");
        if (!armyValid) return new(state);
        if (pair.Target.UnitId is not int targetId || !state.Units.TryGetValue(targetId, out var target) ||
            target.NativeArmyOrigin?.NativeSlotIndex != pair.Target.PhysicalSlot)
            throw Missing("Army_identity");
        if (OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, pair.Target.PhysicalSlot) is not bool excluded)
            throw Missing("Army_manager");
        if (excluded) return new(state);
        int targetForce = current.ReadArmyUnitRawForceSlot(targetReference);
        if (ArmyLogisticsPreparation.ReadNativeRelationGate(state, sourceForce, targetForce) is not bool hostile)
            throw Missing("relation");
        if (!hostile) return new(state);
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
        var afterAutomatic = state;
        bool activated = false;
        ushort word22 = 0;
        if (!blockTactic && damage.Damage.NativeFrameWord20 < pair.Target.Troops)
        {
            var automatic = CompleteBorrowedBaseOriginAutomatic(state, current, origin, pair,
                sampler, operationDate, effects, operationRevision);
            afterAutomatic = automatic.State; activated = automatic.Activated; word22 = automatic.Word22;
            if (automatic.Continuation is { } pending)
                return new(afterAutomatic, damage.Damage.NativeFrameWord20) { BaseAutomaticExecution = pending };
        }
        return CompleteBorrowedBaseArmyPairAfterAutomatic(afterAutomatic, current, pair, origin,
            damage.Damage.NativeFrameWord20, word22, activated, sampler, operationDate, operationRevision, effects);

        InvalidDataException Missing(string owner) => new($"ordinary_army_combat_cut.Base_origin.{owner}.source{pair.Actor.Kind}:{pair.Actor.PhysicalSlot}.target27:{pair.Target.PhysicalSlot}");
    }

    private BorrowedMixedPairApplication ResumeBorrowedBaseArmyPairAfterAutomatic(GameState state,
        SourceWorldContext current, NormalCombatMixedPair pair, ushort normalWord20,
        BaseAutomaticTacticExecutionContinuation saved, NativeArmyCombatCycleSampler sampler,
        NativeMapSeasonDate date, long revision, List<CampaignEffectRequest> effects)
    {
        var automatic = ResumeBaseOriginAutomaticExecution(state, current, pair, saved, sampler, date, revision, effects);
        if (automatic.Continuation is { } pending)
            return new(automatic.State, normalWord20) { BaseAutomaticExecution = pending };
        var origin = BaseLookup.Find(saved.CasterBaseLocationId, current);
        return CompleteBorrowedBaseArmyPairAfterAutomatic(automatic.State, current, pair, origin,
            normalWord20, automatic.Word22, true, sampler, date, revision, effects);
    }

    private BorrowedMixedPairApplication CompleteBorrowedBaseArmyPairAfterAutomatic(GameState state,
        SourceWorldContext current, NormalCombatMixedPair pair, BaseReference origin, ushort normalWord20,
        ushort word22, bool activated, NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate operationDate,
        long operationRevision, List<CampaignEffectRequest> effects)
    {
        int targetId = pair.Target.UnitId ?? throw Missing("Army_identity");
        var request = NormalArmyCombatApplicationSourceMath.BuildNormalRequestedLoss(normalWord20, word22);
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
        var working = application.PendingCleanupState ?? application.State;
        if (application.Outcome == NormalArmyApplicationPrefixOutcome.ZeroTroopsCleanupRequired &&
            application.Receipt is { Killed: true })
        {
            int location = current.ReadBaseObjectLink(origin) ?? throw Missing("winner_Base4E");
            var winner = ArmyDestructionQueue.ResolveBase(current, origin) ?? throw Missing("winner_Base_identity");
            var admitted = OwnedArmyDestructionCurrent.AdmitOrdinaryArmy1745B0(working, current,
                pair.Target.PhysicalSlot, null, location);
            if (admitted.RequiresImmediateSettlement)
            {
                working = ApplyNormalCombatImmediateDefeatCurrent(working, current,
                    new(targetId, null, winner), sampler.Read(working) ?? throw Missing("immediate_sampler"),
                    operationDate, operationRevision, effects);
                if (working.ArmyDefeatContinuation is not null)
                    return new(working, request.FrameWord20After);
            }
            else if (admitted.IsPending && admitted.State is { } queued) working = queued;
            else throw Missing(admitted.UnresolvedOwner ?? "Base_winner1745B0");
            OwnedArmyDestructionCurrent.BindOwnedCurrentContext(working, current);
        }
        //178EC0 rewards and updates Force results only for a real Army
        //source. Target personality remains independent of source kind.
        if (application.Outcome == NormalArmyApplicationPrefixOutcome.AwaitingForceAndAftermath)
        {
            var personality = ApplyBorrowedNormalPersonality(working, current, ReadLatest(working), targetId);
            if (personality.Outcome == NormalArmyPersonalityApplicationOutcome.CurrentInputsUnresolved)
                throw Missing("target_personality");
            working = personality.State;
        }
        if (activated && application.Receipt?.Killed == true)
            working = MarkBorrowedAutomaticTacticFatalFeedback(working);
        return new(CompleteBorrowedBaseArmyPairTail(working, current, pair,
            request.FrameWord20After, sampler, effects));

        InvalidDataException Missing(string owner) => new($"ordinary_army_combat_cut.Base_origin.{owner}.source{pair.Actor.Kind}:{pair.Actor.PhysicalSlot}.target27:{pair.Target.PhysicalSlot}");
    }

    //Resume only174A30's report/view work and174EC0. Numeric damage,
    //personality, queue admission and any actual259360 prefix have run once.
    internal GameState CompleteBorrowedBaseArmyPairTail(GameState working, SourceWorldContext current,
        NormalCombatMixedPair pair, ushort word20, NativeArmyCombatCycleSampler sampler,
        List<CampaignEffectRequest> effects)
    {
        int targetId = pair.Target.UnitId ?? throw Missing("Army_identity");
        working = working with { NativeSubdayReports = [.. working.NativeSubdayReports ?? [],
            new("normal_base_damage", working.NativeClock!.UpdateOrdinal, targetId,
                pair.Actor.ForceSlot, pair.Actor.PhysicalSlot, word20)] };
        //The actual view chooses whether presentation148 exists. Keep this
        //as pending work; no playback or target-view receipt is manufactured.
        effects.Add(new(null, null) { NormalCombatTargetReaction = new(targetId, 1) });
        _baseCapacityInputs.BindCurrent(working, current);
        OwnedArmyDestructionCurrent.BindOwnedCurrentContext(working, current);
        var aftermath = NormalArmyPairAftermath.TryApplyMixed(working, pair.Actor, pair.Target,
            current, Context(current, sampler), _normalAftermathInputs);
        if (aftermath.Outcome == NormalArmyPairAftermathOutcome.CurrentInputsUnresolved)
            throw Missing(aftermath.MissingOwner ?? "aftermath");
        return aftermath.State;

        InvalidDataException Missing(string owner) => new($"ordinary_army_combat_cut.Base_origin.{owner}.source{pair.Actor.Kind}:{pair.Actor.PhysicalSlot}.target27:{pair.Target.PhysicalSlot}");
    }
}

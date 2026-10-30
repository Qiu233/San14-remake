using San14.Core;

namespace San14.Data;

/// <summary>Joins the operation world's saved army, parameter, registry and
/// definition owners. This producer never creates orders or repairs missing
/// march state from a displayed destination.</summary>
public sealed class ArmyMarchCurrentProducer : IArmyMarchCurrentQueries
{
    public const string Revision = "san14:ce-owned-current-march-inputs-20261016";
    private readonly ArmyCampaignRuleAdapter package;
    private readonly ArmyMovementEligibilityCurrentProducer eligibility;
    private readonly ArmyMobilityCurrentProducer mobility;
    private readonly ArmyMarchPenaltyCurrentProducer penalty;
    private readonly SourceWorldContext? operationCurrent;

    public ArmyMarchCurrentProducer(GameResourceRepository repository, ArmyCampaignRuleAdapter ownedPackage,
        LoadedMap map, IArmyMovementEligibilityCurrentOwnerQueries? eligibilityOwners = null,
        SourceWorldContext? operationCurrent = null)
    {
        package = ownedPackage; this.operationCurrent = operationCurrent;
        eligibility = new(repository, package, map, eligibilityOwners ?? new SavedEligibilityOwners());
        mobility = new(repository, package, map, ArmyMobilityParameterSeed.Load(repository, package.Seed.Language));
        penalty = new(repository, package, map, ArmyMarchPenaltyParameterSeed.Load(repository, package.Seed.Language));
    }

    public IReadOnlyList<ArmyMovementManagerActor>? ReadCompleteCurrentManager60Chain(GameState state,
        NativeArmyProgressStageMoment moment)
    {
        if (state.ReadArmyLifecycle() is not { Slots.Count: 501 } lifecycle)
            return MissingChain("lifecycle");
        if (lifecycle.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Army) is not { } registry)
            return MissingChain("registry60");
        var result = new List<ArmyMovementManagerActor>(registry.Count);
        foreach (var entity in registry)
        {
            if (entity.Kind != WorldEntityKind.Army) return MissingChain("entity_kind", entity.Id);
            if (entity.Id is not int unitId)
            {
                if (operationCurrent?.ReadArmyUnitNativeValidity(new(0)) == false) continue;
                return MissingChain("null_identity");
            }
            int? physical = WorldIndexContext.FindArmyPhysical(state, unitId);
            if (physical is null) return MissingChain("receiver_binding", unitId);
            //2C7350's first gate is the actual current receiver header. An
            //invalid retained receiver is excluded without requiring a Unit.
            if (operationCurrent is not null)
            {
                bool? valid = operationCurrent.ReadArmyUnitNativeValidity(new(physical));
                if (valid is null) return MissingChain("receiver_validity", unitId, physical);
                if (!valid.Value) continue;
            }
            var receiver = lifecycle.Receivers.Find(lifecycle.Slots[physical.Value].ReceiverId);
            if (receiver is null) return MissingChain("receiver_owner", unitId, physical);
            if (receiver.ActiveUnitId == unitId)
            {
                if (!state.Units.ContainsKey(unitId)) return MissingChain("active_image", unitId, physical);
                result.Add(new(unitId, physical.Value));
            }
            else if (receiver.PendingImage is { } pending && pending.Id == unitId)
            {
                //Stage5 immediate defeats retain this real image and manager
                //node until26. The selector still executes its target and
                //233560 pending70 exclusion in their original order.
                result.Add(new(unitId, physical.Value) { PendingImage = pending });
            }
            else return MissingChain("registered_image", unitId, physical);
        }
        return result.AsReadOnly();

        IReadOnlyList<ArmyMovementManagerActor>? MissingChain(string owner, int? unit = null, int? physical = null)
        {
            if (operationCurrent is not null)
                throw new InvalidDataException($"army.march.missing.stage7.manager_{owner};cycle={moment.CalendarCycleOrdinal};Army={unit};physical={physical}");
            return null;
        }
    }

    public bool? ReadSourceMovementEligibility2C7350(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment)
    {
        var resolved = operationCurrent is null ? eligibility.Resolve(state, army, moment) :
            eligibility.Resolve(state, army, moment, operationCurrent);
        if (resolved.Eligible is null && operationCurrent is not null)
            throw ArmyMarchSourceRuntime.MissingBranch(state, army, moment,
                "stage7." + (resolved.UnresolvedOwner ?? "eligibility_inputs"));
        return resolved.Eligible;
    }

    public GameState? PrepareOrdinaryNextStep(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Immediate ordinary route requires movement stage entry.");
        return package.PrepareOrdinaryImmediateRoute(state, army, operationCurrent);
    }

    public ArmyMarchCurrentInputs? ReadOrdinaryCurrentInputs(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment)
    {
        var preparation = ReadPreparation(state, army, moment, out var current);
        return preparation is null || current is null ? null : ReadDestination(
            preparation.PreparedState ?? state, army, current, preparation);
    }

    public ArmyMarchCurrentInputs? ReadOrdinaryDestinationInputs(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment, ArmyMarchPreparationInputs preparation)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Rerouted destination requires movement stage entry.");
        var current = operationCurrent ?? package.CaptureCurrentDefinitions(state);
        return current is null ? null : ReadDestination(state, army, current, preparation);
    }

    private static ArmyMarchCurrentInputs? ReadDestination(GameState state, UnitState army,
        SourceWorldContext current, ArmyMarchPreparationInputs preparation)
    {
        if (ArmyMarchSourceAdapter.EncodeDestination(army.March?.NextStep) is not ushort next || next >= ArmyMarchSourceAdapter.SourceHexCount ||
            current.ReadHexTableReference(next) is not { PhysicalSlot: not null } to ||
            current.ReadHexSurfaceCode(to) is not byte toCode ||
            current.ReadSurfaceTypeTableReference(toCode <= 30 ? toCode : 0) is not { } surface ||
            ArmyMarchTerrainCost.Read(state, current, surface) is not int destinationCost ||
            current.ReadHexObjectLink(to) is not ushort objectWord) return null;
        return new(preparation.ArmyValid, preparation.Mobility2B0660, preparation.SpeedScale2695D0,
            preparation.Penalty2B5C10, preparation.CachedMinimumTerrainCost1A24010, destinationCost,
            preparation.CurrentTerrainPointerAvailable, true, objectWord, preparation.SourceProvenance, state);
    }

    public ArmyMarchPreparationInputs? ReadOrdinaryPreparationInputs(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment) => ReadPreparation(state, army, moment, out _);

    public bool? ReadOrdinaryNoStepFallback(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("No-step fallback requires movement stage entry.");
        byte? status = army.NativeTacticRuntime?.Status10;
        byte? action = ArmyConfigurationSourceAdapter.ReadAction(state, army);
        if (state.Units.GetValueOrDefault(army.Id) != army || status is not (1 or 2) ||
            status == 1 && action is not (1 or 4 or 5 or 7 or 8 or 9 or 10 or 13 or 14 or 16 or 17) &&
                !(OrdinaryArmyRouteSource.HasTypedObjectOverride(army) && action is >= 0 and <= 17) &&
                !(action == 6 && OrdinaryArmyRouteSource.UsesObjectCommandMovement(state, army)) ||
            status == 1 && action == 10 &&
                (army.NativeArmyOrigin?.NativeSlotIndex is not int workPhysical || state.ReadArmyLifecycle() is not { } workLifecycle ||
                    OwnedArmyLifecycleTargets.Read(state, workLifecycle.Slots[workPhysical].ReceiverId) is not { Kind38: 11 }) ||
            status == 1 && action == 17 &&
                (army.NativeArmyOrigin?.NativeSlotIndex is not int reportPhysical || state.ReadArmyLifecycle() is not { } reportLifecycle ||
                    OwnedArmyLifecycleTargets.Read(state, reportLifecycle.Slots[reportPhysical].ReceiverId) is not { Kind38: 27 }) ||
            status == 2 && action is not (1 or 2 or 3 or 11 or 12) ||
            army.March?.NextStep != ArmyMarchDestination.None ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort position ||
            (operationCurrent ?? package.CaptureCurrentDefinitions(state)) is not { } current ||
            current.ReadHexTableReference(position) is not { PhysicalSlot: not null } from ||
            current.ReadHexTableReference(0) is not { PhysicalSlot: 0 } fallback ||
            from == fallback || current.ReadHexObjectLink(fallback) != 0) return null;
        current.BindArmyMapOccupancy(state);
        if (current.ReadHexRaw1C(fallback) is not ushort raw || unchecked((short)raw) is >= 1 and <= 500) return null;
        if (status == 1) return true;
        if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical ||
            current.ReadHexForceSlot(fallback) is not byte owner) return null;
        bool? hostile = ArmyLogisticsPreparation.ReadNativeRelationGate(state,
            current.ReadArmyUnitRawForceSlot(new(physical)), owner);
        return hostile is bool reached ? !reached : null;
    }

    private ArmyMarchPreparationInputs? ReadPreparation(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment, out SourceWorldContext? current)
    {
        current = null;
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("March inputs must be read at movement stage entry.");
        if (state.Units.GetValueOrDefault(army.Id) != army || army.NativeArmyOrigin is null ||
            state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Language != package.Seed.Language || simulation.Parameters.RuleVersion != package.Seed.Source.RuleVersion ||
            !simulation.Parameters.Int32Values.TryGetValue(SimulationParameterKeys.ArmyMarch.SpeedScale, out int scale) ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort position || position >= ArmyMarchSourceAdapter.SourceHexCount ||
            (current = operationCurrent ?? package.CaptureCurrentDefinitions(state)) is null) return null;
        var from = current.ReadHexTableReference(position);
        if (from?.PhysicalSlot is null ||
            current.ReadHexSurfaceCode(from) is not byte fromCode ||
            current.ReadSurfaceTypeTableReference(fromCode <= 30 ? fromCode : 0)?.PhysicalSlot is null) return null;
        var movementResult = mobility.Resolve(state, army, current);
        if (movementResult.Receipt is not { } movement)
        {
            if (operationCurrent is not null)
                throw new InvalidDataException("army.march.preparation.mobility." + movementResult.UnresolvedOwner);
            return null;
        }
        var resistanceResult = penalty.Resolve(state, army, current);
        if (resistanceResult.Receipt is not { } resistance)
        {
            if (operationCurrent is not null)
                throw new InvalidDataException("army.march.preparation.penalty." + resistanceResult.UnresolvedOwner);
            return null;
        }
        if (ArmyMarchTerrainCost.InitializeMinimum(state, current) is not { } prepared ||
            !prepared.NativeArmySimulation!.Parameters.Int32Values.TryGetValue(SimulationParameterKeys.ArmyMarch.MinimumTerrainCost, out int minimum) ||
            minimum == int.MaxValue) return null;
        // Mobility's receipt proves current army validity. CHex native validity
        // is true for each non-null receiver, including physical slot zero.
        return new(true, movement.Value, scale, resistance.Value, minimum,
            true, $"{Revision};{movement.SourceProvenance};{resistance.SourceProvenance}", PreparedState: prepared);
    }

    private sealed class SavedEligibilityOwners : IArmyMovementEligibilityCurrentOwnerQueries
    {
        public ushort? ReadParentArmyHex2A(GameState state, UnitState army, NativeArmyProgressStageMoment moment)
        {
            if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical || state.ReadArmyLifecycle() is not { } lifecycle ||
                OwnedArmyLifecycleTargets.Read(state, lifecycle.Slots[physical].ReceiverId) is not { Kind38: 27, Identity3A: ushort raw }) return null;
            var slot = lifecycle.ReceiverSlots()[raw <= 500 ? raw : 0];
            var parent = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
            return parent is null ? null : ArmyMarchSourceAdapter.ReadCurrentHex(state, parent);
        }
        public ArmyMovementEligibilityCurrentFields? ReadTargetFields(GameState state, UnitState army,
            NativeArmyProgressStageMoment moment)
        {
            if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical || state.ReadArmyLifecycle() is not { } lifecycle ||
                physical < 0 || physical >= lifecycle.Slots.Count) return null;
            var receiver = lifecycle.ReceiverSlots()[physical];
            if (receiver.ActiveUnitId != army.Id && receiver.PendingImage?.Id != army.Id) return null;
            var target = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId);
            return new(army.Id, physical, moment, target?.Kind38, target?.Identity3A,
                lifecycle.ArmyFlags150?[physical], army.NativeTacticRuntime?.Status10 == 1
                    ? ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(army.March) : TransportRouteSourceAdapter.ReadWaypoints(state, army), Revision);
        }
    }
}

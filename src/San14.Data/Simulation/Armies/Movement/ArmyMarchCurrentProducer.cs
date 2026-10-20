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

    public ArmyMarchCurrentProducer(GameResourceRepository repository, ArmyCampaignRuleAdapter ownedPackage,
        LoadedMap map, IArmyMovementEligibilityCurrentOwnerQueries? eligibilityOwners = null)
    {
        package = ownedPackage;
        eligibility = new(repository, package, map, eligibilityOwners ?? new SavedEligibilityOwners());
        mobility = new(repository, package, map, ArmyMobilityParameterSeed.Load(repository, package.Seed.Language));
        penalty = new(repository, package, map, ArmyMarchPenaltyParameterSeed.Load(repository, package.Seed.Language));
    }

    public IReadOnlyList<ArmyMovementManagerActor>? ReadCompleteCurrentManager60Chain(GameState state,
        NativeArmyProgressStageMoment moment)
    {
        if (state.ReadArmyLifecycle() is not { Slots.Count: 501 } lifecycle ||
            lifecycle.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Army) is not { } registry) return null;
        var result = new List<ArmyMovementManagerActor>(registry.Count);
        foreach (var entity in registry)
        {
            // The current stage contract only accepts active armies. A pending
            // receiver in the real registry is unsupported, never silently cut.
            if (entity.Kind != WorldEntityKind.Army || entity.Id is not int unitId ||
                WorldIndexContext.FindArmyPhysical(state, unitId) is not int physical ||
                lifecycle.ReceiverSlots()[physical].ActiveUnitId != unitId ||
                !state.Units.ContainsKey(unitId)) return null;
            result.Add(new(unitId, physical));
        }
        return result.AsReadOnly();
    }

    public bool? ReadSourceMovementEligibility2C7350(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment) => eligibility.Resolve(state, army, moment).Eligible;

    public GameState? PrepareOrdinaryNextStep(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Immediate ordinary route requires movement stage entry.");
        return package.PrepareOrdinaryImmediateRoute(state, army);
    }

    public ArmyMarchCurrentInputs? ReadOrdinaryCurrentInputs(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment)
    {
        var preparation = ReadPreparation(state, army, moment, out var current);
        if (preparation is null || current is null ||
            ArmyMarchSourceAdapter.EncodeDestination(army.March?.NextStep) is not ushort next || next >= ArmyMarchSourceAdapter.SourceHexCount ||
            current.ReadHexTableReference(next) is not { PhysicalSlot: not null } to ||
            current.ReadHexSurfaceCode(to) is not byte toCode ||
            current.ReadSurfaceTypeTableReference(toCode <= 30 ? toCode : 0) is not { } surface ||
            ArmyMarchTerrainCost.Read(state, current, surface) is not int destinationCost ||
            current.ReadHexObjectLink(to) is not ushort objectWord) return null;
        return new(preparation.ArmyValid, preparation.Mobility2B0660, preparation.SpeedScale2695D0,
            preparation.Penalty2B5C10, preparation.CachedMinimumTerrainCost1A24010, destinationCost,
            preparation.CurrentTerrainPointerAvailable, true, objectWord, preparation.SourceProvenance, preparation.PreparedState);
    }

    public ArmyMarchPreparationInputs? ReadOrdinaryPreparationInputs(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment) => ReadPreparation(state, army, moment, out _);

    public bool? ReadOrdinaryNoStepFallback(GameState state, UnitState army,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("No-step fallback requires movement stage entry.");
        if (state.Units.GetValueOrDefault(army.Id) != army || army.NativeTacticRuntime?.Status10 != 1 ||
            ArmyConfigurationSourceAdapter.ReadAction(state, army) is not (1 or 8 or 9 or 13) ||
            army.March?.NextStep != ArmyMarchDestination.None ||
            ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort position ||
            package.CaptureCurrentDefinitions(state) is not { } current ||
            current.ReadHexTableReference(position) is not { PhysicalSlot: not null } from ||
            current.ReadHexTableReference(0) is not { PhysicalSlot: 0 } fallback ||
            from == fallback || current.ReadHexObjectLink(fallback) != 0) return null;
        current.BindArmyMapOccupancy(state);
        if (current.ReadHexRaw1C(fallback) is not ushort raw || unchecked((short)raw) is >= 1 and <= 500) return null;
        return true;
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
            (current = package.CaptureCurrentDefinitions(state)) is null) return null;
        var from = current.ReadHexTableReference(position);
        if (from?.PhysicalSlot is null ||
            current.ReadHexSurfaceCode(from) is not byte fromCode ||
            current.ReadSurfaceTypeTableReference(fromCode <= 30 ? fromCode : 0)?.PhysicalSlot is null ||
            mobility.Resolve(state, army).Receipt is not { } movement ||
            penalty.Resolve(state, army).Receipt is not { } resistance ||
            ArmyMarchTerrainCost.InitializeMinimum(state, current) is not { } prepared ||
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
            if (receiver.ActiveUnitId != army.Id) return null;
            var target = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId);
            return new(army.Id, physical, moment, target?.Kind38, target?.Identity3A,
                lifecycle.ArmyFlags150?[physical], army.NativeTacticRuntime?.Status10 == 1
                    ? ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(army.March) : TransportRouteSourceAdapter.ReadWaypoints(state, army), Revision);
        }
    }
}

using San14.Core;

namespace San14.Data;

internal sealed record MapObjectRetirementNumberResult(GameState State,
    MapObjectReceiverId Receiver, int? PreviousHexId, string? DefinitionKey);
public sealed record MapObjectRetirementResult(GameState? State,
    IReadOnlyList<MapObjectReceiverId> RetiredReceivers, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    public MapObjectRetirementResult TryAdvanceMapObjectRetirement(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.MapObjectRetirement ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
            throw new InvalidDataException("Object retirement requires reached stage27.");
        try
        {
            var arena = state.ReadArmyLifecycle() ?? throw DemolitionMissing("retirement_lifecycle");
            var pending = arena.Indexes.PendingObjectRetirements ?? throw DemolitionMissing("retirement_queue");
            GameState working = state;
            var requests = new List<CampaignEffectRequest>();
            var retired = new List<MapObjectReceiverId>();
            foreach (var id in pending)
            {
                if (id is not { } receiver) continue;
                var removed = ApplyMapObjectRetirementNumbers(working, receiver);
                // Optional View removal and paired-view cleanup are retained
                // as one unconsumed operation, not a fabricated receipt.
                requests.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.RetirementVisualDetach,
                    receiver, removed.PreviousHexId, removed.DefinitionKey) });
                working = removed.State;
                retired.Add(receiver);
            }
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
                { Indexes = working.ReadArmyLifecycle()!.Indexes with { PendingObjectRetirements = [] } });
            if (requests.Count != 0) working = working with
                { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw DemolitionMissing("retirement_effect_owner"))
                    .Append(CampaignEffectSource.MapObjectRetirement, moment.CalendarCycleOrdinal, requests) };
            return new(working, retired.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    /// <summary>Only the proved reset and remove-all phase. The stage consumer
    /// must retain the preceding view-detachment intent in the same transaction.</summary>
    private MapObjectRetirementNumberResult ApplyMapObjectRetirementNumbers(GameState state, MapObjectReceiverId id)
    {
        var source = CampaignSourceKnowledge.From(state.ResourceState).MapObjectSource
            ?? throw DemolitionMissing("retirement_source");
        var directory = new MapObjectSourceDirectory(source);
        int physical = directory.PhysicalOf(id);
        var owner = state.MapObjects ?? throw DemolitionMissing("retirement_owner");
        var receiver = owner.Find(id) ?? throw DemolitionMissing("retirement_receiver");
        var arena = state.ReadArmyLifecycle() ?? throw DemolitionMissing("retirement_lifecycle");
        if (!arena.Indexes.Registries.TryGetValue(WorldEntityKind.MapObject, out var saved) || saved is null)
            throw DemolitionMissing("retirement_registry");
        var indexes = arena.Indexes.Copy();
        WorldIndexRules.RemoveAll(indexes.Registries[WorldEntityKind.MapObject]!, new(WorldEntityKind.MapObject, id.Value));
        var cities = new Dictionary<int, CityState>(state.Cities);
        var gates = new Dictionary<int, GateState>(state.Gates);
        var structures = new Dictionary<int, StructureState>(state.Structures ?? []);
        if (cities.TryGetValue(physical, out var linkedCity)) cities[physical] = linkedCity with { Durability = 0 };
        if (gates.TryGetValue(physical, out var linkedGate)) gates[physical] = linkedGate with { Durability = 0 };
        if (receiver.Subject is { } subject)
        {
            if (subject.Category == MapObjectCategory.City)
                cities[subject.LocationId!.Value] = cities[subject.LocationId.Value] with { Durability = 0 };
            else if (subject.Category == MapObjectCategory.Gate)
                gates[subject.LocationId!.Value] = gates[subject.LocationId.Value] with { Durability = 0 };
            else if (subject.StructureId is int structure) structures.Remove(structure);
        }
        //227790 resets the reusable receiver. It does not clear another
        //binding or any Army target, and does not change District/Area owners.
        var reset = receiver with { Subject = null, Pose = new(MapObjectPoseKind.None), BaseMapFaction = null };
        var working = (state with { MapObjects = owner.With(reset), Cities = cities, Gates = gates, Structures = structures,
            ResourceState = CampaignSourceKnowledge.From(state.ResourceState).WithMapObjectSource(source.Reset(physical)) })
            .WithArmyLifecycle(arena with { Indexes = indexes });
        // Base virtualB0 is a different source chain from cleared Object13.
        // In particular GateB0 must follow invalid-object Hex0 fallback now.
        // CityB0 only reads District, which this reset never changes. Do not
        // introduce unrelated queries for every other city or gate.
        if (gates.TryGetValue(physical, out var affectedGate))
        {
            var current = _definitions.Capture(working) ?? throw DemolitionMissing("retirement_base_capture");
            var gate = new BaseReference(BaseKind.Gate, affectedGate.Id - 51);
            int force = DemolitionKnown(current.ReadBaseForceSlot(gate), "retirement_gate_affiliation");
            int district = DemolitionKnown(current.ReadBaseDistrictSlot(gate), "retirement_gate_district");
            gates[affectedGate.Id] = affectedGate with { ForceId = force == 0 ? null : force,
                DistrictId = district == 0 ? null : district };
        }
        working = working with { Cities = cities, Gates = gates };
        int? oldHex = receiver.Pose is { Kind: MapObjectPoseKind.Hex } pose ? pose.HexId : null;
        string? definitionKey = receiver.Subject?.DefinitionKey;
        if (definitionKey is null && source.UnplacedFields.TryGetValue(physical, out var fields))
            definitionKey = MapObjectSourceDirectory.DefinitionKey(fields.Kind <= 30 ? fields.Kind : 0);
        return new(working, id, oldHex, definitionKey);
    }
}

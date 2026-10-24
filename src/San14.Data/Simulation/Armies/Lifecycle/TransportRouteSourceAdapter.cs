using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using San14.Core;

namespace San14.Data;

/// <summary>Unclassified source behavior and waypoint codes. Known route choices
/// live only in TransportRouteState; this immutable channel preserves the rest.</summary>
public readonly record struct TransportRouteSourceOwner(int UnitId, int? DefeatedForceId = null);

public sealed record TransportRouteSourceKnowledge
{
    public TransportRouteSourceOwner Owner { get; }
    public byte Choice41 { get; }
    public IReadOnlyDictionary<int, ushort> UnmappedWaypoints { get; }
    public TransportRouteSourceKnowledge(TransportRouteSourceOwner owner, byte choice41,
        IEnumerable<KeyValuePair<int, ushort>> unmappedWaypoints)
    {
        var values = unmappedWaypoints.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (owner.UnitId < 0 || owner.DefeatedForceId is < 0 || choice41 > 2 ||
            values.Any(pair => pair.Key < 0 || pair.Key >= TransportRouteSourceAdapter.WaypointCapacity || pair.Value <= TransportRouteSourceAdapter.EmptyWaypoint))
            throw new InvalidDataException("Transport compatibility is outside its source format.");
        Owner = owner; Choice41 = choice41;
        UnmappedWaypoints = new ReadOnlyDictionary<int, ushort>(values);
    }
}

internal static class TransportRouteSourceAdapter
{
    internal const int WaypointCapacity = 5, EmptyWaypoint = 48400;
    internal static TransportRouteState Decode(IReadOnlyList<ushort> waypoints)
    {
        if (waypoints.Count != WaypointCapacity) throw new InvalidDataException("Source transport waypoint count is invalid.");
        return new(waypoints.Select(value => value < EmptyWaypoint ? ArmyMarchDestination.AtHex(value) :
            value == EmptyWaypoint ? ArmyMarchDestination.None : null).ToArray());
    }
    internal static CampaignSourceKnowledge Write(ICampaignResourceState? state, int unitId,
        IReadOnlyList<ushort> waypoints, byte choice) => CampaignSourceKnowledge.From(state).WithTransportRoute(
            new(new(unitId), choice, waypoints.Select((value, index) => new KeyValuePair<int, ushort>(index, value))
                .Where(pair => pair.Value > EmptyWaypoint)));
    internal static ushort? ReadWaypoint(GameState state, UnitState unit, int index)
    {
        if (unit.TransportRoute is not { Waypoints.Count: WaypointCapacity } route) return null;
        var waypoint = route.Waypoints[index];
        if (waypoint is null)
            return (state.ResourceState as CampaignSourceKnowledge)?.FindTransportRoute(unit.Id) is { } source &&
                source.UnmappedWaypoints.TryGetValue(index, out ushort raw) ? raw : null;
        return waypoint.Kind switch
        {
            ArmyMarchDestinationKind.None when waypoint.HexId is null => EmptyWaypoint,
            ArmyMarchDestinationKind.Hex when waypoint.HexId is >= 0 and < EmptyWaypoint => (ushort)waypoint.HexId,
            _ => null
        };
    }
    internal static ushort[]? ReadWaypoints(GameState state, UnitState unit)
    {
        var result = new ushort[WaypointCapacity];
        for (int i = 0; i < result.Length; i++)
        {
            if (ReadWaypoint(state, unit, i) is not ushort value) return null;
            result[i] = value;
        }
        return result;
    }
    internal static byte? ReadChoice(GameState state, UnitState unit) =>
        (state.ResourceState as CampaignSourceKnowledge)?.FindTransportRoute(unit.Id)?.Choice41;

    internal static ICampaignResourceState Migrate(CampaignSourceKnowledge knowledge, JsonObject unit, int? defeatedForceId)
    {
        if (unit["Id"] is not JsonValue idNode || !idNode.TryGetValue<int>(out int id) || id < 0 ||
            unit["NativeTransport"] is not JsonObject old || old.Count != 2 ||
            old["Waypoints2C"] is not JsonArray words || words.Count != WaypointCapacity ||
            old["Choice41"] is not JsonValue choiceNode || !choiceNode.TryGetValue<byte>(out byte choice) || choice > 2 ||
            knowledge.TransportRoutes.Any(row => row.Owner == new TransportRouteSourceOwner(id, defeatedForceId)))
            throw new InvalidDataException("Historical transport source knowledge is invalid or duplicated.");
        var values = words.Select(node => node is JsonValue value && value.TryGetValue<ushort>(out ushort word) ? word :
            throw new InvalidDataException("Historical transport waypoint is outside its source encoding.")).ToArray();
        unit["TransportRoute"] = JsonSerializer.SerializeToNode(Decode(values));
        return knowledge.WithTransportRoute(new(new(id, defeatedForceId), choice,
            values.Select((value, index) => new KeyValuePair<int, ushort>(index, value)).Where(pair => pair.Value > EmptyWaypoint)));
    }
    internal static JsonArray EncodeKnowledge(IEnumerable<TransportRouteSourceKnowledge> rows)
    {
        var result = new JsonArray();
        foreach (var row in rows)
        {
            var unmapped = new JsonObject();
            foreach (var pair in row.UnmappedWaypoints) unmapped[pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)] = pair.Value;
            result.Add(new JsonObject { ["UnitId"] = row.Owner.UnitId, ["DefeatedForceId"] = row.Owner.DefeatedForceId, ["Choice41"] = row.Choice41, ["UnmappedWaypoints"] = unmapped });
        }
        return result;
    }
    internal static IReadOnlyList<TransportRouteSourceKnowledge> DecodeKnowledge(JsonArray rows)
    {
        var result = new List<TransportRouteSourceKnowledge>();
        foreach (var node in rows)
        {
            if (node is not JsonObject row || row.Count != 4 || row["UnitId"] is not JsonValue idNode ||
                !idNode.TryGetValue<int>(out int id) || id < 0 || !row.ContainsKey("DefeatedForceId") ||
                row["DefeatedForceId"] is not null && (row["DefeatedForceId"] is not JsonValue forceNode || !forceNode.TryGetValue<int>(out _)) ||
                row["Choice41"] is not JsonValue choiceNode || !choiceNode.TryGetValue<byte>(out byte choice) || choice > 2 ||
                row["UnmappedWaypoints"] is not JsonObject unknown)
                throw new InvalidDataException("Saved transport source knowledge is invalid.");
            var values = new Dictionary<int, ushort>();
            foreach (var pair in unknown)
                if (!int.TryParse(pair.Key, out int index) || index is < 0 or >= WaypointCapacity ||
                    pair.Key != index.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                    pair.Value is not JsonValue value || !value.TryGetValue<ushort>(out ushort word) || word <= EmptyWaypoint ||
                    !values.TryAdd(index, word)) throw new InvalidDataException("Saved transport waypoint residue is invalid.");
            var owner = new TransportRouteSourceOwner(id, row["DefeatedForceId"]?.GetValue<int>());
            if (owner.DefeatedForceId is < 0 || result.Any(item => item.Owner == owner))
                throw new InvalidDataException("Saved transport image ownership is invalid or duplicated.");
            result.Add(new(owner, choice, values));
        }
        return result;
    }
    private static Dictionary<TransportRouteSourceOwner, UnitState> Images(GameState state)
    {
        var result = state.Units.Values.Concat(state.ArmyLifecycle?.Receivers.Entries
            .Where(row => row.PendingImage is not null).Select(row => row.PendingImage!) ?? [])
            .ToDictionary(unit => new TransportRouteSourceOwner(unit.Id));
        foreach (var defeat in state.ForceDefeats)
            foreach (var unit in defeat.Value.WithdrawnUnits) result.Add(new(unit.Id, defeat.Key), unit);
        return result;
    }
    // Explicit domain edits replace corresponding old unknown encodings. Removed
    // images no longer own compatibility; withdrawn histories have separate owners.
    internal static GameState PruneReplacedChannels(GameState state)
    {
        if (state.ResourceState is not CampaignSourceKnowledge knowledge || knowledge.TransportRoutes.Count == 0) return state;
        var units = Images(state);
        return state with { ResourceState = knowledge.ReplaceTransportRoutes(knowledge.TransportRoutes
            .Where(row => units.TryGetValue(row.Owner, out var unit) && unit.TransportRoute is not null)
            .Select(row => new TransportRouteSourceKnowledge(row.Owner, row.Choice41,
                row.UnmappedWaypoints.Where(pair => pair.Key < units[row.Owner].TransportRoute!.Waypoints.Count &&
                    units[row.Owner].TransportRoute!.Waypoints[pair.Key] is null)))) };
    }
    internal static void Validate(GameState state)
    {
        var units = Images(state);
        foreach (var row in (state.ResourceState as CampaignSourceKnowledge)?.TransportRoutes ?? [])
        {
            if (row.Owner.UnitId < 0 || row.Owner.DefeatedForceId is < 0 || row.Choice41 > 2 ||
                !units.TryGetValue(row.Owner, out var unit) || unit.TransportRoute is not { } route)
                throw new InvalidDataException("Saved transport compatibility owner is invalid.");
            foreach (var pair in row.UnmappedWaypoints)
                if (pair.Key is < 0 or >= WaypointCapacity || pair.Value <= EmptyWaypoint ||
                    pair.Key >= route.Waypoints.Count || route.Waypoints[pair.Key] is not null)
                    throw new InvalidDataException("Saved transport residue conflicts with its route owner.");
        }
        //External decode boundary only. The source target/configuration owner
        //classifies native transports whose domain destination is not a City.
        foreach (var unit in state.Units.Values)
        {
            if (unit.NativeArmyOrigin is null || unit.NativeTacticRuntime?.Status10 != 2 ||
                unit.Mission is not { Kind: UnitMissionKind.Transport } mission) continue;
            //Only the newly admitted shapes require this exact native witness.
            //Historical City transports may still carry unknown source action
            //or target words; loading them must preserve that knowledge gap.
            byte? action = ArmyConfigurationSourceAdapter.ReadAction(state, unit);
            if (action != 13 && mission.DestinationCityId is not null && unit.Troops != 0) continue;
            //Native commands preserve the old coarse Arrival/Returning
            //projection. Their actual action/target and officer return bit
            //have independent owners, including after a mode3 replacement.
            var receiver = state.ReadArmyLifecycle()?.Receivers.FindUnit(unit.Id)
                ?? throw new InvalidDataException("Saved transport receiver is absent.");
            var slots = state.ReadArmyLifecycle()!.ReceiverSlots();
            int physical = unit.NativeArmyOrigin.NativeSlotIndex
                ?? throw new InvalidDataException("Saved transport receiver position is unknown.");
            if ((uint)physical >= slots.Length || slots[physical].ReceiverId != receiver.ReceiverId ||
                slots[physical].Commander12 != unit.OfficerId || slots[physical].Status10 != 2)
                throw new InvalidDataException("Saved transport receiver header disagrees with its unit.");
            var target = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId);
            bool valid = (action, target?.Kind38, target?.Identity3A) switch
            {
                (1, 9, ushort hex) => mission.DestinationCityId is null && hex < 48400 && unit.DestinationHexId == hex,
                (13, 9, ushort hex) => mission.DestinationCityId is null && hex < 48400 && unit.DestinationHexId == hex &&
                    ArmyMarchSourceAdapter.ReadCurrentHex(state, unit) == hex &&
                    unit.TransportRoute?.Waypoints is { Count: 5 } points &&
                    points.All(point => point is { Kind: ArmyMarchDestinationKind.None, HexId: null }) &&
                    unit.March?.NextStep == ArmyMarchDestination.None,
                (2 or 11, 5, ushort city) => mission.DestinationCityId == city && state.Cities.ContainsKey(city),
                (3 or 12, 6, ushort gate) => mission.DestinationCityId is null && gate is >= 1 and <= 10 &&
                    state.Gates.TryGetValue(gate + 51, out var destination) && destination.HexId == unit.DestinationHexId,
                _ => false
            };
            if (!valid) throw new InvalidDataException("Saved native transport command and target disagree.");
        }
    }
}

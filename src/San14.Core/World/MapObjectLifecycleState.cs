using System.Text.Json.Serialization;

namespace San14.Core;

/// <summary>A reusable campaign identity, independent of a resource-table ordinal.</summary>
public readonly record struct MapObjectReceiverId(int Value);
public enum MapObjectCategory { City, Gate, Facility, Obstacle, Trap, Unclassified }
public enum MapObjectPoseKind { None, Hex, Unresolved }
/// <summary>None is a known absence of placement; null outer knowledge is unavailable.</summary>
public sealed record MapObjectPose(MapObjectPoseKind Kind, int? HexId = null);
public enum MapObjectFactionKind { Unaffiliated, Force, Unresolved }
/// <summary>Base map allegiance is independent of district or area affiliation.</summary>
public sealed record MapObjectBaseFaction(MapObjectFactionKind Kind, int? ForceId = null);
public sealed record MapObjectSubject(MapObjectCategory Category, string DefinitionKey,
    int? LocationId, int? StructureId);
/// <summary>Subject null is known empty. Pose is independent current knowledge,
/// including for an empty receiver; a null Pose remains unknown.</summary>
public sealed record MapObjectReceiverState(MapObjectReceiverId Id, MapObjectSubject? Subject,
    MapObjectPose? Pose, MapObjectBaseFaction? BaseMapFaction);
/// <summary>A present owner describes the entire map. Omitted cells are known empty.</summary>
public sealed record MapObjectHexBindings(int TileCount, Dictionary<int, MapObjectReceiverId> Occupied)
{
    public MapObjectHexBindings Copy() => this with { Occupied = new(Occupied) };
}

public sealed record MapObjectLifecycleState
{
    public IReadOnlyList<MapObjectReceiverState> Receivers { get; init; }
    public MapObjectHexBindings? HexBindings { get; init; }

    [JsonConstructor]
    public MapObjectLifecycleState(IReadOnlyList<MapObjectReceiverState> receivers,
        MapObjectHexBindings? hexBindings)
    {
        ArgumentNullException.ThrowIfNull(receivers);
        Receivers = Array.AsReadOnly(receivers.ToArray());
        HexBindings = hexBindings;
    }

    public MapObjectReceiverState? Find(MapObjectReceiverId id) => Receivers.FirstOrDefault(row => row.Id == id);
    /// <summary>Replace an allocated receiver. This does not allocate or reuse an identity.</summary>
    public MapObjectLifecycleState With(MapObjectReceiverState receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        if (Find(receiver.Id) is null) throw new InvalidDataException("Map object receiver is not allocated.");
        return new(Receivers.Select(row => row.Id == receiver.Id ? receiver : row).ToArray(), HexBindings);
    }
    public MapObjectLifecycleState Copy() => new(Receivers.ToArray(), HexBindings?.Copy());
}

public sealed partial record GameState
{
    public MapObjectLifecycleState? MapObjects { get; init; }
}

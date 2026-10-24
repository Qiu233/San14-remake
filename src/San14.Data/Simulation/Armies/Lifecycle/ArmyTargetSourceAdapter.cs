using San14.Core;

namespace San14.Data;

/// <summary>Source packet projection used only by import and legacy algorithms.</summary>
public sealed record ArmyTargetSourceFields(byte? Kind38, ushort? Identity3A);
public sealed record ArmyTargetSourceResidue(ArmyReceiverId ReceiverId, byte? KindCode, ushort? IdentityCode)
{
    /// <summary>The actual lookup used for a known receiver when the lookup
    /// alias cannot be replaced by its physical directory position.</summary>
    public ushort? MapObjectLookupAlias { get; init; }
}

/// <summary>Interprets target categories established by consumers. Other codes
/// remain compatibility knowledge, not invented game concepts.</summary>
public static class ArmyTargetSourceAdapter
{
    public static (ArmyTargetState Target, ArmyTargetSourceResidue Residue) Decode(
        ArmyReceiverId owner, ArmyTargetSourceFields fields, ArmyReceiverSourceDirectory receivers,
        MapObjectSourceDirectory? mapObjects = null)
    {
        ArmyTargetKind? kind = fields.Kind38 switch
        {
            null => null, 255 => ArmyTargetKind.None, 5 => ArmyTargetKind.City, 6 => ArmyTargetKind.Gate,
            27 => ArmyTargetKind.ArmyReceiver, 9 => ArmyTargetKind.Hex,
            11 when mapObjects is not null => ArmyTargetKind.MapObjectReceiver, _ => ArmyTargetKind.Unresolved
        };
        ArmyTargetDestination? destination = fields.Identity3A is null ? null :
            kind == ArmyTargetKind.City && fields.Identity3A is >= 1 and <= 51
                ? new(ArmyTargetDestinationKind.City, fields.Identity3A.Value, null)
                : kind == ArmyTargetKind.Gate && fields.Identity3A is >= 1 and <= 10
                    ? new(ArmyTargetDestinationKind.Gate, null, null) { GateId = fields.Identity3A.Value + 51 }
                : kind == ArmyTargetKind.Hex && fields.Identity3A < 48400
                    ? new(ArmyTargetDestinationKind.Hex, null, null) { HexId = fields.Identity3A.Value }
                : kind == ArmyTargetKind.ArmyReceiver && receivers.TryResolveReceiverId(fields.Identity3A.Value, out var id)
                    ? new(ArmyTargetDestinationKind.ArmyReceiver, null, id)
                : kind == ArmyTargetKind.MapObjectReceiver && mapObjects?.Lookup(fields.Identity3A.Value) is { } mapObject
                    ? new(ArmyTargetDestinationKind.MapObjectReceiver, null, null) { MapObjectReceiverId = mapObject }
                    : new(ArmyTargetDestinationKind.Unresolved, null, null);
        ushort? alias = null;
        if (destination?.MapObjectReceiverId is { } mapped && mapObjects is not null && fields.Identity3A is ushort raw)
        {
            int physical = mapObjects.PhysicalOf(mapped);
            if (raw != physical || mapObjects.Lookup(physical) != mapped) alias = raw;
        }
        return (new(kind, destination), new ArmyTargetSourceResidue(owner,
            kind == ArmyTargetKind.Unresolved ? fields.Kind38 : null,
            destination?.Kind == ArmyTargetDestinationKind.Unresolved ? fields.Identity3A : null)
            { MapObjectLookupAlias = alias });
    }
    public static ArmyTargetSourceFields Encode(ArmyTargetState target, ArmyTargetSourceResidue? residue,
        ArmyReceiverSourceDirectory receivers, MapObjectSourceDirectory? mapObjects = null)
    {
        byte? kind = target.Kind switch
        {
            null => null, ArmyTargetKind.None => 255, ArmyTargetKind.City => 5, ArmyTargetKind.Gate => 6,
            ArmyTargetKind.ArmyReceiver => 27, ArmyTargetKind.Hex => 9, ArmyTargetKind.Unresolved => residue?.KindCode,
            ArmyTargetKind.MapObjectReceiver => 11,
            _ => throw new InvalidDataException("Unknown army target category.")
        };
        ushort? identity = target.Destination switch
        {
            null => null,
            { Kind: ArmyTargetDestinationKind.City, CityId: int id } when id is >= 1 and <= 51 => (ushort)id,
            { Kind: ArmyTargetDestinationKind.Gate, GateId: int gate } when gate is >= 52 and <= 61 => (ushort)(gate - 51),
            { Kind: ArmyTargetDestinationKind.Hex, HexId: int hex } when hex is >= 0 and <= ushort.MaxValue => (ushort)hex,
            { Kind: ArmyTargetDestinationKind.ArmyReceiver, ReceiverId: ArmyReceiverId id }
                when receivers.TryResolveSourceOrdinal(id, out int ordinal) && ordinal <= ushort.MaxValue => (ushort)ordinal,
            { Kind: ArmyTargetDestinationKind.MapObjectReceiver, MapObjectReceiverId: MapObjectReceiverId id }
                => EncodeMapObjectIdentity(id, residue?.MapObjectLookupAlias, mapObjects),
            { Kind: ArmyTargetDestinationKind.Unresolved } => residue?.IdentityCode,
            _ => null
        };
        return new(kind, identity);
    }

    private static ushort? EncodeMapObjectIdentity(MapObjectReceiverId receiver, ushort? alias,
        MapObjectSourceDirectory? directory)
    {
        if (directory is null) return null;
        if (alias is ushort raw)
        {
            if (directory.Lookup(raw) != receiver)
                throw new InvalidDataException("Army target lookup alias does not identify its map object receiver.");
            return raw;
        }
        int physical = directory.PhysicalOf(receiver);
        //A new semantic choice without a saved source alias cannot invent an
        //alternative lookup. In particular lookup[physical] may name another
        //receiver or an observed null pointer.
        return directory.Lookup(physical) == receiver ? checked((ushort)physical) : null;
    }
}

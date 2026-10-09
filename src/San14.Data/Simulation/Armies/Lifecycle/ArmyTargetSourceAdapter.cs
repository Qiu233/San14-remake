using San14.Core;

namespace San14.Data;

/// <summary>Source packet projection used only by import and legacy algorithms.</summary>
public sealed record ArmyTargetSourceFields(byte? Kind38, ushort? Identity3A);
public sealed record ArmyTargetSourceResidue(ArmyReceiverId ReceiverId, byte? KindCode, ushort? IdentityCode);

/// <summary>Interprets target categories established by consumers. Other codes
/// remain compatibility knowledge, not invented game concepts.</summary>
public static class ArmyTargetSourceAdapter
{
    public static (ArmyTargetState Target, ArmyTargetSourceResidue Residue) Decode(
        ArmyReceiverId owner, ArmyTargetSourceFields fields, ArmyReceiverSourceDirectory receivers)
    {
        ArmyTargetKind? kind = fields.Kind38 switch
        {
            null => null, 255 => ArmyTargetKind.None, 5 => ArmyTargetKind.City,
            27 => ArmyTargetKind.ArmyReceiver, _ => ArmyTargetKind.Unresolved
        };
        ArmyTargetDestination? destination = fields.Identity3A is null ? null :
            kind == ArmyTargetKind.City && fields.Identity3A is >= 1 and <= 51
                ? new(ArmyTargetDestinationKind.City, fields.Identity3A.Value, null)
                : kind == ArmyTargetKind.ArmyReceiver && receivers.TryResolveReceiverId(fields.Identity3A.Value, out var id)
                    ? new(ArmyTargetDestinationKind.ArmyReceiver, null, id)
                    : new(ArmyTargetDestinationKind.Unresolved, null, null);
        return (new(kind, destination), new(owner,
            kind == ArmyTargetKind.Unresolved ? fields.Kind38 : null,
            destination?.Kind == ArmyTargetDestinationKind.Unresolved ? fields.Identity3A : null));
    }
    public static ArmyTargetSourceFields Encode(ArmyTargetState target, ArmyTargetSourceResidue? residue,
        ArmyReceiverSourceDirectory receivers)
    {
        byte? kind = target.Kind switch
        {
            null => null, ArmyTargetKind.None => 255, ArmyTargetKind.City => 5,
            ArmyTargetKind.ArmyReceiver => 27, ArmyTargetKind.Unresolved => residue?.KindCode,
            _ => throw new InvalidDataException("Unknown army target category.")
        };
        ushort? identity = target.Destination switch
        {
            null => null,
            { Kind: ArmyTargetDestinationKind.City, CityId: int id } when id is >= 1 and <= 51 => (ushort)id,
            { Kind: ArmyTargetDestinationKind.ArmyReceiver, ReceiverId: ArmyReceiverId id }
                when receivers.TryResolveSourceOrdinal(id, out int ordinal) && ordinal <= ushort.MaxValue => (ushort)ordinal,
            { Kind: ArmyTargetDestinationKind.Unresolved } => residue?.IdentityCode,
            _ => null
        };
        return new(kind, identity);
    }
}

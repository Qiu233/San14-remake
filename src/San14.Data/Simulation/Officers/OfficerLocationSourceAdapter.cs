using San14.Core;

namespace San14.Data;

/// <summary>Only this format boundary translates resource place codes. Army
/// places follow receiver identities, including empty and reused receivers.</summary>
public static class OfficerLocationSourceAdapter
{
    public static OfficerLocationState Decode(ushort? position, byte? district, ArmyReceiverSourceDirectory? receivers) =>
        new(DecodePosition(position, receivers), district switch
        {
            null => null,
            0 => OfficerDistrictMembership.Unaffiliated,
            <= 51 => new(OfficerDistrictMembershipKind.District, district),
            _ => OfficerDistrictMembership.Unresolved
        });

    public static OfficerPosition? DecodePosition(ushort? code, ArmyReceiverSourceDirectory? receivers) => code switch
    {
        null => null,
        0 => OfficerPosition.Unplaced,
        <= 51 => new(OfficerPositionKind.City, code),
        <= 61 => new(OfficerPositionKind.Gate, code),
        <= 561 when receivers is not null && receivers.TryResolveReceiverId(code.Value - 61, out _) =>
            OfficerPosition.AtReceiver(Resolve(receivers, code.Value - 61)),
        <= 561 => OfficerPosition.Unresolved,
        <= 1061 => new(OfficerPositionKind.Area, code - 561),
        _ => OfficerPosition.Unresolved
    };

    public static ushort? EncodePosition(OfficerPosition? position, ArmyReceiverSourceDirectory? receivers,
        OfficerLocationSourceResidue? residue = null) => position switch
    {
        null => null,
        { Kind: OfficerPositionKind.Unplaced, EntityId: null } => 0,
        { Kind: OfficerPositionKind.City, EntityId: >= 1 and <= 51 } => (ushort)position.EntityId.Value,
        { Kind: OfficerPositionKind.Gate, EntityId: >= 52 and <= 61 } => (ushort)position.EntityId.Value,
        { Kind: OfficerPositionKind.Area, EntityId: >= 1 and <= 500 } => (ushort)(position.EntityId.Value + 561),
        { Kind: OfficerPositionKind.ArmyReceiver, EntityId: int receiverId } when receivers is not null &&
            receivers.TryResolveSourceOrdinal(new(receiverId), out int ordinal) && ordinal is >= 1 and <= 500 => (ushort)(ordinal + 61),
        { Kind: OfficerPositionKind.Unresolved, EntityId: null } when residue?.LocationCode is >= 0 and <= ushort.MaxValue => (ushort)residue.LocationCode.Value,
        _ => null
    };

    public static byte? EncodeMembership(OfficerDistrictMembership? membership, OfficerLocationSourceResidue? residue = null) => membership switch
    {
        null => null,
        { Kind: OfficerDistrictMembershipKind.Unaffiliated, DistrictId: null } => 0,
        { Kind: OfficerDistrictMembershipKind.District, DistrictId: >= 1 and <= 51 } => (byte)membership.DistrictId.Value,
        { Kind: OfficerDistrictMembershipKind.Unresolved, DistrictId: null } when residue?.DistrictCode is >= 0 and <= byte.MaxValue => (byte)residue.DistrictCode.Value,
        _ => null
    };

    public static OfficerLocationSourceResidue Residue(int officerId, OfficerLocationState state, ushort? position, byte? district) =>
        new(officerId, state.Position?.Kind == OfficerPositionKind.Unresolved ? position : null,
            state.Membership?.Kind == OfficerDistrictMembershipKind.Unresolved ? district : null);

    internal static ArmyReceiverSourceDirectory? Receivers(GameState state) => state.NativeArmyLifecycle is { } lifecycle
        ? new(lifecycle) : null;
    internal static GameState PublishPosition(GameState state, int officerId, SourceWorldContext current)
    {
        OfficerState officer = state.Officers[officerId];
        var location = officer.LocationState ?? new OfficerLocationState(null, null);
        var position = current.ReadOfficerLocation(officerId)?.Position;
        var residues = OfficerLocationSourceResidues.From(state.ResourceState);
        var previous = residues.Find(officerId);
        residues = residues.With(new(officerId, current.ReadOfficerLocationResidue(officerId)?.LocationCode, previous?.DistrictCode));
        return state with { Officers = new(state.Officers) { [officerId] = officer with
            { LocationState = location with { Position = position } } }, ResourceState = residues };
    }
    internal static GameState PublishPositions(GameState state, SourceWorldContext current)
    {
        var people = new Dictionary<int, OfficerState>(state.Officers);
        var residues = OfficerLocationSourceResidues.From(state.ResourceState);
        foreach (var person in state.Officers.Values)
        {
            int physical = current.ResolveOfficerSource(person.Id) ?? throw new InvalidDataException("Current officer position identity is missing.");
            var location = person.LocationState ?? throw new InvalidDataException("Current officer position owner is missing.");
            var position = current.ReadOfficerLocation(person.Id)?.Position;
            var previous = residues.Find(person.Id);
            residues = residues.With(new(person.Id, current.ReadOfficerLocationResidue(person.Id)?.LocationCode, previous?.DistrictCode));
            people[person.Id] = person with { LocationState = location with { Position = position } };
        }
        return state with { Officers = people, ResourceState = residues };
    }
    private static ArmyReceiverId Resolve(ArmyReceiverSourceDirectory receivers, int ordinal)
    {
        _ = receivers.TryResolveReceiverId(ordinal, out var identity); return identity;
    }
}

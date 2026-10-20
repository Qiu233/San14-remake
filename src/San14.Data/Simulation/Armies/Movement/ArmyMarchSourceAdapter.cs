using San14.Core;

namespace San14.Data;

/// <summary>The original map's word encoding is confined to source consumers.
/// Unencodable campaign values remain intact in the domain state.</summary>
public static class ArmyMarchSourceAdapter
{
    public const int SourceHexCount = 48400;
    public static ushort? ReadCurrentHex(GameState state, UnitState unit)
    {
        var residue = (state.ResourceState as CampaignSourceKnowledge)?.FindArmyPosition(unit.Id);
        return unit.PositionKnowledge == ArmyPositionKnowledge.Conflicting
            ? residue?.MarchConflictHex ?? residue?.DirectConflictHex : EncodeCurrentHex(unit);
    }
    public static ushort? ReadCreatedCurrentHex(GameState state, UnitState unit)
    {
        var residue = (state.ResourceState as CampaignSourceKnowledge)?.FindArmyPosition(unit.Id);
        if (residue?.DirectPositionUnavailable == true) return null;
        return unit.PositionKnowledge == ArmyPositionKnowledge.Conflicting ? residue?.DirectConflictHex : EncodeCurrentHex(unit);
    }
    private static ushort? EncodeCurrentHex(UnitState unit) => unit.ConfirmedHexId is int hex &&
        hex is >= 0 and < SourceHexCount ? (ushort)hex : null;
    public static ArmyMarchDestination DecodeDestination(ushort value) => value == SourceHexCount
        ? ArmyMarchDestination.None : ArmyMarchDestination.AtHex(value);
    public static ushort? EncodeDestination(ArmyMarchDestination? value) => value switch
    {
        { Kind: ArmyMarchDestinationKind.None, HexId: null } => SourceHexCount,
        { Kind: ArmyMarchDestinationKind.Hex, HexId: int hex } when hex is >= 0 and <= ushort.MaxValue && hex != SourceHexCount => (ushort)hex,
        _ => null
    };
    public static ushort? EncodeCredit(ArmyMarchState? value) => value?.MovementCredit is int credit &&
        credit is >= 0 and <= ushort.MaxValue ? (ushort)credit : null;

    /// <summary>Encode ordinary intermediate destinations only at the native
    /// algorithm boundary. This does not reuse transport behavior or infer
    /// missing points from the final destination or a calculated route.</summary>
    public static ushort[]? ReadOrdinaryWaypoints(ArmyMarchState? march)
    {
        if (march?.FollowingWaypoints is not { } following || EncodeDestination(march.Target) is not ushort first)
            return null;
        ushort[] result = [(ushort)first, SourceHexCount, SourceHexCount, SourceHexCount, SourceHexCount];
        for (int i = 0; i < following.Destinations.Count; i++)
        {
            if (EncodeDestination(following.Destinations[i]) is not ushort value) return null;
            result[i + 1] = value;
        }
        return result;
    }
}

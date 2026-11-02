using San14.Core;

namespace San14.Data;

/// <summary>The original map's word encoding is confined to source consumers.
/// Unencodable campaign values remain intact in the domain state.</summary>
public static class ArmyMarchSourceAdapter
{
    public const int SourceHexCount = 48400;

    /// <summary>A confirmed campaign position is an int map identity. Only
    /// conflicting old source words require the original profile decoder.</summary>
    internal static int? ReadCurrentMapHex(GameState state, UnitState unit)
    {
        if (unit.PositionKnowledge != ArmyPositionKnowledge.Conflicting)
            return unit.ConfirmedHexId;
        ushort? raw = ReadCurrentHex(state, unit);
        return raw is < SourceHexCount ? raw.Value : null;
    }

    // Route publication still writes the original word fields. Reject the
    // unrepresentable identity explicitly; never clamp or wrap the int path.
    internal static ushort EncodeRouteHex(int hex) => EncodeDestination(ArmyMarchDestination.AtHex(hex))
        ?? throw new InvalidDataException("Route Hex cannot be represented by the original word adapter.");
    // A source Base getter still returns the original None word. Decode it
    // before geometry can admit that same integer as a campaign Hex identity.
    // A known absent linked Object is not revived by a static Base center.
    internal static ArmyMarchDestination? ReadBaseMapDestination(GameState state,
        SourceWorldContext current, BaseReference receiver)
    {
        var source = DecodeDestination(current.ReadBaseCurrentHexIndex(receiver));
        if (source.Kind == ArmyMarchDestinationKind.None) return source;
        if (receiver.PhysicalSlot is int physical)
        {
            if (receiver.Kind == BaseKind.City && state.Cities.TryGetValue(physical, out var city))
                return city.HexId is int cityHex ? ArmyMarchDestination.AtHex(cityHex) : null;
            if (receiver.Kind == BaseKind.Gate && state.Gates.TryGetValue(physical + 51, out var gate))
                return gate.HexId is int gateHex ? ArmyMarchDestination.AtHex(gateHex) : null;
        }
        // Independent source consumers may have no campaign Base projection.
        return source;
    }

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

    internal static ushort? ReadFirstWaypoint(GameState state, UnitState army) =>
        army.NativeTacticRuntime?.Status10 == 2 ? TransportRouteSourceAdapter.ReadWaypoint(state, army, 0)
            : EncodeDestination(army.March?.Target);

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

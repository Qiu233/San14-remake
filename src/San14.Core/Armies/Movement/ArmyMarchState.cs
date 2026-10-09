using System.Text.Json.Serialization;

namespace San14.Core;

public enum ArmyMarchDestinationKind { None, Hex }
public enum ArmyPositionKnowledge { Unknown, Confirmed, Conflicting }

/// <summary>A known absence of a destination is distinct from an unknown one.</summary>
public sealed record ArmyMarchDestination(ArmyMarchDestinationKind Kind, int? HexId = null)
{
    public static ArmyMarchDestination None { get; } = new(ArmyMarchDestinationKind.None);
    public static ArmyMarchDestination AtHex(int hexId) => new(ArmyMarchDestinationKind.Hex, hexId);
}

/// <summary>Independent knowledge of progress and the currently selected route steps.</summary>
public sealed record ArmyMarchState(int? MovementCredit, ArmyMarchDestination? Target,
    ArmyMarchDestination? NextStep);

public sealed partial record UnitState
{
    public ArmyMarchState? March { get; init; }
    /// <summary>The actual position has been supplied by a creation or position writer.
    /// A provisional placement alone does not establish this knowledge.</summary>
    public ArmyPositionKnowledge PositionKnowledge { get; init; }
    [JsonIgnore]
    public bool CurrentHexKnown => PositionKnowledge == ArmyPositionKnowledge.Confirmed;
    [JsonIgnore]
    public int? ConfirmedHexId => CurrentHexKnown ? HexId : null;
}

public static class PlayableUnitPosition
{
    public static UnitState AtHex(UnitState unit, int hexId) => unit with
    {
        HexId = hexId,
        PositionKnowledge = unit.PositionKnowledge == ArmyPositionKnowledge.Unknown
            ? ArmyPositionKnowledge.Unknown : ArmyPositionKnowledge.Confirmed
    };
}

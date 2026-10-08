namespace San14.Core;

public sealed partial record UnitState
{
    /// <summary>Current native Army byte36. Only a proved receiver reset or an
    /// actual adjacent movement edge supplies this field; legacy absence stays unknown.</summary>
    public byte? NativeDirection36 { get; init; }
}

/// <summary>Direction for the actual adjacent edge about to update Army word2A.
/// A missing result does not derive a heading from a destination or a jump.</summary>
public interface IPlayableArmyPositionRules
{
    byte? ReadDirection36(GameState state, UnitState unit, int fromHex, int toHex);
}

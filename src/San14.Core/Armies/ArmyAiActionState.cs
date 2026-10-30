namespace San14.Core;

public sealed partial record UnitState
{
    /// <summary>The source action-result flags carried by this receiver.
    /// Null is unknown; only actual construction/reset or a real writer
    /// establishes the value. Control140 is a different owner.</summary>
    public ushort? AiActionFlags { get; init; }
}

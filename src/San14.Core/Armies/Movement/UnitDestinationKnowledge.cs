namespace San14.Core;

public sealed partial record UnitState
{
    /// <summary>Legacy integer projection of an actual known-no-target Army.
    /// Unknown knowledge remains on the typed target/march owners. This value
    /// is never an ordinary map Hex or a substitute for an unknown endpoint.</summary>
    public const int NoDestinationHex = -1;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasNoDestination => DestinationHexId == NoDestinationHex;
}

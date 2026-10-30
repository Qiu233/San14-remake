namespace San14.Core;

/// <summary>Owned construction and development state. Older campaigns retain
/// unknown development rather than receiving newly randomized starting effects.</summary>
public sealed record LandmarkInvestmentState(byte Phase, ushort AssignedOfficerId, int CompletionDay)
{
    //The historical codec name stores the source investment duration, not a
    //Person identity. Keep its serialized key stable for existing campaigns.
    [System.Text.Json.Serialization.JsonIgnore]
    public ushort InvestmentDays => AssignedOfficerId;
    public LandmarkDevelopmentState? Development { get; init; }
    public byte? Flags { get; init; }
}
public sealed record LandmarkDevelopmentState(byte Level, byte EffectKind, byte Effect);

public sealed partial record GameState
{
    // Null means an older campaign has no owned construction lifecycle.
    public Dictionary<int, LandmarkInvestmentState>? LandmarkInvestments { get; init; }
}

namespace San14.Core;

/// <summary>Verified static income inputs, independent of current development.
/// Remaining source bytes stay opaque and are retained through save/load.</summary>
public sealed record AreaEconomySourceInputs(int RawField44, int RawField46, int RawField52,
    string FixedRemainingBytesHex, string ScenarioRemainingBytesHex,
    string FixedDataSha256, string ScenarioSha256)
{
    public string? ScenarioSourceBytesHex { get; init; }
}

public sealed partial record AreaState
{
    public AreaEconomySourceInputs? EconomySource { get; init; }
}

namespace San14.Data;

/// <summary>Exact deserialize inputs and separate reset-prefix evidence. The
/// prefix zero fields never populate the current monthly context automatically.</summary>
public sealed record OriginalInitialEconomySourceRow(int AreaSlot, ushort Raw44AfterFixedLoad,
    ushort Raw46AfterFixedLoad, ushort Raw52AfterScenarioLoad, byte Raw54AfterScenarioLoad,
    byte? Raw38AfterScenarioLoad, ushort? Officer36AfterReset, ushort? Progress3CAfterReset,
    ushort? Progress3EAfterReset, byte? Bonus6DAfterReset, string FixedSourceSha256,
    string ScenarioSourceSha256)
{
    public string? ResetPrefixExecutableSha256 { get; init; }
}

public static class OriginalInitialEconomySource
{
    public const string ProfileId = "san14:ce-sc-initial-economy-prefix-20261002";
    public const string SourceExecutableSha256 = OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256;
    public const uint AreaResetRva = 0x226BC0;
    public const uint AreaScenarioSerializerRva = 0x235A60;
    public const uint AreaFixedSerializerRva = 0x23BFA0;

    public static OriginalInitialEconomySourceRow Project(AreaPreviewRecord area, LoadedScenario source)
    {
        var inputs = OriginalCeAreaEconomySource.Project(area, source);
        bool auditedReset = source.Snapshot.Locale == "SC";
        return new(area.Slot, checked((ushort)inputs.RawField44), checked((ushort)inputs.RawField46),
            checked((ushort)inputs.RawField52), area.Value2, area.RawFlags38,
            auditedReset ? (ushort)0 : null, auditedReset ? (ushort)0 : null,
            auditedReset ? (ushort)0 : null, auditedReset ? (byte)0 : null,
            inputs.FixedDataSha256, inputs.ScenarioSha256)
        { ResetPrefixExecutableSha256 = auditedReset ? SourceExecutableSha256 : null };
    }
}

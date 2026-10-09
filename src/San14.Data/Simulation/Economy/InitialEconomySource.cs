using San14.Core;

namespace San14.Data;

/// <summary>Exact deserialize inputs and separate reset-prefix evidence. The
/// prefix zero fields never populate the current monthly context automatically.</summary>
public sealed record InitialEconomySourceRow(int AreaSlot, ushort Raw44AfterFixedLoad,
    ushort Raw46AfterFixedLoad, ushort Raw52AfterScenarioLoad, byte Raw54AfterScenarioLoad,
    byte? Raw38AfterScenarioLoad, ushort? Officer36AfterReset, ushort? Progress3CAfterReset,
    ushort? Progress3EAfterReset, byte? Bonus6DAfterReset, string FixedSourceSha256,
    string ScenarioSourceSha256)
{
    public string? ResetRuleVersion { get; init; }
}

public static class InitialEconomySource
{
    public const string ProfileId = "san14:ce-sc-initial-economy-prefix-20261002";
    public const string RuleVersion = SimulationRuleIdentity.Version;

    public static InitialEconomySourceRow Project(AreaPreviewRecord area, LoadedScenario source)
    {
        var inputs = CeAreaEconomySource.Project(area, source);
        bool auditedReset = source.Snapshot.Locale == "SC";
        return new(area.Slot, checked((ushort)inputs.RawField44), checked((ushort)inputs.RawField46),
            checked((ushort)inputs.RawField52), area.Value2, area.RawFlags38,
            auditedReset ? (ushort)0 : null, auditedReset ? (ushort)0 : null,
            auditedReset ? (ushort)0 : null, auditedReset ? (byte)0 : null,
            inputs.FixedDataSha256, inputs.ScenarioSha256)
        { ResetRuleVersion = auditedReset ? RuleVersion : null };
    }
}

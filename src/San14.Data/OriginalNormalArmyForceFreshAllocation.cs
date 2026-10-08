using San14.Core;

namespace San14.Data;

/// <summary>Actual CForce ctor/reset then static-prefix load, for a newly
/// allocated OWN remake Force bank. The static serializer skips runtime15F /
///160 /162. Request/runtime-copy writers must update this saved current owner;
/// this is not a claim about the original first-player's later initialization.</summary>
public static class OriginalNormalArmyForceFreshAllocation
{
    public const string Revision = "san14:ce-force-ctor-static-result-fields-20261006";
    public const string VersionKey = "ce-force-ctor-static-result-fields:version";

    public static GameState InstallFresh(GameState state, LoadedScenario staticScenario,
        OriginalNormalArmyForceResultSourceParameters source)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(staticScenario);
        if (!source.IsInitialAllocationSource() || staticScenario.Metadata.FormatMarker != 0x66 ||
            staticScenario.Snapshot.Locale != source.Language ||
            !staticScenario.Snapshot.Forces.Select(force => force.Slot).Order().SequenceEqual(Enumerable.Range(0, 52)) ||
            staticScenario.Snapshot.Forces.Any(force => force.RemainingBytesHex.Length != 654) ||
            state.ContentHashes.GetValueOrDefault(staticScenario.ScenarioSource.ResourceUri) != staticScenario.ScenarioSource.Sha256 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.Units.Count != 0 || state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Language != source.Language ||
            simulation.Parameters.ExecutableSha256 != source.Source.Sha256 ||
            simulation.Parameters.Int32Values.ContainsKey(VersionKey) ||
            simulation.Parameters.Float32Values.ContainsKey(VersionKey) ||
            !simulation.Forces.Keys.Order().SequenceEqual(state.Forces.Keys.Order()) ||
            simulation.Forces.Values.Any(force => force.ResultGate15F is not null ||
                force.ResultDefinition160 is not null || force.ResultValue162 is not null))
            throw new InvalidDataException("Force result constructor fields require the fresh unmodified owned static allocation.");
        //SC2213C0→2272E0 writes word15E=000B and qword160=0. The
        //SC237F20 static serializer writes only byte15E, then238282/238289
        //jumps to23862D without consuming15F/160/162. TC is paired in source.
        var forces = simulation.Forces.ToDictionary(pair => pair.Key, pair => pair.Value with {
            ResultGate15F = 0, ResultDefinition160 = 0, ResultValue162 = 0 });
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        return state with { NativeArmySimulation = simulation with { Forces = forces,
            Parameters = simulation.Parameters with { Int32Values = integers } } };
    }

    private static bool IsInitialAllocationSource(this OriginalNormalArmyForceResultSourceParameters source) =>
        source.CurrentRevision is null && source.Source.Sha256 == (source.Language switch {
            "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256, _ => null });

    public static OriginalBattleUnitSourceProfile ForLanguage(string language) => language switch
    {
        "SC" => new("SC", OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6",
            Array.AsReadOnly<OriginalBattleUnitSourceWindow>([
                new("freshForceConstructor", 0x2213C0, 0x2214B0, "D89E515359C303F50CCAA4AEDD27C50B8E890B9833B1BD2EE7F5EA11D516D614"),
                new("freshForceReset", 0x2272E0, 0x22765B, "805FE3D051782C0EE49E8D84BD78C02A0E2C8297663A8A34B58FB736A8F27212"),
                new("forceStaticRuntimeSerializer", 0x237F20, 0x23864B, "13A1120CD7988143173F69D902803809F5162311168B84986F09F30419FC04DE")])),
        "TC" => new("TC", OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,
            "FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766",
            Array.AsReadOnly<OriginalBattleUnitSourceWindow>([
                new("freshForceConstructor", 0x221110, 0x221200, "7C2534772AC7C1BF2417D6967F6D63F13A0CE42082E80E84C21FEB0BEA332F64"),
                new("freshForceReset", 0x227070, 0x2273EB, "2AAD9FB71F4C3956E3D54534387778BDF6CCE551755845647D3A15BB7336025F"),
                new("forceStaticRuntimeSerializer", 0x237CC0, 0x2383EB, "C6A9380D92EFF036FB772A3947203A7E7557D10E0CCEBF58CE505DD5C2785C0E")])),
        _ => throw new InvalidDataException("Fresh Force source language is unsupported.")
    };
}

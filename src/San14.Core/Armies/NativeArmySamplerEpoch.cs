namespace San14.Core;

/// <summary>Explicit fresh scenario counters for the pure native tactic
/// sampler. Current450 additionally requires the matching calendar writer.</summary>
public sealed record NativeArmySamplerEpoch(string SourceScenarioUri, string SourceScenarioSha256,
    string SourceWorldSha256, int Day450, int Salt45C, string NativeClockRevision, string Revision);

internal static class NativeArmySamplerEpochValidation
{
    public static void Validate(GameState state, NativeArmySamplerEpoch epoch)
    {
        if (string.IsNullOrWhiteSpace(epoch.SourceScenarioUri) || !Hash(epoch.SourceScenarioSha256) ||
            !Hash(epoch.SourceWorldSha256) || string.IsNullOrWhiteSpace(epoch.Revision) ||
            state.NativeClock is null || epoch.NativeClockRevision != state.NativeClock.Revision ||
            !state.ContentHashes.TryGetValue(epoch.SourceScenarioUri, out string? actual) ||
            !actual.Equals(epoch.SourceScenarioSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Owned native sampler epoch has inconsistent source or clock identity.");
        if (state.NativeMapSeason is { } calendar &&
            (calendar.SourceScenarioUri != epoch.SourceScenarioUri ||
             !calendar.SourceScenarioSha256.Equals(epoch.SourceScenarioSha256, StringComparison.OrdinalIgnoreCase) ||
             !calendar.SourceWorldSha256.Equals(epoch.SourceWorldSha256, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Owned native sampler and calendar belong to different source worlds.");
    }
    private static bool Hash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

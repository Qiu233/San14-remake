namespace San14.Core;

/// <summary>Auxiliary current Person fields constructed by an explicit owned
/// source producer. Field labels use the SC semantic offsets; TC has its own
/// proved physical layout. Missing legacy records remain unknown.</summary>
public sealed record NativePersonAbilityRuntime(string Revision, string Language,
    string ExecutableSha256, string SourceScenarioUri, string SourceScenarioSha256,
    int PhysicalSlot, byte? Status11E, byte? Office11F, byte? Doctrine184,
    ushort? Flags196, byte? Health199, byte? Secondary1F4, byte? Extra1FD,
    byte? EncodedOrdinaryCap224, byte? EncodedCharmCap225);

public sealed partial record OfficerState
{
    public NativePersonAbilityRuntime? NativeAbilityRuntime { get; init; }
}

internal static class NativePersonAbilityRuntimeValidation
{
    public static void Validate(GameState state)
    {
        var physical = new HashSet<int>();
        foreach (OfficerState officer in state.Officers.Values)
        {
            if (officer.NativeAbilityRuntime is not { } runtime) continue;
            if (state.NativeArmySimulation?.Parameters is not { } parameters ||
                string.IsNullOrWhiteSpace(runtime.Revision) || runtime.Language != parameters.Language ||
                runtime.ExecutableSha256 != parameters.ExecutableSha256 ||
                runtime.SourceScenarioSha256 is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) ||
                string.IsNullOrWhiteSpace(runtime.SourceScenarioUri) ||
                state.ContentHashes.GetValueOrDefault(runtime.SourceScenarioUri) != hash ||
                runtime.PhysicalSlot is < 1 or > 1850 || !physical.Add(runtime.PhysicalSlot))
                throw new InvalidDataException("Owned Person ability runtime has inconsistent source or physical identity.");
        }
    }
}

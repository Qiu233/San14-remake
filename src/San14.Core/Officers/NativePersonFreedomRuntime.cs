namespace San14.Core;

/// <summary>The remaining SC-semantic raw fields written by TC265250.
/// Status/location/credit/flags keep their existing independent saved owners.
/// This module is installed by an actual reached writer; old saves stay null.</summary>
public sealed record NativePersonFreedomRuntime(string Revision, ulong? Qword18A,
    uint? Dword192, ushort? Word19E);

public sealed partial record OfficerState
{
    public NativePersonFreedomRuntime? NativeFreedomRuntime { get; init; }
}

public static class NativePersonFreedomRuntimeValidation
{
    public static void Validate(GameState state)
    {
        foreach (var person in state.Officers.Values)
            if (person.NativeFreedomRuntime is { } fields &&
                (string.IsNullOrWhiteSpace(fields.Revision) || person.NativeAbilityRuntime is null))
                throw new InvalidDataException("Native Person freedom fields lack their actual receiver owner.");
    }
}

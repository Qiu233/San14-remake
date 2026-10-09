namespace San14.Core;

/// <summary>Independent current Person auxiliaries reached by ordinary Army
/// destruction. Labels are SC semantic offsets; TC physical fields are
/// 162/164/165/168/16C. Source identity belongs to NativeAbilityRuntime.
/// Null fields, including older saves, remain unresolved.</summary>
public sealed record NativePersonDestructionRuntimeState(string Revision,
    ushort? Word19A, byte? Byte19C, byte? Byte19D, byte? Byte1A0, short? Word1A4);

public sealed partial record OfficerState
{
    public NativePersonDestructionRuntimeState? NativeDestructionRuntime { get; init; }
}

public static class NativePersonDestructionRuntimeStateValidation
{
    public static void Validate(GameState state)
    {
        foreach (OfficerState person in state.Officers.Values)
            if (person.NativeDestructionRuntime is { } value &&
                (string.IsNullOrWhiteSpace(value.Revision) || person.NativeAbilityRuntime is null))
                throw new InvalidDataException("Native Person destruction auxiliaries have no current Person identity or revision.");
    }
}

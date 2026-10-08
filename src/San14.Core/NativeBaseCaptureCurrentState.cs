namespace San14.Core;

/// <summary>Current native fields reached by ordinary Base capture. Null is
/// unresolved; this module never derives them from gameplay ownership.</summary>
public sealed record NativeCityCaptureFields(byte? C9, byte? CF);

public sealed record NativeBaseCaptureCurrentState(string Revision,
    ushort?[] HexWords16, Dictionary<int, NativeCityCaptureFields> Cities, ushort? World1562);

public sealed partial record GameState
{
    public NativeBaseCaptureCurrentState? NativeBaseCaptureCurrent { get; init; }
}

public static class NativeBaseCaptureCurrentStateValidation
{
    public static void Validate(GameState state)
    {
        if (state.NativeBaseCaptureCurrent is not { } owner) return;
        if (string.IsNullOrWhiteSpace(owner.Revision) || owner.HexWords16 is not { Length: 48400 } ||
            owner.Cities is not { Count: 52 } || owner.Cities.Any(x => x.Key is < 0 or > 51 || x.Value is null))
            throw new InvalidDataException("Current native Base capture field directory is malformed.");
        //World1562 is a full native word. Only its reached increment writer
        //caps9999; validation must not rewrite another current raw value.
    }
}

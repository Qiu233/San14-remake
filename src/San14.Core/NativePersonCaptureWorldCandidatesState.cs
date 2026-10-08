namespace San14.Core;

/// <summary>Current CWorldData's five selected Person ID words and five date/
/// auxiliary dwords. Count165D remains NativeArmySimulation.World's existing
/// owner. Null fields/old saves remain unknown; no query reseeds ctor values.</summary>
public sealed record NativePersonCaptureWorldCandidatesState(string Revision,
    ushort?[] PersonIds165E, int?[] Values1668);

public sealed partial record GameState
{
    public NativePersonCaptureWorldCandidatesState? NativePersonCaptureWorldCandidates { get; init; }
}

public static class NativePersonCaptureWorldCandidatesStateValidation
{
    public static void Validate(GameState state)
    {
        if (state.NativePersonCaptureWorldCandidates is not { } owner) return;
        if (string.IsNullOrWhiteSpace(owner.Revision) || owner.PersonIds165E is not { Length: 5 } ||
            owner.Values1668 is not { Length: 5 } || state.NativeArmySimulation is null ||
            state.NativeArmySimulation.World.SelectedCommanderCount165D is > 5)
            throw new InvalidDataException("Current World capture candidates have an invalid source window/count owner.");
    }
}

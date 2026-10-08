namespace San14.Core;

/// <summary>Independent current native District numeric windows and Force
/// ranking/reset stores. Unknown padding remains null. A reset image is the
/// literal writer's output, not a claim that all Force source fields were read.</summary>
public sealed record NativeForceRankingCurrent(ushort? Word1BC, byte? Byte1C4);
public sealed record NativeForceUnregisterCurrentState(string Revision,
    Dictionary<int, byte?[]> DistrictWindows10,
    Dictionary<int, NativeForceRankingCurrent> ForceRankings,
    Dictionary<int, byte?[]> ForceResetImages,
    byte[] PlaceholderRelations13, byte[] PlaceholderRelationsEA)
{
    //326230's word1656 reset constructs the independent interaction target.
    public byte? OtherForce1657 { get; init; }
}

public sealed partial record GameState
{
    public NativeForceUnregisterCurrentState? NativeForceUnregisterCurrent { get; init; }
}

public static class NativeForceUnregisterCurrentStateValidation
{
    public static void Validate(GameState state)
    {
        if (state.NativeForceUnregisterCurrent is not { } owner) return;
        if (string.IsNullOrWhiteSpace(owner.Revision) || owner.DistrictWindows10 is not { Count: 52 } ||
            owner.ForceRankings is not { Count: 52 } || owner.ForceResetImages is null ||
            owner.PlaceholderRelations13 is not { Length: 52 } || owner.PlaceholderRelationsEA is not { Length: 52 } ||
            owner.DistrictWindows10.Any(x => x.Key is < 0 or > 51 || x.Value is not { Length: 28 }) ||
            owner.ForceRankings.Any(x => x.Key is < 0 or > 51 || x.Value is null || x.Value.Byte1C4 > 4) ||
            owner.ForceResetImages.Any(x => x.Key is < 1 or > 45 || x.Value is not { Length: 0x20E }))
            throw new InvalidDataException("Current native Force unregistration fields are malformed.");
    }
}

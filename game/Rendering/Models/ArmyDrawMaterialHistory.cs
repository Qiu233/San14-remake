using San14.Data;

internal sealed record ArmyDrawMaterialState(object Source,
    RemakeArmyInitialization Initialization, uint Flags, ReadOnlyMemory<byte> Pixel);

/// <summary>Material state from the last accepted preparation. Instance keys
/// reuse history only with the same model preparation and unit initialization.</summary>
internal sealed class ArmyDrawMaterialHistory
{
    private IReadOnlyDictionary<string, ArmyDrawMaterialState> accepted =
        new Dictionary<string, ArmyDrawMaterialState>(StringComparer.Ordinal);

    public ArmyDrawMaterialState? Read(string instance, object source,
        RemakeArmyInitialization initialization)
        => accepted.TryGetValue(instance, out var state) && ReferenceEquals(state.Source, source) &&
            ReferenceEquals(state.Initialization, initialization) ? state : null;

    public void Commit(IReadOnlyDictionary<string, ArmyDrawMaterialState> pending)
        => accepted = new Dictionary<string, ArmyDrawMaterialState>(pending, StringComparer.Ordinal);

    public void Clear() => accepted = new Dictionary<string, ArmyDrawMaterialState>(StringComparer.Ordinal);
}

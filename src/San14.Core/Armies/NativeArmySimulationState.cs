namespace San14.Core;

/// <summary>Current parameters owned by this remake session. Source-created
/// seeds and later writes are explicit; old saves never import image globals.</summary>
public sealed record NativeArmyParameterState(string Revision, string Language,
    string RuleVersion, Dictionary<string, int> Int32Values, Dictionary<string, float> Float32Values)
{
    /// <summary>Null means the current bonus vector decoder is unavailable.</summary>
    public OfficeBonusEncoding? OfficeBonusEncoding { get; init; }
}
public sealed record NativeArmyForceState(byte? Flags193)
{
    public byte? Flags12 { get; init; }
    public byte? Flags194 { get; init; }
    public byte[]? RelationsEA { get; init; }
    public NativeNormalArmyForceAftermath? NormalAftermath { get; init; }
}
public sealed record NativeArmyWorldState(byte? SelectedCommanderCount165D)
{
    public NativeArmySamplerEpoch? SamplerEpoch { get; init; }
    public byte? Flags165C { get; init; }
    public byte? SelectedForce1656 { get; init; }
    public int[]? PendingArmyIds { get; init; }
    public NativeArmyObjectPairState[]? ObjectPairs { get; init; }
}
public sealed record NativeArmyObjectPairState(int? FirstArmyId, int? BaseLocationId, int? SecondArmyId);
public sealed record NativeArmySimulationState(NativeArmyParameterState Parameters,
    Dictionary<int, NativeArmyForceState> Forces, NativeArmyWorldState World);

public sealed partial record GameState
{
    public NativeArmySimulationState? NativeArmySimulation { get; init; }
}

internal static class NativeArmySimulationStateValidation
{
    public static void Validate(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation) return;
        if (state.NativeClock is null || simulation.Parameters is not { } parameters ||
            string.IsNullOrWhiteSpace(parameters.Revision) || parameters.Language is not ("SC" or "TC") ||
            string.IsNullOrWhiteSpace(parameters.RuleVersion) ||
            parameters.Int32Values is null || parameters.Float32Values is null || simulation.Forces is null ||
            simulation.World is null || parameters.Int32Values.Keys.Concat(parameters.Float32Values.Keys)
                .Any(string.IsNullOrWhiteSpace) || parameters.Float32Values.Values.Any(value => !float.IsFinite(value)) ||
            simulation.Forces.Any(pair => pair.Value is null || !state.Forces.ContainsKey(pair.Key) ||
                pair.Value.RelationsEA is { Length: not 52 }))
            throw new InvalidDataException("Owned native Army simulation inputs are incomplete or inconsistent.");
        foreach (UnitState army in state.Units.Values)
            if (army.NativeArmyOrigin is { } origin &&
                (origin.Language != parameters.Language || !origin.RuleVersion.Equals(parameters.RuleVersion,
                    StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Owned Army and current parameter source identities differ.");
        if (simulation.World.PendingArmyIds?.Any(id => id < 0) == true ||
            simulation.World.ObjectPairs?.Any(pair => pair is null || pair.FirstArmyId < 0 ||
                pair.SecondArmyId < 0 || pair.BaseLocationId < 0) == true)
            throw new InvalidDataException("Owned native manager lists have invalid typed identities.");
        if (simulation.World.SamplerEpoch is { } epoch) NativeArmySamplerEpochValidation.Validate(state, epoch);
        NativeNormalArmyAftermathStateValidation.Validate(state);
    }
}

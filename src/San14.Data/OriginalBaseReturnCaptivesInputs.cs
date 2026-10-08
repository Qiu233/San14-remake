using San14.Core;

namespace San14.Data;

/// <summary>CFoothold's independent signed int32+50.221090 and226FA0
/// explicitly clear it;223B80 adds with native wrap. It is not268230's
/// troop capacity and is never derived from Core inventory differences.</summary>
public static class OriginalBaseReturnCaptivesInputs
{
    public const string VersionKey = "ce-base-return-captives-current:version";
    public static string Field50Key(OriginalBaseReference target) =>
        $"ce-base-return-captives-current:{target.Kind}:{Physical(target)}:50";

    public static GameState InstallFresh(GameState state)
    {
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Language switch { "SC" => simulation.Parameters.ExecutableSha256 != OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
                "TC" => simulation.Parameters.ExecutableSha256 != OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256, _ => true } ||
            simulation.Parameters.Int32Values.ContainsKey(VersionKey) ||
            Targets().Any(target => simulation.Parameters.Int32Values.ContainsKey(Field50Key(target))))
            throw new InvalidDataException("Base50 requires its unused fresh CE allocation.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        foreach (var target in Targets()) values.Add(Field50Key(target), 0);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static int? ReadCurrent50(GameState state, OriginalBaseReference target) =>
        state.NativeArmySimulation?.Parameters is { } bank && bank.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        !bank.Float32Values.ContainsKey(Field50Key(target)) && bank.Int32Values.TryGetValue(Field50Key(target), out int value) ? value : null;

    public static GameState WithCurrent50(GameState state, OriginalBaseReference target, int? value)
    {
        var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("Current Base50 parameter owner is unknown.");
        if (simulation.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1)
            throw new InvalidDataException("Current Base50 allocation is unknown.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        if (value is int known) values[Field50Key(target)] = known; else values.Remove(Field50Key(target));
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static void BindCurrent(GameState state, OriginalEventInitializationContext current)
    {
        current.ClearBaseReturnCaptivesBinding();
        foreach (var target in Targets()) current.BindBaseReturnField50(target, ReadCurrent50(state, target));
    }

    public static GameState PublishCurrent(GameState state, OriginalEventInitializationContext current)
    {
        if (current.BaseReturnField50Writes.Count == 0) return state;
        var simulation = state.NativeArmySimulation ?? throw new InvalidDataException("Reached Base50 writer lacks a current parameter owner.");
        if (simulation.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1)
            throw new InvalidDataException("Reached Base50 writer lacks its saved allocation.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var target in current.BaseReturnField50Writes)
            if (current.ReadBaseReturnField50(target) is int value) values[Field50Key(target)] = value;
            else values.Remove(Field50Key(target));
        current.BaseReturnField50Writes.Clear();
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    internal static IEnumerable<OriginalBaseReference> Targets() =>
        Enumerable.Range(0, 52).Select(slot => new OriginalBaseReference(OriginalBaseKind.City, slot))
            .Concat(Enumerable.Range(0, 11).Select(slot => new OriginalBaseReference(OriginalBaseKind.Gate, slot)));
    private static int Physical(OriginalBaseReference target) => target.PhysicalSlot is int slot &&
        target.Kind switch { OriginalBaseKind.City => slot is >= 0 and <= 51, OriginalBaseKind.Gate => slot is >= 0 and <= 10, _ => false }
        ? slot : throw new InvalidDataException("Base50 physical receiver is outside its actual pointer table.");
}

public sealed partial class OriginalEventInitializationContext
{
    private readonly Dictionary<OriginalBaseReference, int?> _baseReturnFields50 = [];
    internal HashSet<OriginalBaseReference> BaseReturnField50Writes { get; } = [];
    public int? ReadBaseReturnField50(OriginalBaseReference target) => _baseReturnFields50.GetValueOrDefault(target);
    public void SetBaseReturnField50(OriginalBaseReference target, int? value)
    {
        _ = OriginalBaseReturnCaptivesInputs.Field50Key(target);
        _baseReturnFields50[target] = value; BaseReturnField50Writes.Add(target);
    }
    /// <summary>Actual223B80 signed addition. Missing current data is not0.</summary>
    public void AddBaseReturnField50(OriginalBaseReference target, int amount) => SetBaseReturnField50(target,
        unchecked((ReadBaseReturnField50(target) ?? throw new InvalidDataException("current.223B80_Base50")) + amount));
    internal void BindBaseReturnField50(OriginalBaseReference target, int? value) => _baseReturnFields50[target] = value;
    internal void ClearBaseReturnCaptivesBinding() { _baseReturnFields50.Clear(); BaseReturnField50Writes.Clear(); }
    internal void CopyBaseReturnCaptivesInputsTo(OriginalEventInitializationContext draft)
    {
        foreach (var row in _baseReturnFields50) draft._baseReturnFields50[row.Key] = row.Value;
        draft.BaseReturnField50Writes.UnionWith(BaseReturnField50Writes);
    }
    internal void CommitBaseReturnCaptivesFields(OriginalEventInitializationContext draft)
    {
        foreach (var target in draft.BaseReturnField50Writes) SetBaseReturnField50(target, draft.ReadBaseReturnField50(target));
    }
}

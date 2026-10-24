using San14.Core;

namespace San14.Data;

/// <summary>CE fresh defaults from the retained SC/TC initial parameter tables.
/// Restoring a campaign never installs or repairs missing parameter knowledge.</summary>
public static class PersonnelCommandParameters
{
    public const string Version = "simulation.personnel.version";
    public const string ItemCommandBooks = "simulation.personnel.item-command-books";
    public const string ConfiscationPenalty = "simulation.personnel.confiscation-penalty";
    public const string Revision = "san14:ce-item-personnel-v1";

    internal static GameState InstallFresh(GameState state)
    {
        if (state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0 } ||
            state.NativeArmySimulation is not { Parameters.Language: "SC" or "TC" } native)
            throw new InvalidDataException("Personnel parameters require a fresh CE campaign.");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values);
        foreach (var pair in new[] { (Version, 1), (ItemCommandBooks, 1), (ConfiscationPenalty, 5) })
            if (native.Parameters.Float32Values.ContainsKey(pair.Item1) || !values.TryAdd(pair.Item1, pair.Item2))
                throw new InvalidDataException("Personnel parameters already have an owner.");
        return state with { NativeArmySimulation = native with
            { Parameters = native.Parameters with { Int32Values = values } } };
    }

    internal static int? Read(GameState state, string key) =>
        state.NativeArmySimulation?.Parameters is { } parameters &&
        parameters.Int32Values.GetValueOrDefault(Version) == 1 &&
        !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
            ? value : null;
}

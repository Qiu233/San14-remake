using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>The first implemented special-command parameter did not exist in
/// saves through218. This format upgrade installs its independently extracted
/// SC/TC initial rule value without replacing a current supplied Int32 value.</summary>
internal static class OfficerSpecialCapacitySaveMigration
{
    internal static void Apply(JsonObject state, int version)
    {
        if (version >= 219 || state["NativeArmySimulation"]?["Parameters"] is not JsonObject parameters ||
            parameters["Language"]?.GetValue<string>() is not ("TC" or "SC") ||
            parameters["Int32Values"] is not JsonObject values) return;
        string key = SimulationParameterKeys.Tactics.SpecialOfficerCommandCapacity;
        if (parameters["Float32Values"] is JsonObject floats && floats.ContainsKey(key))
            throw new InvalidDataException("Special officer command capacity has a conflicting Float32 owner.");
        if (!values.ContainsKey(key)) values[key] = SimulationParameterKeys.Tactics.InitialSpecialOfficerCommandCapacity;
    }
}

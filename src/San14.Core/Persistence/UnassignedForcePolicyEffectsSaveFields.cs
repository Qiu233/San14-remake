using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>World196 owns the unassigned Force's current doctrine-panel sets.
/// Older saves contain no evidence for these sets and remain unknown.</summary>
internal static class UnassignedForcePolicyEffectsSaveFields
{
    private const string Field = "UnassignedForcePolicyEffects";

    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 196 && state[Field] is not null)
            throw Invalid();
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 196)
        {
            state[Field] = null;
            return;
        }
        ValidateWorld(state);
    }

    internal static void ValidateWorld(JsonObject state)
    {
        if (!state.ContainsKey(Field)) throw Invalid();
        if (state[Field] is null) return;
        if (state[Field] is not JsonObject effects || effects.Count != 2 ||
            !Set(effects, "ActiveEffectIds") || !Set(effects, "SuppressedEffectIds"))
            throw Invalid();
    }

    private static bool Set(JsonObject effects, string name)
    {
        if (!effects.ContainsKey(name)) return false;
        if (effects[name] is null) return true;
        if (effects[name] is not JsonArray values || values.Count > 80) return false;
        var seen = new HashSet<int>();
        foreach (JsonNode? node in values)
            if (node is not JsonValue value || !value.TryGetValue<int>(out int id) ||
                id is < 1 or > 80 || !seen.Add(id)) return false;
        return true;
    }

    private static InvalidDataException Invalid() =>
        new("Saved unassigned Force policy effects are missing or invalid.");
}

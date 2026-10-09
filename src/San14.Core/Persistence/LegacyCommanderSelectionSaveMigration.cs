using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Joins the historical split count and candidate windows once at
/// the save boundary. A missing window remains unknown, including when the
/// known count is zero. Inactive entries and duplicate identities retain order.</summary>
internal static class LegacyCommanderSelectionSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state.ContainsKey("CommanderSelection"))
            throw new InvalidDataException("Legacy save contains conflicting commander selection owners.");
        JsonObject? world = (state["NativeArmySimulation"] as JsonObject)?["World"] as JsonObject;
        byte? count = null;
        if (world is not null)
        {
            if (!world.ContainsKey("SelectedCommanderCount165D") ||
                world["SelectedCommanderCount165D"] is JsonNode countNode &&
                (countNode is not JsonValue scalar || !scalar.TryGetValue<byte>(out _)))
                throw new InvalidDataException("Legacy commander selection count is invalid.");
            count = world["SelectedCommanderCount165D"]?.GetValue<byte>();
        }
        JsonArray? entries = null;
        if (state["NativePersonCaptureWorldCandidates"] is JsonNode oldNode)
        {
            if (oldNode is not JsonObject old ||
                old["Revision"] is not JsonValue revision || !revision.TryGetValue<string>(out string? version) ||
                string.IsNullOrWhiteSpace(version) || world is null || count > 5 ||
                old["PersonIds165E"] is not JsonArray identities || identities.Count != 5 ||
                old["Values1668"] is not JsonArray values || values.Count != 5)
                throw new InvalidDataException("Legacy commander selection windows are invalid.");
            entries = [];
            for (int ordinal = 0; ordinal < identities.Count; ordinal++)
            {
                if (!NullableScalar<ushort>(identities[ordinal]) || !NullableScalar<int>(values[ordinal]))
                    throw new InvalidDataException("Legacy commander candidate channels are invalid.");
                entries.Add(new JsonObject { ["OfficerId"] = identities[ordinal]?.DeepClone(),
                    ["UninterpretedValue"] = values[ordinal]?.DeepClone() });
            }
        }
        state["CommanderSelection"] = world is not null || entries is not null
            ? new JsonObject { ["ActiveCount"] = count, ["Candidates"] = entries } : null;
        state.Remove("NativePersonCaptureWorldCandidates");
        world?.Remove("SelectedCommanderCount165D");
    }

    private static bool NullableScalar<T>(JsonNode? node) where T : struct =>
        node is null || node is JsonValue scalar && scalar.TryGetValue<T>(out _);
}

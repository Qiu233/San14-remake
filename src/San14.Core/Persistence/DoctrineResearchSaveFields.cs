using System.Text.Json.Nodes;

namespace San14.Core;

internal static class DoctrineResearchSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["Forces"] is not JsonObject forces) return;
        foreach (var pair in forces)
        {
            if (pair.Value is not JsonObject force) throw new InvalidDataException("Saved Force is incomplete.");
            if (version < 163)
            {
                if (force["DoctrineResearch"] is not null)
                    throw new InvalidDataException("Legacy Force contains a future research frontier.");
                force["DoctrineResearch"] = null;
                continue;
            }
            if (!force.ContainsKey("DoctrineResearch"))
                throw new InvalidDataException("Saved research frontier knowledge is missing.");
            if (force["DoctrineResearch"] is not JsonNode saved) continue;
            if (saved is not JsonObject research || research["AvailablePanelIds"] is not JsonArray panels)
                throw new InvalidDataException("Saved research frontier is invalid.");
            var ids = new HashSet<int>();
            foreach (var panel in panels)
                if (panel is not JsonValue field || !field.TryGetValue<int>(out int id) || id <= 0 || !ids.Add(id))
                    throw new InvalidDataException("Saved research panel identity is invalid.");
        }
    }
}

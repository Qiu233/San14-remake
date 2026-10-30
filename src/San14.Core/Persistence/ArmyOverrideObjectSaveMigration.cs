using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Schema216 adds an optional typed Object override destination.
/// The old save has no such identity; its known/raw/unknown values stay intact.</summary>
internal static class ArmyOverrideObjectSaveMigration
{
    internal static void Apply(JsonObject state, int version)
    {
        if (version >= 216) return;
        //A single old-document walk covers active/pending receivers and the
        //nested automatic/manual effect continuations before typed decoding.
        Visit(state);
    }

    private static void Visit(JsonNode? node)
    {
        if (node is JsonObject record)
        {
            if (record["OverrideTarget"] is JsonObject target && target["Destination"] is JsonObject destination)
            {
                if (destination.ContainsKey("MapObjectReceiverId"))
                    throw new InvalidDataException("Legacy override contains a future Object identity field.");
                destination["MapObjectReceiverId"] = null;
            }
            foreach (var child in record) Visit(child.Value);
        }
        else if (node is JsonArray items)
            foreach (var item in items) Visit(item);
    }
}

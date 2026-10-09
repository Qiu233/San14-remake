using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Converts historical effect bitmaps once; simulation does not use their layout.</summary>
internal static class LegacyForcePolicyEffectsSaveMigration
{
    public static void Apply(JsonObject state)
    {
        var forces = state["Forces"] as JsonObject
            ?? throw new InvalidDataException("Saved forces are missing.");
        foreach (JsonNode? node in forces.Select(pair => pair.Value))
            if (node is JsonObject force) force["PolicyEffects"] = null;
        if (state["NativeArmySimulation"]?["Forces"] is not JsonObject legacy) return;
        foreach ((string id, JsonNode? node) in legacy)
        {
            if (node is not JsonObject old || forces[id] is not JsonObject owner ||
                !old.ContainsKey("ActivePolicyBitmap1CB") || !old.ContainsKey("SuppressedPolicyBitmap1D5"))
                throw new InvalidDataException("Legacy policy effects have incomplete source or force ownership.");
            owner["PolicyEffects"] = new JsonObject
            {
                ["ActiveEffectIds"] = ReadBitmap(old["ActivePolicyBitmap1CB"]),
                ["SuppressedEffectIds"] = ReadBitmap(old["SuppressedPolicyBitmap1D5"])
            };
            old.Remove("ActivePolicyBitmap1CB");
            old.Remove("SuppressedPolicyBitmap1D5");
        }
    }

    private static JsonArray? ReadBitmap(JsonNode? node)
    {
        if (node is null) return null;
        if (node is not JsonValue value || !value.TryGetValue<string>(out string? encoded))
            throw new InvalidDataException("Legacy policy bitmap is invalid.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded!); }
        catch (FormatException error) { throw new InvalidDataException("Legacy policy bitmap is invalid.", error); }
        if (bytes.Length != 10) throw new InvalidDataException("Legacy policy bitmap width is invalid.");
        var result = new JsonArray();
        for (int bit = 0; bit < bytes.Length * 8; bit++)
            if ((bytes[bit / 8] & (1 << (bit % 8))) != 0) result.Add(bit + 1);
        return result;
    }
}

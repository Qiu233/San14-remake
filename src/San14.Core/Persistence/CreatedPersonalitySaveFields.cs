using System.Text.Json.Nodes;

namespace San14.Core;

internal static class CreatedPersonalitySaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version < 189 && state["CreatedPersonalities"] is not null)
            throw new InvalidDataException("Legacy world contains future created personality definitions.");
    }

    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 189)
        {
            state["CreatedPersonalities"] = null;
            return;
        }
        if (!state.ContainsKey("CreatedPersonalities")) throw Invalid();
        if (state["CreatedPersonalities"] is not JsonNode saved) return;
        if (saved is not JsonArray definitions || definitions.Count > 100) throw Invalid();
        var identities = new HashSet<int>();
        foreach (var node in definitions)
        {
            if (node is not JsonObject fields || fields.Count != 10 ||
                !Integer(fields, "Id", 256, 355) || !identities.Add(fields["Id"]!.GetValue<int>()) ||
                fields["Name"] is not JsonValue name || !name.TryGetValue<string>(out string? text) ||
                text is null || text.Length > 5 || text.Contains('\0') ||
                !Integer(fields, "ConditionKind", 0, 100) || !Unsigned(fields, "ConditionValue") ||
                !Integer(fields, "OptionKind", 0, 100) || !Unsigned(fields, "OptionValue") ||
                !Integer(fields, "Effect1Code", 0, 126) || !Integer(fields, "Effect1Value", -100, 2000) ||
                !Integer(fields, "Effect2Code", 0, 126) || !Integer(fields, "Effect2Value", -100, 2000))
                throw Invalid();
        }
    }

    private static bool Integer(JsonObject fields, string name, int minimum, int maximum) =>
        fields[name] is JsonValue value && value.TryGetValue<int>(out int number) && number >= minimum && number <= maximum;

    private static bool Unsigned(JsonObject fields, string name) =>
        fields[name] is JsonValue value && value.TryGetValue<uint>(out _);

    private static InvalidDataException Invalid() =>
        new("Saved created personality definitions are missing or invalid.");
}

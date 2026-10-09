using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Retires an old write-only snapshot. It never contributed to remake rules.</summary>
internal static class LegacyUnusedOfficerFreedomSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["Officers"] is not JsonObject officers) return;
        foreach (JsonNode? node in officers.Select(pair => pair.Value))
        {
            if (node is not JsonObject officer) continue;
            if (officer["NativeFreedomRuntime"] is JsonNode carrier)
            {
                if (carrier is not JsonObject snapshot ||
                    snapshot["Revision"] is not JsonValue revision ||
                    !revision.TryGetValue<string>(out string? revisionId) || string.IsNullOrWhiteSpace(revisionId) ||
                    !NullableScalar<ulong>(snapshot, "Qword18A") ||
                    !NullableScalar<uint>(snapshot, "Dword192") ||
                    !NullableScalar<ushort>(snapshot, "Word19E"))
                    throw new InvalidDataException("Legacy officer freedom snapshot is invalid.");
            }
            officer.Remove("NativeFreedomRuntime");
        }
    }

    private static bool NullableScalar<T>(JsonObject owner, string name) where T : struct =>
        owner.ContainsKey(name) && (owner[name] is null || owner[name] is JsonValue value && value.TryGetValue<T>(out _));
}

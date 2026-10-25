using System.Text.Json.Nodes;

namespace San14.Core;

internal static class MonthlyMaintenanceSaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (version < 164 && state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray legacyBatches)
            foreach (var batch in legacyBatches.OfType<JsonObject>())
                if (batch["Requests"] is JsonArray requests)
                    foreach (var request in requests.OfType<JsonObject>())
                        if (request["MonthlyMaintenance"]?["Operation"] is JsonValue operation &&
                            operation.TryGetValue<int>(out int kind) &&
                            kind >= (int)MonthlyMaintenanceOperation.DisasterSceneEnter)
                            throw new InvalidDataException("Legacy monthly request contains a future disaster scene.");
        if (state["Areas"] is JsonObject areas)
            foreach (var pair in areas)
            {
                if (pair.Value is not JsonObject area)
                    throw new InvalidDataException("Saved Area is incomplete.");
                if (version < 161)
                {
                    if (area["MonthlyTimers"] is not null)
                        throw new InvalidDataException("Legacy Area contains future monthly timers.");
                    area["MonthlyTimers"] = null;
                }
                else
                {
                    if (!area.ContainsKey("MonthlyTimers"))
                        throw new InvalidDataException("Saved Area monthly timer knowledge is missing.");
                    if (area["MonthlyTimers"] is JsonNode saved &&
                        (saved is not JsonObject timers || !NullableByte(timers, "First") || !NullableByte(timers, "Second")))
                        throw new InvalidDataException("Saved Area monthly timers are invalid.");
                }
            }

        if (state["CampaignEffectRequests"]?["PendingBatches"] is not JsonArray batches) return;
        foreach (var item in batches)
        {
            if (item is not JsonObject batch) throw new InvalidDataException("Saved campaign batch is invalid.");
            if (version < 161 && batch["Source"] is JsonValue sourceValue &&
                sourceValue.TryGetValue<int>(out int source) && source >= (int)CampaignEffectSource.MonthlyMaintenance)
                throw new InvalidDataException("Legacy campaign batch has a future monthly maintenance origin.");
            if (batch["Requests"] is not JsonArray requests) continue;
            foreach (var request in requests)
            {
                if (request is not JsonObject fields) throw new InvalidDataException("Saved campaign request is invalid.");
                if (version < 161)
                {
                    if (fields["MonthlyMaintenance"] is not null)
                        throw new InvalidDataException("Legacy request contains future monthly maintenance.");
                    fields["MonthlyMaintenance"] = null;
                    continue;
                }
                if (!fields.ContainsKey("MonthlyMaintenance"))
                    throw new InvalidDataException("Saved monthly maintenance request knowledge is missing.");
                if (fields["MonthlyMaintenance"] is not JsonNode payload) continue;
                if (payload is not JsonObject savedRequest ||
                    savedRequest["Operation"] is not JsonValue operation || !operation.TryGetValue<int>(out int kind) ||
                    !Enum.IsDefined((MonthlyMaintenanceOperation)kind) ||
                    !NullableInt(savedRequest, "EventKind") || !NullableUshort(savedRequest, "SpeakerId") ||
                    !NullableInt(savedRequest, "ForceId") || !NullableInt(savedRequest, "HexId") ||
                    !NullableInt(savedRequest, "SceneryId") || !NullableInt(savedRequest, "ArmyId") ||
                    !savedRequest.ContainsKey("TextAtSource") || savedRequest["TextAtSource"] is JsonNode text &&
                    (text is not JsonValue textValue || !textValue.TryGetValue<string>(out _)))
                    throw new InvalidDataException("Saved monthly maintenance request is invalid.");
            }
        }
    }

    private static bool NullableByte(JsonObject owner, string name) => owner.ContainsKey(name) &&
        (owner[name] is null || owner[name] is JsonValue value && value.TryGetValue<byte>(out _));
    private static bool NullableUshort(JsonObject owner, string name) => owner.ContainsKey(name) &&
        (owner[name] is null || owner[name] is JsonValue value && value.TryGetValue<ushort>(out _));
    private static bool NullableInt(JsonObject owner, string name) => owner.ContainsKey(name) &&
        (owner[name] is null || owner[name] is JsonValue value && value.TryGetValue<int>(out _));
}

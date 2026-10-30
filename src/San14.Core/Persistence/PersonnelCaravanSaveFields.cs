using System.Text.Json.Nodes;

namespace San14.Core;

internal static class PersonnelCaravanSaveFields
{
    internal static void RejectLegacyFields(JsonObject state, int version)
    {
        if (version >= 182) return;
        if (state["PersonnelCommands"]?["Caravans"] is not null ||
            state["NativePeriodContinuation"]?["Cursor"]?["AwaitingCaravanTaskId"] is not null ||
            state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray batches && batches.OfType<JsonObject>().Any(batch =>
                Integer(batch["Source"], (int)CampaignEffectSource.ForeignTradeCommands, int.MaxValue)))
            throw new InvalidDataException("Legacy world contains future overseas caravan state.");
    }
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["PersonnelCommands"] is JsonObject owner)
        {
            if (version < 182) owner["Caravans"] = null;
            else
            {
                if (!owner.ContainsKey("Caravans")) throw Invalid();
                if (owner["Caravans"] is JsonNode node)
                {
                    if (node is not JsonObject tasks) throw Invalid();
                    foreach (var pair in tasks)
                    {
                        if (!int.TryParse(pair.Key, out int id) || id < 0 || pair.Value is not JsonObject task ||
                            task.Count != 28 || !new[] { "Id", "DispatchSlot", "MemberIds", "NativeKind", "CountryId",
                                "SourceForceId", "SourceDistrictId", "SourceLocationId", "StartHexId", "EndHexId", "HomeLocationId",
                                "StartDate", "DueDate", "OfferedItemId", "GoldOffer", "SuppliesOffer", "TroopOffer", "RewardStage",
                                "JourneyEvent", "ReturnDisposition", "Started", "NeedsRefresh", "Cancelled", "IsRetiring", "IsAssociated",
                                "ReportToPlayer", "SavedResult", "PendingReport" }.All(task.ContainsKey) ||
                            !Integer(task["Id"], id, id)) throw Invalid();
                        if (task["PendingReport"] is JsonNode report && (report is not JsonObject fields || fields.Count != 7 ||
                            !new[] { "Successful", "GoldAmount", "ItemId", "GemId", "GemCount", "WeaponId", "BonusGold" }.All(fields.ContainsKey))) throw Invalid();
                    }
                }
            }
        }
        if (state["NativePeriodContinuation"]?["Cursor"] is JsonObject cursor)
        {
            if (version < 182) cursor["AwaitingCaravanTaskId"] = null;
            else if (!cursor.ContainsKey("AwaitingCaravanTaskId") || cursor["AwaitingCaravanTaskId"] is JsonNode id &&
                !Integer(id, 0, int.MaxValue)) throw Invalid();
        }
    }
    private static bool Integer(JsonNode? node, int min, int max) => node is JsonValue value &&
        value.TryGetValue<int>(out int number) && number >= min && number <= max;
    private static InvalidDataException Invalid() => new("Saved overseas caravan state is missing or invalid.");
}

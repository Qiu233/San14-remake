using System.Text.Json.Nodes;

namespace San14.Core;

internal static class DiplomacySaveFields
{
    internal static void Migrate(JsonObject state, int version)
    {
        if (state["PersonnelCommands"] is JsonObject owner)
        {
            if (version < 149)
            {
                if (owner["Diplomacies"] is not null)
                    throw new InvalidDataException("Legacy save contains future diplomacy tasks.");
                owner["Diplomacies"] = null;
            }
            else if (!owner.ContainsKey("Diplomacies"))
                throw new InvalidDataException("Saved diplomacy task knowledge is missing.");
            if (owner["Diplomacies"] is JsonNode saved)
            {
                if (saved is not JsonObject tasks) throw new InvalidDataException("Saved diplomacy tasks are invalid.");
                foreach (var task in tasks)
                {
                    if (task.Value is not JsonObject fields || new[] { "Id", "DispatchSlot", "MemberIds", "Kind", "NativeKind",
                        "ForceId", "SourceCityId", "SourceDistrictId", "TargetForceId", "TargetRulerOfficerId", "TargetCapitalCityId",
                        "AllianceMonths", "TargetCityId", "InitialOffer", "StartHexId", "EndHexId", "StartDate", "DueDate",
                        "Started", "NeedsRefresh", "Cancelled", "AwaitingNegotiation", "IsRetiring", "IsAssociated", "NegotiationDate" }.Any(key => !fields.ContainsKey(key)))
                        throw new InvalidDataException("Saved diplomacy task fields are incomplete.");
                    foreach (string name in new[] { "StartDate", "DueDate" })
                        if (fields[name] is not JsonObject date || new[] { "Year", "Month", "Day", "Hour" }.Any(key => !date.ContainsKey(key)))
                            throw new InvalidDataException("Saved diplomacy task date is incomplete.");
                    if (fields["InitialOffer"] is not JsonObject offer || new[] { "Gold", "Supplies", "ItemId", "AreaId" }.Any(key => !offer.ContainsKey(key)))
                        throw new InvalidDataException("Saved diplomacy offer is incomplete.");
                    MigrateNegotiation(fields,version);
                }
            }
        }
        if (state["CampaignEffectRequests"]?["PendingBatches"] is JsonArray batches)
            foreach (var batch in batches.OfType<JsonObject>())
            {
                if (version < 149 && batch["Source"] is JsonValue source && source.TryGetValue<int>(out int origin) && origin >= (int)CampaignEffectSource.DiplomacyCommands)
                    throw new InvalidDataException("Legacy save contains future diplomacy presentation.");
                foreach (var request in (batch["Requests"] as JsonArray ?? []).OfType<JsonObject>())
                {
                    if (version < 149)
                    {
                        if (request["DiplomacyArrivalAudio"] is not null) throw new InvalidDataException("Legacy save contains future diplomacy audio.");
                        request["DiplomacyArrivalAudio"] = null;
                    }
                    else if (!request.ContainsKey("DiplomacyArrivalAudio")) throw new InvalidDataException("Saved diplomacy audio knowledge is missing.");
                }
            }
        if (state["NativeArmySimulation"]?["Forces"] is JsonObject forces)
            foreach (var force in forces.Select(pair => pair.Value).OfType<JsonObject>())
            {
                if (version < 149)
                { force["DiplomacyFlags178"] = null; force["DiplomacyCooldownDate"] = null; }
                else if (!force.ContainsKey("DiplomacyFlags178")) throw new InvalidDataException("Saved diplomacy Force flags are missing.");
                if (version >= 149 && !force.ContainsKey("DiplomacyCooldownDate")) throw new InvalidDataException("Saved diplomacy cooldown knowledge is missing.");
                if (force["DiplomacyCooldownDate"] is JsonNode cooldown && (cooldown is not JsonObject date ||
                    new[] { "Year", "Month", "Day", "Hour" }.Any(key => !date.ContainsKey(key))))
                    throw new InvalidDataException("Saved diplomacy cooldown date is incomplete.");
                if (force["DiplomacyFlags178"] is JsonNode flag &&
                    (flag is not JsonValue value || !value.TryGetValue<ushort>(out _)))
                    throw new InvalidDataException("Saved diplomacy Force flags are invalid.");
            }
    }

    private static void MigrateNegotiation(JsonObject task,int version)
    {
        if(version<153)
        {
            if(task["Negotiation"] is not null)
                throw new InvalidDataException("Legacy save contains future diplomacy negotiation state.");
            task["Negotiation"]=null;
            return;
        }
        if(!task.ContainsKey("Negotiation"))
            throw new InvalidDataException("Saved diplomacy negotiation knowledge is missing.");
        if(task["Negotiation"] is not JsonNode saved)return;
        if(saved is not JsonObject fields || new[]{"Stage","Proposal","Probability","CanEditConditions","ForcedResult",
            "SenderFirstExpense","RecipientExpense","SenderFinalExpense","PersuasionLocked"}.Any(key=>!fields.ContainsKey(key)))
            throw new InvalidDataException("Saved diplomacy negotiation fields are incomplete.");
        if(fields["Stage"] is not JsonValue stage||!stage.TryGetValue<int>(out int raw)||
            !Enum.IsDefined((DiplomaticNegotiationStage)raw))
            throw new InvalidDataException("Saved diplomacy negotiation stage is invalid.");
        if(fields["Probability"] is not JsonValue probability||!probability.TryGetValue<int>(out _)||
            !Boolean("CanEditConditions")||!Boolean("PersuasionLocked")||
            fields["ForcedResult"] is not null&&!Boolean("ForcedResult"))
            throw new InvalidDataException("Saved diplomacy negotiation values are invalid.");
        foreach(string name in new[]{"SenderFirstExpense","RecipientExpense","SenderFinalExpense"})
            if(fields[name] is not JsonValue expense||!expense.TryGetValue<int>(out int value)||value<0)
                throw new InvalidDataException("Saved diplomacy negotiation expense is invalid.");
        if(fields["Proposal"] is not JsonObject offer||new[]{"Gold","Supplies","ItemId","AreaId"}.Any(key=>!offer.ContainsKey(key)))
            throw new InvalidDataException("Saved diplomacy negotiation proposal is incomplete.");
        bool Boolean(string name)=>fields[name] is JsonValue value&&value.TryGetValue<bool>(out _);
    }
}

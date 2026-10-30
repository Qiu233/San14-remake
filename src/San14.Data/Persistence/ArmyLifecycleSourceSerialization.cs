using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Data;

internal static class ArmyLifecycleSourceSerialization
{
    internal static ArmyLifecycleSourceState Decode(JsonNode node, int resourceVersion = 13)
    {
        if (node is not JsonObject source || source["Slots"] is not JsonArray slots ||
            source.Count != 7 || new[] { "Revision", "Language", "RuleVersion", "SourceScenarioUri", "SourceScenarioSha256", "ArmyFlags150" }
                .Any(key => !source.ContainsKey(key)))
            throw new InvalidDataException("Saved army source knowledge is incomplete.");
        foreach (var item in slots)
        {
            if (resourceVersion <= 12 && item is JsonObject oldSlot)
            {
                if (oldSlot.ContainsKey("AiActionFlags") || oldSlot.ContainsKey("AiEvaluation"))
                    throw new InvalidDataException("Historical Army source cannot contain future Army AI state.");
                oldSlot["AiActionFlags"] = null;
                oldSlot["AiEvaluation"] = null;
            }
            if (item is not JsonObject slot || slot.Count != 14 || new[] { "PhysicalSlot", "ReceiverId", "Commander12", "Status10", "Priority14",
                    "AuxiliaryAC", "AuxiliaryF4", "Direction36", "MapUiByte13C", "CommandControl140", "RouteHold56", "RouteGoal1F8", "AiActionFlags", "AiEvaluation" }
                .Any(key => !slot.ContainsKey(key)) || new[] { "ActiveUnitId", "PendingImage", "StoredResources", "OverrideTarget",
                    "GroupAssignmentReady", "OccupancyBindingId" }.Any(slot.ContainsKey) ||
                slot["ReceiverId"] is not JsonObject receiver || receiver.Count != 1 || !receiver.ContainsKey("Value"))
                throw new InvalidDataException("Saved army source slot mixes ownership or omits source knowledge.");
        }
        return source.Deserialize<ArmyLifecycleSourceState>()
            ?? throw new InvalidDataException("Saved army source knowledge is invalid.");
    }
}

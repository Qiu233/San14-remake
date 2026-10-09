using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Reads the live requests of an old save once. Released payloads,
/// descriptor allocation order and free-node storage have no game meaning.</summary>
internal static class LegacyCombatSettlementSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["NativeArmyLifecycle"] is not JsonObject lifecycle ||
            lifecycle["DeferredDestructionQueue"] is not JsonObject legacy) return;
        if (state["PendingCombatSettlements"] is not null)
            throw new InvalidDataException("Save contains conflicting combat settlement owners.");
        var requests = new List<CombatSettlementRequest>();
        var heads = Array(legacy, "Heads10");
        var counts = Array(legacy, "Counts28");
        var nodes = Array(legacy, "Nodes");
        int manager = Int(legacy, "Manager140DescriptorKey");
        if ((uint)manager >= heads.Count || counts.Count != heads.Count)
            throw new InvalidDataException("Legacy combat settlement manager is invalid.");
        for (int i = 0; i < counts.Count; i++)
            if (i != manager && counts[i]?.GetValue<long>() != 0)
                throw new InvalidDataException("Legacy save contains an unsupported extra settlement queue.");
        var visited = new HashSet<int>();
        for (int? at = heads[manager]?.GetValue<int>(); at is int node; at = nodes[node]?["Next30"]?.GetValue<int>())
        {
            if ((uint)node >= nodes.Count || !visited.Add(node) || requests.Count >= CombatSettlementRules.PendingSettlementLimit)
                throw new InvalidDataException("Legacy combat settlement chain is invalid.");
            var payload = nodes[node]?["Payload48"] as JsonObject
                ?? throw new InvalidDataException("Legacy combat settlement payload is missing.");
            requests.Add(Translate(payload));
        }
        if (requests.Count != counts[manager]?.GetValue<long>())
            throw new InvalidDataException("Legacy combat settlement count differs from its requests.");
        state["PendingCombatSettlements"] = JsonSerializer.SerializeToNode(new CombatSettlementQueue(requests));
        lifecycle.Remove("DeferredDestructionQueue");

        CombatSettlementRequest Translate(JsonObject payload)
        {
            var victim = Unit(payload, "VictimArmy0");
            if (victim.Valid)
            {
                if (Int(payload, "NativeMode24") != 1 || Int(payload, "Cause28") != 0)
                    throw new InvalidDataException("Legacy save contains an unsupported army settlement operation.");
                return new UnitDefeatRequest(victim.Id!.Value, Unit(payload, "AttackerArmy10").Id,
                    Base(payload, "AttackerBase18"));
            }
            SettlementBase? target = Base(payload, "VictimBase8");
            if (target is null) return new ExpiredCombatSettlement();
            if (Int(payload, "Raw20") != 0)
                throw new InvalidDataException("Legacy save contains an unsupported base capture mode.");
            return new BaseCaptureRequest(target, Unit(payload, "AttackerArmy10").Id);
        }

        (int? Id, bool Valid) Unit(JsonObject payload, string field)
        {
            JsonObject reference = Reference(payload, field);
            if (Int(reference, "Kind") != 0) throw new InvalidDataException("Legacy unit receiver has another kind.");
            if (reference["PhysicalSlot"] is null) return (null, false);
            int physical = Int(reference, "PhysicalSlot");
            var slots = Array(lifecycle, "Slots");
            if ((uint)physical >= slots.Count || slots[physical] is not JsonObject slot)
                throw new InvalidDataException("Legacy unit receiver is outside the saved lifecycle.");
            int commander = Int(slot, "Commander12");
            bool valid = commander != 0 && Int(slot, "Status10") != 0;
            int? id = slot["ActiveUnitId"]?.GetValue<int>() ?? slot["PendingImage"]?["Id"]?.GetValue<int>();
            if (valid && id is null) throw new InvalidDataException("Legacy valid unit receiver has no saved entity identity.");
            // A receiver already released before this save cannot be rebound to
            // a future occupant. Pending images retain their actual entity ID.
            return (id, valid);
        }

        SettlementBase? Base(JsonObject payload, string field)
        {
            JsonObject reference = Reference(payload, field);
            int kind = Int(reference, "Kind");
            if (kind is not (1 or 2)) throw new InvalidDataException("Legacy base receiver has another kind.");
            if (reference["PhysicalSlot"] is null) return null;
            int physical = Int(reference, "PhysicalSlot");
            if (physical == 0) return null; // legacy lookup sentinel
            int id = kind == 1 ? physical : checked(physical + 51);
            string collection = kind == 1 ? "Cities" : "Gates";
            if (state[collection] is not JsonObject bases || !bases.ContainsKey(id.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                throw new InvalidDataException("Legacy base receiver has no saved game entity.");
            return new(kind == 1 ? SettlementBaseKind.City : SettlementBaseKind.Gate, id);
        }
    }

    private static JsonObject Reference(JsonObject payload, string field) => payload[field] as JsonObject
        ?? throw new InvalidDataException("Legacy settlement receiver is unknown: " + field);
    private static JsonArray Array(JsonObject owner, string field) => owner[field] as JsonArray
        ?? throw new InvalidDataException("Legacy settlement array is missing: " + field);
    private static int Int(JsonObject owner, string field) => owner[field]?.GetValue<int>()
        ?? throw new InvalidDataException("Legacy settlement field is unknown: " + field);
}

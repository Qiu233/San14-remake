using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Converts named live world memberships once. Allocator rows and
/// released payloads never become part of the new game state.</summary>
internal static class LegacyWorldIndexesSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["NativeArmyLifecycle"] is not JsonObject lifecycle || lifecycle["Pool"] is not JsonObject oldPool) return;
        if (lifecycle["Indexes"] is not null) throw new InvalidDataException("Save contains two world membership owners.");
        var pool = oldPool.Deserialize<NativeSharedListPoolState>() ?? throw Missing("primary memberships");
        var slots = lifecycle["Slots"] as JsonArray ?? throw Missing("army identities");
        var unitIds = new Dictionary<int, int>();
        foreach (JsonObject slot in Objects(slots, "army identities"))
        {
            int physical = Number(slot, "PhysicalSlot");
            int? id = slot["ActiveUnitId"]?.GetValue<int>() ?? slot["PendingImage"]?["Id"]?.GetValue<int>();
            if (id is int unitId) unitIds.Add(physical, unitId);
        }
        var officerIds = new Dictionary<int, int>();
        if (state["Officers"] is JsonObject officers)
            foreach (JsonObject person in Objects(officers.Select(pair => pair.Value), "officer identities"))
                if (person["NativeAbilityRuntime"]?["PhysicalSlot"]?.GetValue<int>() is int physical)
                    officerIds.Add(physical, Number(person, "Id"));
        var consumed = new HashSet<int>();
        var registries = new Dictionary<WorldEntityKind, List<WorldEntityReference>?>();
        if (lifecycle["RegistryDescriptors"] is not JsonObject registryHandles) throw Missing("world registries");
        foreach (var registry in registryHandles)
        {
            WorldEntityKind kind = Kind(registry.Key);
            registries.Add(kind, Read(registry.Value)?.Select(payload => Entity(payload, registry.Key)).ToList());
        }
        var bases = new List<BaseIndexState>();
        if (lifecycle["BaseLists"] is not JsonArray baseBindings) throw Missing("base memberships");
        foreach (JsonObject binding in Objects(baseBindings, "base memberships"))
        {
            string kind = Number(binding, "Kind") switch { 0 => "City", 1 => "Gate", _ => throw Missing("base kind") };
            int physical = Number(binding, "PhysicalSlot");
            bases.Add(new(Base(kind, physical), Ids(binding["HexDescriptor"], Hex), Ids(binding["Person88Descriptor"], Officer)));
        }
        var armies = new Dictionary<int, ArmyIndexState>();
        foreach (JsonObject slot in Objects(slots, "army relationships"))
        {
            int physical = Number(slot, "PhysicalSlot");
            if (slot["Trait160Descriptors"] is not JsonArray traits) continue;
            var related = traits.Select(handle => Ids(handle, Army)).ToArray();
            if (unitIds.TryGetValue(physical, out int unitId)) armies.Add(unitId, new(unitId, related));
            else if (related.Any(list => list?.Count > 0)) throw Missing("unrepresented army relationship owner");
            slot.Remove("Trait160Descriptors");
        }
        var caution = new Dictionary<int, List<int?>?>();
        List<int?>? taskOrder = null;
        if (lifecycle["Auxiliaries"] is JsonObject auxiliary)
        {
            if (auxiliary["CautionArmy30Descriptors"] is JsonArray cautionHandles)
                for (int id = 0; id < cautionHandles.Count; id++) caution.Add(id, Ids(cautionHandles[id], Army));
            taskOrder = Ids(auxiliary["AssociatedCommand18Descriptor"], Task);
            auxiliary.Remove("CautionArmy30Descriptors"); auxiliary.Remove("AssociatedCommand18Descriptor");
        }
        var changed = lifecycle["ChangedLists"] as JsonObject;
        var indexes = new WorldIndexesState
        {
            Registries = registries,
            Bases = Read(lifecycle["BaseRegistryDescriptor"])?.Select(payload => Entity(payload, null)).ToList(),
            BaseIndexes = bases, Armies = armies, CautionGroupMembers = caution,
            ReturningPersonnelTaskOrder = taskOrder,
            PendingUnitIds = Ids(lifecycle["Pending70Descriptor"], Army),
            ChangedWorkingUnitIds = changed is null ? null : Ids(changed["Work8"], ChangedArmy),
            ChangedPendingUnitIds = changed is null ? null : Ids(changed["Pending18"], ChangedArmy),
            ChangedProcessing = changed?["Busy90"]?.GetValue<int>() is int busy ? busy != 0 : null
        };
        // Anonymous nonempty saved lists need a business owner; preserving a
        // generic handle directory would keep the old pool as runtime state.
        foreach (int handle in pool.LiveDescriptors)
            if (!consumed.Contains(handle) && pool.Descriptors.TryGetValue(handle, out var row) && row.Count != 0)
                throw new InvalidDataException($"Legacy world list {handle} has no supported persistent owner.");
        WorldIndexesSaveValidation.Validate(indexes);
        lifecycle["Indexes"] = JsonSerializer.SerializeToNode(indexes);
        foreach (string field in new[] { "Pool", "RegistryDescriptors", "Pending70Descriptor", "BaseRegistryDescriptor", "BaseLists", "ChangedLists" })
            lifecycle.Remove(field);

        List<NativeSharedListPayload>? Read(JsonNode? handleNode)
        {
            if (handleNode is null) return null; // unavailable old owner
            if (handleNode is not JsonObject handle) throw Missing("membership handle");
            if (handle["Identity"] is null) return [];
            int id = Number(handle, "Identity");
            if (!pool.LiveDescriptors.Contains(id) || !pool.Descriptors.TryGetValue(id, out var row)) throw Missing("live membership list");
            consumed.Add(id);
            var result = new List<NativeSharedListPayload>(); var visited = new HashSet<int>();
            for (int? at = row.Head; at is int node;)
            {
                if (!visited.Add(node) || !pool.Nodes.TryGetValue(node, out var entry) || entry.Payload is null)
                    throw Missing("membership chain");
                if (entry.Payload.Kind is null) throw Missing("membership payload kind");
                result.Add(entry.Payload); at = entry.Next;
            }
            if (row.Count != result.Count) throw Missing("membership count");
            return result;
        }
        List<int?>? Ids(JsonNode? handle, Func<NativeSharedListPayload, int?> decode) => Read(handle)?.Select(decode).ToList();
        int? Army(NativeSharedListPayload payload)
        {
            if (payload.Kind == "Null" && payload.Identity is null) return null;
            if (payload.Kind != "Registry:Army") throw Missing("army membership payload");
            if (payload.Identity is not int physical) return null;
            return unitIds.TryGetValue(physical, out int id) ? id : throw Missing("retained army identity");
        }
        int? Officer(NativeSharedListPayload payload)
        {
            if (payload.Kind == "Null" && payload.Identity is null) return null;
            if (payload.Kind is not ("Person" or "Registry:Person")) throw Missing("officer membership payload");
            if (payload.Identity is not int physical) return null;
            return officerIds.TryGetValue(physical, out int id) ? id : throw Missing("officer identity");
        }
        int? ChangedArmy(NativeSharedListPayload payload)
        {
            if (payload.Kind == "Null" && payload.Identity is null) return null;
            if (payload.Kind != "Registry:Army") throw Missing("changed army payload");
            // Route-update requests can survive retirement. An old freed
            // receiver has no recoverable unit identity and must remain expired.
            return payload.Identity is int physical && unitIds.TryGetValue(physical, out int id) ? id : null;
        }
        int? Hex(NativeSharedListPayload payload) => payload.Kind switch
        {
            "Hex" or "Registry:Hex" => payload.Identity,
            "Null" when payload.Identity is null => null,
            _ => throw Missing("territory membership payload")
        };
        int? Task(NativeSharedListPayload payload) => payload.Kind == "LinkedEntity" ? payload.Identity : throw Missing("personnel task membership payload");
        WorldEntityReference Entity(NativeSharedListPayload payload, string? expected)
        {
            string sourceKind = payload.Kind.StartsWith("Registry:", StringComparison.Ordinal) ? payload.Kind[9..] :
                payload.Kind.StartsWith("Base:", StringComparison.Ordinal) ? payload.Kind[5..] : throw Missing("world registry payload");
            if (expected is not null && sourceKind != expected) throw Missing("registry payload kind");
            if (sourceKind is "City" or "Gate") return Base(sourceKind, payload.Identity);
            return new(Kind(sourceKind), sourceKind switch
            {
                "Army" => Army(payload), "Person" => Officer(payload), _ => payload.Identity
            });
        }
        WorldEntityReference Base(string kind, int? physical)
        {
            if (physical == 0) return new(kind == "City" ? WorldEntityKind.FallbackCity : WorldEntityKind.FallbackGate, null);
            if (physical is null) return new(kind == "City" ? WorldEntityKind.City : WorldEntityKind.Gate, null);
            int id = kind == "City" ? physical.Value : checked(physical.Value + 51);
            string table = kind == "City" ? "Cities" : "Gates";
            if (state[table] is not JsonObject entities || !entities.ContainsKey(id.ToString(CultureInfo.InvariantCulture))) throw Missing("base identity");
            return new(kind == "City" ? WorldEntityKind.City : WorldEntityKind.Gate, id);
        }
    }

    private static WorldEntityKind Kind(string kind) => kind switch
    {
        "Army" => WorldEntityKind.Army, "City" => WorldEntityKind.City, "Gate" => WorldEntityKind.Gate,
        "Force" => WorldEntityKind.Force, "Area" => WorldEntityKind.Area, "District" => WorldEntityKind.District,
        "Object" => WorldEntityKind.MapObject, "Person" => WorldEntityKind.Officer, "Item" => WorldEntityKind.Item,
        "Scenery" => WorldEntityKind.Landmark, "CautionTroops" => WorldEntityKind.ArmyCautionGroup,
        "Table81B50" => WorldEntityKind.ArmyCaution, _ => throw Missing("registry kind " + kind)
    };
    private static int Number(JsonObject owner, string field) => owner[field]?.GetValue<int>() ?? throw Missing(field);
    private static IEnumerable<JsonObject> Objects(IEnumerable<JsonNode?> rows, string role)
    {
        foreach (var row in rows) yield return row as JsonObject ?? throw Missing(role);
    }
    private static InvalidDataException Missing(string field) => new("Legacy world membership cannot be decoded: " + field + ".");
}

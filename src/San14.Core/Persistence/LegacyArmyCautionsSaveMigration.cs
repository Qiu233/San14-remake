using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

internal static class LegacyArmyCautionsSaveMigration
{
    public static void Apply(JsonObject state)
    {
        if (state["NativeArmyLifecycle"] is not JsonObject lifecycle) return;
        if (!lifecycle.ContainsKey("ArmyCautions")) lifecycle["ArmyCautions"] = null;
        if (lifecycle["Auxiliaries"] is null) { lifecycle.Remove("Auxiliaries"); return; }
        if (lifecycle["Auxiliaries"] is not JsonObject auxiliary) throw Missing("old auxiliary owner shape");
        if (lifecycle["ArmyCautions"] is not null) throw Missing("two caution owners");
        // This old provisional tuple model has no original mechanism or
        // producer. Do not silently reinterpret it as settlement requests.
        if (auxiliary["InteractionPairs140"] is JsonArray { Count: > 0 })
            throw Missing("unsupported historical interaction tuples");
        if (auxiliary["InteractionPairs140"] is not null and not JsonArray) throw Missing("interaction tuple shape");
        if (auxiliary["CautionArmy10"] is not JsonArray armies || auxiliary["CautionArmy12"] is not JsonArray bases ||
            auxiliary["CautionTroops18"] is not JsonArray labels || lifecycle["Slots"] is not JsonArray slots ||
            armies.Count != 1001 || bases.Count != armies.Count || labels.Count != slots.Count)
            throw Missing("old caution table shape");
        var unitIds = new Dictionary<int, int>();
        foreach (var node in slots)
        {
            var slot = node as JsonObject ?? throw Missing("army identity");
            int physical = slot["PhysicalSlot"]?.GetValue<int>() ?? throw Missing("army source identity");
            int? id = slot["ActiveUnitId"]?.GetValue<int>() ?? slot["PendingImage"]?["Id"]?.GetValue<int>();
            if (id is int unitId) unitIds.Add(physical, unitId);
        }
        var cautions = new ArmyCautionsState();
        for (int id = 0; id < armies.Count; id++)
        {
            int? army = Word(armies[id]); int? location = Word(bases[id]);
            ArmyCautionUnitReference unit = army switch
            {
                null => ArmyCautionUnitReference.Unknown,
                0 => ArmyCautionUnitReference.None,
                int physical when physical >= slots.Count => ArmyCautionUnitReference.None,
                int physical => unitIds.TryGetValue(physical, out int unitId)
                    ? new(ArmyCautionUnitKind.Unit, unitId) : ArmyCautionUnitReference.Expired
            };
            // The old value is a resource Object link, not City/Gate index.
            var target = new ArmyCautionBaseReference(location is not null, location is > 0 and <= 61 ? location : null);
            var relation = new ArmyCautionRelation(unit, target);
            if (relation != ArmyCautionRelation.Empty) cautions.Relations.Add(id, relation);
        }
        for (int id = 0; id < labels.Count; id++)
        {
            int? old = Word(labels[id]);
            var label = new ArmyCautionGroupLabel(old is not null, old is > 0 ? old : null);
            if (label != ArmyCautionGroupLabel.Empty) cautions.GroupLabels.Add(id, label);
        }
        ArmyCautionsSaveValidation.Validate(cautions);
        lifecycle["ArmyCautions"] = JsonSerializer.SerializeToNode(cautions);
        lifecycle.Remove("Auxiliaries");
    }

    private static int? Word(JsonNode? node)
    {
        int? value = node?.GetValue<int>();
        return value is < 0 or > ushort.MaxValue ? throw Missing("old caution word") : value;
    }
    private static InvalidDataException Missing(string name) => new("Legacy army cautions cannot be decoded: " + name + ".");
}

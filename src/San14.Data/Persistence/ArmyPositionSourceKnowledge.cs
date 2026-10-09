using System.Text.Json.Nodes;
using San14.Core;

namespace San14.Data;

/// <summary>Only legacy incomplete or contradictory position projections need
/// compatibility data. Ordinary campaigns have one position and no such rows.</summary>
public sealed record ArmyPositionSourceResidue(int UnitId, bool DirectPositionUnavailable,
    ushort? DirectConflictHex, ushort? MarchConflictHex);

internal static class ArmyPositionSourceKnowledge
{
    public static JsonArray Encode(IEnumerable<ArmyPositionSourceResidue> positions)
    {
        var result = new JsonArray();
        foreach (var row in positions) result.Add(new JsonObject { ["UnitId"] = row.UnitId,
            ["DirectPositionUnavailable"] = row.DirectPositionUnavailable,
            ["DirectConflictHex"] = row.DirectConflictHex, ["MarchConflictHex"] = row.MarchConflictHex });
        return result;
    }
    public static IReadOnlyList<ArmyPositionSourceResidue> Decode(JsonArray rows)
    {
        var result = new List<ArmyPositionSourceResidue>();
        foreach (var node in rows)
        {
            if (node is not JsonObject row || row["UnitId"] is not JsonValue id || !id.TryGetValue<int>(out int unitId) || unitId < 0 ||
                result.Any(item => item.UnitId == unitId) || row["DirectPositionUnavailable"] is not JsonValue missing ||
                !missing.TryGetValue<bool>(out bool unavailable) || !row.ContainsKey("DirectConflictHex") || !row.ContainsKey("MarchConflictHex"))
                throw new InvalidDataException("Saved army position compatibility row is invalid.");
            result.Add(new(unitId, unavailable, Read(row["DirectConflictHex"]), Read(row["MarchConflictHex"])));
        }
        return result;
    }
    private static ushort? Read(JsonNode? value) => value is null ? null :
        value is JsonValue node && node.TryGetValue<ushort>(out ushort hex) ? hex :
        throw new InvalidDataException("Saved army position compatibility coordinate is invalid.");

    public static GameState PruneReplacedChannels(GameState state)
    {
        if (state.ResourceState is not CampaignSourceKnowledge knowledge || knowledge.ArmyPositions.Count == 0) return state;
        var units = Images(state).ToDictionary(unit => unit.Id);
        var rows = new List<ArmyPositionSourceResidue>();
        foreach (var row in knowledge.ArmyPositions)
        {
            if (!units.TryGetValue(row.UnitId, out var unit)) continue;
            if (unit.PositionKnowledge == ArmyPositionKnowledge.Conflicting) rows.Add(row);
            else if (row.DirectPositionUnavailable) rows.Add(row with { DirectConflictHex = null, MarchConflictHex = null });
        }
        return state with { ResourceState = knowledge.ReplaceArmyPositions(rows) };
    }
    public static void Validate(GameState state)
    {
        var units = Images(state).ToDictionary(unit => unit.Id);
        var rows = (state.ResourceState as CampaignSourceKnowledge)?.ArmyPositions ?? [];
        foreach (var row in rows)
        {
            if (!units.TryGetValue(row.UnitId, out var unit) ||
                row.DirectPositionUnavailable && row.DirectConflictHex is not null ||
                row.DirectConflictHex is >= ArmyMarchSourceAdapter.SourceHexCount ||
                row.MarchConflictHex is not null && (unit.March is null || row.MarchConflictHex != unit.HexId) ||
                unit.PositionKnowledge == ArmyPositionKnowledge.Unknown)
                throw new InvalidDataException("Saved army position compatibility owner is invalid.");
            if (unit.PositionKnowledge == ArmyPositionKnowledge.Conflicting)
            {
                if (row.DirectPositionUnavailable || row.DirectConflictHex is null || row.DirectConflictHex == unit.HexId)
                    throw new InvalidDataException("Saved conflicting army position has no conflicting source knowledge.");
            }
            else if (!row.DirectPositionUnavailable || row.DirectConflictHex is not null || row.MarchConflictHex is not null)
                throw new InvalidDataException("Saved army position retains superseded source coordinates.");
        }
        foreach (var unit in units.Values)
            if (unit.PositionKnowledge == ArmyPositionKnowledge.Conflicting && !rows.Any(row => row.UnitId == unit.Id))
                throw new InvalidDataException("Saved conflicting army position is missing its compatibility owner.");
    }
    private static IEnumerable<UnitState> Images(GameState state) => state.Units.Values.Concat(
        state.ArmyLifecycle?.Receivers.Entries.Where(slot => slot.PendingImage is not null).Select(slot => slot.PendingImage!) ?? []);
}

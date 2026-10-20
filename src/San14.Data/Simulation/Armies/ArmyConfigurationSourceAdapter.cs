using System.Text.Json.Nodes;
using San14.Core;

namespace San14.Data;

public readonly record struct ArmyConfigurationSourceOwner(int UnitId, int? DefeatedForceId = null);

/// <summary>Only original action/category codes and unclassified formation codes
/// are retained here. Known formation identities belong to the simulation.</summary>
public sealed record ArmyConfigurationSourceKnowledge(
    ArmyConfigurationSourceOwner Owner, byte? ActionCode, byte? CategoryCode,
    byte? UnmappedLandCode, byte? UnmappedNavalCode);

/// <summary>The original formation table and action encoding are interpreted at
/// this boundary. Current formation knowledge never falls back to deployment selection.</summary>
public static class ArmyConfigurationSourceAdapter
{
    private const int MaximumFormationCode = 20;
    public static ArmyFormationState DecodeFormations(int? selectedLand, byte? land, byte? naval) =>
        new(selectedLand, DecodeFormation(land), DecodeFormation(naval));
    private static int? DecodeFormation(byte? code) => code <= MaximumFormationCode ? code : null;
    public static ArmyOrdinaryFieldAttackPolicy DecodePolicy(byte? action) => new(action is byte value ? value != 16 : null);
    public static bool HasConfiguration(UnitState unit) => unit.FieldAttackPolicy is not null;
    public static byte? ReadCurrentLand(GameState state, UnitState unit) => EncodeFormation(
        unit.Formations?.CurrentLandFormationId, Find(state, unit)?.UnmappedLandCode);
    public static byte? ReadNaval(GameState state, UnitState unit) => EncodeFormation(
        unit.Formations?.NavalFormationId, Find(state, unit)?.UnmappedNavalCode);
    public static byte? ReadAction(GameState state, UnitState? unit) => unit is not null &&
        Find(state, unit)?.ActionCode is byte action && unit.FieldAttackPolicy?.UsesConfusionGate == (action != 16) ? action : null;
    internal static byte? ReadCategory(GameState state, UnitState unit) => Find(state, unit)?.CategoryCode;
    private static byte? EncodeFormation(int? id, byte? unmapped) => id is int value
        ? value is >= 0 and <= MaximumFormationCode ? (byte)value : null : unmapped;
    private static ArmyConfigurationSourceKnowledge? Find(GameState state, UnitState unit) =>
        HasConfiguration(unit) ? (state.ResourceState as CampaignSourceKnowledge)?.FindArmyConfiguration(unit.Id) : null;
    internal static ICampaignResourceState? Write(ICampaignResourceState? state, int unitId,
        byte? land, byte? naval, byte? action, byte? category)
    {
        var row = new ArmyConfigurationSourceKnowledge(new(unitId), action, category,
            land > MaximumFormationCode ? land : null, naval > MaximumFormationCode ? naval : null);
        if (state is null && Empty(row)) return null;
        return CampaignSourceKnowledge.From(state).WithArmyConfiguration(row);
    }
    internal static ICampaignResourceState WriteAction(ICampaignResourceState? state, int unitId, byte? action)
    {
        var knowledge = CampaignSourceKnowledge.From(state);
        var row = knowledge.FindArmyConfiguration(unitId) ?? new(new(unitId), null, null, null, null);
        return knowledge.WithArmyConfiguration(row with { ActionCode = action });
    }
    private static bool Empty(ArmyConfigurationSourceKnowledge row) => row.ActionCode is null && row.CategoryCode is null &&
        row.UnmappedLandCode is null && row.UnmappedNavalCode is null;
    internal static ICampaignResourceState? Migrate(ICampaignResourceState? state, JsonObject unit, int? defeatedForceId)
    {
        if (unit.ContainsKey("Formations") || unit.ContainsKey("FieldAttackPolicy") || !unit.ContainsKey("FormationId") ||
            unit["Id"] is not JsonValue idValue || !idValue.TryGetValue<int>(out int id) || id < 0)
            throw new InvalidDataException("Historical army formation ownership is invalid.");
        int? selected = unit["FormationId"]?.GetValue<int>();
        JsonObject? configuration = unit["NativeArmyConfiguration"] as JsonObject;
        if (unit["NativeArmyConfiguration"] is not null && configuration is null)
            throw new InvalidDataException("Historical army configuration is invalid.");
        byte? land = null, naval = null, action = null, category = null;
        if (configuration is not null)
        {
            land = Read(configuration, "Formation1B"); naval = Read(configuration, "Formation1C");
            action = Read(configuration, "Action37"); category = Read(configuration, "Category40");
            if (configuration.Count != 4 || unit["NativeArmyOrigin"] is not null && land is byte known && known != selected)
                throw new InvalidDataException("Historical source-created army configuration conflicts with its selection.");
        }
        unit["Formations"] = selected is null && configuration is null ? null : new JsonObject {
            ["SelectedLandFormationId"] = selected, ["CurrentLandFormationId"] = DecodeFormation(land), ["NavalFormationId"] = DecodeFormation(naval) };
        unit["FieldAttackPolicy"] = configuration is null ? null : new JsonObject { ["UsesConfusionGate"] = action is byte knownAction ? knownAction != 16 : (bool?)null };
        unit.Remove("FormationId"); unit.Remove("NativeArmyConfiguration");
        var row = new ArmyConfigurationSourceKnowledge(new(id, defeatedForceId), action, category,
            land > MaximumFormationCode ? land : null, naval > MaximumFormationCode ? naval : null);
        if (Empty(row)) return state;
        var knowledge = CampaignSourceKnowledge.From(state);
        if (knowledge.ArmyConfigurations.Any(item => item.Owner == row.Owner))
            throw new InvalidDataException("Historical army configuration compatibility is duplicated.");
        return knowledge.WithArmyConfiguration(row);
    }
    private static byte? Read(JsonObject row, string key)
    {
        if (!row.ContainsKey(key)) throw new InvalidDataException("Army configuration channel is missing.");
        return row[key] is null ? null : row[key] is JsonValue value && value.TryGetValue<byte>(out byte code) ? code :
            throw new InvalidDataException("Army configuration channel is outside its encoding.");
    }
    internal static JsonArray EncodeKnowledge(IEnumerable<ArmyConfigurationSourceKnowledge> rows)
    {
        var result = new JsonArray();
        foreach (var row in rows) result.Add(new JsonObject {
            ["UnitId"] = row.Owner.UnitId, ["DefeatedForceId"] = row.Owner.DefeatedForceId,
            ["ActionCode"] = row.ActionCode, ["CategoryCode"] = row.CategoryCode,
            ["UnmappedLandCode"] = row.UnmappedLandCode, ["UnmappedNavalCode"] = row.UnmappedNavalCode });
        return result;
    }
    internal static IReadOnlyList<ArmyConfigurationSourceKnowledge> DecodeKnowledge(JsonArray rows)
    {
        var result = new List<ArmyConfigurationSourceKnowledge>();
        foreach (var node in rows)
        {
            if (node is not JsonObject row || row.Count != 6 || row["UnitId"] is not JsonValue idValue ||
                !idValue.TryGetValue<int>(out int id) || !row.ContainsKey("DefeatedForceId") ||
                row["DefeatedForceId"] is not null && (row["DefeatedForceId"] is not JsonValue force || !force.TryGetValue<int>(out _)))
                throw new InvalidDataException("Saved army configuration compatibility is invalid.");
            var owner = new ArmyConfigurationSourceOwner(id, row["DefeatedForceId"]?.GetValue<int>());
            var value = new ArmyConfigurationSourceKnowledge(owner, Read(row, "ActionCode"), Read(row, "CategoryCode"),
                Read(row, "UnmappedLandCode"), Read(row, "UnmappedNavalCode"));
            if (id < 0 || owner.DefeatedForceId is < 0 || Empty(value) ||
                value.UnmappedLandCode is <= MaximumFormationCode || value.UnmappedNavalCode is <= MaximumFormationCode ||
                result.Any(item => item.Owner == owner))
                throw new InvalidDataException("Saved army configuration compatibility owner or encoding is invalid.");
            result.Add(value);
        }
        return result;
    }
    private static Dictionary<ArmyConfigurationSourceOwner, UnitState> Images(GameState state)
    {
        var result = state.Units.Values.Concat(state.ArmyLifecycle?.Receivers.Entries
            .Where(row => row.PendingImage is not null).Select(row => row.PendingImage!) ?? [])
            .ToDictionary(unit => new ArmyConfigurationSourceOwner(unit.Id));
        foreach (var defeat in state.ForceDefeats)
            foreach (var unit in defeat.Value.WithdrawnUnits) result.Add(new(unit.Id, defeat.Key), unit);
        return result;
    }
    internal static GameState PruneReplacedChannels(GameState state)
    {
        if (state.ResourceState is not CampaignSourceKnowledge knowledge || knowledge.ArmyConfigurations.Count == 0) return state;
        var images = Images(state); var rows = new List<ArmyConfigurationSourceKnowledge>();
        foreach (var row in knowledge.ArmyConfigurations)
        {
            if (!images.TryGetValue(row.Owner, out var unit) || unit.FieldAttackPolicy is not { } policy) continue;
            var kept = row with {
                ActionCode = row.ActionCode is byte action && policy.UsesConfusionGate == (action != 16) ? action : null,
                UnmappedLandCode = unit.Formations?.CurrentLandFormationId is null ? row.UnmappedLandCode : null,
                UnmappedNavalCode = unit.Formations?.NavalFormationId is null ? row.UnmappedNavalCode : null };
            if (!Empty(kept)) rows.Add(kept);
        }
        return state with { ResourceState = knowledge.ReplaceArmyConfigurations(rows) };
    }
    internal static void Validate(GameState state)
    {
        var images = Images(state);
        foreach (var row in (state.ResourceState as CampaignSourceKnowledge)?.ArmyConfigurations ?? [])
            if (row.Owner.UnitId < 0 || row.Owner.DefeatedForceId is < 0 || Empty(row) ||
                row.UnmappedLandCode is <= MaximumFormationCode || row.UnmappedNavalCode is <= MaximumFormationCode ||
                !images.TryGetValue(row.Owner, out var unit) || unit.FieldAttackPolicy is not { } policy ||
                row.ActionCode is byte action && policy.UsesConfusionGate != (action != 16) ||
                row.UnmappedLandCode is not null && unit.Formations?.CurrentLandFormationId is not null ||
                row.UnmappedNavalCode is not null && unit.Formations?.NavalFormationId is not null)
                throw new InvalidDataException("Saved army configuration compatibility conflicts with its image.");
        foreach (var pair in images)
        {
            var unit = pair.Value;
            var source = (state.ResourceState as CampaignSourceKnowledge)?.ArmyConfigurations.FirstOrDefault(row => row.Owner == pair.Key);
            if (unit.NativeArmyOrigin is not null && EncodeFormation(unit.Formations?.CurrentLandFormationId, source?.UnmappedLandCode) is byte land && land != unit.FormationId)
                throw new InvalidDataException("Source-created army formation differs from its deployment selection.");
        }
    }
}

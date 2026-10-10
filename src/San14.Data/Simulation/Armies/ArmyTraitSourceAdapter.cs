using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using San14.Core;

namespace San14.Data;

public readonly record struct ArmyTraitSourceOwner(int UnitId, int? DefeatedForceId = null);

/// <summary>Only unclassified original trait references remain in this immutable
/// image-specific compatibility channel. Known trait IDs belong to the roster.</summary>
public sealed record ArmyTraitSourceKnowledge
{
    public ArmyTraitSourceOwner Owner { get; }
    public IReadOnlyDictionary<int, ushort> UnmappedTraits { get; }
    public ArmyTraitSourceKnowledge(ArmyTraitSourceOwner owner, IEnumerable<KeyValuePair<int, ushort>> unmappedTraits)
    {
        var values = unmappedTraits.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (owner.UnitId < 0 || owner.DefeatedForceId is < 0 || values.Any(pair => pair.Key < 0 || pair.Value <= ArmyTraitSourceAdapter.MaximumTraitCode))
            throw new InvalidDataException("Army trait compatibility is outside its source format.");
        Owner = owner; UnmappedTraits = new ReadOnlyDictionary<int, ushort>(values);
    }
}

internal static class ArmyTraitSourceAdapter
{
    private const int SourceCapacity = 36;
    internal const int MaximumTraitCode = 355;
    private static ArmyTraitRoster DecodeValues(IReadOnlyList<ushort> traits) =>
        new(traits.Select(value => value <= MaximumTraitCode ? (int?)value : null).ToArray());
    internal static ArmyTraitRoster Decode(IReadOnlyList<ushort> traits)
    {
        if (traits.Count != SourceCapacity) throw new InvalidDataException("Original army trait count is invalid.");
        return DecodeValues(traits);
    }
    internal static ICampaignResourceState? Write(ICampaignResourceState? state, int unitId, IReadOnlyList<ushort>? traits)
    {
        var values = traits?.Select((value, index) => new KeyValuePair<int, ushort>(index, value))
            .Where(pair => pair.Value > MaximumTraitCode).ToArray() ?? [];
        if (state is null && values.Length == 0) return null;
        var knowledge = CampaignSourceKnowledge.From(state);
        return values.Length == 0 ? knowledge.WithoutArmyTraits(unitId) : knowledge.WithArmyTraits(new(new(unitId), values));
    }
    internal static ushort[]? Encode(GameState state, UnitState unit)
    {
        if (unit.TraitRoster is not { TraitIds.Count: SourceCapacity } roster) return null;
        var row = (state.ResourceState as CampaignSourceKnowledge)?.FindArmyTraits(unit.Id);
        var result = new ushort[SourceCapacity];
        for (int i = 0; i < result.Length; i++)
        {
            if (roster.TraitIds[i] is int id)
            {
                if (id is < 0 or > MaximumTraitCode) return null;
                result[i] = (ushort)id;
            }
            else if (row is not null && row.UnmappedTraits.TryGetValue(i, out ushort code)) result[i] = code;
            else return null;
        }
        return result;
    }
    internal static ICampaignResourceState? Migrate(ICampaignResourceState? state, JsonObject unit, int? defeatedForceId)
    {
        if (unit["Id"] is not JsonValue idNode || !idNode.TryGetValue<int>(out int id) || id < 0 ||
            unit["NativeArmyConfiguration"] is not JsonObject configuration || !configuration.ContainsKey("TraitSlots64"))
            throw new InvalidDataException("Historical army traits are incomplete.");
        var owner = new ArmyTraitSourceOwner(id, defeatedForceId);
        var knowledge = state as CampaignSourceKnowledge;
        if (knowledge?.ArmyTraits.Any(row => row.Owner == owner) == true)
            throw new InvalidDataException("Historical army trait ownership is duplicated.");
        ushort[]? values = configuration["TraitSlots64"] is null ? null : configuration["TraitSlots64"] is JsonArray words
            ? words.Select(node => node is JsonValue value && value.TryGetValue<ushort>(out ushort word) ? word :
                throw new InvalidDataException("Historical army trait reference is outside its encoding.")).ToArray()
            : throw new InvalidDataException("Historical army trait roster is invalid.");
        if (values is not null && unit["NativeArmyOrigin"] is not null && values.Length != SourceCapacity)
            throw new InvalidDataException("Historical source-created army trait count is invalid.");
        unit["TraitRoster"] = values is null ? null : JsonSerializer.SerializeToNode(DecodeValues(values));
        configuration.Remove("TraitSlots64");
        var residue = values?.Select((value, index) => new KeyValuePair<int, ushort>(index, value))
            .Where(pair => pair.Value > MaximumTraitCode).ToArray() ?? [];
        return residue.Length == 0 ? state : CampaignSourceKnowledge.From(state).WithArmyTraits(new(owner, residue));
    }
    internal static JsonArray EncodeKnowledge(IEnumerable<ArmyTraitSourceKnowledge> rows)
    {
        var result = new JsonArray();
        foreach (var row in rows)
        {
            var unknown = new JsonObject();
            foreach (var pair in row.UnmappedTraits) unknown[pair.Key.ToString(CultureInfo.InvariantCulture)] = pair.Value;
            result.Add(new JsonObject { ["UnitId"] = row.Owner.UnitId, ["DefeatedForceId"] = row.Owner.DefeatedForceId, ["UnmappedTraits"] = unknown });
        }
        return result;
    }
    internal static IReadOnlyList<ArmyTraitSourceKnowledge> DecodeKnowledge(JsonArray rows)
    {
        var result = new List<ArmyTraitSourceKnowledge>();
        foreach (var node in rows)
        {
            if (node is not JsonObject row || row.Count != 3 || row["UnitId"] is not JsonValue idNode ||
                !idNode.TryGetValue<int>(out int id) || !row.ContainsKey("DefeatedForceId") ||
                row["DefeatedForceId"] is not null && (row["DefeatedForceId"] is not JsonValue force || !force.TryGetValue<int>(out _)) ||
                row["UnmappedTraits"] is not JsonObject unknown || unknown.Count == 0)
                throw new InvalidDataException("Saved army trait compatibility is invalid.");
            var owner = new ArmyTraitSourceOwner(id, row["DefeatedForceId"]?.GetValue<int>());
            if (result.Any(item => item.Owner == owner)) throw new InvalidDataException("Saved army trait compatibility is duplicated.");
            var values = new Dictionary<int, ushort>();
            foreach (var pair in unknown)
                if (!int.TryParse(pair.Key, out int index) || index < 0 || pair.Key != index.ToString(CultureInfo.InvariantCulture) ||
                    pair.Value is not JsonValue value || !value.TryGetValue<ushort>(out ushort word) || !values.TryAdd(index, word))
                    throw new InvalidDataException("Saved army trait compatibility reference is invalid.");
            result.Add(new(owner, values));
        }
        return result;
    }
    private static Dictionary<ArmyTraitSourceOwner, UnitState> Images(GameState state)
    {
        var result = state.Units.Values.Concat(state.ArmyLifecycle?.Receivers.Entries
            .Where(row => row.PendingImage is not null).Select(row => row.PendingImage!) ?? [])
            .ToDictionary(unit => new ArmyTraitSourceOwner(unit.Id));
        foreach (var defeat in state.ForceDefeats)
            foreach (var unit in defeat.Value.WithdrawnUnits) result.Add(new(unit.Id, defeat.Key), unit);
        return result;
    }
    internal static GameState PruneReplacedChannels(GameState state)
    {
        if (state.ResourceState is not CampaignSourceKnowledge knowledge || knowledge.ArmyTraits.Count == 0) return state;
        var images = Images(state); var rows = new List<ArmyTraitSourceKnowledge>();
        foreach (var row in knowledge.ArmyTraits)
        {
            if (!images.TryGetValue(row.Owner, out var unit) || unit.TraitRoster is not { } roster) continue;
            var kept = row.UnmappedTraits.Where(pair => pair.Key < roster.TraitIds.Count && roster.TraitIds[pair.Key] is null).ToArray();
            if (kept.Length > 0) rows.Add(kept.Length == row.UnmappedTraits.Count ? row : new(row.Owner, kept));
        }
        return state with { ResourceState = knowledge.ReplaceArmyTraits(rows) };
    }
    internal static void Validate(GameState state)
    {
        var images = Images(state);
        foreach (var row in (state.ResourceState as CampaignSourceKnowledge)?.ArmyTraits ?? [])
        {
            if (row.UnmappedTraits.Count == 0 || !images.TryGetValue(row.Owner, out var unit) || unit.TraitRoster is not { } roster ||
                row.UnmappedTraits.Any(pair => pair.Key >= roster.TraitIds.Count || roster.TraitIds[pair.Key] is not null))
                throw new InvalidDataException("Saved army trait compatibility conflicts with its image.");
        }
    }
}

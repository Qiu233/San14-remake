using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using San14.Core;

namespace San14.Data;

/// <summary>Unmapped source knowledge belongs to the resource adapter. It never
/// supplies a game location or a district identity by interpreting unknown codes.</summary>
public sealed record OfficerLocationSourceResidue
{
    public int OfficerId { get; }
    public int? LocationCode { get; }
    public int? DistrictCode { get; }
    public OfficerLocationSourceResidue(int officerId, int? locationCode, int? districtCode)
    {
        if (officerId < 0 || locationCode is < 0 or > ushort.MaxValue || districtCode is < 0 or > byte.MaxValue)
            throw new InvalidDataException("Officer source residue is outside its resource format.");
        OfficerId = officerId; LocationCode = locationCode; DistrictCode = districtCode;
    }
}

/// <summary>Source encodings without an independent simulation meaning yet.</summary>
public sealed record OfficerAftermathSourceResidue
{
    private readonly int? _unmappedTarget, _previousForce, _captureReset;
    public int OfficerId { get; }
    public int? UnmappedResentmentTarget
    {
        get => _unmappedTarget;
        init => _unmappedTarget = InRange(value, ushort.MaxValue);
    }
    public int? PreviousForceCode
    {
        get => _previousForce;
        init => _previousForce = InRange(value, byte.MaxValue);
    }
    public int? CaptureResetCode
    {
        get => _captureReset;
        init => _captureReset = InRange(value, byte.MaxValue);
    }
    public OfficerAftermathSourceResidue(int officerId, int? unmappedResentmentTarget,
        int? previousForceCode, int? captureResetCode)
    {
        if (officerId < 0) throw new InvalidDataException("Officer source residue identity is invalid.");
        OfficerId = officerId;
        UnmappedResentmentTarget = unmappedResentmentTarget;
        PreviousForceCode = previousForceCode;
        CaptureResetCode = captureResetCode;
    }
    private static int? InRange(int? value, int maximum) => value is < 0 || value > maximum
        ? throw new InvalidDataException("Officer aftermath residue is outside its resource format.") : value;
}

/// <summary>Immutable resource compatibility knowledge. Location and aftermath
/// channels are independent and survive updates to either channel.</summary>
public sealed class CampaignSourceKnowledge : ICampaignResourceState
{
    private readonly IReadOnlyDictionary<int, OfficerLocationSourceResidue> _officers;
    private readonly IReadOnlyDictionary<int, OfficerAftermathSourceResidue> _aftermath;
    private readonly IReadOnlyDictionary<ArmyReceiverId, ArmyTargetSourceResidue> _armyTargets;
    private readonly IReadOnlyDictionary<ArmyOverrideSourceOwner, ArmyOverrideTargetSourceResidue> _armyOverrides;
    public CampaignSourceKnowledge(IEnumerable<OfficerLocationSourceResidue> officers,
        IEnumerable<OfficerAftermathSourceResidue>? aftermath = null,
        IEnumerable<ArmyTargetSourceResidue>? armyTargets = null,
        IEnumerable<ArmyOverrideTargetSourceResidue>? armyOverrides = null)
    {
        _officers = new ReadOnlyDictionary<int, OfficerLocationSourceResidue>(officers.ToDictionary(item => item.OfficerId));
        _aftermath = new ReadOnlyDictionary<int, OfficerAftermathSourceResidue>(
            (aftermath ?? []).ToDictionary(item => item.OfficerId));
        _armyTargets = new ReadOnlyDictionary<ArmyReceiverId, ArmyTargetSourceResidue>(
            (armyTargets ?? []).ToDictionary(item => item.ReceiverId));
        _armyOverrides = new ReadOnlyDictionary<ArmyOverrideSourceOwner, ArmyOverrideTargetSourceResidue>(
            (armyOverrides ?? []).ToDictionary(item => item.Owner));
    }
    public OfficerLocationSourceResidue? Find(int officerId) => _officers.GetValueOrDefault(officerId);
    public OfficerAftermathSourceResidue? FindAftermath(int officerId) => _aftermath.GetValueOrDefault(officerId);
    public ArmyTargetSourceResidue? FindArmyTarget(ArmyReceiverId receiverId) => _armyTargets.GetValueOrDefault(receiverId);
    public IReadOnlyCollection<ArmyTargetSourceResidue> ArmyTargets => _armyTargets.Values.ToArray();
    public ArmyOverrideTargetSourceResidue? FindArmyOverride(ArmyOverrideSourceOwner owner) => _armyOverrides.GetValueOrDefault(owner);
    public IReadOnlyCollection<ArmyOverrideTargetSourceResidue> ArmyOverrides => _armyOverrides.Values.ToArray();
    public CampaignSourceKnowledge ReplaceArmyOverrides(IEnumerable<ArmyOverrideTargetSourceResidue> rows) =>
        new(_officers.Values, _aftermath.Values, _armyTargets.Values, rows);
    public CampaignSourceKnowledge WithArmyOverride(ArmyOverrideTargetSourceResidue row) => ReplaceArmyOverrides(
        _armyOverrides.Values.Where(item => item.Owner != row.Owner).Append(row)
            .Where(item => item.KindCode is not null || item.IdentityCode is not null));
    public CampaignSourceKnowledge WithArmyTarget(ArmyTargetSourceResidue residue) => new(
        _officers.Values, _aftermath.Values, _armyTargets.Values.Where(item => item.ReceiverId != residue.ReceiverId)
            .Append(residue).Where(item => item.KindCode is not null || item.IdentityCode is not null), _armyOverrides.Values);
    public CampaignSourceKnowledge ReplaceArmyTargets(IEnumerable<ArmyTargetSourceResidue> rows) => new(
        _officers.Values, _aftermath.Values, rows, _armyOverrides.Values);
    internal static CampaignSourceKnowledge From(ICampaignResourceState? state) => state switch
    {
        null => new([]),
        CampaignSourceKnowledge locations => locations,
        _ => throw new InvalidDataException("Unsupported campaign resource compatibility owner.")
    };
    public IReadOnlyCollection<OfficerLocationSourceResidue> Officers => _officers.Values.ToArray();
    public IReadOnlyCollection<OfficerAftermathSourceResidue> Aftermath => _aftermath.Values.ToArray();
    public CampaignSourceKnowledge With(OfficerLocationSourceResidue residue) => new(
        _officers.Values.Where(item => item.OfficerId != residue.OfficerId).Append(residue)
            .Where(item => item.LocationCode is not null || item.DistrictCode is not null), _aftermath.Values, _armyTargets.Values, _armyOverrides.Values);
    public CampaignSourceKnowledge WithAftermath(OfficerAftermathSourceResidue residue) => new(
        _officers.Values, _aftermath.Values.Where(item => item.OfficerId != residue.OfficerId).Append(residue)
            .Where(item => item.UnmappedResentmentTarget is not null || item.PreviousForceCode is not null || item.CaptureResetCode is not null), _armyTargets.Values, _armyOverrides.Values);
    public CampaignSourceKnowledge WithoutAftermath(int officerId) => new(
        _officers.Values, _aftermath.Values.Where(item => item.OfficerId != officerId), _armyTargets.Values, _armyOverrides.Values);
}

internal sealed class CampaignResourceStateCodec : ICampaignResourceStateCodec
{
    public JsonObject Encode(ICampaignResourceState state)
    {
        if (state is not CampaignSourceKnowledge locations) throw new InvalidDataException("Unsupported campaign resource state.");
        var rows = new JsonArray();
        foreach (var row in locations.Officers)
            rows.Add(new JsonObject { ["OfficerId"] = row.OfficerId, ["LocationCode"] = row.LocationCode, ["DistrictCode"] = row.DistrictCode });
        var aftermath = new JsonArray();
        foreach (var row in locations.Aftermath)
            aftermath.Add(new JsonObject { ["OfficerId"] = row.OfficerId,
                ["UnmappedResentmentTarget"] = row.UnmappedResentmentTarget,
                ["PreviousForceCode"] = row.PreviousForceCode, ["CaptureResetCode"] = row.CaptureResetCode });
        var targets = new JsonArray();
        foreach (var row in locations.ArmyTargets)
            targets.Add(new JsonObject { ["ReceiverId"] = row.ReceiverId.Value,
                ["KindCode"] = row.KindCode, ["IdentityCode"] = row.IdentityCode });
        var overrides = new JsonArray();
        foreach (var row in locations.ArmyOverrides)
            overrides.Add(new JsonObject { ["ReceiverId"] = row.Owner.ReceiverId?.Value, ["UnitId"] = row.Owner.UnitId,
                ["KindCode"] = row.KindCode, ["IdentityCode"] = row.IdentityCode });
        return new() { ["Kind"] = "campaign-source-knowledge", ["Version"] = 4,
            ["Locations"] = rows, ["Aftermath"] = aftermath, ["ArmyTargets"] = targets, ["ArmyOverrides"] = overrides };
    }
    public ICampaignResourceState Decode(JsonObject document)
    {
        if (document["Kind"] is not JsonValue kindValue || !kindValue.TryGetValue<string>(out string? kind) ||
            document["Version"] is not JsonValue versionValue || !versionValue.TryGetValue<int>(out int version))
            throw new InvalidDataException("Campaign resource state identity is invalid.");
        bool legacy = kind == "officer-location-source-residues" && version == 1;
        if ((!legacy && (kind != "campaign-source-knowledge" || version is not (2 or 3 or 4))) ||
            document[legacy ? "Officers" : "Locations"] is not JsonArray rows ||
            (legacy && (document.ContainsKey("Locations") || document.ContainsKey("Aftermath"))) ||
            (!legacy && (document["Aftermath"] is not JsonArray || document.ContainsKey("Officers"))) ||
            (version >= 3 && document["ArmyTargets"] is not JsonArray) ||
            (version < 3 && document.ContainsKey("ArmyTargets")) ||
            (version == 4 && document["ArmyOverrides"] is not JsonArray) ||
            (version < 4 && document.ContainsKey("ArmyOverrides")))
            throw new InvalidDataException("Unknown or incomplete campaign resource state.");
        var values = new List<OfficerLocationSourceResidue>();
        foreach (var node in rows)
        {
            if (node is not JsonObject row || !row.ContainsKey("LocationCode") || !row.ContainsKey("DistrictCode") ||
                row["OfficerId"] is not JsonValue id || !id.TryGetValue<int>(out int officerId) || officerId < 0 ||
                values.Any(item => item.OfficerId == officerId)) throw new InvalidDataException("Saved officer source residue is invalid.");
            values.Add(new(officerId, Read(row["LocationCode"], ushort.MaxValue), Read(row["DistrictCode"], byte.MaxValue)));
        }
        var aftermath = new List<OfficerAftermathSourceResidue>();
        if (!legacy)
            foreach (var node in (JsonArray)document["Aftermath"]!)
            {
                if (node is not JsonObject row || !row.ContainsKey("UnmappedResentmentTarget") ||
                    !row.ContainsKey("PreviousForceCode") || !row.ContainsKey("CaptureResetCode") ||
                    row["OfficerId"] is not JsonValue id || !id.TryGetValue<int>(out int officerId) || officerId < 0 ||
                    aftermath.Any(item => item.OfficerId == officerId))
                    throw new InvalidDataException("Saved officer aftermath residue is invalid.");
                aftermath.Add(new(officerId, Read(row["UnmappedResentmentTarget"], ushort.MaxValue),
                    Read(row["PreviousForceCode"], byte.MaxValue), Read(row["CaptureResetCode"], byte.MaxValue)));
            }
        var targets = new List<ArmyTargetSourceResidue>();
        if (version >= 3)
            foreach (var node in (JsonArray)document["ArmyTargets"]!)
            {
                if (node is not JsonObject row || !row.ContainsKey("KindCode") || !row.ContainsKey("IdentityCode") ||
                    row["ReceiverId"] is not JsonValue id || !id.TryGetValue<int>(out int receiverId) || receiverId <= 0 ||
                    targets.Any(item => item.ReceiverId.Value == receiverId))
                    throw new InvalidDataException("Saved army target source residue is invalid.");
                int? targetKind = Read(row["KindCode"], byte.MaxValue), identity = Read(row["IdentityCode"], ushort.MaxValue);
                targets.Add(new(new(receiverId), targetKind is int k ? (byte)k : null, identity is int i ? (ushort)i : null));
            }
        var overrides = new List<ArmyOverrideTargetSourceResidue>();
        if (version == 4)
            foreach (var node in (JsonArray)document["ArmyOverrides"]!)
            {
                if (node is not JsonObject row || !row.ContainsKey("ReceiverId") || !row.ContainsKey("UnitId") ||
                    !row.ContainsKey("KindCode") || !row.ContainsKey("IdentityCode"))
                    throw new InvalidDataException("Saved override source knowledge is incomplete.");
                int? receiver = Read(row["ReceiverId"], int.MaxValue), unit = Read(row["UnitId"], int.MaxValue);
                if (receiver == 0 || receiver is null && unit is null)
                    throw new InvalidDataException("Saved override source owner is invalid.");
                ArmyOverrideSourceOwner owner = new(receiver is int r ? new(r) : null, unit);
                if (overrides.Any(item => item.Owner == owner)) throw new InvalidDataException("Saved override source owner is duplicated.");
                int? category = Read(row["KindCode"], byte.MaxValue), identity = Read(row["IdentityCode"], ushort.MaxValue);
                overrides.Add(new(owner, category is int k ? (byte)k : null, identity is int i ? (ushort)i : null));
            }
        return new CampaignSourceKnowledge(values, aftermath, targets, overrides);
    }
    public ICampaignResourceState? MigrateLocations(ICampaignResourceState? state, IReadOnlyList<LegacyOfficerLocationKnowledge> locations)
    {
        var result = state as CampaignSourceKnowledge ?? new CampaignSourceKnowledge([]);
        if (state is not null && state is not CampaignSourceKnowledge) throw new InvalidDataException("Unsupported legacy resource state.");
        foreach (var item in locations)
        {
            if (result.Find(item.OfficerId) is not null) throw new InvalidDataException("Conflicting historical officer resource state.");
            result = result.With(new(item.OfficerId, item.SecondLocationCode, item.DistrictCode));
        }
        return result;
    }
    public ICampaignResourceState? MigrateAftermath(ICampaignResourceState? state, IReadOnlyList<LegacyOfficerAftermathKnowledge> aftermath)
    {
        var result = CampaignSourceKnowledge.From(state);
        foreach (var item in aftermath)
        {
            if (result.FindAftermath(item.OfficerId) is not null)
                throw new InvalidDataException("Conflicting historical officer aftermath resource state.");
            result = result.WithAftermath(new(item.OfficerId, item.UnmappedResentmentTarget,
                item.PreviousForceCode, item.CaptureResetCode));
        }
        return result;
    }
    public ICampaignResourceState? MigrateArmyTargets(ICampaignResourceState? state, IReadOnlyList<LegacyArmyTargetKnowledge> targets)
    {
        var result = CampaignSourceKnowledge.From(state);
        foreach (var item in targets)
        {
            if (result.FindArmyTarget(item.ReceiverId) is not null)
                throw new InvalidDataException("Conflicting historical army target source knowledge.");
            result = result.WithArmyTarget(new(item.ReceiverId,
                item.KindCode is int kind ? checked((byte)kind) : null,
                item.IdentityCode is int identity ? checked((ushort)identity) : null));
        }
        return result;
    }
    public ICampaignResourceState? MigrateArmyOverrides(ICampaignResourceState? state, IReadOnlyList<LegacyArmyOverrideKnowledge> targets)
    {
        var result = CampaignSourceKnowledge.From(state);
        foreach (var item in targets)
        {
            ArmyOverrideSourceOwner owner = new(item.ReceiverId, item.UnitId);
            if (result.FindArmyOverride(owner) is not null) throw new InvalidDataException("Conflicting historical override source knowledge.");
            result = result.WithArmyOverride(new(owner, item.KindCode is int kind ? checked((byte)kind) : null,
                item.IdentityCode is int identity ? checked((ushort)identity) : null));
        }
        return result;
    }
    private static int? Read(JsonNode? node, int maximum)
    {
        if (node is null) return null;
        if (node is not JsonValue value || !value.TryGetValue<int>(out int result) || result < 0 || result > maximum)
            throw new InvalidDataException("Saved officer source residue is outside its original format.");
        return result;
    }
}

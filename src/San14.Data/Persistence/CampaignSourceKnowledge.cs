using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Text.Json;
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
public sealed class CampaignSourceKnowledge : ICampaignResourceState, IArmyIdentityReferences, IArmyHistoryResourceState
{
    private readonly IReadOnlyDictionary<int, CityDistrictSourceResidue> _cityDistricts;
    private readonly IReadOnlyDictionary<int, OfficerLocationSourceResidue> _officers;
    private readonly IReadOnlyDictionary<int, OfficerAftermathSourceResidue> _aftermath;
    private readonly IReadOnlyDictionary<ArmyReceiverId, ArmyTargetSourceResidue> _armyTargets;
    private readonly IReadOnlyDictionary<ArmyOverrideSourceOwner, ArmyOverrideTargetSourceResidue> _armyOverrides;
    private readonly IReadOnlyDictionary<int, ArmyPositionSourceResidue> _armyPositions;
    private readonly ReadOnlyDictionary<TransportRouteSourceOwner, TransportRouteSourceKnowledge> _transportRoutes;
    private readonly ReadOnlyDictionary<ArmyTraitSourceOwner, ArmyTraitSourceKnowledge> _armyTraits;
    private readonly ReadOnlyDictionary<ArmyConfigurationSourceOwner, ArmyConfigurationSourceKnowledge> _armyConfigurations;
    public IReadOnlyCollection<ArmyConfigurationSourceKnowledge> ArmyConfigurations => _armyConfigurations.Values;
    public ArmyConfigurationSourceKnowledge? FindArmyConfiguration(int unitId) => _armyConfigurations.GetValueOrDefault(new(unitId));
    public CampaignSourceKnowledge ReplaceArmyConfigurations(IEnumerable<ArmyConfigurationSourceKnowledge> rows) =>
        new(_officers.Values, _aftermath.Values, _armyTargets.Values, _armyOverrides.Values, _armyPositions.Values,
            ArmyLifecycleSource, _transportRoutes.Values, _armyTraits.Values, rows, MapObjectSource, _cityDistricts.Values);
    public CampaignSourceKnowledge WithArmyConfiguration(ArmyConfigurationSourceKnowledge row) => ReplaceArmyConfigurations(
        _armyConfigurations.Values.Where(item => item.Owner != row.Owner).Concat(
            row.ActionCode is null && row.CategoryCode is null && row.UnmappedLandCode is null && row.UnmappedNavalCode is null ? [] : new[] { row }));
    public CampaignSourceKnowledge WithoutArmyConfiguration(int unitId) =>
        ReplaceArmyConfigurations(_armyConfigurations.Values.Where(row => row.Owner != new ArmyConfigurationSourceOwner(unitId)));
    public IReadOnlyCollection<ArmyTraitSourceKnowledge> ArmyTraits => _armyTraits.Values;
    public ArmyTraitSourceKnowledge? FindArmyTraits(int unitId) => _armyTraits.GetValueOrDefault(new(unitId));
    public CampaignSourceKnowledge ReplaceArmyTraits(IEnumerable<ArmyTraitSourceKnowledge> rows) =>
        new(_officers.Values, _aftermath.Values, _armyTargets.Values, _armyOverrides.Values, _armyPositions.Values, ArmyLifecycleSource, _transportRoutes.Values, rows, _armyConfigurations.Values, MapObjectSource, _cityDistricts.Values);
    public CampaignSourceKnowledge WithArmyTraits(ArmyTraitSourceKnowledge row) => ReplaceArmyTraits(
        _armyTraits.Values.Where(item => item.Owner != row.Owner).Concat(row.UnmappedTraits.Count == 0 ? [] : new[] { row }));
    public CampaignSourceKnowledge WithoutArmyTraits(int unitId) => _armyTraits.ContainsKey(new(unitId))
        ? ReplaceArmyTraits(_armyTraits.Values.Where(row => row.Owner != new ArmyTraitSourceOwner(unitId))) : this;
    public ArmyLifecycleSourceState? ArmyLifecycleSource { get; }
    public MapObjectSourceState? MapObjectSource { get; }
    public IReadOnlyCollection<TransportRouteSourceKnowledge> TransportRoutes => _transportRoutes.Values;
    public TransportRouteSourceKnowledge? FindTransportRoute(int unitId) => _transportRoutes.GetValueOrDefault(new(unitId));
    public CampaignSourceKnowledge ReplaceTransportRoutes(IEnumerable<TransportRouteSourceKnowledge> rows) =>
        new(_officers.Values, _aftermath.Values, _armyTargets.Values, _armyOverrides.Values, _armyPositions.Values, ArmyLifecycleSource, rows, _armyTraits.Values, _armyConfigurations.Values, MapObjectSource, _cityDistricts.Values);
    public CampaignSourceKnowledge WithTransportRoute(TransportRouteSourceKnowledge row) =>
        ReplaceTransportRoutes(_transportRoutes.Values.Where(item => item.Owner != row.Owner).Append(row));

    public CampaignSourceKnowledge(IEnumerable<OfficerLocationSourceResidue> officers,
        IEnumerable<OfficerAftermathSourceResidue>? aftermath = null,
        IEnumerable<ArmyTargetSourceResidue>? armyTargets = null,
        IEnumerable<ArmyOverrideTargetSourceResidue>? armyOverrides = null,
        IEnumerable<ArmyPositionSourceResidue>? armyPositions = null,
        ArmyLifecycleSourceState? armyLifecycleSource = null,
        IEnumerable<TransportRouteSourceKnowledge>? transportRoutes = null,
        IEnumerable<ArmyTraitSourceKnowledge>? armyTraits = null,
        IEnumerable<ArmyConfigurationSourceKnowledge>? armyConfigurations = null,
        MapObjectSourceState? mapObjectSource = null,
        IEnumerable<CityDistrictSourceResidue>? cityDistricts = null)
    {
        _officers = new ReadOnlyDictionary<int, OfficerLocationSourceResidue>(officers.ToDictionary(item => item.OfficerId));
        _aftermath = new ReadOnlyDictionary<int, OfficerAftermathSourceResidue>(
            (aftermath ?? []).ToDictionary(item => item.OfficerId));
        _armyTargets = new ReadOnlyDictionary<ArmyReceiverId, ArmyTargetSourceResidue>(
            (armyTargets ?? []).ToDictionary(item => item.ReceiverId));
        _armyOverrides = new ReadOnlyDictionary<ArmyOverrideSourceOwner, ArmyOverrideTargetSourceResidue>(
            (armyOverrides ?? []).ToDictionary(item => item.Owner));
        _armyPositions = new ReadOnlyDictionary<int, ArmyPositionSourceResidue>((armyPositions ?? []).ToDictionary(item => item.UnitId));
        ArmyLifecycleSource = armyLifecycleSource;
        MapObjectSource = mapObjectSource;
        _cityDistricts = new ReadOnlyDictionary<int, CityDistrictSourceResidue>(
            (cityDistricts ?? []).ToDictionary(item => item.CityId));
        _transportRoutes = new ReadOnlyDictionary<TransportRouteSourceOwner, TransportRouteSourceKnowledge>((transportRoutes ?? []).ToDictionary(item => item.Owner));
        _armyTraits = new ReadOnlyDictionary<ArmyTraitSourceOwner, ArmyTraitSourceKnowledge>((armyTraits ?? []).ToDictionary(item => item.Owner));
        _armyConfigurations = new ReadOnlyDictionary<ArmyConfigurationSourceOwner, ArmyConfigurationSourceKnowledge>((armyConfigurations ?? []).ToDictionary(item => item.Owner));
    }
    public IReadOnlyCollection<CityDistrictSourceResidue> CityDistricts => _cityDistricts.Values.ToArray();
    public CityDistrictSourceResidue? FindCityDistrict(int cityId) => _cityDistricts.GetValueOrDefault(cityId);
    public CampaignSourceKnowledge ReplaceCityDistricts(IEnumerable<CityDistrictSourceResidue> rows) =>
        new(_officers.Values, _aftermath.Values, _armyTargets.Values, _armyOverrides.Values, _armyPositions.Values,
            ArmyLifecycleSource, _transportRoutes.Values, _armyTraits.Values, _armyConfigurations.Values, MapObjectSource, rows);
    public OfficerLocationSourceResidue? Find(int officerId) => _officers.GetValueOrDefault(officerId);
    public OfficerAftermathSourceResidue? FindAftermath(int officerId) => _aftermath.GetValueOrDefault(officerId);
    public ArmyTargetSourceResidue? FindArmyTarget(ArmyReceiverId receiverId) => _armyTargets.GetValueOrDefault(receiverId);
    public IReadOnlyCollection<ArmyTargetSourceResidue> ArmyTargets => _armyTargets.Values.ToArray();
    public ArmyOverrideTargetSourceResidue? FindArmyOverride(ArmyOverrideSourceOwner owner) => _armyOverrides.GetValueOrDefault(owner);
    public IReadOnlyCollection<ArmyOverrideTargetSourceResidue> ArmyOverrides => _armyOverrides.Values.ToArray();
    public CampaignSourceKnowledge ReplaceArmyOverrides(IEnumerable<ArmyOverrideTargetSourceResidue> rows) =>
        new(_officers.Values, _aftermath.Values, _armyTargets.Values, rows, _armyPositions.Values, ArmyLifecycleSource, _transportRoutes.Values, _armyTraits.Values, _armyConfigurations.Values, MapObjectSource, _cityDistricts.Values);
    public CampaignSourceKnowledge WithArmyOverride(ArmyOverrideTargetSourceResidue row) => ReplaceArmyOverrides(
        _armyOverrides.Values.Where(item => item.Owner != row.Owner).Append(row)
            .Where(item => item.KindCode is not null || item.IdentityCode is not null));
    public CampaignSourceKnowledge WithArmyTarget(ArmyTargetSourceResidue residue) => new(
        _officers.Values, _aftermath.Values, _armyTargets.Values.Where(item => item.ReceiverId != residue.ReceiverId)
            .Append(residue).Where(item => item.KindCode is not null || item.IdentityCode is not null || item.MapObjectLookupAlias is not null), _armyOverrides.Values, _armyPositions.Values, ArmyLifecycleSource, _transportRoutes.Values, _armyTraits.Values, _armyConfigurations.Values, MapObjectSource, _cityDistricts.Values);
    public CampaignSourceKnowledge ReplaceArmyTargets(IEnumerable<ArmyTargetSourceResidue> rows) => new(
        _officers.Values, _aftermath.Values, rows, _armyOverrides.Values, _armyPositions.Values, ArmyLifecycleSource, _transportRoutes.Values, _armyTraits.Values, _armyConfigurations.Values, MapObjectSource, _cityDistricts.Values);
    public ArmyPositionSourceResidue? FindArmyPosition(int unitId) => _armyPositions.GetValueOrDefault(unitId);
    public IReadOnlyCollection<ArmyPositionSourceResidue> ArmyPositions => _armyPositions.Values.ToArray();
    IEnumerable<int> IArmyIdentityReferences.ReferencedArmyIds => _armyConfigurations.Keys.Select(owner => owner.UnitId).Concat(_armyTraits.Keys.Select(owner => owner.UnitId)).Concat(_transportRoutes.Keys.Select(owner => owner.UnitId)).Concat(_armyPositions.Keys).Concat(
        _armyOverrides.Keys.Where(owner => owner.UnitId.HasValue).Select(owner => owner.UnitId!.Value));
    public CampaignSourceKnowledge ReplaceArmyPositions(IEnumerable<ArmyPositionSourceResidue> rows) =>
        new(_officers.Values, _aftermath.Values, _armyTargets.Values, _armyOverrides.Values, rows, ArmyLifecycleSource, _transportRoutes.Values, _armyTraits.Values, _armyConfigurations.Values, MapObjectSource, _cityDistricts.Values);
    public CampaignSourceKnowledge WithArmyPosition(ArmyPositionSourceResidue row) =>
        ReplaceArmyPositions(_armyPositions.Values.Where(item => item.UnitId != row.UnitId).Append(row));
    public CampaignSourceKnowledge WithoutArmyPosition(int unitId) =>
        ReplaceArmyPositions(_armyPositions.Values.Where(item => item.UnitId != unitId));
    public CampaignSourceKnowledge WithArmyLifecycleSource(ArmyLifecycleSourceState? source) =>
        new(_officers.Values, _aftermath.Values, _armyTargets.Values, _armyOverrides.Values, _armyPositions.Values, source, _transportRoutes.Values, _armyTraits.Values, _armyConfigurations.Values, MapObjectSource, _cityDistricts.Values);
    public CampaignSourceKnowledge WithMapObjectSource(MapObjectSourceState? source) =>
        new(_officers.Values, _aftermath.Values, _armyTargets.Values, _armyOverrides.Values, _armyPositions.Values,
            ArmyLifecycleSource, _transportRoutes.Values, _armyTraits.Values, _armyConfigurations.Values, source, _cityDistricts.Values);
    ICampaignResourceState IArmyHistoryResourceState.RecordWithdrawnArmies(ICampaignResourceState? current, int defeatedForceId, IEnumerable<int> unitIds)
    {
        var ids = unitIds.ToHashSet();
        var archived = _transportRoutes.Values.Where(row => row.Owner.DefeatedForceId is null && ids.Contains(row.Owner.UnitId))
            .Select(row => new TransportRouteSourceKnowledge(new(row.Owner.UnitId, defeatedForceId), row.Choice41, row.UnmappedWaypoints));
        var target = From(current);
        var archivedTraits = _armyTraits.Values.Where(row => row.Owner.DefeatedForceId is null && ids.Contains(row.Owner.UnitId))
            .Select(row => new ArmyTraitSourceKnowledge(new(row.Owner.UnitId, defeatedForceId), row.UnmappedTraits));
        var archivedConfigurations = _armyConfigurations.Values.Where(row => row.Owner.DefeatedForceId is null && ids.Contains(row.Owner.UnitId))
            .Select(row => row with { Owner = new(row.Owner.UnitId, defeatedForceId) });
        return target.ReplaceTransportRoutes(target._transportRoutes.Values.Concat(archived))
            .ReplaceArmyTraits(target._armyTraits.Values.Concat(archivedTraits))
            .ReplaceArmyConfigurations(target._armyConfigurations.Values.Concat(archivedConfigurations));
    }
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
            .Where(item => item.LocationCode is not null || item.DistrictCode is not null), _aftermath.Values, _armyTargets.Values, _armyOverrides.Values, _armyPositions.Values, ArmyLifecycleSource, _transportRoutes.Values, _armyTraits.Values, _armyConfigurations.Values, MapObjectSource, _cityDistricts.Values);
    public CampaignSourceKnowledge WithAftermath(OfficerAftermathSourceResidue residue) => new(
        _officers.Values, _aftermath.Values.Where(item => item.OfficerId != residue.OfficerId).Append(residue)
            .Where(item => item.UnmappedResentmentTarget is not null || item.PreviousForceCode is not null || item.CaptureResetCode is not null), _armyTargets.Values, _armyOverrides.Values, _armyPositions.Values, ArmyLifecycleSource, _transportRoutes.Values, _armyTraits.Values, _armyConfigurations.Values, MapObjectSource, _cityDistricts.Values);
    public CampaignSourceKnowledge WithoutAftermath(int officerId) => new(
        _officers.Values, _aftermath.Values.Where(item => item.OfficerId != officerId), _armyTargets.Values, _armyOverrides.Values, _armyPositions.Values, ArmyLifecycleSource, _transportRoutes.Values, _armyTraits.Values, _armyConfigurations.Values, MapObjectSource, _cityDistricts.Values);
}

internal sealed class CampaignResourceStateCodec : ICampaignResourceStateCodec
{
    internal int? DecodedVersion { get; private set; }

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
                ["KindCode"] = row.KindCode, ["IdentityCode"] = row.IdentityCode,
                ["MapObjectLookupAlias"] = row.MapObjectLookupAlias });
        var overrides = new JsonArray();
        foreach (var row in locations.ArmyOverrides)
            overrides.Add(new JsonObject { ["ReceiverId"] = row.Owner.ReceiverId?.Value, ["UnitId"] = row.Owner.UnitId,
                ["KindCode"] = row.KindCode, ["IdentityCode"] = row.IdentityCode });
        var cityDistricts = new JsonArray();
        foreach (var row in locations.CityDistricts.OrderBy(row => row.CityId))
            cityDistricts.Add(new JsonObject { ["CityId"] = row.CityId, ["DistrictCode"] = (int)row.DistrictCode });
        return new() { ["Kind"] = "campaign-source-knowledge", ["Version"] = 11,
            ["CityDistricts"] = cityDistricts,
            ["Locations"] = rows, ["Aftermath"] = aftermath, ["ArmyTargets"] = targets, ["ArmyOverrides"] = overrides, ["ArmyPositions"] = ArmyPositionSourceKnowledge.Encode(locations.ArmyPositions),
            ["ArmyLifecycleSource"] = locations.ArmyLifecycleSource is null ? null : JsonSerializer.SerializeToNode(locations.ArmyLifecycleSource),
            ["MapObjectSource"] = locations.MapObjectSource is null ? null : JsonSerializer.SerializeToNode(locations.MapObjectSource),
            ["TransportRoutes"] = TransportRouteSourceAdapter.EncodeKnowledge(locations.TransportRoutes),
            ["ArmyTraits"] = ArmyTraitSourceAdapter.EncodeKnowledge(locations.ArmyTraits),
            ["ArmyConfigurations"] = ArmyConfigurationSourceAdapter.EncodeKnowledge(locations.ArmyConfigurations) };
    }
    public ICampaignResourceState Decode(JsonObject document)
    {
        if (document["Kind"] is not JsonValue kindValue || !kindValue.TryGetValue<string>(out string? kind) ||
            document["Version"] is not JsonValue versionValue || !versionValue.TryGetValue<int>(out int version))
            throw new InvalidDataException("Campaign resource state identity is invalid.");
        bool legacy = kind == "officer-location-source-residues" && version == 1;
        if ((!legacy && (kind != "campaign-source-knowledge" || version is not (2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11))) ||
            document[legacy ? "Officers" : "Locations"] is not JsonArray rows ||
            (legacy && (document.ContainsKey("Locations") || document.ContainsKey("Aftermath"))) ||
            (!legacy && (document["Aftermath"] is not JsonArray || document.ContainsKey("Officers"))) ||
            (version >= 3 && document["ArmyTargets"] is not JsonArray) ||
            (version < 3 && document.ContainsKey("ArmyTargets")) ||
            (version >= 4 && document["ArmyOverrides"] is not JsonArray) ||
            (version < 4 && document.ContainsKey("ArmyOverrides")) ||
            (version >= 5 && document["ArmyPositions"] is not JsonArray) ||
            (version < 5 && document.ContainsKey("ArmyPositions")) ||
            (version >= 6 && !document.ContainsKey("ArmyLifecycleSource")) ||
            (version < 6 && document.ContainsKey("ArmyLifecycleSource")) ||
            (version >= 7 && document["TransportRoutes"] is not JsonArray) ||
            (version < 7 && document.ContainsKey("TransportRoutes")) ||
            (version >= 8 && document["ArmyTraits"] is not JsonArray) ||
            (version < 8 && document.ContainsKey("ArmyTraits")) ||
            (version >= 9 && document["ArmyConfigurations"] is not JsonArray) ||
            (version < 9 && document.ContainsKey("ArmyConfigurations")) ||
            (version >= 10 && !document.ContainsKey("MapObjectSource")) ||
            (version < 10 && document.ContainsKey("MapObjectSource")) ||
            (version >= 11 && document["CityDistricts"] is not JsonArray) ||
            (version < 11 && document.ContainsKey("CityDistricts")))
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
                    (version >= 10 ? !row.ContainsKey("MapObjectLookupAlias") : row.ContainsKey("MapObjectLookupAlias")) ||
                    row["ReceiverId"] is not JsonValue id || !id.TryGetValue<int>(out int receiverId) || receiverId <= 0 ||
                    targets.Any(item => item.ReceiverId.Value == receiverId))
                    throw new InvalidDataException("Saved army target source residue is invalid.");
                int? targetKind = Read(row["KindCode"], byte.MaxValue), identity = Read(row["IdentityCode"], ushort.MaxValue);
                int? alias = version >= 10 ? Read(row["MapObjectLookupAlias"], ushort.MaxValue) : null;
                targets.Add(new ArmyTargetSourceResidue(new(receiverId), targetKind is int k ? (byte)k : null,
                    identity is int i ? (ushort)i : null) { MapObjectLookupAlias = alias is int a ? (ushort)a : null });
            }
        var overrides = new List<ArmyOverrideTargetSourceResidue>();
        if (version >= 4)
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
        var cityDistricts = new List<CityDistrictSourceResidue>();
        if (version >= 11)
            foreach (var node in (JsonArray)document["CityDistricts"]!)
            {
                if (node is not JsonObject row || row.Count != 2 ||
                    row["CityId"] is not JsonValue id || !id.TryGetValue<int>(out int cityId) ||
                    row["DistrictCode"] is not JsonValue code || !code.TryGetValue<int>(out int district) ||
                    (uint)cityId >= 52 || (uint)district > byte.MaxValue ||
                    cityDistricts.Any(item => item.CityId == cityId))
                    throw new InvalidDataException("Saved City district residue is incomplete or invalid.");
                cityDistricts.Add(new(cityId, (byte)district));
            }
        DecodedVersion = version;
        return new CampaignSourceKnowledge(values, aftermath, targets, overrides,
            version >= 5 ? ArmyPositionSourceKnowledge.Decode((JsonArray)document["ArmyPositions"]!) : [],
            version >= 6 && document["ArmyLifecycleSource"] is { } source ? ArmyLifecycleSourceSerialization.Decode(source) : null,
            version >= 7 ? TransportRouteSourceAdapter.DecodeKnowledge((JsonArray)document["TransportRoutes"]!) : [],
            version >= 8 ? ArmyTraitSourceAdapter.DecodeKnowledge((JsonArray)document["ArmyTraits"]!) : [],
            version >= 9 ? ArmyConfigurationSourceAdapter.DecodeKnowledge((JsonArray)document["ArmyConfigurations"]!) : [],
            version >= 10 && document["MapObjectSource"] is { } mapSource ? DecodeMapObjectSource(mapSource) : null, cityDistricts);
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
    public ICampaignResourceState? MigrateArmyPositions(ICampaignResourceState? state, IReadOnlyList<LegacyArmyPositionKnowledge> positions)
    {
        var result = CampaignSourceKnowledge.From(state);
        foreach (var row in positions)
        {
            if (result.FindArmyPosition(row.UnitId) is not null)
                throw new InvalidDataException("Conflicting historical army position knowledge.");
            result = result.WithArmyPosition(new(row.UnitId, row.DirectPositionUnavailable,
                row.DirectConflictHex is int direct ? checked((ushort)direct) : null,
                row.MarchConflictHex is int march ? checked((ushort)march) : null));
        }
        return result;
    }
    public ICampaignResourceState? MigrateArmyLifecycle(ICampaignResourceState? state, JsonObject source)
    {
        var knowledge = CampaignSourceKnowledge.From(state);
        if (knowledge.ArmyLifecycleSource is not null)
            throw new InvalidDataException("Historical army source ownership is duplicated.");
        return knowledge.WithArmyLifecycleSource(ArmyLifecycleSourceSerialization.Decode(source));
    }
    public ICampaignResourceState? MigrateTransportRoute(ICampaignResourceState? state, JsonObject unit, int? defeatedForceId = null) =>
        TransportRouteSourceAdapter.Migrate(CampaignSourceKnowledge.From(state), unit, defeatedForceId);

    public ICampaignResourceState? MigrateArmyTraits(ICampaignResourceState? state, JsonObject unit, int? defeatedForceId = null) =>
        ArmyTraitSourceAdapter.Migrate(state, unit, defeatedForceId);

    public void MigrateArmyMorale(JsonObject state, JsonObject unit) => ArmyMoraleSourceAdapter.Migrate(state, unit);

    public ICampaignResourceState? MigrateArmyConfiguration(ICampaignResourceState? state, JsonObject unit, int? defeatedForceId = null) =>
        ArmyConfigurationSourceAdapter.Migrate(state, unit, defeatedForceId);

    private static MapObjectSourceState DecodeMapObjectSource(JsonNode node)
    {
        string[] keys = ["PhysicalReceivers", "LookupPhysicalSlots", "UnmappedCoordinates", "UnmappedFactions",
            "UnmappedKinds", "UnplacedFields", "CurrentFields16", "HexObjectLookupAliases"];
        if (node is not JsonObject source || keys.Any(key => !source.ContainsKey(key)) ||
            source["PhysicalReceivers"] is not JsonArray receivers || source["LookupPhysicalSlots"] is not JsonArray)
            throw new InvalidDataException("Saved map object source directory is missing or incomplete.");
        foreach (var receiver in receivers)
            if (receiver is not JsonObject identity || !identity.ContainsKey("Value"))
                throw new InvalidDataException("Saved map object source receiver identity is incomplete.");
        foreach (string name in keys.Skip(2))
            if (source[name] is not JsonObject)
                throw new InvalidDataException("Saved map object source knowledge collection is invalid.");
        foreach (var row in (JsonObject)source["UnplacedFields"]!)
            if (row.Value is not JsonObject fields || new[] { "Kind", "Faction", "Durability" }.Any(key => !fields.ContainsKey(key)))
                throw new InvalidDataException("Saved unplaced map object source fields are incomplete.");
        try
        {
            //The actual source-state constructor owns count, alias, unique
            //receiver and source-range validation. No scenario fills gaps.
            return source.Deserialize<MapObjectSourceState>()
                ?? throw new InvalidDataException("Saved map object source directory is invalid.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Saved map object source knowledge has invalid typed fields.", error);
        }
    }

    private static int? Read(JsonNode? node, int maximum)
    {
        if (node is null) return null;
        if (node is not JsonValue value || !value.TryGetValue<int>(out int result) || result < 0 || result > maximum)
            throw new InvalidDataException("Saved officer source residue is outside its original format.");
        return result;
    }
}

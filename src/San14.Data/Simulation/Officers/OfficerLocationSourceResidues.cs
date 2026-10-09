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

public sealed class OfficerLocationSourceResidues : ICampaignResourceState
{
    private readonly IReadOnlyDictionary<int, OfficerLocationSourceResidue> _officers;
    public OfficerLocationSourceResidues(IEnumerable<OfficerLocationSourceResidue> officers) =>
        _officers = new ReadOnlyDictionary<int, OfficerLocationSourceResidue>(officers.ToDictionary(item => item.OfficerId));
    public OfficerLocationSourceResidue? Find(int officerId) => _officers.GetValueOrDefault(officerId);
    internal static OfficerLocationSourceResidues From(ICampaignResourceState? state) => state switch
    {
        null => new([]),
        OfficerLocationSourceResidues locations => locations,
        _ => throw new InvalidDataException("Unsupported campaign resource compatibility owner.")
    };
    public IReadOnlyCollection<OfficerLocationSourceResidue> Officers => _officers.Values.ToArray();
    public OfficerLocationSourceResidues With(OfficerLocationSourceResidue residue) => new(
        _officers.Values.Where(item => item.OfficerId != residue.OfficerId).Append(residue)
            .Where(item => item.LocationCode is not null || item.DistrictCode is not null));
}

internal sealed class OfficerLocationResourceStateCodec : ICampaignResourceStateCodec
{
    public JsonObject Encode(ICampaignResourceState state)
    {
        if (state is not OfficerLocationSourceResidues locations) throw new InvalidDataException("Unsupported campaign resource state.");
        var rows = new JsonArray();
        foreach (var row in locations.Officers)
            rows.Add(new JsonObject { ["OfficerId"] = row.OfficerId, ["LocationCode"] = row.LocationCode, ["DistrictCode"] = row.DistrictCode });
        return new() { ["Kind"] = "officer-location-source-residues", ["Version"] = 1, ["Officers"] = rows };
    }
    public ICampaignResourceState Decode(JsonObject document)
    {
        if (document["Kind"]?.GetValue<string>() != "officer-location-source-residues" ||
            document["Version"]?.GetValue<int>() != 1 || document["Officers"] is not JsonArray rows)
            throw new InvalidDataException("Unknown or incomplete campaign resource state.");
        var values = new List<OfficerLocationSourceResidue>();
        foreach (var node in rows)
        {
            if (node is not JsonObject row || !row.ContainsKey("LocationCode") || !row.ContainsKey("DistrictCode") ||
                row["OfficerId"] is not JsonValue id || !id.TryGetValue<int>(out int officerId) || officerId < 0 ||
                values.Any(item => item.OfficerId == officerId)) throw new InvalidDataException("Saved officer source residue is invalid.");
            values.Add(new(officerId, Read(row["LocationCode"], ushort.MaxValue), Read(row["DistrictCode"], byte.MaxValue)));
        }
        return new OfficerLocationSourceResidues(values);
    }
    public ICampaignResourceState? MigrateLocations(ICampaignResourceState? state, IReadOnlyList<LegacyOfficerLocationKnowledge> locations)
    {
        var result = state as OfficerLocationSourceResidues ?? new OfficerLocationSourceResidues([]);
        if (state is not null && state is not OfficerLocationSourceResidues) throw new InvalidDataException("Unsupported legacy resource state.");
        foreach (var item in locations)
        {
            if (result.Find(item.OfficerId) is not null) throw new InvalidDataException("Conflicting historical officer resource state.");
            result = result.With(new(item.OfficerId, item.SecondLocationCode, item.DistrictCode));
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

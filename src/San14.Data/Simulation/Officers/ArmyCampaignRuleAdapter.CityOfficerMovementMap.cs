namespace San14.Data;

public sealed record CityOfficerMovementMapPoint(int CityId, int HexId);
public sealed record CityOfficerMovementMapPick(int? CityId, bool? Allowed, string? UnresolvedInput = null);

/// <summary>The movement page retains its current source owner. Pointer reads
/// follow one Area and its representative Hex; they never capture a world.</summary>
public sealed class CityOfficerMovementMapQuery
{
    private readonly Func<int, CityOfficerMovementMapPick> read;
    public IReadOnlyList<CityOfficerMovementMapPoint> Points { get; }
    internal CityOfficerMovementMapQuery(IReadOnlyList<CityOfficerMovementMapPoint> points,
        Func<int, CityOfficerMovementMapPick> read) { Points = points; this.read = read; }
    public CityOfficerMovementMapPick QueryHex(int physicalHex) => read(physicalHex);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private static CityOfficerMovementMapQuery CreateCityOfficerMovementMap(SourceWorldContext current,
        IReadOnlyList<CityOfficerMovementCity> destinations)
    {
        var allowed = destinations.Select(row => row.CityId).ToHashSet();
        var points = destinations.Select(row =>
        {
            var receiver = BaseLookup.Find(row.CityId, current);
            int hex = current.ReadBaseCurrentHexIndex(receiver);
            if ((uint)hex >= 48400) throw Missing("destination_Hex_range");
            return new CityOfficerMovementMapPoint(row.CityId, hex);
        }).ToArray();
        return new(Array.AsReadOnly(points), Read);

        CityOfficerMovementMapPick Read(int physicalHex)
        {
            if ((uint)physicalHex >= 48400) return new(null, false);
            try
            {
                // MapSelect InputKind2 resolves the clicked Hex's Area, then
                // the representative Hex's Gate; only an invalid Gate falls
                // back to Area.City. Membership uses the very same candidates
                // supplied to the ordinary list, without another Force rule.
                var hex = current.ReadHexTableReference(physicalHex) ?? throw Missing("Hex_pointer");
                int areaSlot = Need(current.ReadHexAreaSlot(hex), "Hex_Area");
                var area = current.ReadAreaTableReference(areaSlot <= 500 ? areaSlot : 0) ?? throw Missing("Area_pointer");
                int representative = Need(current.ReadAreaHexIndex(area), "Area_representative_Hex");
                var center = current.ReadHexTableReference(representative < 48400 ? representative : 0)
                    ?? throw Missing("representative_Hex_pointer");
                int link = Need(current.ReadHexObjectLink(center), "representative_object_link");
                var target = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0)
                    ?? throw Missing("Gate_pointer");
                if (!Need(current.ReadBaseNativeValidity(target), "Gate_validity"))
                {
                    int city = Need(current.ReadAreaCitySlot(area), "Area_City");
                    target = current.ReadCityTableReference(city <= 51 ? city : 0) ?? throw Missing("City_pointer");
                }
                if (!Need(current.ReadBaseNativeValidity(target), "Foothold_validity")) return new(null, false);
                int identity = Need(current.ReadBaseObjectLink(target), "Foothold_identity");
                return new(identity, allowed.Contains(identity));
            }
            catch (InvalidDataException error) { return new(null, null, error.Message); }
        }
        static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        static InvalidDataException Missing(string field) => new("city-officer-movement.map." + field);
    }
}

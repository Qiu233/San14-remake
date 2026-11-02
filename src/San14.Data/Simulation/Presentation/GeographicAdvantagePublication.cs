using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record GeographicAdvantageCity(int CityId, string Name, int HexId, int ForceId);
public sealed record GeographicAdvantageProvince(int ProvinceId, string Name, int AdvantageId,
    string AdvantageName, string Description, int RequiredCities,
    IReadOnlyList<GeographicAdvantageCity> Cities, IReadOnlyDictionary<int,int> OwnedCityCounts)
{
    public int? QualifiedForceId {get;init;}
    public int IllustrationIndex {get;init;}
}

/// <summary>One ordinary geographical-mode capture. Color indices are the
/// source province-minus-one values, independent of political ownership.</summary>
public sealed record GeographicAdvantagePublication(IReadOnlyList<GeographicAdvantageProvince> Provinces,
    IReadOnlyList<byte> HexProvinceColorIndices, IReadOnlyList<ushort> HexProvinceIds, int PlayerForceId)
{
    public IReadOnlyDictionary<int,DetailForceAppearance?> Forces {get;init;}=ImmutableDictionary<int,DetailForceAppearance?>.Empty;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<GeographicAdvantagePublication> QueryGeographicAdvantages(GameState state)
    {
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            int N(int? value,string field) => value ?? throw GeoMissing(field);
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state,current) ?? throw GeoMissing("player_reference");
            int playerId = N(player.PhysicalSlot,"player_force");
            var catalog = _growthQueries.Definitions.Catalog;
            var provinces = ImmutableArray.CreateBuilder<GeographicAdvantageProvince>(17);
            var provinceCities = new Dictionary<int,List<GeographicAdvantageCity>>();
            //91A770 uses every registered City's current CE byte. Its map tint
            //is applied through Area35; no Force-color substitution is made.
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw GeoMissing("city_registry"))
            {
                if(entry is not {Kind:RegistryKind.City,PhysicalSlot:int id}) throw GeoMissing("city_payload");
                var city = new BaseReference(BaseKind.City,id);
                if(!(current.ReadBaseNativeValidity(city) ?? throw GeoMissing("city_validity"))) continue;
                int province = N(_growthQueries.ReadCityState(state,id) is byte raw ? raw : null,"city_province");
                if(province is <1 or >17) continue;
                int force = N(current.ReadBaseForceSlot(city),"city_force");
                var owner=state.Cities.GetValueOrDefault(id) ?? throw GeoMissing("city_owner");
                int hex=N((current.FindBaseHex(city) ?? throw GeoMissing("city_hex_reference")).PhysicalSlot,"city_hex");
                if(!provinceCities.TryGetValue(province,out var list)) provinceCities.Add(province,list=[]);
                list.Add(new(id,owner.Name,hex,force));
            }
            for(int id=1;id<=17;id++)
            {
                var reference=current.ReadStaticTableReference(0xDC80,id) ?? throw GeoMissing("province_reference");
                int physical=N(reference.PhysicalSlot,"province_receiver");
                if(!(current.ReadConditionStateValidity(new(physical)) ?? throw GeoMissing("province_validity")))
                    throw GeoMissing("province_invalid");
                int raw=N(_growthQueries.ReadStateAdvantage(state,physical) is byte advantage ? advantage : null,"province_advantage");
                int effect=N((current.ReadStaticTableReference(0x85990,raw<18?raw:0) ?? throw GeoMissing("advantage_reference")).PhysicalSlot,"advantage_receiver");
                var definition=catalog.Advantages[effect];
                string name=S14ForceNameDefinitionReader.ReadLiteralUnits(catalog.States[physical].RawBytes.AsSpan(24,6));
                string title=S14ForceNameDefinitionReader.ReadLiteralUnits(definition.RawBytes.AsSpan(0,14));
                string description=S14ForceNameDefinitionReader.ReadLiteralUnits(definition.RawBytes.AsSpan(14,44));
                int required=N(_growthQueries.ReadAdvantageRequirement(state,effect) is byte count ? count : null,"required_cities");
                var cities=provinceCities.GetValueOrDefault(id) ?? [];
                var counts=cities.GroupBy(city=>city.ForceId).ToImmutableDictionary(group=>group.Key,group=>group.Count());
                int? qualified=null;
                //915520 overwrites the flag for every qualifying registered
                //Force, retaining the last match rather than choosing a player.
                foreach(var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw GeoMissing("force_registry"))
                {
                    if(entry is not {Kind:RegistryKind.Force,PhysicalSlot:int force}) throw GeoMissing("force_payload");
                    if(counts.GetValueOrDefault(force)>=required &&
                        (current.ReadForceNativeValidity(new(force)) ?? throw GeoMissing("force_validity"))) qualified=force;
                }
                provinces.Add(new(id,name,effect,title,description,required,cities.ToImmutableArray(),counts)
                    {QualifiedForceId=qualified,IllustrationIndex=definition.RawBytes[162]-1});
            }
            var areaProvinces=new int[501];
            //91A770 colors only registered, native-valid Areas. Unused
            //receivers do not own a current35 and must not be queried.
            foreach(var entry in current.ReadRegistryReferences(RegistryKind.Area) ?? throw GeoMissing("area_registry"))
            {
                if(entry is not {Kind:RegistryKind.Area,PhysicalSlot:int area}) throw GeoMissing("area_payload");
                if((uint)area>=areaProvinces.Length) throw GeoMissing("area_receiver_range");
                var reference=new AreaReference(area);
                if(!(current.ReadAreaNativeValidity(reference) ?? throw GeoMissing("area_validity"))) continue;
                int rawCity=N(current.ReadAreaCitySlot(reference),"area_city");
                var city=current.ReadCityTableReference(rawCity<52?rawCity:0) ?? throw GeoMissing("area_city_reference");
                int cityPhysical=N(city.PhysicalSlot,"area_city_receiver");
                //The source reads City.CE directly; no City-validity gate or
                //missing-dictionary fallback may replace this field.
                areaProvinces[area]=N(_growthQueries.ReadCityState(state,cityPhysical) is byte province?province:null,"area_city_province");
            }
            var colors=new byte[48400];Array.Fill(colors,(byte)60);
            var groups=new ushort[48400];
            for(int hex=0;hex<48400;hex++)
            {
                int area=N(current.ReadHexAreaSlot(new(hex)) is ushort slot ? slot : null,"hex_area");
                if((uint)area>=areaProvinces.Length) throw GeoMissing("hex_area_range");
                int province=areaProvinces[area];
                if(province is >=1 and <=17){colors[hex]=(byte)(province-1);groups[hex]=(ushort)province;}
            }
            _mapInformationNames??=ArmyFlagPaneNames.Load(_scenario);
            var forces=ReadDetailForces(state,current,_mapInformationNames,
                provinceCities.Values.SelectMany(c=>c).Select(c=>c.ForceId).Append(0).Append(playerId));
            return AreaAdministrationQuery<GeographicAdvantagePublication>.Known(new(provinces.ToImmutable(),
                Array.AsReadOnly(colors),Array.AsReadOnly(groups),playerId){Forces=forces});
        }
        catch(InvalidDataException error)
        {return AreaAdministrationQuery<GeographicAdvantagePublication>.Unknown(error.Message);}
    }

    private static InvalidDataException GeoMissing(string field)=>new("geographic-advantages."+field);
}

using San14.Core;

namespace San14.Data;

public sealed record RegisteredOfficerCityPick(int? CityId,bool? Allowed,string? UnresolvedInput=null);

/// <summary>One actual pre-start map's retained read owner. A pointer query
/// reads its Hex link; it neither scans the map nor reconstructs a world.</summary>
public sealed class RegisteredOfficerCityMapQuery
{
    private readonly Func<int,RegisteredOfficerCityPick> read;
    internal RegisteredOfficerCityMapQuery(Func<int,RegisteredOfficerCityPick> read)=>this.read=read;
    public RegisteredOfficerCityPick QueryHex(int physicalHex)=>read(physicalHex);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public RegisteredOfficerCityMapQuery CreateRegisteredOfficerCityMapQuery(GameState state)
    {
        if(!ArmyOperationContext.IsNewGamePreview(state))
            throw new InvalidOperationException("Registered-officer placement requires the actual pre-start world.");
        var current=_definitions.Capture(state)??throw new InvalidDataException("registered_officers.city_picker.preview_owner");
        return new(Read);

        RegisteredOfficerCityPick Read(int physicalHex)
        {
            if(physicalHex is <0 or >=48400)return new(null,false);
            try
            {
                var hex=current.ReadHexTableReference(physicalHex)??throw Missing("Hex_pointer");
                //611E90: this is Hex+1A, not the Area's associated City.
                ushort link=current.ReadHexObjectLink(hex)??throw Missing("Hex_City_link");
                var city=current.ReadCityTableReference(link<=51?link:0)??throw Missing("City_pointer");
                if(!(current.ReadBaseNativeValidity(city)??throw Missing("City_validity")))return new(null,false);
                int identity=city.PhysicalSlot??throw Missing("City_identity");
                ushort areaLink=current.ReadHexAreaSlot(hex)??throw Missing("Hex_Area_link");
                var area=current.ReadAreaTableReference(areaLink<=500?areaLink:0)??throw Missing("Area_pointer");
                //5F7440 reads the allegiance of the representative Hex. It is
                //independent of City allegiance and caller's eligible set.
                int force=current.ReadAreaCurrentForceSlot(area)??throw Missing("Area_representative_Force");
                return new(identity,force is not(>=46 and <=50));
            }
            catch(InvalidDataException error){return new(null,null,error.Message);}
        }
        static InvalidDataException Missing(string field)=>new("registered_officers.city_picker."+field);
    }
}

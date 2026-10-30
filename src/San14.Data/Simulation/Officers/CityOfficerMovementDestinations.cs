using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //749460 uses the ordinary seven-column foothold group141A386F0.
    //Prepared once for this retained movement page, never on header clicks.
    private sealed class CityOfficerMovementDestinationCurrent(ArmyCampaignRuleAdapter rules,
        GameState state, SourceWorldContext current, int sourceLocation)
    {
        private readonly Lazy<Dictionary<int,int>> people = new(() => CountPeople(current));
        private readonly Dictionary<int,string> districtNames = [];
        private readonly Dictionary<int,PersonReturnPathResult> paths = [];
        internal CityOfficerMovementCity Read(BaseReference receiver)
        {
            int location = N(current.ReadBaseObjectLink(receiver), "destination_Base4E");
            string name = state.Cities.GetValueOrDefault(location)?.Name ?? state.Gates.GetValueOrDefault(location)?.Name
                ?? throw Missing("destination_name12");
            return new(location,name) { PreparedCells = new(() => Array.AsReadOnly(
                CityOfficerMovementCity.Columns.Select(column => Cell(column.Id)).ToArray())) };
            CityOfficerMovementDestinationCell Cell(int column)
            {
                try
                {
                    if(column==0)
                    {
                        int physical=N(receiver.PhysicalSlot,"destination_name_receiver");
                        string key;
                        if(receiver.Kind==BaseKind.City)
                        {
                            var rows=rules._scenario.Snapshot.Cities;
                            if((uint)physical>=(uint)rows.Count || rows[physical].Slot!=physical)throw Missing("City_definition");
                            key=rows[physical].SecondaryText ?? throw Missing("City_secondary_text1A");
                        }
                        else
                        {
                            var rows=rules._scenario.Snapshot.Gates;
                            if((uint)physical>=(uint)rows.Count || rows[physical].Slot!=physical)throw Missing("Gate_definition");
                            key=rows[physical].SecondaryText ?? throw Missing("Gate_secondary_text1A");
                        }
                        return new(column,name,null) { TextKey=key };
                    }
                    if(column==1)
                    {
                        var district=current.ReadBaseDistrictReference(receiver) ?? throw Missing("destination_District_v80");
                        string text="";
                        if(N(current.ReadDistrictNativeValidity(district),"District_validity"))
                        {
                            int physical=N(district.PhysicalSlot,"District_receiver");
                            if(!districtNames.TryGetValue(physical,out text!))
                                districtNames[physical]=text=rules.ReadDistrictInformationLeaderCaption(state,current,physical);
                        }
                        //358970/3469B0:22ED50 actual leader text, not District number or phonetics.
                        return new(column,text,null) { TextKey=text };
                    }
                    int value;
                    if(column==2)value=people.Value.GetValueOrDefault(location);
                    else if(column==3)value=N(current.ReadBaseResource(receiver,BaseResourceKind.Troops),"Base3C");
                    else if(column is 4 or 5)
                    {
                        //Gate.v90/vA0 delegate to the actual linked City.
                        var resourceBase=receiver.Kind==BaseKind.Gate?CurrentGateMapQueries.FindCity(receiver,current):receiver;
                        value=N(current.ReadBaseResource(resourceBase,column==4?BaseResourceKind.Gold:BaseResourceKind.Supplies),"Base_resource");
                    }
                    else
                    {
                        //32ED00 canonicalizes the pair before231670->2318F0.
                        //The same-Force bases use the mature mode8 path and scalar order.
                        if(!paths.TryGetValue(location,out var path))
                            paths[location]=path=PersonReturnPathSource.QueryReassignment(state,current,
                                Math.Min(sourceLocation,location),Math.Max(sourceLocation,location));
                        value=path.Days ?? throw Missing(path.UnresolvedOwner ?? "destination_path_days");
                    }
                    return new(column,value.ToString(CultureInfo.InvariantCulture),null) { NumberKey=value };
                }
                catch(InvalidDataException error) { return new(column,null,error.Message); }
            }
        }
        private static Dictionary<int,int> CountPeople(SourceWorldContext current)
        {
            var counts=new Dictionary<int,int>();
            //2304B0 mode0: raw HomeE2, status1..4, no extra validity/place/Force filter.
            foreach(int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person_registry100"))
            {
                int person=N(entry,"Person_pointer");
                int home=N(current.ReadPersonFirstLocation(person),"PersonE2");
                if(home is <1 or >61)continue;
                int status=N(current.ReadPersonStatus(person),"PersonE6");
                if(status is >=1 and <=4)counts[home]=counts.GetValueOrDefault(home)+1;
            }
            return counts;
        }
        private static T N<T>(T? value,string field) where T:struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field)=>new("city-officer-movement.destination."+field);
    }
}

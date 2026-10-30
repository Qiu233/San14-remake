using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record PersonnelHireCitySelection(CityInformationPageSet Pages, CityOfficerMovementMapQuery Map);

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<PersonnelHireCitySelection> PreparePersonnelHireCitySelection(GameState state,
        int forceId,IReadOnlyList<int> cityIds)
    {
        try
        {
            var source=new CityInformationCurrent(this,state,forceId);
            return AreaAdministrationQuery<PersonnelHireCitySelection>.Known(new(source.PersonnelHirePages(cityIds),source.PersonnelHireMap(cityIds)));
        }
        catch(InvalidDataException error){return AreaAdministrationQuery<PersonnelHireCitySelection>.Unknown(error.Message);}
    }

    private sealed partial class CityInformationCurrent
    {
        internal CityOfficerMovementMapQuery PersonnelHireMap(IReadOnlyList<int> cityIds)
        {
            var current=Current;
            var candidates=cityIds.ToHashSet();
            var points=cityIds.Select(id=>
            {
                var city=current.ReadCityTableReference(id)??throw new InvalidDataException("hire_city.map.City_pointer");
                int hex=current.ReadBaseCurrentHexIndex(city);
                if((uint)hex>=48400)throw new InvalidDataException("hire_city.map.center_Hex_range");
                return new CityOfficerMovementMapPoint(id,hex);
            }).ToArray();
            return new(points,Read);

            CityOfficerMovementMapPick Read(int physicalHex)
            {
                if((uint)physicalHex>=48400)return new(null,false);
                try
                {
                    //454A10 installs InputKind5: clicked Hex -> Area.City.
                    //Unlike movement's kind2, it never substitutes a Gate.
                    var hex=current.ReadHexTableReference(physicalHex)??throw new InvalidDataException("hire_city.map.Hex_pointer");
                    int areaSlot=current.ReadHexAreaSlot(hex)??throw new InvalidDataException("hire_city.map.Hex_Area");
                    var area=current.ReadAreaTableReference(areaSlot<=500?areaSlot:0)??throw new InvalidDataException("hire_city.map.Area_pointer");
                    if(!(current.ReadAreaNativeValidity(area)??throw new InvalidDataException("hire_city.map.Area_validity")))return new(null,false);
                    int citySlot=current.ReadAreaCitySlot(area)??throw new InvalidDataException("hire_city.map.Area_City");
                    var city=current.ReadCityTableReference(citySlot<=51?citySlot:0)??throw new InvalidDataException("hire_city.map.City_pointer");
                    bool valid=current.ReadBaseNativeValidity(city)??throw new InvalidDataException("hire_city.map.City_validity");
                    if(!valid)return new(null,false);
                    int identity=current.ReadBaseObjectLink(city)??throw new InvalidDataException("hire_city.map.City_identity");
                    return new(identity,candidates.Contains(identity));
                }
                catch(InvalidDataException error){return new(null,null,error.Message);}
            }
        }

        internal CityInformationPageSet PersonnelHirePages(IReadOnlyList<int> ids)
        {
            //743E80 uses the CCityData selector bank, not the ordinary
            //information list's numerically different City column bank.
            (int Id,int Width,int Flags,int Caption)[] authored=
                [(0,132,1,0x205),(1,216,1,0x1FE),(9,132,1,0x215),(2,76,0x20000002,0x218),
                 (10,104,0x20000002,0x5EA),(4,104,0x20000002,0x20C),(5,118,0x20000002,0x20D),(3,104,0x20000002,0x20E)];
            var group=new CityInformationGroup(0,0x3CD,Data(1,0x3CD),authored.Select(source=>
                new CityInformationColumn(source.Id,source.Caption,Data(1,source.Caption),source.Width,
                    (source.Flags&2)!=0?5:3,(source.Flags&0x20000000)==0,true,true)).ToImmutableArray());
            var prepared=new Dictionary<(int City,int Column),PreparedCell>();
            PreparedCell Read(int id,int column)
            {
                if(prepared.TryGetValue((id,column),out var cached))return cached;
                if(column==9)
                {
                    var owner=ReadCity(id);
                    string Name()
                    {
                        int physical=Governor(owner);
                        return Need(Current.ReadPersonNativeValidity(physical),"hire_city.Governor_validity")?PersonText(physical):"";
                    }
                    return prepared[(id,column)]=MakeCell(Name,()=>new NameKey(Name()));
                }
                if(column!=1)
                    return prepared[(id,column)]=Cell(id,column switch
                    {0=>0,2=>8,10=>4,4=>5,5=>6,3=>7,_=>throw Missing("hire_city.column")});
                var city=ReadCity(id);
                string Text()
                {
                    var district=District(city);
                    return Need(Current.ReadDistrictNativeValidity(district),"hire_city.District_validity")
                        ?rules.ReadDistrictInformationLeaderCaption(state,Current,Need(district.PhysicalSlot,"hire_city.District_receiver")):"";
                }
                return prepared[(id,column)]=MakeCell(Text,()=>new NameKey(Text()));
            }
            return new(Data(1,0x39A),[group],ids,
                (id,column)=>Read(id,column).Display with{SortUnresolvedOwner=Read(id,column).SortUnresolvedOwner},
                (id,column)=>Read(id,column).SortUnresolvedOwner,
                (left,right,column,ascending)=>(Read(left,column).Key??throw Missing("hire_city.sort_key"))
                    .Before(Read(right,column).Key??throw Missing("hire_city.sort_key"),ascending));
        }
    }
}

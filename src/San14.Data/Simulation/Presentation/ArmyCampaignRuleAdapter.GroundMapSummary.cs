using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal MapSummaryPage QueryGroundMapSummaryCurrent(GameState state,SourceWorldContext current,MapSummaryReceiver receiver)
    {
        bool discovery=receiver.Slot==12;
        string layout=discovery?"03_81_08_tinori_discovery_en":"03_81_08_tinori_offset_01";
        var text=new Dictionary<int,string>();var visibility=new Dictionary<int,bool>();
        var images=new List<MapSummaryImage>();var flags=new List<MapSummaryForceFlag>();
        var presets=new Dictionary<int,int>();var missing=new Dictionary<int,string>();
        var colors=new Dictionary<int,MapCornerColors>();
        var result=new MapSummaryPage(receiver.Slot,receiver.ReceiverId,layout,null,text)
            {Visibility=visibility,Images=images,Flags=flags,TextPresets=presets,Colors=colors,UnresolvedFields=missing};
        try
        {
            int id=receiver.ReceiverId;
            if(id is <0 or >17) return result with {ReceiverValid=false};
            var reference=current.ReadStaticTableReference(0xDC80,id)??throw MapSummaryMissing("province_pointer");
            int physical=MapSummaryNeed(reference.PhysicalSlot,"province_receiver");
            if(!MapSummaryNeed(current.ReadConditionStateValidity(new(physical)),"province_validity"))return result with {ReceiverValid=false};
            result=result with {ReceiverValid=true};
            var catalog=_growthQueries.Definitions.Catalog;
            var cities=new List<(int Physical,string Name,int Force)>();
            foreach(var entry in current.ReadRegistryReferences(RegistryKind.City)??throw MapSummaryMissing("province_City_registry"))
            {
                if(entry is not {Kind:RegistryKind.City,PhysicalSlot:int city})throw MapSummaryMissing("province_City_payload");
                int province=_growthQueries.ReadCityState(state,city) is byte rawProvince?rawProvince:throw MapSummaryMissing("City_provinceCE");
                if(province!=physical)continue;
                var basis=new BaseReference(BaseKind.City,city);
                int force=MapSummaryNeed(current.ReadBaseForceSlot(basis),"province_City_Force");
                string name=state.Cities.GetValueOrDefault(city)?.Name??throw MapSummaryMissing("province_City_name");
                cities.Add((city,name,force));
            }
            _mapInformationNames??=ArmyFlagPaneNames.Load(_scenario);
            var appearances=ReadDetailForces(state,current,_mapInformationNames,cities.Select(c=>c.Force).Append(0));
            if(discovery)
            {
                int raw=_growthQueries.ReadStateAdvantage(state,physical) is byte advantage?advantage:throw MapSummaryMissing("province_advantage");
                int effect=MapSummaryNeed((current.ReadStaticTableReference(0x85990,raw<18?raw:0)??throw MapSummaryMissing("advantage_pointer")).PhysicalSlot,"advantage_receiver");
                var definition=catalog.Advantages[effect];
                text[13]=S14ForceNameDefinitionReader.ReadLiteralUnits(catalog.States[physical].RawBytes.AsSpan(24,6));
                text[6]=S14ForceNameDefinitionReader.ReadLiteralUnits(definition.RawBytes.AsSpan(0,14));
                text[8]=S14ForceNameDefinitionReader.ReadLiteralUnits(definition.RawBytes.AsSpan(14,44));
                text[7]=ReadHistoryDataText(1,0x411);presets[7]=13;
                int image=definition.RawBytes[162]-1;
                images.Add(new(2,$"sti_advantage{image:D3}",$"texture_stl_advantage_{image:D3}"));
                int required=_growthQueries.ReadAdvantageRequirement(state,effect) is byte count?count:throw MapSummaryMissing("advantage_required_cities");
                int qualified=0;
                var counts=cities.GroupBy(c=>c.Force).ToDictionary(g=>g.Key,g=>g.Count());
                foreach(var entry in current.ReadRegistryReferences(RegistryKind.Force)??throw MapSummaryMissing("province_Force_registry"))
                {
                    if(entry is not {Kind:RegistryKind.Force,PhysicalSlot:int force})throw MapSummaryMissing("province_Force_payload");
                    if(counts.GetValueOrDefault(force)>=required && MapSummaryNeed(current.ReadForceNativeValidity(new(force)),"qualified_Force_validity"))qualified=force;
                }
                if(!appearances.TryGetValue(qualified,out var appearance))
                    appearance=ReadDetailForces(state,current,_mapInformationNames,[qualified]).GetValueOrDefault(qualified);
                if(appearance is not null)flags.Add(new(9,appearance));
                else {visibility[9]=false;missing[9]="map-summary.province_qualified_Force_appearance";}
            }
            else
            {
                text[3]=ReadHistoryDataText(1,0x6F3);presets[3]=13;
                for(int index=0;index<5;index++)
                {
                    int block=index*8; visibility[4+block]=index<cities.Count;
                    if(index>=cities.Count)continue;
                    var city=cities[index];text[6+block]=city.Name;presets[6+block]=13;
                    if(appearances.GetValueOrDefault(city.Force) is { } appearance)
                    {
                        if(appearance.DisplayName is string name)text[8+block]=name;
                        else {visibility[8+block]=false;missing[8+block]="map-summary.province_Force_name";}
                        flags.Add(new(9+block,appearance){Neighbor=true});
                    }
                    else {visibility[9+block]=false;missing[9+block]="map-summary.province_City_Force_appearance";}
                }
            }
        }
        catch(InvalidDataException error)
        {
            //Never expose the authored placeholder illustration or flag as current facts.
            if(discovery){visibility[2]=false;visibility[9]=false;foreach(int node in new[]{6,8,13})if(!text.ContainsKey(node))visibility[node]=false;}
            else for(int i=0;i<5;i++)visibility[4+i*8]=false;
            return result with {UnresolvedOwner=error.Message};
        }
        return result;
    }
}

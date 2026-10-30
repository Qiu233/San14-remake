using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>New-game new-Force configuration. This projects the source
/// scenario before ordinary world allocation; it is not the political
/// independence command and creates no campaign task or presentation queue.</summary>
public static partial class RegisteredOfficerNewGameForceProjection
{
    private sealed record Assignment(RegisteredOfficerNewGameForce Choice,int Force,int District,int[] Cities);
    private const int InitialCityGold=5000,InitialCitySupplies=30000,InitialCityTroops=10000;
    private const int InitialGateTroops=2000,InitialMorale=80;

    public static LoadedScenario Apply(LoadedScenario current,RegisteredOfficerNewGameSelection selection)
    {
        ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(selection);
        if(selection.NewForces.IsDefault||selection.NewForces.Length>4)throw Missing("force_choices");
        if(selection.NewForces.Length==0)return current;
        var original=current.InitialScenarioDocument??throw Missing("initial_document");
        if(original.FormatMarker!=0x66||current.Snapshot.Locale is not("TC" or "SC"))throw Missing("CE_profile");
        //Validate the established format boundaries once before mutating our
        //own document. Offsets below are serializer boundaries, not addresses.
        _=S14ScenarioTables.ReadScenarioForces(original);
        _=S14ScenarioTables.ReadScenarioDistricts(original);
        _=S14ScenarioTables.ReadScenarioCities(original);
        _=S14ScenarioTables.ReadScenarioGates(original);
        _=S14ScenarioTables.ReadScenarioAreas(original);
        var objects=S14ScenarioObjectReader.Read(original);
        var document=original with{DecodedPayload=original.DecodedPayload.ToArray()};
        byte[] bytes=document.DecodedPayload;
        var snapshot=current.Snapshot;
        var forces=snapshot.Forces.ToDictionary(value=>value.Slot);
        var districts=snapshot.Districts.ToDictionary(value=>value.Slot);
        var cities=snapshot.Cities.ToDictionary(value=>value.Slot);
        var gates=snapshot.Gates.ToDictionary(value=>value.LocationSlot);
        var areas=snapshot.Areas.ToDictionary(value=>value.Slot);
        var people=snapshot.Characters.Where(value=>value.CharacterId!=0).ToDictionary(value=>value.CharacterId);
        var assignments=new List<Assignment>();var claimed=new HashSet<int>();
        int cityStart=checked(8+original.LeadingRecordCount!.Value*2+0x22);
        int gateStart=cityStart+52*23+8;
        const int forceStart=0x143A,districtStart=0x5716,areaStart=districtStart+52*4+16;
        var baseParameters=BaseParameterReader.ReadDefaults(snapshot.Locale);
        foreach(var choice in selection.NewForces)
        {
            if(choice is null||choice.AdditionalCityIds.IsDefault||choice.ColourId is <50 or >59||
                choice.RankId is <0 or >12||choice.KingdomId is <0 or >100||
                !people.TryGetValue(choice.RulerOfficerId,out var ruler))throw Missing("force_choice");
            if(snapshot.WorldParameters?.RawModeValue!=2&&
                (current.Metadata.StartYear??throw Missing("start_year"))-(ruler.BirthYear??throw Missing("ruler_birth_year"))+1<15)
                throw Missing("ruler_underage");
            int force=Enumerable.Range(1,45).Reverse().FirstOrDefault(id=>forces[id].RulerSlot==0);
            int district=Enumerable.Range(1,51).FirstOrDefault(id=>districts[id].ForceSlot==0);
            if(force==0||district==0)throw Missing("vacant_Force_or_District");
            int[] owned=[choice.CityId,..choice.AdditionalCityIds];
            if(owned.Distinct().Count()!=owned.Length||owned.Any(id=>id is <1 or >51||
                !cities.TryGetValue(id,out var city)||city.DistrictSlot!=0||city.RawValidityFlag69!=0||
                objects.Objects[id].RawField10>=48400||objects.Objects[id].RawField12==0||!claimed.Add(id)))throw Missing("vacant_City");
            var adjacent=AdjacentCities(choice.CityId,cities,gates);
            if(choice.AdditionalCityIds.Any(id=>!adjacent.Contains(id)))throw Missing("capital_adjacent_City");
            int rank=choice.RankId,kingdom=rank==1?choice.KingdomId:0;
            //631170 removes duplicate existing kingdom identities before
            //writing the selected one;0 is the unnamed source value.
            if(kingdom!=0)
                foreach(int other in Enumerable.Range(1,45))
                    if(forces[other].RawKingdomName48==kingdom)
                    {bytes[forceStart+other*329+55]=0;forces[other]=forces[other] with{RawKingdomName48=0};}
            int f=forceStart+force*329;
            Word(bytes,f,choice.RulerOfficerId);bytes[f+54]=(byte)rank;bytes[f+55]=(byte)kingdom;bytes[f+56]=(byte)choice.ColourId;
            int d=districtStart+district*4;
            bytes[d]=(byte)force;bytes[d+1]=1;Word(bytes,d+2,choice.RulerOfficerId);
            forces[force]=forces[force] with{RulerSlot=(ushort)choice.RulerOfficerId,RulerName=ruler.Name,
                RankId=rank,RawKingdomName48=(byte)kingdom,RawReportColor49=(byte)choice.ColourId};
            districts[district]=districts[district] with{ForceSlot=(byte)force,Number=1,
                LeaderSlot=(ushort)choice.RulerOfficerId,LeaderName=ruler.Name};
            assignments.Add(new(choice,force,district,owned));
            foreach(int id in owned)
            {
                var city=cities[id];int at=cityStart+id*23;
                bytes[at+2]=(byte)district;
                U32(bytes,at+3,InitialCityGold);U32(bytes,at+7,InitialCitySupplies);
                int capacity=CityTroopCapacity(city,baseParameters);
                U32(bytes,at+11,Math.Clamp(InitialCityTroops,0,capacity));bytes[at+15]=InitialMorale;
                int max=CityOpeningPostLoadSource.ReadCityCapacity(city.SizeCode,false,0,baseParameters,false,null);
                ObjectOwner(id,force,max);
                cities[id]=city with{DistrictSlot=(byte)district,ForceSlot=(byte)force,Gold=InitialCityGold,
                    Supplies=InitialCitySupplies,Troops=(uint)Math.Clamp(InitialCityTroops,0,capacity),Morale=InitialMorale};
                //61B2B0 adds the representative Area and its actual adjacent
                //Areas belonging to this City. It does not paint every Area.
                int center=city.HexId??throw Missing("City_representative_Hex");
                var main=areas.Values.SingleOrDefault(area=>area.CitySlot==id&&area.HexId==center)
                    ??throw Missing("City_representative_Area");
                foreach(int areaId in main.AdjacentAreaSlots.Append(main.Slot).Distinct())
                {
                    if(!areas.TryGetValue(areaId,out var area)||area.CitySlot!=id)continue;
                    bytes[areaStart+areaId*8+3]=(byte)force;
                    areas[areaId]=area with{ForceSlot=(byte)force};
                    //232D20/22C900 select a Gate only at this acquired
                    //Area's representative Hex, independently of city reach.
                    foreach(var gate in gates.Values.Where(value=>value.LocationSlot is >=52 and <=61&&value.HexId==area.HexId).ToArray())
                    {
                        int g=gateStart+gate.Slot*20;
                        int gateCapacity=BaseParameterReader.BaseTroopCapacity+(baseParameters.RuntimeTroopsExtraInitial??throw Missing("Gate_capacity_extra"));
                        int troops=Math.Clamp(InitialGateTroops,0,gateCapacity);
                        int durable=gate.RawField46??throw Missing("Gate_capacity46");
                        bytes[g+2]=(byte)district;U32(bytes,g+11,troops);bytes[g+15]=InitialMorale;
                        ObjectOwner(gate.LocationSlot,force,Math.Min(durable,9999));
                        gates[gate.LocationSlot]=gate with{DistrictSlot=(byte)district,ForceSlot=(byte)force,
                            Troops=(uint)troops,RawMorale44=InitialMorale};
                    }
                }
            }
            int capital=cityStart+choice.CityId*23;
            Word(bytes,capital,choice.RulerOfficerId);
            cities[choice.CityId]=cities[choice.CityId] with{OfficerSlot=(ushort)choice.RulerOfficerId,OfficerName=ruler.Name};
        }
        //631410/60EF80's ruler identity and placement writes precede normal
        //fresh allocation. Other serving placements keep their actual city.
        var table=S14ScenarioTables.ReadCharacterNames(document,snapshot.Locale);
        int delta=snapshot.Locale=="SC"?56:0;
        foreach(var assignment in assignments)
        {
            var choice=assignment.Choice;
            foreach(var placement in selection.Placements)
            {
                if(!placement.Serving||!assignment.Cities.Contains(placement.CityId)||
                    !people.TryGetValue(placement.OfficerId,out var person)||person.StatusCode!=4)continue;
                int at=table.RecordOffset+person.Slot*table.RecordStride;
                bytes[at+156+delta]=(byte)assignment.District;
            }
            var ruler=people[choice.RulerOfficerId];int row=table.RecordOffset+ruler.Slot*table.RecordStride;
            bytes[row+156+delta]=(byte)assignment.District;Word(bytes,row+157+delta,choice.CityId);
            Word(bytes,row+159+delta,choice.CityId);bytes[row+161+delta]=1;
        }
        //All changed typed source rows are refreshed from the same detached
        //document, so no reader can see a Force-only or Object-only overlay.
        var rawForces=S14ScenarioTables.ReadScenarioForces(document).ToDictionary(row=>row.Slot);
        var rawCities=S14ScenarioTables.ReadScenarioCities(document).ToDictionary(row=>row.Slot);
        var rawGates=S14ScenarioTables.ReadScenarioGates(document).ToDictionary(row=>row.Slot);
        var rawAreas=S14ScenarioTables.ReadScenarioAreas(document).ToDictionary(row=>row.Slot);
        foreach(int id in forces.Keys.ToArray())forces[id]=forces[id] with{RemainingBytesHex=rawForces[id].RemainingBytesHex};
        foreach(int id in cities.Keys.ToArray())cities[id]=cities[id] with{RemainingBytesHex=rawCities[id].RemainingBytesHex};
        foreach(int id in gates.Keys.ToArray())gates[id]=gates[id] with{StateRemainingBytesHex=rawGates[gates[id].Slot].RemainingBytesHex};
        foreach(int id in areas.Keys.ToArray())areas[id]=areas[id] with{StateRemainingBytesHex=rawAreas[id].RemainingBytesHex};
        ApplyGovernors(document,snapshot,cities,gates,forces,districts,assignments.SelectMany(value=>value.Cities).ToHashSet());
        var objectRows=S14ScenarioObjectReader.Read(document);
        var projected=current with{InitialScenarioDocument=document,Snapshot=snapshot with{
            DecodedSha256=objectRows.SourceSha256,Forces=forces.Values.OrderBy(value=>value.Slot).ToArray(),
            Districts=districts.Values.OrderBy(value=>value.Slot).ToArray(),Cities=cities.Values.OrderBy(value=>value.Slot).ToArray(),
            Gates=gates.Values.OrderBy(value=>value.Slot).ToArray(),Areas=areas.Values.OrderBy(value=>value.Slot).ToArray(),
            ObjectSourceRecords=objectRows,WorldParameters=S14ScenarioWorldReader.Read(document,snapshot.Locale)}};
        return RegisteredOfficerScenario.RefreshPersons(projected,document);

        void ObjectOwner(int physical,int force,int durability)
        {
            var obj=objects.Objects[physical];bytes[obj.Offset+3]=(byte)force;
            Word(bytes,obj.Offset+4,Math.Clamp(durability,0,9999));
        }
    }

    private static HashSet<int> AdjacentCities(int capital,IReadOnlyDictionary<int,CityPreviewRecord> cities,
        IReadOnlyDictionary<int,GatePreviewRecord> gates)
    {
        var result=new HashSet<int>();Walk(capital,0);return result;
        void Walk(int location,int intermediate)
        {
            var links=location<=51?cities[location].RawNeighborSlots58:gates[location].RawNeighborSlots58;
            if(links is not{Count:10})throw Missing("ten_Base_edges");
            foreach(int next in links)
                if(next is >=1 and <=51)result.Add(next);
                else if(next is >=52 and <=61&&intermediate<2)Walk(next,intermediate+1);
        }
    }
    private static int CityTroopCapacity(CityPreviewRecord city,BaseParameterCatalog parameters)
    {
        int extra=city.SizeCode switch{2=>BaseParameterReader.MediumCityExtraTroops,3=>BaseParameterReader.LargeCityExtraTroops,
            4=>BaseParameterReader.MetropolisExtraTroops,_=>parameters.RuntimeTroopsExtraInitial??throw Missing("City_capacity_extra")};
        return unchecked(BaseParameterReader.BaseTroopCapacity+extra);
    }
    private static void Word(byte[] bytes,int at,int value)=>BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at,2),checked((ushort)value));
    private static void U32(byte[] bytes,int at,int value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at,4),checked((uint)value));
    private static InvalidDataException Missing(string field)=>new("registered-officer.new-Force."+field);
}

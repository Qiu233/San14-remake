using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapNeighborInterpolation(int Slot, ushort FromHex, ushort ToHex,
    IReadOnlyList<ushort> IntermediateHexes);
public sealed record MapInformationLink(int FirstArea, int SecondArea,
    IReadOnlyList<int> Hexes, byte? CommonForceColor);
public sealed record MapDiplomacyMarker(string Text, string BaseSpriteName);
public sealed record MapDiplomacyInformation(int ForceId, int CapitalHex, string Name,
    OfficerPortraitRoute? Portrait, DetailForceAppearance? Flag, int? RelationLevel,
    int? AllianceMonths, bool? UpperRelationFlag, string? UnresolvedInput)
{
    public bool? IsCurrentPlayerForce { get; init; }
    public string? ForceName { get; init; }
    public IReadOnlyList<MapDiplomacyMarker>? Markers { get; init; }
    public ArmySimpleOutlineSelection? FlagOutline { get; init; }
    public BaseReference? CapitalBase { get; init; }
}
public sealed record MapInformationLayerSnapshot(IReadOnlyList<MapInformationLink> Links,
    IReadOnlyList<MapDiplomacyInformation> Diplomacy, IReadOnlyList<string> UnresolvedInputs)
{
    public int? CurrentPlayerPhysical { get; init; }
}

/// <summary>Normal fixed-data CNeighborInterpData, not executable bytes. The
/// loading boundary reads the50 actual seven-word rows between markers32/33.</summary>
public static class MapNeighborInterpolationReader
{
    public static IReadOnlyList<MapNeighborInterpolation> Read(S14Document document)
    {
        if(document.FormatMarker!=0x66)throw new NotSupportedException("Map neighbor interpolation requires the CE fixed profile.");
        ReadOnlySpan<byte> bytes=document.DecodedPayload;
        const int count=50,stride=14;
        int start=-1;
        for(int at=4;at<=bytes.Length-12-count*stride;at++)
            if(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(at,4))==32&&
                BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(at+4,4))==count&&
                BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(at+8+count*stride,4))==33)
            {
                if(start>=0)throw new InvalidDataException("Neighbor interpolation fixed-data boundary is ambiguous.");
                start=at+8;
            }
        if(start<0)throw new InvalidDataException("Neighbor interpolation fixed-data table is absent.");
        var rows=new MapNeighborInterpolation[count];
        for(int slot=0;slot<count;slot++)
        {
            var raw=bytes.Slice(start+slot*stride,stride);
            var points=new ushort[5];
            for(int i=0;i<5;i++)points[i]=BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(4+i*2,2));
            rows[slot]=new(slot,BinaryPrimitives.ReadUInt16LittleEndian(raw),
                BinaryPrimitives.ReadUInt16LittleEndian(raw[2..]),Array.AsReadOnly(points));
        }
        return Array.AsReadOnly(rows);
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private IReadOnlyList<MapNeighborInterpolation>? _mapNeighborInterpolation;
    private LoadedArmyFlagPaneNames? _mapInformationNames;

    /// <summary>One map/world publication. Drawing only projects these retained
    /// values; it never queries live simulation or resamples application RNG.</summary>
    public MapInformationLayerSnapshot QueryMapInformationLayers(GameState state,int playerForceId)
    {
        var issues=new List<string>();
        var links=new List<MapInformationLink>();
        var forces=new List<MapDiplomacyInformation>();
        var current=CaptureDetailForceInputs(state);
        if(current is null)return new([],[],["map-information.current-owner"]);
        // The detail capture intentionally omits live list owners. Borrow the
        // actual saved District registration order, using the same direct
        // District ID/physical mapping as WorldIndexContext.ToSource. Preserve
        // duplicates and null payloads; do not rebuild this from valid districts.
        if(state.ReadArmyLifecycle()?.Indexes.Registries.TryGetValue(WorldEntityKind.District,out var districtOrder)!=true||districtOrder is null)
            return new([],[],["map-information.District-registry-owner"]);
        current.SetArmyReturnRegisteredDistricts(districtOrder.Select(row=>row.Kind==WorldEntityKind.District
            ?new DistrictReference(row.Id):throw new InvalidDataException("map-information.District-registry-kind")).ToArray());
        ForceUnregisterInputs.BindCurrent(state,current);
        current.CurrentPlayerForce=OwnedCurrentPlayerForce.ReadCurrentReference(state,current);
        try{_mapNeighborInterpolation??=_repository.LoadMapNeighborInterpolation(_scenario.Snapshot.FixedDataPath);}
        catch(Exception error)when(error is InvalidDataException or NotSupportedException){issues.Add(error.Message);}
        _mapInformationNames??=ArmyFlagPaneNames.Load(_scenario);

        //187730: current Area table order, native validity, neighbor sequence,
        //and raw3A ascending avoid drawing the same connection twice.
        for(int lookup=0;lookup<501;lookup++)
        {
            var area=current.ReadAreaTableReference(lookup);
            if(area is null||area.PhysicalSlot is null||current.ReadAreaNativeValidity(area)!=true)continue;
            if(current.ReadAreaHexIndex(area) is not ushort from){issues.Add("map-information.Area3A:"+lookup);continue;}
            for(int i=0;i<8;i++)
            {
                if(current.ReadAreaNeighborSlot(area,i) is not ushort raw){issues.Add("map-information.Area-neighbor:"+lookup);break;}
                var other=current.ReadAreaTableReference(raw<501?raw:0);
                if(other is null||other.PhysicalSlot is null||current.ReadAreaNativeValidity(other)!=true)break;
                if(current.ReadAreaHexIndex(other) is not ushort to){issues.Add("map-information.neighbor-Area3A:"+lookup);continue;}
                if(from>=to)continue;
                var points=new List<int>{from};
                var interpolation=_mapNeighborInterpolation?.FirstOrDefault(row=>row.Slot!=0&&row.FromHex==from&&row.ToHex==to);
                if(interpolation is not null)
                    foreach(ushort point in interpolation.IntermediateHexes)
                    {if(point==0)break;points.Add(point);}
                //An unavailable table cannot establish that this edge has no
                //intermediates. Keep it unknown instead of inventing a shortcut.
                if(_mapNeighborInterpolation is null)continue;
                points.Add(to);
                if(points.Any(hex=>(uint)hex>=48400)){issues.Add("map-information.neighbor-Hex-range");continue;}
                byte? color=null;
                var firstHex=current.ReadHexTableReference(from<48400?from:0);
                var secondHex=current.ReadHexTableReference(to<48400?to:0);
                if(firstHex?.PhysicalSlot is not null&&secondHex?.PhysicalSlot is not null&&
                    current.ReadHexForceSlot(firstHex) is byte own&&own!=0&&current.ReadHexForceSlot(secondHex)==own)
                {
                    var force=current.ReadForceTableReference(own<52?own:0);
                    if(force is not null&&current.ReadForceNativeValidity(force)==true)color=current.ReadForceReportColor(force);
                }
                links.Add(new(area.PhysicalSlot.Value,other.PhysicalSlot.Value,points.AsReadOnly(),color));
            }
        }
        var playerReference=OwnedCurrentPlayerForce.ReadCurrentReference(state,current);
        int? actualPlayer=playerReference is null?null:current.ReadForcePhysicalSlot(playerReference);
        var historyNames=new CampaignHistoryWriter(_repository,_scenario,Seed.Language);
        for(int lookup=1;lookup<52;lookup++)
        {
            var force=current.ReadForceTableReference(lookup);
            if(force?.PhysicalSlot is not int physical||current.ReadForceNativeValidity(force)!=true)continue;
            try
            {
                ushort ruler=current.ReadForceRecord(physical)?.RawRuler10??throw Missing("ruler");
                var person=current.ReadPersonLookupReference(ruler<=6000?ruler:0)??throw Missing("ruler-pointer");
                int p=person.PhysicalSlot??throw Missing("ruler-null");
                if(current.ReadPersonNativeValidity(p)!=true)continue;
                int id=current.ReadPersonIdentity(p)??throw Missing("ruler-identity");
                var home=BaseLookup.Find(current.ReadPersonFirstLocation(p)??throw Missing("capital"),current);
                if(current.ReadBaseNativeValidity(home)!=true)continue;
                ushort rawHex=current.ReadBaseCurrentHexIndex(home);
                int hex=current.ReadHexTableReference(rawHex<48400?rawHex:0)?.PhysicalSlot??throw Missing("capital-Hex");
                OfficerPortraitRoute? portrait=null;DetailForceAppearance? flag=null;
                int? level=null,months=null;bool? upper=null;string? missing=null;
                bool? isPlayer=null;string? forceName=null;
                IReadOnlyList<MapDiplomacyMarker>? markers=null;
                ArmySimpleOutlineSelection? outline=null;
                try
                {
                    ushort image=current.ReadPersonPortraitAppearance(p)??throw Missing("portrait");
                    portrait=ReadOfficerPortraitAppearance(state,current,_definitions,id,image,ArmyMapUiSources.Create());
                    int actual=actualPlayer??throw Missing("current-player-Force");
                    isPlayer=actual!=0&&actual==physical;
                    forceName=historyNames.FallbackName(state,current,force,(person,full)=>
                        full?person.Name:person.CurrentSurname??throw Missing("ruler-surname"));
                    var district=ForcePrimaryDistrictSource.Resolve(current,force);
                    int districtId=district.PhysicalSlot??throw Missing("primary-District");
                    byte flagRaw=current.ReadDistrictForceId(districtId)??throw Missing("primary-District-Force");
                    var flagForce=current.ReadForceTableReference(flagRaw<52?flagRaw:0)??throw Missing("flag-Force");
                    bool flagValid=current.ReadForceNativeValidity(flagForce)??throw Missing("flag-Force-validity");
                    flag=ReadDetailForceAppearance(state,current,_mapInformationNames,flagForce,flagValid,MapSpritePalettes.Create());
                    var selectedOutline=ArmySimpleOutlineSource.ReadConfiguredSceneryMap(state,current,district);
                    outline=selectedOutline.Selection??throw Missing(selectedOutline.Missing??"flag-outline");
                    var relation=ReadDiplomacyRelationCurrent(state,current,actual,physical);
                    level=relation.Level;months=relation.AllianceMonths;upper=relation.UpperFlag;
                    var list=new List<MapDiplomacyMarker>();
                    if(isPlayer==false)
                    {
                        if((uint)relation.Level>=6)throw Missing("relation-level");
                        string[] backgrounds=["000_cmn_mrk_base_red","000_cmn_mrk_base_red","000_cmn_mrk_base_yellow",
                            "000_cmn_mrk_base_green","000_cmn_mrk_base_cyan","000_cmn_mrk_base_cyan"];
                        list.Add(new(ReadHistoryDataText(0,0x141+relation.Level),backgrounds[relation.Level]));
                        // The stop-war relation looks back from target to player.
                        if(ReadDiplomacyRelationCurrent(state,current,physical,actual).AllianceMonths!=0)
                            list.Add(new(ReadHistoryDataText(1,0x289),"000_cmn_mrk_base_cyan"));
                    }
                    int selected=state.NativeArmySimulation?.World.SelectedForce1656??throw Missing("coalition-target");
                    int role=0;
                    if(selected!=0)
                    {
                        if(physical==selected)role=1;
                        else
                        {
                            int other=state.NativeForceUnregisterCurrent?.OtherForce1657??throw Missing("coalition-leader");
                            if(physical==other)role=2;
                            else
                            {
                                byte flags=state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical,out var reset)==true
                                    ?reset[0x12]??throw Missing("reset-Force-flags")
                                    :state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.Flags12??throw Missing("Force-flags");
                                role=(flags&2)!=0?3:0;
                            }
                        }
                    }
                    if(role!=0)list.Add(new(ReadHistoryDataText(0,0x801+role),"000_cmn_mrk_base_purple"));
                    markers=list.AsReadOnly();
                }
                catch(InvalidDataException error){missing=error.Message;}
                forces.Add(new(physical,hex,state.Officers.GetValueOrDefault(id)?.Name??"",portrait,flag,
                    level,months,upper,missing) {IsCurrentPlayerForce=isPlayer,ForceName=forceName,Markers=markers,FlagOutline=outline,CapitalBase=home});
            }
            catch(InvalidDataException error){issues.Add(error.Message+":"+lookup);}
        }
        return new(links.AsReadOnly(),forces.AsReadOnly(),issues.AsReadOnly()) {CurrentPlayerPhysical=actualPlayer};
        static InvalidDataException Missing(string field)=>new("map-information."+field);
    }
}

namespace San14.Data;

public enum MapTargetPresentation { Standard, IntelligenceAwayForce, IntelligenceInspectCity }

public sealed record DiplomacyTerritoryCell(byte ForceSlot,byte ForceColor,ushort CityLink,ushort GateLink,ushort HoverLink);

public sealed record DiplomacyTargetMapPick(int? Id,bool? Allowed,string? UnresolvedInput=null);

/// <summary>MapSelect identity against the diplomacy draft's retained current
/// and the exact same candidate pointers as its ordinary list.</summary>
public sealed class DiplomacyTargetMapQuery
{
    private readonly Func<int,DiplomacyTargetMapPick> read;
    private readonly Func<int,DiplomacyTerritoryCell> territory;
    private DiplomacyTerritoryCell[]? territoryCells;
    // The retained query belongs to one captured diplomacy draft/current.
    // First map entry captures its source arrays; list/map reentry and hover
    // retain them. No resource palette is cached here: the native resource
    // owner continues to supply the currently loaded original MapColor data.
    public DiplomacyTerritoryCell ReadTerritoryCell(int hex)
    {
        if((uint)hex>=48400)throw new ArgumentOutOfRangeException(nameof(hex));
        if(territoryCells is not {} cells)
        {
            cells=new DiplomacyTerritoryCell[48400];
            for(int i=0;i<cells.Length;i++)cells[i]=territory(i);
            territoryCells=cells;
        }
        return cells[hex];
    }
    public DiplomacyTargetSelectionKind Kind { get; }
    public MapTargetPresentation Presentation { get; init; }=MapTargetPresentation.Standard;
    public IReadOnlySet<int>? DisabledIds { get; init; }
    public IReadOnlyDictionary<int,AreaAdministrationQuery<IReadOnlyList<MapDiplomacyMarker>>>? IntelligenceSpyMarkers { get; init; }
    private readonly HashSet<int> candidates;
    public bool Contains(int id)=>candidates.Contains(id);
    internal DiplomacyTargetMapQuery(DiplomacyTargetSelectionKind kind,HashSet<int> candidates,Func<int,DiplomacyTargetMapPick> read,Func<int,DiplomacyTerritoryCell> territory)
    { Kind=kind;this.candidates=candidates;this.read=read;this.territory=territory; }
    public DiplomacyTargetMapPick QueryHex(int physicalHex)=>read(physicalHex);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private static DiplomacyTargetMapQuery CreateDiplomacyTargetMap(SourceWorldContext current,
        DiplomacyTargetSelectionKind kind,IReadOnlyList<int> candidates,
        MapTargetPresentation presentation=MapTargetPresentation.Standard,
        IReadOnlyDictionary<int,AreaAdministrationQuery<IReadOnlyList<MapDiplomacyMarker>>>? intelligenceSpyMarkers=null,
        IReadOnlySet<int>? disabledIds=null)
    {
        var allowed=candidates.ToHashSet();
        return new(kind,allowed,Read,ReadTerritory){Presentation=presentation,IntelligenceSpyMarkers=intelligenceSpyMarkers,DisabledIds=disabledIds};
        DiplomacyTerritoryCell ReadTerritory(int physicalHex)
        {
            if((uint)physicalHex>=48400)throw new ArgumentOutOfRangeException(nameof(physicalHex));
            var hex=current.ReadHexTableReference(physicalHex)??throw Missing("overlay_Hex_pointer");
            if(kind==DiplomacyTargetSelectionKind.Force)
            {
                byte raw=Need(current.ReadHexForceSlot(hex),"overlay_Hex14");
                var force=current.ReadForceTableReference(raw<52?raw:0)??throw Missing("overlay_Force_pointer");
                byte color=Need(current.ReadForceReportColor(force),"overlay_Force49");
                if(color>60)throw Missing("overlay_Force49_range");
                return new(raw,color,0,0,0);
            }
            int rawArea=Need(current.ReadHexAreaSlot(hex),"overlay_Hex12");
            var area=current.ReadAreaTableReference(rawArea<501?rawArea:0)??throw Missing("overlay_Area_pointer");
            int rawCity=Need(current.ReadAreaCitySlot(area),"overlay_Area35");
            var city=current.ReadCityTableReference(rawCity<52?rawCity:0)??throw Missing("overlay_City_pointer");
            ushort cityLink=Need(current.ReadBaseObjectLink(city),"overlay_City4E");
            int representative=Need(current.ReadAreaHexIndex(area),"overlay_Area3A");
            var center=current.ReadHexTableReference(representative<48400?representative:0)??throw Missing("overlay_representative_Hex");
            int link=Need(current.ReadHexObjectLink(center),"overlay_representative_link");
            var gate=current.ReadGateTableReference(link is >=52 and <=61?link-51:0)??throw Missing("overlay_Gate_pointer");
            ushort gateLink=Need(current.ReadBaseObjectLink(gate),"overlay_Gate4E");
            ushort hover=Need(current.ReadBaseNativeValidity(gate),"overlay_Gate_validity")?gateLink:cityLink;
            return new(0,0,cityLink,gateLink,hover);
        }
        DiplomacyTargetMapPick Read(int physicalHex)
        {
            if((uint)physicalHex>=48400)return new(null,false);
            try
            {
                var hex=current.ReadHexTableReference(physicalHex)??throw Missing("Hex_pointer");
                if(kind==DiplomacyTargetSelectionKind.Force)
                {
                    int raw=Need(current.ReadHexForceSlot(hex),"Hex_Force");
                    var force=current.ReadForceTableReference(raw<=51?raw:0)??throw Missing("Force_pointer");
                    if(!Need(current.ReadForceNativeValidity(force),"Force_validity"))return new(null,false);
                    int id=Need(force.PhysicalSlot,"Force_identity");
                    return new(id,allowed.Contains(id)&&disabledIds?.Contains(id)!=true);
                }
                // MapSelect type2: the Area's representative special Foothold
                // takes priority; only its invalidity permits the City fallback.
                int areaSlot=Need(current.ReadHexAreaSlot(hex),"Hex_Area");
                var area=current.ReadAreaTableReference(areaSlot<=500?areaSlot:0)??throw Missing("Area_pointer");
                int representative=Need(current.ReadAreaHexIndex(area),"Area_representative_Hex");
                var center=current.ReadHexTableReference(representative<48400?representative:0)??throw Missing("representative_Hex_pointer");
                int link=Need(current.ReadHexObjectLink(center),"representative_object_link");
                var target=current.ReadGateTableReference(link is >=52 and <=61?link-51:0)??throw Missing("Gate_pointer");
                if(!Need(current.ReadBaseNativeValidity(target),"Gate_validity"))
                {
                    int city=Need(current.ReadAreaCitySlot(area),"Area_City");
                    target=current.ReadCityTableReference(city<=51?city:0)??throw Missing("City_pointer");
                }
                if(!Need(current.ReadBaseNativeValidity(target),"Foothold_validity"))return new(null,false);
                int identity=Need(current.ReadBaseObjectLink(target),"Foothold_identity");
                return new(identity,allowed.Contains(identity));
            }
            catch(InvalidDataException error){return new(null,null,error.Message);}
        }
        static T Need<T>(T? value,string field) where T:struct=>value??throw Missing(field);
        static InvalidDataException Missing(string field)=>new("diplomacy.target_map."+field);
    }
}

using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapNeighborInterpolation(int Slot, ushort FromHex, ushort ToHex,
    IReadOnlyList<ushort> IntermediateHexes);
public sealed record MapInformationLink(int FirstArea, int SecondArea,
    IReadOnlyList<int> Hexes, byte? CommonForceColor);
public sealed record MapDiplomacyInformation(int ForceId, int CapitalHex, string Name,
    OfficerPortraitRoute? Portrait, DetailForceAppearance? Flag, int? RelationLevel,
    int? AllianceMonths, bool? UpperRelationFlag, string? UnresolvedInput);
public sealed record MapInformationLayerSnapshot(IReadOnlyList<MapInformationLink> Links,
    IReadOnlyList<MapDiplomacyInformation> Diplomacy, IReadOnlyList<string> UnresolvedInputs);

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
                try
                {
                    ushort image=current.ReadPersonPortraitAppearance(p)??throw Missing("portrait");
                    portrait=ReadOfficerPortraitAppearance(state,current,_definitions,id,image,ArmyMapUiSources.Create());
                    flag=ReadDetailForceAppearance(state,current,_mapInformationNames,force,true,MapSpritePalettes.Create());
                    var relation=ReadDiplomacyRelationCurrent(state,current,playerForceId,physical);
                    level=relation.Level;months=relation.AllianceMonths;upper=relation.UpperFlag;
                }
                catch(InvalidDataException error){missing=error.Message;}
                forces.Add(new(physical,hex,state.Officers.GetValueOrDefault(id)?.Name??"",portrait,flag,
                    level,months,upper,missing));
            }
            catch(InvalidDataException error){issues.Add(error.Message+":"+lookup);}
        }
        return new(links.AsReadOnly(),forces.AsReadOnly(),issues.AsReadOnly());
        static InvalidDataException Missing(string field)=>new("map-information."+field);
    }
}

using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One ordinary Area page, bound only to the supplied world.</summary>
    public AreaInformationPage QueryAreaInformation(GameState state, int areaId)
        => QueryAreaInformationCore(state, areaId, null);

    internal AreaInformationPage QueryAreaInformationCurrent(GameState state, int areaId,
        SourceWorldContext suppliedCurrent)
        => QueryAreaInformationCore(state, areaId, suppliedCurrent);

    private AreaInformationPage QueryAreaInformationCore(GameState state, int areaId,
        SourceWorldContext? suppliedCurrent)
    {
        var text = new Dictionary<int,string>();
        var missing = new Dictionary<int,string>();
        var styles = new Dictionary<int,int>();
        var alignments = new Dictionary<int,int>();
        var colors = new Dictionary<int,MapCornerColors>();
        var sprites = new Dictionary<int,int>();
        var hidden = new HashSet<int> { 18 }; //902610 finally hides the numeric District sprite unconditionally.
        var neighbors = new List<AreaInformationNeighbor>();
        IReadOnlyList<AreaInformationTrait> traits = [];
        IReadOnlyList<CityInformationGauge> gauges = [];
        string name = state.Areas.GetValueOrDefault(areaId)?.Name ?? "";
        int? forceId = null, appearanceId = null, districtNumber = null, selectionIcon = null;
        AreaMarkerColor? letter = null;
        DetailForceAppearance? flag = null;
        DeploymentNarrationPortraitRoute? standing = null;
        DetailMiniMapValues? miniMap = null;
        bool? nearby = null;
        try
        {
            if (!state.Areas.ContainsKey(areaId) || areaId is < 1 or > 500) throw Missing("Area_identity");
            var current = suppliedCurrent ?? CaptureOwnedArmyReturnWorld(state).Context;
            BaseReturnCaptivesInputs.BindCurrent(state,current);
            var area = current.ReadAreaTableReference(areaId) ?? throw Missing("Area_pointer");
            if (!K(current.ReadAreaNativeValidity(area),"Area_validity")) throw Missing("Area_invalid");
            var palettes = MapSpritePalettes.Create();
            foreach (var caption in AreaInformationSources.Captions)
            { Text(caption.Node,()=>ReadHistoryDataText(1,caption.Message)); styles[caption.Node]=13; }
            foreach(int node in new[]{14,16,19,23,31,32,33,34,39,40,41,42,47,48,49,50,56,60,78,83,88,103,109,111,113,120,121,122,127}) styles[node]=0;
            foreach(int node in new[]{31,39,47}) alignments[node]=0x01;
            Read(4,()=>
            {
                int raw=K(current.ReadAreaCurrentForceSlot(area),"Force_Hex14"); forceId=raw;
                var force=current.ReadForceTableReference(raw<52?raw:0) ?? throw Missing("Force_pointer");
                bool valid=K(current.ReadForceNativeValidity(force),"Force_validity");
                _mapInformationNames ??= ArmyFlagPaneNames.Load(_scenario);
                flag=ReadDetailForceAppearance(state,current,_mapInformationNames,force,valid,palettes) ?? throw Missing("Force_flag");
                byte color=K(current.ReadForceReportColor(force),"Force_color49"); letter=palettes.ForceLetter(color);
            });
            Text(14,()=>
            {
                var force=Force();
                if(!K(current.ReadForceNativeValidity(force),"Force_validity"))return "";
                _mapInformationNames ??=ArmyFlagPaneNames.Load(_scenario);
                return _mapInformationNames.Resolve(current,force).NativeShortName;
            });
            Read(2,()=>
            {
                int raw=K(current.ReadAssignmentArea36(areaId),"Area36");
                var person=(raw<=6000?current.ReadPersonLookupReference(raw):current.ReadTransferPersonTableReference(0)) ?? throw Missing("Overseer_pointer");
                int physical=K(person.PhysicalSlot,"Overseer_physical");
                bool valid=K(current.ReadPersonNativeValidity(physical),"Overseer_validity");
                int identity=valid?K(current.ReadPersonIdentity(physical),"Overseer_identity"):0;
                //3660D0 has an explicit zero selector exit; no fabricated officer is displayed.
                text[16]=raw==0 || !valid?"":state.Officers.GetValueOrDefault(identity)?.Name ?? throw Missing("Overseer_name");
                ushort appearance=valid?K(current.ReadPersonPortraitAppearance(physical),"Overseer_appearanceA6"):(ushort)2198;
                appearanceId=appearance;
                standing=valid?ReadNarrationPortrait(state,current,_definitions,identity,appearance)
                    :new DeploymentNarrationPortraitRoute(2198,2198,"texture_chara_anime_2198",["face_2198_b","face_2198_a","face_2198_c"]){Kind=StandingPortraitKind.ThreePart};
            });
            Text(19,()=>
            {
                if(!K(current.ReadForceNativeValidity(Force()),"District_Force_validity"))return "";
                int raw=K(current.ReadAreaCitySlot(area),"District_Area35");
                var city=current.ReadCityTableReference(raw<52?raw:0) ?? throw Missing("District_City_pointer");
                int slot=K(current.ReadBaseDistrictSlot(city),"District_ordinal");
                var district=current.ReadDistrictTableReference(slot<52?slot:0) ?? throw Missing("District_pointer");
                if(!K(current.ReadDistrictNativeValidity(district),"District_validity"))return "";
                byte number=K(current.ReadDistrictNumber(slot),"District11"); districtNumber=number;
                return FormatSceneryPanelNumber(ReadHistoryDataText(1,0x494),number);
            });
            Text(23,()=>
            {
                int city=K(current.ReadAreaCitySlot(area),"City_Area35");
                if(city==0) city=K(current.ReadAreaValidityCode(area),"City_Area34");
                if(city==0)return "";
                var receiver=current.ReadCityTableReference(city<52?city:0) ?? throw Missing("City_pointer");
                int physical=K(receiver.PhysicalSlot,"City_physical");
                return physical==0?"":state.Cities.GetValueOrDefault(physical)?.Name ?? throw Missing("City_name");
            });
            Read(120,()=>Coverage(area,120));
            Read(24,()=> { nearby=ReadAreaInformationMarkedArmy(state,current,areaId); if(nearby==false)hidden.Add(24); });
            Read(179,()=>
            {
                int hex=K(current.ReadAreaHexIndex(area),"Area3A");
                var bases=ReadDetailMiniMapBases(current) ?? throw Missing("MiniMap_bases");
                byte color=K(ReadMiniMapColor(current,forceId),"MiniMap_Force_color");
                miniMap=new(bases,DetailMiniMapProjection.Project(DetailMiniMapIconKind.Area,areaId,hex,color));
            });
            Read(-2,()=>
            {
                int raw=K(current.ReadAreaHexIndex(area),"Selection_Area3A");
                var hex=current.ReadHexTableReference(raw<48400?raw:0) ?? throw Missing("Selection_Hex");
                selectionIcon=K(current.ReadHexSurfaceCode(hex),"Selection_Hex10") switch {16=>0x1C3,17=>0x198,18=>0x1D6,_=>null};
            });
            Read(131,()=>
            {
                //22F2D0 compacts valid references in the eight source slots,
                //only where the corresponding static regional-mask bit is set.
                byte mask=_scenario.Snapshot.Areas[areaId].RegionalNeighborMask;
                HexListHandle list=current.CreateTemporaryHexList() ?? throw Missing("Neighbor_list");
                try
                {
                    int output=0;
                    for(int slot=0;slot<8;slot++)
                    {
                        int raw=K(current.ReadAreaNeighborSlot(area,slot),"Neighbor_word");
                        if(raw==0 || (mask&(1<<slot))==0)continue;
                        var next=current.ReadAreaTableReference(raw<501?raw:0) ?? throw Missing("Neighbor_pointer");
                        if(!K(current.ReadAreaNativeValidity(next),"Neighbor_validity"))continue;
                        var node=current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("Neighbor_append");
                        if(!node.Identity.HasValue)continue;
                        current.WriteRegistryNodePayload(node,new(RegistryKind.Area,K(next.PhysicalSlot,"Neighbor_physical")));
                        int row=output++, panel=131+6*row, flagNode=132+6*row;
                        DetailForceAppearance? nextFlag=null;
                        string? unresolved=null;
                        try
                        {
                            int nextId=K(next.PhysicalSlot,"Neighbor_identity");
                            text[133+6*row]=state.Areas.GetValueOrDefault(nextId)?.Name ?? throw Missing("Neighbor_name");
                            styles[133+6*row]=13;
                            Coverage(next,134+6*row);
                            int owner=K(current.ReadAreaCurrentForceSlot(next),"Neighbor_Force");
                            var nextForce=current.ReadForceTableReference(owner<52?owner:0) ?? throw Missing("Neighbor_Force_pointer");
                            _mapInformationNames ??=ArmyFlagPaneNames.Load(_scenario);
                            nextFlag=ReadDetailForceAppearance(state,current,_mapInformationNames,nextForce,
                                K(current.ReadForceNativeValidity(nextForce),"Neighbor_Force_validity"),palettes) ?? throw Missing("Neighbor_flag");
                        }
                        catch(InvalidDataException error)
                        {
                            unresolved=error.Message;missing[flagNode]=error.Message;
                            foreach(int valueNode in new[]{133+6*row,134+6*row,135+6*row,136+6*row})
                                if(!text.ContainsKey(valueNode))missing.TryAdd(valueNode,error.Message);
                        }
                        neighbors.Add(new(row,K(next.PhysicalSlot,"Neighbor_identity"),nextFlag,unresolved));
                    }
                    for(int row=output;row<8;row++) hidden.Add(131+6*row);
                }
                finally {current.ClearTemporaryHexList(list);current.ReleaseTemporaryHexList(list);}
            });
            Read(-3,()=>
            {
                var economy=ReadAreaDetailEconomyCurrent(state,current,areaId);
                foreach(var row in economy.Text)text[row.Key]=row.Value;
                foreach(var row in economy.UnresolvedFields)missing[row.Key]=row.Value;
                foreach(var row in economy.TextAlignments)alignments[row.Key]=row.Value;
                gauges=economy.Gauges;
            });
            Read(-4,()=>
            {
                var status=ReadAreaDetailStatusCurrent(state,current,areaId);
                foreach(var row in status.Text)text[row.Key]=row.Value;
                foreach(var row in status.UnresolvedFields)missing[row.Key]=row.Value;
                foreach(int node in status.HiddenNodes)hidden.Add(node);
                foreach(var row in status.TextColors)colors[row.Key]=row.Value;
                foreach(var row in status.SpriteIds)sprites[row.Key]=row.Value;
                traits=status.Traits;
            });
            ForceReference Force()=>current.ReadForceTableReference(forceId is int known?known<52?known:0:throw Missing("Force_identity")) ?? throw Missing("Force_pointer");
            void Coverage(AreaReference receiver,int first)
            {
                int owner=K(current.ReadAreaCurrentForceSlot(receiver),"Coverage_Force");
                var force=current.ReadForceTableReference(owner<52?owner:0) ?? throw Missing("Coverage_Force_pointer");
                if(!K(current.ReadForceNativeValidity(force),"Coverage_Force_validity"))
                {text[first]=text[first+1]=text[first+2]="";return;}
                int owned=K(current.ReadAreaRawField68(receiver),"Coverage_owned68");
                int total=K(current.ReadAreaRawField76(receiver),"Coverage_total76");
                text[first]=owned.ToString(CultureInfo.InvariantCulture);text[first+1]=ReadHistoryDataText(1,0x60C);text[first+2]=total.ToString(CultureInfo.InvariantCulture);
                int preset=first==120?(owned<total*0.5f?16:owned==total?17:18):0;
                styles[first]=styles[first+1]=styles[first+2]=preset;
            }
        }
        catch(InvalidDataException error){missing[-1]=error.Message;}
        if(missing.TryGetValue(120,out string? coverageMissing))
            foreach(int node in new[]{121,122})if(!text.ContainsKey(node))missing.TryAdd(node,coverageMissing);
        if(missing.TryGetValue(2,out string? overseerMissing) && !text.ContainsKey(16))missing.TryAdd(16,overseerMissing);
        if(missing.TryGetValue(131,out string? neighborsMissing))
            for(int slot=0;slot<8;slot++)
                foreach(int node in new[]{132+6*slot,133+6*slot,134+6*slot,135+6*slot,136+6*slot})
                    if(!text.ContainsKey(node) && !hidden.Contains(131+6*slot))missing.TryAdd(node,neighborsMissing);
        return new(areaId,name,text){HiddenNodes=hidden,TextStyles=styles,TextAlignments=alignments,TextColors=colors,SpriteIds=sprites,
            UnresolvedFields=missing,PortraitAppearanceId=appearanceId,StandingPortrait=standing,ForceId=forceId,ForceNameColor=letter,
            ForceFlag=flag,DistrictNumber=districtNumber,NearbyEnemyPanelVisible=nearby,SelectionIconSpriteId=selectionIcon,
            MiniMap=miniMap,Gauges=gauges,Traits=traits,Neighbors=neighbors,
            UnresolvedOwner=missing.Count==0?null:string.Join("; ",missing.Values.Distinct(StringComparer.Ordinal))};
        void Read(int node,Action query){try{query();}catch(InvalidDataException error){missing[node]=error.Message;}}
        void Text(int node,Func<string> query)=>Read(node,()=>text[node]=query());
        static T K<T>(T? value,string field)where T:struct=>value ?? throw Missing(field);
        static InvalidDataException Missing(string field)=>new("area-information."+field);
    }

    //232570: actual area iterator and signed occupancy, not a hostile-allegiance heuristic.
    private static bool ReadAreaInformationMarkedArmy(GameState state,SourceWorldContext current,int area)
    {
        for(int index=AreaHexIterator.FindNextIndex(0,area,current);index<48400;index=AreaHexIterator.FindNextIndex(index+1,area,current))
        {
            var hex=current.ReadHexTableReference(index) ?? throw new InvalidDataException("area-information.Nearby_Hex");
            short physical=unchecked((short)(current.ReadHexRaw1C(hex) ?? throw new InvalidDataException("area-information.Nearby_Hex1C")));
            if(physical is <1 or >500)continue;
            var army=current.ReadArmyUnitTableReference(physical) ?? throw new InvalidDataException("area-information.Nearby_Army");
            bool valid=current.ReadArmyUnitNativeValidity(army) ?? throw new InvalidDataException("area-information.Nearby_Army_validity");
            if(!valid)continue;
            byte flags=NotificationArmy(state,physical).NativeTacticRuntime?.Flags26 ?? throw new InvalidDataException("area-information.Nearby_Army26");
            if((flags&1)!=0)return true;
        }
        return false;
    }
}

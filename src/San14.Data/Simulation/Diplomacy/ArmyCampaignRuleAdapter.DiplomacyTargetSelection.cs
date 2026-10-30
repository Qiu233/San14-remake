using System.Collections.Immutable;
using San14.Core;
namespace San14.Data;
public sealed partial class ArmyCampaignRuleAdapter
{
    internal DiplomacyTargetSelectionPage ReadDiplomacyTargetSelectionCurrent(GameState state,
        SourceWorldContext current, int playerForceId, DiplomacyTargetSelectionKind kind,
        IReadOnlyList<int> ids, bool returnCaptives = false)
    {
        var page = kind switch
        {
            DiplomacyTargetSelectionKind.Force => new ForceInformationCurrent(this,state,playerForceId,current)
                .DiplomacyTargets(ids,returnCaptives),
            DiplomacyTargetSelectionKind.City => new CityInformationCurrent(this,state,playerForceId,current)
                .DiplomacyTargets(ids),
            DiplomacyTargetSelectionKind.Area => new AreaInformationCurrent(this,state,playerForceId,current)
                .DiplomacyTargets(ids),
            DiplomacyTargetSelectionKind.Item => ReadDiplomacyItemTargetsCurrent(state,current,playerForceId,ids),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        if(kind is DiplomacyTargetSelectionKind.Force or DiplomacyTargetSelectionKind.City)
            page.MapSelection=CreateDiplomacyTargetMap(current,kind,ids);
        return page;
    }
    private ImmutableArray<DiplomacyTargetGroup> DiplomacyGroups(DiplomacyTargetColumns.Column[] columns,
        DiplomacyTargetColumns.Group[] groups)
    {
        var byId=columns.ToDictionary(column=>column.Id);
        return groups.Select((group,index)=>new DiplomacyTargetGroup(index,ReadHistoryDataText(1,group.Caption),
            group.Columns.Select(id=>
            {
                var column=byId[id];
                return new DiplomacyTargetColumn(id,column.Width,column.Caption,ReadHistoryDataText(1,column.Caption),
                    column.Flags,(column.Flags&0x20000000)==0);
            }).ToImmutableArray())).ToImmutableArray();
    }
    private sealed partial class ForceInformationCurrent
    {
        internal ForceInformationCurrent(ArmyCampaignRuleAdapter rules,GameState state,int playerForceId,
            SourceWorldContext current)
        {
            this.rules=rules;this.state=state;this.playerForceId=playerForceId;captured=new(()=>current);
        }
        private Dictionary<int,int>? diplomacyCaptiveCounts;
        private PreparedCell DiplomacyCaptiveCell(int target)
        {
            if(prepared.TryGetValue((target,28),out var saved))return saved;
            return prepared[(target,28)]=MakeCell(()=>Number(DiplomacyCaptiveCount(target)),()=>new NumberKey(DiplomacyCaptiveCount(target)));
        }
        private int DiplomacyCaptiveCount(int target)
        {
            if(diplomacyCaptiveCounts is null)
            {
                var counts=new Dictionary<int,int>();
                //The normal map caller's observer is the actual player; this
                //is2349D0(mode1), not the stricter selectable-ransom predicate.
                int observer=Need(Current.ReadCurrentPlayerForce()?.PhysicalSlot,"diplomacy.observer_Force");
                if(observer is >=1 and <=51)
                    foreach(int? entry in Current.ReadRegisteredPersonPhysicalSlots()??throw Missing("diplomacy.Person_registry"))
                    {
                        int person=Need(entry,"diplomacy.Person_pointer");
                        if(Need(Current.ReadPersonOldForceSlot(person),"diplomacy.Person_Force22CF60")!=observer ||
                            Need(Current.ReadPersonStatus(person),"diplomacy.PersonE6")!=6)continue;
                        var home=BaseLookup.Find(Need(Current.ReadPersonFirstLocation(person),"diplomacy.PersonE2"),Current);
                        if(!Need(Current.ReadBaseNativeValidity(home),"diplomacy.home_validity"))continue;
                        int force=Need(Current.ReadBaseForceSlot(home),"diplomacy.home_Force");
                        counts[force]=counts.GetValueOrDefault(force)+1;
                    }
                diplomacyCaptiveCounts=counts;
            }
            return diplomacyCaptiveCounts.GetValueOrDefault(target);
        }
        internal DiplomacyTargetSelectionPage DiplomacyTargets(IReadOnlyList<int> ids,bool returning)
        {
            var groups=rules.DiplomacyGroups(returning?DiplomacyTargetColumns.ReturnForce:DiplomacyTargetColumns.Force,
                returning?DiplomacyTargetColumns.ReturnForceGroups:DiplomacyTargetColumns.ForceGroups);
            PreparedCell Read(int id,int column) => column==28?DiplomacyCaptiveCell(id):Cell(id,column);
            return new(ids,groups,(id,column)=>
            {
                var cell=Read(id,column);
                return new(cell.Display.Text,cell.Display.UnresolvedOwner)
                { SortUnresolvedInput=cell.SortUnresolvedOwner,FirstGlyphColour=cell.Display.MarkerColour?.CpuPacked };
            },(left,right,column,ascending)=>column==28?
                (Read(left,column).Key??throw Missing("sort_key")).Before(Read(right,column).Key??throw Missing("sort_key"),ascending):
                Before(Read(left,column).Key??throw Missing("sort_key"),Read(right,column).Key??throw Missing("sort_key"),column,ascending));
        }
    }
    private sealed partial class CityInformationCurrent
    {
        internal CityInformationCurrent(ArmyCampaignRuleAdapter rules,GameState state,int playerForceId,
            SourceWorldContext current)
        {
            this.rules=rules;this.state=state;this.playerForceId=playerForceId;captured=new(()=>current);
        }
        private readonly Dictionary<(int,int),PreparedCell> diplomacyBaseCells=[];
        private CampaignHistoryWriter? diplomacyBaseNames;
        private PreparedCell DiplomacyBaseCell(int id,int column)
        {
            if(diplomacyBaseCells.TryGetValue((id,column),out var saved))return saved;
            var city=ReadCity(id);
            string Text()
            {
                if(!Need(Current.ReadBaseNativeValidity(city.Receiver),"diplomacy.Base_validity"))return "";
                if(column==1)
                {
                    var district=Current.ReadBaseDistrictReference(city.Receiver)??throw Missing("diplomacy.Base_District_v80");
                    return Need(Current.ReadDistrictNativeValidity(district),"diplomacy.District_validity")?
                        rules.ReadDistrictInformationLeaderCaption(state,Current,Need(district.PhysicalSlot,"diplomacy.District_receiver")):"";
                }
                if(column==8)
                {
                    var force=Force(city.Receiver);
                    if(!Need(Current.ReadForceNativeValidity(force),"diplomacy.Force_validity"))return "";
                    diplomacyBaseNames??=new(rules._repository,rules._scenario,rules.Seed.Language);
                    return diplomacyBaseNames.FallbackName(state,Current,force,(person,full)=>
                        full?person.Name:person.CurrentSurname??throw Missing("diplomacy.ruler_surname"));
                }
                int governor=Governor(city);
                return Need(Current.ReadPersonNativeValidity(governor),"diplomacy.governor_validity")?PersonText(governor):"";
            }
            //These Base descriptors return tagged *display text*. They do
            //not share the City list's ruler/Person phonetic key operands.
            return diplomacyBaseCells[(id,column)]=MakeCell(Text,()=>new NameKey(Text()));
        }
        internal DiplomacyTargetSelectionPage DiplomacyTargets(IReadOnlyList<int> ids)
        {
            //The diplomacy Base descriptors and the City domain reader have
            //distinct column identities. Match their closed getter semantics.
            int DomainColumn(int column)=>column switch
            {0=>0,8=>1,1=>2,9=>3,10=>4,4=>5,5=>6,3=>7,2=>8,11=>9,12=>10,
                _=>throw Missing("diplomacy_column")};
            PreparedCell Read(int id,int column)=>column is 1 or 8 or 9?DiplomacyBaseCell(id,column):Cell(id,DomainColumn(column));
            return new(ids,rules.DiplomacyGroups(DiplomacyTargetColumns.City,DiplomacyTargetColumns.CityGroups),
                (id,column)=>
                {
                    var cell=Read(id,column);
                    return new(cell.Display.Text,cell.Display.UnresolvedOwner)
                    {SortUnresolvedInput=cell.SortUnresolvedOwner,FirstGlyphColour=cell.Display.MarkerColour?.CpuPacked};
                },(left,right,column,ascending)=>(Read(left,column).Key??throw Missing("sort_key"))
                    .Before(Read(right,column).Key??throw Missing("sort_key"),ascending));
        }
    }
    private sealed partial class AreaInformationCurrent
    {
        internal AreaInformationCurrent(ArmyCampaignRuleAdapter rules,GameState state,int playerForceId,
            SourceWorldContext current)
        {
            this.rules=rules;this.state=state;this.playerForceId=playerForceId;captured=new(()=>current);
        }
        private int? DiplomacyAreaIcon(int id)
        {
            var area=ReadArea(id);
            int raw=Need(Current.ReadAreaHexIndex(area.Receiver),"diplomacy.icon_hex3A");
            var hex=Current.ReadHexTableReference(raw<48400?raw:0)??throw Missing("diplomacy.icon_Hex");
            return Need(Current.ReadHexSurfaceCode(hex),"diplomacy.icon_surface10") switch
            {16=>0x1C3,17=>0x198,18=>0x1D6,_=>null};
        }
        internal DiplomacyTargetSelectionPage DiplomacyTargets(IReadOnlyList<int> ids)
        {
            return new(ids,rules.DiplomacyGroups(DiplomacyTargetColumns.Area,DiplomacyTargetColumns.AreaGroups),
                (id,column)=>
                {
                    var cell=Cell(id,column);
                    int? icon=null;string? iconIssue=null;
                    if(column==0)try{icon=DiplomacyAreaIcon(id);}catch(InvalidDataException error){iconIssue=error.Message;}
                    return new(cell.Display.Text,cell.Display.UnresolvedOwner)
                    {SortUnresolvedInput=cell.SortUnresolvedOwner,FirstGlyphColour=cell.Display.MarkerColour?.CpuPacked,
                        AreaIconSpriteId=icon,AreaIconUnresolvedInput=iconIssue};
                },(left,right,column,ascending)=>(Cell(left,column).Key??throw Missing("sort_key"))
                    .Before(Cell(right,column).Key??throw Missing("sort_key"),ascending)) {HasLeadingAreaIcon=true};
        }
    }
}

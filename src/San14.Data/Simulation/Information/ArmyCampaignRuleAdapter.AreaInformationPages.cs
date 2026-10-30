using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaInformationPageSet PrepareAreaInformationPages(GameState state, int playerForceId,
        IReadOnlyList<int> actualAreaIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actualAreaIds);
        return new AreaInformationCurrent(this, state, playerForceId).Pages(actualAreaIds);
    }

    public AreaAdministrationQuery<AreaInformationPageSet> PrepareAllAreaInformationPages(
        GameState state, int playerForceId, bool ownForceOnly)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var source = new AreaInformationCurrent(this, state, playerForceId);
            return AreaAdministrationQuery<AreaInformationPageSet>.Known(source.Pages(source.OrdinaryCandidates(ownForceOnly)));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<AreaInformationPageSet>.Unknown(error.Message); }
    }

    private sealed partial class AreaInformationCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly int playerForceId;
        private readonly Lazy<SourceWorldContext> captured;
        private SourceWorldContext Current => captured.Value;
        private readonly Dictionary<int,Area> areas = [];
        private readonly Dictionary<(int AreaId,int Column),PreparedCell> prepared = [];

        internal AreaInformationCurrent(ArmyCampaignRuleAdapter rules,GameState state,int playerForceId)
        {
            this.rules=rules;this.state=state;this.playerForceId=playerForceId;
            captured=new(()=>rules.CaptureOwnedArmyReturnWorld(state).Context);
        }
        internal AreaInformationPageSet Pages(IReadOnlyList<int> ids)
        {
            var groups=AreaInformationColumnsSource.Groups.Select((group,index)=>
                new AreaInformationGroup(index,group.CaptionId,Data(1,group.CaptionId),group.ColumnIds.Select(id=>
                {
                    var row=AreaInformationColumnsSource.Rows[id];
                    int format=(row.Flags&1)!=0?3:4;
                    if((row.Flags&8)!=0)format=4;
                    if((row.Flags&2)!=0)format=5;
                    return new AreaInformationColumn(id,row.CaptionId,Data(1,row.CaptionId),row.Width,format,
                        (row.Flags&0x20000000)==0,true,row.Visible);
                }).ToImmutableArray())).ToImmutableArray();
            return new(Data(1,0x5AE),groups,ids,
                (id,column)=>Cell(id,column).Display with {SortUnresolvedOwner=Cell(id,column).SortUnresolvedOwner},
                (id,column)=>Cell(id,column).SortUnresolvedOwner,
                (left,right,column,ascending)=>(Cell(left,column).Key??throw Missing("sort_key"))
                    .Before(Cell(right,column).Key??throw Missing("sort_key"),ascending));
        }
        internal int[] OrdinaryCandidates(bool own)
        {
            int player=Need(Current.ReadForceTableReference(playerForceId is >=0 and <52?playerForceId:0)?.PhysicalSlot,"player_Force");
            var ids=new List<int>();
            //8E84B0 case10 scans the501 Area pointers, then the ordinary
            //empty selected-membership sort retains this caller sequence.
            for(int lookup=0;lookup<501;lookup++)
            {
                var receiver=Current.ReadAreaTableReference(lookup)??throw Missing("candidate_Area_lookup");
                if(!Need(Current.ReadAreaNativeValidity(receiver),"candidate_Area_validity"))continue;
                if(own && Need(Force(receiver).PhysicalSlot,"candidate_Force")!=player)continue;
                int physical=Need(receiver.PhysicalSlot,"candidate_Area_receiver");
                var owner=state.Areas.GetValueOrDefault(physical)??throw Missing("candidate_Area_owner");
                areas[physical]=new(physical,physical,receiver,owner);ids.Add(physical);
            }
            return ids.ToArray();
        }
        private Area ReadArea(int id)
        {
            if(areas.TryGetValue(id,out var saved))return saved;
            if(id is <0 or >500)throw Missing("Area_index");
            var receiver=Current.ReadAreaTableReference(id)??throw Missing("Area_lookup");
            int physical=Need(receiver.PhysicalSlot,"Area_receiver");
            var owner=state.Areas.GetValueOrDefault(physical)??throw Missing("Area_owner");
            return areas[id]=new(id,physical,receiver,owner);
        }
        private PreparedCell Cell(int id,int column)
        {
            if(prepared.TryGetValue((id,column),out var saved))return saved;
            try
            {
                var area=ReadArea(id);
                saved=column switch
                {
                    >=6 and <=14=>DevelopmentCell(area,column),
                    >=15 and <=17=>IncomeCell(area,column),
                    _=>IdentityCell(area,column)
                };
            }
            catch(InvalidDataException error){saved=new(new(null,null,error.Message),null,error.Message);}
            return prepared[(id,column)]=saved;
        }
        private PreparedCell MakeCell(Func<string?> display,Func<SortKey>? sort=null)
        {
            string? text=null,issue=null,sortIssue=null;SortKey? key=null;
            try{text=display()??"";}catch(InvalidDataException error){issue=error.Message;}
            if(sort is not null)try{key=sort();}catch(InvalidDataException error){sortIssue=error.Message;}
            return new(new(text,null,issue),key,sortIssue);
        }
        private ForceReference Force(AreaReference area)
        {
            int raw=Need(Current.ReadAreaCurrentForceSlot(area),"Area_representative_Hex14");
            return Current.ReadForceTableReference(raw<52?raw:0)??throw Missing("Force_lookup");
        }
        private bool Special(Area area)=>Need(Force(area.Receiver).PhysicalSlot,"Force_receiver") is >=46 and <=50;
        private string Data(int section,int id)=>rules.ReadHistoryDataText(section,id);
        private static string Number(long value)=>value.ToString(CultureInfo.InvariantCulture);
        private static T Need<T>(T? value,string field)where T:struct=>value??throw Missing(field);
        private static InvalidDataException Missing(string field)=>new("area-information-list."+field);
        private sealed record Area(int Id,int PhysicalSlot,AreaReference Receiver,AreaState Owner);
        private sealed record PreparedCell(AreaInformationCell Display,SortKey? Key,string? SortUnresolvedOwner);
        private abstract record SortKey{internal abstract bool Before(SortKey other,bool ascending);}
        private sealed record NumberKey(long Value,bool Empty=false):SortKey
        {
            internal override bool Before(SortKey other,bool ascending)
            {
                var right=(NumberKey)other;
                if(Empty!=right.Empty)return ascending?!Empty:Empty;
                return !Empty && (ascending?Value<right.Value:Value>right.Value);
            }
        }
        private sealed record NameKey(string Value,bool Empty=false):SortKey
        {
            internal override bool Before(SortKey other,bool ascending)
            {
                var right=(NameKey)other;
                if(Empty!=right.Empty)return ascending?!Empty:Empty;
                return !Empty && (ascending?PersonNameOrder.Compare(Value,right.Value)<0:PersonNameOrder.Compare(right.Value,Value)<0);
            }
        }
    }
}

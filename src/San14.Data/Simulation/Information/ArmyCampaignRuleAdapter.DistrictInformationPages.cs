using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public DistrictInformationPageSet PrepareDistrictInformationPages(GameState state,int playerForceId,
        IReadOnlyList<int> actualDistrictIds)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(actualDistrictIds);
        return new DistrictInformationCurrent(this,state,playerForceId).Pages(actualDistrictIds);
    }

    public AreaAdministrationQuery<DistrictInformationPageSet> PrepareAllDistrictInformationPages(
        GameState state,int playerForceId,bool ownForceOnly)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var source=new DistrictInformationCurrent(this,state,playerForceId);
            var pages=source.Pages(source.OrdinaryCandidates(ownForceOnly),initialSort:true);
            var sorted=pages.Sort(0,0,true);
            return sorted.Available?AreaAdministrationQuery<DistrictInformationPageSet>.Known(pages)
                :AreaAdministrationQuery<DistrictInformationPageSet>.Unknown(sorted.UnresolvedInput ?? "district-information.initial_sort");
        }
        catch(InvalidDataException error)
        {return AreaAdministrationQuery<DistrictInformationPageSet>.Unknown(error.Message);}
    }

    private sealed partial class DistrictInformationCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly int playerForceId;
        private readonly Lazy<SourceWorldContext> captured;
        private SourceWorldContext Current=>captured.Value;
        private readonly Dictionary<(int District,int Column),PreparedCell> cells=[];
        private readonly Dictionary<(int District,int Node),DistrictDetailEconomyPublication> economics=[];
        private readonly Dictionary<(int District,bool Special),int> people=[];
        private Dictionary<int,int>? cityCounts;

        internal DistrictInformationCurrent(ArmyCampaignRuleAdapter rules,GameState state,int playerForceId)
        {
            this.rules=rules;this.state=state;this.playerForceId=playerForceId;
            captured=new(()=>
            {
                var current=rules.CaptureOwnedArmyReturnWorld(state).Context;
                BaseReturnCaptivesInputs.BindCurrent(state,current);
                current.EventConditions.ScenarioMode=state.CampaignEventWorld?.ScenarioMode;
                return current;
            });
        }

        internal DistrictInformationPageSet Pages(IReadOnlyList<int> ids,bool initialSort=false)
        {
            var groups=DistrictInformationColumnsSource.Groups.Select((group,index)=>
                new DistrictInformationGroup(index,group.CaptionId,Data(1,group.CaptionId),group.ColumnIds.Select(id=>
                {
                    var row=DistrictInformationColumnsSource.Rows[id];
                    int format=(row.Flags&1)!=0?3:4;if((row.Flags&8)!=0)format=4;if((row.Flags&2)!=0)format=5;
                    bool nextAscending=(row.Flags&0x20000000)==0;
                    if(initialSort && index==0 && id==0)nextAscending=!nextAscending;
                    return new DistrictInformationColumn(id,row.CaptionId,Data(1,row.CaptionId),row.Width,format,nextAscending,true,true);
                }).ToImmutableArray())).ToImmutableArray();
            return new(Data(1,0x5EF),groups,ids,
                (id,column)=>Cell(id,column).Display with{SortUnresolvedOwner=Cell(id,column).SortUnresolvedOwner},
                (id,column)=>Cell(id,column).SortUnresolvedOwner,
                (left,right,column,ascending)=>Before(Cell(left,column).Key ?? throw Missing("sort_key"),
                    Cell(right,column).Key ?? throw Missing("sort_key"),column,ascending));
        }

        internal int[] OrdinaryCandidates(bool own)
        {
            int? player=own?Need(OwnedCurrentPlayerForce.ReadCurrentReference(state,Current)?.PhysicalSlot,"candidate_player"):null;
            var result=new List<int>();
            for(int slot=0;slot<52;slot++)
            {
                var district=District(slot);
                if(!Need(Current.ReadDistrictNativeValidity(district),"candidate_validity") || slot==51)continue;
                if(own && ForceSlot(slot)!=player)continue;
                result.Add(Need(district.PhysicalSlot,"candidate_receiver"));
            }
            //8E84B0 ->8B60E0 passes initial sort0, so the caller above applies
            //the actual343CC0 predicate once. It is not a name sort.
            return result.ToArray();
        }

        private PreparedCell Cell(int id,int column)
        {
            if(cells.TryGetValue((id,column),out var saved))return saved;
            try
            {
                _=District(id);
                saved=column switch
                {
                    0=>DistrictNameCell(id),
                    1=>MakeCell(()=>LeaderName(id),()=>LeaderKey(id)),
                    2 or 3 or 4 or >=9 and <=12=>EconomyCell(id,column),
                    5=>MakeCell(()=>Number(CityCount(id)),()=>new NumberKey(CityCount(id))),
                    6=>MakeCell(()=>Number(PersonCount(id,true)),()=>new NumberKey(PersonCount(id,false))),
                    7=>MakeCell(()=>Number11(id)==1?"":PolicyText(id),()=>PolicyKey(id)),
                    8=>MakeCell(()=>TargetText(id),()=>TargetKey(id)),
                    13=>MakeCell(()=>EconomicText(id,99),()=>!Valid(id)||Special(id)?new NumberKey(0,true)
                        :new NumberKey(ReadDistrictAdministrationIntelligenceCurrent(Current,id))),
                    _=>throw Missing("column."+column)
                };
            }
            catch(InvalidDataException error){saved=new(new(null,null,error.Message),null,error.Message);}
            return cells[(id,column)]=saved;
        }

        private PreparedCell MakeCell(Func<string?> display,Func<SortKey> sort)
        {
            string? text=null,missing=null,sortMissing=null;SortKey? key=null;
            try{text=display() ?? "";}catch(InvalidDataException error){missing=error.Message;}
            try{key=sort();}catch(InvalidDataException error){sortMissing=error.Message;}
            return new(new(text,null,missing),key,sortMissing);
        }
        private DistrictReference District(int id)=>id is>=0 and<52
            ?Current.ReadDistrictTableReference(id) ?? throw Missing("District_pointer"):throw Missing("District_identity");
        private bool Valid(int id)=>Need(Current.ReadDistrictNativeValidity(District(id)),"District_validity");
        private int Number11(int id)=>Need(Current.ReadDistrictNumber(id),"District11_number");
        private int Raw(int id,int offset)=>Need(Current.ReadDistrictRawByte(id,offset),"District_"+offset.ToString("X"));
        private int ForceSlot(int id)
        {
            int raw=Need(Current.ReadDistrictForceId(id),"District10_Force");
            return Need(Current.ReadForceTableReference(raw<52?raw:0)?.PhysicalSlot,"Force_receiver");
        }
        private static bool Special(int id)=>id is>=46 and<=50;
        private int Leader(int id)
        {
            int raw=Need(Current.ReadDistrictLeader(id),"District12_leader");
            return Need((raw<=6000?Current.ReadPersonLookupReference(raw):Current.ReadTransferPersonTableReference(0))?.PhysicalSlot,"leader_pointer");
        }
        private string LeaderName(int id)=>Need(Current.ReadPersonNativeValidity(Leader(id)),"leader_validity")?PersonName(Leader(id),false):"";
        private string PersonName(int physical,bool sort)
        {
            int identity=Need(Current.ReadPersonIdentity(physical),"Person_identity");
            if(state.Officers.TryGetValue(identity,out var person))return sort
                ?person.CurrentNameSortKey ?? throw Missing("Person_sort34_5A"):person.Name;
            if(physical==0 && rules._scenario.Snapshot.Characters is{Count:>0} rows && rows[0].Slot==0)
                return sort?rows[0].NameSortKey ?? throw Missing("Person0_sort34_5A"):rows[0].Name;
            throw Missing("Person_owner");
        }
        private SortKey LeaderKey(int id)=>Need(Current.ReadPersonNativeValidity(Leader(id)),"leader_sort_validity")
            ?new NameKey(PersonName(Leader(id),true)):new NameKey("",true);
        private string Data(int section,int id)=>rules.ReadHistoryDataText(section,id);
        private static string Number(int value)=>value.ToString(CultureInfo.InvariantCulture);
        private static T Need<T>(T? value,string field)where T:struct=>value ?? throw Missing(field);
        private static InvalidDataException Missing(string field)=>new("district-information.list."+field);
        private sealed record PreparedCell(DistrictInformationCell Display,SortKey? Key,string? SortUnresolvedOwner);
        private abstract record SortKey{internal virtual bool Empty=>false;internal abstract bool Before(SortKey other,bool ascending);}
        private static bool Before(SortKey left,SortKey right,int column,bool ascending)
        {
            if(DistrictInformationColumnsSource.Rows[column].SortMode==1 && left.Empty!=right.Empty)return !left.Empty;
            return left.Before(right,ascending);
        }
        private sealed record NumberKey(long Value,bool IsEmpty=false):SortKey
        {
            internal override bool Empty=>IsEmpty;
            internal override bool Before(SortKey other,bool ascending)
            {var right=(NumberKey)other;if(Empty!=right.Empty)return ascending?!Empty:Empty;return !Empty && (ascending?Value<right.Value:Value>right.Value);}
        }
        private sealed record NameKey(string Value,bool IsEmpty=false):SortKey
        {
            internal override bool Empty=>IsEmpty;
            internal override bool Before(SortKey other,bool ascending)
            {var right=(NameKey)other;if(Empty!=right.Empty)return ascending?!Empty:Empty;return !Empty && (ascending?PersonNameOrder.Compare(Value,right.Value)<0:PersonNameOrder.Compare(right.Value,Value)<0);}
        }
    }
}

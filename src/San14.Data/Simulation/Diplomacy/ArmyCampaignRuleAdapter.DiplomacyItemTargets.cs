using System.Globalization;
using San14.Core;
namespace San14.Data;
public sealed partial class ArmyCampaignRuleAdapter
{
    private DiplomacyTargetSelectionPage ReadDiplomacyItemTargetsCurrent(GameState state,SourceWorldContext current,
        int playerForceId,IReadOnlyList<int> ids)
    {
        var reader=new DiplomacyItemTargetCurrent(this,state,current,playerForceId);
        return new(ids,DiplomacyGroups(DiplomacyTargetColumns.Item,DiplomacyTargetColumns.ItemGroups),reader.Cell,reader.Before);
    }
    private sealed class DiplomacyItemTargetCurrent
    {
        private readonly ArmyCampaignRuleAdapter rules;
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly ForceInformationCurrent forces;
        private readonly Dictionary<(int,int),Prepared> cells=[];
        private sealed record Key(long Number=0,string? Text=null,bool Empty=false);
        private sealed record Prepared(DiplomacyTargetCell Cell,Key? Key);
        internal DiplomacyItemTargetCurrent(ArmyCampaignRuleAdapter rules,GameState state,SourceWorldContext current,int player)
        {this.rules=rules;this.state=state;this.current=current;forces=new(rules,state,player,current);}
        internal DiplomacyTargetCell Cell(int id,int column)=>Read(id,column).Cell;
        private Prepared Read(int id,int column)
        {
            if(cells.TryGetValue((id,column),out var saved))return saved;
            string? text=null,issue=null,sortIssue=null;Key? key=null;
            try{text=Display(id,column);}catch(InvalidDataException error){issue=error.Message;}
            try{key=SortKey(id,column);}catch(InvalidDataException error){sortIssue=error.Message;}
            return cells[(id,column)]=new(new(text,issue){SortUnresolvedInput=sortIssue},key);
        }
        private ItemDefinition Definition(int id)
        {
            var item=state.Items?.GetValueOrDefault(id)??throw Missing("Item_owner");
            return state.ItemDefinitions.GetValueOrDefault(item.DefinitionId)??throw Missing("Item_definition");
        }
        private ItemAppearance Appearance(int id)=>Definition(id).Appearance??throw Missing("Item_appearance44_48");
        private int Person(int id)
        {
            var holder=state.Items?.GetValueOrDefault(id)?.Holder??throw Missing("Item_holder4A");
            int raw=holder.OfficerId??0;
            return Need((raw<=6000?current.ReadPersonLookupReference(raw):current.ReadTransferPersonTableReference(0))?.PhysicalSlot,"Person_receiver");
        }
        private bool ValidPerson(int person)=>Need(current.ReadPersonNativeValidity(person),"Person_validity");
        private string PersonName(int person)
        {
            int id=Need(current.ReadPersonIdentity(person),"Person_identity");
            if(state.Officers.TryGetValue(id,out var owner))return owner.Name;
            if(person==0&&rules._scenario.Snapshot.Characters is {Count:>0} rows&&rows[0].Slot==0)return rows[0].Name;
            throw Missing("Person_name");
        }
        private string PersonKey(int person)
        {
            int id=Need(current.ReadPersonIdentity(person),"Person_sort_identity");
            if(state.Officers.TryGetValue(id,out var owner))return owner.CurrentNameSortKey??throw Missing("Person_name_key");
            if(person==0&&rules._scenario.Snapshot.Characters is {Count:>0} rows&&rows[0].Slot==0)
                return rows[0].NameSortKey??throw Missing("Person0_name_key");
            throw Missing("Person_sort_owner");
        }
        private int HolderForce(int id)=>Need(current.ReadPersonForce(Person(id))?.PhysicalSlot,"holder_Force");
        private bool ValidForce(int id)=>Need(current.ReadForceNativeValidity(new ForceReference(id)),"Force_validity");
        private int Value(int id)=>Math.Min(Definition(id).LoyaltyValue??throw Missing("Item_value49"),50);
        private int Retained(int id)=>(state.Items?.GetValueOrDefault(id)?.Lifecycle?.RetainedFlags??throw Missing("Item_flags4F"))&1;
        private string Data(int section,int message)=>rules.ReadHistoryDataText(section,message);
        private string Display(int id,int column)
        {
            if(column==0)return Definition(id).Name;
            if(column==4)return Value(id).ToString(CultureInfo.InvariantCulture);
            if(column==7)return Data(1,Retained(id)!=0?0x338:0x339);
            if(column==6){int person=Person(id);return ValidPerson(person)?PersonName(person):"";}
            if(column==5){int person=Person(id);if(!ValidPerson(person))return "";int force=HolderForce(id);return ValidForce(force)?forces.DiplomacyItemForceName(force):"";}
            var item=Appearance(id);
            return column switch
            {
                1=>item.Kind is >=1 and <=26?Data(0,0x81E+item.Kind):"",
                8=>item.AbilityKind==0?"":Data(0,0x183+item.AbilityKind-1)+"+"+Math.Min(item.AbilityValue,10).ToString(CultureInfo.InvariantCulture),
                3=>item.EffectKind is 1 or 2?Data(0,0x839+item.EffectKind):"",
                _=>throw Missing("column")
            };
        }
        private Key SortKey(int id,int column)
        {
            if(column==0)
            {
                if(rules.Seed.Language!="TC")throw Missing("Item_name_comparator_"+rules.Seed.Language);
                return new(Text:Definition(id).NameSortKey??throw Missing("Item_name_key1E"));
            }
            if(column==4)return new(Value(id));
            if(column==7)return new(Retained(id));
            if(column==6){int person=Person(id);return ValidPerson(person)?new(Text:PersonKey(person)):new(Empty:true);}
            if(column==5){int person=Person(id);if(!ValidPerson(person))return new(Empty:true);int force=HolderForce(id);return ValidForce(force)?new(Text:forces.DiplomacyItemForceSortText(force)):new(Empty:true);}
            var item=Appearance(id);
            return column switch
            {
                1=>new(item.Kind),
                8=>item.AbilityKind==0?new(Empty:true):new((5-item.AbilityKind)*1000+Math.Min(item.AbilityValue,10)),
                3=>item.EffectKind==0?new(Empty:true):new(item.EffectKind),
                _=>throw Missing("sort_column")
            };
        }
        internal bool Before(int left,int right,int column,bool ascending)
        {
            var a=Read(left,column).Key??throw Missing("sort_key");
            var b=Read(right,column).Key??throw Missing("sort_key");
            //Common mode1 keeps tagged empties last in either direction.
            if(a.Empty!=b.Empty)return column is 3 or 8?!a.Empty:ascending?!a.Empty:a.Empty;
            if(a.Empty)return false;
            if(a.Text is not null&&b.Text is not null)return ascending?PersonNameOrder.Compare(a.Text,b.Text)<0:PersonNameOrder.Compare(b.Text,a.Text)<0;
            return ascending?a.Number<b.Number:a.Number>b.Number;
        }
        private static T Need<T>(T? value,string name)where T:struct=>value??throw Missing(name);
        private static InvalidDataException Missing(string name)=>new("diplomacy.target.Item."+name);
    }
    private sealed partial class ForceInformationCurrent
    {
        internal string DiplomacyItemForceName(int id)
        {
            int person=id==0?Person(5014):Ruler(id);
            return Need(Current.ReadPersonNativeValidity(person),"Item_ruler_validity")?PersonDisplay(person):"";
        }
        internal string DiplomacyItemForceSortText(int id)
        {
            //22CB40 already resolved the holder's Force pointer.230FB0
            //operates on that physical receiver, not another table lookup.
            if(!Need(Current.ReadForceNativeValidity(new ForceReference(id)),"Item_Force_validity"))return "";
            int person=id==0?Person(5014):Ruler(id);
            if(!Need(Current.ReadPersonNativeValidity(person),"Item_sort_ruler_validity"))return "";
            int identity=Need(Current.ReadPersonIdentity(person),"Item_sort_ruler_identity");
            bool surname=Special(id)&&identity is not(>=5101 and <=5200)&&identity is not(>=2062 and <=2066);
            return PersonSort(person,surname);
        }
    }
}

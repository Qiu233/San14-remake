using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DistrictInformationCurrent
    {
        private CampaignHistoryWriter? names;
        private PreparedCell DistrictNameCell(int id)
        {
            var cell=MakeCell(()=>Valid(id)?rules.ReadDistrictInformationLeaderCaption(state,Current,id):"",()=>DistrictNameKey(id));
            try
            {
                return cell with{Display=cell.Display with{IconSpriteId=Valid(id)
                    ?ArmyMapUiSources.DistrictIcon(checked((byte)Number11(id))).SpriteId:null}};
            }
            catch(InvalidDataException error)
            {return cell with{Display=cell.Display with{UnresolvedOwner=cell.Display.UnresolvedOwner ?? error.Message}};}
        }
        private SortKey DistrictNameKey(int id)
        {
            var player=OwnedCurrentPlayerForce.ReadCurrentReference(state,Current) ?? throw Missing("sort_player");
            int force=ForceSlot(id);
            bool owned=Need(Current.ReadForceNativeValidity(player),"sort_player_validity")
                && Need(Current.ReadForcePhysicalSlot(player),"sort_player_receiver")==force;
            return new DistrictKey(force,Number11(id),owned);
        }
        private sealed record DistrictKey(int Force,int Number,bool IsPlayer):SortKey
        {
            internal override bool Before(SortKey other,bool ascending)
            {
                var right=(DistrictKey)other;
                if(!ascending)return right.Before(this,true);
                if(Force==right.Force)return Number<right.Number;
                if(IsPlayer!=right.IsPlayer)return IsPlayer;
                return Force<right.Force;
            }
        }
        private PreparedCell EconomyCell(int id,int column)
        {
            int node=column switch{2=>32,3=>48,4=>64,9=>37,10=>53,11=>42,12=>58,_=>throw Missing("economy_column")};
            return MakeCell(()=>EconomicText(id,node),()=>Special(id) && column!=4?new NumberKey(0,true)
                :new NumberKey(EconomicNumber(id,node)));
        }
        private DistrictDetailEconomyPublication Economic(int id,int node)
        {
            if(economics.TryGetValue((id,node),out var result))return result;
            //Do not run forecast-only iterators while preparing a stock or
            //troop column. The existing helper keeps all original formulas;
            //the optional node filter only selects the reached getter.
            return economics[(id,node)]=rules.ReadDistrictDetailEconomyCurrent(state,Current,id,new HashSet<int>{node});
        }
        private string EconomicText(int id,int node)
        {
            var result=Economic(id,node);
            if(result.UnresolvedFields.TryGetValue(node,out var error))throw new InvalidDataException(error);
            return result.Text.GetValueOrDefault(node) ?? throw Missing("economy_text."+node);
        }
        private int EconomicNumber(int id,int node)
        {
            var result=Economic(id,node);
            if(result.UnresolvedFields.TryGetValue(node,out var error))throw new InvalidDataException(error);
            return result.NumericValues.TryGetValue(node,out int number)?number:throw Missing("economy_number."+node);
        }
        private int CityCount(int id)
        {
            if(cityCounts is null)
            {
                var counts=new Dictionary<int,int>();
                //22AAC0 uses all52 fixed City pointers and compares District
                //references; it does not add an independent City-valid gate.
                for(int slot=0;slot<52;slot++)
                {
                    var city=Current.ReadCityTableReference(slot) ?? throw Missing("count_City_pointer");
                    int owner=Need(Current.ReadCityDistrictReference(city)?.PhysicalSlot,"count_City_District");
                    counts[owner]=counts.GetValueOrDefault(owner)+1;
                }
                cityCounts=counts;
            }
            return cityCounts.GetValueOrDefault(id);
        }
        private int PersonCount(int id,bool display)
        {
            if(people.TryGetValue((id,display),out int value))return value;
            return people[(id,display)]=ReadDistrictInformationPersonCount(state,Current,id,
                specialFilter:display?null:false);
        }
        private string PolicyText(int id)
        {
            int raw=Raw(id,0x17);
            return raw is>=1 and<=5?Data(0,0x175+raw):"";
        }
        private SortKey PolicyKey(int id)
        {
            if(Number11(id)==1)return new NumberKey(0,true);
            int raw=Raw(id,0x17);
            return new NumberKey(raw,raw==0);
        }
        private string TargetText(int id)
        {
            int kind=Raw(id,0x19),raw=Raw(id,0x18);
            if(kind==2)
            {
                var home=BaseLookup.Find(raw,Current);
                return rules.UnitDetailBaseName(state,Current,home.Kind,Need(home.PhysicalSlot,"target_Base_receiver"));
            }
            if(kind==7)
            {
                var force=Current.ReadForceTableReference(raw<52?raw:0) ?? throw Missing("target_Force_pointer");
                if(!Need(Current.ReadForceNativeValidity(force),"target_Force_validity"))return "";
                names ??= new(rules._repository,rules._scenario,rules.Seed.Language);
                return names.FallbackName(state,Current,force,(person,full)=>full?person.Name:
                    person.CurrentSurname ?? throw Missing("target_Force_surname"));
            }
            return kind==8?DistrictNumberText(raw):"";
        }
        private string DistrictNumberText(int raw)
        {
            var selected=District(raw<52?raw:0);
            int physical=Need(selected.PhysicalSlot,"target_District_receiver");
            return FormatSceneryPanelNumber(Data(1,0x494),Number11(physical));
        }
        private SortKey TargetKey(int id)
        {
            int policy=Raw(id,0x17),raw=Raw(id,0x18);
            return policy switch
            {
                0 or 1 or 4=>new ObjectiveKey(int.MaxValue,0,null),
                2=>new ObjectiveKey(10,10,TargetForceSort(raw)),
                3=>BaseTargetKey(raw),
                5=>new ObjectiveKey(100,100,DistrictNumberText(raw)),
                _=>new ObjectiveKey(0,0,null)
            };
        }
        private SortKey BaseTargetKey(int raw)
        {
            var home=BaseLookup.Find(raw,Current);
            int link=Need(Current.ReadBaseObjectLink(home),"target_Base4E");
            int physical=Need(home.PhysicalSlot,"target_Base_receiver");
            string text;
            if(home.Kind==BaseKind.City)text=rules.ReadCityDetailNameSortKeyCurrent(Current,physical);
            else
            {
                var rows=rules._scenario.Snapshot.Gates;
                if((uint)physical>=(uint)rows.Count || rows[physical].Slot!=physical)throw Missing("target_Gate_definition");
                text=rows[physical].SecondaryText;
            }
            int rank=link<52?1000:10000;
            return new ObjectiveKey(rank,rank,text);
        }
        private string TargetForceSort(int raw)
        {
            var force=Current.ReadForceTableReference(raw<52?raw:0) ?? throw Missing("objective_Force_pointer");
            if(!Need(Current.ReadForceNativeValidity(force),"objective_Force_validity"))return "";
            int physical=Need(Current.ReadForcePhysicalSlot(force),"objective_Force_receiver");
            int person=physical==0?Person(5014):Person(Need(Current.ReadForceRecord(physical)?.RawRuler10,"objective_Force_ruler"));
            if(!Need(Current.ReadPersonNativeValidity(person),"objective_ruler_validity"))return "";
            int identity=Need(Current.ReadPersonIdentity(person),"objective_ruler_identity");
            if(Special(physical) && identity is not(>=5101 and<=5200 or>=2062 and<=2066))
                return state.Officers.GetValueOrDefault(identity)?.CurrentSurnameSortKey ?? throw Missing("objective_ruler_surname_key");
            return PersonName(person,true);
        }
        private int Person(int raw)=>Need((raw<=6000?Current.ReadPersonLookupReference(raw):Current.ReadTransferPersonTableReference(0))?.PhysicalSlot,"Person_pointer");
        private sealed record ObjectiveKey(int ForwardRank,int ReverseRank,string? Text):SortKey
        {
            internal override bool Before(SortKey other,bool ascending)
            {
                var right=(ObjectiveKey)other;
                int a=ascending?ForwardRank:ReverseRank,b=ascending?right.ForwardRank:right.ReverseRank;
                if(a!=b)return ascending?a<b:a>b;
                //342E80 only supplies a pointer for the reached target kinds.
                if(Text is null)return false;
                if(right.Text is null)throw Missing("objective_other_text");
                return ascending?PersonNameOrder.Compare(Text,right.Text)<0:PersonNameOrder.Compare(right.Text,Text)<0;
            }
        }
    }
}

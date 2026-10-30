using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal OfficerDetailRelations ReadOfficerDetailRelationsCurrent(GameState state,
        SourceWorldContext current,int personPhysical)
        => new OfficerDetailRelationReader(this,state,current,personPhysical).Read();

    internal OfficerDetailRelations ReadDeploymentOfficerRelationsCurrent(GameState state,
        SourceWorldContext current, int personPhysical) =>
        new OfficerDetailRelationReader(this, state, current, personPhysical, false).Read();

    private sealed class OfficerDetailRelationReader(ArmyCampaignRuleAdapter package,
        GameState state,SourceWorldContext current,int person, bool filterAffectionVisibility = true)
    {
        private readonly HashSet<string> unresolved=[];
        private readonly ArmyMapUiSources portraitNames=ArmyMapUiSources.Create();
        private static OfficerDetailRelatedPerson Empty()=>new(null,string.Empty,null,null);
        internal OfficerDetailRelations Read()
        {
            var father=Family(0xB4);var mother=Family(0xB6);var spouse=Family(0xBA);
            var sworn=Sworn();var likes=Affections(false);var dislikes=Affections(true);
            //Long biographies are explicitly outside the current restoration
            //scope. Known(null) is intentional absence, not a loading failure.
            return new(father,mother,spouse,sworn,likes,dislikes,
                AreaAdministrationQuery<string?>.Known(null)){UnresolvedOwners=unresolved.ToImmutableArray()};
        }
        private OfficerDetailRelatedPerson Family(int field)
        {
            try
            {
                int physical=Lookup(Word(person,field));
                return Valid(physical)?Card(physical):Empty();
            }
            catch(InvalidDataException error){return Unknown(error.Message);}
        }
        private ImmutableArray<OfficerDetailRelatedPerson> Sworn()
        {
            var members=new List<int>();string? sortingIssue=null;
            try
            {
                int anchor=Lookup(Word(person,0xBC));
                if(!Valid(anchor))return Padded([],3);
                //909A60 scans the complete physical Person pointer table,
                //not the active registry and not a reverse-relation index.
                for(int slot=0;slot<1851;slot++)
                {
                    try
                    {
                        var pointer=current.ReadTransferPersonTableReference(slot) ?? throw Missing("sworn_Person_table");
                        int candidate=Need(pointer.PhysicalSlot,"sworn_Person_pointer");
                        if(Lookup(Word(candidate,0xBC))==anchor)members.Add(candidate);
                    }
                    catch(InvalidDataException error){unresolved.Add(error.Message);}
                }
                //8BBF90/8BB710: the anchor itself is first, followed by age
                //descending. Equal ages retain the physical table order.
                for(int index=1;index<members.Count;index++)
                {
                    int selected=members[index],previous=index-1;
                    while(previous>=0)
                    {
                        bool before;
                        try{before=selected==anchor || members[previous]!=anchor && Age(selected)>Age(members[previous]);}
                        catch(InvalidDataException error)
                        {unresolved.Add(error.Message);sortingIssue=error.Message;break;}
                        if(!before)break;
                        members[previous+1]=members[previous];previous--;
                    }
                    members[previous+1]=selected;
                }
            }
            catch(InvalidDataException error){unresolved.Add(error.Message);}
            var rows=members.Take(3).Select(Card).ToList();
            if(sortingIssue is not null)
                for(int i=0;i<rows.Count;i++)rows[i]=Issue(rows[i],sortingIssue);
            return Padded(rows,3);
        }
        private ImmutableArray<OfficerDetailRelatedPerson> Affections(bool dislike)
        {
            var unknown=new List<OfficerDetailRelatedPerson>();var candidates=new List<int>();
            HexListHandle? list=null;
            try
            {
                list=current.CreateTemporaryHexList() ?? throw Missing("affection_list_pool");
                //22E460/22B9D0 mode0 consume precisely eight saved words.
                //Their successful native-valid lookups append actual nodes.
                for(int slot=0;slot<8;slot++)
                {
                    try
                    {
                        ushort raw=Need(dislike?OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state,person,slot)
                            :OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state,person,slot),dislike?"dislike_D0":"like_C0");
                        int physical=Lookup(raw);
                        if(Valid(physical))Append(list,physical);
                    }
                    catch(InvalidDataException error){unknown.Add(Unknown(error.Message));}
                }
                //8FAEB0 collapses the first four special pairs only while
                //their saved status is neither7 nor9. Other duplicates remain.
                var seenPairs=new bool[4];
                foreach(int physical in People(list))
                {
                    try
                    {
                        int identity=Identity(physical),pair=identity is >=5101 and <=5108?(identity-5101)/2:-1;
                        if(pair>=0 && Status(physical) is not(7 or 9))
                        {if(seenPairs[pair])continue;seenPairs[pair]=true;}
                        candidates.Add(physical);
                    }
                    catch(InvalidDataException error){unknown.Add(Unknown(error.Message));}
                }
                //Its <=8-entry stable sort has only this special-person key.
                candidates=candidates.Where(p=>Identity(p) is >=5101 and <=5200)
                    .Concat(candidates.Where(p=>Identity(p) is not(>=5101 and <=5200))).ToList();
                current.ClearTemporaryHexList(list);
                foreach(int physical in candidates)Append(list,physical);
                candidates=People(list);
                var result=new List<OfficerDetailRelatedPerson>();
                foreach(int physical in candidates)
                {
                    try
                    {
                        if(!filterAffectionVisibility || VisibleAffection(physical,dislike))
                            result.Add(filterAffectionVisibility ? Card(physical) : NameCard(physical));
                    }
                    catch(InvalidDataException error){result.Add(Issue(Card(physical),error.Message));}
                }
                result.AddRange(unknown);
                return Padded(result,8);
            }
            catch(InvalidDataException error)
            {
                unresolved.Add(error.Message);
                var result=candidates.Select(p=>Issue(Card(p),error.Message)).ToList();result.AddRange(unknown);
                if(result.Count==0)result.Add(Unknown(error.Message));
                return Padded(result,8);
            }
            finally
            {
                if(list is not null)
                {
                    try{current.ClearTemporaryHexList(list);current.ReleaseTemporaryHexList(list);}
                    catch(InvalidDataException error){unresolved.Add(error.Message);}
                }
            }
        }
        private OfficerDetailRelatedPerson NameCard(int physical)
        {
            //The setup pane displays these names directly after8FAEB0;
            //it neither reads portrait routes nor adds detail-page visibility.
            try { return new(Identity(physical), Owner(physical).Name, null, null); }
            catch (InvalidDataException error) { return Unknown(error.Message); }
        }
        private bool VisibleAffection(int physical,bool dislike)
        {
            if(!Valid(physical))return false;
            byte status=Status(physical);
            if(status==7)return false;
            //Likes' foreign-person branch excludes7 but intentionally skips
            //status9 and the appearance-year gate; dislikes has no such branch.
            if(!dislike && IsForeignPerson(physical))return true;
            if(status==9)return false;
            int appearance=Owner(physical).AppearanceYear ?? throw Missing("appearance_year_AA");
            int year=Need(current.ReadCurrentYear(),"World_current_year");
            return appearance<=year || Need(current.ReadScenarioMode(),"World44_mode")==2;
        }
        private bool IsForeignPerson(int physical)
        {
            if(!BattleRemakeCurrentRegionFactor.IsNativeBandPerson(Identity(physical)))return false;
            int home=Need(current.ReadPersonFirstLocation(physical),"foreign_Person_E2");
            if(home is <1 or >61)return false;
            var force=current.ReadPersonForce(physical) ?? throw Missing("foreign_Person_Force");
            int ordinal=Need(current.ReadForcePhysicalSlot(force),"foreign_Force_ordinal");
            return ordinal is not(>=46 and <=50);
        }
        private int Age(int physical)
        {
            return PersonCurrentAgeSource.Read(state, current, physical);
        }
        private OfficerDetailRelatedPerson Card(int physical)
        {
            int? identity=null;string name=string.Empty;OfficerPortraitRoute? portrait=null;
            var errors=new List<string>();
            try{identity=Identity(physical);name=Owner(physical).Name;}
            catch(InvalidDataException error){errors.Add(error.Message);unresolved.Add(error.Message);}
            try
            {
                int id=identity ?? Identity(physical);
                ushort appearance=Need(current.ReadPersonPortraitAppearance(physical),"related_person_appearance_A6");
                portrait=ReadOfficerPortraitAppearance(state,current,package._definitions,id,appearance,portraitNames);
                if(portrait.Kind==OfficerPortraitRouteKind.Unresolved)
                {string issue=portrait.UnresolvedInput ?? "officer-detail.relations.portrait";errors.Add(issue);unresolved.Add(issue);}
            }
            catch(InvalidDataException error){errors.Add(error.Message);unresolved.Add(error.Message);}
            return new(identity,name,portrait,errors.Count==0?null:string.Join("; ",errors.Distinct()));
        }
        private OfficerDetailRelatedPerson Unknown(string owner)
        {unresolved.Add(owner);return new(null,string.Empty,null,owner);}
        private OfficerDetailRelatedPerson Issue(OfficerDetailRelatedPerson row,string issue)
        {unresolved.Add(issue);return row with{UnresolvedOwner=row.UnresolvedOwner is null?issue:row.UnresolvedOwner+"; "+issue};}
        private static ImmutableArray<OfficerDetailRelatedPerson> Padded(IEnumerable<OfficerDetailRelatedPerson> rows,int count)
        {
            var result=rows.Take(count).ToList();while(result.Count<count)result.Add(Empty());return result.ToImmutableArray();
        }
        private void Append(HexListHandle list,int physical)
        {
            var node=current.AllocateRegistryNode(new(list.Identity));
            if(node?.Identity is not null)current.WriteRegistryNodePayload(node,new(RegistryKind.Person,physical));
        }
        private List<int> People(HexListHandle list)
        {
            var result=new List<int>();
            var node=current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("affection_list_head");
            while(node.Identity is not null)
            {
                var entry=current.ReadRegistryNodePayload(node) ?? throw Missing("affection_list_payload");
                if(entry.Kind!=RegistryKind.Person)throw Missing("affection_list_person_kind");
                result.Add(Need(entry.PhysicalSlot,"affection_list_person"));
                node=current.ReadRegistryNodeNext(node) ?? throw Missing("affection_list_next");
            }
            return result;
        }
        private ushort Word(int physical,int field)=>Need(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state,physical,field),"relation_word_"+field.ToString("X"));
        private int Lookup(ushort identity)=>(identity<=6000?current.ReadPersonLookupReference(identity):current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw Missing("related_Person_lookup");
        private bool Valid(int physical)=>Need(current.ReadPersonNativeValidity(physical),"related_Person_validity");
        private byte Status(int physical)=>Need(current.ReadPersonStatus(physical),"related_Person_status_E6");
        private ushort Identity(int physical)=>Need(current.ReadPersonIdentity(physical),"related_Person_identity10");
        private OfficerState Owner(int physical)
        {
            int id=Identity(physical);
            if(current.ResolveOfficerSource(id)!=physical || !state.Officers.TryGetValue(id,out var owner))throw Missing("related_Person_owner");
            return owner;
        }
        private static T Need<T>(T? value,string owner)where T:struct=>value??throw Missing(owner);
        private static InvalidDataException Missing(string owner)=>new("officer-detail.relations."+owner);
    }
}

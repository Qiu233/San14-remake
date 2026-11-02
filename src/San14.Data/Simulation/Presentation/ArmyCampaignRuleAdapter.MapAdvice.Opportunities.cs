using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private const int MapAdviceMaximumVisibleEntries=21; //87FFA0 accepts count<0x15.

    // The original regional advice rows are resource identities, not source
    // executable dependencies. Values were extracted once during research.
    private static readonly int[] MapAdviceProvinceSlots=[4,6,7,8,15];
    private static readonly int[] MapAdviceProvinceMessages=[0x5BB4,0x5BB9,0x5BBE,0x5BC3,0x5BC8];
    private static readonly int[] MapAdviceGeneralMessages=[
        0x5AD4,0x5AD9,0x5ADE,0x5AE3,0x5AE8,0x5AED,0x5AF2,0x5AF7,0x5AFC,
        0x5B02,0x5B07,0x5B0D,0x5B12,0x5B18,0x5B1D,0x5B23,0x5B29,0x5B2E,
        0x5B34,0x5B39,0x5B3F,0x5B44,0x5B4A,0x5B50,0x5B56,0x5B5C,0x5B63,
        0x5B68,0x5B6D,0x5B72,0x5B79,0x5B7F,0x5B85,0x5B8B];

    // 1D7220 and1D70E0 exchange two list payloads for (count/2)*count
    // rounds. This consumes the existing pure World450/45C sampler only.
    private static void ShuffleMapAdvice<T>(IList<T> values,TacticSourceSamplerWorld sampler)
    {
        int salt=0,count=values.Count;
        for(int rounds=(count/2)*count;rounds>0;rounds--)
        {
            int first=TacticEffectSourceSamplerMath.Sample(count,1,salt,0,0,0,sampler);
            int second=count-1-TacticEffectSourceSamplerMath.Sample(count,1,salt+1,0,0,0,sampler);
            salt=unchecked(salt+2);
            (values[first],values[second])=(values[second],values[first]);
        }
    }

    private void AddMapAdviceResourceCurrent(GameState state,SourceWorldContext current,
        int speakerId,int rulerId,NarrationResourceMessages formatter,TacticRemakeCurrentAbilities abilities,
        List<MapAdviceEntry> entries,List<string> missing,string key,int message,
        int? province=null,int? force0=null,int? force1=null)
    {
        try
        {
            var people=new DeploymentNarrationCurrentPeople(state,current,abilities,speakerId,rulerId,
                panelReader:(f,e)=>_baseCapacityInputs.ReadPanelModifier(state,f,e),
                appearanceRepository:_repository,appearanceScenario:_scenario,appearanceLanguage:Seed.Language,
                forceArgument:force0,forceArgument1:force1);
            var context=new MapAdviceStrategicArguments(new MapAdviceArguments(people,current,0,null),people,
                province is int selected?new StoryProvinceMessageCurrent(this,()=>state,current,[selected]):null);
            string body=formatter.ResolveStoryText(context,_=>throw MapAdviceMissing("unexpected_presentation_random"),message);
            entries.Add(new(key,body,0,null,null,message));
        }
        catch(InvalidDataException error){missing.Add(key+":"+error.Message);}
    }

    private void AppendMapAdviceOpportunitiesCurrent(GameState state,SourceWorldContext current,
        ForceReference player,DistrictReference primary,int speakerId,int rulerId,
        NarrationResourceMessages formatter,TacticRemakeCurrentAbilities abilities,
        List<MapAdviceEntry> entries,List<string> missing)
    {
        Read("province-benefit",()=>
        {
            var provinces=new List<int>();
            foreach(int province in MapAdviceProvinceSlots)
                if(PersonReturnPathSource.HasQualifiedProvinceAdvantage(state,current,player,province))provinces.Add(province);
            if(provinces.Count==0)return;
            if(provinces.Count>1)ShuffleMapAdvice(provinces,Sampler());
            int selected=provinces[0],index=Array.IndexOf(MapAdviceProvinceSlots,selected);
            AddMapAdviceResourceCurrent(state,current,speakerId,rulerId,formatter,abilities,entries,missing,
                "province-benefit",MapAdviceProvinceMessages[index],province:selected);
        });
        Read("tribal-conquest",()=>
        {
            int elapsed=unchecked((state.CampaignEventWorld?.ElapsedPeriodOffset??throw MapAdviceMissing("conquest_World454_offset"))+
                (int)(state.NativeMapSeason?.AdvancedPeriods??throw MapAdviceMissing("conquest_World454_periods")));
            if(elapsed<=3)return;
            int playerId=MapAdviceNeed(current.ReadForcePhysicalSlot(player),"conquest_player");
            var candidate=new int?[5];var required=new int[5];
            var registered=current.ReadRegistryReferences(RegistryKind.Force)??throw MapAdviceMissing("conquest_Force_registry");
            foreach(var entry in registered)
            {
                int force=MapAdviceNeed(entry?.PhysicalSlot,"conquest_Force_payload");
                if(BandForce(force))continue;
                for(int tribal=46;tribal<=50;tribal++)
                {
                    int at=tribal-46;
                    if(candidate[at] is not null)continue;
                    var eligibility=PersonReturnPathSource.ReadSpecialPersonnelEligibility(state,current,new(force),tribal,ReadApplicationGameplay());
                    if(eligibility.Value is not bool qualifies)throw MapAdviceMissing(eligibility.UnresolvedOwner??"conquest_advantage");
                    if(!qualifies)continue;
                    candidate[at]=force;
                    int friendship=ReadEditedEventFriendship(state,current,new(force),tribal);
                    if(friendship>=70)required[at]=Math.Max(1,Math.Min((int)MathF.Ceiling((friendship-100+1)/10f),
                        (int)MathF.Ceiling((friendship-70+1)/10f)));
                }
            }
            if(!required.Any(x=>x>0))return;
            int district=MapAdviceNeed(primary.PhysicalSlot,"conquest_primary"),capable=0;
            foreach(int? item in current.ReadRegisteredPersonPhysicalSlots()??throw MapAdviceMissing("conquest_Person_registry"))
            {
                int person=MapAdviceNeed(item,"conquest_Person_payload");
                if(MapAdviceNeed(current.ReadPersonDistrictSlot(person),"conquest_Person_District")!=district||
                    MapAdviceNeed(current.ReadPersonStatus(person),"conquest_Person_status") is <1 or >4)continue;
                if(MapAdviceNeed(OfficerHealthSourceAdapter.ReadByte(current,person),"conquest_Person_health")>=2||
                    (MapAdviceNeed(current.ReadArmyReturnPersonFlags196(person),"conquest_Person_flags")&1)!=0||
                    PersonCurrentBusySource.Read(current,person,MapAdviceMissing))continue;
                int force=MapAdviceNeed(current.ReadPersonForce(person)?.PhysicalSlot,"conquest_Person_Force");
                if(force is >=46 and <=50)continue;
                int identity=MapAdviceNeed(current.ReadPersonIdentity(person),"conquest_Person_identity");
                bool special=new[]{4111,4131,4151,4171,4191}.Any(first=>unchecked((uint)(identity-first))<=9)&&
                    MapAdviceNeed(current.ReadPersonFirstLocation(person),"conquest_special_home") is >=1 and <=61;
                if(special)continue;
                int intelligence=MapAdviceNeed(abilities.ResolveDimension(state,identity,2,
                    (f,e)=>_baseCapacityInputs.ReadPanelModifier(state,f,e)),"conquest_intelligence");
                if(intelligence>79)capable++;
            }
            var targets=new List<(int Force,int Tribal,int Required,int Friendship,int People,int Cities)>();
            for(int at=0;at<5;at++)
            {
                if(candidate[at] is not int force||required[at]==0||required[at]>capable||force==playerId)continue;
                if(MapAdviceNeed(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state,player,new(force)),"conquest_protected_pair")||
                    !NeighborForce(force))continue;
                int friendship=ReadEditedEventFriendship(state,current,new(force),46+at);
                int people=0;
                foreach(int? item in current.ReadRegisteredPersonPhysicalSlots()??throw MapAdviceMissing("conquest_tie_registry"))
                {
                    int person=MapAdviceNeed(item,"conquest_tie_payload");
                    if(MapAdviceNeed(current.ReadPersonOldForceSlot(person),"conquest_tie_Force")==force&&
                        MapAdviceNeed(current.ReadPersonStatus(person),"conquest_tie_status") is >=1 and <=4)people++;
                }
                int cities=MapAdviceNeed(PersonArmyCaptureSource.ReadOwnedCityCount(current,new(force)),"conquest_tie_cities");
                targets.Add((force,46+at,required[at],friendship,people,cities));
            }
            if(targets.Count==0)return;
            var chosen=targets.OrderBy(t=>t.Required).ThenByDescending(t=>t.Friendship)
                .ThenByDescending(t=>t.People).ThenByDescending(t=>t.Cities).ThenBy(t=>t.Force).First();
            // This is a strategic reminder only. The native message opens no
            // command from the adviser button, so its Action remains zero.
            AddMapAdviceResourceCurrent(state,current,speakerId,rulerId,formatter,abilities,entries,missing,
                "tribal-conquest",0x5BCD,force0:chosen.Force,force1:chosen.Tribal);
        });
        TacticSourceSamplerWorld Sampler()=>Context(current).ReadCurrentSamplerWorld(state)??throw MapAdviceMissing("sampler_World450_45C");
        void Read(string key,Action action){try{action();}catch(InvalidDataException error){missing.Add(key+":"+error.Message);}}
        bool BandForce(int force)
        {
            ushort ruler=(current.ReadForceRecord(force)??throw MapAdviceMissing("conquest_Force_record")).RawRuler10;
            int person=MapAdviceNeed(current.ReadPersonLookupReference(ruler)?.PhysicalSlot,"conquest_ruler");
            return MapAdviceNeed(current.ReadPersonFirstLocation(person),"conquest_ruler_home") is >=1 and <=61 && force is >=46 and <=50;
        }
        bool NeighborForce(int target)
        {
            foreach(var city in current.ReadRegistryReferences(RegistryKind.City)??throw MapAdviceMissing("conquest_City_registry"))
            {
                int id=MapAdviceNeed(city?.PhysicalSlot,"conquest_City_payload");var home=new BaseReference(BaseKind.City,id);
                if(MapAdviceNeed(current.ReadBaseForceSlot(home),"conquest_City_Force")!=player.PhysicalSlot)continue;
                var list=current.CreateTemporaryHexList()??throw MapAdviceMissing("conquest_neighbor_list");
                try
                {
                    (_captiveFreedomHomes??=new(_scenario)).FillCities22F430(current,home,list);
                    var node=current.ReadRegistryHead(new(list.Identity))??throw MapAdviceMissing("conquest_neighbor_head");
                    while(node.Identity.HasValue)
                    {
                        var payload=current.ReadRegistryNodePayload(node)??throw MapAdviceMissing("conquest_neighbor_payload");
                        if(payload.Kind!=RegistryKind.City)throw MapAdviceMissing("conquest_neighbor_kind");
                        if(MapAdviceNeed(current.ReadBaseForceSlot(new(BaseKind.City,payload.PhysicalSlot)),"conquest_neighbor_Force")==target)return true;
                        node=current.ReadRegistryNodeNext(node)??throw MapAdviceMissing("conquest_neighbor_next");
                    }
                }
                finally{current.ClearTemporaryHexList(list);current.ReleaseTemporaryHexList(list);}
            }
            return false;
        }
    }

    private void AppendMapAdviceGeneralTipsCurrent(GameState state,SourceWorldContext current,
        int speakerId,int rulerId,NarrationResourceMessages formatter,TacticRemakeCurrentAbilities abilities,
        List<MapAdviceEntry> entries,List<string> missing)
    {
        try
        {
            var sampler=Context(current).ReadCurrentSamplerWorld(state)??throw MapAdviceMissing("tips_World450_45C");
            var messages=MapAdviceGeneralMessages.ToArray();ShuffleMapAdvice(messages,sampler);
            foreach(int message in messages.Take(2))
                AddMapAdviceResourceCurrent(state,current,speakerId,rulerId,formatter,abilities,entries,missing,
                    "general-tip-"+message.ToString("X"),message);
        }
        catch(InvalidDataException error){missing.Add("general-tips:"+error.Message);}
    }
}

using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //8887C0's independent current advisory leaves. The map host calls this
    //with the six-group publication's retained current and resource formatter.
    private void AppendMapAdviceStrategicCurrent(GameState state,SourceWorldContext current,
        ForceReference player,DistrictReference primary,int speakerId,int rulerId,
        NarrationResourceMessages formatter,TacticRemakeCurrentAbilities abilities,
        List<MapAdviceEntry> entries,List<string> missing)
    {
        int forceId=MapAdviceNeed(player.PhysicalSlot,"strategic_Force");
        int districtId=MapAdviceNeed(primary.PhysicalSlot,"strategic_District");
        current.CurrentPlayerForce=player;
        void Add(string key,int root,int action=0,int? city=null,int? area=null,int? scenery=null,
            int count=0,string? text=null,int? province=null,int? targetForce=null)
        {
            try
            {
                var people=new DeploymentNarrationCurrentPeople(state,current,abilities,speakerId,rulerId,area,
                    panelReader:(f,e)=>_baseCapacityInputs.ReadPanelModifier(state,f,e),cityArgument:city is int c?(ushort)c:null,
                    appearanceRepository:_repository,appearanceScenario:_scenario,appearanceLanguage:Seed.Language,forceArgument:targetForce);
                var basic=new MapAdviceArguments(people,current,count,text);
                var context=new MapAdviceStrategicArguments(basic,people,province is int region?
                    new StoryProvinceMessageCurrent(this,()=>state,current,[region]):null);
                string body=formatter.ResolveStoryText(context,_=>throw MapAdviceMissing("unexpected_presentation_random"),root);
                entries.Add(new(key,body,action,city,area,root){SceneryId=scenery});
            }
            catch(InvalidDataException error){missing.Add(key+":"+error.Message);}
        }
        void Read(string key,Action read)
        {try{read();}catch(InvalidDataException error){missing.Add(key+":"+error.Message);}}
        Read("tribal-incursion",()=>
        {
            var threatened=new List<AreaState>();
            var armies=state.Units.Values.ToDictionary(a=>MapAdviceNeed(a.NativeArmyOrigin?.NativeSlotIndex,"tribe_Army_receiver"));
            foreach(var entry in current.ReadRegistryReferences(RegistryKind.Army)??throw MapAdviceMissing("tribe_Army_registry"))
            {
                int receiver=MapAdviceNeed(entry?.PhysicalSlot,"tribe_Army_payload");
                if(MapAdviceNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state,current,receiver),"tribe_Army_excluded"))continue;
                if(!armies.TryGetValue(receiver,out var army))throw MapAdviceMissing("tribe_Army_domain_owner");
                if(army.ForceId is <46 or >51)continue;
                var hex=current.ReadHexTableReference(army.HexId)??throw MapAdviceMissing("tribe_Hex_pointer");
                int rawArea=MapAdviceNeed(current.ReadHexAreaSlot(hex),"tribe_Hex_Area");
                var area=current.ReadAreaTableReference(rawArea<501?rawArea:0)??throw MapAdviceMissing("tribe_Area_pointer");
                if(!MapAdviceNeed(current.ReadAreaNativeValidity(area),"tribe_Area_validity")||
                    MapAdviceNeed(current.ReadAreaCurrentForceSlot(area),"tribe_Area_Force")!=forceId)continue;
                int rawCity=MapAdviceNeed(current.ReadAreaCitySlot(area),"tribe_Area_City");
                var city=current.ReadCityTableReference(rawCity<52?rawCity:0)??throw MapAdviceMissing("tribe_City_pointer");
                if(MapAdviceNeed(current.ReadBaseDistrictSlot(city),"tribe_City_District")!=districtId)continue;
                threatened.Add(state.Areas[MapAdviceNeed(area.PhysicalSlot,"tribe_Area_identity")]);
            }
            if(threatened.Count!=0)
            {var first=threatened[0];Add("tribal-incursion",threatened.Count==1?0x5AC5:0x5ACA,city:first.CityId,area:first.Id,count:threatened.Count);}
        });
        Read("personnel-shortage",()=>
        {
            int districts=0;
            foreach(var entry in current.ReadRegistryReferences(RegistryKind.District)??throw MapAdviceMissing("District_registry"))
            {
                if(entry is not{PhysicalSlot:int id})throw MapAdviceMissing("District_payload");
                if(MapAdviceNeed(current.ReadDistrictForceSlot(new(id)),"District_Force")==forceId)districts++;
            }
            if(districts!=1)return;
            int people=0;
            foreach(int? entry in current.ReadRegisteredPersonPhysicalSlots()??throw MapAdviceMissing("Person_registry"))
            {
                int id=MapAdviceNeed(entry,"Person_payload");
                if(MapAdviceNeed(current.ReadPersonOldForceSlot(id),"Person_Force")!=forceId)continue;
                if(MapAdviceNeed(current.ReadPersonStatus(id),"Person_status") is >=1 and <=4)people++;
            }
            int count=MapAdviceNeed(PersonArmyCaptureSource.ReadOwnedCityCount(current,player),"Force_City_count");
            if(people<count*5)Add("personnel-shortage",0x5ACF);
        });
        Read("province-opportunities",()=>
        {
            var reader=new ProvinceIndicatorReader(this,state,current);
            var gain=new List<CityState>();var loss=new List<CityState>();
            foreach(var city in state.Cities.Values.OrderBy(c=>c.Id))
            {
                var row=reader.Read(city.Id);
                if(row.GainOpportunity is null||row.LossWarning is null)
                {if(row.UnresolvedOwner is {} issue)missing.Add("province-city-"+city.Id+":"+issue);}
                if(row.IsPlayerCity)
                {if(row.LossWarning==true)loss.Add(city);continue;}
                if(row.GainOpportunity!=true)continue;
                var at=new BaseReference(BaseKind.City,city.Id);
                int other=MapAdviceNeed(current.ReadBaseForceSlot(at),"province_City_Force");
                var force=current.ReadForceTableReference(other<52?other:0)??throw MapAdviceMissing("province_Force_pointer");
                if(MapAdviceNeed(current.ReadForceNativeValidity(force),"province_Force_validity")&&
                    MapAdviceNeed(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state,player,force),"province_protected_pair"))continue;
                gain.Add(city);
            }
            IEnumerable<CityState> OpportunityOrder(IEnumerable<CityState> source)=>source
                .OrderByDescending(c=>c.Troops)
                .ThenByDescending(c=>new FootholdMapSummaryValues(this,state,current,new(BaseKind.City,c.Id),c.Id).Defense)
                .ThenBy(c=>new FootholdMapSummaryValues(this,state,current,new(BaseKind.City,c.Id),c.Id).HomePeople).ThenBy(c=>c.Id);
            int Province(CityState city)=>MapAdviceNeed(_growthQueries.ReadCityState(state,city.Id),"province_CityCE");
            var special=OpportunityOrder(gain.Where(c=>Province(c) is 3 or 5 or 14)).FirstOrDefault();
            if(special is not null)
            {
                int province=Province(special);int message=province switch{5=>0x5BA0,3=>0x5B9B,_=>0x5B96};
                Add("province-gain-special",message,city:special.Id,province:province);
            }
            var ordinary=OpportunityOrder(gain.Where(c=>Province(c) is not(3 or 5 or 14))).FirstOrDefault();
            if(ordinary is not null)Add("province-gain",0x5B91,city:ordinary.Id,province:Province(ordinary));
            var vulnerable=OpportunityOrder(loss).FirstOrDefault();
            if(vulnerable is not null)Add("province-loss",0x5BA5,city:vulnerable.Id,province:Province(vulnerable));
        });
        Read("tribal-goodwill",()=>
        {
            PersonReturnCommands.BindCurrent(state,current);
            if(!ReadMainCommandAvailabilityCurrent(state,current,MapMenuCommand.Goodwill))return;
            var diplomacy=new DiplomacyUiSource(this,state,current,_scenario,forceId,districtId,
                DiplomaticMissionKind.Goodwill,_=>throw MapAdviceMissing("goodwill_card_not_requested"));
            int? target=null;int best=int.MaxValue;
            foreach(int candidate in diplomacy.Targets)
            {
                if(candidate is <46 or >50)continue;
                var force=current.ReadForceTableReference(candidate)??throw MapAdviceMissing("goodwill_Force");
                ushort ruler=(current.ReadForceRecord(candidate)??throw MapAdviceMissing("goodwill_Force_record")).RawRuler10;
                int person=MapAdviceNeed(current.ReadPersonLookupReference(ruler)?.PhysicalSlot,"goodwill_ruler");
                if(MapAdviceNeed(current.ReadPersonFirstLocation(person),"goodwill_ruler_home") is <1 or >61)continue;
                int friendship=ReadEditedEventFriendship(state,current,force,forceId);
                if(friendship<best){target=candidate;best=friendship;}
            }
            if(target is int chosen&&PersonReturnPathSource.ReadTribalRequestProvinceCurrent(state,current,chosen) is int province)
                Add("tribal-goodwill",0x5BAA,province:province,targetForce:chosen);
        });
        Read("overseas-trade",()=>
        {
            PersonReturnCommands.BindCurrent(state,current);
            if(!NormalTurnForeignTradeAvailable(state,current,player,primary,districtId))return;
            var caravans=(current.ReadOwnedPersonCommandManager()??throw MapAdviceMissing("trade_manager")).Caravans
                ??throw MapAdviceMissing("trade_caravans");
            var application=ReadApplicationGameplay();
            var candidates=new List<int>();
            for(int country=1;country<5;country++)
                if(PersonReturnPathSource.ReadForeignTradeEligibility(state,current,player,country,application)&&
                    !caravans.Values.Any(c=>c.SourceForceId==forceId&&c.CountryId==country))candidates.Add(country);
            if(candidates.Count==0)return;
            int target=candidates.OrderBy(id=>ForeignTradeCurrentSource.ReadFriendship(state,forceId,id)).ThenBy(id=>id).First();
            var definition=ReadForeignTradeCountry(target);
            int province=MapAdviceNeed(_growthQueries.ReadCityState(state,definition.HostCityId),"trade_HostCity_province");
            Add("overseas-trade",0x5BAF,text:definition.Name,province:province);
        });
    }
    private sealed class MapAdviceStrategicArguments(INarrationCurrentContext basic,
        INarrationCurrentContext people,StoryProvinceMessageCurrent? provinces):INarrationCurrentContext
    {
        public int PersonValue(int a,int f)=>basic.PersonValue(a,f);
        public string PersonText(int a,int f)=>basic.PersonText(a,f);
        public int Relationship(int f,int a,int b)=>basic.Relationship(f,a,b);
        public int Personality(int a,Func<int> f)=>basic.Personality(a,f);
        public string AreaText(int a,int f)=>people.AreaText(a,f);
        public int AreaValue(int a,int f)=>people.AreaValue(a,f);
        public string CityText(int a,int f)=>people.CityText(a,f);
        public int CityValue(int a,int f)=>people.CityValue(a,f);
        public string ForceText(int a,int f)=>people.ForceText(a,f);
        public int ForceValue(int a,int f)=>people.ForceValue(a,f);
        public string ProvinceText(int a,int f)=>provinces?.ProvinceText(a,f)??people.ProvinceText(a,f);
        public int ProvinceValue(int a,int f)=>provinces?.ProvinceValue(a,f)??people.ProvinceValue(a,f);
        public int IntegerValue(int a)=>basic.IntegerValue(a);
        public string StringText(int a,int f)=>basic.StringText(a,f);
        public int StringValue(int a,int f)=>basic.StringValue(a,f);
    }
}

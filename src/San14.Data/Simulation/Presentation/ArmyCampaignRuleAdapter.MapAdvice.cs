using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapAdviceEntry(string Key, string Body, int Action, int? CityId, int? AreaId, int MessageId)
{ public int? SceneryId { get; init; } }
public sealed record MapAdvicePublication(int PersonId, string Name, IReadOnlyList<MapAdviceEntry> Entries, OfficerPortraitRoute Portrait)
{
    public IReadOnlyList<string> UnresolvedOwners { get; init; } = Array.Empty<string>();
}

public sealed record MapAdvicePreparation(GameState State, AreaAdministrationQuery<MapAdvicePublication> Publication);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary CMainAdviceLayout's current owner. Call when the
    /// campaign world changes or on explicit refresh, never from painting.</summary>
    public AreaAdministrationQuery<MapAdvicePublication> QueryMapAdvice(GameState state)
        => QueryMapAdviceCurrent(state,null);

    /// <summary>Explicit presentation preparation. The host accepts the reached App32 cursor once
    /// with this publication, then rotates its retained entries without running
    /// the producer again. Only reached narration App32 draws change State.</summary>
    public MapAdvicePreparation PrepareMapAdvice(GameState state,CampaignAppRandomState? appRandomSnapshot=null)
    {
        var source=appRandomSnapshot is null?state:state with{CampaignAppRandom32=appRandomSnapshot};
        var latest=source;
        var publication=QueryMapAdviceCurrent(source,value=>latest=value);
        return new(latest,publication);
    }

    private AreaAdministrationQuery<MapAdvicePublication> QueryMapAdviceCurrent(GameState state,Action<GameState>? publishReached)
    {
        try
        {
            var current = CaptureReportContentCurrent(state);
            BindOwnedArmyReturnWorld(state,current);
            var lifecycle = state.ReadArmyLifecycle() ?? throw MapAdviceMissing("saved_indexes");
            _ = WorldIndexContext.ImportOwnedState(current,lifecycle.Indexes,state);
            _moraleWaveInputs.BindCurrent(state,current);
            _personAssignmentPrecallInputs.BindAreaAdministrationScalars(state,current);
            _baseCapacityInputs.BindCurrent(state,current);
            AreaStatisticsSourceAdapter.BindCurrent(state,current);
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state,current) ?? throw MapAdviceMissing("player_force");
            int forceId = MapAdviceNeed(player.PhysicalSlot,"player_pointer");
            if (!MapAdviceNeed(current.ReadForceNativeValidity(player),"player_validity"))
                return AreaAdministrationQuery<MapAdvicePublication>.Known(null!);
            var force = current.ReadForceRecord(forceId) ?? throw MapAdviceMissing("force_record");
            int rulerId = force.RawRuler10;
            var primary = ForcePrimaryDistrictSource.Resolve(current,player);
            int districtId = MapAdviceNeed(primary.PhysicalSlot,"primary_district");
            var selection = new PersonDeathHistorySelection(state,current,
                current.ReadOwnedCurrentListPool() ?? throw MapAdviceMissing("current_pool"),
                SearchAbilities(current),_governorInputs,(f,e)=>_baseCapacityInputs.ReadPanelModifier(state,f,e));
            var speaker = selection.Select(primary);
            int physical = MapAdviceNeed(speaker.PhysicalSlot,"speaker_pointer");
            if (!MapAdviceNeed(current.ReadPersonNativeValidity(physical),"speaker_validity"))
                return AreaAdministrationQuery<MapAdvicePublication>.Known(null!);
            int speakerId = MapAdviceNeed(current.ReadPersonIdentity(physical),"speaker_identity");
            var person = state.Officers.GetValueOrDefault(speakerId) ?? throw MapAdviceMissing("speaker_owner");
            ushort appearance = MapAdviceNeed(current.ReadPersonPortraitAppearance(physical),"speaker_appearance");
            var portrait = ReadOfficerPortraitAppearance(state,current,_definitions,speakerId,appearance,ArmyMapUiSources.Create());
            if (portrait.Kind == OfficerPortraitRouteKind.Unresolved) throw MapAdviceMissing(portrait.UnresolvedInput ?? "speaker_portrait");
            var entries = new List<MapAdviceEntry>();
            var missing = new List<string>();
            var formatter = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language,MessageKind.Game).Messages);
            var abilities = SearchAbilities(current);
            void Add(string key,int root,int action=0,int? city=null,int? area=null,int? scenery=null,int count=0,string? text=null)
            {
                try
                {
                    var people = new DeploymentNarrationCurrentPeople(state,current,abilities,speakerId,rulerId,area,
                        panelReader:(f,e)=>_baseCapacityInputs.ReadPanelModifier(state,f,e),cityArgument:city is int c?(ushort)c:null,
                        appearanceRepository:_repository,appearanceScenario:_scenario,appearanceLanguage:Seed.Language);
                    string body = formatter.ResolveStoryText(new MapAdviceArguments(people,current,count,text),
                        _=>throw MapAdviceMissing("unexpected_presentation_random"),root);
                    entries.Add(new(key,body,action,city,area,root){SceneryId=scenery});
                }
                catch (InvalidDataException error) { missing.Add(key+":"+error.Message); }
            }
            void Read(string key,Action read)
            { try { read(); } catch (InvalidDataException error) { missing.Add(key+":"+error.Message); } }
            Add("welcome",0x5AA0);
            var cities = state.Cities.Values.Where(c=>c.ForceId==forceId&&c.DistrictId==districtId).ToArray();
            int rulerCity = state.Officers.GetValueOrDefault(rulerId)?.LocationId ?? 0;
            IEnumerable<CityState> CityOrder(IEnumerable<CityState> source) => source
                .OrderByDescending(c=>c.Id==rulerCity)
                .ThenByDescending(c=>MapAdviceNeed(current.ReadCitySizeB8(new(BaseKind.City,c.Id)),"sort_city_size"))
                .ThenByDescending(c=>c.AreaIds.Sum(a=>MapAdviceNeed(state.Areas.GetValueOrDefault(a)?.NativeNormalAftermath?.Quantity52,"sort_population")))
                .ThenBy(c=>c.Id);
            Read("recruitment",()=>
            {
                var candidates = cities.Where(c=>MapAdviceNeed(current.ReadAssignmentCityCA(c.Id),"CityCA")==0&&
                    new FootholdMapSummaryValues(this,state,current,new(BaseKind.City,c.Id),c.Id) is {} value&&value.Troops<value.TroopCapacity).ToArray();
                if (candidates.Length!=0) Add("recruitment",candidates.Length==1?0x5AA5:0x5AA9,1,CityOrder(candidates).First().Id,count:candidates.Length);
            });
            Read("training",()=>
            {
                var candidates = cities.Where(c=>MapAdviceNeed(current.ReadAssignmentCityCC(c.Id),"CityCC")==0&&
                    new FootholdMapSummaryValues(this,state,current,new(BaseKind.City,c.Id),c.Id) is {} value&&value.Morale<value.MoraleMaximum).ToArray();
                if (candidates.Length!=0) Add("training",candidates.Length==1?0x5AAD:0x5AB1,2,CityOrder(candidates).First().Id,count:candidates.Length);
            });
            var ownAreas = cities.SelectMany(c=>c.AreaIds).Distinct().Select(a=>state.Areas.GetValueOrDefault(a)
                ?? throw MapAdviceMissing("city_area_owner")).Where(a=>a.ForceId==forceId).ToArray();
            IEnumerable<AreaState> AreaOrder(IEnumerable<AreaState> source) => source.OrderByDescending(a=>a.CityId==rulerCity)
                .ThenByDescending(a=>a.CityId is int c?MapAdviceNeed(current.ReadCitySizeB8(new(BaseKind.City,c)),"sort_area_city_size"):0)
                .ThenByDescending(a=>MapAdviceNeed(a.NativeNormalAftermath?.Quantity52,"sort_area_population")).ThenBy(a=>a.Id);
            Read("appointment",()=>
            {
                var development = new OwnedAreaDevelopmentContext(state,current,_normalAftermathInputs);
                var candidates = ownAreas.Where(a=>MapAdviceNeed(current.ReadAssignmentArea36(a.Id),"Area36")==0&&
                    _normalAftermathInputs.Development.ReadLevel(MapAdviceNeed(development.ReadAreaProgress(new(a.Id),CeAreaDevelopmentField.Commerce),"Area_commerce"))<5).ToArray();
                if (candidates.Length!=0)
                {
                    var first=AreaOrder(candidates).First();
                    Add("appointment",candidates.Length==1?0x5AB5:0x5AB9,3,first.CityId,first.Id,count:candidates.Length);
                }
            });
            Read("coverage",()=>
            {
                var candidates=ownAreas.Where(a=>MapAdviceNeed(current.ReadAreaRawField68(new(a.Id)),"Area68")<
                    MapAdviceNeed(current.ReadAreaRawField76(new(a.Id)),"Area76")).ToArray();
                if(candidates.Length!=0)
                {var first=candidates[0];Add("coverage",candidates.Length==1?0x5ABD:0x5AC1,city:first.CityId,area:first.Id,count:candidates.Length);}
            });
            AppendMapAdviceStrategicCurrent(state,current,player,primary,speakerId,rulerId,formatter,abilities,entries,missing);
            AppendMapAdviceOpportunitiesCurrent(state,current,player,primary,speakerId,rulerId,formatter,abilities,entries,missing);
            Read("investment",()=>
            {
                var sites=new List<(int City,int Site)>();
                var investmentCurrent=CaptureSceneryMapCurrent(state);
                foreach(var city in cities)
                    foreach(int site in InvestmentCandidates(state,investmentCurrent,city.Id,0))sites.Add((city.Id,site));
                if(sites.Count!=0)
                {
                    var selected=sites.OrderByDescending(x=>x.City==rulerCity)
                        .ThenByDescending(x=>MapAdviceNeed(current.ReadCitySizeB8(new(BaseKind.City,x.City)),"sort_scenery_city_size"))
                        .ThenBy(x=>x.Site).First();
                    Add("investment",sites.Count==1?0x5BD2:0x5BD7,4,selected.City,scenery:selected.Site,count:sites.Count,
                        text:_landmarkDefinitions.Catalog.Sceneries[selected.Site].Name);
                }
            });
            AppendMapAdviceWarningsCurrent(state,current,player,primary,speakerId,rulerId,formatter,abilities,entries,missing,publishReached);
            AppendMapAdviceGeneralTipsCurrent(state,current,speakerId,rulerId,formatter,abilities,entries,missing);
            return AreaAdministrationQuery<MapAdvicePublication>.Known(new(speakerId,person.Name,entries.Take(MapAdviceMaximumVisibleEntries).ToArray(),portrait)
            {UnresolvedOwners=missing});
        }
        catch(InvalidDataException error) {return AreaAdministrationQuery<MapAdvicePublication>.Unknown(error.Message);}
    }

    private static T MapAdviceNeed<T>(T? value,string owner) where T:struct => value??throw MapAdviceMissing(owner);
    private static InvalidDataException MapAdviceMissing(string owner)=>new("map.advice."+owner);

    private sealed class MapAdviceArguments(INarrationCurrentContext people,SourceWorldContext current,int count,string? text):INarrationCurrentContext
    {
        public int PersonValue(int a,int f)
        {
            if(f!=0x71)return people.PersonValue(a,f);
            //3108E0 case71: actual Person Force, then232F80's valid
            //kingdom-name definition6. It is unrelated to doctrine or ID.
            int id=people.PersonValue(a,0);
            int physical=MapAdviceNeed(current.ReadPersonLookupReference(id)?.PhysicalSlot,"q_Person_pointer");
            var force=current.ReadPersonForce(physical)??throw MapAdviceMissing("q_Force_pointer");
            if(!MapAdviceNeed(current.ReadForceNativeValidity(force),"q_Force_validity"))return 0;
            return MapAdviceNeed(current.ReadForceKingdomNameIndex(MapAdviceNeed(force.PhysicalSlot,"q_Force_identity")),"q_kingdom48")==6?1:0;
        }
        public string PersonText(int a,int f)=>people.PersonText(a,f);
        public int Relationship(int f,int a,int b)=>people.Relationship(f,a,b);
        public int Personality(int a,Func<int> f)=>people.Personality(a,f);
        public string AreaText(int a,int f)=>people.AreaText(a,f);
        public string CityText(int a,int f)=>people.CityText(a,f);
        public int IntegerValue(int a)=>a==0?count:throw MapAdviceMissing("integer_argument");
        public string StringText(int a,int f)=>a==0&&f is 0 or '2'?text??throw MapAdviceMissing("string_argument"):throw MapAdviceMissing("string_field");
        public int StringValue(int a,int f)=>a==0&&f is 0 or '2' or '3'?f=='3'?(text??"").Length:count:throw MapAdviceMissing("string_field");
    }
}

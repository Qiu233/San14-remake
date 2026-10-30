using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual application inputs captured by the title owner. This
/// private scenario does not seed or replace application random generators.</summary>
public sealed record CollectionEventSessionInputs(ulong RandomSeed,
    CampaignAppRandomState AppRandom, CampaignCrtRandomState CrtRandom,
    IS14AudioEventExecutionState Audio, NarrationVoiceSettings VoiceSettings,
    NewGameOptions Options);

public sealed record CollectionEventPreparation(int RowId, LoadedScenario? Scenario,
    LoadedMap? Map, GameState? World, ArmyCampaignRuleAdapter? Rules,
    EventStorySession? Session, int? PlayerForceId, CollectionEventConditions Conditions,
    string? UnresolvedOwner);

public sealed partial class CollectionEventCatalog
{
    private readonly Dictionary<int, S14EventConditionProgram> selectedConditionPrograms = [];

    /// <summary>Each playback starts from the selected scenario, with a new
    /// cursor and the application's current audio owner. Replaying a row never
    /// reuses its previous completed cursor or reparses the event resource.</summary>
    public EventStorySession CreatePlaybackSession(int rowId, CollectionEventPreparation preparation,
        IS14AudioEventExecutionState audio)
    {
        ArgumentNullException.ThrowIfNull(preparation); ArgumentNullException.ThrowIfNull(audio);
        var plan = ReadPlayback(rowId);
        if (preparation.RowId != rowId || preparation.UnresolvedOwner is not null ||
            preparation.World is null || preparation.Rules is null)
            throw new InvalidOperationException("collection.event.selected_world");
        return EventStorySession.CreateLoaded(events, preparation.World, preparation.Rules,
            plan.ScriptEventIds, plan.ExecutionMode, plan.VariantFlag, audio);
    }

    /// <summary>6354E0's selected-scenario environment, isolated from any
    /// playable campaign. A locked row can preview its conditions; it cannot
    /// acquire a playback session or write occurrence progress.</summary>
    public CollectionEventPreparation PrepareSession(GameResourceRepository repository,
        int rowId, CollectionEventSessionInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.AppRandom); ArgumentNullException.ThrowIfNull(inputs.CrtRandom);
        ArgumentNullException.ThrowIfNull(inputs.Audio); ArgumentNullException.ThrowIfNull(inputs.VoiceSettings);
        ArgumentNullException.ThrowIfNull(inputs.Options);
        var row = Row(rowId);
        LoadedScenario? scenario = null; LoadedMap? map = null;
        ArmyCampaignRuleAdapter? rules = null; GameState? world = null;
        CollectionEventConditions conditions = new(rowId, [], "collection.event.selected_world");
        try
        {
            var plan = ReadSelectionPlan(rowId);
            //337860(0) does not select the title scenario or an arbitrary
            //campaign. Its native empty filename is a failed source load.
            if (plan.ScenarioId == 0 || plan.ScenarioPath is null)
                throw new InvalidDataException("collection.event.setup_scenario_mapping");
            var selected = repository.EffectiveScenarios.SingleOrDefault(candidate =>
                candidate.Locale == Locale && candidate.Number == plan.ScenarioId && candidate.SourcePath == plan.ScenarioPath)
                ?? throw new InvalidDataException("collection.event.selected_scenario_resource");
            scenario = repository.LoadScenario(selected);
            map = repository.LoadMapForScenario(selected);
            var initial = ScenarioInitializer.Create(scenario, map, inputs.RandomSeed, inputs.Options).State;
            rules = ArmyCampaignRuleAdapter.FromOwnedCampaign(repository, scenario, map,
                Locale, SimulationRuleIdentity.ForLocale(Locale));
            rules.ConfigurePersonnelNarrationVoice(inputs.VoiceSettings);
            initial = rules.CreateFreshScenarioMapObjects(initial);
            var started = PlayableSkeleton.Start(initial) with
                { CampaignAppRandom32 = inputs.AppRandom, CampaignCrtRandom = inputs.CrtRandom };
            world = rules.CreateFreshOwnedSimulation(started);
            world = MapSeasonRuntime.CreateForFreshCampaign(repository, world, MapSeasonNativeSource.Load(string.Empty, Locale));
            world = rules.ApplyCollectionEventWorldSetup(world, plan);
            conditions = rules.QueryCollectionEventConditions(world, this, rowId);
            int? playerForce = OwnedCurrentPlayerForce.ReadRawForce3A(world);
            EventStorySession? session = row.CanPlay ? EventStorySession.CreateLoaded(events,
                world, rules, plan.ScriptEventIds, plan.ExecutionMode, plan.VariantFlag, inputs.Audio) : null;
            return new(rowId, scenario, map, world, rules, session, playerForce, conditions, null);
        }
        catch (InvalidDataException error)
        {
            return new(rowId, scenario, map, world, rules, null,
                world is null ? null : OwnedCurrentPlayerForce.ReadRawForce3A(world),
                conditions with { UnresolvedOwner = error.Message }, error.Message);
        }
    }

    internal CollectionEventRow ReadRow(int rowId) => Row(rowId);
    internal S14EventConditionProgram ReadConditionProgram(int eventId)
    {
        if (selectedConditionPrograms.TryGetValue(eventId, out var cached)) return cached;
        long slot = (long)eventId - events.Document.NativeBaseId;
        if (slot < 0 || slot >= events.Document.ConditionStreams.Count)
            throw new InvalidDataException("collection.event.condition_directory");
        var program = S14EventConditionProgram.Read(events.Document.ConditionStreams[(int)slot]);
        selectedConditionPrograms.Add(eventId, program);
        return program;
    }
    internal string ReadEventName(int eventId) => Data(3, checked(eventId * 4 + 1));
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One captured current for all source condition rows. Display
    /// formatting consumes current fields without creating a campaign command.</summary>
    public CollectionEventConditions QueryCollectionEventConditions(GameState world,
        CollectionEventCatalog catalog, int rowId)
    {
        var row = catalog.ReadRow(rowId);
        try
        {
            var current = CaptureCurrentCampaignEventQueries(world);
            var program = catalog.ReadConditionProgram(row.EventId);
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            DeploymentNarrationCurrentPeople People(int id, ushort? city = null) => new(world,
                current, abilities, id, null,
                panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(world, force, effect),
                cityArgument: city, appearanceRepository: _repository,
                appearanceScenario: _scenario, appearanceLanguage: Seed.Language);
            int Person(S14ConditionInstruction operation, int selector)
            {
                var pointer = selector == -4 ? current.ReadConditionCurrentPlayerPerson() : selector >= 0
                    ? selector <= 6000 ? current.ReadConditionPersonTableSlot(selector) : current.ReadConditionBasePerson()
                    : throw new InvalidDataException($"collection.event.conditions.person_selector_{selector}_operation_{operation.Index}");
                if (pointer?.Value is not { } person) throw new InvalidDataException("collection.event.conditions.person_pointer");
                return current.ReadConditionPersonCharacterId(person)
                    ?? throw new InvalidDataException("collection.event.conditions.person_identity");
            }
            int City(S14ConditionInstruction operation, int selector)
            {
                if (selector < -1)
                    throw new InvalidDataException($"collection.event.conditions.city_selector_{selector}_operation_{operation.Index}");
                var pointer = (selector == -1 ? current.ReadConditionCurrentPlayerCity() :
                    current.ReadConditionCityTableSlot(selector < 52 ? selector : 0))
                    ?? throw new InvalidDataException("collection.event.conditions.city_pointer");
                return pointer.PhysicalSlot ?? throw new InvalidDataException("collection.event.conditions.city_physical");
            }
            var queries = new CollectionEventConditionTextQueries(Person, City,
                (id, field) => People(id).PersonText(0, field),
                (id, field) => People(id).PersonValue(0, field),
                (city, field) => People(0, checked((ushort)city)).CityText(0, field),
                (city, field) => field == 0 ? city : throw new InvalidDataException("collection.event.conditions.city_value_field"),
                catalog.ReadEventName)
            { PersonName = id => world.Officers.GetValueOrDefault(id)?.Name
                    ?? throw new InvalidDataException("collection.event.conditions.person_name"),
                IsPersonValid = id => current.ReadPersonLookupReference(id)?.PhysicalSlot is int p
                ? current.ReadPersonNativeValidity(p) ?? throw new InvalidDataException("collection.event.conditions.person_validity")
                : throw new InvalidDataException("collection.event.conditions.person_pointer") };
            return CollectionEventConditionSource.Build(_repository, Seed.Language, row, program,
                ReadOrdinaryEventDispatchInputs(world).SettingPredicateValue != 0, queries);
        }
        catch (InvalidDataException error) { return new(rowId, [], error.Message); }
    }
}

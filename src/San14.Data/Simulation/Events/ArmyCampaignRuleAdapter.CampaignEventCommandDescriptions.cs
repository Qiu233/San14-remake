using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //Resource definitions belong to this detached catalogue publication. A
    //new query sees current overrides; there is no global or cross-world cache.
    private sealed record CampaignEventCommandTextResources(MessageDocument Data, int? ScenarioDirectory,
        Lazy<MessageDocument> Game, Lazy<LoadedForceNameDefinitions> ForceNames);

    private CampaignEventCommandTextResources PrepareCampaignEventCommandTextResourcesCurrent(GameState state)
    {
        int? directory = null;
        if (OccupationKnown(ReadApplicationInformationRestriction(state), "campaign_event_command_selected_user"))
        {
            int raw = state.CampaignEventWorld?.ScenarioId ?? throw OccupationMissing("campaign_event_command_scenario");
            int scenario = raw > 99 ? raw - 100 : -1;
            if (scenario > 49) scenario -= 45;
            directory = scenario;
        }
        return new(_repository.LoadMessages(Seed.Language, MessageKind.Data).Messages, directory,
            new(() => _repository.LoadMessages(Seed.Language, MessageKind.Game).Messages),
            new(() => _repository.LoadForceNameDefinitions(_scenario.FixedDataSource.SourcePath)));
    }

    private static string ReadCampaignEventCommandText(CampaignEventCommandTextResources messages, int eventId, int field)
    {
        int index = messages.ScenarioDirectory is int scenario
            ? checked(scenario * 200 + 0xFA4 + eventId * 4 + field)
            : checked(eventId * 4 + field);
        return ReadCampaignEventCommandData(messages.Data, index);
    }

    private static IReadOnlyList<string> ReadCampaignEventCommandResultLines(CampaignEventCommandTextResources messages, int eventId)
    {
        bool special = messages.ScenarioDirectory is not null;
        string[] parts = [ReadCampaignEventCommandText(messages, eventId, 3),
            special ? "" : ReadCampaignEventCommandData(messages.Data, checked(0x1F49 + eventId * 2)),
            ReadCampaignEventCommandText(messages, eventId, 4),
            special ? "" : ReadCampaignEventCommandData(messages.Data, checked(0x1F4A + eventId * 2))];
        return parts.SelectMany(text => text.Split('\n')).Where(line => line.Length != 0).ToArray();
    }

    private static string ReadCampaignEventCommandData(MessageDocument messages, int index)
    {
        if (messages.Sections.Count <= 3) throw OccupationMissing("campaign_event_command_data_section3");
        var rows = messages.Sections[3].Records;
        //Native Data lookup returns its existing empty string outside the table.
        return (uint)index < (uint)rows.Count ? rows[index].Text
            ?? throw OccupationMissing("campaign_event_command_data_text") : string.Empty;
    }

    private CampaignEventConditionDescription ReadCampaignEventCommandDescriptionSafeCurrent(GameState state,
        SourceWorldContext current, CampaignEventCommandTextResources messages, int eventId, S14EventEligibilityResult? result)
    {
        try
        {
            var document = _campaignEvents.Value.Document;
            long slot = (long)eventId - document.NativeBaseId;
            var stream = slot >= 0 && slot < document.ConditionStreams.Count
                ? document.ConditionStreams[(int)slot] : new S14EventStream(0, 0, 0, []);
            var program = S14EventConditionProgram.Read(stream);
            var runtime = new S14PlayerEventConditionRuntime(current, _ => ReadOrdinaryEventDispatchInputs(state), current);
            //The source uses a fresh query-only final selector context for
            //wording, including already-occurred single events. This does not
            //replace the eligibility callback bitmap or execute story business.
            _ = S14EventConditionEvaluator.Evaluate(program, runtime);
            return ReadCampaignEventCommandDescriptionCurrent(state, current, messages, eventId, program, runtime, result);
        }
        catch (InvalidDataException error) { return new(eventId, [], [error.Message]); }
    }

    private CampaignEventConditionDescription ReadCampaignEventCommandDescriptionCurrent(GameState state,
        SourceWorldContext current, CampaignEventCommandTextResources messages, int eventId, S14EventConditionProgram program,
        S14PlayerEventConditionRuntime runtime, S14EventEligibilityResult? result)
    {
        //The operation bitmap belongs to the actual eligibility evaluation;
        //wording alone consumes the separately prepared final selector context.
        var passed = new bool?[program.Operations.Count];
        if (result is not null)
        {
            Array.Fill(passed, false);
            foreach (var callback in result.ConditionResult?.Callbacks ?? []) passed[callback.OperationIndex] = callback.Result != 0;
        }
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        DeploymentNarrationCurrentPeople People(int id, ushort? city = null) => new(state,
            current, abilities, id, null,
            panelReader: (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect),
            cityArgument: city, appearanceRepository: _repository,
            appearanceScenario: _scenario, appearanceLanguage: Seed.Language,
            appearanceDataSource: messages.Data, appearanceNamesSource: messages.ForceNames.Value);
        string PersonName(int id) => state.Officers.GetValueOrDefault(id)?.Name
            ?? throw OccupationMissing("campaign_event_command_person_name");
        int Person(S14ConditionInstruction operation, int selector)
        {
            var person = runtime.ReadCommandTextPerson(selector)
                ?? throw OccupationMissing("campaign_event_command_person_pointer");
            return OccupationKnown(current.ReadConditionPersonCharacterId(person), "campaign_event_command_person_identity");
        }
        int City(S14ConditionInstruction operation, int selector) => runtime.ReadCommandTextCity(selector).PhysicalSlot
            ?? throw OccupationMissing("campaign_event_command_city_pointer");
        string ForceName(S14ConditionInstruction operation, int selector)
        {
            var person = runtime.ReadCommandTextPerson(selector);
            if (person is null || !OccupationKnown(current.ReadConditionPersonValidity(person), "campaign_event_command_person_validity"))
                return string.Empty;
            int id = OccupationKnown(current.ReadConditionPersonCharacterId(person), "campaign_event_command_person_identity");
            if (OccupationKnown(current.ReadConditionPersonStatus(person), "campaign_event_command_person_status") == 1)
            {
                var lookup = current.ReadConditionPersonForce(person)
                    ?? throw OccupationMissing("campaign_event_command_force_pointer");
                if (lookup.Value is { } force && OccupationKnown(current.ReadConditionForceValidity(force), "campaign_event_command_force_validity"))
                {
                    var reference = current.ReadForceTableReference(force.PhysicalSlot)
                        ?? throw OccupationMissing("campaign_event_command_force_reference");
                    return new CampaignHistoryWriter(_repository, _scenario, Seed.Language).FallbackName(state, current, reference,
                        (owner, full) => full ? owner.Name : owner.CurrentSurname
                            ?? throw OccupationMissing("campaign_event_command_person_surname"), messages.Data);
                }
            }
            return PersonName(id);
        }
        var queries = new CollectionEventConditionTextQueries(Person, City,
            (id, field) => People(id).PersonText(0, field), (id, field) => People(id).PersonValue(0, field),
            (city, field) => People(0, checked((ushort)city)).CityText(0, field),
            (city, field) => field == 0 ? city : throw OccupationMissing("campaign_event_command_city_value_field"),
            id => ReadCampaignEventCommandText(messages, id, 1))
        {
            PersonName = PersonName,
            IsPersonValid = id => current.ReadPersonLookupReference(id)?.PhysicalSlot is int physical
                ? OccupationKnown(current.ReadPersonNativeValidity(physical), "campaign_event_command_person_validity")
                : throw OccupationMissing("campaign_event_command_person_pointer"),
            ResolveBaseId = (operation, selector) => OccupationKnown(current.ReadConditionBaseObjectId(runtime.ReadCommandTextBase(selector)),
                "campaign_event_command_base_identity"),
            ForceName = ForceName,
            NationalTitleName = id => messages.ForceNames.Value.Catalog.Definitions[id].ShortName,
            DoctrineName = id => state.Doctrines.GetValueOrDefault(id)?.Name ?? throw OccupationMissing("campaign_event_command_doctrine_name"),
            RankName = id => state.ForceRanks.GetValueOrDefault(id)?.Name ?? throw OccupationMissing("campaign_event_command_rank_name"),
            RegionName = id => _growthQueries.Definitions.Catalog.RegionNames[id],
            ProvinceName = id => S14ForceNameDefinitionReader.ReadLiteralUnits(_growthQueries.Definitions.Catalog.States[id].RawBytes.AsSpan(24, 6))
        };
        return CollectionEventConditionSource.BuildCurrent(_repository, Seed.Language, eventId, program,
            ReadOrdinaryEventDispatchInputs(state).SettingPredicateValue != 0, passed, queries, messages.Data, messages.Game.Value);
    }
}

using System.Collections.Immutable;
using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record NormalTurnStoryResultPreparation(GameState State,
    NormalTurnStoryResultSummary? Summary, CampaignStoryOutcomes Outcomes, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>3DAE20 publishes the actual invocation occurrences once. It does
    /// not execute event business, infer occurrences or acknowledge this page.</summary>
    internal NormalTurnStoryResultPreparation PrepareNormalTurnStoryResultCurrent(GameState state,
        SourceWorldContext current, EventSourceReference source, CampaignStoryOutcomes outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        try
        {
            var loaded = _campaignEvents.Value;
            if (source.ResourceUri != loaded.Source.ResourceUri || source.ResourceSha256 != loaded.Source.Sha256 ||
                !loaded.Document.Events.Any(value => value.Id == source.EventId))
                throw NormalResultMissing("event_source");
            var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            var body = new StringBuilder();

            foreach (int person in outcomes.RaisedArmyPersonSlots)
                Line(0x87EB, new(Person0: PersonId(person), Base0: NormalResultNeed(current.ReadPersonFirstLocation(person), "raised_PersonE2")));
            Pairs(outcomes.AlliancesCreated, 0x87E3);
            Pairs(outcomes.AlliancesExtended, 0x87E4);
            Pairs(outcomes.AlliancesCancelled, 0x87E5);
            People(outcomes.DeathPersonSlots, 0x87D5, 0x87D8);
            People(outcomes.FreePersonSlots, 0x87D6, 0x87D9);
            Groups(outcomes.CapturedByForce, 0x87E1, 0x87E2, showEmpty: true);
            Groups(outcomes.JoinedByForce, 0x87D7, 0x87DA, showEmpty: false);
            foreach (var receiver in outcomes.TroopIncreasedBases)
                Line(0x87E6, new(Base0: NormalResultNeed(current.ReadBaseObjectLink(
                    new(receiver.IsGate ? BaseKind.Gate : BaseKind.City, receiver.PhysicalSlot)), "troops_Base4E")));
            foreach (var change in outcomes.StatusChanges)
            {
                if (!NormalResultNeed(current.ReadPersonNativeValidity(change.PersonPhysicalSlot), "status_Person_validity") || change.Status == 0xFF) continue;
                string label = change.Status is >= 0 and < 4 ? ReadHistoryDataText(0, 0x123 + change.Status) : string.Empty;
                Line(0x87ED, new(Person0: PersonId(change.PersonPhysicalSlot), String0: label));
            }
            People(outcomes.MeritPersonSlots, 0x87E7, 0x87E8);
            foreach (var group in outcomes.ItemsGainedByPerson)
            {
                if (!NormalResultNeed(current.ReadPersonNativeValidity(group.PersonPhysicalSlot), "item_Person_validity")) continue;
                foreach (int item in group.ItemPhysicalSlots)
                    Line(0x87EC, new(Person0: PersonId(group.PersonPhysicalSlot)), item);
            }
            var consumed = outcomes;
            if (outcomes.CurrentPlayerCityIncreaseKind is >= 1 and <= 3)
            {
                //32EFA0 returns the actual current Force, then its ruler lookup.
                //PersonE2 is this model's first/Home location; E4 is independent.
                var force = current.ReadCurrentPlayerForce() ?? throw NormalResultMissing("player_Force");
                if (NormalResultNeed(current.ReadForceNativeValidity(force), "player_Force_validity"))
                {
                    int forceSlot = NormalResultNeed(current.ReadForcePhysicalSlot(force), "player_Force_identity");
                    ushort ruler = NormalResultNeed(current.ReadForceRecord(forceSlot)?.RawRuler10, "player_Force_ruler10");
                    var person = (ruler <= 6000 ? current.ReadPersonLookupReference(ruler) : current.ReadTransferPersonTableReference(0))
                        ?? throw NormalResultMissing("player_Person_lookup");
                    if (person.PhysicalSlot is int physical && NormalResultNeed(current.ReadPersonNativeValidity(physical), "player_Person_validity"))
                    {
                        var location = BaseLookup.Find(NormalResultNeed(current.ReadPersonFirstLocation(physical), "player_PersonE2"), current);
                        if (NormalResultNeed(current.ReadBaseNativeValidity(location), "player_Base_validity"))
                        {
                            var city = CurrentGateMapQueries.FindCity(location, current);
                            if (NormalResultNeed(current.ReadBaseNativeValidity(city), "player_City_validity"))
                                Line(outcomes.CurrentPlayerCityIncreaseKind switch { 1 => 0x87E9, 2 => 0x87EA, _ => 0x87E6 },
                                    new(Base0: NormalResultNeed(current.ReadBaseObjectLink(city), "player_City4E")));
                        }
                    }
                }
                //The recognized-kind branch consumes this notice even when a
                //native-invalid receiver prevented a visible resource line.
                consumed = outcomes with { CurrentPlayerCityIncreaseKind = 0 };
            }
            var resourceText = ReadNormalTurnResultResourceText(state, source.EventId);
            var complete = new StringBuilder();
            if (resourceText.Description.Length != 0)
            { NormalResultAppend(complete, resourceText.Description); NormalResultAppend(complete, "\n"); }
            if (resourceText.Supplement.Length != 0)
            { NormalResultAppend(complete, resourceText.Supplement); NormalResultAppend(complete, "\n"); }
            NormalResultAppend(complete, body.ToString());
            return new(state, new(Seed.Language, resourceText.Title, complete.ToString()), consumed, null);

            int PersonId(int physical) => NormalResultNeed(current.ReadPersonIdentity(physical), "Person10");
            int ForceId(int physical) => NormalResultNeed(current.ReadForcePhysicalSlot(new(physical)), "Force_virtual60");
            bool ForceValid(int physical) => NormalResultNeed(current.ReadForceNativeValidity(new(physical)), "Force_validity");
            void Pairs(ImmutableArray<CampaignStoryForcePair> pairs, int root)
            {
                foreach (var pair in pairs)
                    if (ForceValid(pair.FirstForcePhysicalSlot) && ForceValid(pair.SecondForcePhysicalSlot))
                        Line(root, new(Force0: ForceId(pair.FirstForcePhysicalSlot), Force1: ForceId(pair.SecondForcePhysicalSlot)));
            }
            void Groups(ImmutableArray<CampaignStoryForcePeople> groups, int single, int multiple, bool showEmpty)
            {
                foreach (var group in groups)
                {
                    if (!ForceValid(group.ForcePhysicalSlot) || group.PersonPhysicalSlots.Length == 0 && !showEmpty) continue;
                    People(group.PersonPhysicalSlots, single, multiple, ForceId(group.ForcePhysicalSlot), showEmpty);
                }
            }
            void People(ImmutableArray<int> people, int single, int multiple, int? force = null, bool showEmpty = false)
            {
                if (people.Length == 0 && !showEmpty) return;
                if (people.Length == 1) { Line(single, new(Person0: PersonId(people[0]), Force0: force)); return; }
                Line(multiple, new(Force0: force));
                if (people.Length < 2) return;
                int[] first = people.Take(5).Select(PersonId).ToArray();
                int root = people.Length switch { 2 => 0x87DB, 3 => 0x87DC, 4 => 0x87DD, 5 => 0x87DE, 6 => 0x87DF, _ => 0x87E0 };
                Line(root, new(Person0: first[0], Person1: first[1], Person2: first.Length > 2 ? first[2] : null,
                    Person3: first.Length > 3 ? first[3] : null, Person4: first.Length > 4 ? first[4] : null,
                    Integers: people.Length > 5 ? [people.Length - 5] : null));
            }
            void Line(int root, HistoryMessageOperands arguments, int? item = null)
            {
                if (body.Length != 0) NormalResultAppend(body, "\n");
                var persons = new DeploymentNarrationCurrentPeople(state, current, abilities,
                    arguments.Person0, arguments.Person1, cityArgument: arguments.Base0,
                    appearanceRepository: _repository, appearanceScenario: _scenario, appearanceLanguage: Seed.Language,
                    forceArgument: arguments.Force0, personArgument2: arguments.Person2,
                    personArgument3: arguments.Person3, forceArgument1: arguments.Force1, personArgument4: arguments.Person4);
                var report = new ReportCurrent(this, () => state, current, persons, arguments);
                var items = new StoryItemMessageCurrent(this, () => state, current, item is int value ? [value] : null);
                var text = new NormalResultMessageCurrent(this, state, current, report, items, arguments);
                NormalResultAppend(body, vm.ResolveNormalTurnEventResult(text, root));
            }
        }
        catch (InvalidDataException error) { return new(state, null, outcomes, error.Message); }
    }

    private static void NormalResultAppend(StringBuilder buffer, string text)
    {
        //The native8192-byte UTF16 buffer reserves its terminating unit.
        //Each append is all-or-nothing; its preceding LF is a separate append.
        if (buffer.Length + text.Length < 4096) buffer.Append(text);
    }
    private static T NormalResultNeed<T>(T? value, string field) where T : struct => value ?? throw NormalResultMissing(field);
    private static InvalidDataException NormalResultMissing(string field) => new("normal_event.result." + field);
}

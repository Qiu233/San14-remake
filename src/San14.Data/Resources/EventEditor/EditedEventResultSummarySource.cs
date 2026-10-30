using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>399FB0's edited-definition result page. Ordinary event-manager
    /// success lists have a different producer and are not its input.</summary>
    internal EditedEventPlaybackCompletionResult ApplyEditedEventResultsAndPrepareSummaryCurrent(
        GameState candidate, EditedEventCompiledDefinition definition,
        CampaignStorySelectionContext? selections = null)
    {
        EditedEventResultExecution.RequireImplemented(definition.ResultsProgram.DispatchProgram);
        var current = CaptureCurrentCampaignEventQueries(candidate);
        var application = ApplyEditedEventResultProgramCurrent(candidate, current, definition.ResultsProgram,
            _ => ReadOrdinaryEventDispatchInputs(candidate), selections);
        if (application.IsPaused) return new(application.State, null) { PendingBusiness = true };
        //The split presentation/results APIs still form one83C100 execution.
        //Its unconditional History+10 reset follows the last business leaf,
        //before either the summary page or the return-to-title branch.
        var completed = CampaignHistoryPlaybackControl.Exit(application.State);
        bool returning = ReadApplicationReturnToTitlePending() ?? throw SummaryMissing("return_to_title");
        return returning ? new(completed, null) :
            PrepareEditedEventResultSummaryCurrent(completed, definition, current);
    }

    private EditedEventPlaybackCompletionResult PrepareEditedEventResultSummaryCurrent(
        GameState state, EditedEventCompiledDefinition definition, SourceWorldContext current)
    {
        var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
        var instructions = definition.Definition.Results.Instructions;
        var body = new StringBuilder();
        int count = 0;
        foreach (var instruction in instructions)
        {
            if (body.Length + 1 > 2048) break;
            if (body.Length != 0) body.Append('\n');
            string line = Describe(instruction);
            if (body.Length + line.Length > 2048) break;
            count++;
            if (instructions.Length > 10 && count > 9)
            { body.Append(Text(0x28E4, new())); break; }
            body.Append(line);
        }
        var summary = new EditedEventResultSummary(Seed.Language, definition.Definition.Name,
            body.ToString(), Text(0x28E3, new()));
        return new(state, null) { Summary = summary };

        string Text(int root, HistoryMessageOperands arguments)
        {
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            var people = new DeploymentNarrationCurrentPeople(state, current, abilities,
                arguments.Person0, arguments.Person1, cityArgument: arguments.Base0,
                appearanceRepository: _repository, appearanceScenario: _scenario,
                appearanceLanguage: Seed.Language, personArgument2: arguments.Person2);
            return vm.ResolveEditedEventResultSummary(new ReportCurrent(this, () => state, current, people, arguments), root);
        }
        string Describe(EditedEventInstruction row)
        {
            var a = row.Arguments;
            return row.Code switch
            {
                388 => Text(0x87FC, new(String0: ForceName(a[0]), String1: ForceName(a[1]))),
                389 => a[2] is >= 1 and <= 100 && a[2] is not (>= 38 and <= 43)
                    ? Text(0x8844, new(Person0: a[0], Person1: a[1],
                        String2: ReadEmpireNameResourcesCurrent().CustomNames.Definitions[a[2]].ShortName))
                    : Text(0x87FD, new(Person0: a[0], Person1: a[1])),
                391 => Text(0x87FF, new(String0: ForceName(a[0]), Integers: [a[1]], String2: Mode(a[2]))),
                392 => Text(0x8800, new(String0: ForceName(a[0]), String1: ForceName(a[1]), String2: Friendship(a[2]))),
                393 => Text(0x8801, new(String0: ForceName(a[0]), String1: ForceName(a[1]), Integers: [a[2]])),
                394 => Text(0x8802, new(String0: ForceName(a[0]), String1: ForceName(a[1]))),
                395 or 396 or 397 or 398 => Text(0x8803 + row.Code - 395,
                    new(Base0: Base(a[0]), Integers: [a[1]], String2: Mode(a[2]))),
                399 => Text(0x8807, new(Base0: Base(a[0] > 51 ? 0 : a[0]), String0: Size(a[1]))),
                400 => Text(0x8808, new(Person0: a[0], String0: ForceName(a[1]))),
                401 => Text(0x8809, new(Base0: Base(a[0]), Person0: a[1])),
                403 => Text(0x880B, new(Person0: a[0], String0: Field(a[1]), String1: Mode(a[2]), Integers: [a[3]])),
                404 => Text(0x880C, new(Person0: a[0], Integers: [a[1]])),
                405 => Text(0x880E, new(Person0: a[0])),
                407 => Text(0x8810, new(Person0: a[0], Person1: a[1])),
                408 => Text(0x8811, new(Person0: a[0], String0: a[1] is >= 0 and < 4 ? ReadHistoryDataText(0, 0x123 + a[1]) : "")),
                409 => Text(0x8812, new(Person0: a[0], Person1: a[1])),
                419 => Sworn(a),
                429 => Text(0x884C, new(Person0: a[0], Integers: [a[1]])),
                //383420 has no formatter for structural/unknown statements;
                //the freshly cleared definition buffer remains empty.
                _ => string.Empty
            };
        }
        string Sworn(System.Collections.Immutable.ImmutableArray<int> arguments)
        {
            int[] people = arguments.Take(3).Where(id => id != 0).ToArray();
            return people.Length switch
            {
                2 => Text(0x884A, new(Person0: people[0], Person1: people[1])),
                3 => Text(0x884B, new(Person0: people[0], Person1: people[1], Person2: people[2])),
                _ => string.Empty
            };
        }
        string Mode(int mode) => mode switch
        {
            0 => ReadHistoryDataText(1, 0x5D9), 1 => ReadHistoryDataText(1, 0x7B6),
            2 => ReadHistoryDataText(1, 0x24C), 3 => ReadHistoryDataText(1, 0x24D), _ => ""
        };
        string Field(int kind) => kind switch
        {
            >= 1 and <= 5 => ReadHistoryDataText(0, 0x182 + kind),
            6 => ReadHistoryDataText(1, 0x21D), _ => ""
        };
        string Size(int size)
        {
            int[] words = [0x46C, 0x46C, 0x46D, 0x46E, 0x46F];
            return (uint)size < (uint)words.Length ? ReadHistoryDataText(1, words[size]) :
                throw SummaryMissing("size_definition");
        }
        string Friendship(int value)
        {
            int level = 0;
            for (int candidate = 1; candidate <= 5; candidate++)
            {
                int threshold = SummaryNeed(ArmyDestructionParameterSeed.ReadCurrent(state,
                    SimulationParameterKeys.ForceRelations.LevelThreshold(candidate)), "friendship_threshold");
                if (value < threshold) break;
                level = candidate;
            }
            return ReadHistoryDataText(0, 0x141 + level);
        }
        string ForceName(int selector)
        {
            if (selector < 0) throw SummaryMissing("definition_person_selector");
            var pointer = (selector <= 6000 ? current.ReadPersonLookupReference(selector) : current.ReadTransferPersonTableReference(0))
                ?? throw SummaryMissing("definition_person_pointer");
            int physical = SummaryNeed(pointer.PhysicalSlot, "definition_person_physical");
            if (!SummaryNeed(current.ReadPersonNativeValidity(physical), "definition_person_validity")) return "";
            int id = SummaryNeed(current.ReadPersonIdentity(physical), "definition_person_identity");
            var person = state.Officers.GetValueOrDefault(id) ?? throw SummaryMissing("definition_person_owner");
            ushort home = SummaryNeed(current.ReadPersonFirstLocation(physical), "definition_person_home");
            bool special = home is >= 1 and <= 61 && DuelAftermathForce(current, physical) is >= 46 and <= 50;
            //309640(...,0) uses this PERSON, not a Force's current ruler.
            //Its normal branch appends the actual army suffix without color;
            //the template itself supplies the force-name color escape.
            string name = special && id is not (>= 5101 and <= 5200) && id is not (>= 2062 and <= 2066)
                ? person.CurrentSurname ?? throw SummaryMissing("definition_person_surname") : person.Name;
            return HeadHistory(name) + (special ? "" : HeadHistory(ReadHistoryDataText(1, 0x34F)));
        }
        static ushort Base(int value) => value is >= 0 and <= ushort.MaxValue ? (ushort)value :
            throw SummaryMissing("definition_base_selector");
    }

    private static T SummaryNeed<T>(T? value, string field) where T : struct => value ?? throw SummaryMissing(field);
    private static InvalidDataException SummaryMissing(string field) => new("edited_event.result_summary." + field);
}

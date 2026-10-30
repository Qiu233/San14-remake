using San14.Data.Formats;

namespace San14.Data;

public sealed record CollectionEventConditionLine(string Text, int IndentPixels,
    bool ShowBox, bool ShowCheck, int TextPreset = 0);

public sealed record CollectionEventConditions(int RowId,
    IReadOnlyList<CollectionEventConditionLine> Lines, string? UnresolvedOwner);

internal sealed record CollectionEventConditionTextQueries(
    Func<S14ConditionInstruction, int, int> ResolvePersonId,
    Func<S14ConditionInstruction, int, int> ResolveCityId,
    Func<int, int, string> PersonText,
    Func<int, int, int> PersonValue,
    Func<int, int, string> CityText,
    Func<int, int, int> CityValue,
    Func<int, string> EventName)
{
    internal Func<int, bool>? IsPersonValid { get; init; }
    internal Func<int, string>? PersonName { get; init; }
    internal Func<S14ConditionInstruction, int, string>? ForceName { get; init; }
    internal Func<S14ConditionInstruction, int, int>? ResolveBaseId { get; init; }
    internal Func<int, string>? NationalTitleName { get; init; }
    internal Func<int, string>? DoctrineName { get; init; }
    internal Func<int, string>? RankName { get; init; }
    internal Func<int, string>? RegionName { get; init; }
    internal Func<int, string>? ProvinceName { get; init; }
}

/// <summary>The Collection condition-list consumer. The caller supplies the
/// selected event's prepared selector context; this does not capture a world,
/// execute conditions again, or substitute their result for profile progress.</summary>
internal static partial class CollectionEventConditionSource
{
    internal static CollectionEventConditions Build(GameResourceRepository repository,
        string locale, CollectionEventRow row, S14EventConditionProgram program,
        bool? alternateDispatchMode, CollectionEventConditionTextQueries queries)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(queries);
        var messages = repository.LoadMessages(locale, MessageKind.Data).Messages;
        var formatter = new NarrationResourceMessages(repository.LoadMessages(locale, MessageKind.Game).Messages);
        var lines = new List<CollectionEventConditionLine>();
        string? unresolved = null;
        int depth = 0;

        string Data(int section, int index)
        {
            if ((uint)section >= (uint)messages.Sections.Count ||
                (uint)index >= (uint)messages.Sections[section].Records.Count ||
                messages.Sections[section].Records[index].Text is not string text)
                throw Missing($"data.{section}.{index:X}");
            return text;
        }

        foreach (var operation in program.Operations)
        {
            try
            {
                bool admitted = operation.RawField04 switch
                {
                    1 => alternateDispatchMode ?? throw Missing("setting_predicate"),
                    2 => !(alternateDispatchMode ?? throw Missing("setting_predicate")),
                    _ => true
                };
                if (!admitted || operation.RawField06 != 0) continue;
                int code = operation.Code;
                depth += code switch
                {
                    3 or 6 or 8 => -1,
                    4 => -2,
                    225 or 229 or 231 or 373 or 374 => 1,
                    _ => 0
                };
                if (code is not (4 or 6 or 8 or 257 or 379))
                {
                    var arguments = new Arguments(queries);
                    int root = Prepare(operation, arguments, queries, Data);
                    string text = root == 0 ? string.Empty :
                        formatter.ResolveCollectionEventCondition(arguments, root);
                    bool first = true;
                    foreach (string line in text.Split('\n'))
                    {
                        if (line.Length == 0) continue;
                        bool showBox = first && code is not (225 or 229 or 231 or 369 or 373 or 374);
                        if (showBox && row.Unlocked is null)
                            unresolved ??= "collection.event.conditions.profile_progress";
                        // The native list stores indentation as a byte. Its
                        // multiplier is the original 28-pixel spacing.
                        lines.Add(new(line, unchecked((byte)depth) * 28,
                            showBox, showBox && row.Unlocked == true));
                        first = false;
                    }
                }
                depth += code switch
                {
                    1 or 2 or 3 or 5 or 7 => 1,
                    225 or 229 or 231 or 373 or 374 => -1,
                    _ => 0
                };
            }
            catch (InvalidDataException error)
            {
                // Keep already formatted rows. Unknown operations are not
                // rendered as fake condition descriptions or check results.
                unresolved ??= $"collection.event.conditions.operation_{operation.Index}_code_{operation.Code}:{error.Message}";
                break;
            }
        }
        if (row.EventId >= 950)
        {
            // The original ending condition panel masks the description even
            // when its event has been seen. Preserve the actual first-row
            // decoration only when its formatter established that row.
            if (lines.Count > 0)
            {
                var first = lines[0] with { Text = Data(1, 0x7DB) };
                lines.Clear(); lines.Add(first);
            }
            else unresolved ??= "collection.event.conditions.ending_first_row";
        }
        return new(row.RowId, Array.AsReadOnly(lines.ToArray()), unresolved);
    }

    private static int Prepare(S14ConditionInstruction operation, Arguments output,
        CollectionEventConditionTextQueries queries, Func<int, int, string> data)
    {
        var args = operation.RawArguments;
        if (args.Count != 8) throw Missing("arguments");
        string Table(int value, int[] indices)
        {
            if ((uint)value >= (uint)indices.Length) throw Missing("comparison_mode");
            return indices[value] < 0 ? string.Empty : data(1, indices[value]);
        }
        string Numeric(int value) => Table(value, [-1, 0x4F4, 0x4F5, 0x4F6, 0x4F7, 0x4F8, 0x4F9]);
        string Equality(int value) => Table(value, [-1, 0x4F4, 0x4F5, -1, -1, -1, -1]);
        string Boolean(int value) => Table(value, [0x4F2, 0x4F3]);
        int Person(int selector) => queries.ResolvePersonId(operation, selector);
        int City(int selector) => queries.ResolveCityId(operation, selector);
        switch (operation.Code)
        {
            case 0: return 0;
            case 5: return 0x873E;
            case 7: return 0x873F;
            case 9:
                output.Strings[0] = queries.EventName(args[0]);
                output.Strings[1] = Table(args[1], [0x4F0, 0x4F1]);
                return 0x8740;
            case 10:
                output.Strings[0] = queries.EventName(args[0]);
                output.Strings[1] = Numeric(args[1]); output.Integers[0] = args[2];
                return 0x8741;
            case 353:
                //The Scenario-ID formatter uses the boolean label first,
                //then Data1[scenario+0x747]. It describes the source operand;
                //the currently prepared world's title is not a substitute.
                output.Strings[0] = Boolean(args[1]);
                output.Strings[1] = data(1, checked(args[0] + 0x747));
                return 0x87B2;
            case 13 or 14:
                output.Strings[0] = Table(args[0], [-1, 0x4F4, 0x4F5, 0x4FC, 0x4FD, 0x4FA, 0x4FB]);
                output.Strings[1] = data(1, args[3] < 11 ? 0x2AD : args[3] <= 20 ? 0x2AE : 0x2AF);
                output.Integers[0] = args[1]; output.Integers[1] = args[2];
                return operation.Code == 13 ? 0x8743 : 0x8744;
            case 15:
                output.Strings[0] = Numeric(args[0]); output.Integers[0] = args[1];
                return 0x8745;
            case 16:
                output.Strings[0] = Boolean(args[0]); return 0x8746;
            case 17:
                output.Strings[0] = Equality(args[0]); output.People[0] = Person(args[1]);
                return 0x8747;
            case 19:
                output.Strings[0] = Equality(args[0]); output.Cities[0] = City(args[1]);
                return 0x8749;
            case 59:
            {
                output.People[0] = Person(args[0]);
                int count = 0;
                for (int index = 1; index < 6; index++)
                    if (args[index] is > 0 and < 10) output.Strings[count++] = data(0, 0xF9 + args[index]);
                return count == 0 ? 0 : 0x876E + count - 1;
            }
            case 66:
                output.People[0] = Person(args[0]); output.Strings[0] = Boolean(args[1]);
                return 0x877C;
            case 64:
            {
                var valid = queries.IsPersonValid ?? throw Missing("person_validity");
                var name = queries.PersonName ?? throw Missing("person_name");
                bool Accepted(int id) => id is >= 2001 and <= 2088 or >= 3001 and <= 3150 || valid(id);
                int count = 0;
                for (int index = 0; index < 5; index++)
                {
                    int id = Person(args[index]);
                    if (Accepted(id)) output.Strings[count++] = name(id);
                    else if (args[index] is -8 or -7 or -4)
                        output.Strings[count++] = data(1, args[index] == -8 ? 0x62F : args[index] == -7 ? 0x630 : 0x50D);
                }
                for (int index = 0; index < count; index++)
                    for (int other = index + 1; other < count; other++)
                        if (Person(args[index]) == Person(args[other]))
                        {
                            // These two source tests deliberately refer to
                            // the first and second raw arguments.
                            if (args[0] == -4) output.Strings[index] = data(1, 0x50D);
                            if (args[1] == -4) output.Strings[other] = data(1, 0x50D);
                        }
                int reference = Person(args[5]);
                if (Accepted(reference))
                    for (int index = 0; index < count; index++)
                        if (Person(args[index]) == reference && args[index] == -8)
                            output.Strings[index] = data(1, 0x62F);
                return count == 0 ? 0 : 0x8776 + count - 1;
            }
            case 75:
                output.People[0] = Person(args[0]);
                output.Strings[0] = args[1] is >= 1 and <= 5 ? data(0, 0x183 + args[1] - 1) :
                    args[1] == 6 ? data(1, 0x21D) : string.Empty;
                output.Strings[1] = Numeric(args[2]); output.Integers[0] = args[3];
                return 0x878C;
            case 244:
            {
                var valid = queries.IsPersonValid ?? throw Missing("person_validity");
                int count = 0;
                for (int index = 0; index < 5; index++)
                {
                    int person = Person(args[index]);
                    if (valid(person)) output.People[count++] = person;
                }
                return count == 0 ? 0 : 0x8799 + count - 1;
            }
            case 298:
            {
                int person = Person(args[0]);
                if (!(queries.IsPersonValid ?? throw Missing("person_validity"))(person)) return 0;
                output.People[0] = person;
                output.Strings[0] = Table(args[1], [0x6D5, 0x6D4]);
                return 0x87A3;
            }
            default: throw Missing($"formatter_code_{operation.Code}");
        }
    }

    private static InvalidDataException Missing(string field) => new($"collection.event.conditions.{field}");

    private sealed class Arguments(CollectionEventConditionTextQueries source) : INarrationCurrentContext
    {
        internal string? LiteralText { get; set; }
        internal string[] Strings { get; } = ["", "", "", "", ""];
        internal int[] Integers { get; } = new int[5];
        internal int[] People { get; } = new int[5];
        internal int[] Cities { get; } = new int[5];
        public string StringText(int argument, int field) => field is 0 or '2' ? Strings[argument] :
            field == '3' ? string.Empty : throw Missing("string_field");
        public int StringValue(int argument, int field) => field is 0 or '2' or '3' ? Strings[argument].Length :
            throw Missing("string_field");
        public int IntegerValue(int argument) => Integers[argument];
        public int PersonValue(int argument, int field) => source.PersonValue(People[argument], field);
        public string PersonText(int argument, int field) => source.PersonText(People[argument], field);
        public int CityValue(int argument, int field) => source.CityValue(Cities[argument], field);
        public string CityText(int argument, int field) => source.CityText(Cities[argument], field);
        public int Relationship(int field, int firstPersonId, int secondPersonId) => throw Missing("relationship");
        public string AreaText(int argument, int field) => throw Missing("area");
    }
}

internal sealed partial class NarrationResourceMessages
{
    internal string ResolveCollectionEventCondition(INarrationCurrentContext context, int root)
    {
        bool Allowed(int id) => id is 0x873E or 0x873F or 0x8740 or 0x8741 or 0x8743 or 0x8744 or
            0x8745 or 0x8746 or 0x8747 or 0x8749 or >= 0x876E and <= 0x8772 or
            >= 0x8776 and <= 0x877A or 0x877C or
            0x878C or >= 0x8799 and <= 0x879D or 0x87A3 or 0x87B2;
        if (!Allowed(root)) throw new InvalidDataException("collection.event.conditions.message_root");
        _prefix = "collection.event.conditions";
        bool previous = _report; _report = true;
        try
        {
            return ExecuteMessage(context, _ => throw Failure("unexpected_random"), root,
                Get(root, true), _ => false, true, null, Allowed,
                (group, code) => (group, code) switch
                {
                    ('C', 'J') => "^02", ('C', 'L') => "^05",
                    ('E', 'K') or ('E', 'M') => string.Empty,
                    _ => throw Failure($"unresolved_escape_{group:X2}_{code:X2}")
                }, true).Text;
        }
        finally { _report = previous; }
    }
}

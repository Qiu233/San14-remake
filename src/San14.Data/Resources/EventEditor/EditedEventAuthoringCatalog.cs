using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public enum EditedEventParameterKind
{
    Unknown, Integer, Boolean, Choice, Person, Force, City, Province,
    Scenery, Message, Still, Music, Sound, Movie, Foothold
}
public sealed record EditedEventParameterValue(int Value, string Caption);
public sealed record EditedEventParameterSpec(int ArgumentIndex, EditedEventParameterKind Kind,
    string Caption, int? SelectorKind, int? Minimum, int? Maximum, string? UnresolvedOwner = null)
{
    public IReadOnlyList<EditedEventParameterValue> Values { get; init; } = [];
}
public sealed record EditedEventAuthoringChoice(int Code, string Caption,
    IReadOnlyList<EditedEventParameterSpec> Parameters, EditedEventInstruction DefaultInstruction,
    string? UnresolvedOwner = null);
public sealed record EditedEventAuthoringDefaults(string Name, string Description, byte? Category,
    bool Enabled, bool Automatic, bool Repeat);

/// <summary>Normal editor choices and parameter bindings. The source-language
/// parser accepts additional operations which are deliberately absent here.
/// Unresolved selector data remains explicit instead of exposing raw opcodes.</summary>
public sealed partial class EditedEventAuthoringCatalog
{
    public IReadOnlyList<EditedEventAuthoringChoice> Conditions { get; }
    public IReadOnlyList<EditedEventAuthoringChoice> Results { get; }
    public IReadOnlyList<EditedEventAuthoringChoice> ContentOperations { get; }
    public static EditedEventAuthoringDefaults FreshDefaults { get; } =
        new("", "", null, false, false, false);

    //5FCD90 clears six statements then installs the actual editor insertion
    //defaults. This is more than the bare39D5D0 constructor's zero operands.
    public static EditedEventContent CreateDefaultContent() => new(new([
        EditedEventInstruction.Create(85, 0, 1), EditedEventInstruction.Create(410, 0, 13),
        EditedEventInstruction.Create(91, -2), EditedEventInstruction.Create(92, -2),
        EditedEventInstruction.Create(107), EditedEventInstruction.Create(385, 0, -1, 1)]));

    public static EditedEventAuthoringCatalog Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new EditedEventAuthoringCatalog(repository.LoadMessages(locale, MessageKind.Data)).BindResourceInventories(repository, locale);
    }
    private readonly string unknownText;
    private readonly IReadOnlyDictionary<int, string> groupEndCaptions;
    private readonly IReadOnlyDictionary<int, byte> personSexes;
    private EditedEventAuthoringCatalog(IReadOnlyList<EditedEventAuthoringChoice> conditions,
        IReadOnlyList<EditedEventAuthoringChoice> results,
        IReadOnlyList<EditedEventAuthoringChoice> contentOperations, string unknownText,
        IReadOnlyDictionary<int, string> groupEndCaptions, IReadOnlyDictionary<int, byte> personSexes)
    { Conditions = conditions; Results = results; ContentOperations = contentOperations; this.unknownText = unknownText;
        this.groupEndCaptions = groupEndCaptions; this.personSexes = personSexes; }

    /// <summary>500030 ordinary parameter selectors use only the actual selected
    /// code64 people. The caller supplies their current title-world names.</summary>
    public EditedEventAuthoringCatalog WithKeyPeople(IReadOnlyList<EditedEventParameterValue> people)
    {
        ArgumentNullException.ThrowIfNull(people);
        if (people.Count > 20 || people.Select(person => person.Value).Distinct().Count() != people.Count ||
            people.Any(person => person.Value is < 1 or > 6000 || string.IsNullOrEmpty(person.Caption)))
            throw new InvalidDataException("event_editor.key_people_inventory");
        var inventory = Array.AsReadOnly(people.ToArray());
        EditedEventAuthoringChoice Bind(EditedEventAuthoringChoice choice)
        {
            //59 and244 deliberately use the independent full person pool.
            if (choice.Code is 59 or 244) return choice;
            var parameters = choice.Parameters.Select(parameter =>
            {
                if (parameter.SelectorKind is not (9 or 10 or 15 or 22 or 23 or 41)) return parameter;
                var values = parameter.SelectorKind is 22 or 23
                    ? inventory.Where(person => personSexes.TryGetValue(person.Value, out byte sex) &&
                        (parameter.SelectorKind == 22 ? sex == 0 : sex != 0)).ToArray()
                    : inventory.ToArray();
                string? missing = parameter.SelectorKind is 22 or 23 && inventory.Any(person => !personSexes.ContainsKey(person.Value))
                    ? "event_editor.key_people_sex_inventory" : null;
                if (parameter.SelectorKind == 41) values = new[] { new EditedEventParameterValue(0, unknownText) }.Concat(values).ToArray();
                return parameter with { Values = Array.AsReadOnly(values), Minimum = null, Maximum = null,
                    UnresolvedOwner = missing ?? (values.Length == 0 ? "event_editor.key_people_empty" : null) };
            }).ToArray();
            return PrepareChoice(choice with { Parameters = Array.AsReadOnly(parameters),
                UnresolvedOwner = parameters.Select(parameter => parameter.UnresolvedOwner)
                    .FirstOrDefault(owner => owner is not null) });
        }
        return new(Array.AsReadOnly(Conditions.Select(Bind).ToArray()),
            Array.AsReadOnly(Results.Select(Bind).ToArray()), ContentOperations, unknownText, groupEndCaptions, personSexes);
    }

    public string DescribeParameterValue(EditedEventParameterSpec parameter, int value)
    {
        var selected = parameter.Values.FirstOrDefault(candidate => candidate.Value == value);
        if (selected is not null) return selected.Caption;
        return parameter.Kind == EditedEventParameterKind.Integer && parameter.UnresolvedOwner is null
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : unknownText;
    }
    public string DescribeInstruction(EditedEventInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        instruction.Validate();
        var choice = Conditions.Concat(Results).Concat(ContentOperations)
            .FirstOrDefault(candidate => candidate.Code == instruction.Code);
        if (choice is null) return groupEndCaptions.TryGetValue(instruction.Code, out string? ending) ? ending : unknownText;
        if (choice.Parameters.Count == 0) return choice.Caption;
        return choice.Caption + "  " + string.Join("  ", choice.Parameters.Select(parameter =>
            parameter.Caption + ": " + DescribeParameterValue(parameter, instruction.Arguments[parameter.ArgumentIndex])));
    }

    private EditedEventAuthoringCatalog(LoadedMessages data)
    {
        string Text(int section, int id) => data.Messages.Sections[section].Records[id].Text
            ?? throw new InvalidDataException($"event_editor.message.{section}.{id}");
        unknownText = Text(1, 0x353);
        groupEndCaptions = new Dictionary<int, string> { [6] = Text(0, 0x149D + 6), [8] = Text(0, 0x149D + 8) };
        personSexes = new Dictionary<int, byte>();
        string Operation(int code) => Text(0, checked(0x149D + code));
        EditedEventParameterSpec Parameter(int index, int selector)
        {
            var (kind, min, max, owner) = ParameterShape(selector);
            var parameter = new EditedEventParameterSpec(index, kind, Text(1, ParameterCaptions[selector]),
                selector, min, max, owner);
            int? enumRow = selector switch {
                >= 1 and <= 8 => selector - 1, 13 => 8, 21 => 9, 44 => 11, _ => null };
            if (enumRow is { } row && min is { } first && max is { } last)
                return parameter with { Values = Array.AsReadOnly(Enumerable.Range(first, last - first + 1)
                    .Select(value => new EditedEventParameterValue(value, Text(1, EnumCaptionIds[row * 7 + value])))
                    .ToArray()) };
            if ((selector is 11 or 42) && min is { } abilityFirst && max is { } abilityLast)
                return parameter with { Values = Array.AsReadOnly(Enumerable.Range(abilityFirst, abilityLast - abilityFirst + 1)
                    .Select(value => new EditedEventParameterValue(value,
                        value == 0 ? unknownText : Text(0, 0xF9 + AbilityValues[value]))).ToArray()) };
            if (selector == 14)
                return parameter with { Values = Array.AsReadOnly(Enumerable.Range(0, 4)
                    .Select(value => new EditedEventParameterValue(value, Text(0, 0x123 + DeathValues[value]))).ToArray()) };
            if (selector == 20)
                return parameter with { Values = Array.AsReadOnly(Enumerable.Range(1, 6)
                    .Select(value => new EditedEventParameterValue(value, Text(0, 0x141 + ParameterValues[value]))).ToArray()) };
            return parameter;
        }
        EditedEventAuthoringChoice Make(ChoiceSource source, bool result)
        {
            var parameters = source.Selectors.Select((selector, index) => Parameter(index, selector)).ToArray();
            string? missing = parameters.Select(parameter => parameter.UnresolvedOwner).FirstOrDefault(owner => owner is not null);
            //59B120 defines argument selectors; 3842xx factories initialize the
            //underlying standard instruction's eight arguments and two controls to zero.
            return PrepareChoice(new(source.Code, Operation(result ? ResultCaptionCode(source.Code) : source.Code),
                Array.AsReadOnly(parameters), EditedEventInstruction.Create(source.Code), missing));
        }
        Conditions = Array.AsReadOnly(ConditionSources.Select(source => Make(source, false)).ToArray());
        Results = Array.AsReadOnly(ResultSources.Select(source => Make(source, true)).ToArray());
        ContentOperations = Array.AsReadOnly(new[] { 85, 410, 91, 92, 107, 385 }.Select(code =>
            new EditedEventAuthoringChoice(code, Operation(code), [], EditedEventInstruction.Create(code),
                $"event_editor.contents.{code}.parameter_selectors")).ToArray());
    }

    private sealed record ChoiceSource(int Code, int[] Selectors);
    //59B120: normal editor tables, in source order (33 conditions, 21 results).
    private static readonly ChoiceSource[] ConditionSources =
    [
        new(5, []),
        new(7, []),
        new(9, [0, 1]),
        new(10, [0, 5, 28]),
        new(13, [6, 25, 26, 27]),
        new(14, [6, 25, 26, 27]),
        new(15, [5, 28]),
        new(16, [2]),
        new(18, [4, 15]),
        new(19, [4, 16]),
        new(20, [15, 5, 34]),
        new(23, [15, 5, 37]),
        new(24, [15, 7, 12]),
        new(25, [15, 1, 15]),
        new(26, [15, 15, 5, 20]),
        new(27, [15, 2, 15]),
        new(36, [4, 15]),
        new(37, [15, 5, 15, 34]),
        new(40, [5, 33]),
        new(49, [17, 4, 15]),
        new(56, [9, 4, 15]),
        new(57, [9, 4, 16]),
        new(59, [9, 11, 42, 42, 42]),
        new(60, [9, 2, 9]),
        new(61, [9, 5, 31]),
        new(62, [9, 1, 9]),
        new(66, [9, 2]),
        new(74, [9, 4, 17]),
        new(244, [9, 41, 41, 41, 41]),
        new(298, [10, 3]),
        new(335, [18, 2, 15]),
        new(336, [40, 2]),
        new(418, [9, 9, 41]),
    ];
    private static readonly ChoiceSource[] ResultSources =
    [
        new(388, [15, 15]),
        new(389, [10, 9]),
        new(391, [15, 30, 8]),
        new(392, [15, 15, 32]),
        new(393, [15, 15, 39]),
        new(394, [15, 15]),
        new(395, [16, 35, 8]),
        new(396, [16, 36, 8]),
        new(397, [16, 37, 8]),
        new(398, [16, 38, 8]),
        new(399, [16, 21]),
        new(400, [9, 15]),
        new(401, [17, 9]),
        new(403, [9, 13, 8, 29]),
        new(404, [9, 31]),
        new(405, [9]),
        new(407, [22, 23]),
        new(408, [9, 14]),
        new(409, [9, 9]),
        new(419, [9, 9, 41]),
        new(429, [9, 45]),
    ];
    //59AEC0: Data section 1 caption for each actual parameter selector kind.
    private static readonly int[] ParameterCaptions =
    [
        0x2EC, 0x44F, 0x794, 0x794, 0x797, 0x79A, 0x79A, 0x79A, 0x7AB, 0x795, 0x213, 0x212, 0x7A8, 0x452, 0x229, 0x79E, 0x798, 0x7A9, 0x200, 0x3C8, 0x413, 0x5EA, 0x7C1, 0x7C2, 0x7A0, 0x79F, 0x7A3, 0x47A, 0x7A5, 0x7C3, 0x7A6, 0x7A2, 0x413, 0x7C5, 0x7C6, 0x7C7, 0x7C8, 0x7C9, 0x7CA, 0x7CB, 0x81C, 0x7BF, 0x7C0, 0x2FD, 0x7B8, 0x868
    ];

    //521E30 maps parameter selector kinds to these 5BF540 menu rows.
    private static readonly int[] EnumCaptionIds =
    [
        0x4F0, 0x4F1, 0x353, 0x353, 0x353, 0x353, 0x353, 0x4F2, 0x4F3, 0x353, 0x353, 0x353, 0x353, 0x353, 0x6D5, 0x6D4, 0x353, 0x353, 0x353, 0x353, 0x353, 0x353, 0x4F4, 0x4F5, 0x353, 0x353, 0x353, 0x353, 0x353, 0x4F4, 0x4F5, 0x4F6, 0x4F7, 0x4F8, 0x4F9, 0x353, 0x4F4, 0x4F5, 0x4FC, 0x4FD, 0x4FA, 0x4FB, 0x353, 0x4F4, 0x4F5, 0x4FE, 0x4F9, 0x4FF, 0x4F7, 0x5D9, 0x7B6, 0x24C, 0x24D, 0x353, 0x353, 0x353, 0x353, 0x238, 0x239, 0x23A, 0x23B, 0x23C, 0x353, 0x353, 0x46C, 0x46D, 0x46E, 0x46F, 0x353, 0x353, 0x2AD, 0x2AE, 0x2AF, 0x353, 0x353, 0x353, 0x353, 0x53F, 0x7B9, 0x7BA, 0x7BB, 0x353, 0x353, 0x353
    ];

    private static readonly int[] AbilityValues = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
    private static readonly int[] ParameterValues = [0, 0, 1, 2, 3, 4, 5, 0];
    private static readonly int[] DeathValues = [0, 1, 2, 3];

    private static int ResultCaptionCode(int code) => code switch
    {
        388 => 0x75, 389 => 0x76, 391 => 0x7A, 392 => 0x7B, 393 => 0x7D,
        394 => 0x7E, 395 => 0x8E, 396 => 0x8F, 397 => 0x90, 398 => 0x91,
        399 => 0x98, 400 => 0x99, 401 => 0x9A, 403 => 0x9F, 404 => 0xA0,
        405 => 0xA2, 407 => 0xA4, 408 => 0x103, 409 => 0x13F,
        419 => 0x1A4, 429 => 0x1AC,
        _ => throw new InvalidDataException("event_editor.results.caption_mapping")
    };

    //5BED90/5BF160 supply actual native parameter limits. Dynamic selector
    //inventories are not replaced with integer fields or assumed title-world IDs.
    private static (EditedEventParameterKind Kind, int? Min, int? Max, string? Missing)
        ParameterShape(int selector) => selector switch
    {
        1 or 2 or 3 => (EditedEventParameterKind.Choice, 0, 1, null),
        4 => (EditedEventParameterKind.Choice, 1, 2, null),
        5 or 6 or 7 => (EditedEventParameterKind.Choice, 1, 6, null),
        8 => (EditedEventParameterKind.Choice, 0, 3, null),
        11 => (EditedEventParameterKind.Choice, 1, 9, null),
        13 => (EditedEventParameterKind.Choice, 1, 5, null),
        14 => (EditedEventParameterKind.Choice, 0, 3, null),
        20 => (EditedEventParameterKind.Choice, 1, 6, null),
        21 => (EditedEventParameterKind.Choice, 2, 4, null),
        44 => (EditedEventParameterKind.Choice, 0, 3, null),
        25 => (EditedEventParameterKind.Integer, 180, 350, null),
        26 => (EditedEventParameterKind.Integer, 1, 12, null),
        27 => (EditedEventParameterKind.Integer, 1, 21, null),
        28 => (EditedEventParameterKind.Integer, 0, 999, null),
        29 => (EditedEventParameterKind.Integer, 1, 100, null),
        30 => (EditedEventParameterKind.Integer, 1, 130, null),
        31 => (EditedEventParameterKind.Integer, 0, 100, null),
        32 => (EditedEventParameterKind.Integer, 0, 100, null),
        34 => (EditedEventParameterKind.Integer, 1, 46, null),
        39 => (EditedEventParameterKind.Integer, 1, 24, null),
        45 => (EditedEventParameterKind.Integer, 1, 10000, null),
        33 => (EditedEventParameterKind.Integer, 2, 50, null),
        35 => (EditedEventParameterKind.Integer, 0, 200000, null),
        36 => (EditedEventParameterKind.Integer, 0, 400000, null),
        37 => (EditedEventParameterKind.Integer, 0, 150000, null),
        38 => (EditedEventParameterKind.Integer, 0, 9999, null),
        0 => (EditedEventParameterKind.Choice, 1, 313, "event_editor.parameter.event_selection_inventory"),
        12 => (EditedEventParameterKind.Choice, 1, 10, "event_editor.parameter.region_selection_inventory"),
        18 => (EditedEventParameterKind.Province, 1, 17, "event_editor.parameter.province_selection_inventory"),
        19 => (EditedEventParameterKind.Scenery, 1, 43, "event_editor.parameter.scenery_selection_inventory"),
        22 or 23 => (EditedEventParameterKind.Person, null, null, "event_editor.parameter.key_people_sex_inventory"),
        40 => (EditedEventParameterKind.Person, 0x1005, 0x1055, "event_editor.parameter.force_person_selection_inventory"),
        41 => (EditedEventParameterKind.Person, 0, null, "event_editor.parameter.person_selection_inventory"),
        42 => (EditedEventParameterKind.Choice, 0, 9, null),
        9 => (EditedEventParameterKind.Person, null, null, "event_editor.parameter.person_selection_inventory"),
        10 => (EditedEventParameterKind.Person, null, null, "event_editor.parameter.person_selection_inventory"),
        15 => (EditedEventParameterKind.Person, null, null, "event_editor.parameter.person_selection_inventory"),
        16 => (EditedEventParameterKind.City, 1, 46, "event_editor.parameter.city_selection_inventory"),
        17 => (EditedEventParameterKind.Foothold, 1, 61, "event_editor.parameter.foothold_selection_inventory"),
        _ => (EditedEventParameterKind.Unknown, null, null, $"event_editor.parameter.selector_{selector}")
    };
}

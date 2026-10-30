using San14.Data.Formats;
using San14.Core;

namespace San14.Data;

public sealed partial class EditedEventAuthoringCatalog
{
    /// <summary>Real admitted title and registered people. No campaign is created
    /// for an editor selector; sex is used only by the two partner lists.</summary>
    public EditedEventAuthoringCatalog WithTitleContext(IReadOnlyList<EditedEventParameterValue> people,
        IReadOnlyDictionary<int, byte>? personSexes = null)
    {
        ArgumentNullException.ThrowIfNull(people);
        if (people.Any(person => person.Value is < 1 or > 6000 || string.IsNullOrEmpty(person.Caption)) ||
            people.Select(person => person.Value).Distinct().Count() != people.Count)
            throw new InvalidDataException("event_editor.title_person_inventory");
        var inventory = people.ToArray();
        EditedEventAuthoringChoice Bind(EditedEventAuthoringChoice choice)
        {
            if (choice.Code is not (59 or 244)) return choice;
            var parameters = choice.Parameters.Select(parameter =>
            {
                if (parameter.SelectorKind is not (9 or 41)) return parameter;
                var values = parameter.SelectorKind == 41
                    ? new[] { new EditedEventParameterValue(0, unknownText) }.Concat(inventory).ToArray() : inventory;
                return parameter with { Values = Array.AsReadOnly(values), Minimum = null, Maximum = null,
                    UnresolvedOwner = values.Length == 0 ? "event_editor.title_person_empty" : null };
            }).ToArray();
            return PrepareChoice(choice with { Parameters = Array.AsReadOnly(parameters),
                UnresolvedOwner = parameters.Select(p => p.UnresolvedOwner).FirstOrDefault(p => p is not null) });
        }
        return new(Array.AsReadOnly(Conditions.Select(Bind).ToArray()), Array.AsReadOnly(Results.Select(Bind).ToArray()),
            ContentOperations, unknownText, groupEndCaptions,
            personSexes is null ? new Dictionary<int, byte>() : new Dictionary<int, byte>(personSexes));
    }

    private EditedEventAuthoringCatalog BindResourceInventories(GameResourceRepository repository, string locale)
    {
        var directories = repository.LoadEditedEventAuthoringInventories(locale);
        EditedEventAuthoringChoice Bind(EditedEventAuthoringChoice choice)
        {
            var parameters = choice.Parameters.Select(parameter =>
            {
                if (parameter.SelectorKind is not int selector || !directories.TryGetValue(selector, out var values)) return parameter;
                return parameter with { Values = values, Minimum = null, Maximum = null,
                    UnresolvedOwner = values.Count == 0 ? $"event_editor.parameter.selector_{selector}_empty" : null };
            }).ToArray();
            return PrepareChoice(choice with { Parameters = Array.AsReadOnly(parameters),
                UnresolvedOwner = parameters.Select(p => p.UnresolvedOwner).FirstOrDefault(p => p is not null) });
        }
        return new(Array.AsReadOnly(Conditions.Select(Bind).ToArray()), Array.AsReadOnly(Results.Select(Bind).ToArray()),
            ContentOperations, unknownText, groupEndCaptions, personSexes);
    }

    private static EditedEventAuthoringChoice PrepareChoice(EditedEventAuthoringChoice choice)
    {
        //500030 installs the actual minimum/current first candidate for a
        //new input dialog. Existing instructions never use these defaults.
        var instruction = choice.DefaultInstruction;
        foreach (var parameter in choice.Parameters)
        {
            if (parameter.UnresolvedOwner is not null) continue;
            int? value = parameter.Values.Count > 0 ? parameter.Values[0].Value : parameter.Minimum;
            if (value is int first) instruction = instruction with
                { Arguments = instruction.Arguments.SetItem(parameter.ArgumentIndex, first) };
        }
        return choice with { DefaultInstruction = instruction };
    }
}

public sealed partial class GameResourceRepository
{
    internal IReadOnlyDictionary<int, IReadOnlyList<EditedEventParameterValue>> LoadEditedEventAuthoringInventories(string locale)
    {
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        var (bytes, _) = ReadSource($"0010_{locale}/fixdataexce.s14");
        var fixedData = S14Reader.Read(bytes, false);
        var messages = LoadMessages(locale, MessageKind.Data).Messages;
        string Text(int section, int id) => messages.Sections[section].Records[id].Text
            ?? throw new InvalidDataException($"event_editor.inventory.message.{section}.{id}");
        var cities = S14ScenarioTables.ReadCityDefinitions(fixedData);
        var gates = S14ScenarioTables.ReadGateDefinitions(fixedData);
        var growth = S14GrowthModifierReader.Read(fixedData);
        var sceneries = S14SceneryReader.ReadFixed(fixedData);
        var (prototypeBytes, _) = ReadSource($"0010_{locale}/scedaexce08.s14");
        var prototype = S14Reader.Read(prototypeBytes, true);
        var identities = S14ScenarioTables.ReadCharacterIdentities(prototype, locale);
        var names = S14ScenarioTables.ReadCharacterNames(prototype, locale).Names;
        var forcePeople = identities.Where(row => row.CharacterId is >= 0x1005 and <= 0x1055)
            .Select(row => new EditedEventParameterValue(row.CharacterId, names[row.Slot].DisplayName)).ToArray();
        IReadOnlyList<EditedEventParameterValue> Read(IEnumerable<EditedEventParameterValue> values) => Array.AsReadOnly(values.ToArray());
        return new Dictionary<int, IReadOnlyList<EditedEventParameterValue>>
        {
            //3DA010's title/no-player branch displays Data3[event*4+1].
            [0] = Read(Enumerable.Range(1, 313).Select(id => new EditedEventParameterValue(id, Text(3, id * 4 + 1)))),
            //5BF540's region indirection is the source identity0..10 table.
            [12] = Read(Enumerable.Range(1, 10).Select(id => new EditedEventParameterValue(id, growth.RegionNames[id]))),
            [16] = Read(cities.Where(c => c.Slot is >= 1 and <= 46).Select(c => new EditedEventParameterValue(c.Slot, c.Name))),
            [17] = Read(cities.Where(c => c.Slot is >= 1 and <= 46).Select(c => new EditedEventParameterValue(c.Slot, c.Name))
                .Concat(gates.Where(g => g.LocationSlot is >= 52 and <= 61).Select(g => new EditedEventParameterValue(g.LocationSlot, g.Name)))),
            [18] = Read(growth.States.Where(s => s.Slot is >= 1 and <= 17).Select(s =>
                new EditedEventParameterValue(s.Slot, S14ForceNameDefinitionReader.ReadLiteralUnits(s.RawBytes.AsSpan(24, 6))))),
            [19] = Read(sceneries.Sceneries.Where(s => s.Slot is >= 1 and <= 43).Select(s => new EditedEventParameterValue(s.Slot, s.Name))),
            [27] = Read(new[] { 1, 11, 21 }.Select((day, index) => new EditedEventParameterValue(day, Text(1, 0x2AD + index)))),
            //40 is a real Person-name lookup, not a scenario selector.
            [40] = Read(forcePeople)
        };
    }
}

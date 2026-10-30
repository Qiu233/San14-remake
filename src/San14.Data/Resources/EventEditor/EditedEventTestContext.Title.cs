using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    internal EditedEventTestContext LoadEditedEventTestContext(string locale,
        RegisteredOfficerLibrary registered, CollectionOfficerAdmission? admission)
    {
        var title = LoadCollectionOfficerCatalog(locale, admission);
        var (bytes, _) = ReadSource($"0010_{locale}/scedaexce08.s14");
        var prototype = S14Reader.Read(bytes, true);
        var identity = S14ScenarioTables.ReadCharacterIdentities(prototype, locale)[0];
        var name = S14ScenarioTables.ReadCharacterNames(prototype, locale).Names[0];
        var placement = S14ScenarioTables.ReadCharacterPlacements(prototype, locale)[0];
        var credit = S14CreditEffectReader.ReadCharacterReferences(prototype, locale).Characters[0];
        var zero = new EditedEventTestPerson(identity.Slot, identity.CharacterId, name.DisplayName,
            identity.AppearanceCode, placement.StatusCode,
            credit.RawFlagsWord ?? throw new InvalidDataException("event_editor.test.zero_flags"), credit.RawTailCode);
        return title.CreateEditedEventTestContext(this, registered, zero);
    }
}

public sealed partial class CollectionOfficerCatalog
{
    internal EditedEventTestContext CreateEditedEventTestContext(GameResourceRepository repository, RegisteredOfficerLibrary registered,
        EditedEventTestPerson zero)
    {
        EditedEventTestPerson Project(Person person) => new(person.Slot, checked((ushort)person.Id),
            person.Name.DisplayName, person.Appearance, person.Status, person.Flags, person.Behavior);
        var projection = people.Values.Select(Project).ToDictionary(person => (int)person.Identity);
        foreach (var person in specialPeople.Values) projection[person.Id] = Project(person);
        // The existing title producer owns DLC admission and its status5 projection.
        foreach (var entry in admitted) projection[entry.Person.Id] = Project(entry.Person);
        foreach (var person in registered.Officers)
            // The registered-person source installation uses physical1401..1550;
            // its populated title definition has nonzero status9, before new-game adoption.
            projection[person.Id] = new(1401 + person.Id - 3001, checked((ushort)person.Id),
                person.Name, checked((ushort)person.PortraitId), 9, 0, 0);
        return new(repository, Locale, projection.Values, zero, Sources);
    }
}

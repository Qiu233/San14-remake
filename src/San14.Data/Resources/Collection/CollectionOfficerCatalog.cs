using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The title loads the specified scenario08 prototype before entering
/// Collection. This resource-only projection follows that producer; it never
/// constructs a campaign or reads the player's current world.</summary>
public sealed partial class CollectionOfficerCatalog
{
    private sealed record Person(int Slot, int Id, ushort Appearance, S14CharacterName Name,
        S14CharacterDialogue Dialogue, byte Sex, byte Status, ushort Flags, byte Behavior,
        PersonRelationFields Relations, int[] Abilities, ushort[] Traits, byte[] Tactics,
        uint Formations, byte Doctrine, byte Policy, byte PolicyCost, short[] Growth)
    {
        internal byte RawPolicyCost { get; init; }
        internal ushort FirstLocation { get; init; }
        internal int ForceSlot { get; init; }
        internal short TerminalYear { get; init; }
    }
    private readonly Dictionary<int, Person> people = [];
    private readonly Dictionary<int, Person> specialPeople = [];
    private readonly List<(Person Person, int Group)> admitted = [];
    private readonly Dictionary<int, IReadOnlyList<CollectionOfficerRow>> pages = [];
    private readonly Dictionary<(int Id, int Variant), CollectionOfficerDetail> details = [];
    private readonly LoadedMessages messages;
    private readonly S14FormationCatalog combat;
    private readonly S14DoctrineCatalog doctrines;
    private readonly S14PolicyCatalog policies;
    private readonly CollectionOfficerSkills skills;
    private readonly int prototypeYear;
    private readonly HashSet<int> unresolvedPolicyLevels = [];
    private readonly CollectionOfficerAdmission? admission;
    private readonly GameResourceRepository? portraitRepository;
    public string Locale { get; }
    public string Title { get; }
    public IReadOnlyList<CollectionOfficerTab> Tabs { get; }
    public IReadOnlyList<SourceProvenance> Sources { get; }
    public IReadOnlyList<CollectionOfficerUnresolvedCandidate> UnresolvedCandidates { get; }
    public IReadOnlyList<string> UnresolvedOwners { get; }
    public IReadOnlyList<CollectionOfficerSpecialEntry> SpecialDirectory { get; }

    public static CollectionOfficerCatalog Load(GameResourceRepository repository, string locale,
        CollectionOfficerAdmission? admission = null) => repository.LoadCollectionOfficerCatalog(locale, admission);

    internal CollectionOfficerCatalog(string locale, S14Document prototype, SourceProvenance prototypeSource,
        S14Document fixedData, SourceProvenance fixedSource, LoadedMessages messages,
        CollectionOfficerAdmission? admission, IReadOnlyList<LoadedCollectionSpecialOfficer> specialDirectory,
        GameResourceRepository? portraitRepository = null)
    {
        Locale = locale; this.messages = messages; this.admission = admission; this.portraitRepository = portraitRepository;
        prototypeYear = prototype.StartYear ?? throw new InvalidDataException("collection.officer.prototype_year");
        Title = Data(1, 0x6C);
        combat = S14FormationReader.Read(fixedData);
        doctrines = S14DoctrineReader.Read(fixedData); policies = S14PolicyReader.Read(fixedData);
        skills = new(S14PersonalityReader.ReadFixed(fixedData), combat, S14GemReader.ReadFixed(fixedData));
        Sources = Array.AsReadOnly(new[] { prototypeSource, fixedSource, messages.Source }
            .Concat(specialDirectory.Where(slot => slot.Source is not null).Select(slot => slot.Source!)).ToArray());
        var table = S14ScenarioTables.ReadCharacterNames(prototype, locale);
        var ids = S14ScenarioTables.ReadCharacterIdentities(prototype, locale);
        var placements = S14ScenarioTables.ReadCharacterPlacements(prototype, locale);
        var districts = S14ScenarioTables.ReadScenarioDistricts(prototype);
        int ForceSlot(byte districtSlot)
        {
            if (districtSlot == 0 || districtSlot >= districts.Count) return 0;
            var district = districts[districtSlot];
            return district.ForceSlot is > 0 and < 52 && district.Number != 0 && district.LeaderSlot != 0
                ? district.ForceSlot : 0;
        }
        var abilities = S14ScenarioTables.ReadCharacterAbilities(prototype, locale);
        var dialogue = S14CharacterDialogueReader.Read(prototype, locale);
        var sexes = S14ScenarioTables.ReadCharacterSexes(prototype, locale);
        var lifespans = S14ScenarioTables.ReadCharacterLifespans(prototype, locale);
        var relation = OwnedPersonRelationInputs.ReadSourceRows(prototype, locale);
        var credit = S14CreditEffectReader.ReadCharacterReferences(prototype, locale).Characters;
        var policy = S14PolicyReader.ReadCharacterReferences(prototype, locale).Characters;
        var doctrine = S14DoctrineReader.ReadCharacterReferences(prototype, locale).Characters;
        var skillFields = S14PersonSkillReader.ReadCharacterReferences(prototype, locale)
            ?? throw new InvalidDataException("collection.officer.static_skills");
        int delta = locale == "SC" ? 56 : 0;
        foreach (var identity in ids)
        {
            int slot = identity.Slot;
            if (identity.CharacterId == 0) continue;
            if (credit[slot].RawStageCode != 0 && credit[slot].RawSelectionCodes.Any(code => code is >= 1 and <= 6) &&
                placements[slot].StatusCode != 1 && (credit[slot].RawFlagsWord!.Value & 0x4000) == 0)
                unresolvedPolicyLevels.Add(identity.CharacterId);
            var projected = skills.Project(skillFields.Characters[slot]);
            var a = abilities[slot];
            var raw = prototype.DecodedPayload.AsSpan(table.RecordOffset + slot * table.RecordStride, table.RecordStride);
            var growth = new short[12];
            for (int index = 0; index < growth.Length; index++)
                growth[index] = BinaryPrimitives.ReadInt16LittleEndian(raw.Slice(276 + delta + index * 2, 2));
            people.Add(identity.CharacterId, new(slot, identity.CharacterId, identity.AppearanceCode,
                table.Names[slot], dialogue[slot], sexes[slot].RawCode, placements[slot].StatusCode,
                credit[slot].RawFlagsWord ?? throw new InvalidDataException("collection.officer.static_flags"),
                credit[slot].RawTailCode, relation[slot],
                [a.Leadership, a.Strength, a.Intelligence, a.Politics, a.Charm],
                projected.Traits, projected.Tactics, projected.Formations,
                doctrine[slot].RawDoctrineId ?? throw new InvalidDataException("collection.officer.static_doctrine"),
                policy[slot].RawPolicySlot == 22 ? (byte)24 : policy[slot].RawPolicySlot,
                (byte)Math.Min((int)(policy[slot].RawPolicyBaseCost ?? throw new InvalidDataException("collection.officer.static_policy_cost")), 6), growth)
            {
                RawPolicyCost = policy[slot].RawPolicyBaseCost!.Value,
                FirstLocation = placements[slot].CitySlot, ForceSlot = ForceSlot(placements[slot].DistrictSlot),
                TerminalYear = unchecked((short)lifespans[slot].TerminalYear)
            });
        }
        foreach (var person in people.Values.OrderBy(person => person.Slot))
            if (person.Id is >= 1 and <= 1000 && NativeValid(person)) admitted.Add((person, 0));
        var unresolved = new List<CollectionOfficerUnresolvedCandidate>();
        for (int id = 2001; id <= 2088; id++)
        {
            bool? allowed = ReadBonusAdmission(id);
            if (allowed is null)
            {
                unresolved.Add(new(id, OrdinaryPortraitEligibility.NeedsApplicationSetting(id)
                    ? "collection.officer.application_profile_unlock" : "collection.officer.content_ownership_and_availability"));
                continue;
            }
            if (!allowed.Value) continue;
            if (!people.TryGetValue(id, out var person))
            { unresolved.Add(new(id, "collection.officer.prototype_person_definition")); continue; }
            admitted.Add((person with { Status = 5 }, 1));
        }
        var specialEntries = new List<CollectionOfficerSpecialEntry>(100);
        foreach (var slot in specialDirectory)
        {
            if (slot.Person is not { Id: not 0 } definition)
            { specialEntries.Add(new(slot.Slot, slot.FilenameKey, slot.AppId, null, slot.Source, null)); continue; }
            ushort flags = definition.Id is >= 5101 and <= 5200 ? (ushort)0x600 : (ushort)0;
            var projected = skills.Project(new(slot.Slot, checked((ushort)definition.Id), definition.Traits,
                definition.Formations, definition.Tactics, []));
            var person = new Person(1851 + slot.Slot, definition.Id, definition.Appearance, definition.Name,
                definition.Dialogue, definition.Sex, definition.Status, flags, 0, definition.Relations,
                definition.Abilities, projected.Traits, projected.Tactics, projected.Formations,
                definition.Doctrine, definition.Policy == 22 ? (byte)24 : definition.Policy,
                (byte)Math.Min((int)definition.PolicyCost, 6), definition.Growth)
            { RawPolicyCost = definition.PolicyCost, FirstLocation = definition.FirstLocation,
                ForceSlot = ForceSlot(definition.DistrictSlot), TerminalYear = definition.DeathYear };
            if (!specialPeople.TryAdd(person.Id, person))
                throw new InvalidDataException("collection.officer.special_person_duplicate_identity");
            bool classified = person.Id is >= 5101 and <= 5200 or >= 2078 and <= 2082;
            bool? owned = !classified ? true : admission is not null && admission.Content.TryGetValue(person.Id, out var content)
                ? content.Owned : null;
            string? missing = owned == false ? null : slot.Subscribed is null && slot.AppId > 0 ? "collection.officer.special_person_dlc_subscription" :
                owned is null ? "collection.officer.special_person_content_ownership" : null;
            specialEntries.Add(new(slot.Slot, slot.FilenameKey, slot.AppId, person.Id, slot.Source, missing));
            // The original last group visits only the first eighty physical
            // receivers. Definition presence alone does not grant ownership.
            if (slot.Slot >= 80 || owned == false) continue;
            if (missing is not null) { unresolved.Add(new(person.Id, missing)); continue; }
            if (admitted.Any(entry => entry.Person.Id == person.Id))
                throw new InvalidDataException("collection.officer.duplicate_catalog_identity");
            admitted.Add((person, 2));
        }
        SpecialDirectory = specialEntries.AsReadOnly();
        UnresolvedCandidates = unresolved.AsReadOnly();
        UnresolvedOwners = specialEntries.Where(entry => entry.UnresolvedOwner is not null)
            .Select(entry => entry.UnresolvedOwner!).Distinct().ToArray();
        Tabs = BuildTabs();
    }

    public IReadOnlyList<CollectionOfficerRow> ReadRows(int tabId)
    {
        if ((uint)tabId >= (uint)Tabs.Count) throw new ArgumentOutOfRangeException(nameof(tabId));
        if (pages.TryGetValue(tabId, out var existing)) return existing;
        var rows = admitted.Select(entry => new CollectionOfficerRow(entry.Person.Id,
            new ReadOnlyDictionary<int, CollectionOfficerCell>(Tabs[tabId].Columns
                .ToDictionary(column => column.Id, column => Cell(entry.Person, column.Id))))
            { GroupIndex = entry.Group }).ToArray();
        return pages[tabId] = Array.AsReadOnly(rows);
    }
    public string ReadName(int officerId) => specialPeople.TryGetValue(officerId, out var original)
        ? original.Name.DisplayName : people.TryGetValue(officerId, out var person)
        ? person.Name.DisplayName : throw new ArgumentOutOfRangeException(nameof(officerId));

    private bool NativeValid(Person person) => person.Slot != 0 &&
        ((person.Flags & 0x4000) != 0 ? person.Behavior != 0 : person.Id is >= 5001 and <= 5100 || person.Status != 0);
    private bool? ReadBonusAdmission(int id)
    {
        if (admission?.AllowedOfficerIds.TryGetValue(id, out bool allowed) == true) return allowed;
        if (OrdinaryPortraitEligibility.NeedsApplicationSetting(id)) return null;
        // 3701A0 bypasses both content bits for unclassified identities.
        // Only the five 2078..2082 people reach its two-bit DLC gate here.
        if (!OrdinaryPortraitEligibility.NeedsUnlock(id)) return true;
        if (admission is null || !admission.Content.TryGetValue(id, out var content)) return null;
        return content.Owned switch { false => false, true => content.Available, _ => null };
    }
    private string Data(int section, int id) => (uint)section < (uint)messages.Messages.Sections.Count &&
        (uint)id < (uint)messages.Messages.Sections[section].Records.Count &&
        messages.Messages.Sections[section].Records[id].Text is string text ? text :
        throw new InvalidDataException($"collection.officer.data.{section}.{id:X}");
    private string Name(int id) => people.TryGetValue(id, out var person) ? person.Name.DisplayName : "";
    private string Sex(Person person) => person.Sex < 2 ? Data(1, person.Sex == 0 ? 0x495 : 0x496) : "";
    private string Doctrine(Person person) => person.Doctrine is > 0 and < 7 ? doctrines.Doctrines[person.Doctrine].Name : "";
    private S14PolicyDefinition? Policy(Person person) => person.Policy is > 0 and < 121 ? policies.Policies[person.Policy] : null;
    private int PolicyCost(Person person) => Math.Max(1, person.PolicyCost + (Policy(person)?.RawCostAddition ?? 0));
    private bool Formation(Person person, int id) => (person.Formations & 1u << id) != 0 &&
        (id != 14 || person.Id is >= 4101 and <= 4200);
    private string Tactic(Person person, int slot) => person.Tactics[slot] is > 0 and < 201 &&
        skills.TacticAvailable(person.Tactics[slot]) ? combat.Tactics[person.Tactics[slot]].Name : "";
    private string Year(Person person) => person.Id is not (>= 1 and <= 1000) ? Data(1, 0x353) :
        person.Dialogue.BirthYear != 0 ? unchecked((short)person.Dialogue.BirthYear).ToString(CultureInfo.InvariantCulture) : "";

    private CollectionOfficerCell Cell(Person person, int column)
    {
        string? text = column switch
        {
            0 or 182 => person.Name.DisplayName,
            29 => Sex(person), 137 => person.Dialogue.CourtesyName, 183 => Year(person), 184 => DeathYear(person),
            >= 13 and <= 17 => person.Abilities[column - 13] == 0 && column != 13 ? Data(1, 0x353) : person.Abilities[column - 13].ToString(CultureInfo.InvariantCulture),
            >= 40 and <= 49 => Formation(person, column - 39) ? Data(1, 0x338) : "",
            50 => Formation(person, 14) ? Data(1, 0x338) : "",
            >= 51 and <= 60 => Tactic(person, column - 51),
            >= 61 and <= 65 => person.Traits[column - 61] is ushort id && id != 0 ? skills.TraitName(id) : "",
            98 => Policy(person)?.Name ?? "", 99 => "", 27 => PolicyCost(person).ToString(CultureInfo.InvariantCulture),
            101 => Policy(person)?.Description ?? "", 24 => Doctrine(person),
            30 => Name(person.Relations.B4), 31 => Name(person.Relations.B6),
            32 => Name(person.Relations.BA), 33 => Name(person.Relations.BC),
            186 => Rank(person.Growth[1]), 187 => Rank(person.Growth[9]), 188 => Rank(person.Growth[8]),
            189 => Rank(person.Growth[0]), 190 => Rank(person.Growth[7]), 192 => Rank(person.Growth[11]),
            191 => Data(1, person.Growth[3] != 0 ? 0x20C : person.Growth[4] != 0 ? 0x20D : person.Growth[5] != 0 ? 0x20E : 0x353),
            _ => null
        };
        string? missing = text is null ? $"collection.officer.column_{column}" : null;
        var sort = SortKey(person, column);
        return new(column, text, sort.Value) { UnresolvedOwner = missing, SortGroup = sort.Group };
    }
    private static string Rank(short value) => (value switch { -20 => 1, -10 => 2, 0 => 3, 10 => 4, 20 => 5, _ => 0 }).ToString(CultureInfo.InvariantCulture);
    private string DeathYear(Person person) => person.Id is not (>= 1 and <= 1000) ? Data(1, 0x353) :
        person.TerminalYear != 0 ? person.TerminalYear.ToString(CultureInfo.InvariantCulture) : "";

    private IReadOnlyList<CollectionOfficerTab> BuildTabs()
    {
        int[][] ids = [[182,29,183,184,13,14,15,16,17], [182,40,41,42,43,44,45,46,47,48,49,50],
            [182,51,52,53,54,55], [182,56,57,58,59,60], [182,98,99,27,101], [182,61,62,63,64,65],
            [182,137,29,183,184,24,30,31,32,33], [0,186,187,188,189,190,191,192]];
        int[] captions = [0x3CD,0x22B,0x3DD,0x3DE,0x241,0x23D,0x451,0x7E8];
        (int Caption, int Width) Column(int id) => id switch
        {
            0 or 182 => (0x211,132), 29 => (0x42B,76), 183 => (0x4E7,76), 184 => (0x4E8,76),
            >=13 and <=17 => (0x238+id-13,76), >=40 and <=49 => (0x432+id-40,76), 50 => (0x6FE,76),
            >=51 and <=53 => (0x3DD+id-51,132), >=54 and <=60 => (0x43C+id-54,132),
            98 => (0x241,132), 99 => (0,0), 27 => (0x386,104), 101 => (0x400,860),
            >=61 and <=65 => (0x3E2+id-61,104), 137 => (0x4E0,132), 24 => (0x244,76),
            >=30 and <=33 => (0x42C+id-30,132), >=186 and <=189 => (0x7E9+id-186,76),
            190 => (2,76), 191 => (0x2BB,76), 192 => (0x262,76), _ => throw new InvalidDataException("collection.officer.column_profile")
        };
        return Array.AsReadOnly(ids.Select((columns,index) => new CollectionOfficerTab(index,Data(1,captions[index]),
            Array.AsReadOnly(columns.Select(id => { var column=Column(id); return new CollectionColumn(id, column.Width==0?"":Data(1,column.Caption),column.Width,CanSortColumn(id)); }).ToArray()))
            { InitialSortDirections = new ReadOnlyDictionary<int, bool>(columns.ToDictionary(id => id, InitialSortAscending)) }).ToArray());
    }
}

public sealed partial class GameResourceRepository
{
    public CollectionOfficerCatalog LoadCollectionOfficerCatalog(string locale, CollectionOfficerAdmission? admission = null)
    {
        if (locale is not ("TC" or "SC")) throw new ArgumentException("Collection locale must be TC or SC.", nameof(locale));
        var (prototypeBytes, prototypeSource) = ReadSource($"0010_{locale}/scedaexce08.s14");
        var (fixedBytes, fixedSource) = ReadSource($"0010_{locale}/fixdataexce.s14");
        var prototype = S14Reader.Read(prototypeBytes, true); var fixedData = S14Reader.Read(fixedBytes, false);
        if (prototype.FormatMarker != 0x66 || fixedData.FormatMarker != 0x66)
            throw new InvalidDataException("Collection officers require the actual CE title prototype and fixed resources.");
        admission=LoadCollectionOfficerAdmission(admission);
        return new(locale, prototype, prototypeSource, fixedData, fixedSource, LoadMessages(locale, MessageKind.Data), admission,
            ReadCollectionSpecialOfficerDirectory(admission), this);
    }
}

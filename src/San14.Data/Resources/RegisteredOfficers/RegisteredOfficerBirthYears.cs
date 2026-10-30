using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record RegisteredOfficerBirthYearPlan(ImmutableArray<int> SelectedIds, int ReferenceId,
    int OriginalBirthYear, int MinimumBirthYear, int MaximumBirthYear, int Lifespan);
public sealed record RegisteredOfficerBirthYearPerson(int Slot, int Id, int BirthYear, int DeathYear,
    ushort FamilyId, ushort FatherId, ushort MotherId, ushort SpouseId, ushort SwornBrotherId);
public sealed record RegisteredOfficerBirthYearScenario(int StartYear, string Name);

/// <summary>The title editor's selected-group date shift. Resource definitions
/// are detached inputs; no running campaign or original executable is used.</summary>
public static class RegisteredOfficerBirthYears
{
    public static RegisteredOfficerBirthYearPlan Preview(RegisteredOfficerLibrary library,
        IReadOnlyList<int> selectedIds, int referenceId)
    {
        if (selectedIds.Count is < 1 or > 150 || selectedIds.Distinct().Count() != selectedIds.Count ||
            !selectedIds.Contains(referenceId)) throw new InvalidDataException("The birth-year reference must belong to the selected registered officers.");
        var selected = selectedIds.Select(id => library.Find(id) ?? throw new InvalidDataException("A selected registered officer no longer exists.")).ToArray();
        var reference = selected.First(person => person.Id == referenceId);
        int minimum = reference.BirthYear + 135 - selected.Min(person => person.BirthYear);
        int maximum = reference.BirthYear + 250 - selected.Max(person => person.BirthYear);
        if (minimum > maximum) throw new InvalidDataException("The selected birth years have no common shift within years 135 through 250.");
        return new([.. selectedIds], referenceId, reference.BirthYear, minimum, maximum,
            reference.DeathYear - reference.BirthYear + 1);
    }

    public static IReadOnlyList<RegisteredOfficerBirthYearPerson> LoadSourcePeople(GameResourceRepository repository,
        LoadedScenario source)
    {
        string locale = source.Snapshot.Locale;
        if (source.Metadata.FormatMarker != 0x66 || locale is not ("SC" or "TC"))
            throw new InvalidDataException("Registered-officer date editing requires coherent CE person definitions.");
        string path = string.IsNullOrWhiteSpace(source.ScenarioSource.PhysicalPath)
            ? Path.Combine(repository.SourceDirectory, source.ScenarioSource.SourcePath) : source.ScenarioSource.PhysicalPath;
        var document = source.InitialScenarioDocument ?? S14Reader.Read(File.ReadAllBytes(path), true);
        var identities = S14ScenarioTables.ReadCharacterIdentities(document, locale);
        var placements = S14ScenarioTables.ReadCharacterPlacements(document, locale);
        var dialogue = S14CharacterDialogueReader.Read(document, locale);
        var lifespans = S14ScenarioTables.ReadCharacterLifespans(document, locale);
        var credits = S14CreditEffectReader.ReadCharacterReferences(document, locale).Characters;
        var relations = OwnedPersonRelationInputs.ReadSourceRows(document, locale);
        var result = new List<RegisteredOfficerBirthYearPerson>();
        foreach (var identity in identities)
        {
            int slot = identity.Slot, id = identity.CharacterId;
            if (slot == 0 || id == 0 || id is >= 3001 and <= 3150) continue;
            var credit = credits[slot];
            ushort flags = credit.RawFlagsWord ?? throw new InvalidDataException("Birth-year person validity flags are missing.");
            if ((flags & 0x4000) != 0 ? credit.RawTailCode == 0 : id is not (>= 5001 and <= 5100) && placements[slot].StatusCode == 0) continue;
            var relation = relations[slot];
            result.Add(new(slot, id, dialogue[slot].BirthYear, lifespans[slot].TerminalYear,
                relation.B2, relation.B4, relation.B6, relation.BA, relation.BC));
        }
        return result;
    }

    public static IReadOnlyList<RegisteredOfficerBirthYearScenario> LoadHistoryScenarios(GameResourceRepository repository,
        string locale)
    {
        if (locale is not ("SC" or "TC")) throw new ArgumentOutOfRangeException(nameof(locale));
        var paths = repository.EffectiveScenarios.Where(scenario => scenario.Locale == locale)
            .Select(scenario => scenario.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = repository.ScenarioDocuments.Where(scenario => paths.Contains(scenario.SourcePath) &&
            scenario.SelectionMetadata is { NativeId: < 199, FilterKind: 0 or 1, ScenarioKindCode: not 0 } metadata &&
            metadata.EditionCode is int edition && ((edition >> 8) & 255) == 0).ToList();
        // The complete list is sorted before the history control takes twelve.
        // Strict insertion retains source order for equal source keys.
        for (int index = 1; index < rows.Count; index++)
        {
            var value = rows[index]; int prior = index - 1;
            while (prior >= 0 && CompareScenarios(value, rows[prior]) < 0)
            { rows[prior + 1] = rows[prior]; prior--; }
            rows[prior + 1] = value;
        }
        return rows.Take(12).Select(scenario => new RegisteredOfficerBirthYearScenario(
            scenario.StartYear ?? throw new InvalidDataException("Birth-year comparison scenario has no year."),
            scenario.Title ?? throw new InvalidDataException("Birth-year comparison scenario has no name."))).ToArray();
    }

    public static bool RequiresRelationshipConfirmation(RegisteredOfficerLibrary library,
        RegisteredOfficerBirthYearPlan plan, int birthYear, IReadOnlyList<RegisteredOfficerBirthYearPerson> sourcePeople)
    {
        var shifted = Shift(library, plan, birthYear); var world = World(shifted, sourcePeople);
        return plan.SelectedIds.Any(id => InvalidRelations(shifted.Find(id)!, world).Any);
    }

    public static RegisteredOfficerLibrary Apply(RegisteredOfficerLibrary library, RegisteredOfficerBirthYearPlan plan,
        int birthYear, IReadOnlyList<RegisteredOfficerBirthYearPerson> sourcePeople, bool relationshipsConfirmed)
    {
        var shifted = Shift(library, plan, birthYear);
        var world = World(shifted, sourcePeople);
        var working = shifted.Officers.ToDictionary(person => person.Id);
        foreach (int id in plan.SelectedIds)
        {
            var person = working[id]; var invalid = InvalidRelations(person, world);
            if (invalid.Any && !relationshipsConfirmed)
                throw new InvalidOperationException("Registered-officer date changes require the original relationship confirmation.");
            if (invalid.Father) RemoveFather(id);
            if (invalid.Mother) working[id] = working[id] with { MotherId = 0 };
            if (invalid.Spouse)
            {
                if (working.TryGetValue(working[id].SpouseId, out var spouse)) working[spouse.Id] = spouse with { SpouseId = 0 };
                working[id] = working[id] with { SpouseId = 0 };
            }
            if (invalid.Children)
            {
                foreach (int childId in shifted.Officers.Select(child => child.Id))
                {
                    if (working[childId].FatherId == id) RemoveFather(childId);
                    if (working[childId].MotherId == id) working[childId] = working[childId] with { MotherId = 0 };
                }
            }
            // 206A30 removes this registered member from the group and then
            // 21D8A0 chooses its remaining historical or registered anchor.
            CanonicalizeSworn(working[id], removeCurrent: invalid.Sworn);
        }
        return new([.. shifted.Officers.Select(person => working[person.Id])]);

        void RemoveFather(int id)
        {
            var root = working[id] with { FatherId = 0 };
            working[id] = root with { FamilyId = (ushort)id };
            if (root.Sex != 0 || root.FamilyId == id) return;
            var pending = new Queue<int>(working.Values.Where(child => child.Id != id && child.FatherId == id).Select(child => child.Id));
            var visited = new HashSet<int>();
            while (pending.TryDequeue(out int descendantId))
            {
                if (!visited.Add(descendantId)) continue;
                var descendant = working[descendantId];
                if (world.ContainsKey(descendant.MotherId) || world.ContainsKey(descendant.SpouseId) || world.ContainsKey(descendant.SwornBrotherId))
                    throw new InvalidDataException("Paternal family propagation requires the complete mother, spouse and sworn eligibility rules.");
                working[descendantId] = descendant with { FamilyId = (ushort)id };
                if (descendant.Sex == 0)
                    foreach (var child in working.Values.Where(child => child.Id != descendantId && child.FatherId == descendantId)) pending.Enqueue(child.Id);
            }
        }
        IEnumerable<RegisteredOfficerBirthYearPerson> Group(RegisteredOfficer person) => world.Values.Where(other =>
        {
            ushort otherAnchor = working.TryGetValue(other.Id, out var otherDraft) ? otherDraft.SwornBrotherId : other.SwornBrotherId;
            return person.Id != other.Id && (otherAnchor is >= 1 and <= 6000 || person.SwornBrotherId is >= 1 and <= 6000) &&
                (otherAnchor == person.SwornBrotherId || otherAnchor == person.Id || other.Id == person.SwornBrotherId);
        });
        void CanonicalizeSworn(RegisteredOfficer person, bool removeCurrent)
        {
            var members = Group(person).ToList();
            if (!removeCurrent && members.Count == 0) return;
            if (!removeCurrent) members.Add(Project(person));
            var historical = members.Where(member => !working.ContainsKey(member.Id)).ToArray();
            int anchor = historical.Length == 1 ? historical[0].Id : historical.Length > 1 ? historical[0].SwornBrotherId :
                members.OrderBy(member => member.BirthYear).ThenBy(member => member.Id).Select(member => member.Id).FirstOrDefault();
            foreach (var member in members)
                if (working.TryGetValue(member.Id, out var entry)) working[member.Id] = entry with { SwornBrotherId = (ushort)anchor };
            if (removeCurrent) working[person.Id] = working[person.Id] with { SwornBrotherId = 0 };
        }
    }

    private sealed record InvalidAgeRelations(bool Father, bool Mother, bool Spouse, bool Children, bool Sworn)
    { internal bool Any => Father || Mother || Spouse || Children || Sworn; }
    private static InvalidAgeRelations InvalidRelations(RegisteredOfficer person,
        IReadOnlyDictionary<int, RegisteredOfficerBirthYearPerson> world)
    {
        int birth = person.BirthYear, death = person.DeathYear;
        bool Parent(ushort id) => !world.TryGetValue(id, out var parent) ||
            parent.BirthYear >= birth - 50 && parent.BirthYear <= birth - 14 && birth <= parent.DeathYear;
        bool spouse = world.TryGetValue(person.SpouseId, out var partner) &&
            !(partner.BirthYear >= birth - 40 && partner.BirthYear <= birth + 40 && death - partner.BirthYear >= 14 && partner.DeathYear - birth >= 14);
        bool children = false;
        // 205E90 returns after the first matching valid world child.
        foreach (var child in world.Values)
            if (child.FatherId == person.Id || child.MotherId == person.Id)
            { children = !(birth >= child.BirthYear - 50 && birth <= child.BirthYear - 14 && child.BirthYear <= death); break; }
        bool sworn = false;
        if (world.TryGetValue(person.SwornBrotherId, out var anchor))
            foreach (var member in world.Values)
                if (member.Id != person.Id && (member.Id == anchor.Id || member.SwornBrotherId == anchor.Id) &&
                    !(member.BirthYear >= birth - 75 && member.BirthYear <= birth + 75 && death - member.BirthYear >= 14 && member.DeathYear - birth >= 14))
                { sworn = true; break; }
        return new(!Parent(person.FatherId), !Parent(person.MotherId), spouse, children, sworn);
    }
    private static RegisteredOfficerLibrary Shift(RegisteredOfficerLibrary library, RegisteredOfficerBirthYearPlan plan, int birthYear)
    {
        var current = Preview(library, plan.SelectedIds, plan.ReferenceId);
        if (current.OriginalBirthYear != plan.OriginalBirthYear || birthYear < current.MinimumBirthYear || birthYear > current.MaximumBirthYear)
            throw new InvalidDataException("The registered-officer birth-year plan is stale or outside its selected-group range.");
        int delta = birthYear - current.OriginalBirthYear;
        return new([.. library.Officers.Select(person => plan.SelectedIds.Contains(person.Id) ? person with
        { BirthYear = (ushort)Math.Clamp(person.BirthYear + delta, 0, 350), DeathYear = (ushort)Math.Clamp(person.DeathYear + delta, 0, 350) } : person)]);
    }
    private static Dictionary<int, RegisteredOfficerBirthYearPerson> World(RegisteredOfficerLibrary library,
        IReadOnlyList<RegisteredOfficerBirthYearPerson> sourcePeople)
    {
        var rows = sourcePeople.Where(person => person.Id is not (>= 3001 and <= 3150)).Concat(library.Officers.Select(Project))
            .OrderBy(person => person.Slot).ToDictionary(person => person.Id);
        foreach (var person in library.Officers)
            foreach (int id in new[] { (int)person.FatherId, person.MotherId, person.SpouseId, person.SwornBrotherId })
                if (id is >= 5101 and <= 5200 && !rows.ContainsKey(id))
                    throw new InvalidDataException("Birth-year relationship validation needs the referenced installed special-officer definition.");
        return rows;
    }
    private static RegisteredOfficerBirthYearPerson Project(RegisteredOfficer person) => new(1401 + person.Id - 3001,
        person.Id, person.BirthYear, person.DeathYear, person.FamilyId, person.FatherId, person.MotherId, person.SpouseId, person.SwornBrotherId);
    private static int CompareScenarios(S14Entry first, S14Entry second)
    {
        var a = first.SelectionMetadata!; var b = second.SelectionMetadata!;
        int result = (b.FilterKind == 2).CompareTo(a.FilterKind == 2); if (result != 0) return result;
        result = (a.SortSubtypeCode == 2).CompareTo(b.SortSubtypeCode == 2); if (result != 0) return result;
        result = (a.ScenarioKindCode == 0).CompareTo(b.ScenarioKindCode == 0); if (result != 0) return result;
        result = Nullable.Compare(first.StartYear, second.StartYear); if (result != 0) return result;
        result = Nullable.Compare(first.StartMonth, second.StartMonth); if (result != 0) return result;
        result = AlternateMap(a.NativeId).CompareTo(AlternateMap(b.NativeId)); if (result != 0) return result;
        if (a.SortSubtypeCode == 2 && b.SortSubtypeCode == 2)
        { if (a.NativeId == 63 && b.NativeId == 35) return -1; if (a.NativeId == 35 && b.NativeId == 63) return 1; }
        return a.NativeId.CompareTo(b.NativeId);
    }
    private static bool AlternateMap(int id) => id is not (30 or 31 or 34 or 35 or >= 21 and <= 25) &&
        id is >= 9 and <= 50 or >= 150 and < 200;
}

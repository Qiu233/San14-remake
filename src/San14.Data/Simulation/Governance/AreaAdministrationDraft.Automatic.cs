using San14.Core;

namespace San14.Data;

public enum AreaAdministrationAutomaticMode
{
    Occupation = 0,
    Security = 1,
    Commerce = 2,
    Agriculture = 3,
    Barracks = 4,
    Appointment = 5
}

public sealed partial class AreaAdministrationDraft
{
    private sealed record AutomaticPlan(int Books, List<int> Areas,
        List<AreaAdministrationExecution.Candidate>[] Groups, List<byte> Focus,
        List<AreaAdministrationExecution.Candidate> Ordered);

    public AreaAdministrationQuery<bool?> QueryCanAutomatic()
    {
        try
        {
            EnsureAutomaticParent();
            var heldPeople = new List<int>();
            foreach (int area in _held)
            {
                int person = PersonAreaAppointmentSource.Resolve(_current, Person(area));
                if (K(_current.ReadPersonNativeValidity(person), "automatic_held_person_validity")) heldPeople.Add(person);
            }
            int books = AutomaticCommandBooks();
            int changed = CountChangedAreas();
            // The source constructs the complete ordered qualification list,
            // even if the final empty-area or command-book test will fail.
            var candidates = AreaAdministrationExecution.OrderedCandidates(_current, _cityId);
            bool found = false;
            foreach (var candidate in candidates)
            {
                int person = candidate.Physical;
                if (!K(_current.ReadPersonNativeValidity(person), "automatic_candidate_validity")) continue;
                if (heldPeople.Contains(person) &&
                    unchecked(PersonAreaAppointmentSource.AdditionalCount(_state, _current, person) + 1) <= _vectors.Count(person)) continue;
                if (!AutomaticCandidateAvailable(candidate)) continue;
                found = true;
                break;
            }
            return AreaAdministrationQuery<bool?>.Known(_held.Length > heldPeople.Count &&
                Math.Max(unchecked(books - changed), 0) > 0 && found);
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<bool?>.Unknown(unknown.Message); }
    }

    public AreaAdministrationDraftResult StageAutomatic(AreaAdministrationAutomaticMode mode)
    {
        AutomaticPlan? plan = null;
        return Atomic(() => ApplyAutomatic(plan!, mode), prepareKnownPeople: () =>
        {
            EnsureAutomaticParent();
            if (mode is < AreaAdministrationAutomaticMode.Occupation or > AreaAdministrationAutomaticMode.Appointment)
                throw Missing("automatic_mode");
            plan = PrepareAutomatic(mode);
            // Only checkpoint membership uses a set. Candidate records, held
            // order and each nonexclusive capacity group retain duplicates.
            var physical = new HashSet<int>();
            foreach (int area in _held) AddKnownPerson(Person(area));
            foreach (var group in plan.Groups)
                foreach (var candidate in group)
                {
                    physical.Add(candidate.Physical);
                    AddKnownPerson(candidate.Identity);
                }
            return physical;

            void AddKnownPerson(ushort identity)
            {
                Add(identity);
                Add(ClampPerson(identity));
            }
            void Add(ushort identity)
            {
                var reference = identity <= 6000 ? _current.ReadPersonLookupReference(identity) :
                    _current.ReadTransferPersonTableReference(0);
                if (reference?.PhysicalSlot is int person) physical.Add(person);
            }
        });
    }

    private AutomaticPlan PrepareAutomatic(AreaAdministrationAutomaticMode mode)
    {
        int books = AutomaticCommandBooks();
        var areas = new List<int>();
        foreach (int area in _held)
        {
            int person = PersonAreaAppointmentSource.Resolve(_current, Person(area));
            if (!K(_current.ReadPersonNativeValidity(person), "automatic_held_person_validity")) areas.Add(area);
        }
        AreaAdministrationAutomaticOrder.SortAreas(areas, (left, right) => AutomaticAreaBefore(left, right, mode));
        var candidates = AreaAdministrationExecution.OrderedCandidates(_current, _cityId);
        List<AreaAdministrationExecution.Candidate>[] groups = [[], [], []];
        foreach (var candidate in candidates)
        {
            if (!AutomaticCandidateAvailable(candidate)) continue;
            int person = candidate.Physical;
            if (_vectors.Count(person) < 1 || _vectors.Entry(person, 0) == 0) groups[0].Add(candidate);
            if (unchecked(PersonAreaAppointmentSource.AdditionalCount(_state, _current, person) + 1) > 1 &&
                (_vectors.Count(person) < 2 || _vectors.Entry(person, 1) == 0)) groups[1].Add(candidate);
            if (unchecked(PersonAreaAppointmentSource.AdditionalCount(_state, _current, person) + 1) > 2 &&
                (_vectors.Count(person) < 3 || _vectors.Entry(person, 2) == 0)) groups[2].Add(candidate);
        }

        var focus = new List<byte>();
        var ordered = new List<AreaAdministrationExecution.Candidate>();
        if (mode is AreaAdministrationAutomaticMode.Commerce or AreaAdministrationAutomaticMode.Agriculture or AreaAdministrationAutomaticMode.Barracks)
        {
            foreach (int area in areas) focus.Add(AutomaticRequestedFocus(area, (byte)((int)mode - 1)));
        }
        else
        {
            for (int group = 0; group < groups.Length; group++)
            {
                bool compareCount = group != 0;
                AreaAdministrationAutomaticOrder.Sort(groups[group],
                    (left, right) => AutomaticInitialBefore(left, right, mode, compareCount));
            }
            if (mode == AreaAdministrationAutomaticMode.Occupation)
            {
                foreach (var group in groups)
                {
                    var scores = new List<(AreaAdministrationExecution.Candidate Person, int Score)>();
                    foreach (var person in group)
                        scores.Add((person, AutomaticValue(_previewSource.AutoOccupation(person.Physical, _vectors.Count))));
                    AreaAdministrationAutomaticOrder.StableSort(scores, (left, right) => left.Score > right.Score);
                    group.Clear();
                    foreach (var score in scores) group.Add(score.Person);
                }
                var ratios = new List<(int Area, float Ratio)>();
                foreach (int area in areas)
                {
                    var statistics = _state.RegionStatistics?.Entries.GetValueOrDefault(area) ?? throw Missing("automatic_land_statistics");
                    if (!statistics.IsOwnedCountCurrent) throw Missing("automatic_land_owned_count_current");
                    if (statistics.OwnedCellCount is < 0 or > ushort.MaxValue || statistics.TerrainCellCount is < 0 or > ushort.MaxValue)
                        throw Missing("automatic_land_range");
                    ratios.Add((area, (float)statistics.OwnedCellCount / (float)statistics.TerrainCellCount));
                }
                // A strict float predicate retains the source unordered-NaN
                // behavior, including zero terrain without a denominator fix.
                AreaAdministrationAutomaticOrder.StableSort(ratios, (left, right) => left.Ratio < right.Ratio);
                areas.Clear();
                foreach (var ratio in ratios) areas.Add(ratio.Area);
            }
            foreach (var group in groups) ordered.AddRange(group);
            int pairs = Math.Min(areas.Count, ordered.Count);
            for (int index = 0; index < pairs; index++)
                focus.Add(AutomaticPersonFocus(areas[index], ordered[index].Physical));
        }
        return new(books, areas, groups, focus, ordered);
    }

    private void ApplyAutomatic(AutomaticPlan plan, AreaAdministrationAutomaticMode mode)
    {
        int index = 0;
        if (mode is AreaAdministrationAutomaticMode.Commerce or AreaAdministrationAutomaticMode.Agriculture or AreaAdministrationAutomaticMode.Barracks)
        {
            foreach (var group in plan.Groups)
                while (index < plan.Areas.Count && index < plan.Focus.Count && group.Count != 0 &&
                    unchecked(plan.Books - CountChangedAreas()) > 0)
                {
                    int area = plan.Areas[index]; byte focus = plan.Focus[index];
                    AreaAdministrationAutomaticOrder.Sort(group,
                        (left, right) => AutomaticMatchBefore(left, right, area, focus));
                    StageIdentity(area, group[0].Identity, focus, childStage: false);
                    group.RemoveAt(0);
                    index++;
                }
        }
        else
        {
            while (index < plan.Areas.Count && index < plan.Ordered.Count && index < plan.Focus.Count &&
                unchecked(plan.Books - CountChangedAreas()) > 0)
            {
                StageIdentity(plan.Areas[index], plan.Ordered[index].Identity, plan.Focus[index], childStage: false);
                index++;
            }
        }
    }

    private bool AutomaticCandidateAvailable(AreaAdministrationExecution.Candidate candidate)
    {
        if (candidate.Physical == PersonAreaAppointmentSource.Resolve(_current,
            K(_current.ReadAssignmentCityCA(_cityId), "automatic_recruiter"))) return false;
        if (candidate.Physical == PersonAreaAppointmentSource.Resolve(_current,
            K(_current.ReadAssignmentCityCC(_cityId), "automatic_trainer"))) return false;
        var force = _current.ReadPersonForce(candidate.Physical) ?? throw Missing("automatic_person_force");
        if (candidate.Identity == 0) return true;
        for (int slot = 0; slot < 3; slot++)
            if (K(_current.ReadForceAssociationPersonId(force, slot), "automatic_association_person") == candidate.Identity) return false;
        return true;
    }

    private bool AutomaticAreaBefore(int left, int right, AreaAdministrationAutomaticMode mode)
    {
        int leftBasis = _previewSource.AutomaticAreaBasis(left, (int)mode);
        int rightBasis = _previewSource.AutomaticAreaBasis(right, (int)mode);
        if (leftBasis != rightBasis) return leftBasis > rightBasis;
        var leftArea = _state.Areas.GetValueOrDefault(left) ?? throw Missing("automatic_area_owner");
        var rightArea = _state.Areas.GetValueOrDefault(right) ?? throw Missing("automatic_area_owner");
        int leftSecurity = K(leftArea.Security, "automatic_security");
        int rightSecurity = K(rightArea.Security, "automatic_security");
        if (leftSecurity is < 0 or > byte.MaxValue || rightSecurity is < 0 or > byte.MaxValue) throw Missing("automatic_security_range");
        if (leftSecurity != rightSecurity) return leftSecurity < rightSecurity;
        int leftPopulation = K(leftArea.NativeNormalAftermath?.Quantity52, "automatic_population");
        int rightPopulation = K(rightArea.NativeNormalAftermath?.Quantity52, "automatic_population");
        if (leftPopulation != rightPopulation) return leftPopulation > rightPopulation;
        return left < right;
    }

    private bool AutomaticInitialBefore(AreaAdministrationExecution.Candidate left,
        AreaAdministrationExecution.Candidate right, AreaAdministrationAutomaticMode mode, bool compareCount)
    {
        if (compareCount)
        {
            int rightCount = _vectors.Count(right.Physical), leftCount = _vectors.Count(left.Physical);
            if (rightCount != leftCount) return unchecked(rightCount - leftCount) > 0;
        }
        if (mode == AreaAdministrationAutomaticMode.Security)
        {
            bool rightTrait = _previewSource.AutomaticTrait111(right.Physical);
            bool leftTrait = _previewSource.AutomaticTrait111(left.Physical);
            if (rightTrait != leftTrait) return _previewSource.AutomaticTrait111(left.Physical);
        }
        else
        {
            int sum = 0;
            for (int dimension = 0; dimension < 4; dimension++)
                sum = unchecked(sum + _previewSource.AutomaticAbilityDifference(left.Physical, right.Physical, dimension));
            if (sum != 0) return sum > 0;
            sum = _previewSource.AutomaticAbilityDifference(left.Physical, right.Physical, 0);
            sum = unchecked(sum + _previewSource.AutomaticAbilityDifference(left.Physical, right.Physical, 2));
            sum = unchecked(sum + _previewSource.AutomaticAbilityDifference(left.Physical, right.Physical, 3));
            if (sum != 0) return sum > 0;
        }
        foreach (int dimension in new[] { 0, 4, 3, 2, 1 })
        {
            int difference = _previewSource.AutomaticAbilityDifference(left.Physical, right.Physical, dimension);
            if (difference != 0) return difference > 0;
        }
        return left.Identity < right.Identity;
    }

    private bool AutomaticMatchBefore(AreaAdministrationExecution.Candidate left,
        AreaAdministrationExecution.Candidate right, int area, byte focus)
    {
        int rightCount = _vectors.Count(right.Physical), leftCount = _vectors.Count(left.Physical);
        if (rightCount != leftCount) return unchecked(rightCount - leftCount) > 0;
        int leftScore = AutomaticValue(_previewSource.AutoDevelopment(area, left.Physical, focus, _vectors.Count));
        int rightScore = AutomaticValue(_previewSource.AutoDevelopment(area, right.Physical, focus, _vectors.Count));
        int difference = unchecked(leftScore - rightScore);
        if (difference != 0) return difference > 0;
        foreach (int dimension in new[] { 0, 4 })
        {
            int leftAbility = _previewSource.AutomaticAbility(left.Physical, dimension);
            int rightAbility = _previewSource.AutomaticAbility(right.Physical, dimension);
            difference = unchecked(leftAbility - rightAbility);
            if (difference != 0) return difference > 0;
        }
        return left.Identity < right.Identity;
    }

    private byte AutomaticRequestedFocus(int area, byte requested)
    {
        var progress = AutomaticProgress(area);
        if (progress[requested - 1].Progress < 1000) return requested;
        var sorted = progress.ToList();
        AreaAdministrationAutomaticOrder.Sort(sorted, (left, right) => left.Progress < right.Progress);
        return sorted[0].Progress < sorted[1].Progress ? sorted[0].Focus : AutomaticFallbackFocus(progress);
    }

    private byte AutomaticPersonFocus(int area, int person)
    {
        var progress = AutomaticProgress(area);
        var sorted = progress.ToList();
        AreaAdministrationAutomaticOrder.Sort(sorted, (left, right) => left.Progress < right.Progress);
        if (sorted[0].Progress < sorted[1].Progress) return sorted[0].Focus;
        if (_previewSource.AutomaticAbility(person, 3) >= 60 && progress[1].Progress < 1000) return 2;
        if (_previewSource.AutomaticAbility(person, 2) >= 60 && progress[0].Progress < 1000) return 1;
        if (_previewSource.AutomaticAbility(person, 1) >= 60 && progress[2].Progress < 1000) return 3;
        return AutomaticFallbackFocus(progress);
    }

    private (byte Focus, ushort Progress)[] AutomaticProgress(int area)
    {
        var row = _state.Areas.GetValueOrDefault(area) ?? throw Missing("automatic_area_owner");
        return [(1, Read(AreaDevelopmentFocus.Commerce)), (2, Read(AreaDevelopmentFocus.Agriculture)),
            (3, Read(AreaDevelopmentFocus.Barracks))];
        ushort Read(AreaDevelopmentFocus focus)
        {
            int value = Progress(row, focus) ?? throw Missing("automatic_progress");
            if (value is < 0 or > ushort.MaxValue) throw Missing("automatic_progress_range");
            return (ushort)value;
        }
    }

    private static byte AutomaticFallbackFocus((byte Focus, ushort Progress)[] progress) =>
        progress[1].Progress < 1000 ? (byte)2 : progress[0].Progress < 1000 ? (byte)1 :
        progress[2].Progress < 1000 ? (byte)3 : (byte)0;

    private int AutomaticCommandBooks()
    {
        var city = _current.ReadCityTableReference(_cityId) ?? throw Missing("automatic_city_pointer");
        var district = _current.ReadCityDistrictReference(city) ?? throw Missing("automatic_district_pointer");
        int id = K(district.PhysicalSlot, "automatic_district_identity");
        var row = _state.Districts.GetValueOrDefault(id) ?? throw Missing("automatic_district_owner");
        int books = K(row.OrdersRemaining, "automatic_command_books");
        if (books is < 0 or > byte.MaxValue) throw Missing("automatic_command_books_range");
        return books;
    }

    private void EnsureAutomaticParent()
    {
        EnsureOpen();
        if (_child is not null) throw Missing("child_still_open");
    }

    private static int AutomaticValue(AreaAdministrationQuery<int?> value) =>
        value.Available && value.Value.HasValue ? value.Value.Value :
            throw new InvalidDataException(value.UnresolvedInput ?? "area-draft.automatic_preview_value");
}

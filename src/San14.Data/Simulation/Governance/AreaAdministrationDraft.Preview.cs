namespace San14.Data;

public sealed partial class AreaAdministrationDraft
{
    private readonly record struct IncomePreviewValues(
        AreaAdministrationQuery<int?> GoldBefore, AreaAdministrationQuery<int?> GoldAfter,
        AreaAdministrationQuery<int?> FoodBefore, AreaAdministrationQuery<int?> FoodAfter,
        AreaAdministrationQuery<int?> SoldiersBefore, AreaAdministrationQuery<int?> SoldiersAfter);

    private IncomePreviewValues IncomePreview()
    {
        SourceWorldContext.AreaPreviewCheckpoint? checkpoint = null;
        var original = _vectors;
        var goldBefore = Unknown("gold_membership"); var goldAfter = goldBefore;
        var foodBefore = Unknown("food_membership"); var foodAfter = foodBefore;
        var soldiersBefore = Unknown("soldiers_membership"); var soldiersAfter = soldiersBefore;
        bool beforeEvaluated = false;
        try
        {
            int area = Active();
            ushort baseline = _parent.People[Array.IndexOf(_held, area)];
            int baselinePerson = PersonAreaAppointmentSource.Resolve(_current, baseline);
            int? previousArea = null;
            if (K(_current.ReadPersonNativeValidity(baselinePerson), "gold_baseline_person_validity"))
            {
                int additional = PersonAreaAppointmentSource.AdditionalCount(_state, _current, baselinePerson);
                if (_vectors.Count(baselinePerson) >= unchecked(additional + 1))
                {
                    int first = _vectors.Entry(baselinePerson, 0);
                    if (first < 0) throw Missing("gold_previous_area_index");
                    var prior = _current.ReadAreaTableReference(first <= 500 ? first : 0) ?? throw Missing("gold_previous_area_pointer");
                    if (K(_current.ReadAreaNativeValidity(prior), "gold_previous_area_validity"))
                        previousArea = K(prior.PhysicalSlot, "gold_previous_area_identity");
                }
            }
            int[] touched = previousArea is int previous ? [area, previous] : [area];
            ushort currentRaw = previousArea == area ? (ushort)0 : Person(area);
            // R may be selected itself: C is then the Person0 pointer loaded
            // AFTER its tentative clear. Preflight only collects known write
            // targets; unknown late restore inputs must not suppress before.
            int? currentPerson = KnownPointer(currentRaw);
            ushort? identity = currentPerson is int physical ? _current.ReadPersonIdentity(physical) : null;
            var ids = touched.Select(Person).Append(baseline).Append((ushort)0);
            if (identity is ushort knownIdentity) ids = ids.Append(knownIdentity);
            var rawIds = ids.ToArray();
            var people = rawIds.Concat(rawIds.Select(ClampPerson)).Distinct().Select(KnownPointer)
                .Where(p => p.HasValue).Select(p => p!.Value);
            checkpoint = _current.CaptureAreaPreview(touched, people);
            _vectors = original.Fork();
            if (previousArea is int old && Person(old) != 0) SetPerson(old, 0);
            SetPerson(area, baseline);
            goldBefore = _previewSource.Gold(area, _vectors.Count);
            foodBefore = _previewSource.Food(area, _vectors.Count);
            soldiersBefore = _previewSource.Soldiers(area); beforeEvaluated = true;
            int savedPerson = currentPerson ?? throw Missing("gold_restore_person_pointer");
            ushort restorePerson = K(_current.ReadPersonIdentity(savedPerson), "gold_restore_person_identity");
            SetPerson(area, restorePerson);
            if (previousArea is int priorArea) SetPerson(priorArea, baseline);
            goldAfter = _previewSource.Gold(area, _vectors.Count);
            foodAfter = _previewSource.Food(area, _vectors.Count);
            soldiersAfter = _previewSource.Soldiers(area);
        }
        catch (InvalidDataException unknown)
        {
            if (!beforeEvaluated)
            {
                goldBefore = AreaAdministrationQuery<int?>.Unknown(unknown.Message);
                foodBefore = AreaAdministrationQuery<int?>.Unknown(unknown.Message);
                soldiersBefore = AreaAdministrationQuery<int?>.Unknown(unknown.Message);
            }
            goldAfter = AreaAdministrationQuery<int?>.Unknown(unknown.Message);
            foodAfter = AreaAdministrationQuery<int?>.Unknown(unknown.Message);
            soldiersAfter = AreaAdministrationQuery<int?>.Unknown(unknown.Message);
        }
        finally
        {
            if (checkpoint is not null) _current.RestoreAreaPreview(checkpoint);
            _vectors = original;
        }
        return new(goldBefore, goldAfter, foodBefore, foodAfter, soldiersBefore, soldiersAfter);

        int? KnownPointer(ushort id)
        {
            try { return PersonAreaAppointmentSource.Resolve(_current, id); }
            catch (InvalidDataException) { return null; } // Reached reads still fail at their original operation.
        }
    }

    private (AreaAdministrationQuery<int?> Extent, AreaAdministrationQuery<bool?> Highlight) OccupationPreview()
    {
        var checkpoint = _current.CaptureAreaPreview([], [], captureCityDistricts: true);
        try
        {
            int area = Active();
            ushort raw = Person(area);
            int person = PersonAreaAppointmentSource.Resolve(_current, raw);
            return (_previewSource.Occupation(area, person, _vectors.Count),
                _previewSource.OccupationHighlight(raw));
        }
        finally { _current.RestoreAreaPreview(checkpoint); }
    }

    public AreaAdministrationQuery<AreaAdministrationDevelopmentPreview> QueryDevelopmentPreview() => DevelopmentPreview(null);

    //341D30's finite candidate evaluator, shared with future selector getters.
    //This does not admit or stage a public officer-selection command.
    internal AreaAdministrationQuery<AreaAdministrationDevelopmentPreview> QueryCandidateDevelopmentPreview(int officerId) => DevelopmentPreview(officerId);

    private AreaAdministrationQuery<AreaAdministrationDevelopmentPreview> DevelopmentPreview(int? candidate)
    {
        SourceWorldContext.AreaPreviewCheckpoint? checkpoint = null;
        var original = _vectors;
        try
        {
            int area = Active();
            ushort raw = Person(area);
            ushort desired = candidate is int id ? id is >= 0 and <= 6000 ? (ushort)id : throw Missing("preview_candidate_identity") : raw;
            int person = PersonAreaAppointmentSource.Resolve(_current, desired);
            int? previousArea = null;
            if (candidate.HasValue && raw != desired && K(_current.ReadPersonNativeValidity(person), "preview_candidate_validity"))
            {
                int additional = PersonAreaAppointmentSource.AdditionalCount(_state, _current, person);
                if (_vectors.Count(person) >= unchecked(additional + 1))
                {
                    int first = _vectors.Entry(person, 0);
                    if (first < 0) throw Missing("preview_candidate_area_index");
                    var reference = _current.ReadAreaTableReference(first <= 500 ? first : 0) ?? throw Missing("preview_candidate_area_pointer");
                    if (K(_current.ReadAreaNativeValidity(reference), "preview_candidate_area_validity"))
                        previousArea = K(reference.PhysicalSlot, "preview_candidate_area_identity");
                }
            }
            int[] touched = previousArea is int prior ? [area, prior] : [area];
            var ids = touched.Select(Person).Append(desired);
            var people = ids.Select(p => PersonAreaAppointmentSource.Resolve(_current, p));
            checkpoint = _current.CaptureAreaPreview(touched, people);
            _vectors = original.Fork();
            if (candidate.HasValue && raw != desired)
            {
                if (previousArea is int old && Person(old) != 0) SetPerson(old, 0);
                SetPerson(area, desired);
            }
            AreaAdministrationDevelopmentValues Values(bool full) => new(
                _previewSource.Development(area, person, 1, full, _vectors.Count, candidate.HasValue),
                _previewSource.Development(area, person, 2, full, _vectors.Count, candidate.HasValue),
                _previewSource.Development(area, person, 3, full, _vectors.Count, candidate.HasValue),
                _previewSource.Security(area, person, full, _vectors.Count));
            return AreaAdministrationQuery<AreaAdministrationDevelopmentPreview>.Known(new(area, Revision, Values(false), Values(true)));
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<AreaAdministrationDevelopmentPreview>.Unknown(unknown.Message); }
        finally
        {
            if (checkpoint is not null) _current.RestoreAreaPreview(checkpoint);
            _vectors = original;
        }
    }
}

using San14.Core;

namespace San14.Data;

/// <summary>One city's two-level UI projection. Only the existing formal order
/// writer publishes state. Source cancel and exact failure rollback are distinct.</summary>
public sealed partial class AreaAdministrationDraft : IDisposable
{
    private sealed record Snapshot(ushort[] People, byte[] Focus, ushort Recruiter, ushort Trainer,
        (ushort Person, byte Target)[] Associations);
    private readonly GameState _state;
    private readonly SourceWorldContext _current;
    private readonly IReadOnlyList<AreaPreviewRecord> _areaDefinitions;
    private readonly AreaAdministrationOfficerCardSource _officerCardSource;
    private readonly AreaAdministrationNarrationSource _narrationSource;
    private readonly AreaAdministrationPreviewSource _previewSource;
    private readonly int _cityId, _forceId;
    private readonly bool _forceValid;
    private readonly int[] _held;
    private readonly Snapshot _parent;
    private Snapshot? _child;
    private int? _area;
    private bool _closed;
    private PersonAssignmentPrecallInputs.AreaAppointmentDraft _vectors;
    public long Revision { get; private set; }
    public IReadOnlyList<int> HeldAreaIds { get; }
    public int? ActiveAreaId => _area;

    internal AreaAdministrationDraft(GameState state, SourceWorldContext current, CityState city,
        IReadOnlyList<AreaPreviewRecord> areaDefinitions, AreaAdministrationOfficerCardSource officerCardSource,
        AreaAdministrationNarrationSource narrationSource, AreaAdministrationPreviewSource previewSource)
    {
        _state = state; _current = current; _cityId = city.Id; _areaDefinitions = areaDefinitions;
        _officerCardSource = officerCardSource; _narrationSource = narrationSource; _previewSource = previewSource;
        var sourceCity = current.ReadCityTableReference(city.Id) ?? throw Missing("city_pointer");
        if (K(sourceCity.PhysicalSlot, "city_identity") != city.Id) throw Missing("city_domain_identity");
        if (!K(current.ReadBaseNativeValidity(sourceCity), "city_validity")) throw Missing("invalid_city");
        int rawCityForce = K(current.ReadBaseForceSlot(sourceCity), "city_force");
        var force = current.ReadForceTableReference(rawCityForce <= 51 ? rawCityForce : 0) ?? throw Missing("force_pointer");
        _forceId = K(force.PhysicalSlot, "force_identity");
        _forceValid = K(current.ReadForceNativeValidity(force), "force_validity");
        var representative = CurrentGateMapQueries.FindArea(sourceCity, current);
        int representativeId = K(representative.PhysicalSlot, "representative_area");
        int cityHex = K(CurrentGateMapQueries.FindHex(sourceCity, current).PhysicalSlot, "city_hex");
        var held = new List<int> { representativeId };
        foreach (int area in RegisteredAreas(current))
        {
            if (K(current.ReadAreaCitySlot(new(area)), "held_area_city") != city.Id) continue;
            int hex = K(current.ReadAreaHexIndex(new(area)), "held_area_hex");
            var reference = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("held_hex_pointer");
            if (K(current.ReadHexForceSlot(reference), "held_area_owner") == rawCityForce && hex != cityHex) held.Add(area);
        }
        _held = held.ToArray(); HeldAreaIds = Array.AsReadOnly(_held);
        _vectors = new(state); _parent = Capture();
    }

    public AreaAdministrationDraftResult BeginArea(int areaId) => Atomic(() =>
    {
        if (_child is not null) throw Missing("child_already_open");
        if (!_held.Contains(areaId)) throw Missing("area_outside_held_list");
        _child = Capture(); _area = areaId;
    });

    public AreaAdministrationQuery<AreaAdministrationPanel> QueryPanel()
    {
        try
        {
            int area = Active(); ushort person = Person(area); byte focus = Focus(area);
            int physical = PersonAreaAppointmentSource.Resolve(_current, person);
            bool valid = K(_current.ReadPersonNativeValidity(physical), "panel_person_validity");
            var row = _state.Areas.GetValueOrDefault(area) ?? throw Missing("area_domain");
            var options = new[] { AreaDevelopmentFocus.Commerce, AreaDevelopmentFocus.Agriculture, AreaDevelopmentFocus.Barracks }
                .Select(f =>
                {
                    int? progress = Progress(row, f);
                    return new AreaAdministrationFocusOption(f, progress, !valid ? false : progress.HasValue ? progress <= 999 : null,
                        valid && focus == EncodeFocus(f) && progress is <= 999, valid && !progress.HasValue ? "area-draft.focus_progress" : null);
                }).ToArray();
            int changed = CountChangedAreas();
            var parentStatus = QueryParentStatus();
            var preview = QueryDevelopmentPreview();
            var occupation = OccupationPreview();
            var income = IncomePreview();
            var development = preview.Available ? focus switch
            {
                1 => preview.Value!.Normal.CommerceIncrease, 2 => preview.Value!.Normal.AgricultureIncrease,
                3 => preview.Value!.Normal.BarracksIncrease, _ => AreaAdministrationQuery<int?>.Known(0)
            } : AreaAdministrationQuery<int?>.Unknown(preview.UnresolvedInput!);
            var securityDelta = preview.Available ? preview.Value!.Normal.SecurityIncrease : AreaAdministrationQuery<int?>.Unknown(preview.UnresolvedInput!);
            return AreaAdministrationQuery<AreaAdministrationPanel>.Known(new(area, valid ? person : null,
                valid ? AreaAdministration.DecodeFocus(focus) : AreaDevelopmentFocus.None, row.NativeNormalAftermath?.Quantity52, Array.AsReadOnly(options),
                occupation.Extent, occupation.Highlight,
                development, securityDelta,
                income.GoldBefore, income.GoldAfter,
                income.FoodBefore, income.FoodAfter,
                income.SoldiersBefore, income.SoldiersAfter, changed,
                parentStatus.Available ? parentStatus.Value!.CommandBooks.Required.Value : null)
            {
                SecurityBefore = row.Security is int security ? AreaAdministrationQuery<int?>.Known(security) : Unknown("security_current"),
                SecurityAfter = row.Security is int before && securityDelta.Available && securityDelta.Value is int delta
                    ? AreaAdministrationQuery<int?>.Known(Math.Clamp(unchecked(before + delta), 0, 100))
                    : AreaAdministrationQuery<int?>.Unknown(securityDelta.UnresolvedInput ?? "area-draft.security_current"),
                ParentStatus = parentStatus, ChildStatus = QueryChildStatus()
            });
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<AreaAdministrationPanel>.Unknown(unknown.Message); }
    }

    public AreaAdministrationQuery<IReadOnlyList<int>> QueryPersonCandidates() =>
        AreaAdministrationQuery<IReadOnlyList<int>>.Unknown("area-draft.initial_sort_ability4_weight_and_seven_page_contract");

    public AreaAdministrationDraftResult StageFocus(AreaDevelopmentFocus focus) => Atomic(() =>
    {
        int area = Active();
        if (focus is not (AreaDevelopmentFocus.Commerce or AreaDevelopmentFocus.Agriculture or AreaDevelopmentFocus.Barracks))
            throw Missing("focus_choice");
        int person = PersonAreaAppointmentSource.Resolve(_current, Person(area));
        if (!K(_current.ReadPersonNativeValidity(person), "focus_person_validity")) throw Missing("focus_requires_person");
        int progress = Progress(_state.Areas[area], focus) ?? throw Missing("focus_progress");
        if (progress > 999) throw Missing("focus_complete");
        _current.SetAssignmentArea72(area, EncodeFocus(focus)); //7546B0 identity-equal path only.
    });

    public AreaAdministrationDraftResult Dismiss() => Atomic(() => StageIdentity(Active(), 0, 0, true), activeVector: true);

    //6B7820 acts on the complete parent held chain, independently of the
    //display selection. It neither restores the parent nor opens a child.
    public AreaAdministrationDraftResult DismissAll() => Atomic(() =>
    {
        if (_child is not null) throw Missing("child_still_open");
        foreach (int area in _held) Clear(area);
    }, allVectors: true);

    public AreaAdministrationDraftResult CancelArea() => Atomic(() =>
    {
        _ = Active(); RestoreSource(_child!); _child = null; _area = null;
    }, allVectors: true);

    public AreaAdministrationDraftResult AcceptArea() => Atomic(() =>
    {
        int area = Active(); ushort person = Person(area); byte focus = Focus(area);
        RestoreSource(_child!); StageIdentity(area, person, focus, false);
        _child = null; _area = null;
    }, allVectors: true);

    public AreaAdministrationDraftResult CancelParent() => Atomic(() =>
    {
        if (_child is not null) throw Missing("child_still_open");
        RestoreSource(_parent); _closed = true;
    }, allVectors: true);

    public AreaAdministrationQuery<IReadOnlyList<AreaAdministrationChange>> GetCommitChanges()
    {
        try
        {
            EnsureOpen(); if (_child is not null) throw Missing("child_still_open");
            return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationChange>>.Known(Changes().AsReadOnly());
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<IReadOnlyList<AreaAdministrationChange>>.Unknown(unknown.Message); }
    }

    //7546B0 child stage /754B70 parent stage. New officer selection is deliberately
    //not public until its actual source sorting and all selector pages are closed.
    private void StageIdentity(int area, ushort desired, byte focus, bool childStage)
    {
        ushort old = Person(area);
        if (old != desired)
        {
            int incoming = PersonAreaAppointmentSource.Resolve(_current, desired);
            foreach (int other in _held)
            {
                if (other == area || Person(other) != desired) continue;
                int additional = PersonAreaAppointmentSource.AdditionalCount(_state, _current, incoming);
                if (additional == 0) Clear(other);
                else
                {
                    int capacity = unchecked(PersonAreaAppointmentSource.AdditionalCount(_state, _current, incoming) + 1);
                    if (_vectors.Count(incoming) >= capacity)
                        throw Missing("replacement_selected_old_area_current");
                }
            }
            var snapshot = childStage ? _child! : _parent;
            if (snapshot.Recruiter == desired) _current.SetAssignmentCityCA(_cityId, 0);
            if (snapshot.Trainer == desired) _current.SetAssignmentCityCC(_cityId, 0);
            if (_forceValid)
            {
                var clear = PersonAssignmentPrecallSource.Apply2783B0ForCapture(_current, new(_forceId), incoming);
                if (!clear.NativeReturn.HasValue) throw new InvalidDataException(clear.UnresolvedOwner);
                if (clear.Reports.Count != 0) throw Missing("association_report_consumer");
            }
            if (_parent.Recruiter == old) _current.SetAssignmentCityCA(_cityId, ClampPerson(_parent.Recruiter));
            if (_parent.Trainer == old) _current.SetAssignmentCityCC(_cityId, ClampPerson(_parent.Trainer));
            if (_forceValid)
                for (int i = 0; i < 3; i++)
                    if (_parent.Associations[i].Person == (childStage ? desired : old))
                    {
                        RestoreAssociation(i, _parent.Associations[i]);
                        if (childStage) break;
                    }
            if (childStage && K(_current.ReadPersonNativeValidity(PersonAreaAppointmentSource.Resolve(_current, old)), "old_person_validity"))
            {
                Clear(area);
                for (int i = 0; i < _held.Length; i++)
                    if (snapshot.People[i] == old && Person(_held[i]) != old)
                    { SetPerson(_held[i], old); _current.SetAssignmentArea72(_held[i], ClampFocus(snapshot.Focus[i])); }
            }
            SetPerson(area, desired);
        }
        _current.SetAssignmentArea72(area, ClampFocus(focus));
    }

    //742DA0: two ordered passes, then scalar snapshot restoration. This may
    //normalize a legally inconsistent vector; it is not a byte-copy rollback.
    private void RestoreSource(Snapshot snapshot)
    {
        foreach (int area in _held) Clear(area);
        for (int i = 0; i < _held.Length; i++)
        { SetPerson(_held[i], snapshot.People[i]); _current.SetAssignmentArea72(_held[i], ClampFocus(snapshot.Focus[i])); }
        _current.SetAssignmentCityCA(_cityId, ClampPerson(snapshot.Recruiter)); _current.SetAssignmentCityCC(_cityId, ClampPerson(snapshot.Trainer));
        if (_forceValid)
            for (int i = 0; i < 3; i++) RestoreAssociation(i, snapshot.Associations[i]);
    }

    private void Clear(int area) { SetPerson(area, 0); _current.SetAssignmentArea72(area, 0); }
    private void SetPerson(int area, ushort desired)
    {
        ushort old = Person(area); if (old == desired) return;
        ushort clamped = ClampPerson(desired);
        _current.SetAssignmentArea36(area, clamped);
        _ = PersonAreaAppointmentSource.Change(_state, _current, _vectors, area, clamped, old);
    }
    private void RestoreAssociation(int slot, (ushort Person, byte Target) association) =>
        _current.SetAssignmentForceAssociation(_forceId, slot, ClampPerson(association.Person), (byte)Math.Min(association.Target, (byte)51));
    private static ushort ClampPerson(ushort person) => Math.Min(person, (ushort)6000);
    private static byte ClampFocus(byte focus) => Math.Min(focus, (byte)3);
    private Snapshot Capture() => new(_held.Select(Person).ToArray(), _held.Select(Focus).ToArray(),
        K(_current.ReadAssignmentCityCA(_cityId), "recruiter"), K(_current.ReadAssignmentCityCC(_cityId), "trainer"),
        _forceValid ? Enumerable.Range(0, 3).Select(i =>
            (K(_current.ReadForceAssociationPersonId(new(_forceId), i), "association_person"),
             K(_current.ReadForceAssociationTargetSlot(new(_forceId), i), "association_target"))).ToArray() : []);
    private List<AreaAdministrationChange> Changes()
    {
        var result = new List<AreaAdministrationChange>();
        for (int i = 0; i < _held.Length; i++)
        {
            ushort person = Person(_held[i]); byte focus = Focus(_held[i]);
            if (person != _parent.People[i] || focus != _parent.Focus[i])
                result.Add(new(_held[i], person == 0 ? null : person, AreaAdministration.DecodeFocus(focus)));
        }
        return result;
    }
    private AreaAdministrationDraftResult Atomic(Action operation, bool allVectors = false, bool activeVector = false,
        Func<IEnumerable<int>>? prepareKnownPeople = null)
    {
        SourceWorldContext.AreaAdministrationCheckpoint? checkpoint = null;
        var beforeVectors = _vectors.Fork(); var child = _child; var area = _area; bool closed = _closed;
        bool completed = false;
        try
        {
            EnsureOpen();
            if (prepareKnownPeople is not null)
            {
                // Preparation can allocate source temporary lists. Capture the
                // scalar/list owner first without resolving unused people.
                checkpoint = _current.CaptureAreaAdministrationCheckpoint(_held, _cityId, _forceId, []);
                _current.CaptureAreaAdministrationVectors(checkpoint, prepareKnownPeople());
            }
            else
            {
                IEnumerable<ushort> ids = allVectors ? _held.Select(Person).Concat(_parent.People).Concat(_child?.People ?? []) :
                    activeVector ? new ushort[] { Person(Active()), 0 } : [];
                //742DA0 compares raw snapshot IDs, then clamps before Change.
                if (allVectors) ids = ids.Concat(ids.Select(ClampPerson)).Distinct();
                var people = ids.Select(id => PersonAreaAppointmentSource.Resolve(_current, id));
                checkpoint = _current.CaptureAreaAdministrationCheckpoint(_held, _cityId, _forceId, people);
            }
            operation(); Revision++; completed = true; return new(true);
        }
        catch (InvalidDataException unknown)
        {
            return new(false, unknown.Message);
        }
        finally
        {
            if (checkpoint is not null)
            {
                if (completed) _current.RestoreAreaAdministrationTemporaryLists(checkpoint);
                else _current.RestoreAreaAdministrationCheckpoint(checkpoint);
            }
            if (!completed)
            { _vectors = beforeVectors; _child = child; _area = area; _closed = closed; }
        }
    }
    private int Active() { EnsureOpen(); return _area ?? throw Missing("no_active_child"); }
    private void EnsureOpen() { if (_closed) throw Missing("closed"); }
    private ushort Person(int area) => K(_current.ReadAssignmentArea36(area), "area_person");
    private byte Focus(int area) => K(_current.ReadAssignmentArea72(area), "area_focus");
    private static int? Progress(AreaState area, AreaDevelopmentFocus focus) => focus switch
    { AreaDevelopmentFocus.Commerce => area.Development?.Commerce, AreaDevelopmentFocus.Agriculture => area.Development?.Agriculture,
      AreaDevelopmentFocus.Barracks => area.Development?.Barracks, _ => null };
    private static AreaAdministrationQuery<int?> Unknown(string key) => AreaAdministrationQuery<int?>.Unknown("area-draft." + key);
    private static byte EncodeFocus(AreaDevelopmentFocus focus) => focus switch
    { AreaDevelopmentFocus.Commerce => 1, AreaDevelopmentFocus.Agriculture => 2, AreaDevelopmentFocus.Barracks => 3,
      AreaDevelopmentFocus.None => 0, _ => throw Missing("focus") };
    private static IEnumerable<int> RegisteredAreas(SourceWorldContext current)
    {
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Area) ?? throw Missing("area_registry");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("area_head");
        while (node.Identity.HasValue)
        {
            var value = current.ReadRegistryNodePayload(node) ?? throw Missing("area_payload");
            if (value.Kind != RegistryKind.Area) throw Missing("area_payload_kind");
            yield return K(value.PhysicalSlot, "area_pointer");
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("area_next");
        }
    }
    public void Dispose() { _closed = true; _child = null; _area = null; }
    private static T K<T>(T? value, string key) where T : struct => value ?? throw Missing(key);
    private static InvalidDataException Missing(string key) => new("area-draft." + key);
}

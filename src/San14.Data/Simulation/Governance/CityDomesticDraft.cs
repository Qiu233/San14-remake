using San14.Core;

namespace San14.Data;

/// <summary>Discardable city-page projection. No public-state writer or formal
/// confirmation is provided. Source Cancel and precise failure rollback differ.</summary>
public sealed partial class CityDomesticDraft : IDisposable
{
    private sealed record Snapshot(ushort[] People, byte[] Focus, ushort Recruiter, ushort Trainer,
        (ushort Person, byte Target)[] Associations);
    private readonly GameState _state;
    private readonly SourceWorldContext _current;
    private readonly CityState _city;
    private readonly ArmyOperationContext _definitions;
    private readonly LoadedScenario _scenario;
    private readonly string _language;
    private readonly OwnedBaseCapacityInputs _panels;
    private readonly bool _capacityBound;
    private readonly AreaAdministrationOfficerCardSource _card;
    private readonly AreaAdministrationPreviewSource _preview;
    private readonly CityDomesticNarrationSource? _narrationSource;
    private readonly OwnedArmyGovernorComparatorInputs? _governorInputs;
    private readonly GameResourceRepository? _personResources;
    private readonly int[] _held;
    private readonly int _forceId;
    private readonly bool _forceValid;
    private readonly Snapshot _original;
    private PersonAssignmentPrecallInputs.AreaAppointmentDraft _vectors;
    private SourceWorldContext.AreaAdministrationCheckpoint? _checkpoint;
    private readonly List<CityDomesticStageOperation> _operations = [];
    private readonly HashSet<int> _touchedCities = [], _touchedAreas = [];
    private readonly HashSet<(int Force, int Slot)> _touchedAssociations = [];
    private bool _closed;
    private CityDomesticPolicy _policy, _cachedPolicy;
    private bool _delegatedEnabled = true;
    private CityDomesticPolicyState? _policyState;
    public long Revision { get; private set; }
    public IReadOnlyList<int> HeldAreaIds { get; }
    public int CityId => _city.Id;
    internal CityDomesticDraft(GameState state, SourceWorldContext current, CityState city,
        ArmyOperationContext definitions, LoadedScenario scenario, string language, OwnedBaseCapacityInputs panels,
        bool capacityBound, AreaAdministrationOfficerCardSource card, AreaAdministrationPreviewSource preview, bool initializePolicy = true,
        CityDomesticNarrationSource? narrationSource = null, OwnedArmyGovernorComparatorInputs? governorInputs = null, GameResourceRepository? personResources = null)
    {
        _state = state; _current = current; _city = city; _definitions = definitions; _scenario = scenario;
        _language = language; _panels = panels; _capacityBound = capacityBound; _card = card; _preview = preview; _narrationSource = narrationSource; _governorInputs = governorInputs; _personResources = personResources;
        var receiver = City();
        if (K(receiver.PhysicalSlot, "city_identity") != city.Id) throw Missing("city_domain_identity");
        if (!K(current.ReadBaseNativeValidity(receiver), "city_validity")) throw Missing("invalid_city");
        int rawForce = K(current.ReadBaseForceSlot(receiver), "city_force");
        var force = Force(receiver); _forceId = K(force.PhysicalSlot, "force_identity");
        _forceValid = K(current.ReadForceNativeValidity(force), "force_validity");
        var held = new List<int>();
        foreach (int area in RegisteredAreas())
        {
            if (city.Id is < 1 or > 51 || K(current.ReadAreaCitySlot(new(area)), "iterator_city") != city.Id) continue;
            if (K(current.ReadAreaCurrentForceSlot(new(area)), "held_area_force") == rawForce) held.Add(area);
        }
        _held = held.ToArray(); HeldAreaIds = Array.AsReadOnly(_held); _vectors = new(state);
        _original = new(_held.Select(Person).ToArray(), _held.Select(Focus).ToArray(),
            RoleRaw(CityDomesticDuty.Recruiter), RoleRaw(CityDomesticDuty.Trainer), _forceValid ? Enumerable.Range(0, 3).Select(i =>
                (K(current.ReadForceAssociationPersonId(force, i), "association_person"),
                 K(current.ReadForceAssociationTargetSlot(force, i), "association_target"))).ToArray() : []);
        if (initializePolicy) RefreshPolicy();
    }
    /// <summary>The modal supplies its selected receiver, not an identity
    /// round-trip through the independent lookup table.</summary>
    public AreaAdministrationDraftResult StageDuty(CityDomesticDuty duty, CityDomesticCandidate selected) => Atomic(() =>
    {
        CheckDuty(duty);
        if (selected is null || selected.PhysicalSlot is < 0 or >= 1851) throw Missing("selected_receiver");
        if (K(_current.ReadPersonIdentity(selected.PhysicalSlot), "selected_identity") != selected.RawIdentity)
            throw Missing("selected_identity_binding");
        ushort desired = Stage(duty, selected.PhysicalSlot); RefreshPolicy();
        Record(duty, CityDomesticSelectionKind.SelectReceiver, selected.PhysicalSlot, desired);
    });
    /// <summary>Explicit identity-table query for callers holding a lookup key.
    /// Modal rows must use the receiver overload above.</summary>
    public AreaAdministrationDraftResult StageDutyLookup(CityDomesticDuty duty, int selectedRawIdentity) => Atomic(() =>
    {
        CheckDuty(duty);
        if (selectedRawIdentity is < 0 or > 6000) throw Missing("selected_lookup_range");
        int physical = PersonAreaAppointmentSource.Resolve(_current, (ushort)selectedRawIdentity);
        ushort desired = Stage(duty, physical); RefreshPolicy();
        Record(duty, CityDomesticSelectionKind.SelectReceiver, physical, desired);
    });
    public AreaAdministrationDraftResult Dismiss(CityDomesticDuty duty) => Atomic(() =>
    {
        CheckDuty(duty); int physical = K((_current.ReadTransferPersonTableReference(0) ?? throw Missing("physical_zero_pointer")).PhysicalSlot, "physical_zero");
        ushort desired = Stage(duty, physical); RefreshPolicy();
        Record(duty, CityDomesticSelectionKind.Dismiss, physical, desired);
    });
    public AreaAdministrationDraftResult SetPolicy(CityDomesticPolicy policy)
    {
        try
        {
            EnsureOpen();
            if (policy is < CityDomesticPolicy.Current or > CityDomesticPolicy.Delegated) throw Missing("policy_choice");
            var option = policy switch { CityDomesticPolicy.Commerce => _policyState!.Commerce,
                CityDomesticPolicy.Agriculture => _policyState!.Agriculture, CityDomesticPolicy.Barracks => _policyState!.Barracks,
                CityDomesticPolicy.Delegated => _policyState!.Delegated, _ => AreaAdministrationQuery<bool?>.Known(true) };
            if (option.Value != true) throw Missing(option.UnresolvedInput ?? "policy_disabled");
            //Only a local radio index changes. No assignment checkpoint (and
            //therefore no held/scalar/vector read) is necessary for this event.
            _policy = policy; Revision++; return new(true);
        }
        catch (InvalidDataException unknown) { return new(false, unknown.Message); }
    }
    public AreaAdministrationDraftResult Cancel() => Atomic(() =>
    { RestoreSource(); _closed = true; });
    private void RestoreSource()
    {
        for (int i = 0; i < _held.Length; i++)
        { SetPerson(_held[i], _original.People[i]); SetFocus(_held[i], Math.Min(_original.Focus[i], (byte)3)); }
        SetRole(CityDomesticDuty.Recruiter, Clamp(_original.Recruiter)); SetRole(CityDomesticDuty.Trainer, Clamp(_original.Trainer));
        var force = Force(City());
        if (K(_current.ReadForceNativeValidity(force), "restore_force_validity"))
        {
            if (force.PhysicalSlot != _forceId || !_forceValid) throw Missing("restore_force_changed");
            for (int i = 0; i < 3; i++)
            {
                _current.SetAssignmentForceAssociation(_forceId, i, Clamp(_original.Associations[i].Person), Math.Min(_original.Associations[i].Target, (byte)51));
                _touchedAssociations.Add((_forceId, i));
            }
        }
    }
    private ushort Stage(CityDomesticDuty duty, int incoming)
    {
        ushort desired = K(_current.ReadPersonIdentity(incoming), "incoming_identity");
        ClearAppointments(incoming);
        var other = duty == CityDomesticDuty.Recruiter ? CityDomesticDuty.Trainer : CityDomesticDuty.Recruiter;
        if (RoleRaw(other) == desired) SetRole(other, 0);
        var force = Force(City());
        var clear = PersonAssignmentPrecallSource.Apply2783B0ForDraft(_current, force, incoming);
        if (!clear.NativeReturn.HasValue) throw new InvalidDataException(clear.UnresolvedOwner);
        foreach (var write in clear.Writes)
            if (write.NativeOffset is >= 0x196 and <= 0x19A)
                _touchedAssociations.Add((write.PhysicalSlot, (write.NativeOffset - 0x196) / 2));
        ushort old = RoleRaw(duty); int physical = PersonAreaAppointmentSource.Resolve(_current, old);
        if (K(_current.ReadPersonNativeValidity(physical), "old_person_validity"))
            for (int i = 0; i < _held.Length; i++)
                if (_original.People[i] == old)
                {
                    SetPerson(_held[i], old);
                    if (unchecked(PersonAreaAppointmentSource.AdditionalCount(_state, _current, physical) + 1) <= _vectors.Count(physical)) break;
                }
        SetRole(duty, Clamp(desired));
        return desired;
    }
    private void ClearAppointments(int physical, bool clearFocus = false)
    {
        if (!K(_current.ReadPersonNativeValidity(physical), "incoming_validity")) return;
        //One29C340 loop: UI passes0/0; formal roles pass1/0. Focus72
        //is cleared only in the latter; both preserve6D. Not29C430.
        while (_vectors.Count(physical) > 0)
        {
            int raw = _vectors.Entry(physical, 0);
            if (raw < 0) throw Missing("clear_area_negative_index");
            var area = _current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw Missing("clear_area_pointer");
            int id = K(area.PhysicalSlot, "clear_area_identity");
            if (!K(_current.ReadAreaNativeValidity(area), "clear_area_validity")) throw Missing("clear_vector_no_progress");
            int before = _vectors.Count(physical); SetPerson(id, 0);
            if (clearFocus) SetFocus(id, 0);
            if (_vectors.Count(physical) >= before) throw Missing("clear_vector_no_progress");
        }
    }
    private void SetPerson(int area, ushort desired)
    {
        ushort old = Person(area); if (old == desired) return;
        if (_checkpoint is not null && !_checkpoint.Areas.ContainsKey(area))
            _checkpoint.Areas.Add(area, (old, _current.ReadAssignmentArea72(area)));
        ushort clamped = Clamp(desired);
        if (_checkpoint is not null)
        {
            if (old != 0) _current.CaptureAreaAdministrationVectors(_checkpoint, [PersonAreaAppointmentSource.Resolve(_current, old)]);
            if (clamped != 0) _current.CaptureAreaAdministrationVectors(_checkpoint, [PersonAreaAppointmentSource.Resolve(_current, clamped)]);
        }
        _touchedAreas.Add(area);
        _current.SetAssignmentArea36(area, clamped);
        _ = PersonAreaAppointmentSource.Change(_state, _current, _vectors, area, clamped, old);
    }
    private AreaAdministrationDraftResult Atomic(Action action)
    {
        int operationCount = _operations.Count;
        var cities = _touchedCities.ToArray(); var areas = _touchedAreas.ToArray(); var associations = _touchedAssociations.ToArray();
        var vectors = _vectors.Fork(); var policy = _policy; var cached = _cachedPolicy; bool enabled = _delegatedEnabled; var policyState = _policyState; bool closed = _closed; bool success = false;
        try
        {
            EnsureOpen(); _checkpoint = _current.CaptureAreaAdministrationCheckpoint(_held, CityId, _forceId, []);
            action(); Revision++; success = true; return new(true);
        }
        catch (InvalidDataException unknown) { return new(false, unknown.Message); }
        finally
        {
            if (_checkpoint is not null)
            { if (success) _current.RestoreAreaAdministrationTemporaryLists(_checkpoint); else _current.RestoreAreaAdministrationCheckpoint(_checkpoint); }
            if (!success) { _operations.RemoveRange(operationCount, _operations.Count - operationCount);
                _touchedCities.Clear(); _touchedCities.UnionWith(cities); _touchedAreas.Clear(); _touchedAreas.UnionWith(areas);
                _touchedAssociations.Clear(); _touchedAssociations.UnionWith(associations);
                _vectors = vectors; _policy = policy; _cachedPolicy = cached; _delegatedEnabled = enabled; _policyState = policyState; _closed = closed; }
            _checkpoint = null;
        }
    }
    private ushort RoleRaw(CityDomesticDuty duty) => duty == CityDomesticDuty.Recruiter
        ? K(_current.ReadAssignmentCityCA(CityId), "recruiter") : K(_current.ReadAssignmentCityCC(CityId), "trainer");
    private void SetRole(CityDomesticDuty duty, ushort value)
    { _touchedCities.Add(CityId); if (duty == CityDomesticDuty.Recruiter) _current.SetAssignmentCityCA(CityId, value); else _current.SetAssignmentCityCC(CityId, value); }
    private void SetFocus(int area, byte value)
    { _touchedAreas.Add(area); _current.SetAssignmentArea72(area, value); }
    private void Record(CityDomesticDuty duty, CityDomesticSelectionKind selection, int physical, ushort identity) =>
        _operations.Add(new(duty == CityDomesticDuty.Recruiter ? CityDutyKind.Recruitment : CityDutyKind.Training,
            selection, physical, identity));
    public bool IsForWorld(GameState state) => !_closed && ReferenceEquals(_state, state);
    public AreaAdministrationQuery<CityDomesticCompletionIntent> ExportCompletionIntent(CityDomesticTerminal terminal)
    {
        try
        {
            EnsureOpen();
            if (!Enum.IsDefined(terminal)) throw Missing("terminal");
            return AreaAdministrationQuery<CityDomesticCompletionIntent>.Known(new(CityId, Revision,
                Array.AsReadOnly(_operations.ToArray()), terminal,
                terminal == CityDomesticTerminal.Confirm ? _policy : CityDomesticPolicy.Current));
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<CityDomesticCompletionIntent>.Unknown(unknown.Message); }
    }
    private ushort Person(int area) => K(_current.ReadAssignmentArea36(area), "area_person");
    private byte Focus(int area) => K(_current.ReadAssignmentArea72(area), "area_focus");
    private BaseReference City() => _current.ReadCityTableReference(CityId) ?? throw Missing("city_pointer");
    private ForceReference Force(BaseReference city)
    {
        int raw = K(_current.ReadBaseForceSlot(city), "city_force");
        return _current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("force_pointer");
    }
    private IEnumerable<int> RegisteredAreas()
    {
        var descriptor = _current.ReadRegistryDescriptor(RegistryKind.Area) ?? throw Missing("area_registry");
        var node = _current.ReadRegistryHead(descriptor) ?? throw Missing("area_head");
        while (node.Identity.HasValue)
        {
            var payload = _current.ReadRegistryNodePayload(node) ?? throw Missing("area_payload");
            if (payload.Kind != RegistryKind.Area) throw Missing("area_payload_kind");
            yield return K(payload.PhysicalSlot, "area_payload_pointer");
            node = _current.ReadRegistryNodeNext(node) ?? throw Missing("area_next");
        }
    }
    private static void CheckDuty(CityDomesticDuty duty)
    { if (duty is not (CityDomesticDuty.Recruiter or CityDomesticDuty.Trainer)) throw Missing("duty_choice"); }
    private void EnsureOpen() { if (_closed) throw Missing("closed"); }
    public void Dispose() => _closed = true;
    private static ushort Clamp(ushort value) => Math.Min(value, (ushort)6000);
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("city-domestic." + name);
}

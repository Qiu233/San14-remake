using San14.Core;

namespace San14.Data;

// A complete formatter's actual source-time arguments, never names split from
// a rendered sentence or guessed from the force's domain display label.
internal sealed record CityDomesticAssociationNames(string Officer, string TargetForce);

/// <summary>The same operation-local draft owns UI staging and command replay.
/// No draft projection is accepted as command input. Only this private operation
/// is published after the terminal and every reached late dependency succeed.</summary>
public sealed partial class CityDomesticDraft
{
    internal CommandResult CompleteSource(CompleteCityDomesticOrder order, Func<int?> readReportDay,
        Func<SourceWorldContext, AssociationRemovalReport, CityDomesticAssociationNames?>? readReportNames = null)
    {
        try
        {
            EnsureOpen();
            foreach (var operation in order.Operations)
            {
                CityDomesticDuty duty = operation.Duty == CityDutyKind.Recruitment
                    ? CityDomesticDuty.Recruiter : CityDomesticDuty.Trainer;
                if (operation.ReceiverPhysicalSlot is < 0 or >= 1851) throw Missing("selected_receiver");
                ushort identity = K(_current.ReadPersonIdentity(operation.ReceiverPhysicalSlot), "selected_identity");
                if (identity != operation.ObservedRawIdentity) throw Missing("selected_identity_binding");
                if (operation.Selection == CityDomesticSelectionKind.Dismiss)
                {
                    int zero = K((_current.ReadTransferPersonTableReference(0) ?? throw Missing("physical_zero_pointer")).PhysicalSlot, "physical_zero");
                    if (operation.ReceiverPhysicalSlot != zero) throw Missing("dismiss_receiver_binding");
                }
                else
                {
                    if (!AreaAdministrationExecution.OrderedCandidates(_current, CityId)
                        .Any(x => x.Physical == operation.ReceiverPhysicalSlot && x.Identity == identity))
                        return CommandResult.Reject("Officer is not a current city candidate.", "duty.officer");
                    RequireOfficerJoin(identity, operation.ReceiverPhysicalSlot);
                    if (PlayableDiplomacy.IsOfficerOnMission(_state, identity))
                        return CommandResult.Reject("Officer is serving as a diplomatic envoy.", "diplomacy.executor_busy");
                }
                var staged = operation.Selection == CityDomesticSelectionKind.Dismiss ? Dismiss(duty)
                    : StageDuty(duty, new(identity, operation.ReceiverPhysicalSlot));
                if (!staged.Available) throw new InvalidDataException(staged.UnresolvedInput);
            }

            ushort recruiter = 0, trainer = 0;
            bool changeRecruiter = false, changeTrainer = false;
            if (order.Terminal == CityDomesticTerminal.Confirm)
            {
                _policy = order.Policy;
                var budget = CommandBooks().SourceBudgetCanConfirm;
                if (!budget.Available) throw new InvalidDataException(budget.UnresolvedInput);
                if (budget.Value != true)
                    return CommandResult.Reject("City confirmation has no affordable source command cost.", "duty.source_budget");
                recruiter = RoleRaw(CityDomesticDuty.Recruiter); trainer = RoleRaw(CityDomesticDuty.Trainer);
                changeRecruiter = recruiter != _original.Recruiter; changeTrainer = trainer != _original.Trainer;
            }
            RestoreSource();
            var books = new Dictionary<int, int>();
            var requests = new List<CampaignEffectRequest>();
            if (order.Terminal == CityDomesticTerminal.Confirm)
            {
                if (changeRecruiter) ApplyFormalDuty(CityDomesticDuty.Recruiter, recruiter, books, requests, readReportDay, readReportNames);
                if (changeTrainer) ApplyFormalDuty(CityDomesticDuty.Trainer, trainer, books, requests, readReportDay, readReportNames);
                if (order.Policy != CityDomesticPolicy.Current) ApplyFormalPolicy(order.Policy, books);
            }

            foreach (int city in _touchedCities)
            {
                if (!_state.Cities.ContainsKey(city)) throw Missing("published_city_owner");
                RequireRawJoin(K(_current.ReadAssignmentCityCA(city), "published_recruiter"));
                RequireRawJoin(K(_current.ReadAssignmentCityCC(city), "published_trainer"));
            }
            foreach (int area in _touchedAreas)
            {
                if (!_state.Areas.ContainsKey(area)) throw Missing("published_area_owner");
                RequireRawJoin(K(_current.ReadAssignmentArea36(area), "published_area_person"));
            }
            var result = PersonAssignmentPrecallInputs.PublishAreaAppointments(_state, _current,
                _touchedCities, _touchedAreas, _touchedAssociations);
            result = _vectors.Publish(result);
            if (books.Count != 0)
            {
                var districts = new Dictionary<int, DistrictState>(result.Districts);
                foreach (var (id, remaining) in books)
                {
                    if (remaining < CommandBookAvailability.Reserved(result, id))
                        return CommandResult.Reject("City confirmation would consume reserved command books.", "duty.reserved_orders");
                    districts[id] = districts[id] with { OrdersRemaining = remaining };
                }
                result = result with { Districts = districts };
            }
            if (requests.Count != 0) result = result with { CampaignEffectRequests =
                (result.CampaignEffectRequests ?? throw Missing("association_report_pending_owner"))
                    .Append(CampaignEffectSource.ForceAssociationRemoval,
                        result.NativeClock?.UpdateOrdinal ?? throw Missing("association_report_revision"), requests) };
            // Validate the existing save-bound domain invariants; do not serialize
            // or hash the world to manufacture a command fingerprint.
            StateValidator.Validate(result);
            _closed = true;
            return CommandResult.Accept(result);
        }
        catch (InvalidDataException unknown)
        { return CommandResult.RuleUnavailable("rules.city_domestic_execution", unknown.Message); }
    }

    // Original 1F22B0/200810: books -> statistics -> incoming -> CA/CC
    // resolved conflicts -> this District10 Force -> association -> 29C340 -> role.
    private bool ApplyFormalDuty(CityDomesticDuty duty, ushort desired, Dictionary<int, int> books,
        List<CampaignEffectRequest> requests, Func<int?> readReportDay,
        Func<SourceWorldContext, AssociationRemovalReport, CityDomesticAssociationNames?>? readReportNames)
    {
        var district = CommandDistrict(); int districtId = K(district.PhysicalSlot, "formal_district_identity");
        int remaining = CurrentBooks(districtId, books);
        string parameter = duty == CityDomesticDuty.Recruiter ? "recruiterCommandCost" : "trainerCommandCost";
        if (remaining < Parameter(parameter)) return false;
        books[districtId] = Math.Clamp(unchecked(remaining - Parameter(parameter)), 0, 200);
        if (_state.CampaignEventWorld?.CurrentUserIndex != -1) throw Missing("formal_statistics_current_user");
        int incoming = PersonAreaAppointmentSource.Resolve(_current, desired);
        if (PersonAreaAppointmentSource.Resolve(_current, RoleRaw(CityDomesticDuty.Recruiter)) == incoming)
            SetRole(CityDomesticDuty.Recruiter, 0);
        if (PersonAreaAppointmentSource.Resolve(_current, RoleRaw(CityDomesticDuty.Trainer)) == incoming)
            SetRole(CityDomesticDuty.Trainer, 0);
        int rawForce = K(_current.ReadDistrictForceSlot(district), "formal_district_force");
        var force = _current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw Missing("formal_force_pointer");
        ForcePersonAssociation.Clear(force, incoming, new FormalAssociationState(this, requests, readReportDay, readReportNames));
        ClearAppointments(incoming, true);
        SetRole(duty, Clamp(desired));
        return true;
    }

    private void ApplyFormalPolicy(CityDomesticPolicy policy, Dictionary<int, int> books)
    {
        foreach (int area in _held)
        {
            var development = _state.Areas.GetValueOrDefault(area)?.Development ?? throw Missing("formal_policy_progress");
            var options = new List<(byte Focus, int Progress)>();
            Add(1, development.Commerce); Add(2, development.Agriculture); Add(3, development.Barracks);
            int physical = PersonAreaAppointmentSource.Resolve(_current, Person(area));
            byte selected = 0;
            if (K(_current.ReadPersonNativeValidity(physical), "formal_policy_person_validity") && options.Count != 0)
            {
                byte requested = (byte)policy;
                if (requested is >= 1 and <= 3 && options.Any(x => x.Focus == requested)) selected = requested;
                else
                {
                    var best = options[0];
                    foreach (var option in options.Skip(1)) if (option.Progress < best.Progress) best = option;
                    selected = best.Focus;
                }
            }
            SetFocus(area, selected);
            void Add(byte focus, int? progress)
            {
                if (progress is not (>= 0 and <= 65535)) throw Missing("formal_policy_progress." + focus);
                if (progress.Value != 1000) options.Add((focus, progress.Value));
            }
        }
        int id = K(CommandDistrict().PhysicalSlot, "policy_district_identity");
        int before = CurrentBooks(id, books);
        int cost = Parameter("policyCommandCost"); //The source reads District and its books before the late cost.
        books[id] = Math.Clamp(unchecked(before - cost), 0, 200);
    }

    private DistrictReference CommandDistrict()
    {
        var district = _current.ReadCityDistrictReference(City()) ?? throw Missing("formal_district_pointer");
        if (district.PhysicalSlot != _city.DistrictId) throw Missing("formal_district_binding");
        return district;
    }
    private int CurrentBooks(int district, Dictionary<int, int>? books = null)
    {
        int value = books is not null && books.TryGetValue(district, out int current) ? current :
            _state.Districts.GetValueOrDefault(district)?.OrdersRemaining ?? throw Missing("formal_command_books");
        return value is >= 0 and <= 255 ? value : throw Missing("formal_command_books_byte");
    }
    private void RequireRawJoin(ushort raw)
    { if (raw != 0) RequireOfficerJoin(raw, PersonAreaAppointmentSource.Resolve(_current, raw)); }
    private void RequireOfficerJoin(int identity, int physical)
    {
        if (!_state.Officers.ContainsKey(identity) || _definitions.ResolveOfficerSource(identity) != physical ||
            K(_current.ReadPersonIdentity(physical), "person_domain_identity") != identity)
            throw Missing("person_domain_binding");
    }

    private sealed class FormalAssociationState(CityDomesticDraft draft, List<CampaignEffectRequest> requests,
        Func<int?> readDay, Func<SourceWorldContext, AssociationRemovalReport, CityDomesticAssociationNames?>? readNames)
        : IForcePersonAssociationState
    {
        private SourceWorldContext Current => draft._current;
        public bool? ReadForceNativeValidity(ForceReference force) => Current.ReadForceNativeValidity(force);
        public bool? ReadPersonNativeValidity(int person) => Current.ReadPersonNativeValidity(person);
        public ushort? ReadPersonIdentity(int person) => Current.ReadPersonIdentity(person);
        public ushort? ReadForceAssociationPersonId(ForceReference force, int index) => Current.ReadForceAssociationPersonId(force, index);
        public byte? ReadForceAssociationTargetSlot(ForceReference force, int index) => Current.ReadForceAssociationTargetSlot(force, index);
        public ForceReference? ReadForceTableReference(int slot) => Current.ReadForceTableReference(slot);
        public int? ReadForcePhysicalSlot(ForceReference force) => Current.ReadForcePhysicalSlot(force);
        public ForceReference? ReadCurrentPlayerForce() => OwnedCurrentPlayerForce.ReadCurrentReference(draft._state, Current);
        public void WriteForceAssociationTargetSlot(ForceReference force, int index, byte value)
        { Current.WriteForceAssociationTargetSlot(force, index, value); Mark(force, index); }
        public void WriteForceAssociationPersonId(ForceReference force, int index, ushort value)
        { Current.WriteForceAssociationPersonId(force, index, value); Mark(force, index); }
        private void Mark(ForceReference force, int index) => draft._touchedAssociations.Add((K(force.PhysicalSlot, "association_force_identity"), index));
        public void EmitAssociationRemovalReport(AssociationRemovalReport report)
        {
            var names = readNames?.Invoke(Current, report) ?? throw Missing("association_report_source_names_formatter");
            if (names.Officer is null || names.TargetForce is null) throw Missing("association_report_source_names_formatter");
            ushort location = K(Current.ReadPersonFirstLocation(report.PersonPhysicalSlot), "association_report_location");
            int day = K(readDay(), "association_report_day");
            var selected = BaseLookup.Find(location, Current);
            byte rawDistrict = K(Current.ReadPersonDistrictSlot(report.PersonPhysicalSlot), "association_report_district");
            var district = Current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0) ?? throw Missing("association_report_district_pointer");
            int hex = K(Current.ReadReportBaseHex(selected)?.PhysicalSlot, "association_report_hex");
            int officer = K(Current.ReadPersonIdentity(report.PersonPhysicalSlot), "association_report_person_identity");
            draft.RequireOfficerJoin(officer, report.PersonPhysicalSlot);
            requests.Add(new(null, null) { ForceAssociationRemovalReport = new(
                K(report.OldForce.PhysicalSlot, "association_report_old_force"), officer, report.AssociationSlot,
                K(report.TargetForce.PhysicalSlot, "association_report_target_force"), K(district.PhysicalSlot, "association_report_district_identity"),
                location, hex, day, names.Officer, names.TargetForce) });
        }
    }
}

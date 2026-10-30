using San14.Core;

namespace San14.Data;

/// <summary>The source-backed ordered appointment transaction. SourceWorldContext
/// and vector edits are private to this operation; no prefix is published.</summary>
internal static class AreaAdministrationExecution
{
    internal readonly record struct Candidate(int Physical, ushort Identity);

    internal static HashSet<int> Candidates(SourceWorldContext current, int cityId) =>
        OrderedCandidates(current, cityId).Select(p => (int)p.Identity).ToHashSet();

    internal static List<Candidate> OrderedCandidates(SourceWorldContext current, int cityId)
    {
        var city = current.ReadCityTableReference(cityId) ?? throw Missing("candidate_city_pointer");
        var bases = new List<BaseReference> { city };
        foreach (int gate in Registered(current, RegistryKind.Gate))
        {
            var reference = new BaseReference(BaseKind.Gate, gate);
            var mapped = CurrentGateMapQueries.FindCity(reference, current);
            if (K(mapped.PhysicalSlot, "candidate_gate_city") == cityId) bases.Add(reference);
        }
        var people = Registered(current, RegistryKind.Person).ToArray();
        var result = new List<Candidate>();
        foreach (var reference in bases)
        {
            int link = K(current.ReadBaseObjectLink(reference), "candidate_base_link");
            if (link is < 1 or > 61) continue;
            foreach (int person in people)
            {
                if (K(current.ReadPersonFirstLocation(person), "candidate_first_location") != link) continue;
                if (K(current.ReadPersonStatus(person), "candidate_status") is < 1 or > 4) continue;
                int id = K(current.ReadPersonIdentity(person), "candidate_identity");
                if (Special(id))
                {
                    //233240 excludes these bands except the current special
                    //forces46..50. E2 is already a valid foothold link.
                    var force = current.ReadPersonForce(person) ?? throw Missing("candidate_special_force");
                    if (K(force.PhysicalSlot, "candidate_special_force_pointer") is not (>= 46 and <= 50)) continue;
                }
                result.Add(new(person, checked((ushort)id)));
            }
        }
        return result;
    }

    internal static CommandResult Apply(GameState state, SourceWorldContext current, CityState city,
        IReadOnlyList<AreaAdministrationChange> changes, long reservedCommandBooks,
        bool? userSelectionServicePresent = null,
        Func<GameState, int, PersonAssignmentPrecallResult, GameState>? assignmentReports = null)
    {
        if (changes.Count == 0) return CommandResult.Accept(state);
        int cityId = K(current.ReadAreaCitySlot(new(changes[0].AreaId)), "batch_area_city");
        if (cityId != city.Id || state.Cities.GetValueOrDefault(cityId)?.DistrictId != city.DistrictId)
            throw Missing("batch_domain_city");
        return ApplyOrdered(state, current, changes, reservedCommandBooks, userSelectionServicePresent, assignmentReports);
    }

    /// <summary>1ED380's ordered rows. Repeated areas and areas in different
    /// cities are significant: compare all rows with the entry world, detach
    /// incoming duties, clear every target, then install every row in order.
    /// Core supplies the same force/scope authority as ordinary commands.</summary>
    internal static CommandResult ApplyOrdered(GameState state, SourceWorldContext current,
        IReadOnlyList<AreaAdministrationChange> changes, long reservedCommandBooks,
        bool? userSelectionServicePresent = null,
        Func<GameState, int, PersonAssignmentPrecallResult, GameState>? assignmentReports = null)
    {
        if (changes.Count == 0) return CommandResult.Accept(state);
        int rawCity = K(current.ReadAreaCitySlot(new(changes[0].AreaId)), "batch_area_city");
        var sourceCity = current.ReadCityTableReference(rawCity <= 51 ? rawCity : 0) ?? throw Missing("batch_city_pointer");
        var district = current.ReadCityDistrictReference(sourceCity) ?? throw Missing("batch_district_pointer");
        int districtId = K(district.PhysicalSlot, "batch_district_identity");
        int cityId = K(sourceCity.PhysicalSlot, "batch_city_identity");
        if (!state.Cities.TryGetValue(cityId, out var city) || city.DistrictId != districtId)
            throw Missing("batch_domain_city_district");
        var districtState = state.Districts.GetValueOrDefault(districtId) ?? throw Missing("district_owner");
        int remaining = K(districtState.OrdersRemaining, "command_books");
        int cost = PersonAssignmentPrecallInputs.ReadAreaAppointmentCost(state) ?? throw Missing("command_cost");
        int changed = 0;
        foreach (var change in changes)
            if (K(current.ReadAssignmentArea36(change.AreaId), "initial_area_person") != (change.OverseerId ?? 0) ||
                K(current.ReadAssignmentArea72(change.AreaId), "initial_area_focus") != Focus(change.DevelopmentFocus)) changed++;
        int debit = unchecked(cost * changed);
        if (cost < 0 || debit < 0) throw Missing("command_cost_range");
        if (remaining - reservedCommandBooks < debit)
            return CommandResult.Reject("Not enough unreserved orders.", "administration.insufficient_orders");
        int afterBooks = Math.Clamp(unchecked(remaining - debit), 0, 200);
        //1ED380 asks1D3840 before reading the selected user. This statistics
        //controller is independent of the tutorial controller. An attached
        //lesson which did not construct it follows the actual absent branch.
        if (state.CampaignEventWorld?.CurrentUserIndex != -1 && userSelectionServicePresent != false)
            throw Missing("selected_user_statistics_service");
        int forceSlot = K(current.ReadDistrictForceSlot(district), "batch_force");
        var force = current.ReadForceTableReference(forceSlot <= 51 ? forceSlot : 0) ?? throw Missing("batch_force_pointer");
        var vectors = new PersonAssignmentPrecallInputs.AreaAppointmentDraft(state);
        //Reports are written at the actual detach point, before the remaining
        //appointment passes. Retain their App/history candidate through the
        //final publication; the enclosing command remains atomic on failure.
        GameState working = state;
        var cities = new HashSet<int>();
        var areas = new HashSet<int>();
        var associations = new HashSet<(int Force, int Slot)>();

        // Pass one: incoming appointments detach city duties, association,
        // and at most the first other same-city appointment for capacity one.
        foreach (var change in changes)
        {
            ushort desired = checked((ushort)(change.OverseerId ?? 0));
            int person = PersonAreaAppointmentSource.Resolve(current, desired);
            int first = K(current.ReadPersonFirstLocation(person), "incoming_first_location");
            var oldCity = first switch
            {
                >= 1 and <= 51 => current.ReadCityTableReference(first) ?? throw Missing("incoming_city_pointer"),
                >= 52 and <= 61 => CurrentGateMapQueries.FindCity(new(BaseKind.Gate, first - 51), current),
                _ => current.ReadCityTableReference(0) ?? throw Missing("incoming_city_zero")
            };
            int oldCityId = K(oldCity.PhysicalSlot, "incoming_city_identity");
            if (PersonAreaAppointmentSource.Resolve(current, K(current.ReadAssignmentCityCA(oldCityId), "incoming_city_recruiter")) == person)
            { current.SetAssignmentCityCA(oldCityId, 0); cities.Add(oldCityId); }
            if (PersonAreaAppointmentSource.Resolve(current, K(current.ReadAssignmentCityCC(oldCityId), "incoming_city_trainer")) == person)
            { current.SetAssignmentCityCC(oldCityId, 0); cities.Add(oldCityId); }
            var cleared = PersonAssignmentPrecallSource.Apply2783B0ForCapture(current, force, person);
            if (!cleared.NativeReturn.HasValue) throw new InvalidDataException(cleared.UnresolvedOwner);
            foreach (var write in cleared.Writes)
                if (write.NativeOffset is >= 0x196 and <= 0x19A)
                    associations.Add((write.PhysicalSlot, (write.NativeOffset - 0x196) / 2));
            if (cleared.Reports.Count != 0)
                working = assignmentReports is not null ? assignmentReports(working, person, cleared)
                    : throw Missing("association_report_saved_consumer");
            foreach (int area in Registered(current, RegistryKind.Area))
            {
                if (oldCityId is < 1 or > 51 || K(current.ReadAreaCitySlot(new(area)), "old_area_city") != oldCityId) continue;
                ushort old = K(current.ReadAssignmentArea36(area), "old_area_person");
                if (PersonAreaAppointmentSource.Resolve(current, old) != person || area == change.AreaId) continue;
                if (PersonAreaAppointmentSource.AdditionalCount(state, current, person) != 0) continue;
                ClearArea(area, old);
                break;
            }
        }
        //Keep repeated targets. All removals precede every new assignment;
        //installing an explicit clear row and the same Area's appointment is
        //different from collapsing those rows before the transaction.
        foreach (var change in changes)
            ClearArea(change.AreaId, K(current.ReadAssignmentArea36(change.AreaId), "target_old_person"));
        foreach (var change in changes)
        {
            ushort old = K(current.ReadAssignmentArea36(change.AreaId), "target_cleared_person");
            ushort desired = checked((ushort)(change.OverseerId ?? 0));
            if (old != desired)
            {
                current.SetAssignmentArea36(change.AreaId, desired);
                _ = PersonAreaAppointmentSource.Change(state, current, vectors, change.AreaId, desired, old);
            }
            current.SetAssignmentArea72(change.AreaId, Focus(change.DevelopmentFocus));
            areas.Add(change.AreaId);
        }
        var result = working with { Districts = new(working.Districts)
            { [districtId] = districtState with { OrdersRemaining = afterBooks } } };
        result = PersonAssignmentPrecallInputs.PublishAreaAppointments(result, current, cities, areas, associations);
        return CommandResult.Accept(vectors.Publish(result));

        void ClearArea(int area, ushort old)
        {
            if (old != 0)
            {
                current.SetAssignmentArea36(area, 0);
                _ = PersonAreaAppointmentSource.Change(state, current, vectors, area, 0, old);
            }
            current.SetAssignmentArea72(area, 0);
            areas.Add(area);
        }
    }
    private static byte Focus(AreaDevelopmentFocus focus) => focus switch
    { AreaDevelopmentFocus.None => 0, AreaDevelopmentFocus.Commerce => 1, AreaDevelopmentFocus.Agriculture => 2,
      AreaDevelopmentFocus.Barracks => 3, _ => throw Missing("focus") };
    private static bool Special(int id) => unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
        unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 || unchecked((uint)(id - 4191)) <= 9;
    private static IEnumerable<int> Registered(SourceWorldContext current, RegistryKind kind)
    {
        var descriptor = current.ReadRegistryDescriptor(kind) ?? throw Missing(kind + "_registry");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing(kind + "_head");
        while (node.Identity.HasValue)
        {
            var value = current.ReadRegistryNodePayload(node) ?? throw Missing(kind + "_payload");
            if (value.Kind != kind) throw Missing(kind + "_payload_kind");
            yield return K(value.PhysicalSlot, kind + "_pointer");
            node = current.ReadRegistryNodeNext(node) ?? throw Missing(kind + "_next");
        }
    }
    private static T K<T>(T? value, string input) where T : struct => value ?? throw Missing(input);
    private static InvalidDataException Missing(string input) => new("area-administration." + input);
}

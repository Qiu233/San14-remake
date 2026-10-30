using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public CommandResult SetIntelligenceAssignments(GameState state, ForceState force,
        SetIntelligenceAssignmentsOrder order, CommandContext context)
    {
        try
        {
            if (order.Assignments is not { Count: 3 })
                return CommandResult.Reject("Three intelligence assignment slots are required.", "intelligence.home_slots");
            var desired = order.Assignments.ToArray();
            if (desired.Any(row => row.TargetForceId is < 0 or > 51 || row.OfficerId is < 0 or > 6000 ||
                (row.TargetForceId == 0) != (row.OfficerId == 0)) ||
                desired.Where(row => row.OfficerId != 0).Select(row => row.OfficerId).Distinct().Count() != desired.Count(row => row.OfficerId != 0) ||
                desired.Where(row => row.TargetForceId != 0).Select(row => row.TargetForceId).Distinct().Count() != desired.Count(row => row.TargetForceId != 0))
                return CommandResult.Reject("The intelligence assignments contain an invalid or repeated selection.", "intelligence.home_selection");

            var current = CaptureOwnedArmyReturnWorld(state).Context;
            PersonReturnCommands.BindCurrent(state, current);
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            if (!_baseCapacityInputs.BindCurrent(state, current)) throw IntelligenceMissing("home_capacity_owner");
            var own = current.ReadForceTableReference(order.ForceId) ?? throw IntelligenceMissing("home_Force");
            var original = ReadIntelligenceAssignmentsCurrent(current, order.ForceId);
            var targets = ReadIntelligenceTargetsCurrent(state, current, order.ForceId, 0);
            var people = ReadIntelligencePeopleCurrent(state, current, order.ForceId, 0);
            int Person(IReadOnlyList<IntelligenceAssignment> rows, int target) =>
                rows.FirstOrDefault(row => row.TargetForceId == target)?.OfficerId ?? 0;
            foreach (var row in desired.Where(row => row.OfficerId != 0))
            {
                if (Person(original, row.TargetForceId) == row.OfficerId) continue;
                if (!targets.Any(candidate => candidate.Id == row.TargetForceId && candidate.Enabled) ||
                    !people.Any(candidate => candidate.Id == row.OfficerId && candidate.Enabled))
                    return CommandResult.Reject("An intelligence target or officer is no longer available.", "intelligence.home_unavailable");
            }
            //6DA690 walks the actual target list. A replacement belongs only
            //to the outgoing list: it costs one book unit, not two.
            var removed = new List<IntelligenceAssignment>();
            var added = new List<IntelligenceAssignment>();
            foreach (var target in targets)
            {
                int before = Person(original, target.Id), after = Person(desired, target.Id);
                if (before == after) continue;
                if (before != 0) removed.Add(new(target.Id, before));
                else added.Add(new(target.Id, after));
            }
            if (original.Concat(desired).Any(row => row.TargetForceId != 0 &&
                !targets.Any(target => target.Id == row.TargetForceId) &&
                Person(original, row.TargetForceId) != Person(desired, row.TargetForceId)))
                return CommandResult.Reject("An intelligence assignment is outside the current target list.", "intelligence.home_target");
            int changes = removed.Count + added.Count;
            if (changes == 0) return CommandResult.Accept(state);
            int cost = checked(changes * IntelligenceParameter(state, "home-book"));
            var district = ForcePrimaryDistrictSource.Resolve(current, own);
            int districtId = IntelligenceNeed(district.PhysicalSlot, "home_District");
            int books = state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining ?? throw IntelligenceMissing("home_books14");
            if (cost < 0 || books - cost < CommandBookAvailability.Reserved(state, districtId))
                return CommandResult.Reject("There are not enough command books for these intelligence assignments.", "intelligence.home_books");

            //6D04B0 chooses one source-time line before the terminal callback.
            //Only an all-addition batch draws App; withdrawal sorting is pure.
            IntelligenceAssignment report;
            int root;
            if (removed.Count != 0)
            {
                report = removed[0];
                foreach (var candidate in removed.Skip(1))
                    if (CompareIntelligenceHomeOutgoing(state, current, own, candidate, report) < 0) report = candidate;
                root = 0x5C32;
            }
            else
            {
                var random = new SharedSourceRandom32(state.CampaignAppRandom32?.State ?? throw IntelligenceMissing("home_App_owner"));
                report = added[random.Next(added.Count)];
                state = state with { CampaignAppRandom32 = new(random.State) };
                root = 0x5C2C;
            }
            var formatted = FormatReportMessage(state, current, root,
                new(Person0: report.OfficerId, Person1: TalentRulerCurrent(current, own), Force0: report.TargetForceId),
                ReportMessageOrigin.IntelligenceCommands, null, _personnelNarrationVoice);
            state = formatted.State;

            var vectors = new PersonAssignmentPrecallInputs.AreaAppointmentDraft(state);
            var cities = new HashSet<int>();
            var areas = new HashSet<int>();
            //6CC010 clears the incoming receiver's CA/CC and29C340(0,0).
            //742760 restores departing receivers from the original snapshot;
            //therefore only final incoming receivers remain cleared. Keep72/6D.
            foreach (var assignment in desired.Where(row => row.OfficerId != 0))
            {
                if (Person(original, assignment.TargetForceId) == assignment.OfficerId) continue;
                int person = PersonAreaAppointmentSource.Resolve(current, (ushort)assignment.OfficerId);
                if (!IntelligenceNeed(current.ReadPersonNativeValidity(person), "home_incoming_validity"))
                    return CommandResult.Reject("The intelligence officer is no longer valid.", "intelligence.home_officer");
                var home = BaseLookup.Find(IntelligenceNeed(current.ReadPersonFirstLocation(person), "home_incoming_E2"), current);
                var city = CurrentGateMapQueries.FindCity(home, current);
                if (IntelligenceNeed(current.ReadBaseNativeValidity(city), "home_incoming_City_validity"))
                {
                    int cityId = IntelligenceNeed(city.PhysicalSlot, "home_incoming_City");
                    if (IntelligenceNeed(current.ReadAssignmentCityCA(cityId), "home_recruiter_CA") == assignment.OfficerId)
                    { current.SetAssignmentCityCA(cityId, 0); cities.Add(cityId); }
                    if (IntelligenceNeed(current.ReadAssignmentCityCC(cityId), "home_trainer_CC") == assignment.OfficerId)
                    { current.SetAssignmentCityCC(cityId, 0); cities.Add(cityId); }
                }
                while (vectors.Count(person) > 0)
                {
                    int raw = vectors.Entry(person, 0);
                    if (raw < 0) throw IntelligenceMissing("home_clear_area_index");
                    var area = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw IntelligenceMissing("home_clear_Area");
                    if (!IntelligenceNeed(current.ReadAreaNativeValidity(area), "home_clear_Area_validity"))
                        throw IntelligenceMissing("home_clear_vector_no_progress");
                    int areaId = IntelligenceNeed(area.PhysicalSlot, "home_clear_Area_identity");
                    ushort old = IntelligenceNeed(current.ReadAssignmentArea36(areaId), "home_clear_Area36");
                    int before = vectors.Count(person);
                    if (old != 0)
                    {
                        current.SetAssignmentArea36(areaId, 0);
                        PersonAreaAppointmentSource.Change(state, current, vectors, areaId, 0, old);
                        areas.Add(areaId);
                    }
                    if (vectors.Count(person) >= before) throw IntelligenceMissing("home_clear_vector_no_progress");
                }
            }
            for (int slot = 0; slot < 3; slot++)
                current.SetAssignmentForceAssociation(order.ForceId, slot,
                    (ushort)desired[slot].OfficerId, (byte)desired[slot].TargetForceId);
            state = PersonAssignmentPrecallInputs.PublishAreaAppointments(state, current, cities, areas,
                Enumerable.Range(0, 3).Select(slot => (order.ForceId, slot)));
            state = vectors.Publish(state);
            state = SpendStrategicCommandBooksCurrent(state, current, districtId, cost);
            var request = CreateSingleOfficerPresentation(current, state, report.OfficerId, formatted,
                TalentCommandPresentationOperation.ConditionalOfficerText, "intelligence.home_report");
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw IntelligenceMissing("home_presentation_owner"))
                .Append(CampaignEffectSource.IntelligenceCommands,
                    state.NativeMapSeason?.AdvancedCalendarSteps ?? throw IntelligenceMissing("home_calendar"), [request]) };
            return CommandResult.Accept(state);
        }
        catch (InvalidDataException missing)
        { return CommandResult.RuleUnavailable("rules.intelligence_home", missing.Message); }
    }

    //6B1B60: current gain, personality211, Intelligence, then raw identity.
    private int CompareIntelligenceHomeOutgoing(GameState state, SourceWorldContext current, ForceReference own,
        IntelligenceAssignment left, IntelligenceAssignment right)
    {
        int l = PersonAreaAppointmentSource.Resolve(current, (ushort)left.OfficerId);
        int r = PersonAreaAppointmentSource.Resolve(current, (ushort)right.OfficerId);
        var lt = current.ReadForceTableReference(left.TargetForceId) ?? throw IntelligenceMissing("home_sort_target");
        var rt = current.ReadForceTableReference(right.TargetForceId) ?? throw IntelligenceMissing("home_sort_target");
        if (!IntelligenceNeed(current.ReadForceNativeValidity(lt), "home_sort_target_validity")) return 0;
        if (!IntelligenceNeed(current.ReadForceNativeValidity(rt), "home_sort_target_validity")) return -1;
        int order = ReadHireAssociationGain(state, current, own, rt, r).CompareTo(ReadHireAssociationGain(state, current, own, lt, l));
        if (order != 0) return order;
        bool Has(int person) => Enumerable.Range(0, 9).Any(index =>
            IntelligenceNeed(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person, index), "home_sort_trait") == 211);
        order = Has(r).CompareTo(Has(l));
        if (order != 0) return order;
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        int Ability(int person) => IntelligenceNeed(abilities.ResolveDimension(state,
            IntelligenceNeed(current.ReadPersonIdentity(person), "home_sort_Person10"), 2,
            (forceId, effect) => _baseCapacityInputs.ReadPanelModifier(state, forceId, effect)), "home_sort_intelligence");
        order = Ability(r).CompareTo(Ability(l));
        return order != 0 ? order : left.OfficerId.CompareTo(right.OfficerId);
    }
}

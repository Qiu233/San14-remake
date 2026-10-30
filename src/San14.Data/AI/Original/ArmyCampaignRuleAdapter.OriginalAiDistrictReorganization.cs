using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The computer-only43F9D0 tail is independent of the behavior-manager
    //enable bit. The caller retains its real Force and current transaction.
    internal GameState CompleteOriginalAiComputerStrategyCurrent(GameState state,
        SourceWorldContext current, int forceId)
    {
        var date = state.NativeMapSeason?.ThirdDate ?? throw AiReorganizationMissing("World34_date");
        if (date.Month != 1 || date.Day >= 11) return state; //333E20.
        if (Seed.Language != "TC") throw AiReorganizationMissing("SC_source_contract");
        var force = new ForceReference(forceId);
        int limit = (current.ReadRegistryReferences(RegistryKind.City)
            ?? throw AiReorganizationMissing("City90_registry")).Count / 7;
        var forceCities = ForceCities().ToArray(); //2243A0: physical City-table order.
        var districts = ForceDistricts();
        if (districts.Count == 8 || !districts.Any(d => DistrictCities(d).Count > limit)) return state;

        var working = state;
        var pool = current.ReadOwnedCurrentListPool() ?? throw AiReorganizationMissing("current_pool");
        var effects = new List<CampaignEffectRequest>();
        int ruler = Person(AiReorganizationNeed(current.ReadForceRecord(forceId)?.RawRuler10, "Force10_ruler"));
        var operation = CreateCaptureDistrictOperationQueries(current, () => working,
            value => working = value, date, effects);
        var priorAdvisor = current.AssignmentAdvisorResetCallbacks;
        var priorArea = current.AssignmentAreaOfficerRemoval;
        current.AssignmentAdvisorResetCallbacks = owner =>
        {
            var result = PersonAdvisorResetSource.ApplyWithinPersonCapture(working, current, pool, owner);
            working = result.State ?? throw AiReorganizationMissing(result.UnresolvedOwner ?? "advisor_reset");
            return true;
        };
        current.AssignmentAreaOfficerRemoval = (area, old) =>
        {
            var result = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, old);
            if (result.NativeReturn is null) throw AiReorganizationMissing(result.UnresolvedOwner ?? "area_removal");
            if (result.NativeReturn != 0 && result.PersonPhysical is int person)
                working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, person, [], publishVector: true);
            return result.NativeReturn != 0;
        };
        try
        {
            //Reacquire the live D0 chain after each293490 unlink/reset.
            int attempts = 0;
            while ((districts = ForceDistricts()).Count != 1)
            {
                var primary = ForcePrimaryDistrictSource.Resolve(current, force);
                var old = districts.FirstOrDefault(d => d != primary)
                    ?? throw AiReorganizationMissing("merge_nonprimary_District");
                PersonReturnDistrictReorganizationSource.MergeCurrent(current, old, primary, operation);
                if (++attempts > 8) break; //Actual source loop bound.
            }

            var visited = new HashSet<BaseReference>();
            int nextNumber = 2;
            foreach (var city in forceCities)
            {
                if (!visited.Add(city)) continue;
                var group = new List<BaseReference> { city };
                //0C37F0 appends through the same live list. It can visit the
                //newly appended cities in this pass; another pass occurs only
                //when the count changed and remains above the target bound.
                int before = group.Count;
                Expand(group, visited, limit);
                int rounds = 0;
                while (before != group.Count && group.Count > limit && ++rounds <= 100)
                { before = group.Count; Expand(group, visited, limit); }
                foreach (var member in group) visited.Add(member);

                bool containsRuler = false;
                int ordinaryCount = 0;
                foreach (var member in group)
                {
                    foreach (int person in ResidentsAndGates(member))
                    {
                        if (ExcludedSpecial(person) || Guarded(person)) continue;
                        ordinaryCount++;
                        if (person == ruler) { containsRuler = true; break; }
                    }
                    if (containsRuler) break;
                }
                if (containsRuler) { Assign(group, 1); continue; }
                DistrictReference? neighborDistrict = null;
                if (group.Count == 1)
                    foreach (var neighbor in Neighbors(group[0]))
                        if (AiReorganizationNeed(current.ReadBaseForceSlot(neighbor), "neighbor_Force") == forceId)
                        { neighborDistrict = current.ReadBaseDistrictReference(neighbor)
                            ?? throw AiReorganizationMissing("neighbor_District"); break; }

                if (ordinaryCount == 0)
                {
                    group.AddRange(DistrictCities(ForcePrimaryDistrictSource.Resolve(current, force)));
                    Assign(group, 1);
                }
                else if (neighborDistrict is not null && AiReorganizationNeed(
                    current.ReadDistrictNativeValidity(neighborDistrict), "neighbor_District_validity"))
                {
                    group.AddRange(DistrictCities(neighborDistrict));
                    Assign(group, AiReorganizationNeed(current.ReadDistrictNumber(
                        AiReorganizationNeed(neighborDistrict.PhysicalSlot, "neighbor_District_identity")), "neighbor_District11"));
                }
                else Assign(group, nextNumber++);
            }

            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
            working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
            working = BaseCaptureCurrentInputs.PublishCurrent(working, current);
            working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current,
                operation.ChangedAreas, operation.ChangedGates);
            working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
            var published = new Dictionary<int, DistrictState>(working.Districts);
            foreach (int district in operation.TouchedDistricts)
                if (published.TryGetValue(district, out var record) &&
                    AiReorganizationNeed(current.ReadDistrictNativeValidity(district), "publish_District_validity"))
                    published[district] = record with
                    {
                        OrdersRemaining = AiReorganizationNeed(current.ReadDistrictRawByte(district, 0x14), "publish_books14"),
                        Settings = ReadDistrictAdministrationSettingsCurrent(current, district)
                    };
            working = working with { Districts = published,
                PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw AiReorganizationMissing("personnel_manager") };
            if (effects.Count != 0)
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw AiReorganizationMissing("effect_owner")).Append(CampaignEffectSource.PersonnelAdministrationCommands,
                        working.NativeMapSeason?.AdvancedCalendarSteps ?? throw AiReorganizationMissing("effect_calendar"), effects) };
            return working.WithArmyLifecycle((working.ReadArmyLifecycle()
                ?? throw AiReorganizationMissing("Army_lifecycle")) with { Indexes = pool.ExportOwnedState() });
        }
        finally
        { current.AssignmentAdvisorResetCallbacks = priorAdvisor; current.AssignmentAreaOfficerRemoval = priorArea; }

        List<DistrictReference> ForceDistricts() => (current.ReadArmyReturnRegisteredDistricts()
            ?? throw AiReorganizationMissing("DistrictD0_registry"))
            .Select(d => d ?? throw AiReorganizationMissing("District_pointer"))
            .Where(d => AiReorganizationNeed(current.ReadDistrictForceId(
                AiReorganizationNeed(d.PhysicalSlot, "District_identity")), "District10") == forceId).ToList();

        IEnumerable<BaseReference> ForceCities()
        {
            if (forceId is < 1 or > 51) yield break;
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw AiReorganizationMissing("City_table");
                if (AiReorganizationNeed(current.ReadBaseForceSlot(city), "City_Force") == forceId) yield return city;
            }
        }
        List<BaseReference> DistrictCities(DistrictReference district)
        {
            int key = AiReorganizationNeed(district.PhysicalSlot, "DistrictCities_identity");
            var result = new List<BaseReference>();
            if (key is < 1 or > 51) return result;
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw AiReorganizationMissing("DistrictCities_City_table");
                if (AiReorganizationNeed(current.ReadBaseDistrictSlot(city), "City78") == key) result.Add(city);
            }
            return result;
        }
        IEnumerable<BaseReference> Neighbors(BaseReference origin)
        {
            var temporary = current.CreateTemporaryHexList() ?? throw AiReorganizationMissing("neighbor_temporary");
            try
            {
                new PersonFreedomHomeSource(_scenario).FillCities22F430(current, origin, temporary);
                var node = current.ReadRegistryHead(new(temporary.Identity)) ?? throw AiReorganizationMissing("neighbor_head");
                while (node.Identity.HasValue)
                {
                    var payload = current.ReadRegistryNodePayload(node) ?? throw AiReorganizationMissing("neighbor_payload");
                    if (payload.Kind != RegistryKind.City) throw AiReorganizationMissing("neighbor_City_kind");
                    yield return new(BaseKind.City, payload.PhysicalSlot);
                    node = current.ReadRegistryNodeNext(node) ?? throw AiReorganizationMissing("neighbor_next");
                }
            }
            finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        }
        void Expand(List<BaseReference> group, HashSet<BaseReference> visited, int target)
        {
            for (int index = 0; index < group.Count; index++)
                foreach (var neighbor in Neighbors(group[index]))
                    if (!visited.Contains(neighbor) && !group.Contains(neighbor) &&
                        AiReorganizationNeed(current.ReadBaseForceSlot(neighbor), "expand_neighbor_Force") ==
                        AiReorganizationNeed(current.ReadBaseForceSlot(group[index]), "expand_origin_Force"))
                    { group.Add(neighbor); if (group.Count == target) return; }
        }
        IEnumerable<int> ResidentsAndGates(BaseReference city)
        {
            foreach (int person in DistrictAdministrationHomePeople(current, city)) yield return person;
            foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw AiReorganizationMissing("GateA0_registry"))
            {
                var gate = entry ?? throw AiReorganizationMissing("Gate_pointer");
                if (CurrentGateMapQueries.FindCity(gate, current) == city)
                    foreach (int person in DistrictAdministrationHomePeople(current, gate)) yield return person;
            }
        }
        bool ExcludedSpecial(int person)
        {
            int id = AiReorganizationNeed(current.ReadPersonIdentity(person), "candidate_Person10");
            if (!Enumerable.Range(0, 5).Any(i => id >= 4111 + i * 20 && id <= 4120 + i * 20)) return false;
            ushort location = AiReorganizationNeed(current.ReadPersonFirstLocation(person), "special_Home");
            if (location is < 1 or > 61) return false;
            var owner = current.ReadPersonForce(person) ?? throw AiReorganizationMissing("special_Force");
            return AiReorganizationNeed(current.ReadForcePhysicalSlot(owner), "special_Force_identity") is not (>= 46 and <= 50);
        }
        bool Guarded(int person) => (AiReorganizationNeed(current.ReadArmyReturnPersonFlags196(person), "candidate_Person15E") & 0x4000) != 0;
        bool TribalHome(int person)
        {
            ushort home = AiReorganizationNeed(current.ReadPersonFirstLocation(person), "tribal_Home");
            return home is >= 1 and <= 61 && AiReorganizationNeed(current.ReadPersonOldForceSlot(person), "tribal_Force") is >= 46 and <= 50;
        }
        int Person(ushort id) => AiReorganizationNeed((id <= 6000 ? current.ReadPersonLookupReference(id)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        void Assign(List<BaseReference> cities, int number)
        {
            //0B2B00's source capacity branch discards this candidate list.
            if (ForceDistricts().Count == 8) return;
            var people = cities.SelectMany(ResidentsAndGates)
                .Where(p => !TribalHome(p) && !ExcludedSpecial(p) && !Guarded(p)).ToList();
            //021FD0 uses stable insertion by the five effective abilities.
            var scores = new Dictionary<int, int>();
            int Score(int person)
            {
                if (scores.TryGetValue(person, out int cached)) return cached;
                int id = AiReorganizationNeed(current.ReadPersonIdentity(person), "ability_Person10"), total = 0;
                for (int dimension = 0; dimension < 5; dimension++)
                    total = unchecked(total + ReadOriginalAiCityDutyAbility(working, current, id, dimension));
                return scores[person] = total;
            }
            for (int at = 1; at < people.Count; at++)
            {
                int selected = people[at], position = at;
                while (position > 0 && Score(selected) > Score(people[position - 1]))
                { people[position] = people[position - 1]; position--; }
                people[position] = selected;
            }
            if (people.Count == 0) throw AiReorganizationMissing("native_empty_leader_candidates");
            int leader = number == 1 ? ruler : people[0] == ruler ? 0 : people[0];
            var district = ForceDistricts().LastOrDefault(d => AiReorganizationNeed(current.ReadDistrictNumber(
                AiReorganizationNeed(d.PhysicalSlot, "assignment_District_identity")), "assignment_District11") == number);
            if (number == 1) district = ForcePrimaryDistrictSource.Resolve(current, force);
            if (ForceDistricts().Count < number || district is null ||
                !AiReorganizationNeed(current.ReadDistrictNativeValidity(district), "assignment_District_validity"))
            {
                //0B2B00 includes every linked Gate, including another Force's
                //Gate.2D7A70 reaches real26DA10 there, rather than filtering it.
                var bases = new List<BaseReference>();
                foreach (var city in cities)
                {
                    bases.Add(city);
                    foreach (var gate in DistrictCreationGates(current))
                        if (CurrentGateMapQueries.FindCity(gate, current) == city) bases.Add(gate);
                }
                try { CreateDistrictAdministrationCurrent(current, pool, force, leader, bases, operation,
                    transferForeignBase: (at, destination) => working = ApplyForceBaseTransferCurrent(
                        working, current, pool, at, destination, new(0), date, effects,
                        preserveResources: false, suppressMaintenance: false, fullCentralRadius: true,
                        transferDistrict: transfer => TransferDistrictAdministrationBaseCurrent(current, at, destination, transfer))); }
                catch (InvalidDataException error) when (error.Message == "district.create.no_free_District")
                { /*2D7A70 known receiver exhaustion returns native0.*/ }
                return;
            }
            int key = AiReorganizationNeed(district.PhysicalSlot, "assignment_District");
            int previous = Person(AiReorganizationNeed(current.ReadDistrictLeader(key), "assignment_previous_leader"));
            foreach (var city in cities)
            {
                Transfer(city);
                foreach (var gate in DistrictCreationGates(current))
                    if (CurrentGateMapQueries.FindCity(gate, current) == city) Transfer(gate);
            }
            current.WriteDistrictLeader(key, (ushort)Math.Min((int)AiReorganizationNeed(current.ReadPersonIdentity(leader), "assignment_leader10"), 6000));
            operation.TouchedDistricts.Add(key);
            if (AiReorganizationNeed(current.ReadDistrictNumber(key), "assignment_number") != 1 && leader != previous)
            { var newHome = SetStatus(leader, 2); var oldHome = SetStatus(previous, 4); Update(newHome); Update(oldHome); }

            void Transfer(BaseReference at)
            {
                if (AiReorganizationNeed(current.ReadBaseDistrictSlot(at), "transfer_Base78") != key)
                    TransferDistrictAdministrationBaseCurrent(current, at, district, operation);
            }
            BaseReference? SetStatus(int person, byte status)
            {
                if (!AiReorganizationNeed(current.ReadPersonNativeValidity(person), "changed_leader_validity")) return null;
                current.WritePersonStatus(person, status);
                return BaseLookup.Find(AiReorganizationNeed(current.ReadPersonFirstLocation(person), "changed_leader_Home"), current);
            }
            void Update(BaseReference? at)
            {
                if (at is null || !AiReorganizationNeed(current.ReadBaseNativeValidity(at), "changed_Home_validity")) return;
                var result = ArmyReturnGovernorUpdater.PrepareCurrent(at, current, operation);
                ArmyReturnGovernorUpdater.ApplyCurrentWrites(current, result.Packet
                    ?? throw AiReorganizationMissing(result.UnresolvedOwner ?? "changed_Governor"));
            }
        }
    }

    private static T AiReorganizationNeed<T>(T? value, string field) where T : struct => value ?? throw AiReorganizationMissing(field);
    private static InvalidDataException AiReorganizationMissing(string field) => new("original_ai.district_reorganization." + field);
}

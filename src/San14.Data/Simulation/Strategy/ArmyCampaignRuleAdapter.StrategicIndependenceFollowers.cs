using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2E6010 after2D8050 has actually created the new Force. Source reports
    //are formatted by the enclosing command at the precise callback points.
    internal GameState ApplyStrategicIndependenceFollowersCurrent(GameState input,
        SourceWorldContext current, WorldIndexContext pool, ForceReference originalForce,
        int rebelPhysical, int? executorPhysical, BaseReference originalHome, ForceReference newForce,
        NativeMapSeasonDate date, List<CampaignEffectRequest> feedback,
        Func<GameState, int, int, GameState> reportAtSource)
    {
        var working = input;
        int original = K(current.ReadForcePhysicalSlot(originalForce), "original_Force");
        int created = K(current.ReadForcePhysicalSlot(newForce), "new_Force");
        int oldRuler = Lookup(K(current.ReadForceRecord(original)?.RawRuler10, "original_ruler10"));
        int homeId = K(current.ReadBaseObjectLink(originalHome), "rebel_Home4E");
        var followers = current.CreateNearbyTemporary();
        try
        {
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
            {
                int person = K(entry, "Person_pointer");
                if (K(current.ReadPersonOldForceSlot(person), "candidate_Force") != original ||
                    Status(person) is < 1 or > 4) continue;
                if (executorPhysical is int executor && Valid(executor) && Id(person) == Id(executor)) continue;
                ushort first = K(current.ReadPersonFirstLocation(person), "candidate_E2");
                bool atBase = first is >= 1 and <= 61;
                int owner = K(current.ReadForcePhysicalSlot(current.ReadPersonForce(person)
                    ?? throw Missing("candidate_Force_pointer")), "candidate_Force_ordinal");
                if (BattleRemakeCurrentRegionFactor.IsNativeBandPerson(Id(person)) && atBase && owner is not (>= 46 and <= 50) ||
                    atBase && owner is >= 46 and <= 50 ||
                    (K(current.ReadArmyReturnPersonFlags196(person), "candidate_flags15E") & 0x4000) != 0 ||
                    Id(person) == Id(rebelPhysical) || Status(person) == 1 ||
                    PersonCurrentBusySource.Read(current, person, Missing)) continue;

                if (!Sworn(person, rebelPhysical) && !Spouse(person, rebelPhysical))
                {
                    bool willing = false;
                    //TC1A4A818's immutable value95 is independent of the
                    //later minimum follower loyalty, although equal here.
                    if ((first == K(current.ReadPersonFirstLocation(rebelPhysical), "rebel_E2") ||
                        Dislikes(person, oldRuler) || Loyalty(person) < StrategicParameter(working,
                            "resolution.independence-follower-loyalty-threshold", 95)) &&
                        !Dislikes(person, rebelPhysical) && !Dislikes(rebelPhysical, person))
                        willing = ReadStrategicIndependenceInclinationCurrent(working, current, person, rebelPhysical, newForce);
                    bool likesRebel = Likes(person, rebelPhysical);
                    bool blocked = !likesRebel || Loyalty(person) > 99 || Likes(person, oldRuler) ||
                        Sworn(person, oldRuler) || Spouse(person, oldRuler) ||
                        ParentOrGrandparent(person, oldRuler) || ParentOrGrandparent(oldRuler, person) ||
                        OfficeRelatedCurrent(working, current, person, oldRuler) && !Dislikes(person, oldRuler);
                    if (blocked && !willing) continue;
                }
                var appended = current.AllocateRegistryNode(new(followers.Identity)) ?? throw Missing("candidate_node_owner");
                if (appended.Identity.HasValue) current.WriteRegistryNodePayload(appended, new(RegistryKind.Person, person));
            }
            SortFollowers();
            var selected = People(followers);
            if (selected.Count != 0)
            {
                int first = selected[0];
                if (TalentForceIsPlayerCurrent(working, current, originalForce))
                    working = reportAtSource(working, 0x5E4B, first);
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current) ?? throw Missing("player_Force");
                int playerRuler = Lookup(K(current.ReadForceRecord(K(player.PhysicalSlot, "player_Force_slot"))?.RawRuler10, "player_ruler10"));
                if (Valid(playerRuler)) working = reportAtSource(working, 0x5E58, first);
                bool additionalReported = false;
                foreach (int person in selected)
                {
                    var destination = current.ReadDistrictTableReference(K(current.ReadPersonDistrictSlot(rebelPhysical), "rebel_DistrictE0"))
                        ?? throw Missing("rebel_District_pointer");
                    var home = BaseLookup.Find(K(current.ReadPersonFirstLocation(rebelPhysical), "rebel_current_E2"), current);
                    working = ApplyStrategicIndependencePersonJoinCurrent(working, current, person,
                        destination, home, date, Day, feedback, followers).State;
                    if (!additionalReported && Id(person) != Id(first))
                    {
                        additionalReported = true;
                        working = reportAtSource(working, 0x5E59, person);
                    }
                    var army = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw Missing("follower_Army_pointer");
                    if (K(current.ReadArmyUnitNativeValidity(army), "follower_Army_validity"))
                    {
                        var destroyed = new OwnedArmyDestructionStage(this).ApplyAbsorptionCommanderCause4Current(
                            working, current, pool, K(army.PhysicalSlot, "follower_Army_slot"), date, feedback);
                        working = destroyed.State ?? throw Missing(destroyed.UnresolvedOwner ?? "follower_cause4");
                    }
                    else working = ApplyStrategicIndependencePlacementCurrent(working, current, pool, person, originalHome, date);
                    byte loyalty = (byte)Math.Max(95, Loyalty(person));
                    if ((K(current.ReadArmyReturnPersonFlags196(person), "follower_loyalty_flags") & 0x2000) != 0) loyalty = 255;
                    current.SetPersonDestructionLoyaltyE8(person, loyalty);
                    working = ArmyDestructionLoyalty.PublishCurrentRawE8(working, current, person, loyalty);
                    var aftermath = current.ReadOfficerAftermath(person) ?? throw Missing("follower_resentment_owner");
                    int former = Math.Min((int)Id(oldRuler), 6000);
                    bool mapped = working.Officers.ContainsKey(former);
                    current.SetOfficerAftermath(person, aftermath with
                    {
                        Resentment = new(mapped ? former : null, 24),
                        SourceKnowledge = (aftermath.SourceKnowledge ?? new(Id(person), null, null, null)) with
                        { UnmappedResentmentTarget = mapped ? null : former }
                    });
                    working = current.PublishOfficerAftermath(working, Id(person));
                }
            }

            //234920(Home,0) is evaluated after all actual joins/placements.
            //Remaining ordinary old-Home residents keep their old Force and
            //use260310's real replacement-Home choice, never an empty move.
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("remaining_Person100"))
            {
                int person = K(entry, "remaining_Person_pointer");
                if (Status(person) is < 1 or > 4 || K(current.ReadPersonFirstLocation(person), "remaining_E2") != homeId ||
                    Id(person) == Id(rebelPhysical) || executorPhysical is int executor && Valid(executor) && Id(person) == Id(executor) ||
                    selected.Contains(person)) continue;
                var relocationOperation = CreateCaptureDistrictOperationQueries(current, () => working, value => working = value, date, feedback);
                var relocated = new CaptureReturningPersonHomeSource(this, _governorInputs).TryApplyHomeCurrent(
                    working, current, homeId, original, Id(person), date, relocationOperation, feedback);
                working = relocated.State ?? throw Missing(relocated.UnresolvedOwner ?? "remaining_260310");
                working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, relocationOperation);
                working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current,
                    relocationOperation.ChangedAreas, relocationOperation.ChangedGates);
                var random = new SharedSourceRandom32((working.CampaignAppRandom32 ?? throw Missing("remaining_App32")).State);
                bool announce = random.Next(100) < 20;
                working = working with { CampaignAppRandom32 = new(random.ReadCurrentState()) };
                if (announce) working = reportAtSource(working, 0x5E6F, person);
            }
            foreach (var district in (current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("old_DistrictD0")).ToArray())
            {
                int at = K(district?.PhysicalSlot, "old_District_pointer");
                if (K(current.ReadDistrictForceId(at), "old_District_Force") != original) continue;
                var operation = CreateCaptureDistrictOperationQueries(current, () => working, value => working = value, date, feedback);
                ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(new(at), current, operation);
                working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
                working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
                working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current, operation.ChangedAreas, operation.ChangedGates);
            }
            string relationKey = "simulation.strategic-command.resolution.independence-initial-relations";
            if (working.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(relationKey, out int relation) != true)
                throw Missing("current_parameter_" + relationKey);
            byte bounded = (byte)Math.Clamp(relation, 0, 200);
            working = AbsorptionWriteRelation(working, current, original, created, false, bounded);
            working = AbsorptionWriteRelation(working, current, created, original, false, bounded);
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            return working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
        }
        finally { current.ClearNearbyTemporary(followers, true); }

        int Day() => (ReadArmyDestructionSampler(working, current) ?? throw Missing("World450_45C")).Day450;
        int Lookup(int id) => K((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        bool Valid(int person) => K(current.ReadPersonNativeValidity(person), "Person_validity");
        ushort Id(int person) => K(current.ReadPersonIdentity(person), "Person10");
        byte Status(int person) => K(current.ReadPersonStatus(person), "PersonE6");
        int Loyalty(int person) => K(ArmyDestructionLoyalty.ReadCurrentRawE8(working, current, person, _governorInputs), "PersonE8");
        bool Sworn(int a, int b) => StrategicIndependenceSwornCurrent(working, current, a, b);
        bool Spouse(int a, int b) => StrategicIndependenceSpouseCurrent(working, current, a, b);
        bool Likes(int a, int b) => OfficeRelationList(working, current, a, b, 0xC0);
        bool Dislikes(int a, int b) => OfficeRelationList(working, current, a, b, 0xD0);
        bool Parent(int a, int b) => Valid(a) && Valid(b) && Id(a) != Id(b) &&
            (OfficeWord(working, a, 0xB4) != 0 && OfficeWord(working, a, 0xB4) == Id(b) ||
             OfficeWord(working, a, 0xB6) != 0 && OfficeWord(working, a, 0xB6) == Id(b));
        bool ParentOrGrandparent(int a, int b) => Parent(a, b) ||
            Parent(Lookup(OfficeWord(working, a, 0xB4)), b) || Parent(Lookup(OfficeWord(working, a, 0xB6)), b);
        List<int> People(HexListHandle list)
        {
            var result = new List<int>();
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("follower_head");
            while (node.Identity.HasValue)
            {
                result.Add(K(current.ReadRegistryNodePayload(node)?.PhysicalSlot, "follower_payload"));
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("follower_next");
            }
            return result;
        }
        void SortFollowers()
        {
            var order = new List<HexListNodeReference>();
            var node = current.ReadHexListHead(followers) ?? throw Missing("sort_head");
            while (node.Identity.HasValue) { order.Add(node); node = current.ReadHexListNodeNext(node) ?? throw Missing("sort_next"); }
            if (order.Count == 0) return;
            int PersonAt(HexListNodeReference at) => K(current.ReadRegistryNodePayload(new(at.Identity))?.PhysicalSlot, "sort_payload");
            bool Before(int a, int b)
            {
                bool first = Sworn(a, rebelPhysical), second = Sworn(b, rebelPhysical);
                if (first != second) return first;
                first = Spouse(a, rebelPhysical); second = Spouse(b, rebelPhysical);
                if (first != second) return first;
                byte officeA = K(current.ReadMoraleWavePersonPostE7(a), "sort_officeA"), officeB = K(current.ReadMoraleWavePersonPostE7(b), "sort_officeB");
                return officeA != officeB ? officeA < officeB : Id(a) < Id(b);
            }
            for (int i = 1; i < order.Count; i++)
            {
                var value = order[i]; int at = i;
                while (at > 0 && Before(PersonAt(value), PersonAt(order[at - 1]))) { order[at] = order[at - 1]; at--; }
                order[at] = value;
            }
            current.ReorderCurrentList(followers, order);
        }
        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => StrategyMissing("independence_followers." + field);
    }

    private static bool StrategicIndependenceSwornCurrent(GameState state, SourceWorldContext current, int a, int b)
        => StrategyNeed(current.ReadPersonNativeValidity(a), "independence_sworn_first_validity") &&
            StrategyNeed(current.ReadPersonNativeValidity(b), "independence_sworn_second_validity") &&
            StrategyNeed(current.ReadPersonIdentity(a), "independence_sworn_first_ID") != StrategyNeed(current.ReadPersonIdentity(b), "independence_sworn_second_ID") &&
            OfficeWord(state, a, 0xBC) != 0 && OfficeWord(state, a, 0xBC) == OfficeWord(state, b, 0xBC);

    private static bool StrategicIndependenceSpouseCurrent(GameState state, SourceWorldContext current, int a, int b)
        => StrategyNeed(current.ReadPersonNativeValidity(a), "independence_spouse_first_validity") &&
            StrategyNeed(current.ReadPersonNativeValidity(b), "independence_spouse_second_validity") &&
            StrategyNeed(current.ReadPersonIdentity(a), "independence_spouse_first_ID") != StrategyNeed(current.ReadPersonIdentity(b), "independence_spouse_second_ID") &&
            OfficeWord(state, a, 0xBA) != 0 && OfficeWord(state, a, 0xBA) == StrategyNeed(current.ReadPersonIdentity(b), "independence_spouse_other_ID");

    internal GameState ApplyStrategicIndependencePlacementCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int person, BaseReference home, NativeMapSeasonDate date)
    {
        int homeId = K(current.ReadBaseObjectLink(home), "Home4E");
        current.WritePersonFirstLocation(person, (ushort)Math.Min(homeId, 61));
        var position = PersonReturnCommands.ResolvePersonPosition(state, current, person, date);
        if (current.ReadOwnedPersonCommandManager() is { } positioned) state = state with { PersonnelCommands = positioned };
        if (position.PhysicalSlot == current.ReadBaseCurrentHexIndex(home))
        {
            current.WritePersonSecondLocation(person, (ushort)Math.Min(homeId, 1061));
            return OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
        }
        var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("associated");
        if (K(current.ReadLinkedEntityNativeValidity(linked), "associated_validity"))
        {
            var detached = PersonReturnCommands.DetachPersonFromFirstAssociated(state, current, person);
            state = state with { PersonnelCommands = detached.Owner ?? throw Missing(detached.UnresolvedOwner ?? "member_detachment") };
        }
        if (K(current.ReadPersonStatus(person), "PersonE6") == 1)
        {
            var destinationDistrict = current.ReadBaseDistrictReference(home) ?? throw Missing("Home_District");
            int leader = K((current.ReadPersonLookupReference(K(current.ReadDistrictLeader(K(destinationDistrict.PhysicalSlot,
                "Home_District_slot")), "Home_District_leader")))?.PhysicalSlot, "Home_District_leader_pointer");
            if (!K(current.ReadPersonNativeValidity(leader), "Home_District_leader_validity") ||
                K(current.ReadPersonStatus(leader), "Home_District_leader_status") != 1)
            {
                int district = K(current.ReadPersonDistrictSlot(person), "ruler_DistrictE0");
                if (home.Kind != BaseKind.City) throw Missing("ruler_nonprimary_Gate_Home");
                current.SetCityDistrictSlot(K(home.PhysicalSlot, "Home_City"), (byte)Math.Min(district, 51));
                foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("ruler_Person100"))
                {
                    int member = K(entry, "ruler_Person");
                    if (K(current.ReadPersonDistrictSlot(member), "ruler_member_District") != district ||
                        K(current.ReadPersonStatus(member), "ruler_member_status") is < 1 or > 4) continue;
                    var associations = new List<PersonAssignmentPrecallResult>();
                    PersonDistrictTransfer.Apply(member, district, ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, associations));
                    if (associations.Count != 0) state = PersonAssignmentPrecallInputs.PublishDeathTouched(state, current, member, associations);
                }
            }
        }
        state = PersonReturnCommands.StartIndependenceFollowerMove(state, current, person, home, position, date);
        var force = current.ReadPersonForce(person) ?? throw Missing("Person_Force");
        foreach (var at in current.ReadRegisteredBases() ?? throw Missing("Base80"))
        {
            if (at is null || !K(current.ReadBaseNativeValidity(at), "Base_validity") || current.ReadBaseForceSlot(at) != force.PhysicalSlot) continue;
            ApplyPersonFreedomGovernorCurrent(state, current, at);
        }
        state = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });

        T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        InvalidDataException Missing(string field) => StrategyMissing("independence_placement." + field);
    }
}

public static partial class PersonReturnCommands
{
    //2DB250 retains the old22D6C0 position before detaching its association.
    //1F6590 therefore starts from that actual Hex, although E2 already names
    //the new Home. Ordinary player reassignment keeps its existing contract.
    internal static GameState StartIndependenceFollowerMove(GameState state, SourceWorldContext current,
        int person, BaseReference destination, HexReference startingPosition, NativeMapSeasonDate date)
    {
        var members = current.CreateNearbyTemporary();
        try
        {
            var node = current.AllocateRegistryNode(new(members.Identity))
                ?? throw Missing("independence movement Person temporary");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, person));
            //The actual1F0010 call observes the allocated member chain.
            if (Get(current.ReadTemporaryHexListCount(members), "independence movement member count") == 0) return state;
            var before = CurrentCommandOwner(state, current);
            if (before.AvailableDispatchSlots.Length == 0) return state; //Real1DE290 admission failure.
            ushort second = Get(current.ReadPersonSecondLocation(person), "independence movement E4");
            if (second == Get(current.ReadBaseObjectLink(destination), "independence movement destination4E")) return state;
            int id = Get(current.ReadPersonIdentity(person), "independence movement Person10");
            int startHex = Get(startingPosition.PhysicalSlot, "independence movement source Hex");
            var target = ToDestination(destination);
            int taskId = before.NextTaskId, slot = before.AvailableDispatchSlots[0];
            byte district = unchecked((byte)Get(current.ReadBaseDistrictSlot(destination), "independence movement District"));
            var force = current.ReadPersonForce(person) ?? throw Missing("independence movement Force");
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("independence movement player");
            bool isPlayer = player.PhysicalSlot is int own && own != 0 && own == force.PhysicalSlot;
            var task = new PersonnelReturnTask(taskId, slot, [], [target], target, district, startHex,
                false, false, isPlayer, false, true, date, new(0, 1, 1, 0)) { IsReassignment = true };
            var owner = before with { NextTaskId = checked(taskId + 1), AvailableDispatchSlots = before.AvailableDispatchSlots[1..],
                Returns = new(before.Returns) { [taskId] = task } };
            var handle = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("independence movement association index");
            if (!current.AppendPersonReturnAssociated(new(handle.Identity), taskId))
                throw Missing("independence movement association registration");
            current.SetOwnedPersonCommandManager(owner);
            current.SetLinkedEntityRawState58(taskId, slot);
            current.SetLinkedEntityMemberIds(taskId, []);
            current.SetAssociatedPayloadPostLoadCallback(taskId, PostLoadCallback.NoOp);
            ushort flags = Get(current.ReadArmyReturnPersonFlags196(person), "independence movement flags");
            current.WriteArmyReturnPersonFlags196(person, (ushort)(flags | 1));
            //1F6590's per-member getter is after registration while the new
            //receiver still has an empty member list.
            var position = ResolvePersonPosition(state, current, person, date);
            ushort area = Get(current.ReadHexAreaSlot(position), "independence movement current Area");
            current.WritePersonSecondLocation(person, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
            task = task with { MemberIds = [id] };
            owner = CurrentCommandOwner(state, current);
            owner = owner with { Returns = new(owner.Returns) { [taskId] = task } };
            current.SetOwnedPersonCommandManager(owner);
            current.SetLinkedEntityMemberIds(taskId, [(uint?)id]);

            //22A9E0/22D0D0 chooses the starting Base (or its Area's City)
            //for2ACD10; absence really leaves1F6590's duration at zero.
            BaseReference startBase = new(BaseKind.City, null);
            foreach (var at in current.ReadRegisteredBases() ?? throw Missing("independence movement Base80"))
                if (at is not null && Get(current.ReadBaseNativeValidity(at), "independence movement Base validity") &&
                    current.ReadBaseCurrentHexIndex(at) == startHex) { startBase = at; break; }
            if (!Get(current.ReadBaseNativeValidity(startBase), "independence movement start Base validity"))
            {
                ushort rawArea = Get(current.ReadHexAreaSlot(startingPosition), "independence movement start Area");
                byte city = Get(current.ReadAreaCitySlot(new(rawArea < 501 ? rawArea : 0)), "independence movement start City");
                startBase = current.ReadCityTableReference(city < 52 ? city : 0) ?? throw Missing("independence movement City pointer");
            }
            int days = 0;
            if (Get(current.ReadBaseNativeValidity(startBase), "independence movement origin validity"))
            {
                var sourceHex = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(startBase))
                    ?? throw Missing("independence movement origin Hex");
                var endHex = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(destination))
                    ?? throw Missing("independence movement target Hex");
                var path = current.CreateNearbyTemporary();
                try
                {
                    var route = PersonReturnPathSource.Fill(state, current, sourceHex, endHex, force, path);
                    days = Get(route.Days, route.UnresolvedOwner ?? "independence movement 2ACD10_2318F0");
                }
                finally { current.ClearNearbyTemporary(path, true); }
            }
            task = task with { ArriveOn = DateFromOrdinal(unchecked(DateOrdinal(date) + days)) };
            owner = CurrentCommandOwner(state, current);
            owner = owner with { Returns = new(owner.Returns) { [taskId] = task } };
            current.SetOwnedPersonCommandManager(owner);
            var result = state with { PersonnelCommands = owner };
            BindCurrent(result, current);
            return PublishBorrowedCurrent(result, current);
        }
        finally { current.ClearNearbyTemporary(members, true); }
    }
}

public sealed partial class CaptureReturningPersonHomeSource
{
    //260310's empty same-District list enters a separate Force-City list.
    //254000 uses capital/District Home/number/City identity, without reading
    //the distance160 fields used by the earlier nonempty-District branch.
    private ReturningPersonHomeResult ApplyIndependenceForceFallback(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int person, int personId, ushort oldHome,
        bool idle, UnitState? activeArmy, Dictionary<int, int> distances,
        ArmyReturnDistrictOperationQueries operation, NativeMapSeasonDate date,
        List<CampaignEffectRequest> feedback)
    {
        var cities = current.CreateNearbyTemporary();
        try
        {
            int force = Need(current.ReadPersonOldForceSlot(person), "fallback_Person_Force");
            var rows = new List<(BaseReference City, HexListNodeReference Node)>();
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw Missing("fallback_City_pointer");
                if (Need(current.ReadBaseForceSlot(city), "fallback_City_Force") != force ||
                    Need(current.ReadBaseObjectLink(city), "fallback_City4E") == oldHome) continue;
                var appended = current.AllocateRegistryNode(new(cities.Identity)) ?? throw Missing("fallback_City_node");
                if (appended.Identity.HasValue)
                {
                    current.WriteRegistryNodePayload(appended, new(RegistryKind.City, Need(city.PhysicalSlot, "fallback_City_slot")));
                    rows.Add((city, new(appended.Identity)));
                }
            }
            for (int i = 1; i < rows.Count; i++)
            {
                var value = rows[i]; int at = i;
                while (at > 0 && Before(value.City, rows[at - 1].City)) { rows[at] = rows[at - 1]; at--; }
                rows[at] = value;
            }
            if (rows.Count != 0) current.ReorderCurrentList(cities, rows.Select(row => row.Node).ToArray());
            var chosen = rows.Count == 0 ? new BaseReference(BaseKind.City, null) : rows[0].City;
            var governors = new List<ArmyReturnGovernorPacket>();
            if (Need(current.ReadBaseNativeValidity(chosen), "fallback_selected_validity"))
            {
                var previousDistrict = current.ReadDistrictTableReference(Need(current.ReadPersonDistrictSlot(person), "fallback_PersonE0"))
                    ?? throw Missing("fallback_old_District");
                current.WritePersonFirstLocation(person, Math.Min(Need(current.ReadBaseObjectLink(chosen), "fallback_selected4E"), (ushort)61));
                var destinationDistrict = current.ReadBaseDistrictReference(chosen) ?? throw Missing("fallback_new_District");
                governors.AddRange(ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(destinationDistrict, current, operation));
                governors.AddRange(ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(previousDistrict, current, operation));
            }
            var working = operation.Latest();
            CapturedArmyHomeWrites? armyHome = activeArmy is null ? null : ApplyDeployedArmyPrisonerHomes(working, current, person, activeArmy);
            working = Publish(working, current, person, governors, distances, pool, armyHome, projectOwnedRoles: true);
            if (idle)
            {
                var owner = current.ReadPersonForce(person) ?? throw Missing("fallback_return_Force");
                var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(person), "fallback_return_E2"), current);
                if (Need(current.ReadForceNativeValidity(owner), "fallback_return_Force_validity") &&
                    Need(current.ReadBaseNativeValidity(home), "fallback_return_Home_validity") &&
                    current.ReadBaseForceSlot(home) == owner.PhysicalSlot)
                {
                    var returned = ApplyIdleReturn(working, current, person, date);
                    working = returned.State ?? throw Missing(returned.UnresolvedOwner ?? "fallback_return2EA9A0");
                }
                else
                {
                    TacticSourceSamplerWorld Sampler() => package.ReadArmyDestructionSampler(working, current)
                        ?? throw Missing("fallback_freedom_World450_45C");
                    working = package.ApplyCaptiveFreedomCurrent(working, current, pool, person, date,
                        () => Sampler().Day450, Sampler, feedback);
                }
            }
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
            return new(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() }),
                new(personId, person, oldHome, Need(current.ReadPersonFirstLocation(person), "fallback_published_E2"),
                    [], rows.Select(row => Need(row.City.PhysicalSlot, "fallback_sorted_City")).ToArray(), governors,
                    ["260310.empty_District_list→2243A0_actual_Force_City_list→24C500_254000→260B29_current_Home"]), null);

            bool Before(BaseReference a, BaseReference b)
            {
                int ruler = Need((current.ReadPersonLookupReference(Need(current.ReadForceRecord(force)?.RawRuler10,
                    "fallback_ruler10")))?.PhysicalSlot, "fallback_ruler_pointer");
                var capital = CurrentGateMapQueries.FindCity(BaseLookup.Find(Need(current.ReadPersonFirstLocation(ruler), "fallback_ruler_E2"), current), current);
                bool left = Need(current.ReadBaseObjectLink(a), "fallback_first4E") == Need(current.ReadBaseObjectLink(capital), "fallback_capital4E");
                bool right = Need(current.ReadBaseObjectLink(b), "fallback_second4E") == Need(current.ReadBaseObjectLink(capital), "fallback_capital4E");
                if (left != right) return left;
                var first = current.ReadBaseDistrictReference(a) ?? throw Missing("fallback_first_District");
                var second = current.ReadBaseDistrictReference(b) ?? throw Missing("fallback_second_District");
                bool primaryA = Primary(first), primaryB = Primary(second);
                if (primaryA != primaryB) return !primaryA;
                if (!primaryA)
                {
                    left = DistrictHome(first) == a; right = DistrictHome(second) == b;
                    if (left != right) return left;
                    byte one = Need(current.ReadDistrictNumber(Need(first.PhysicalSlot, "fallback_first_District_slot")), "fallback_first_number");
                    byte two = Need(current.ReadDistrictNumber(Need(second.PhysicalSlot, "fallback_second_District_slot")), "fallback_second_number");
                    if (one != two) return one < two;
                }
                return Need(a.PhysicalSlot, "fallback_first_City") < Need(b.PhysicalSlot, "fallback_second_City");
            }
            int Leader(DistrictReference district) => Need((current.ReadPersonLookupReference(Need(current.ReadDistrictLeader(
                Need(district.PhysicalSlot, "fallback_District_slot")), "fallback_District_leader")))?.PhysicalSlot, "fallback_District_leader_pointer");
            bool Primary(DistrictReference district)
            {
                int leader = Leader(district);
                return Need(current.ReadPersonNativeValidity(leader), "fallback_leader_validity") && Need(current.ReadPersonStatus(leader), "fallback_leader_status") == 1;
            }
            BaseReference DistrictHome(DistrictReference district) => CurrentGateMapQueries.FindCity(BaseLookup.Find(
                Need(current.ReadPersonFirstLocation(Leader(district)), "fallback_leader_E2"), current), current);
        }
        finally { current.ClearNearbyTemporary(cities, true); }
    }
}

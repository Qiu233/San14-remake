using San14.Core;

namespace San14.Data;

public sealed record ReturningPersonHomeCandidate(int CityPhysical, ushort Location4E, int Distance160,
    bool DistrictHome, bool? PathFound, int[]? PathHexes);
public sealed record ReturningPersonHomeReceipt(int OfficerId, int PersonPhysical, int OldHome, int NewHome,
    ReturningPersonHomeCandidate[] CandidateOrder, int[] SortedCityOrder,
    IReadOnlyList<ArmyReturnGovernorPacket> GovernorUpdates, IReadOnlyList<string> SourceEffectOrder)
{
    public CapturedArmyHomeWrites? ArmyHome { get; init; }
    public bool UsedGateParentCity { get; init; }
    public int? ReturnNative { get; init; }
    public int? ReturnCommandId { get; init; }
    public int? ReturnTravelDays { get; init; }
}
public sealed record ReturningPersonHomeResult(GameState? State,
    ReturningPersonHomeReceipt? Receipt, string? UnresolvedOwner);

/// <summary>2851B0→260310 ordinary busy Return/deployed commander and already
/// noncaptured idle Base residents, with nonempty same-District City candidates.
/// Source Home selection and Primary callbacks precede any reached new Return.</summary>
public sealed partial class CaptureReturningPersonHomeSource(ArmyCampaignRuleAdapter package,
    OwnedArmyGovernorComparatorInputs governorInputs) : IPlayableCaptureReturningPersonRules
{
    public const string Revision = "ce-2851B0-260310-busy-Return-Home";
    public const string IdleRevision = "ce-2851B0-260310-idle-Base-Home";
    public static string CityDistance160Key(int physical) => $"ce-city-home-choice-current:{physical}:160";

    public PlayableCaptureReturningPersonResolution? ChangeCapturedHome(GameState state, int capturedLocationId,
        int previousForceId, OfficerState officer)
    {
        var result = TryApply(state, capturedLocationId, previousForceId, officer.Id);
        return result.State is { } next ? new(next, result.Receipt!.NewHome, Revision) : null;
    }

    public PlayableCaptureReturningPersonResolution? ChangeCapturedHome(GameState state, int capturedLocationId,
        int previousForceId, OfficerState officer, CityState beforeCaptureCity)
    {
        var result = TryApply(state, capturedLocationId, previousForceId, officer.Id, beforeCaptureCity);
        return result.State is { } next ? new(next, result.Receipt!.NewHome, Revision) : null;
    }

    /// <summary>Source260310 runs before26DA10. With an already transferred
    /// City, the caller must provide its real former owner/District snapshot.</summary>
    public ReturningPersonHomeResult TryApply(GameState state, int capturedLocationId, int previousForceId, int officerId)
    {
        if (!state.Cities.TryGetValue(capturedLocationId, out var city) || city.ForceId != previousForceId)
            return new(null, null, "current.preCaptureCity_original_owner_District");
        return ApplyBeforeTransfer(state, capturedLocationId, previousForceId, officerId);
    }

    public ReturningPersonHomeResult TryApply(GameState state, int capturedLocationId, int previousForceId,
        int officerId, CityState beforeCaptureCity)
        => ApplyWithPreviousOwner(state, capturedLocationId, previousForceId, officerId, beforeCaptureCity, null);

    /// <summary>2851B0 has already evaluated27E870=false for this ordinary
    /// resident. Consume its actual260310 call before Base ownership transfer.</summary>
    public ReturningPersonHomeResult TryApplyIdleBase(GameState beforeTransfer, int capturedLocationId,
        int previousForceId, int officerId, NativeMapSeasonDate? operationThirdDate = null) =>
        ApplyBeforeTransfer(beforeTransfer, capturedLocationId, previousForceId, officerId,
            idleResident: true, operationThirdDate: operationThirdDate);

    //2851B0 reaches260310 for every noncaptured ordinary Home owner. Read
    //the actual232370 predicate instead of treating an out-of-Base actor as idle.
    internal ReturningPersonHomeResult TryApplyHomeCurrent(GameState state, SourceWorldContext current,
        int capturedLocationId, int previousForceId, int officerId, NativeMapSeasonDate? operationThirdDate,
        ArmyReturnDistrictOperationQueries? districtOperation = null, List<CampaignEffectRequest>? feedback = null)
    {
        try
        {
            int person = current.ReadPersonLookupReference(officerId)?.PhysicalSlot
                ?? throw Missing("current.260310_dispatch_Person_pointer");
            if (current.ReadPersonIdentity(person) != officerId ||
                !Need(current.ReadPersonNativeValidity(person), "current.260310_dispatch_Person_validity") ||
                Need(current.ReadPersonStatus(person), "current.260310_dispatch_PersonE6") is < 1 or > 4)
                throw Missing("branch.260310_dispatch_nonordinary_Person");
            bool busy = PersonCurrentBusySource.Read(current, person,
                owner => Missing("current.260310_dispatch_busy." + owner));
            var army = PersonArmyLocationSource.ReadCommanderArmy(current, person)
                ?? throw Missing("current.260310_dispatch_22A200_Army");
            UnitState? activeArmy = null;
            if (Need(current.ReadArmyUnitNativeValidity(army), "current.260310_dispatch_Army_validity"))
            {
                int physical = Need(army.PhysicalSlot, "current.260310_dispatch_Army_physical");
                var arena = state.ReadArmyLifecycle() ?? throw Missing("current.260310_dispatch_Army_arena");
                var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId)
                    ?? throw Missing("current.260310_dispatch_Army_receiver");
                if (receiver.ActiveUnitId is not int unitId || !state.Units.TryGetValue(unitId, out activeArmy) ||
                    activeArmy.NativeArmyOrigin?.NativeSlotIndex != physical || activeArmy.OfficerId != officerId)
                    throw Missing("current.260310_dispatch_active_Commander_Army");
            }
            return ApplyBeforeTransfer(state, capturedLocationId, previousForceId, officerId, activeArmy,
                idleResident: !busy, operationThirdDate: operationThirdDate, operationCurrent: current, currentBusy: busy,
                districtOperation: districtOperation, feedback: feedback);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private ReturningPersonHomeResult ApplyWithPreviousOwner(GameState state, int capturedLocationId,
        int previousForceId, int officerId, CityState beforeCaptureCity, UnitState? activeArmy)
    {
        if (beforeCaptureCity.Id != capturedLocationId || beforeCaptureCity.ForceId != previousForceId ||
            beforeCaptureCity.DistrictId is not (>= 1 and <= 51) || !state.Cities.TryGetValue(capturedLocationId, out var captured))
            return new(null, null, "current.preCaptureCity_original_owner_District");
        if (captured.DistrictId is not (>= 1 and <= 51))
            return new(null, null, "branch.26DA10_invalid_new_District_transfer");
        //Only the source owner/District view is pre-transfer. Keep this
        //operation's actual resources, ready3C, wounded40, walls and raw2E.
        var sourceView = state with { Cities = new(state.Cities) { [capturedLocationId] = captured with
            { ForceId = beforeCaptureCity.ForceId, DistrictId = beforeCaptureCity.DistrictId } } };
        var applied = ApplyBeforeTransfer(sourceView, capturedLocationId, previousForceId, officerId, activeArmy);
        if (applied.State is not { } current) return applied;
        var city = current.Cities[capturedLocationId];
        //26DA10's ordinary valid-District transfer preserves the actual raw2E.
        //The Core takeover projection already owns Force/District/GovernorId.
        return applied with { State = current with { Cities = new(current.Cities) { [capturedLocationId] = city with
            { ForceId = captured.ForceId, DistrictId = captured.DistrictId, GovernorId = captured.GovernorId } } } };
    }

    private ReturningPersonHomeResult ApplyBeforeTransfer(GameState state, int capturedLocationId, int previousForceId,
        int officerId, UnitState? activeArmy = null, bool idleResident = false, NativeMapSeasonDate? operationThirdDate = null,
        SourceWorldContext? operationCurrent = null, bool? currentBusy = null,
        ArmyReturnDistrictOperationQueries? districtOperation = null, List<CampaignEffectRequest>? feedback = null)
    {
        SourceWorldContext? current = null;
        HexListHandle? cities = null;
        try
        {
            if (capturedLocationId < 1 || capturedLocationId > (idleResident ? 61 : 51) || previousForceId is < 1 or > 45 ||
                !state.Officers.TryGetValue(officerId, out var officer) || officer.LocationId != capturedLocationId ||
                officer.ForceId != previousForceId || (idleResident ? officer.Deployed : activeArmy is null
                    ? officer.Deployed || (currentBusy is null && !PlayableOfficerAvailability.IsSourceBusy(state, officer))
                    : !officer.Deployed || activeArmy.OfficerId != officerId || !state.Units.ContainsKey(activeArmy.Id)))
                throw Missing("branch.not_ordinary_returning_captured_Home");
            var world = operationCurrent is null ? package.CaptureOwnedArmyReturnWorld(state) :
                OwnedArmyReturnWorld.Borrow(state, operationCurrent, operationCurrent.ReadOwnedCurrentListPool()
                    ?? throw Missing("borrowed_current_pool"));
            current = world.Context; var pool = world.Pool;
            int person = current.ReadPersonLookupReference(officerId)?.PhysicalSlot ?? throw Missing("current.Person_pointer");
            byte status = Need(current.ReadPersonStatus(person), "current.PersonE6");
            if (!Need(current.ReadPersonNativeValidity(person), "current.Person_validity") || status is < 1 or > 4)
                throw Missing("branch.nonordinary_Person");
            ushort first = Need(current.ReadPersonFirstLocation(person), "current.PersonE2");
            ushort second = Need(current.ReadPersonSecondLocation(person), "current.PersonE4");
            var firstPosition = PersonPositionRepair.DecodeFirstLocation(first);
            var secondPosition = PersonPositionRepair.DecodeSecondLocation(second);
            if (first != capturedLocationId || (idleResident
                ? (districtOperation is null && status is not (3 or 4)) || first != second
                : currentBusy is null && (firstPosition == secondPosition ||
                    (activeArmy is null ? secondPosition.Kind == 27 : secondPosition.Kind != 27))))
                throw Missing("branch.260310_not_busy_Return_position");
            if (idleResident && currentBusy is null) RequireIdle232370(current, person);
            //Busy leaves short-circuit232370 on position mismatch. The idle
            //leaf also reads its real associated receiver before busy0.
            var district = current.ReadDistrictTableReference(Need(current.ReadPersonDistrictSlot(person), "current.PersonE0"))
                ?? throw Missing("current.original_District");
            int districtSlot = Need(district.PhysicalSlot, "current.original_District_pointer");
            var force = current.ReadPersonForce(person) ?? throw Missing("current.original_Force");
            if (Need(current.ReadForcePhysicalSlot(force), "current.original_Force_ordinal") != previousForceId)
                throw Missing("branch.changed_current_Force");
            var oldHome = BaseLookup.Find(first, current);
            if (idleResident && districtOperation is null && (Need(current.ReadBaseForceSlot(oldHome), "current.preCaptureBase_original_Force") != previousForceId ||
                Need(current.ReadBaseDistrictSlot(oldHome), "current.preCaptureBase_original_District") != districtSlot))
                throw Missing("current.preCaptureBase_original_owner_District");
            cities = NewTemporary(current); //260310 ptr_list<CCity>.
            var candidates = new List<(ReturningPersonHomeCandidate Evidence, HexListNodeReference Node)>();
            var writes160 = new Dictionary<int, int>();
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw Missing("current.City_pointer_table");
                if (Need(current.ReadBaseDistrictSlot(city), "current.City_V78") != districtSlot) continue;
                ushort location = Need(current.ReadBaseObjectLink(city), "current.City4E");
                if (location == first) continue;
                int physical = Need(city.PhysicalSlot, "current.City_physical");
                int distance = 48400; bool? found = null; int[]? path = null;
                writes160[physical] = distance; //Real signed160 sentinel store.
                var outer = NewTemporary(current); //Per-candidate Hex output.
                try
                {
                    if (Need(current.ReadBaseNativeValidity(oldHome), "current.old_Base_validity") &&
                        Need(current.ReadBaseNativeValidity(city), "current.candidate_Base_validity"))
                    {
                        var start = Hex(current, current.ReadBaseCurrentHexIndex(oldHome));
                        var end = Hex(current, current.ReadBaseCurrentHexIndex(city));
                        var route = PersonReturnPathSource.FillCityHomeChoicePath(current, start, end, outer);
                        found = route.NativePathFound ?? throw Missing(route.UnresolvedOwner ?? "current.2AEDC0_city_choice");
                        path = route.PathHexes;
                        if (found == true) writes160[physical] = distance = Need(route.OutputNodeCount, "current.City160_path_count");
                    }
                    var node = current.AllocateBaseRegistryNode(new(cities.Identity)) ?? throw Missing("current.City_candidate_node");
                    if (node.Identity is int identity)
                    {
                        current.WriteBaseRegistryNodePayload(node, city);
                        candidates.Add((new(physical, location, distance, IsDistrictHome(current, city), found, path), new(identity)));
                    }
                }
                finally { ClearRelease(current, outer); }
            }
            if (candidates.Count == 0)
            {
                if (districtOperation is null) throw Missing("branch.260310_no_same_District_City_Force_fallback");
                return ApplyIndependenceForceFallback(state, current, pool, person, officerId, first,
                    idleResident, activeArmy, writes160, districtOperation,
                    operationThirdDate ?? throw Missing("current.independence_260310_date"),
                    feedback ?? throw Missing("current.independence_260310_report_owner"));
            }
            //24C2F0's input and074560's copied empty Hex functor both allocate
            //actual descriptors.253F20 ignores the stored oldBase argument.
            var originalOrder = candidates.Select(x => x.Evidence).ToArray();
            BaseReference? chosen = null;
            bool gateParent = false;
            if (idleResident && first is >= 52 and <= 61)
            {
                var parent = CurrentGateMapQueries.FindCity(oldHome, current); //22A940.
                if (Need(current.ReadBaseForceSlot(parent), "current.260310_Gate_parent_Force") == previousForceId &&
                    Need(current.ReadBaseDistrictSlot(parent), "current.260310_Gate_parent_District") == districtSlot)
                { chosen = parent; gateParent = true; } //260B29, without24C2F0.
            }
            if (chosen is null)
            {
                var functor = NewTemporary(current); var copiedFunctor = NewTemporary(current);
                try
                {
                    for (int i = 1; i < candidates.Count; i++)
                    {
                        var value = candidates[i]; int at = i;
                        while (at > 0 && Preferred(value.Evidence, candidates[at - 1].Evidence))
                        { candidates[at] = candidates[at - 1]; at--; }
                        candidates[at] = value;
                    }
                    current.ReorderCurrentList(cities, candidates.Select(row => row.Node).ToArray());
                }
                finally { ClearRelease(current, copiedFunctor); ClearRelease(current, functor); }
                chosen = new(BaseKind.City, candidates[0].Evidence.CityPhysical);
            }
            if (!Need(current.ReadBaseNativeValidity(chosen), "current.selected_City_validity"))
                throw Missing("branch.260310_selected_City_invalid");
            var newDistrict = current.ReadCityDistrictReference(chosen) ?? throw Missing("current.selected_City_V80");
            ushort destination = (ushort)Math.Min(Need(current.ReadBaseObjectLink(chosen), "current.selected_City4E"), (ushort)61);
            current.WritePersonFirstLocation(person, destination);
            IArmyReturnGovernorCurrentQueries queries = districtOperation is not null
                ? districtOperation : new Queries(state, governorInputs, current);
            var governors = new List<ArmyReturnGovernorPacket>();
            governors.AddRange(ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(newDistrict, current, queries));
            governors.AddRange(ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(district, current, queries));
            CapturedArmyHomeWrites? armyHome = activeArmy is null ? null :
                ApplyDeployedArmyPrisonerHomes(state, current, person, activeArmy);
            //Return/Area skips22A200's Army-kind27. A deployed commander
            //takes its actual230C70 status6 prisoner branch instead. Both
            //saved busy1 leaves skip2EA9A0 and preserve E4/status/Army fields.
            PersonReturnCommandResult? returning = null;
            var working = districtOperation?.Latest() ?? state;
            if (idleResident)
            {
                RequireIdleNoCommanderArmy(current);
                //The live City candidate descriptor survives Primary and
                //2EA9A0; release it only after the real Return allocations.
                working = Publish(working, current, person, governors, writes160, pool, armyHome, projectOwnedRoles: true);
                returning = ApplyIdleReturn(working, current, person, operationThirdDate);
                working = returning.State ?? throw Missing(returning.UnresolvedOwner ?? "current.260310_2EA9A0_Return");
            }
            ClearRelease(current, cities); cities = null;
            var result = Publish(working, current, person, governors, writes160, pool, armyHome,
                projectOwnedRoles: operationCurrent is not null);
            if (idleResident) result = PublishIdleReturnFields(result, current, officerId, person);
            var effects = new List<string> { idleResident ? "260310.232370_busy0" : "260310.232370_busy1", "224300.same_District_fixed_City_table",
                "2AEDC0.mode8_count_to_City160", gateParent ? "260310.22A940_same_Force_District_Gate_parent_City" : "24C2F0.253F20_DistrictHome_distance160_CityID", "260B44.PersonE2",
                "260B60.new_District2E9140", "260B6D.original_District2E9140" };
            if (armyHome is not null)
                effects.AddRange(["260B72.22A200_effective_Commander_Army", "230C70.manager100_status6_prisoner_temporary",
                    "260C46.each22A7A0_current_CommanderHome_to_260C5A_prisonerE2"]);
            if (idleResident) effects.Add("260B72.22A200_Base_position_Army0_invalid");
            effects.Add(idleResident ? "260CE4.busy0_2EA9A0" : "260CE4.busy1_skip_2EA9A0");
            if (idleResident) effects.Add("260D00.City_candidate_temporary_release_after_Return");
            return new(result, new(officerId, person, first, destination, originalOrder,
                candidates.Select(x => x.Evidence.CityPhysical).ToArray(), governors, effects) { ArmyHome = armyHome,
                    UsedGateParentCity = gateParent, ReturnNative = returning?.NativeReturn,
                    ReturnCommandId = returning?.CommandId, ReturnTravelDays = returning?.TravelDays }, null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
        finally { if (cities is not null && current is not null) ClearRelease(current, cities); }
    }

    private static GameState Publish(GameState state, SourceWorldContext current, int person,
        IReadOnlyList<ArmyReturnGovernorPacket> governors, Dictionary<int, int> distances, WorldIndexContext pool,
        CapturedArmyHomeWrites? armyHome, bool projectOwnedRoles = false)
    {
        var people = new Dictionary<int, OfficerState>(state.Officers);
        int id = Need(current.ReadPersonIdentity(person), "current.published_PersonID");
        people[id] = people[id] with { LocationId = Need(current.ReadPersonFirstLocation(person), "current.published_PersonE2") };
        foreach (var write in governors.SelectMany(x => x.PersonWrites))
        {
            int changedId = Need(current.ReadPersonIdentity(write.PersonPhysical), "current.Governor_PersonID");
            byte status = Need(current.ReadPersonStatus(write.PersonPhysical), "current.Governor_PersonE6");
            people[changedId] = people[changedId] with { Role = OfficerServiceRules.PublishedRole(
                    OfficerServiceStatusSourceAdapter.Decode(status), people[changedId].Role),
                ServiceStatus = OfficerServiceStatusSourceAdapter.Decode(status) };
        }
        if (armyHome is not null)
            foreach (var write in armyHome.PrisonerHomeWrites)
            {
                int prisoner = Need(current.ReadPersonIdentity(write.PersonPhysical), "current.230C70_PersonID");
                people[prisoner] = people[prisoner] with { LocationId = write.HomeE2 };
            }
        var cities = new Dictionary<int, CityState>(state.Cities);
        var gates = new Dictionary<int, GateState>(state.Gates);
        var simulation = state.NativeArmySimulation ?? throw Missing("current.City160_parameter_owner");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var governor in governors)
        {
            if (governor.Base.Kind == BaseKind.Gate)
            {
                int physical = Need(governor.Base.PhysicalSlot, "current.Governor_Gate");
                ushort raw = Need(current.ReadArmyReturnBaseGovernor2E(governor.Base), "current.Governor_Gate2E");
                int location = physical + 51;
                var gate = gates.GetValueOrDefault(location) ?? throw Missing("current.Governor_Gate_owner");
                gates[location] = gate with { GovernorId = raw == 0 ? null : raw };
                values[OwnedArmyLifecycleDetachedInputs.Key("Gate", physical, "2E")] = raw;
                continue;
            }
            int city = Need(governor.Base.PhysicalSlot, "current.Governor_City");
            ushort governorId = Need(current.ReadArmyReturnBaseGovernor2E(governor.Base), "current.Governor_City2E");
            var fields = cities[city].NativeReturnRuntime ?? throw Missing("current.Governor_City_owner");
            cities[city] = cities[city] with { GovernorId = governorId == 0 ? null : governorId,
                NativeReturnRuntime = fields with { Governor2E = governorId } };
        }
        foreach (var row in distances) values[CityDistance160Key(row.Key)] = row.Value;
        var units = armyHome is null ? state.Units : new(state.Units)
            { [armyHome.UnitId] = state.Units[armyHome.UnitId] with { SourceLocationId = armyHome.CommanderHomeE2 } };
        return (state with { Officers = people,
                Cities = cities,
                Gates = gates,
                Units = units,
                NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } }).WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = projectOwnedRoles ? pool.ProjectOwnedRoles() : pool.ExportOwnedState() });
    }
    private static bool Preferred(ReturningPersonHomeCandidate left, ReturningPersonHomeCandidate right) =>
        left.DistrictHome != right.DistrictHome ? left.DistrictHome : left.Distance160 != right.Distance160
            ? left.Distance160 < right.Distance160 : left.CityPhysical < right.CityPhysical;
    private static bool IsDistrictHome(SourceWorldContext current, BaseReference city)
    {
        var district = current.ReadCityDistrictReference(city) ?? throw Missing("current.253F20_City_District");
        ushort leader = Need(current.ReadDistrictLeader(Need(district.PhysicalSlot, "current.253F20_District_pointer")), "current.253F20_District12");
        var person = leader <= 6000 ? current.ReadPersonLookupReference(leader) : current.ReadTransferPersonTableReference(0);
        ushort home = Need(current.ReadPersonFirstLocation(Need(person?.PhysicalSlot, "current.253F20_leader_pointer")), "current.253F20_leaderE2");
        var homeBase = BaseLookup.Find(home, current);
        return Need(current.ReadBaseObjectLink(homeBase), "current.253F20_District_Home4E") == Need(current.ReadBaseObjectLink(city), "current.253F20_City4E");
    }
    private static HexReference Hex(SourceWorldContext current, int raw) =>
        current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("current.City_choice_Hex_pointer");
    private static HexListHandle NewTemporary(SourceWorldContext current) =>
        current.CreateTemporaryHexList() ?? throw Missing("current.260310_temporary_descriptor");
    private static void ClearRelease(SourceWorldContext current, HexListHandle handle)
    { current.ClearTemporaryHexList(handle); current.ReleaseTemporaryHexList(handle); }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string message) => new(message);
    private sealed class Queries(GameState state, OwnedArmyGovernorComparatorInputs inputs,
        SourceWorldContext current) : IArmyReturnGovernorCurrentQueries
    {
        public ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int physical) => inputs.ReadCurrentComparatorFields(state, physical);
        public int? ReadCurrentAssociatedVirtual98(PersonLinkedEntityReference reference) =>
            reference.Identity is int receiver && current.ReadOwnedPersonCommandManager()?.Returns.ContainsKey(receiver) == true
                ? 15 : null; //Actual1EDB40, read only after a live associated receiver is reached.
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public CaptureReturningPersonHomeSource CreateCaptureReturningPersonRules() => new(this, _governorInputs);
}

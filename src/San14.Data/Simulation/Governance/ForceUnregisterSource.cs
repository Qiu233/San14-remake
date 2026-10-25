using San14.Core;

namespace San14.Data;

public sealed record ForceUnregisterReceipt(int ForcePhysical, int CapturingForcePhysical,
    int RemainingOrdinaryForces, int RemainingPlayerForces, int[] Districts,
    int[] ChangedHexes, int ForceNodesRemoved, int DistrictNodesRemoved,
    ArmyReturnGovernorPacket[] GovernorUpdates, int[] RankedForces,
    IReadOnlyList<string> SourceEffectOrder);
public sealed record ForceUnregisterResult(GameState? State,
    ForceUnregisterReceipt? Receipt, string? UnresolvedOwner);

/// <summary>Ordinary non-player293BD0 numeric leaf after real265250 has
/// removed every current Force Person/Army. Empty Gate/Object destruction
/// branches are proved from current tables; residual Hex ownership is executed.
/// Original presentation/AI cache receivers are not constructed by this client.</summary>
public sealed partial class ForceUnregisterSource(ArmyCampaignRuleAdapter package)
{
    public const string Revision = "ce-293BD0-ordinary-empty-person-army-unregister";

    public ForceUnregisterResult TryApplyCurrent(GameState input, int victim, int captor)
        => TryApplyCurrent(input, victim, captor, null);

    public ForceUnregisterResult TryApplyCurrent(GameState input, int victim, int captor,
        Action<GameState>? publishReached)
        => ApplyCore(input, victim, captor, publishReached, null, null);

    internal ForceUnregisterResult TryApplyBorrowedCurrent(GameState input, int victim, int captor,
        SourceWorldContext current, WorldIndexContext pool)
        => ApplyCore(input, victim, captor, null, current, pool);

    /// <summary>2A2760's293BD0(from,null,1,1), after its actual Base,
    /// Person and territory transfers. The caller retains its live temporaries.</summary>
    internal ForceUnregisterResult TryApplyAbsorptionBorrowedCurrent(GameState input, int victim,
        SourceWorldContext current, WorldIndexContext pool)
        => ApplyCore(input, victim, 0, null, current, pool, absorption: true);

    internal ForceUnregisterResult TryApplyCaptureBorrowedCurrent(GameState input, int victim, int captor,
        SourceWorldContext current, WorldIndexContext pool, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects, Action<int>? resetArmyReceiver)
        => ApplyCore(input, victim, captor, null, current, pool, captureDate: date,
            captureEffects: effects, resetArmyReceiver: resetArmyReceiver);

    private ForceUnregisterResult ApplyCore(GameState input, int victim, int captor,
        Action<GameState>? publishReached, SourceWorldContext? borrowedCurrent, WorldIndexContext? borrowedPool,
        bool absorption = false, NativeMapSeasonDate? captureDate = null,
        List<CampaignEffectRequest>? captureEffects = null, Action<int>? resetArmyReceiver = null)
    {
        try
        {
            bool capture = captureDate is not null;
            if (!absorption && !capture) NativeForceUnregisterCurrentStateValidation.Validate(input);
            var saved = input.NativeForceUnregisterCurrent ?? throw Missing("saved_numeric_owner");
            var world = borrowedCurrent is null ? package.CaptureOwnedArmyReturnWorld(input)
                : OwnedArmyReturnWorld.Borrow(input, borrowedCurrent, borrowedPool
                    ?? throw Missing("borrowed_pool"));
            var current = world.Context; var pool = world.Pool;
            if (borrowedCurrent is null) ForceUnregisterInputs.BindCurrent(input, current);
            var victimReference = new ForceReference(victim);
            if (victim is < 1 or > 45 || captor is < 0 or > 45 ||
                captor == 0 && borrowedCurrent is null || victim == captor ||
                !Known(current.ReadForceNativeValidity(victimReference), "victim_validity") ||
                captor != 0 && !Known(current.ReadForceNativeValidity(new(captor)), "captor_validity")) throw Missing("branch.ordinary_valid_forces");
            int player = current.ReadCurrentPlayerForce()?.PhysicalSlot ?? throw Missing("World3A_player");
            if (!capture && victim == player) throw Missing("branch.player_terminal_service");
            var remaining = Enumerable.Range(0, 52).Where(i => i != victim && i is >= 1 and <= 45 &&
                Known(current.ReadForceNativeValidity(new(i)), "terminal_other_Force_validity")).ToArray();
            int playerCount = remaining.Count(i => i == player && player != 0);
            if (!capture && (remaining.Length == 0 || playerCount != 1)) throw Missing("branch.27A6B0_terminal_service");
            if (input.NativeArmySimulation!.World.SelectedForce1656 is not byte selected || saved.OtherForce1657 is not byte other)
                throw Missing("World1656_1657_current_interaction");
            var working = input;
            if (capture)
                working = package.ApplyForceUnregisterCoalitionCurrent(working, current, pool,
                    victim, captor, captureEffects ?? throw Missing("capture_effect_sink"));
            else if (selected == victim || other == victim) throw Missing("branch.277590_274DD0_current_interaction");
            ushort ruler = Need(current.ReadForceRecord(victim)?.RawRuler10, "victim_ruler10");
            //279EA0 only updates the platform achievement service. The
            //independent campaign deliberately does not implement Steam.
            if (!capture && ruler is 521 or 35) throw Missing("branch.279EA0_achievement_pair");
            if (!capture)
            {
                RequireEmptyPeopleAndArmies(input, current, victim, absorption);
                foreach (var entry in current.ReadRegisteredBases() ?? throw Missing("manager80"))
                    if (entry is not null && current.ReadBaseForceSlot(entry) == victim)
                        throw Missing("branch.293BD0_remaining_Base_or_Gate");
            }
            var effects = new List<string> { "27A6B0.remaining_ordinary_and_actual_player_nonterminal",
                capture ? "277590_then_274DD0.current_coalition_callbacks_and_293040_mode0"
                    : "277590_274DD0_279EA0.no_reached_numeric_writer",
                capture ? "293BD0.live_remaining_Gate_neutralization" : "293BD0.Gate_destruction_iterator_empty" };
            if (capture)
            {
                //param_1 is a pointer-to-pointer in the decompiler: +0x2F
                //is byte178, not byte15E. This is the coalition leader bit.
                var native = working.NativeArmySimulation ?? throw Missing("Force178_owner");
                var force = native.Forces.GetValueOrDefault(victim) ?? throw Missing("Force178_row");
                ushort flags = Need(force.DiplomacyFlags178, "Force178");
                working = working with { NativeArmySimulation = native with { Forces = new(native.Forces)
                    { [victim] = force with { DiplomacyFlags178 = (ushort)(flags & 0xFFFE) } } } };
            }
            else
            {
                int originalFlag = input.NativeArmySimulation.Parameters.Int32Values.GetValueOrDefault(
                    BattleRemakeCurrentForcePolicies.ForceFieldKey(victim, 0x15E), -1);
                if (originalFlag < 0 || originalFlag > 255) throw Missing("Force15E");
                var values = new Dictionary<string, int>(input.NativeArmySimulation.Parameters.Int32Values)
                    { [BattleRemakeCurrentForcePolicies.ForceFieldKey(victim, 0x15E)] = originalFlag & 0xFE };
                working = input with { NativeArmySimulation = input.NativeArmySimulation with
                    { Parameters = input.NativeArmySimulation.Parameters with { Int32Values = values } } };
            }
            if (capture) working = RemoveRemainingGatesCurrent(working, current, pool, victim,
                captureDate!, captureEffects ?? throw Missing("capture_effect_sink"));
            working = RemoveOwnedObjects(working, current, victim, publishReached, captureEffects);
            effects.Add("293BD0.live_Object_table_category2_4_removal");
            var objectTemporary = Temporary(current);
            var changedHexes = SettleHexes(working, current, victim, objectTemporary,
                capture ? (hex, force) => working = package.ApplyOrdinaryLastCityHexTransferCurrent(
                    working, current, hex, force, captureEffects!) : null);
            effects.Add("293Fxx.Object_temporary→48400_current_Hex_force14_writers→empty_Object13_clear");
            var territory = new Dictionary<int, TerritoryState>(working.Territory);
            foreach (int hex in changedHexes)
                if (territory.TryGetValue(hex, out var cell))
                {
                    byte force = Need(current.ReadHexForceSlot(new(hex)), "Hex14_publish");
                    territory[hex] = cell with { ForceId = force == 0 ? null : force };
                }
            var changed = changedHexes.ToHashSet();
            var areas = new Dictionary<int, AreaState>(working.Areas);
            foreach (var area in working.Areas.Values)
                if (area.HexId is int hex && changed.Contains(hex))
                {
                    byte force = Need(current.ReadAreaCurrentForceSlot(new(area.Id)), "Area_Hex14_publish");
                    areas[area.Id] = area with { ForceId = force == 0 ? null : force };
                }
            working = working with { Territory = territory, Areas = areas };
            if (capture)
            {
                working = package.WriteOrdinaryForceUnregisterHistoryCurrent(working, current, victim, captureEffects!);
                working = package.ApplyOrdinaryLastCityArmyCleanupCurrent(working, current, pool,
                    victimReference, captureDate!, captureEffects!);
            }
            publishReached?.Invoke(working);
            foreach (var area in current.ReadRegisteredAreaReferences() ?? throw Missing("managerB0"))
                if (area is not null && current.ReadAreaCurrentForceSlot(area) == victim)
                    throw Missing("branch.293BD0_remaining_Force_Area");
            ClearTargetAssociations(current, victim);
            if (capture)
            {
                working = package.ApplyOrdinaryLastCityRemainingPeopleCurrent(working, current, pool,
                    victimReference, new(captor), captureDate!, captureEffects!);
                //Status6 keeps its captivity and position but now belongs to
                //District0. Other nonserving statuses can retain rawE0 until
                //the District reset, exactly as in the absorption sibling.
                RequireEmptyPeopleAndArmies(working, current, victim, true);
            }
            effects.Add("293BD0.empty_Force_Army_and_Area→278090_current_associations→empty_Force_Person");
            var districtTemporary = Temporary(current);
            var districts = Chain(current, RegistryKind.District).Where(d =>
                current.ReadDistrictForceId(d) == victim).ToArray();
            foreach (int district in districts) Append(current, districtTemporary, RegistryKind.District, district);
            int removedDistrictNodes = 0; var governors = new List<ArmyReturnGovernorPacket>();
            foreach (int district in districts)
            {
                ushort leaderId = Need(current.ReadDistrictLeader(district), "District12");
                int leader = (leaderId <= 6000 ? current.ReadPersonLookupReference(leaderId) : current.ReadTransferPersonTableReference(0))
                    ?.PhysicalSlot ?? throw Missing("District_leader_lookup");
                byte status = Need(current.ReadPersonStatus(leader), "District_leaderE6");
                if (status is >= 1 and <= 4 && status != 1 && (!absorption || status != 3))
                    current.WritePersonStatus(leader, 4);
                //293490's third argument1 preserves a current Governor and
                //skips the former leader Home governor refresh altogether.
                if (!absorption)
                {
                    var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(leader), "District_leaderE2"), current);
                    if (Known(current.ReadBaseNativeValidity(home), "District_leader_current_Home_validity"))
                    {
                        var packet = package.ApplyPersonFreedomGovernorCurrent(working, current, home);
                        governors.Add(packet); working = PublishGovernor(working, current, packet);
                    }
                }
                if ((current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("manager100")).Any(p =>
                    p is int person && current.ReadPersonDistrictSlot(person) == district &&
                    (!(absorption || capture) || HasServingOrCaptiveStatus(current, person))))
                    throw Missing("branch.293490_nonempty_District_Person");
                if ((current.ReadRegisteredBases() ?? throw Missing("manager80")).Any(b =>
                    b is not null && current.ReadBaseDistrictSlot(b) == district))
                    throw Missing("branch.293490_nonempty_District_Base");
                if (absorption || capture) working = ArmyCautionDistrictResetSource.ResetForDistrict(working, current, district);
                else RequireNoTroopsDistrict(working, current, district);
                //District0 is native-invalid, so the treasury24 transfer
                //condition short-circuits before its old numeric stock read.
                ClearRouting(current, 8, district);
                removedDistrictNodes += RemoveAll(current, RegistryKind.District, district);
                current.ResetDistrict(district);
            }
            effects.Add(absorption
                ? "227FC0.live_D0→293490_mode1_preserve_Governor→actual_caution_reset→32B690→226DC0"
                : "227FC0.live_D0→District_pointer_temporary→293490.Governor→empty_Person_Base_Troops→32B690→226DC0");
            ClearRouting(current, 7, victim);
            int forceNodesRemoved = RemoveAll(current, RegistryKind.Force, victim);
            var resets = new Dictionary<int, byte?[]>(saved.ForceResetImages) { [victim] = ForceResetImage() };
            working = ResetForce(working, current, victim, resets);
            if (absorption || capture) MirrorAbsorptionRelations(working, current, victim);
            var windows = saved.DistrictWindows10.ToDictionary(x => x.Key,
                x => Enumerable.Range(0x10, 28).Select(o => current.ReadDistrictRawByte(x.Key, o)).ToArray());
            working = (working with { NativeForceUnregisterCurrent = working.NativeForceUnregisterCurrent! with { DistrictWindows10 = windows },
                Districts = absorption || capture ? working.Districts.Where(row => !districts.Contains(row.Key))
                    .ToDictionary(row => row.Key, row => row.Value) : working.Districts
            }).WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
            effects.Add("32B710→3DFB50.all_equal_Force_nodes→2261B0.literal_numeric_reset_and_bidirectional_relations");
            //The old finite leaf admits only an empty queue. Absorption owns
            //the real borrowed lifecycle pass, including reached retirements.
            if (!absorption && !capture && ((current.ReadArmyReturnPending70Descriptor() is not { } pending) ||
                current.ReadRegistryHead(pending)?.Identity is not null)) throw Missing("branch.28E_nonempty_pending_after_Freedom_window");
            working = (capture && resetArmyReceiver is not null
                ? OwnedArmyLifecycleAuxiliaries.ConsumePendingCurrent(working, current, pool, resetArmyReceiver)
                : borrowedCurrent is null ? package.ConsumeArmyLifecyclePending(working)
                : package.ConsumeArmyLifecyclePendingCurrent(working, current, pool))
                ?? throw Missing(absorption ? "28E_absorption_current_window" : "28E_current_empty_window");
            //Project only permanent domain roles while this caller still owns its
            //District/Object temporary lists;28E does not own those lists.
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() });
            effects.Add(absorption ? "28E6D0.actual_current_pending70_window" : "28E6D0.actual_empty_pending70_window");
            var ranked = RankCurrent(working, current, pool);
            working = ranked.State; effects.Add("2924B0.actual_two_pointer_temporaries_and_shared_Int32_temporary");
            current.ClearTemporaryHexList(districtTemporary); current.ReleaseTemporaryHexList(districtTemporary);
            current.ClearTemporaryHexList(objectTemporary); current.ReleaseTemporaryHexList(objectTemporary);
            working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
            working = working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
                { Indexes = borrowedCurrent is null ? pool.ExportOwnedState() : pool.ProjectOwnedRoles() });
            if (!absorption && !capture) NativeForceUnregisterCurrentStateValidation.Validate(working);
            return new(working, new(victim, captor, remaining.Length, playerCount, districts, changedHexes,
                forceNodesRemoved, removedDistrictNodes, governors.ToArray(), ranked.Forces, effects.AsReadOnly()), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private static int[] Chain(SourceWorldContext current, RegistryKind kind) =>
        (current.ReadRegistryReferences(kind) ?? throw Missing("registry_" + kind)).Select(x =>
            x?.PhysicalSlot ?? throw Missing("registry_payload_" + kind)).ToArray();
    private static HexListHandle Temporary(SourceWorldContext current)
    {
        var handle = current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
        if (handle.Identity is null) throw Missing("branch.temporary_allocation_failure");
        return handle;
    }
    private static void Append(SourceWorldContext current, HexListHandle handle, RegistryKind kind, int physical)
    {
        var node = current.AllocateRegistryNode(new(handle.Identity));
        if (node?.Identity is null) throw Missing("branch.temporary_node_allocation_failure");
        current.WriteRegistryNodePayload(node, new(kind, physical));
    }
    private static int RemoveAll(SourceWorldContext current, RegistryKind kind, int physical)
    {
        var descriptor = current.ReadRegistryDescriptor(kind) ?? throw Missing("remove_descriptor");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("remove_head"); int removed = 0;
        while (node.Identity is not null)
        {
            var next = current.ReadRegistryNodeNext(node) ?? throw Missing("remove_next");
            if (current.ReadRegistryNodePayload(node) == new RegistryObjectReference(kind, physical))
            { current.UnlinkArmyReturnTraitNode(descriptor, node); removed++; }
            node = next;
        }
        return removed;
    }
    private static void ClearRouting(SourceWorldContext current, byte kind, int target)
    {
        foreach (int district in Chain(current, RegistryKind.District))
            if (Need(current.ReadDistrictRawByte(district, 0x19), "District19") == kind &&
                Need(current.ReadDistrictRawByte(district, 0x18), "District18") == target)
            {
                current.SetDistrictRawByte(district, 0x17, 1); current.SetDistrictRawByte(district, 0x18, 0);
                current.SetDistrictRawByte(district, 0x19, 0xFF); current.SetDistrictRawByte(district, 0x1A, 0);
                current.SetDistrictRawByte(district, 0x1B, 0xFF);
                for (int i = 0; i < 4; i++) current.SetDistrictRawByte(district, 0x1C + i, i == 3 ? (byte)0x7F : (byte)0xFF);
                for (int i = 0; i < 3; i++) current.SetDistrictRawByte(district, 0x20 + i, 0);
            }
    }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static bool Known(bool? value, string field) => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.ForceUnregister_" + field);
}

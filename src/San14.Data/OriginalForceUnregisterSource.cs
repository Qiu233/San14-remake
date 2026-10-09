using San14.Core;

namespace San14.Data;

public sealed record OriginalForceUnregisterReceipt(int ForcePhysical, int CapturingForcePhysical,
    int RemainingOrdinaryForces, int RemainingPlayerForces, int[] Districts,
    int[] ChangedHexes, int ForceNodesRemoved, int DistrictNodesRemoved,
    OriginalArmyReturnGovernorPacket[] GovernorUpdates, int[] RankedForces,
    IReadOnlyList<string> SourceEffectOrder);
public sealed record OriginalForceUnregisterResult(GameState? State,
    OriginalForceUnregisterReceipt? Receipt, string? UnresolvedOwner);

/// <summary>Ordinary non-player293BD0 numeric leaf after real265250 has
/// removed every current Force Person/Army. Empty Gate/Object destruction
/// branches are proved from current tables; residual Hex ownership is executed.
/// Original presentation/AI cache receivers are not constructed by this client.</summary>
public sealed partial class OriginalForceUnregisterSource(OriginalOwnedArmyCampaignRules package)
{
    public const string Revision = "ce-293BD0-ordinary-empty-person-army-unregister";

    public OriginalForceUnregisterResult TryApplyCurrent(GameState input, int victim, int captor)
    {
        try
        {
            NativeForceUnregisterCurrentStateValidation.Validate(input);
            var saved = input.NativeForceUnregisterCurrent ?? throw Missing("saved_numeric_owner");
            var world = package.CaptureOwnedArmyReturnWorld(input); var current = world.Context; var pool = world.Pool;
            OriginalForceUnregisterInputs.BindCurrent(input, current);
            var victimReference = new OriginalForceReference(victim);
            if (victim is < 1 or > 45 || captor is < 1 or > 45 || victim == captor ||
                !Known(current.ReadForceNativeValidity(victimReference), "victim_validity") ||
                !Known(current.ReadForceNativeValidity(new(captor)), "captor_validity")) throw Missing("branch.ordinary_valid_forces");
            int player = current.ReadCurrentPlayerForce()?.PhysicalSlot ?? throw Missing("World3A_player");
            if (victim == player) throw Missing("branch.player_terminal_service");
            var remaining = Enumerable.Range(0, 52).Where(i => i != victim && i is >= 1 and <= 45 &&
                Known(current.ReadForceNativeValidity(new(i)), "terminal_other_Force_validity")).ToArray();
            int playerCount = remaining.Count(i => i == player && player != 0);
            if (remaining.Length == 0 || playerCount != 1) throw Missing("branch.27A6B0_terminal_service");
            if (input.NativeArmySimulation!.World.SelectedForce1656 is not byte selected || saved.OtherForce1657 is not byte other)
                throw Missing("World1656_1657_current_interaction");
            if (selected == victim || other == victim) throw Missing("branch.277590_274DD0_current_interaction");
            ushort ruler = Need(current.ReadForceRecord(victim)?.RawRuler10, "victim_ruler10");
            if (ruler is 521 or 35) throw Missing("branch.279EA0_achievement_pair");
            RequireEmptyPeopleAndArmies(input, current, victim);
            if (input.Structures is not { Count: 0 }) throw Missing("branch.current_dynamic_Object_table");
            foreach (var entry in current.ReadRegisteredBases() ?? throw Missing("manager80"))
                if (entry is not null && current.ReadBaseForceSlot(entry) == victim)
                    throw Missing("branch.293BD0_remaining_Base_or_Gate");
            for (int slot = 0; slot < 3001; slot++)
            {
                var obj = current.ReadObjectTableReference(slot) ?? throw Missing("Object_table");
                if (Known(current.ReadObjectNativeValidity(obj), "Object_validity") &&
                    Need(current.ReadObjectForceSlot13(obj), "Object13") == victim)
                    throw Missing("branch.294FD0_owned_Object_destroy");
            }
            var effects = new List<string> { "27A6B0.remaining_ordinary_and_actual_player_nonterminal",
                "277590_274DD0_279EA0.no_reached_numeric_writer", "293BD0.Gate_Object_destruction_iterators_empty" };
            int originalFlag = input.NativeArmySimulation.Parameters.Int32Values.GetValueOrDefault(
                OriginalBattleRemakeCurrentForcePolicies.ForceFieldKey(victim, 0x15E), -1);
            if (originalFlag < 0 || originalFlag > 255) throw Missing("Force15E");
            var values = new Dictionary<string, int>(input.NativeArmySimulation.Parameters.Int32Values)
                { [OriginalBattleRemakeCurrentForcePolicies.ForceFieldKey(victim, 0x15E)] = originalFlag & 0xFE };
            var working = input with { NativeArmySimulation = input.NativeArmySimulation with
                { Parameters = input.NativeArmySimulation.Parameters with { Int32Values = values } } };
            var objectTemporary = Temporary(current);
            var changedHexes = SettleHexes(working, current, victim, objectTemporary);
            effects.Add("293Fxx.Object_temporary→48400_current_Hex_force14_writers→empty_Object13_clear");
            working = working with { Territory = working.Territory.ToDictionary(x => x.Key,
                x => x.Value with { ForceId = current.ReadHexForceSlot(new(x.Key)) is byte f ? f == 0 ? null : f : throw Missing("Hex14_publish") }) };
            foreach (var area in current.ReadRegisteredAreaReferences() ?? throw Missing("managerB0"))
                if (area is not null && current.ReadAreaCurrentForceSlot(area) == victim)
                    throw Missing("branch.293BD0_remaining_Force_Area");
            ClearTargetAssociations(current, victim);
            effects.Add("293BD0.empty_Force_Army_and_Area→278090_current_associations→empty_Force_Person");
            var districtTemporary = Temporary(current);
            var districts = Chain(current, OriginalRegistryKind.District).Where(d =>
                current.ReadDistrictRawByte(d, 0x10) == victim).ToArray();
            foreach (int district in districts) Append(current, districtTemporary, OriginalRegistryKind.District, district);
            int removedDistrictNodes = 0; var governors = new List<OriginalArmyReturnGovernorPacket>();
            foreach (int district in districts)
            {
                ushort leaderId = Need(current.ReadDistrictLeader(district), "District12");
                int leader = (leaderId <= 6000 ? current.ReadPersonLookupReference(leaderId) : current.ReadTransferPersonTableReference(0))
                    ?.PhysicalSlot ?? throw Missing("District_leader_lookup");
                byte status = Need(current.ReadPersonStatus(leader), "District_leaderE6");
                if (status is >= 1 and <= 4 && status != 1) current.WritePersonStatus(leader, 4);
                var home = OriginalBaseLookup.Find(Need(current.ReadPersonFirstLocation(leader), "District_leaderE2"), current);
                if (Known(current.ReadBaseNativeValidity(home), "District_leader_current_Home_validity"))
                {
                    var packet = package.ApplyPersonFreedomGovernorCurrent(working, current, home);
                    governors.Add(packet); working = PublishGovernor(working, current, packet);
                }
                if ((current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("manager100")).Any(p =>
                    p is int person && current.ReadPersonDistrictSlot(person) == district))
                    throw Missing("branch.293490_nonempty_District_Person");
                if ((current.ReadRegisteredBases() ?? throw Missing("manager80")).Any(b =>
                    b is not null && current.ReadBaseDistrictSlot(b) == district))
                    throw Missing("branch.293490_nonempty_District_Base");
                RequireNoTroopsDistrict(working, current, district);
                //District0 is native-invalid, so the treasury24 transfer
                //condition short-circuits before its old numeric stock read.
                ClearRouting(current, 8, district);
                removedDistrictNodes += RemoveAll(current, OriginalRegistryKind.District, district);
                current.ResetDistrict(district);
            }
            effects.Add("227FC0.live_D0→District_pointer_temporary→293490.Governor→empty_Person_Base_Troops→32B690→226DC0");
            ClearRouting(current, 7, victim);
            int forceNodesRemoved = RemoveAll(current, OriginalRegistryKind.Force, victim);
            var resets = new Dictionary<int, byte?[]>(saved.ForceResetImages) { [victim] = ForceResetImage() };
            working = ResetForce(working, current, victim, resets);
            var windows = saved.DistrictWindows10.ToDictionary(x => x.Key,
                x => Enumerable.Range(0x10, 28).Select(o => current.ReadDistrictRawByte(x.Key, o)).ToArray());
            working = working with { NativeForceUnregisterCurrent = working.NativeForceUnregisterCurrent! with { DistrictWindows10 = windows },
                NativeArmyLifecycle = working.NativeArmyLifecycle! with { Indexes = pool.ExportOwnedState() } };
            effects.Add("32B710→3DFB50.all_equal_Force_nodes→2261B0.literal_numeric_reset_and_bidirectional_relations");
            //This finite caller admits no surviving pending header. It still
            //executes the normal28E window using the saved actual empty queue.
            if ((current.ReadArmyReturnPending70Descriptor() is not { } pending) ||
                current.ReadRegistryHead(pending)?.Identity is not null) throw Missing("branch.28E_nonempty_pending_after_Freedom_window");
            working = package.ConsumeArmyLifecyclePending(working) ?? throw Missing("28E_current_empty_window");
            //The current operation's primary pool includes still-live outer
            //temporaries; the empty28E window has no numeric/pool writer.
            working = working with { NativeArmyLifecycle = working.NativeArmyLifecycle! with { Indexes = pool.ExportOwnedState() } };
            effects.Add("28E6D0.actual_empty_pending70_window");
            var ranked = RankCurrent(working, current, pool);
            working = ranked.State; effects.Add("2924B0.actual_two_pointer_temporaries_and_shared_Int32_temporary");
            current.ClearTemporaryHexList(districtTemporary); current.ReleaseTemporaryHexList(districtTemporary);
            current.ClearTemporaryHexList(objectTemporary); current.ReleaseTemporaryHexList(objectTemporary);
            working = OriginalPersonAssignmentPrecallInputs.PublishCurrent(working, current);
            working = working with { NativeArmyLifecycle = working.NativeArmyLifecycle! with { Indexes = pool.ExportOwnedState() } };
            NativeForceUnregisterCurrentStateValidation.Validate(working);
            return new(working, new(victim, captor, remaining.Length, playerCount, districts, changedHexes,
                forceNodesRemoved, removedDistrictNodes, governors.ToArray(), ranked.Forces, effects.AsReadOnly()), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private static int[] Chain(OriginalEventInitializationContext current, OriginalRegistryKind kind) =>
        (current.ReadRegistryReferences(kind) ?? throw Missing("registry_" + kind)).Select(x =>
            x?.PhysicalSlot ?? throw Missing("registry_payload_" + kind)).ToArray();
    private static OriginalHexListHandle Temporary(OriginalEventInitializationContext current)
    {
        var handle = current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
        if (handle.Identity is null) throw Missing("branch.temporary_allocation_failure");
        return handle;
    }
    private static void Append(OriginalEventInitializationContext current, OriginalHexListHandle handle, OriginalRegistryKind kind, int physical)
    {
        var node = current.AllocateRegistryNode(new(handle.Identity));
        if (node?.Identity is null) throw Missing("branch.temporary_node_allocation_failure");
        current.WriteRegistryNodePayload(node, new(kind, physical));
    }
    private static int RemoveAll(OriginalEventInitializationContext current, OriginalRegistryKind kind, int physical)
    {
        var descriptor = current.ReadRegistryDescriptor(kind) ?? throw Missing("remove_descriptor");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("remove_head"); int removed = 0;
        while (node.Identity is not null)
        {
            var next = current.ReadRegistryNodeNext(node) ?? throw Missing("remove_next");
            if (current.ReadRegistryNodePayload(node) == new OriginalRegistryObjectReference(kind, physical))
            { current.UnlinkArmyReturnTraitNode(descriptor, node); removed++; }
            node = next;
        }
        return removed;
    }
    private static void ClearRouting(OriginalEventInitializationContext current, byte kind, int target)
    {
        foreach (int district in Chain(current, OriginalRegistryKind.District))
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

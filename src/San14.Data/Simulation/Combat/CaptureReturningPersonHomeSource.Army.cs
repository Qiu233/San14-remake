using San14.Core;

namespace San14.Data;

public sealed record CapturedArmyPrisonerHomeWrite(int PersonPhysical, ushort HomeE2);
public sealed record CapturedArmyHomeWrites(int UnitId, int ArmyPhysical, ushort CommanderHomeE2,
    int[] PrisonerPersonOrder, CapturedArmyPrisonerHomeWrite[] PrisonerHomeWrites);

public sealed partial class CaptureReturningPersonHomeSource : IPlayableCaptureArmyHomeRules
{
    public int[]? ReadCapturedArmyHomeOrder(GameState state, int capturedLocationId, int previousForceId)
    {
        var arena = state.NativeArmyLifecycle;
        if (arena?.Indexes.Registries.GetValueOrDefault(WorldEntityKind.Officer) is not { } order) return null;
        var targets = state.Units.Values.Where(x => x.ForceId == previousForceId && x.NativeArmyOrigin is not null &&
            x.Mission?.Kind != UnitMissionKind.Transport && state.Officers[x.OfficerId].LocationId == capturedLocationId).ToArray();
        if (targets.Any(x => state.Officers[x.OfficerId].NativeAbilityRuntime is null)) return null;
        var armies = targets.ToDictionary(x => x.OfficerId);
        var result = new List<int>();
        //2851B0's234920/234D80 uses the actual manager100 Person chain.
        //This is the active-Commander intersection of that source order,
        //not execution of the full2851B0 personnel loop. Unrelated Person
        //E2/status owners cannot affect these already identified actors.
        foreach (var entity in order)
        {
            if (entity.Kind != WorldEntityKind.Officer || entity.Id is not int id) return null;
            if (!armies.TryGetValue(id, out var army)) continue;
            var person = state.Officers[army.OfficerId];
            if (person.LocationId != capturedLocationId) continue;
            if (person.NativeAbilityRuntime!.Status11E is not byte status) return null;
            if (status is < 1 or > 4) continue;
            if (person.DistrictId is not int district || !state.Districts.TryGetValue(district, out var districtState)) return null;
            if (districtState.ForceId != previousForceId) continue;
            if (person.NativeLocationRuntime?.Second11C is not ushort second) return null;
            var position = PersonPositionRepair.DecodeSecondLocation(second);
            if (position.Kind != 27 || position.Index is < 1 or > 500 || position.Index >= arena.Slots.Length ||
                arena.Slots[position.Index].Commander12 != person.Id || arena.Slots[position.Index].Status10 is null or 0 ||
                arena.Slots[position.Index].ActiveUnitId != army.Id || army.NativeArmyOrigin!.NativeSlotIndex != position.Index)
                return null;
            result.Add(army.Id);
        }
        return result.ToArray();
    }

    public PlayableCaptureReturningPersonResolution? ChangeCapturedArmyHome(GameState state,
        int capturedLocationId, int previousForceId, UnitState army, CityState beforeCaptureCity)
    {
        var result = TryApplyArmy(state, capturedLocationId, previousForceId, army.Id, beforeCaptureCity);
        return result.State is { } next ? new(next, result.Receipt!.NewHome, Revision) : null;
    }

    public ReturningPersonHomeResult TryApplyArmy(GameState state, int capturedLocationId,
        int previousForceId, int unitId, CityState beforeCaptureCity) =>
        state.Units.TryGetValue(unitId, out var current)
            ? ApplyWithPreviousOwner(state, capturedLocationId, previousForceId, current.OfficerId, beforeCaptureCity, current)
            : new(null, null, "current.capture_active_Army");

    //260310347..400.230C70 is the registered status6 prisoner chain,
    //not a roster of ordinary Army commanders or subcommanders.
    private static CapturedArmyHomeWrites ApplyDeployedArmyPrisonerHomes(GameState state,
        SourceWorldContext current, int commanderPhysical, UnitState activeArmy)
    {
        var position = PersonPositionRepair.DecodeSecondLocation(
            Need(current.ReadPersonSecondLocation(commanderPhysical), "current.22A200_PersonE4"));
        var receiver = current.ReadArmyUnitTableReference(position.Kind == 27 && position.Index <= 500 ? position.Index : 0)
            ?? throw Missing("current.22A200_Army_pointer");
        if (Need(current.ReadArmyUnitCommanderId(receiver), "current.22A200_Army12") !=
            Need(current.ReadPersonIdentity(commanderPhysical), "current.22A200_Person10"))
            receiver = current.ReadArmyUnitTableReference(0) ?? throw Missing("current.22A200_Army0");
        if (!Need(current.ReadArmyUnitNativeValidity(receiver), "current.22A200_Army_validity") ||
            receiver.PhysicalSlot is not (>= 1 and <= 500) || activeArmy.NativeArmyOrigin?.NativeSlotIndex != receiver.PhysicalSlot ||
            state.NativeArmyLifecycle?.Slots[receiver.PhysicalSlot.Value].ActiveUnitId != activeArmy.Id)
            throw Missing("branch.260310_not_effective_Commander_Army");
        int army = receiver.PhysicalSlot.Value;
        var members = NewTemporary(current);
        var output = new List<int>(); var writes = new List<CapturedArmyPrisonerHomeWrite>();
        try
        {
            current.ClearTemporaryHexList(members); //230C70's669E60.
            var chain = current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("current.230C70_manager100");
            foreach (int? item in chain)
            {
                int person = Need(item, "current.230C70_Person_payload");
                if (Need(current.ReadPersonStatus(person), "current.230C70_PersonE6") != 6) continue;
                if (Need(current.ReadPersonSecondLocation(person), "current.230C70_PersonE4") != army + 61) continue;
                _ = current.AppendPersonReturnTemporary(members, person); //018CC0 failure drops only this node.
            }
            var node = current.ReadHexListHead(members) ?? throw Missing("current.230C70_temporary_head");
            while (node.Identity.HasValue)
            {
                int prisoner = Need(current.ReadPersonRelationListPayload(node)?.PhysicalSlot, "current.230C70_temporary_Person");
                //The getter is called again for each actual output node, after
                //the commander E2 store and both real District callbacks.
                ushort home = ReadCommanderHome22A7A0(current, receiver);
                ushort value = (ushort)Math.Clamp((int)home, 0, 61);
                current.WritePersonFirstLocation(prisoner, value);
                output.Add(prisoner); writes.Add(new(prisoner, value));
                node = current.ReadHexListNodeNext(node) ?? throw Missing("current.230C70_temporary_next");
            }
        }
        finally { ClearRelease(current, members); }
        return new(activeArmy.Id, army, ReadCommanderHome22A7A0(current, receiver), output.ToArray(), writes.ToArray());
    }

    private static ushort ReadCommanderHome22A7A0(SourceWorldContext current, ArmyUnitReference receiver)
    {
        ushort commander = Need(current.ReadArmyUnitCommanderId(receiver), "current.22A7A0_Army12");
        var person = commander <= 6000 ? current.ReadPersonLookupReference(commander) : current.ReadTransferPersonTableReference(0);
        int physical = Need(person?.PhysicalSlot, "current.22A7A0_Commander_pointer");
        return Need(current.ReadPersonNativeValidity(physical), "current.22A7A0_Commander_validity")
            ? Need(current.ReadPersonFirstLocation(physical), "current.22A7A0_CommanderE2") : (ushort)0;
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public CaptureReturningPersonHomeSource CreateCaptureArmyHomeRules() => new(this, _governorInputs);
}

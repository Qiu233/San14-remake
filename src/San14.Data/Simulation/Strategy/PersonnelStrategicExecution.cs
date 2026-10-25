using San14.Core;

namespace San14.Data;

internal sealed record PersonnelStrategicAdvanceResult(GameState State, bool Retire);
internal interface IPersonnelStrategicExecution
{
    PersonnelStrategicAdvanceResult AdvanceStrategicCurrent(GameState state, SourceWorldContext current,
        PersonnelStrategicTask task, NativeMapSeasonDate date);
    PersonnelStrategicAdvanceResult AdvanceIndependenceCurrent(GameState state, SourceWorldContext current,
        PersonnelIndependenceTask task, NativeMapSeasonDate date);
}

public static partial class PersonReturnCommands
{
    //1F2160 is the cancellation virtual90. Ordinary successful completion
    //calls2247F0 directly and does not refund its military reservation.
    internal static GameState RefundStrategicMilitaryCurrent(GameState state,
        SourceWorldContext current, PersonnelStrategicTask task)
    {
        if (task.MilitaryIntelligence <= 0 || task.MemberIds.Length == 0) return state;
        int person = current.ResolveOfficerSource(task.MemberIds[0]) ?? throw Missing("strategic refund member");
        var force = current.ReadPersonForce(person) ?? throw Missing("strategic refund Force");
        if (!Get(current.ReadForceNativeValidity(force), "strategic refund Force validity")) return state;
        int raw = Get(current.ReadPersonDistrictSlot(person), "strategic refund E0");
        var district = current.ReadDistrictTableReference(raw < 52 ? raw : 0) ?? throw Missing("strategic refund District");
        if (!Get(current.ReadDistrictNativeValidity(district), "strategic refund District validity"))
            district = ForcePrimaryDistrictSource.Resolve(current, force);
        if (!Get(current.ReadDistrictNativeValidity(district), "strategic refund primary validity")) return state;
        int id = Get(district.PhysicalSlot, "strategic refund District identity");
        int stock = ArmyCampaignRuleAdapter.ReadDistrictAdministrationIntelligenceCurrent(current, id);
        int after = Math.Clamp(unchecked(stock + task.MilitaryIntelligence), 0,
            ArmyCampaignRuleAdapter.DistrictAdministrationIntelligenceCapacity(state, current, id));
        for (int index = 0; index < 4; index++)
            current.SetDistrictRawByte(id, 0x24 + index, unchecked((byte)(after >> (index * 8))));
        var saved = state.NativeForceUnregisterCurrent ?? throw Missing("strategic refund saved District");
        return state with { NativeForceUnregisterCurrent = saved with
            { DistrictWindows10 = new(saved.DistrictWindows10)
                { [id] = Enumerable.Range(0x10, 28).Select(at => current.ReadDistrictRawByte(id, at)).ToArray() } } };
    }
    /// <summary>1DE290 registers an empty receiver before1F78A0 marks and
    /// appends its member. An earlier association retains its actual order.</summary>
    internal static GameState RegisterStrategicCurrent(GameState state, SourceWorldContext current,
        PersonnelStrategicTask prepared, int executorPhysical, NativeMapSeasonDate date)
    {
        var before = CurrentCommandOwner(state, current);
        if (before.Strategies is null) throw Missing("strategic task owner");
        if (before.AvailableDispatchSlots.Length == 0) throw Missing("strategic dispatch allocation");
        int id = before.NextTaskId, slot = before.AvailableDispatchSlots[0];
        var task = prepared with { Id = id, DispatchSlot = slot, MemberIds = [] };
        var owner = before with { NextTaskId = checked(id + 1),
            AvailableDispatchSlots = before.AvailableDispatchSlots[1..],
            Strategies = new(before.Strategies) { [id] = task } };
        var handle = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("strategic association index");
        if (!current.AppendPersonReturnAssociated(new(handle.Identity), id))
            throw Missing("strategic association registration");
        current.SetOwnedPersonCommandManager(owner);
        current.SetLinkedEntityRawState58(id, slot);
        current.SetLinkedEntityMemberIds(id, []);
        current.SetAssociatedPayloadPostLoadCallback(id, PostLoadCallback.NoOp);
        ushort flags = Get(current.ReadArmyReturnPersonFlags196(executorPhysical), "strategic executor15E");
        current.WriteArmyReturnPersonFlags196(executorPhysical, (ushort)(flags | 1));
        var position = ResolvePersonPosition(state, current, executorPhysical, date);
        ushort area = Get(current.ReadHexAreaSlot(position), "strategic executor current Area");
        current.WritePersonSecondLocation(executorPhysical,
            area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
        task = task with { MemberIds = prepared.MemberIds.ToArray() };
        owner = CurrentCommandOwner(state, current);
        owner = owner with { Strategies = new(owner.Strategies!) { [id] = task } };
        current.SetOwnedPersonCommandManager(owner);
        current.SetLinkedEntityMemberIds(id, task.MemberIds.Select(member => (uint?)Get(
            current.ReadPersonIdentity(current.ResolveOfficerSource(member) ?? throw Missing("strategic member source")),
            "strategic member identity")).ToArray());
        return state with { PersonnelCommands = owner };
    }
}

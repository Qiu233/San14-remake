using San14.Core;

namespace San14.Data;

internal sealed record PersonnelIntelligenceAdvanceResult(GameState State, bool Retire);
internal interface IPersonnelIntelligenceExecution
{
    PersonnelIntelligenceAdvanceResult AdvanceIntelligenceCurrent(GameState state, SourceWorldContext current,
        PersonnelIntelligenceTask task, NativeMapSeasonDate date);
}

public static partial class PersonReturnCommands
{
    //Registration precedes member initialization, so current position still
    //resolves through the member's previous associated command.
    internal static GameState RegisterIntelligenceCurrent(GameState state, SourceWorldContext current,
        PersonnelIntelligenceTask prepared, int executorPhysical, NativeMapSeasonDate sourceDate)
    {
        var before = CurrentCommandOwner(state, current);
        if (before.Intelligences is null) throw Missing("intelligence task owner");
        if (before.AvailableDispatchSlots.Length == 0) throw Missing("intelligence dispatch allocation");
        int id = before.NextTaskId, slot = before.AvailableDispatchSlots[0];
        var task = prepared with { Id = id, DispatchSlot = slot, MemberIds = [] };
        var owner = before with { NextTaskId = checked(id + 1),
            AvailableDispatchSlots = before.AvailableDispatchSlots[1..],
            Intelligences = new(before.Intelligences) { [id] = task } };
        var handle = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("intelligence association index");
        if (!current.AppendPersonReturnAssociated(new(handle.Identity), id)) throw Missing("intelligence registration");
        current.SetOwnedPersonCommandManager(owner);
        current.SetLinkedEntityRawState58(id, slot);
        current.SetLinkedEntityMemberIds(id, []);
        current.SetAssociatedPayloadPostLoadCallback(id, PostLoadCallback.NoOp);
        ushort flags = Get(current.ReadArmyReturnPersonFlags196(executorPhysical), "intelligence executor15E");
        current.WriteArmyReturnPersonFlags196(executorPhysical, (ushort)(flags | 1));
        var position = ResolvePersonPosition(state, current, executorPhysical, sourceDate);
        ushort area = Get(current.ReadHexAreaSlot(position), "intelligence executor current Area");
        current.WritePersonSecondLocation(executorPhysical, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
        task = task with { MemberIds = prepared.MemberIds.ToArray() };
        owner = CurrentCommandOwner(state, current);
        owner = owner with { Intelligences = new(owner.Intelligences!) { [id] = task } };
        current.SetOwnedPersonCommandManager(owner);
        current.SetLinkedEntityMemberIds(id, task.MemberIds.Select(member => (uint?)Get(
            current.ReadPersonIdentity(current.ResolveOfficerSource(member) ?? throw Missing("intelligence member source")),
            "intelligence member identity")).ToArray());
        return state with { PersonnelCommands = owner };
    }
}

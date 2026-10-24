using San14.Core;

namespace San14.Data;

public static partial class PersonReturnCommands
{
    //1E91F0 compares the actual Force pointers, including table aliases. Its
    //caller runs this at8, and again at22 only once the journey is due.
    private static bool MovementDestinationLost(SourceWorldContext current, PersonnelReturnTask task)
    {
        if (task.MemberIds.Length == 0 || task.Destination is null) return true;
        int id = task.MemberIds[0];
        int first = Get((id < 6001 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
            "movement first member");
        _ = current.ReadPersonForce(first) ?? throw Missing("movement initial member Force");
        var target = ToBase(task.Destination);
        bool valid = Get(current.ReadBaseNativeValidity(target), "movement target validity");
        bool lost = !valid;
        if (valid)
        {
            int raw = Get(current.ReadBaseForceSlot(target), "movement target Force");
            var targetForce = current.ReadForceTableReference(raw < 52 ? raw : 0)
                ?? throw Missing("movement target Force pointer");
            var force = current.ReadPersonForce(first) ?? throw Missing("movement member Force");
            lost = Get(current.ReadForcePhysicalSlot(targetForce), "movement target Force identity") == 0 || targetForce != force;
        }
        foreach (int member in task.MemberIds)
        {
            int p = Get((member < 6001 ? current.ReadPersonLookupReference(member) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
                "movement cancellation member");
            if (Get(current.ReadPersonStatus(p), "movement cancellation member status") == 1 && valid)
                _ = current.ReadBaseDistrictReference(target) ?? throw Missing("movement target District");
        }
        return task.CancellationRequested || lost;
    }

    /// <summary>The serving-person1F1360/1F6590 branch. The enclosing
    /// command owns the working current and publishes the complete batch.</summary>
    internal static PersonReturnCommandResult StartReassignment(GameState state, SourceWorldContext current,
        IReadOnlyList<int> members, int destinationId)
    {
        try
        {
            if (members.Count == 0) throw Missing("movement members");
            var before = CurrentCommandOwner(state, current);
            if (before.AvailableDispatchSlots.Length == 0) throw Missing("movement dispatch allocation");
            var destination = BaseLookup.Find(destinationId, current);
            if (!Get(current.ReadBaseNativeValidity(destination), "movement target validity")) throw Missing("movement invalid target");
            int first = Pointer(members[0]);
            ushort homeId = Get(current.ReadPersonFirstLocation(first), "movement first Home");
            var home = BaseLookup.Find(homeId, current);
            if (!Get(current.ReadBaseNativeValidity(home), "movement Home validity")) throw Missing("movement invalid Home");
            foreach (int id in members)
            {
                int p = Pointer(id);
                if (!Get(current.ReadPersonNativeValidity(p), "movement Person validity")) throw Missing("movement invalid Person");
                var associated = current.ReadPersonLinkedEntity(p) ?? throw Missing("movement association");
                if (Get(current.ReadLinkedEntityNativeValidity(associated), "movement associated validity"))
                {
                    int receiver = Get(associated.Identity, "movement associated identity");
                    if (before.Returns.ContainsKey(receiver)) throw Missing("movement Person already owns Move or Return");
                    if (before.Disappearances?.ContainsKey(receiver) != true) throw Missing("movement associated command kind");
                }
                if (Get(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, p), "movement Army validity"))
                    throw Missing("movement active Army precondition1F0010");
            }
            var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("movement date");
            int taskId = before.NextTaskId, slot = before.AvailableDispatchSlots[0];
            var target = ToDestination(destination);
            byte district = unchecked((byte)Get(current.ReadBaseDistrictSlot(home), "movement origin District"));
            int startHex = current.ReadBaseCurrentHexIndex(home);
            var task = new PersonnelReturnTask(taskId, slot, [], [target], target, district, startHex,
                false, false, true, false, true, date, new(0, 1, 1, 0)) { IsReassignment = true };
            var owner = before with { NextTaskId = checked(taskId + 1), AvailableDispatchSlots = before.AvailableDispatchSlots[1..],
                Returns = new(before.Returns) { [taskId] = task } };
            var handle = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("movement association index");
            if (!current.AppendPersonReturnAssociated(new(handle.Identity), taskId)) throw Missing("movement association registration");
            current.SetOwnedPersonCommandManager(owner);
            foreach (int id in members)
            {
                int p = Pointer(id);
                ushort flags = Get(current.ReadArmyReturnPersonFlags196(p), "movement Person15E");
                current.WriteArmyReturnPersonFlags196(p, (ushort)(flags | 1));
                var position = ResolvePersonPosition(state, current, p, date);
                ushort area = Get(current.ReadHexAreaSlot(position), "movement current Area");
                current.WritePersonSecondLocation(p, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
                task = task with { MemberIds = task.MemberIds.Append(id).ToArray() };
                owner.Returns[taskId] = task;
                current.SetLinkedEntityMemberIds(taskId, task.MemberIds.Select(person => (uint?)person).ToArray());
            }
            var route = PersonReturnPathSource.QueryReassignment(state, current, homeId, destinationId);
            int days = Get(route.Days, route.UnresolvedOwner ?? "movement2318F0");
            task = task with { ArriveOn = DateFromOrdinal(unchecked(DateOrdinal(date) + days)) };
            owner.Returns[taskId] = task;
            var working = state with { PersonnelCommands = owner };
            BindCurrent(working, current);
            working = PublishBorrowedCurrent(working, current);
            return new(working, 1, slot, taskId, days, null);

            int Pointer(int id) => Get((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
                "movement Person pointer");
        }
        catch (InvalidDataException error) { return new(null, null, null, null, null, error.Message); }
    }

}

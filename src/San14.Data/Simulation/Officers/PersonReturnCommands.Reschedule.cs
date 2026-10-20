using San14.Core;

namespace San14.Data;

public static partial class PersonReturnCommands
{
    /// <summary>The common cancellation branch keeps only serving members,
    /// preserving member order and duplicates, and creates a new Return task.</summary>
    private static (PersonnelCommandTasks Owner, int? NewTaskId) RescheduleEligibleMembers(GameState state,
        SourceWorldContext current, int[] members, bool isPlayer, NativeMapSeasonDate date)
    {
        var owner = CurrentCommandOwner(state, current);
        if (members.Length == 0) return (owner, null);
        var temporary = current.CreateTemporaryHexList() ?? throw Missing("reschedule person temporary");
        try
        {
            current.ClearTemporaryHexList(temporary);
            var people = new List<int>();
            foreach (int member in members)
            {
                int person = Get((member < 6001 ? current.ReadPersonLookupReference(member) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
                    "reschedule member pointer");
                if (Get(current.ReadPersonStatus(person), "reschedule member status") is not (>= 1 and <= 4)) continue;
                if (current.AppendPersonReturnTemporary(temporary, person)) people.Add(person);
            }
            if (people.Count == 0) return (owner, null);
            if (owner.AvailableDispatchSlots.Length == 0)
            {
                // The source falls through to a null virtual call after its
                // fallback place writes. There is no successful lifecycle to
                // reproduce; retain the original world and report this limit.
                throw Missing("reschedule command allocation failure");
            }
            int id = owner.NextTaskId, slot = owner.AvailableDispatchSlots[0], first = people[0];
            var firstHome = BaseLookup.Find(Get(current.ReadPersonFirstLocation(first), "reschedule first home"), current);
            var destination = ToDestination(firstHome);
            byte district = unchecked((byte)Get(current.ReadBaseDistrictSlot(firstHome), "reschedule district"));
            // Registration precedes group initialization. The new command has
            // no members yet, so position lookup still finds the old command.
            var task = new PersonnelReturnTask(id, slot, [], [], destination, district, 48400,
                false, false, false, false, true, new(0, 1, 1, 0), new(0, 1, 1, 0));
            owner = owner with { NextTaskId = id + 1, AvailableDispatchSlots = owner.AvailableDispatchSlots[1..],
                Returns = new(owner.Returns) { [id] = task } };
            var handle = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("reschedule association index");
            if (!current.AppendPersonReturnAssociated(new(handle.Identity), id)) throw Missing("reschedule association registration");
            BindCurrent(state with { PersonnelCommands = owner }, current);
            var start = ResolvePersonPosition(state, current, first, date);
            var memberIds = new List<int>();
            foreach (int person in people)
            {
                ushort flags = Get(current.ReadArmyReturnPersonFlags196(person), "reschedule person flags");
                current.WriteArmyReturnPersonFlags196(person, OfficerBehaviorSourceAdapter.MarkReturn(flags));
                var position = ResolvePersonPosition(state, current, person, date);
                ushort area = Get(current.ReadHexAreaSlot(position), "reschedule person area");
                current.WritePersonSecondLocation(person, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
                memberIds.Add(Get(current.ReadPersonIdentity(person), "reschedule person identity"));
            }
            var path = current.CreateTemporaryHexList() ?? throw Missing("reschedule path temporary");
            PersonReturnPathResult route;
            try
            {
                int target = current.ReadBaseCurrentHexIndex(firstHome);
                var end = current.ReadHexTableReference(target < 48400 ? target : 0) ?? throw Missing("reschedule target hex");
                var force = current.ReadPersonForce(first) ?? throw Missing("reschedule person force");
                route = PersonReturnPathSource.Fill(state, current, start, end, force, path);
            }
            finally { current.ClearTemporaryHexList(path); current.ReleaseTemporaryHexList(path); }
            int days = Get(route.Days, route.UnresolvedOwner ?? "reschedule travel days");
            owner.Returns[id] = task with { StartHex = Get(start.PhysicalSlot, "reschedule start hex"), MemberIds = memberIds.ToArray(),
                RouteDestinations = [destination], ArriveOn = DateFromOrdinal(unchecked(DateOrdinal(date) + days)), IsPlayer = isPlayer };
            BindCurrent(state with { PersonnelCommands = owner }, current);
            return (owner, id);
        }
        finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
    }
}

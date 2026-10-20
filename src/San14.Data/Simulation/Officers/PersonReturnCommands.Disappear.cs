using San14.Core;

namespace San14.Data;

public sealed record PersonDisappearanceRegistration(GameState? State, int? TaskId, string? UnresolvedOwner);
public sealed record PersonDisappearanceAdvanceReceipt(int TaskId, int? RemainingDays, string Branch, int? ReturnTaskId);

public static partial class PersonReturnCommands
{
    /// <summary>Schedules a fixed-place absence. Force ownership, return-path
    /// legality and player identity are not admission gates for this command.</summary>
    public static PersonDisappearanceRegistration RegisterDisappearance(GameState state, SourceWorldContext current,
        int physical, int delay, bool isPlayer, NativeMapSeasonDate date)
    {
        try
        {
            if (!Get(current.ReadPersonNativeValidity(physical), "absence person validity") || delay <= 0)
                return new(state, null, null);
            var linked = current.ReadPersonLinkedEntity(physical) ?? throw Missing("absence association lookup");
            if (Get(current.ReadLinkedEntityNativeValidity(linked), "absence association validity")) return new(state, null, null);
            var before = CurrentCommandOwner(state, current);
            if (before.Disappearances is null) throw Missing("absence task owner");
            if (before.AvailableDispatchSlots.Length == 0) return new(state, null, null);
            int id = before.NextTaskId, slot = before.AvailableDispatchSlots[0];
            ushort personId = Get(current.ReadPersonIdentity(physical), "absence person identity");
            var home = BaseLookup.Find(Get(current.ReadPersonFirstLocation(physical), "absence home"), current);
            var destination = ToDestination(home);
            if (destination.BaseId <= 0) throw Missing("absence home identity");
            byte district = unchecked((byte)Get(current.ReadBaseDistrictSlot(home), "absence home district"));
            ushort priorCode = Math.Min(Get(current.ReadPersonSecondLocation(physical), "absence prior place"), (ushort)1061);
            var previous = OfficerLocationSourceAdapter.DecodePosition(priorCode, OfficerLocationSourceAdapter.Receivers(state));
            if (previous is null || previous.Kind == OfficerPositionKind.Unresolved) throw Missing("absence prior place identity");
            var at = ResolvePersonPosition(state, current, physical, date);
            ushort area = Get(current.ReadHexAreaSlot(at), "absence current area");
            ushort station = Get(current.ReadAreaHexIndex(new(area)), "absence area representative hex");
            // Both Stay start and target use the same source hex pointer.
            int stationHex = station < 48400 ? station : 0;
            var task = new PersonnelDisappearanceTask(id, slot, [personId], destination, district,
                stationHex, previous, date, DateFromOrdinal(unchecked(DateOrdinal(date) + delay)),
                false, false, isPlayer, false, false, true);
            var owner = before with { NextTaskId = id + 1, AvailableDispatchSlots = before.AvailableDispatchSlots[1..],
                Disappearances = new(before.Disappearances) { [id] = task } };
            var associated = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("absence association index");
            if (!current.AppendPersonReturnAssociated(new(associated.Identity), id)) throw Missing("absence registration node");
            current.WritePersonSecondLocation(physical, area is >= 1 and <= 500 ? (ushort)(area + 561) : (ushort)0);
            var temporary = current.CreateTemporaryHexList() ?? throw Missing("absence initializer temporary");
            current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary);
            var next = state with { PersonnelCommands = owner };
            BindCurrent(next, current);
            return new(next, id, null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private static (PersonnelCommandTasks Owner, PersonDisappearanceAdvanceReceipt Receipt, bool Retire)
        AdvanceDisappearance(GameState state, SourceWorldContext current, int taskId, NativeMapSeasonDate date)
    {
        var owner = CurrentCommandOwner(state, current);
        var task = owner.Disappearances?.GetValueOrDefault(taskId) ?? throw Missing("absence dispatch task");
        if (date.Hour != 8)
        {
            owner.Disappearances![taskId] = task with { NeedsRefresh = true };
            return (owner, new(taskId, null, "source.other_hour", null), false);
        }
        task = task with { Started = true }; owner.Disappearances![taskId] = task;
        int? physical = task.MemberIds.Length == 0 ? null : ResolveMember(task.MemberIds[0]);
        bool cancel = physical is null || Get(current.ReadPersonStatus(physical.Value), "absence first member status") == 9 || task.CancellationRequested;
        int remaining = Math.Max(0, unchecked(DateOrdinal(task.DueOn) - DateOrdinal(date)));
        if (!cancel && remaining > 1)
        {
            owner.Disappearances![taskId] = task with { NeedsRefresh = true };
            return (owner, new(taskId, remaining, "source.wait", null), false);
        }
        if (!cancel && Get(current.ReadPersonStatus(physical!.Value), "absence expiry status") is not (>= 1 and <= 4))
        {
            ushort code = Get(OfficerLocationSourceAdapter.EncodePosition(task.PreviousPosition,
                OfficerLocationSourceAdapter.Receivers(state)), "absence restored place");
            current.WritePersonSecondLocation(physical.Value, Math.Min(code, (ushort)1061));
            return (owner, new(taskId, remaining, "source.restore_previous_place", null), true);
        }
        var returned = RescheduleEligibleMembers(state, current, task.MemberIds, task.IsPlayer, date);
        return (returned.Owner, new(taskId, remaining, cancel ? "source.cancel" : "source.return_to_home", returned.NewTaskId), true);

        int? ResolveMember(int id) => (id < 6001 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot;
    }
}

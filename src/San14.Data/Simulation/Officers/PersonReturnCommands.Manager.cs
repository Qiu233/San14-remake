using San14.Core;

namespace San14.Data;

public sealed record PersonReturnAdvanceReceipt(int CommandId, int ReceiverIdentity,
    int NativeReturn, int? RemainingDays, PersonReturnArrivalReceipt? Arrival, int AssociatedRemoved)
{
    public PersonReturnRetargetReceipt? Retarget { get; init; }
    public PersonReturnCancellationReceipt? Cancellation { get; init; }
}
public sealed record PersonReturnManagerResult(GameState? State,
    IReadOnlyList<PersonReturnAdvanceReceipt> Receipts, string? UnresolvedOwner);

public static partial class PersonReturnCommands
{
    /// <summary>Updates journeys in dispatch order at the morning/evening phases.
    /// Retirement is deferred until every journey in the phase has been visited.</summary>
    public static PersonReturnManagerResult TryAdvanceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool,
        NativeMapSeasonDate date, IArmyReturnGovernorCurrentQueries queries)
    {
        var receipts = new List<PersonReturnAdvanceReceipt>();
        if (date.Hour is not (8 or 22)) return new(state, receipts, null);
        try
        {
            var owner = CurrentCommandOwner(state, current);
            owner = owner with { Tasks = new(owner.Tasks) };
            var pending = owner.PendingRetirements.ToList();
            var working = state with { PersonnelReturns = owner };
            current.SetOwnedPersonCommandManager(owner);
            foreach (var original in owner.InDispatchOrder().ToArray())
            {
                int taskId = original.Id, slot = original.DispatchSlot!.Value;
                var task = owner.Tasks[taskId];
                if (date.Hour == 8)
                {
                    owner.Tasks[taskId] = task with { NeedsRefresh = true };
                    receipts.Add(new(slot, taskId, 1, null, null, 0));
                    continue;
                }
                var retargeted = RetargetCurrentHome(working, current, taskId, date);
                owner = retargeted.Owner ?? throw Missing(retargeted.UnresolvedOwner ?? "home retargeting");
                working = working with { PersonnelReturns = owner };
                task = owner.Tasks[taskId];
                var retarget = retargeted.Receipt?.Branch == "source.same_Force_District_City_retarget" ? retargeted.Receipt : null;
                if (retargeted.NativeReturn == 1)
                {
                    int removed = Retire(task);
                    receipts.Add(new(slot, taskId, 0, null, null, removed) { Cancellation = retargeted.Cancellation });
                    continue;
                }
                if (retargeted.NativeReturn != 0) throw Missing("home retargeting result");
                task = task with { Started = true };
                owner.Tasks[taskId] = task;
                if (!CommandDateNativeValid(task.ArriveOn))
                {
                    var initialized = EnsureArrivalDate(working, current, taskId, date);
                    owner = initialized.Owner ?? throw Missing(initialized.UnresolvedOwner ?? "journey arrival date");
                    task = owner.Tasks[taskId];
                    working = working with { PersonnelReturns = owner };
                }
                int duration = unchecked(DateOrdinal(task.ArriveOn) - DateOrdinal(task.StartedOn));
                int elapsed = Math.Max(0, unchecked(DateOrdinal(date) - DateOrdinal(task.StartedOn)));
                int remaining = Math.Max(0, unchecked(duration - elapsed));
                if (remaining > 1)
                {
                    owner.Tasks[taskId] = task with { NeedsRefresh = true };
                    receipts.Add(new(slot, taskId, 1, remaining, null, 0) { Retarget = retarget });
                    continue;
                }
                var arrived = PersonReturnArrivalSource.Apply(working, current, pool, taskId,
                    ToBase(Get(task.Destination, "return journey destination")), queries);
                working = arrived.State ?? throw Missing(arrived.UnresolvedOwner ?? "personnel arrival");
                int associationsRemoved = Retire(task);
                receipts.Add(new(slot, taskId, 0, remaining, arrived.Receipt, associationsRemoved) { Retarget = retarget });
            }
            var freeSlots = owner.AvailableDispatchSlots.ToList();
            foreach (int taskId in pending)
            {
                var task = owner.Tasks[taskId];
                if (task.DispatchSlot is int slot) freeSlots.Insert(0, slot);
                owner.Tasks.Remove(taskId);
            }
            owner = owner with { AvailableDispatchSlots = freeSlots.ToArray(), PendingRetirements = [] };
            current.SetOwnedPersonCommandManager(owner);
            working = PublishCurrent(working with { PersonnelReturns = owner }, current);
            BindCurrent(working, current);
            return new(working, receipts.AsReadOnly(), null);

            int Retire(PersonnelReturnTask task)
            {
                if (task.IsRetiring) return 0;
                owner.Tasks[task.Id] = task with { IsRetiring = true, IsAssociated = false };
                pending.Add(task.Id);
                var associated = current.OwnedIndexes?.AssociatedTaskHandle
                    ?? throw Missing("personnel association index");
                return current.RemovePersonReturnAssociated(new(associated.Identity), task.Id);
            }
        }
        catch (InvalidDataException error) { return new(null, receipts.AsReadOnly(), error.Message); }
    }
}

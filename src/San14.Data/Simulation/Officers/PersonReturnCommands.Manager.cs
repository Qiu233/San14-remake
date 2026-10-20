using San14.Core;

namespace San14.Data;

public sealed record PersonReturnAdvanceReceipt(int CommandId, int ReceiverIdentity,
    int NativeReturn, int? RemainingDays, PersonReturnArrivalReceipt? Arrival, int AssociatedRemoved)
{
    public PersonReturnRetargetReceipt? Retarget { get; init; }
    public PersonReturnCancellationReceipt? Cancellation { get; init; }
}
public sealed record PersonReturnManagerResult(GameState? State,
    IReadOnlyList<PersonReturnAdvanceReceipt> Receipts, string? UnresolvedOwner)
{
    public IReadOnlyList<PersonDisappearanceAdvanceReceipt> Disappearances { get; init; } = [];
}

public static partial class PersonReturnCommands
{
    /// <summary>Updates journeys in dispatch order at the morning/evening phases.
    /// Retirement is deferred until every journey in the phase has been visited.</summary>
    public static PersonReturnManagerResult TryAdvanceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool,
        NativeMapSeasonDate date, IArmyReturnGovernorCurrentQueries queries)
    {
        var receipts = new List<PersonReturnAdvanceReceipt>();
        var absences = new List<PersonDisappearanceAdvanceReceipt>();
        if (date.Hour is not (8 or 22)) return new(state, receipts, null);
        try
        {
            var owner = CurrentCommandOwner(state, current);
            if (owner.Disappearances is null) throw Missing("absence task owner");
            owner = owner with { Returns = new(owner.Returns), Disappearances = new(owner.Disappearances) };
            var pending = owner.PendingRetirements.ToList();
            var working = state with { PersonnelCommands = owner };
            current.SetOwnedPersonCommandManager(owner);
            int previousSlot = -1;
            while (owner.DispatchOrder().FirstOrDefault(task => task.Slot > previousSlot) is { } entry)
            {
                int taskId = entry.Id, slot = entry.Slot;
                previousSlot = slot;
                if (entry.Kind == PersonnelCommandKind.Disappear)
                {
                    var result = AdvanceDisappearance(working, current, taskId, date);
                    owner = result.Owner; working = working with { PersonnelCommands = owner };
                    absences.Add(result.Receipt);
                    if (result.Retire) RetireDisappearance(taskId);
                    continue;
                }
                var task = owner.Returns[taskId];
                if (date.Hour == 8)
                {
                    owner.Returns[taskId] = task with { NeedsRefresh = true };
                    receipts.Add(new(slot, taskId, 1, null, null, 0));
                    continue;
                }
                var retargeted = RetargetCurrentHome(working, current, taskId, date);
                owner = retargeted.Owner ?? throw Missing(retargeted.UnresolvedOwner ?? "home retargeting");
                working = working with { PersonnelCommands = owner };
                task = owner.Returns[taskId];
                var retarget = retargeted.Receipt?.Branch == "source.same_Force_District_City_retarget" ? retargeted.Receipt : null;
                if (retargeted.NativeReturn == 1)
                {
                    int removed = Retire(task);
                    receipts.Add(new(slot, taskId, 0, null, null, removed) { Cancellation = retargeted.Cancellation });
                    continue;
                }
                if (retargeted.NativeReturn != 0) throw Missing("home retargeting result");
                task = task with { Started = true };
                owner.Returns[taskId] = task;
                if (!CommandDateNativeValid(task.ArriveOn))
                {
                    var initialized = EnsureArrivalDate(working, current, taskId, date);
                    owner = initialized.Owner ?? throw Missing(initialized.UnresolvedOwner ?? "journey arrival date");
                    task = owner.Returns[taskId];
                    working = working with { PersonnelCommands = owner };
                }
                int duration = unchecked(DateOrdinal(task.ArriveOn) - DateOrdinal(task.StartedOn));
                int elapsed = Math.Max(0, unchecked(DateOrdinal(date) - DateOrdinal(task.StartedOn)));
                int remaining = Math.Max(0, unchecked(duration - elapsed));
                if (remaining > 1)
                {
                    owner.Returns[taskId] = task with { NeedsRefresh = true };
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
                if (owner.Returns.Remove(taskId, out var task))
                { if (task.DispatchSlot is int slot) freeSlots.Insert(0, slot); }
                else if (owner.Disappearances!.Remove(taskId, out var absence)) freeSlots.Insert(0, absence.DispatchSlot);
                else throw Missing("retiring personnel task");
            }
            owner = owner with { AvailableDispatchSlots = freeSlots.ToArray(), PendingRetirements = [] };
            current.SetOwnedPersonCommandManager(owner);
            working = PublishCurrent(working with { PersonnelCommands = owner }, current);
            BindCurrent(working, current);
            return new(working, receipts.AsReadOnly(), null) { Disappearances = absences.AsReadOnly() };

            void RetireDisappearance(int id)
            {
                var task = owner.Disappearances![id];
                if (task.IsRetiring) return;
                owner.Disappearances[id] = task with { IsRetiring = true, IsAssociated = false };
                pending.Add(id);
                var associated = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("personnel association index");
                current.RemovePersonReturnAssociated(new(associated.Identity), id);
            }

            int Retire(PersonnelReturnTask task)
            {
                if (task.IsRetiring) return 0;
                owner.Returns[task.Id] = task with { IsRetiring = true, IsAssociated = false };
                pending.Add(task.Id);
                var associated = current.OwnedIndexes?.AssociatedTaskHandle
                    ?? throw Missing("personnel association index");
                return current.RemovePersonReturnAssociated(new(associated.Identity), task.Id);
            }
        }
        catch (InvalidDataException error) { return new(null, receipts.AsReadOnly(), error.Message) { Disappearances = absences.AsReadOnly() }; }
    }
}

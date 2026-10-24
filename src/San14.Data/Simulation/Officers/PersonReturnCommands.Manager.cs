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
    public int? NextAfterSlot { get; init; }
    public int? AwaitingDiplomacyTaskId { get; init; }
}

public static partial class PersonReturnCommands
{
    /// <summary>Updates journeys in dispatch order at the morning/evening phases.
    /// Retirement is deferred until every journey in the phase has been visited.</summary>
    public static PersonReturnManagerResult TryAdvanceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool,
        NativeMapSeasonDate date, IArmyReturnGovernorCurrentQueries queries)
        => AdvanceCurrent(state, current, pool, date, queries, false);

    internal static PersonReturnManagerResult TryAdvanceBorrowedCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, NativeMapSeasonDate date,
        IArmyReturnGovernorCurrentQueries queries)
        => AdvanceCurrent(state, current, pool, date, queries, true);

    internal static PersonReturnManagerResult AdvanceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, NativeMapSeasonDate date,
        IArmyReturnGovernorCurrentQueries queries, bool borrowedCurrent, IPersonnelTalentExecution? talent = null,
        int resumeAfterSlot = -1)
    {
        var receipts = new List<PersonReturnAdvanceReceipt>();
        var absences = new List<PersonDisappearanceAdvanceReceipt>();
        if (date.Hour is not (8 or 22)) return new(state, receipts, null);
        try
        {
            var owner = CurrentCommandOwner(state, current);
            if (owner.Disappearances is null) throw Missing("absence task owner");
            owner = owner with { Returns = new(owner.Returns), Disappearances = new(owner.Disappearances),
                Talents = owner.Talents is null ? null : new(owner.Talents),
                Diplomacies = owner.Diplomacies is null ? null : new(owner.Diplomacies) };
            var pending = owner.PendingRetirements.ToList();
            var working = state with { PersonnelCommands = owner };
            current.SetOwnedPersonCommandManager(owner);
            int previousSlot = resumeAfterSlot;
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
                if (entry.Kind == PersonnelCommandKind.Diplomacy)
                {
                    if (date.Hour != 8) continue;
                    if (talent is not IPersonnelDiplomacyExecution diplomacy)
                        throw Missing("diplomacy execution owner");
                    var advanced = diplomacy.AdvanceDiplomacyCurrent(working, current, owner.Diplomacies![taskId], date);
                    working = advanced.State;
                    owner = CurrentCommandOwner(working, current);
                    SyncPending();
                    if (advanced.Retire) QueueRetirement(taskId);
                    if (owner.Diplomacies?.GetValueOrDefault(taskId)?.AwaitingNegotiation == true)
                    {
                        //No retirement is consumed yet. Save the actual manager
                        //prefix and continue strictly after this dispatch slot
                        //only after its explicit negotiation order succeeds.
                        owner = owner with { PendingRetirements = pending.ToArray() };
                        current.SetOwnedPersonCommandManager(owner);
                        working = borrowedCurrent
                            ? PublishBorrowedCurrent(working with { PersonnelCommands = owner }, current)
                            : PublishCurrent(working with { PersonnelCommands = owner }, current);
                        return new(working, receipts.AsReadOnly(), null)
                        {
                            Disappearances = absences.AsReadOnly(), NextAfterSlot = slot,
                            AwaitingDiplomacyTaskId = taskId
                        };
                    }
                    continue;
                }
                if (entry.Kind == PersonnelCommandKind.Talent)
                {
                    var taskTalent = owner.Talents![taskId];
                    if (date.Hour == 8)
                    {
                        owner.Talents[taskId] = taskTalent with { NeedsRefresh = true };
                        continue;
                    }
                    if (talent is null) throw Missing("talent execution owner");
                    var advanced = talent.AdvanceTalentCurrent(working, current, taskTalent, date);
                    working = advanced.State;
                    owner = CurrentCommandOwner(working, current);
                    SyncPending();
                    if (advanced.Retire) QueueRetirement(taskId);
                    continue;
                }
                var task = owner.Returns[taskId];
                if (date.Hour == 8 && !task.IsReassignment)
                {
                    owner.Returns[taskId] = task with { NeedsRefresh = true };
                    receipts.Add(new(slot, taskId, 1, null, null, 0));
                    continue;
                }
                if (task.IsReassignment)
                {
                    int movementRemaining = Math.Max(0, unchecked(DateOrdinal(task.ArriveOn) - DateOrdinal(date)));
                    task = task with { NeedsRefresh = true };
                    owner.Returns[taskId] = task;
                    if (date.Hour == 22 && movementRemaining > 1)
                    {
                        receipts.Add(new(slot, taskId, 1, movementRemaining, null, 0));
                        continue;
                    }
                    bool canceled = MovementDestinationLost(current, task);
                    if (canceled)
                    {
                        var cancellation = CancelCurrentMembers(working, current, taskId, date);
                        owner = cancellation.Owner ?? throw Missing(cancellation.UnresolvedOwner ?? "movement cancellation");
                        SyncPending();
                        working = working with { PersonnelCommands = owner };
                        int removed = Retire(task);
                        receipts.Add(new(slot, taskId, 0, movementRemaining, null, removed)
                            { Cancellation = cancellation.Receipt });
                        continue;
                    }
                    if (date.Hour == 8)
                    {
                        receipts.Add(new(slot, taskId, 1, null, null, 0));
                        continue;
                    }
                    task = task with { Started = true };
                    owner.Returns[taskId] = task;
                    var movementArrival = ApplyArrival(working, taskId, ToBase(Get(task.Destination, "movement destination")));
                    working = movementArrival.State ?? throw Missing(movementArrival.UnresolvedOwner ?? "movement arrival32B190");
                    owner = CurrentCommandOwner(working, current);
                    SyncPending();
                    int arrivedRemoved = Retire(task);
                    receipts.Add(new(slot, taskId, 0, movementRemaining, movementArrival.Receipt, arrivedRemoved));
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
                var arrived = ApplyArrival(working, taskId, ToBase(Get(task.Destination, "return journey destination")));
                working = arrived.State ?? throw Missing(arrived.UnresolvedOwner ?? "personnel arrival");
                owner = CurrentCommandOwner(working, current);
                SyncPending();
                int associationsRemoved = Retire(task);
                receipts.Add(new(slot, taskId, 0, remaining, arrived.Receipt, associationsRemoved) { Retarget = retarget });
            }
            var freeSlots = owner.AvailableDispatchSlots.ToList();
            var removedAssociations = new Dictionary<int, int>();
            foreach (int taskId in pending)
            {
                var associated = current.OwnedIndexes?.AssociatedTaskHandle ?? throw Missing("personnel association index");
                removedAssociations[taskId] = current.RemovePersonReturnAssociated(new(associated.Identity), taskId);
                current.SetLinkedEntityRawState58(taskId, -1);
                current.SetLinkedEntityMemberIds(taskId, []);
                if (owner.Returns.Remove(taskId, out var task))
                { if (task.DispatchSlot is int slot) freeSlots.Insert(0, slot); }
                else if (owner.Disappearances!.Remove(taskId, out var absence)) freeSlots.Insert(0, absence.DispatchSlot);
                else if (owner.Talents?.Remove(taskId, out var talentTask) == true) freeSlots.Insert(0, talentTask.DispatchSlot);
                else if (owner.Diplomacies?.Remove(taskId, out var diplomacyTask) == true) freeSlots.Insert(0, diplomacyTask.DispatchSlot);
                else throw Missing("retiring personnel task");
            }
            for (int i = 0; i < receipts.Count; i++)
                if (removedAssociations.TryGetValue(receipts[i].ReceiverIdentity, out int removed))
                    receipts[i] = receipts[i] with { AssociatedRemoved = removed };
            owner = owner with { AvailableDispatchSlots = freeSlots.ToArray(), PendingRetirements = [] };
            current.SetOwnedPersonCommandManager(owner);
            working = borrowedCurrent
                ? PublishBorrowedCurrent(working with { PersonnelCommands = owner }, current)
                : PublishCurrent(working with { PersonnelCommands = owner }, current);
            BindCurrent(working, current);
            return new(working, receipts.AsReadOnly(), null) { Disappearances = absences.AsReadOnly() };

            PersonReturnArrivalResult ApplyArrival(GameState input, int id, BaseReference destination)
                => talent is ArmyCampaignRuleAdapter adapter
                    ? adapter.ApplyPersonReturnArrivalCurrent(input, current, pool, id, destination, queries, borrowedCurrent, date)
                    : PersonReturnArrivalSource.Apply(input, current, pool, id, destination, queries, borrowedCurrent);

            void SyncPending()
            {
                foreach (int id in owner.PendingRetirements)
                    if (!pending.Contains(id)) pending.Add(id);
            }

            void QueueRetirement(int id)
            {
                owner = QueueRetirementCurrent(owner, current, id);
                SyncPending();
                working = working with { PersonnelCommands = owner };
            }

            void RetireDisappearance(int id) => QueueRetirement(id);

            int Retire(PersonnelReturnTask task)
            {
                QueueRetirement(task.Id);
                return 0;
            }
        }
        catch (InvalidDataException error) { return new(null, receipts.AsReadOnly(), error.Message) { Disappearances = absences.AsReadOnly() }; }
    }
}

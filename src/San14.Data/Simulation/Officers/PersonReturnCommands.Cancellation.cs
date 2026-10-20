using San14.Core;

namespace San14.Data;

public sealed record PersonReturnCancellationMember(int Id, int PersonPhysical, byte StatusE6);
public sealed record PersonReturnCancellationReceipt(int ReceiverIdentity, int MemberCount,
    IReadOnlyList<PersonReturnCancellationMember> Members, int? TemporaryPersonDescriptor,
    string Branch, IReadOnlyList<string> SourceEffectOrder)
{
    public int? ScheduledReturnTaskId { get; init; }
}
public sealed record PersonReturnCancellationResult(PersonnelCommandTasks? Owner,
    int? NativeReturn, PersonReturnCancellationReceipt? Receipt, string? UnresolvedOwner);

public static partial class PersonReturnCommands
{
    public static PersonReturnCancellationResult CancelWithoutEligibleMembers(GameState state,
        SourceWorldContext current, int taskId)
    {
        var examined = new List<PersonReturnCancellationMember>();
        try
        {
            var owner = CurrentCommandOwner(state, current);
            var task = owner.Returns.GetValueOrDefault(taskId) ?? throw Missing("return journey");
            foreach (int id in task.MemberIds)
            {
                var reference = id < 6001 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
                int physical = reference?.PhysicalSlot ?? throw Missing("return journey officer");
                byte status = Get(current.ReadPersonStatus(physical), "return journey officer status");
                examined.Add(new(id, physical, status));
                if (status is >= 1 and <= 4) throw Missing("cancellation requires scheduling eligible members again");
            }
            return new(owner, 1, new(taskId, task.MemberIds.Length, examined.AsReadOnly(), null,
                task.MemberIds.Length == 0 ? "source.cancel_empty_Members" : "source.cancel_no_eligible_Members", []), null);
        }
        catch (InvalidDataException error) { return new(null, null, null, error.Message); }
    }

    private static PersonReturnCancellationResult CancelCurrentMembers(GameState state,
        SourceWorldContext current, int taskId, NativeMapSeasonDate date)
    {
        try
        {
            var owner = CurrentCommandOwner(state, current);
            var task = owner.Returns.GetValueOrDefault(taskId) ?? throw Missing("return cancellation task");
            var members = new List<PersonReturnCancellationMember>();
            foreach (int member in task.MemberIds)
            {
                int physical = (member < 6001 ? current.ReadPersonLookupReference(member) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
                    ?? throw Missing("return cancellation member pointer");
                members.Add(new(member, physical, Get(current.ReadPersonStatus(physical), "return cancellation member status")));
            }
            var result = RescheduleEligibleMembers(state, current, task.MemberIds, task.IsPlayer, date);
            return new(result.Owner, 1, new(taskId, task.MemberIds.Length, members.AsReadOnly(), null,
                result.NewTaskId.HasValue ? "source.cancel_and_reschedule" : "source.cancel_without_serving_members", [])
                { ScheduledReturnTaskId = result.NewTaskId }, null);
        }
        catch (InvalidDataException error) { return new(null, null, null, error.Message); }
    }
}

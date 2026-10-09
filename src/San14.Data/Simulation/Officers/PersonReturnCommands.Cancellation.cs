using San14.Core;

namespace San14.Data;

public sealed record PersonReturnCancellationMember(int Id, int PersonPhysical, byte StatusE6);
public sealed record PersonReturnCancellationReceipt(int ReceiverIdentity, int MemberCount,
    IReadOnlyList<PersonReturnCancellationMember> Members, int? TemporaryPersonDescriptor,
    string Branch, IReadOnlyList<string> SourceEffectOrder);
public sealed record PersonReturnCancellationResult(PersonnelReturnTasks? Owner,
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
            var task = owner.Tasks.GetValueOrDefault(taskId) ?? throw Missing("return journey");
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
}

using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonReturnCancellationMember(int Id, int PersonPhysical, byte StatusE6);
public sealed record OriginalPersonReturnCancellationReceipt(int ReceiverIdentity, int MemberCount,
    IReadOnlyList<OriginalPersonReturnCancellationMember> Members, int? TemporaryPersonDescriptor,
    string Branch, IReadOnlyList<string> SourceEffectOrder);
public sealed record OriginalPersonReturnCancellationResult(PersonnelReturnTasks? Owner,
    int? NativeReturn, OriginalPersonReturnCancellationReceipt? Receipt, string? UnresolvedOwner);

public static partial class OriginalPersonReturnCommands
{
    public static OriginalPersonReturnCancellationResult CancelWithoutEligibleMembers(GameState state,
        OriginalEventInitializationContext current, int taskId)
    {
        var examined = new List<OriginalPersonReturnCancellationMember>();
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

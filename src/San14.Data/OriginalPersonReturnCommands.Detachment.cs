using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonCommandMemberDetachmentReceipt(int PersonPhysical, ushort PersonId,
    int? ReceiverIdentity, int[] RemovedValueNodes);
public sealed record OriginalPersonCommandMemberDetachmentResult(PersonnelReturnTasks? Owner,
    OriginalPersonCommandMemberDetachmentReceipt? Receipt, string? UnresolvedOwner);

public static partial class OriginalPersonReturnCommands
{
    public static OriginalPersonCommandMemberDetachmentResult DetachPersonFromFirstAssociated(
        GameState state, OriginalEventInitializationContext current, int personPhysical)
    {
        try
        {
            var owner = CurrentCommandOwner(state, current);
            ushort id = Get(current.ReadPersonIdentity(personPhysical), "return member identity");
            var linked = current.ReadPersonLinkedEntity(personPhysical) ?? throw Missing("personnel association");
            if (!Get(current.ReadLinkedEntityNativeValidity(linked), "personnel association validity"))
                return new(owner, new(personPhysical, id, null, []), null);
            int taskId = Get(linked.Identity, "personnel task identity");
            var task = owner.Tasks.GetValueOrDefault(taskId) ?? throw Missing("associated personnel journey");
            int[] removed = task.MemberIds.Select((member, index) => (member, index))
                .Where(entry => entry.member == id).Select(entry => entry.index).ToArray();
            if (removed.Length == 0) return new(owner, new(personPhysical, id, taskId, []), null);
            var updated = task with { MemberIds = task.MemberIds.Where(member => member != id).ToArray() };
            var after = owner with { Tasks = new(owner.Tasks) { [taskId] = updated } };
            current.SetOwnedPersonCommandManager(after);
            current.SetLinkedEntityMemberIds(taskId, updated.MemberIds.Select(member => (uint?)member).ToArray());
            return new(after, new(personPhysical, id, taskId, removed), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
}

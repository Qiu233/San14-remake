using San14.Core;

namespace San14.Data;

public sealed record PersonCommandMemberDetachmentReceipt(int PersonPhysical, ushort PersonId,
    int? ReceiverIdentity, int[] RemovedValueNodes);
public sealed record PersonCommandMemberDetachmentResult(PersonnelCommandTasks? Owner,
    PersonCommandMemberDetachmentReceipt? Receipt, string? UnresolvedOwner);

public static partial class PersonReturnCommands
{
    public static PersonCommandMemberDetachmentResult DetachPersonFromFirstAssociated(
        GameState state, SourceWorldContext current, int personPhysical)
    {
        try
        {
            var owner = CurrentCommandOwner(state, current);
            ushort id = Get(current.ReadPersonIdentity(personPhysical), "return member identity");
            var linked = current.ReadPersonLinkedEntity(personPhysical) ?? throw Missing("personnel association");
            if (!Get(current.ReadLinkedEntityNativeValidity(linked), "personnel association validity"))
                return new(owner, new(personPhysical, id, null, []), null);
            int taskId = Get(linked.Identity, "personnel task identity");
            if (owner.Disappearances?.GetValueOrDefault(taskId) is { } absence)
            {
                int[] removedAbsence = absence.MemberIds.Select((member, index) => (member, index))
                    .Where(entry => entry.member == id).Select(entry => entry.index).ToArray();
                var members = absence.MemberIds.Where(member => member != id).ToArray();
                var afterAbsence = owner with { Disappearances = new(owner.Disappearances)
                    { [taskId] = absence with { MemberIds = members } } };
                current.SetOwnedPersonCommandManager(afterAbsence);
                current.SetLinkedEntityMemberIds(taskId, members.Select(member => (uint?)member).ToArray());
                return new(afterAbsence, new(personPhysical, id, taskId, removedAbsence), null);
            }
            var task = owner.Returns.GetValueOrDefault(taskId) ?? throw Missing("associated personnel journey");
            int[] removed = task.MemberIds.Select((member, index) => (member, index))
                .Where(entry => entry.member == id).Select(entry => entry.index).ToArray();
            if (removed.Length == 0) return new(owner, new(personPhysical, id, taskId, []), null);
            var updated = task with { MemberIds = task.MemberIds.Where(member => member != id).ToArray() };
            var after = owner with { Returns = new(owner.Returns) { [taskId] = updated } };
            current.SetOwnedPersonCommandManager(after);
            current.SetLinkedEntityMemberIds(taskId, updated.MemberIds.Select(member => (uint?)member).ToArray());
            return new(after, new(personPhysical, id, taskId, removed), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
}

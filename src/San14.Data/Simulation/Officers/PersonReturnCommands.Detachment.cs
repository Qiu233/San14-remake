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
            //228890 has a common receiver+20 member list. It does not
            //dispatch on the receiver's Return/Talent/Diplomacy virtual kind.
            if (id is < 1 or > 6000) return new(owner, new(personPhysical, id, null, []), null);
            var linked = current.ReadPersonLinkedEntity(personPhysical) ?? throw Missing("personnel association");
            if (!Get(current.ReadLinkedEntityNativeValidity(linked), "personnel association validity"))
                return new(owner, new(personPhysical, id, null, []), null);
            int taskId = Get(linked.Identity, "personnel task identity");
            int[] members = owner.Returns.TryGetValue(taskId, out var journey) ? journey.MemberIds :
                owner.Disappearances?.TryGetValue(taskId, out var absence) == true ? absence.MemberIds :
                owner.Talents?.TryGetValue(taskId, out var talent) == true ? talent.MemberIds :
                owner.Diplomacies?.TryGetValue(taskId, out var diplomacy) == true ? diplomacy.MemberIds :
                owner.Caravans?.TryGetValue(taskId, out var caravan) == true ? caravan.MemberIds :
                owner.Intelligences?.TryGetValue(taskId, out var intelligence) == true ? intelligence.MemberIds :
                throw Missing("associated personnel command member owner");
            int[] removed = members.Select((member, index) => (member, index))
                .Where(entry => entry.member == id).Select(entry => entry.index).ToArray();
            if (removed.Length == 0) return new(owner, new(personPhysical, id, taskId, []), null);
            int[] remaining = members.Where(member => member != id).ToArray();
            var after = owner.Returns.TryGetValue(taskId, out journey)
                ? owner with { Returns = new(owner.Returns) { [taskId] = journey with { MemberIds = remaining } } }
                : owner.Disappearances?.TryGetValue(taskId, out absence) == true
                ? owner with { Disappearances = new(owner.Disappearances) { [taskId] = absence with { MemberIds = remaining } } }
                : owner.Talents?.TryGetValue(taskId, out talent) == true
                ? owner with { Talents = new(owner.Talents) { [taskId] = talent with { MemberIds = remaining } } }
                : owner.Diplomacies?.TryGetValue(taskId, out diplomacy) == true
                ? owner with { Diplomacies = new(owner.Diplomacies) { [taskId] = diplomacy with { MemberIds = remaining } } }
                : owner.Caravans?.TryGetValue(taskId, out caravan) == true
                ? owner with { Caravans = new(owner.Caravans) { [taskId] = caravan with { MemberIds = remaining } } }
                : owner.Intelligences?.TryGetValue(taskId, out intelligence) == true
                ? owner with { Intelligences = new(owner.Intelligences) { [taskId] = intelligence with { MemberIds = remaining } } }
                : throw Missing("associated personnel command member publication");
            current.SetOwnedPersonCommandManager(after);
            current.SetLinkedEntityMemberIds(taskId, remaining.Select(member => (uint?)member).ToArray());
            return new(after, new(personPhysical, id, taskId, removed), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
}

using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonCommandMemberDetachmentReceipt(int PersonPhysical, ushort PersonId,
    int? ReceiverIdentity, int[] RemovedValueNodes);
public sealed record OriginalPersonCommandMemberDetachmentResult(NativePersonCommandManagerState? Owner,
    OriginalPersonCommandMemberDetachmentReceipt? Receipt, string? UnresolvedOwner);

public static partial class OriginalPersonReturnCommands
{
    /// <summary>265250's22AC00→228890: remove all matching Person IDs from
    /// the first current valid associated receiver. Preserve receiver/manager18,
    /// targets and command ID until the normal source manager retires it.</summary>
    public static OriginalPersonCommandMemberDetachmentResult DetachPersonFromFirstAssociated228890(
        GameState state, OriginalEventInitializationContext current, int personPhysical)
    {
        try
        {
            var owner = CurrentCommandOwner(state, current);
            ushort id = Get(current.ReadPersonIdentity(personPhysical), "detach_Person10");
            var linked = current.ReadPersonLinkedEntity(personPhysical) ?? throw Missing("detach_current_22AC00");
            if (!Get(current.ReadLinkedEntityNativeValidity(linked), "detach_associated_validity"))
                return new(owner, new(personPhysical, id, null, []), null);
            int receiver = Get(linked.Identity, "detach_associated_pointer");
            var command = owner.Commands.GetValueOrDefault(receiver) ?? throw Missing("detach_current_associated_receiver");
            var row = CurrentMemberRow(owner, command);
            var members = new OriginalPersonCommandValuePool(owner.MembersPool);
            var removed = new List<int>();
            for (int? at = row.Head; at is int node;)
            {
                var entry = owner.MembersPool.Nodes[node]; at = entry.Next; //saved Next before018810.
                if (entry.Payload is not { Kind: "MemberId32", Identity: int member }) throw Missing("detach_current_MemberId32");
                if (member == id) { members.RemoveAt(command.Members20, node); removed.Add(node); }
            }
            if (removed.Count == 0) return new(owner, new(personPhysical, id, receiver, []), null);
            var updated = owner with { MembersPool = members.Export() };
            current.SetOwnedPersonCommandManager(updated);
            current.SetLinkedEntityMemberIds(receiver, members.Values(command.Members20)
                .Select(x => x?.Kind == "MemberId32" && x.Identity is int value ? (uint?)value : null).ToArray());
            return new(updated, new(personPhysical, id, receiver, removed.ToArray()), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
}

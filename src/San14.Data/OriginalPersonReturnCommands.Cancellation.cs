using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonReturnCancellationMember(int Id, int PersonPhysical, byte StatusE6);
public sealed record OriginalPersonReturnCancellationReceipt(int ReceiverIdentity, int MemberCount,
    IReadOnlyList<OriginalPersonReturnCancellationMember> Members, int? TemporaryPersonDescriptor,
    string Branch, IReadOnlyList<string> SourceEffectOrder);
public sealed record OriginalPersonReturnCancellationResult(NativePersonCommandManagerState? Owner,
    int? NativeReturn, OriginalPersonReturnCancellationReceipt? Receipt, string? UnresolvedOwner);

public static partial class OriginalPersonReturnCommands
{
    /// <summary>235730's actual Return v68=0, v78/v80/v88=0 branch through
    ///2247F0 when no current member has native status1..4. It creates no new
    ///Return and does not delete or clear the existing receiver. C0's caller
    ///returns1; A0 then returns0 and the manager owns ordinary retirement.</summary>
    public static OriginalPersonReturnCancellationResult TryCancelWithoutEligibleMembers235730(GameState state,
        OriginalEventInitializationContext current, int receiverIdentity)
    {
        OriginalHexListHandle? temporary = null;
        var examined = new List<OriginalPersonReturnCancellationMember>();
        var order = new List<string>();
        try
        {
            var owner = CurrentCommandOwner(state, current);
            var command = owner.Commands.GetValueOrDefault(receiverIdentity) ?? throw Missing("cancel_current_Return_receiver");
            if (command.Kind != "return") throw Missing("branch.cancel_non_Return_receiver");
            order.Add("235730.Return_v80_v88_v78_zero→2247F0");
            var row = CurrentMemberRow(owner, command);
            int count = Get(row.Count, "cancel2247F0_member_count");
            if (count == 0)
                return new(owner, 1, new(receiverIdentity, count, examined.AsReadOnly(), null,
                    "source.cancel_empty_Members", order.AsReadOnly()), null);
            //01ABD0 allocates the shared primary Person descriptor; 669E60
            //clears its backing chain before any current member is inspected.
            temporary = current.CreateTemporaryHexList() ?? throw Missing("cancel2247F0_Person_temporary");
            if (temporary.Identity is not int descriptor) throw Missing("branch.cancel_Person_descriptor_allocation");
            order.Add("01ABD0.primary_Person_descriptor");
            current.ClearTemporaryHexList(temporary); order.Add("669E60.018640.initial_clear");
            for (int? at = row.Head; at is int node; at = owner.MembersPool.Nodes[node].Next)
            {
                var member = owner.MembersPool.Nodes.GetValueOrDefault(node)?.Payload;
                if (member is not { Kind: "MemberId32", Identity: int id }) throw Missing("cancel2247F0_member_ID");
                var reference = id < 6001 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
                int physical = reference?.PhysicalSlot ?? throw Missing("cancel2247F0_current_Person_pointer");
                byte status = Get(current.ReadPersonStatus(physical), "cancel2247F0_PersonE6");
                examined.Add(new(id, physical, status));
                if (status is >= 1 and <= 4) throw Missing("branch.cancel2247F0_eligible_new_Return");
            }
            //No eligible member means no018CC0 node, no1EC330/new Return,
            //no1F68B0, no E4 fallback and no receiver v38 callback.
            current.ClearTemporaryHexList(temporary); order.Add("7B39A0.018640.final_clear");
            current.ReleaseTemporaryHexList(temporary); order.Add("7B39A0.018700.release");
            temporary = null;
            return new(owner, 1, new(receiverIdentity, count, examined.AsReadOnly(), descriptor,
                "source.cancel_no_eligible_Members", order.AsReadOnly()), null);
        }
        catch (InvalidDataException unknown) { return new(null, null, null, unknown.Message); }
        finally
        {
            if (temporary is not null)
            { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        }
    }

    private static NativeSharedListDescriptor CurrentMemberRow(NativePersonCommandManagerState owner,
        NativePersonCommandState command) => command.Members20.Identity is int descriptor &&
            owner.MembersPool.Descriptors.TryGetValue(descriptor, out var row) ? row
            : throw Missing("current_Return_member_descriptor");
}

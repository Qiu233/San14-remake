using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonReturnAdvanceReceipt(int CommandId, int ReceiverIdentity,
    int NativeReturn, int? RemainingDays, OriginalPersonReturnArrivalReceipt? Arrival, int AssociatedRemoved)
{
    public OriginalPersonReturnRetargetReceipt? Retarget { get; init; }
    public OriginalPersonReturnCancellationReceipt? Cancellation { get; init; }
}
public sealed record OriginalPersonReturnManagerResult(GameState? State,
    IReadOnlyList<OriginalPersonReturnAdvanceReceipt> Receipts, string? UnresolvedOwner);

public static partial class OriginalPersonReturnCommands
{
    /// <summary>202360's ordinary Return branch: ordered ID tree at8/22,
    /// hour22 advance, actual manager28 queue then ID/value-list retirement.
    /// Includes ordinary same-Force/same-District City Home retargeting;
    /// Includes cancellation with no eligible members; cancellation requiring
    /// a new Return and non-Return receivers remain separate owners.</summary>
    public static OriginalPersonReturnManagerResult TryAdvanceCurrent(GameState state,
        OriginalEventInitializationContext current, OriginalCurrentListPool pool,
        NativeMapSeasonDate currentWorldThirdDate, IOriginalArmyReturnGovernorCurrentQueries queries)
    {
        var receipts = new List<OriginalPersonReturnAdvanceReceipt>();
        if (currentWorldThirdDate.Hour is not (8 or 22)) return new(state, receipts, null);
        try
        {
            var before = CurrentCommandOwner(state, current);
            var commands = before.Commands.ToDictionary(pair => pair.Key,
                pair => pair.Value with { RawFields = pair.Value.RawFields.ToArray() });
            var ids = new Dictionary<int, int>(before.IdToReceiver); var free = before.FreeNext30.ToArray();
            ushort? freeHead = before.FreeHeadEA8;
            var members = new OriginalPersonCommandValuePool(before.MembersPool);
            var targets = new OriginalPersonCommandValuePool(before.TargetsPool);
            var unionValues = before.TargetNodeValues;
            var working = state with { NativePersonReturnCommands = Image() };
            current.SetOwnedPersonCommandManager(working.NativePersonReturnCommands);
            foreach (var entry in ids.OrderBy(pair => pair.Key).ToArray())
            {
                int receiver = entry.Value;
                var command = commands.GetValueOrDefault(receiver) ?? throw Missing("manager_ID_receiver");
                if (command.Kind != "return") throw Missing("branch.manager_non_Return_receiver");
                if (currentWorldThirdDate.Hour != 22)
                {
                    WriteInt(command, 0x44, 1); //242140 after ReturnA0==1.
                    receipts.Add(new(entry.Key, receiver, 1, null, null, 0)); continue;
                }
                var c0Before = CurrentCommandOwner(working, current);
                var c0 = TryRetargetCurrentCityHome1E93F0(working, current, receiver, currentWorldThirdDate);
                if (c0.Owner is null || c0.NativeReturn is not (0 or 1))
                    throw Missing(c0.UnresolvedOwner ?? "branch.Return_cancel235730");
                command = c0.Owner.Commands[receiver]; commands[receiver] = command;
                if (!ReferenceEquals(c0.Owner.TargetsPool, c0Before.TargetsPool))
                    targets = new OriginalPersonCommandValuePool(c0.Owner.TargetsPool);
                unionValues = c0.Owner.TargetNodeValues;
                var retarget = c0.Receipt?.Branch == "source.same_Force_District_City_retarget" ? c0.Receipt : null;
                if (c0.NativeReturn == 1)
                {
                    //1DC380 returns0 immediately on C0!=0: no started40,
                    //remaining-days query or32B190 arrival occurs.
                    int cancelledAssociationCount = EnqueueRetirement(command, receiver);
                    receipts.Add(new(entry.Key, receiver, 0, null, null, cancelledAssociationCount) { Cancellation = c0.Cancellation });
                    continue;
                }
                ushort code = Get(ReadWord(command, 0x2A), "Return_Cmd2A");
                var requested = OriginalBaseLookup.Find(code, current);
                if (Get(ReadInt(command, 0x40), "Return_started40") == 0) WriteInt(command, 0x40, 1);
                var last = ReadCommandDate(command, 0x66);
                if (!CommandDateNativeValid(last))
                {
                    var initialized = TryInitializeInvalidDeadline2351C0(working, current, receiver, currentWorldThirdDate);
                    if (initialized.Owner is null) throw Missing(initialized.UnresolvedOwner ?? "Command_invalid_deadline2351C0");
                    command = initialized.Owner.Commands[receiver]; commands[receiver] = command;
                    last = ReadCommandDate(command, 0x66);
                }
                var first = ReadCommandDate(command, 0x60);
                int duration = unchecked(DateOrdinal(last) - DateOrdinal(first));
                int elapsed = Math.Max(0, unchecked(DateOrdinal(currentWorldThirdDate) - DateOrdinal(first)));
                int remaining = Math.Max(0, unchecked(duration - elapsed));
                if (remaining > 1)
                {
                    WriteInt(command, 0x44, 1);
                    receipts.Add(new(entry.Key, receiver, 1, remaining, null, 0) { Retarget = retarget }); continue;
                }
                var arrived = OriginalPersonReturnArrivalSource.Apply(working, current, pool, receiver, requested, queries);
                working = arrived.State ?? throw Missing(arrived.UnresolvedOwner ?? "Return32B190_arrival");
                //The independent client creates no original presentation map.
                //1DC380 returns0;242140 invokes110 only while54 is0.
                int removed = EnqueueRetirement(command, receiver);
                receipts.Add(new(entry.Key, receiver, 0, remaining, arrived.Receipt, removed) { Retarget = retarget });
            }
            //202360 visits the actual secondary chain only after the entire ID
            //tree. Its Return destructors use independent member/target pools.
            var secondary = new OriginalRegistryListHandle(before.Secondary28Descriptor.Identity);
            var at = current.ReadAssociatedChainHead(secondary) ?? throw Missing("Return_manager28_head");
            while (at.Identity.HasValue)
            {
                int receiver = (current.ReadAssociatedChainPayload(at) ?? throw Missing("Return_manager28_payload")).Identity
                    ?? throw Missing("Return_manager28_receiver");
                var command = commands.GetValueOrDefault(receiver) ?? throw Missing("Return_delete_current_receiver");
                int id = Get(ReadInt(command, 0x58), "Return_delete_ID58");
                ids.Remove(id); //1EC8C0's tree erase before returning the ID.
                if ((uint)id < 1852) { free[id] = Get(freeHead, "Return_ID_free_head"); freeHead = (ushort)id; }
                targets.Clear(command.Targets38); targets.ReleaseDescriptor(command.Targets38); //221B80.
                members.Clear(command.Members20); members.ReleaseDescriptor(command.Members20); //009D40.
                commands.Remove(receiver); //Deleting destructor releases this receiver.
                at = current.ReadRegistryNodeNext(at) ?? throw Missing("Return_manager28_next");
            }
            current.ClearTemporaryHexList(new(before.Secondary28Descriptor.Identity)); //Keep descriptor.
            var image = Image(); current.SetOwnedPersonCommandManager(image);
            working = PublishCurrent(working with { NativePersonReturnCommands = image }, current);
            BindCurrent(working, current); //Current tree now has the retired IDs erased.
            StateValidator.Validate(working);
            return new(working, receipts.AsReadOnly(), null);

            int EnqueueRetirement(NativePersonCommandState command, int receiver)
            {
                if (Get(ReadInt(command, 0x54), "Return_delete54") != 0) return 0;
                WriteInt(command, 0x54, 1);
                _ = OriginalBaseLookup.Find(Get(ReadWord(command, 0x2A), "Return_delete_Cmd2A"), current);
                if (!current.AppendPersonReturnAssociated(new(before.Secondary28Descriptor.Identity), receiver))
                    throw Missing("branch.Return_delete_secondary_node_allocation");
                var associated = working.NativeArmyLifecycle!.Auxiliaries!.AssociatedCommand18Descriptor!
                    ?? throw Missing("Return_manager18_association");
                return current.RemovePersonReturnAssociated(new(associated.Identity), receiver);
            }

            NativePersonCommandManagerState Image() => before with {
                Commands = commands, IdToReceiver = ids, FreeNext30 = free, FreeHeadEA8 = freeHead,
                MembersPool = members.Export(), TargetsPool = targets.Export(), TargetNodeValues = unionValues };
        }
        catch (InvalidDataException error) { return new(null, receipts.AsReadOnly(), error.Message); }
    }
}

using San14.Core;

namespace San14.Data;

public static partial class OriginalPersonReturnCommands
{
    /// <summary>Current Return target2315C0, not the Person's Home. City3 and
    /// Gate4's decoded313500/313520 thunks both tail-call22D640.</summary>
    public static OriginalHexReference ReadCommandTargetHex(GameState state, OriginalEventInitializationContext current,
        int identity)
    {
        var owner = CurrentCommandOwner(state, current);
        var command = owner.Commands.GetValueOrDefault(identity) ?? throw Missing("current_Command_receiver");
        var target = ReadFirstCommandValue(owner.TargetsPool, command.Targets38);
        if (target is null) return new(null); //The actual empty first-head branch.
        var union = target.Identity is int node ? owner.TargetNodeValues.GetValueOrDefault(node) : null;
        if (union is null || union.FirstKind8 is not (3 or 4) || union.FirstPhysical0 is not int physical)
            throw Missing("Command_target_C8_current_Union_Base");
        var selected = new OriginalBaseReference(union.FirstKind8 == 3 ? OriginalBaseKind.City : OriginalBaseKind.Gate, physical);
        int hex = current.ReadBaseCurrentHexIndex(selected);
        return current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("Command_target_C8_Hex_pointer");
    }

    /// <summary>22D6C0 calls Return D8=22B350. Uses actual temporary nodes,
    /// current first member Force, saved dates and int32 projection order.
    /// Invalid deadlines use the original2351C0 nested writer.</summary>
    public static OriginalHexReference ReadCommandCurrentHex(GameState state, OriginalEventInitializationContext current,
        int identity, NativeMapSeasonDate currentWorldThirdDate)
    {
        var owner = CurrentCommandOwner(state, current);
        var command = owner.Commands.GetValueOrDefault(identity) ?? throw Missing("current_Command_receiver");
        var temporary = current.CreateTemporaryHexList() ?? throw Missing("Command_D8_temporary_descriptor");
        try
        {
            ushort rawStart = Get(ReadWord(command, 0x4C), "Command_start4C");
            var start = current.ReadHexTableReference(rawStart < 48400 ? rawStart : 0) ?? throw Missing("Command_start118_Hex_pointer");
            var end = ReadCommandTargetHex(state, current, identity);
            if (start.PhysicalSlot == end.PhysicalSlot) return start;
            var force = ReadCurrentCommandForce(owner, command, current);
            //Fill performs2AEDC0 and the real2318F0 calculation/State temporary
            //effects even though D8 discards its returned travel-days value.
            var path = OriginalPersonReturnPathSource.Fill(state, current, start, end, force, temporary);
            if (path.Days is null) throw Missing(path.UnresolvedOwner ?? "Command_D8_path_and2318F0");
            var firstDate = ReadCommandDate(command, 0x60);
            var lastDate = ReadCommandDate(command, 0x66);
            if (!CommandDateNativeValid(lastDate))
            {
                var initialized = TryInitializeInvalidDeadline2351C0(state, current, identity, currentWorldThirdDate);
                if (initialized.Owner is null) throw Missing(initialized.UnresolvedOwner ?? "Command_invalid_deadline2351C0");
                command = initialized.Owner.Commands[identity];
                lastDate = ReadCommandDate(command, 0x66);
            }
            int duration = unchecked(DateOrdinal(lastDate) - DateOrdinal(firstDate));
            if (duration <= 0) return end;
            int count = Get(current.ReadTemporaryHexListCount(temporary), "Command_D8_current_node_count");
            int elapsed = Math.Max(0, unchecked(DateOrdinal(currentWorldThirdDate) - DateOrdinal(firstDate)));
            int desired = unchecked(elapsed * count) / duration;
            int index = 0;
            var at = current.ReadHexListHead(temporary) ?? throw Missing("Command_D8_current_path_head");
            while (at.Identity.HasValue)
            {
                if (desired <= index) return current.ReadHexListNodePayload(at) ?? throw Missing("Command_D8_current_path_Hex");
                index++;
                at = current.ReadHexListNodeNext(at) ?? throw Missing("Command_D8_current_path_next");
            }
            return end;
        }
        finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
    }

    private static NativePersonCommandManagerState CurrentCommandOwner(GameState state, OriginalEventInitializationContext current) =>
        current.ReadOwnedPersonCommandManager() ?? state.NativePersonReturnCommands ?? throw Missing("current_Command_manager_owner");

    private static NativeSharedListPayload? ReadFirstCommandValue(NativeSharedListPoolState pool, NativeSharedListHandle handle)
    {
        if (handle.Identity is not int key) return null;
        if (!pool.Descriptors.TryGetValue(key, out var descriptor)) throw Missing("Command_current_value_descriptor");
        if (descriptor.Head is not int node) return null;
        return pool.Nodes.TryGetValue(node, out var value) ? value.Payload ?? throw Missing("Command_current_value_payload") :
            throw Missing("Command_current_value_head_node");
    }

    private static OriginalForceReference ReadCurrentCommandForce(NativePersonCommandManagerState owner,
        NativePersonCommandState command, OriginalEventInitializationContext current)
    {
        var member = ReadFirstCommandValue(owner.MembersPool, command.Members20);
        if (member is not { Kind: "MemberId32", Identity: int id }) throw Missing("Command_22CCB0_first_member");
        var person = id < 6001 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
        int physical = person?.PhysicalSlot ?? throw Missing("Command_22CCB0_current_Person_pointer");
        return Get(current.ReadPersonNativeValidity(physical), "Command_22CCB0_current_Person_validity")
            ? current.ReadPersonForce(physical) ?? throw Missing("Command_22CCB0_current_Person_Force") : new(0);
    }

    private static NativeMapSeasonDate ReadCommandDate(NativePersonCommandState command, int offset) =>
        new(unchecked((short)Get(ReadWord(command, offset), "Command_date_year")),
            unchecked((sbyte)Get(command.RawFields[offset + 2], "Command_date_month")),
            unchecked((sbyte)Get(command.RawFields[offset + 3], "Command_date_day")),
            unchecked((sbyte)Get(command.RawFields[offset + 4], "Command_date_hour")));

    private static bool CommandDateNativeValid(NativeMapSeasonDate date) =>
        !(date.Year == 0 && date.Month == 1 && date.Day == 1 && date.Hour == 0) &&
        unchecked((ushort)date.Year) < 0x160 && unchecked((byte)(date.Month - 1)) < 0xD &&
        unchecked((byte)(date.Day - 1)) < 0x1F && unchecked((byte)date.Hour) < 0x19;
}

using San14.Core;

namespace San14.Data;

public static partial class PersonReturnCommands
{
    /// <summary>Reads the first route destination, independently of an officer's home.</summary>
    public static HexReference ReadCommandTargetHex(GameState state, SourceWorldContext current,
        int identity)
    {
        var owner = CurrentCommandOwner(state, current);
        var command = owner.Tasks.GetValueOrDefault(identity) ?? throw Missing("current_Command_receiver");
        if (command.RouteDestinations.Length == 0) return new(null);
        var selected = ToBase(command.RouteDestinations[0]);
        int hex = current.ReadBaseCurrentHexIndex(selected);
        return current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("Command_target_C8_Hex_pointer");
    }

    /// <summary>Resolves journey position from elapsed travel time. Missing arrival dates are initialized and published before interpolation.</summary>
    public static HexReference ResolveJourneyPosition(GameState state, SourceWorldContext current,
        int identity, NativeMapSeasonDate currentWorldThirdDate)
    {
        var owner = CurrentCommandOwner(state, current);
        var command = owner.Tasks.GetValueOrDefault(identity) ?? throw Missing("current_Command_receiver");
        var temporary = current.CreateTemporaryHexList() ?? throw Missing("Command_D8_temporary_descriptor");
        try
        {
            ushort rawStart = checked((ushort)command.StartHex);
            var start = current.ReadHexTableReference(rawStart < 48400 ? rawStart : 0) ?? throw Missing("Command_start118_Hex_pointer");
            var end = ReadCommandTargetHex(state, current, identity);
            if (start.PhysicalSlot == end.PhysicalSlot) return start;
            var force = ReadCurrentCommandForce(owner, command, current);
            //Fill performs2AEDC0 and the real2318F0 calculation/State temporary
            //effects even though D8 discards its returned travel-days value.
            var path = PersonReturnPathSource.Fill(state, current, start, end, force, temporary);
            if (path.Days is null) throw Missing(path.UnresolvedOwner ?? "Command_D8_path_and2318F0");
            var firstDate = command.StartedOn;
            var lastDate = command.ArriveOn;
            if (!CommandDateNativeValid(lastDate))
            {
                var initialized = EnsureArrivalDate(state, current, identity, currentWorldThirdDate);
                if (initialized.Owner is null) throw Missing(initialized.UnresolvedOwner ?? "Command_invalid_deadline2351C0");
                command = initialized.Owner.Tasks[identity];
                lastDate = command.ArriveOn;
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

    private static PersonnelReturnTasks CurrentCommandOwner(GameState state, SourceWorldContext current) =>
        current.ReadOwnedPersonCommandManager() ?? state.PersonnelReturns ?? throw Missing("personnel journey state");

    private static ForceReference ReadCurrentCommandForce(PersonnelReturnTasks owner,
        PersonnelReturnTask command, SourceWorldContext current)
    {
        if (command.MemberIds.Length == 0) throw Missing("return journey member");
        int id = command.MemberIds[0];
        var person = id < 6001 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
        int physical = person?.PhysicalSlot ?? throw Missing("return journey officer");
        return Get(current.ReadPersonNativeValidity(physical), "return journey officer validity")
            ? current.ReadPersonForce(physical) ?? throw Missing("return journey force") : new(0);
    }

    private static bool CommandDateNativeValid(NativeMapSeasonDate date) =>
        !(date.Year == 0 && date.Month == 1 && date.Day == 1 && date.Hour == 0) &&
        unchecked((ushort)date.Year) < 352 && unchecked((byte)(date.Month - 1)) < 13 &&
        unchecked((byte)(date.Day - 1)) < 31 && unchecked((byte)date.Hour) < 25;
}

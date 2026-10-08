using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonReturnDeadlineInitializationResult(
    NativePersonCommandManagerState? Owner, NativeMapSeasonDate? Date66After,
    int? Days, string? UnresolvedOwner);

public static partial class OriginalPersonReturnCommands
{
    /// <summary>3349D0's known-invalid deadline branch calls actual2351C0.
    /// Reads this operation's current command owner and mutates only its new
    /// numeric receiver image. The saved input stays unchanged. Caller adopts
    /// Owner.Commands[receiverIdentity] before continuing A8/D8/manager work.</summary>
    public static OriginalPersonReturnDeadlineInitializationResult TryInitializeInvalidDeadline2351C0(
        GameState state, OriginalEventInitializationContext current, int receiverIdentity,
        NativeMapSeasonDate currentWorldThirdDate)
    {
        OriginalHexListHandle? temporary = null;
        try
        {
            var before = CurrentCommandOwner(state, current);
            var command = before.Commands.GetValueOrDefault(receiverIdentity) ?? throw Missing("deadline_current_receiver");
            if (command.Kind != "return") throw Missing("branch.deadline_non_Return_receiver");
            var deadline = ReadCommandDate(command, 0x66);
            if (CommandDateNativeValid(deadline)) return new(before, deadline, null, null);

            //The source allocates before resolving Force, target and start.
            //Fill reuses2351C0's real empty result list,2AEDC0 and2318F0
            //actorPerson=0 path rather than introducing a one-day fallback.
            temporary = current.CreateTemporaryHexList() ?? throw Missing("deadline_primary_Hex_descriptor");
            var force = ReadCurrentCommandForce(before, command, current);
            var end = ReadCommandTargetHex(state, current, receiverIdentity);
            ushort rawStart = Get(ReadWord(command, 0x4C), "deadline_start4C");
            var start = current.ReadHexTableReference(rawStart < 48400 ? rawStart : 0)
                ?? throw Missing("deadline_start118_Hex_pointer");
            var route = OriginalPersonReturnPathSource.Fill(state, current, start, end, force, temporary);
            int days = Get(route.Days, route.UnresolvedOwner ?? "deadline2318F0_source_days");
            var afterDate = DateFromOrdinal(unchecked(DateOrdinal(currentWorldThirdDate) + days));
            var updated = command with { RawFields = command.RawFields.ToArray() };
            WriteDate(updated, 0x66, afterDate); //33B9E0 writes5 bytes, keeps6B.
            var owner = before with { Commands = new(before.Commands) { [receiverIdentity] = updated } };
            current.SetOwnedPersonCommandManager(owner);
            return new(owner, afterDate, days, null);
        }
        catch (InvalidDataException missing) { return new(null, null, null, missing.Message); }
        finally
        {
            if (temporary is not null)
            {
                current.ClearTemporaryHexList(temporary);
                current.ReleaseTemporaryHexList(temporary);
            }
        }
    }
}

using San14.Core;

namespace San14.Data;

public sealed record PersonReturnDeadlineInitializationResult(
    PersonnelReturnTasks? Owner, NativeMapSeasonDate? ArrivalDate,
    int? Days, string? UnresolvedOwner);

public static partial class PersonReturnCommands
{
    /// <summary>Resolves a missing arrival date against the current journey route and publishes the updated task.</summary>
    public static PersonReturnDeadlineInitializationResult EnsureArrivalDate(
        GameState state, SourceWorldContext current, int receiverIdentity,
        NativeMapSeasonDate currentWorldThirdDate)
    {
        HexListHandle? temporary = null;
        try
        {
            var before = CurrentCommandOwner(state, current);
            var command = before.Tasks.GetValueOrDefault(receiverIdentity) ?? throw Missing("deadline_current_receiver");
            var deadline = command.ArriveOn;
            if (CommandDateNativeValid(deadline)) return new(before, deadline, null, null);

            //The source allocates before resolving Force, target and start.
            //Fill reuses2351C0's real empty result list,2AEDC0 and2318F0
            //actorPerson=0 path rather than introducing a one-day fallback.
            temporary = current.CreateTemporaryHexList() ?? throw Missing("deadline_primary_Hex_descriptor");
            var force = ReadCurrentCommandForce(before, command, current);
            var end = ReadCommandTargetHex(state, current, receiverIdentity);
            ushort rawStart = checked((ushort)command.StartHex);
            var start = current.ReadHexTableReference(rawStart < 48400 ? rawStart : 0)
                ?? throw Missing("deadline_start118_Hex_pointer");
            var route = PersonReturnPathSource.Fill(state, current, start, end, force, temporary);
            int days = Get(route.Days, route.UnresolvedOwner ?? "deadline2318F0_source_days");
            var afterDate = DateFromOrdinal(unchecked(DateOrdinal(currentWorldThirdDate) + days));
            var updated = command with { ArriveOn = afterDate };
            var owner = before with { Tasks = new(before.Tasks) { [receiverIdentity] = updated } };
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

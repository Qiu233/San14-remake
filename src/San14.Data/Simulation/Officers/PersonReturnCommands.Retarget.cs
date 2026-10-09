using San14.Core;

namespace San14.Data;

public sealed record PersonReturnRetargetReceipt(int ReceiverIdentity, ushort PersonId,
    BaseReference PreviousTarget, BaseReference CurrentHome,
    int? PreviousPositionHex, int? AppendedTargetNode, int? PoppedTargetNode,
    NativeMapSeasonDate? Date60After, NativeMapSeasonDate? Date66After, int? Days, string Branch);
public sealed record PersonReturnRetargetResult(PersonnelReturnTasks? Owner,
    int? NativeReturn, PersonReturnRetargetReceipt? Receipt, string? UnresolvedOwner)
{
    public PersonReturnCancellationReceipt? Cancellation { get; init; }
}

public static partial class PersonReturnCommands
{
    /// <summary>Return C0=1E93F0 unchanged or a City Home change whose new
    /// Home has the Person's Force/District. The old City may now be hostile.
    /// Native C0 returns0 to continue A0; this is not arrival
    /// or cancellation. The whole operation publishes the current owner/pool.</summary>
    public static PersonReturnRetargetResult RetargetCurrentHome(GameState state,
        SourceWorldContext current, int receiverIdentity, NativeMapSeasonDate currentWorldThirdDate)
    {
        HexListHandle? temporary = null;
        try
        {
            var before = CurrentCommandOwner(state, current);
            var command = before.Tasks.GetValueOrDefault(receiverIdentity) ?? throw Missing("C0_current_receiver");
            if (command.MemberIds.Length == 0)
                return Cancel();
            int id = command.MemberIds[0];
            var pointer = id < 6001 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
            int person = pointer?.PhysicalSlot ?? throw Missing("C0_first_Person_pointer");
            if (!Get(current.ReadPersonNativeValidity(person), "C0_Person_validity") ||
                Get(current.ReadPersonStatus(person), "C0_PersonE6") is not (>= 1 and <= 4)) return Cancel();
            if (command.RouteDestinations.Length == 0) return Cancel();
            var target = ToBase(command.RouteDestinations[0]);
            var requested = ToBase(Get(command.Destination, "return journey destination"));
            var home = BaseLookup.Find(Get(current.ReadPersonFirstLocation(person), "C0_PersonE2"), current);
            if (target == requested && target == home)
                return new(before, 0, new(receiverIdentity, (ushort)id, target, home, null, null, null, null, null, null,
                    "source.unchanged_Home"), null);

            //The source reads the old D8 before Home validity or any retarget
            //writer. Nested2351C0 may replace its numeric receiver image.
            var oldPosition = ResolveJourneyPosition(state, current, receiverIdentity, currentWorldThirdDate);
            before = CurrentCommandOwner(state, current); command = before.Tasks[receiverIdentity];
            if (!Get(current.ReadBaseNativeValidity(home), "C0_new_Home_validity"))
                return new(before, 0, new(receiverIdentity, (ushort)id, target, home, oldPosition.PhysicalSlot,
                    null, null, null, null, null, "source.invalid_new_Home_no_retarget"), null);
            var force = current.ReadPersonForce(person) ?? throw Missing("C0_current_Person_Force");
            int forcePhysical = Get(current.ReadForcePhysicalSlot(force), "C0_Person_Force_ordinal");
            int district = Get(current.ReadPersonDistrictSlot(person), "C0_Person_DistrictE0");
            //These limits describe this owned producer, not additional source
            //C0 checks. The actual original only tests new Home validity;
            //its old target remains an ordinary Base pointer after capture.
            if (target.Kind != BaseKind.City || home.Kind != BaseKind.City ||
                current.ReadBaseForceSlot(home) != forcePhysical || current.ReadBaseDistrictSlot(home) != district)
                throw Missing("branch.C0_city_Home_change_other_Force_or_District");
            if (command.RouteDestinations.Length != 1) throw Missing("return journey has multiple route destinations");
            var updated = command with { Destination = ToDestination(home), RouteDestinations = [ToDestination(home)],
                StartedOn = currentWorldThirdDate, StartHex = Math.Clamp(Get(oldPosition.PhysicalSlot, "previous journey position"), 0, 48399) };
            temporary = current.CreateTemporaryHexList() ?? throw Missing("C0_retarget_Hex_temporary");
            var start = current.ReadHexTableReference(updated.StartHex)
                ?? throw Missing("C0_new118_Hex_pointer");
            int homeHex = current.ReadBaseCurrentHexIndex(home);
            var end = current.ReadHexTableReference(homeHex < 48400 ? homeHex : 0) ?? throw Missing("C0_Home22D640_Hex_pointer");
            var route = PersonReturnPathSource.Fill(state, current, start, end, force, temporary);
            int days = Get(route.Days, route.UnresolvedOwner ?? "C0_retarget231670_days");
            var deadline = DateFromOrdinal(unchecked(DateOrdinal(currentWorldThirdDate) + days));
            updated = updated with { ArriveOn = deadline };
            var owner = before with { Tasks = new(before.Tasks) { [receiverIdentity] = updated } };
            current.SetOwnedPersonCommandManager(owner);
            return new(owner, 0, new(receiverIdentity, (ushort)id, target, home, oldPosition.PhysicalSlot,
                null, null, currentWorldThirdDate, deadline, days, "source.same_Force_District_City_retarget"), null);

            PersonReturnRetargetResult Cancel()
            {
                var canceled = CancelWithoutEligibleMembers(state, current, receiverIdentity);
                return new(canceled.Owner, canceled.NativeReturn, null, canceled.UnresolvedOwner)
                    { Cancellation = canceled.Receipt };
            }
        }
        catch (InvalidDataException missing) { return new(null, null, null, missing.Message); }
        finally
        {
            if (temporary is not null)
            { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        }
    }
}

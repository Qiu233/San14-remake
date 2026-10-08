using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonReturnRetargetReceipt(int ReceiverIdentity, ushort PersonId,
    OriginalBaseReference PreviousTarget, OriginalBaseReference CurrentHome,
    int? PreviousPositionHex, int? AppendedTargetNode, int? PoppedTargetNode,
    NativeMapSeasonDate? Date60After, NativeMapSeasonDate? Date66After, int? Days, string Branch);
public sealed record OriginalPersonReturnRetargetResult(NativePersonCommandManagerState? Owner,
    int? NativeReturn, OriginalPersonReturnRetargetReceipt? Receipt, string? UnresolvedOwner)
{
    public OriginalPersonReturnCancellationReceipt? Cancellation { get; init; }
}

public static partial class OriginalPersonReturnCommands
{
    /// <summary>Return C0=1E93F0 unchanged or a City Home change whose new
    /// Home has the Person's Force/District. The old City may now be hostile.
    /// Native C0 returns0 to continue A0; this is not arrival
    /// or cancellation. The whole operation publishes the current owner/pool.</summary>
    public static OriginalPersonReturnRetargetResult TryRetargetCurrentCityHome1E93F0(GameState state,
        OriginalEventInitializationContext current, int receiverIdentity, NativeMapSeasonDate currentWorldThirdDate)
    {
        OriginalHexListHandle? temporary = null;
        try
        {
            var before = CurrentCommandOwner(state, current);
            var command = before.Commands.GetValueOrDefault(receiverIdentity) ?? throw Missing("C0_current_receiver");
            if (command.Kind != "return") throw Missing("branch.C0_non_Return_receiver");
            if (Get(CurrentMemberRow(before, command).Count, "C0_member_count") == 0)
                return Cancel();
            var member = ReadFirstCommandValue(before.MembersPool, command.Members20);
            if (member is not { Kind: "MemberId32", Identity: int id }) throw Missing("C0_current_first_member_ID");
            var pointer = id < 6001 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
            int person = pointer?.PhysicalSlot ?? throw Missing("C0_first_Person_pointer");
            if (!Get(current.ReadPersonNativeValidity(person), "C0_Person_validity") ||
                Get(current.ReadPersonStatus(person), "C0_PersonE6") is not (>= 1 and <= 4)) return Cancel();
            var firstTarget = ReadFirstCommandValue(before.TargetsPool, command.Targets38);
            var firstUnion = firstTarget?.Identity is int firstNode ? before.TargetNodeValues.GetValueOrDefault(firstNode) : null;
            if (firstUnion is null) return Cancel();
            if (firstUnion.FirstKind8 is not (3 or 4) || firstUnion.FirstPhysical0 is not int firstPhysical)
                throw Missing("C0_first_Union_Base");
            var target = new OriginalBaseReference(firstUnion.FirstKind8 == 3 ? OriginalBaseKind.City : OriginalBaseKind.Gate, firstPhysical);
            var requested = OriginalBaseLookup.Find(Get(ReadWord(command, 0x2A), "C0_Cmd2A"), current);
            var home = OriginalBaseLookup.Find(Get(current.ReadPersonFirstLocation(person), "C0_PersonE2"), current);
            if (target == requested && target == home)
                return new(before, 0, new(receiverIdentity, (ushort)id, target, home, null, null, null, null, null, null,
                    "source.unchanged_Home"), null);

            //The source reads the old D8 before Home validity or any retarget
            //writer. Nested2351C0 may replace its numeric receiver image.
            var oldPosition = ReadCommandCurrentHex(state, current, receiverIdentity, currentWorldThirdDate);
            before = CurrentCommandOwner(state, current); command = before.Commands[receiverIdentity];
            if (!Get(current.ReadBaseNativeValidity(home), "C0_new_Home_validity"))
                return new(before, 0, new(receiverIdentity, (ushort)id, target, home, oldPosition.PhysicalSlot,
                    null, null, null, null, null, "source.invalid_new_Home_no_retarget"), null);
            var force = current.ReadPersonForce(person) ?? throw Missing("C0_current_Person_Force");
            int forcePhysical = Get(current.ReadForcePhysicalSlot(force), "C0_Person_Force_ordinal");
            int district = Get(current.ReadPersonDistrictSlot(person), "C0_Person_DistrictE0");
            //These limits describe this owned producer, not additional source
            //C0 checks. The actual original only tests new Home validity;
            //its old target remains an ordinary Base pointer after capture.
            if (target.Kind != OriginalBaseKind.City || home.Kind != OriginalBaseKind.City ||
                current.ReadBaseForceSlot(home) != forcePhysical || current.ReadBaseDistrictSlot(home) != district)
                throw Missing("branch.C0_city_Home_change_other_Force_or_District");
            var targets = new OriginalPersonCommandValuePool(before.TargetsPool);
            if (targets.Count(command.Targets38) != 1) throw Missing("branch.C0_multi_target_chain");
            var updated = command with { RawFields = command.RawFields.ToArray() };
            ushort code = Get(current.ReadBaseObjectLink(home), "C0_new_Home4E");
            WriteWord(updated, 0x2A, code);
            var unionValues = new Dictionary<int, NativeCommandUnionValue>(before.TargetNodeValues);
            int? appended = targets.Append(updated.Targets38, new("Union24", 0)); //235000 tail append.
            if (appended is int appendedNode)
            {
                targets.SetPayload(appendedNode, new("Union24", appendedNode));
                int kind = code < 52 ? 3 : 4;
                unionValues[appendedNode] = new(kind, home.PhysicalSlot, kind, home.PhysicalSlot);
            }
            //235480 copies World34 before popping its old current first node.
            WriteDate(updated, 0x60, currentWorldThirdDate);
            int? popped = targets.PopFirst(updated.Targets38);
            WriteWord(updated, 0x4C, (ushort)Math.Clamp(Get(oldPosition.PhysicalSlot, "C0_old_D8_Hex"), 0, 48399));
            temporary = current.CreateTemporaryHexList() ?? throw Missing("C0_retarget_Hex_temporary");
            var start = current.ReadHexTableReference(Get(ReadWord(updated, 0x4C), "C0_new118_start"))
                ?? throw Missing("C0_new118_Hex_pointer");
            int homeHex = current.ReadBaseCurrentHexIndex(home);
            var end = current.ReadHexTableReference(homeHex < 48400 ? homeHex : 0) ?? throw Missing("C0_Home22D640_Hex_pointer");
            var route = OriginalPersonReturnPathSource.Fill(state, current, start, end, force, temporary);
            int days = Get(route.Days, route.UnresolvedOwner ?? "C0_retarget231670_days");
            var deadline = DateFromOrdinal(unchecked(DateOrdinal(currentWorldThirdDate) + days));
            WriteDate(updated, 0x66, deadline);
            var owner = before with { Commands = new(before.Commands) { [receiverIdentity] = updated },
                TargetsPool = targets.Export(), TargetNodeValues = unionValues };
            current.SetOwnedPersonCommandManager(owner);
            return new(owner, 0, new(receiverIdentity, (ushort)id, target, home, oldPosition.PhysicalSlot,
                appended, popped, currentWorldThirdDate, deadline, days, "source.same_Force_District_City_retarget"), null);

            OriginalPersonReturnRetargetResult Cancel()
            {
                var canceled = TryCancelWithoutEligibleMembers235730(state, current, receiverIdentity);
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

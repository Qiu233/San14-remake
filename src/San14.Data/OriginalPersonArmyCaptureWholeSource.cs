using San14.Core;

namespace San14.Data;

public sealed record OriginalPersonArmyCaptureWholeReceipt(int PersonPhysical, ushort PersonId,
    OriginalForceReference SavedOriginalForce, OriginalDistrictReference SavedOriginalDistrict,
    OriginalPersonCaptureWorldCandidatesReceipt? Candidates,
    OriginalPersonAssignmentPrecallResult Appointments,
    OriginalPersonCaptureForceDepartmentsReceipt? Departments,
    OriginalPersonArmyCaptureCustodyReceipt Custody,
    IReadOnlyList<string> SourceEffectOrder);
public sealed record OriginalPersonArmyCaptureWholeResult(GameState? State,
    OriginalPersonArmyCaptureWholeReceipt? Receipt, string? UnresolvedOwner);

/// <summary>2E0FC0's ordinary employed status3/4 capture into a valid winner
/// Army. Runs the actual job/department/primary-District callbacks before
/// status6 and custody writes. The caller owns this private current world;
/// unknown discards the entire enclosing Army operation.</summary>
public static class OriginalPersonArmyCaptureWholeSource
{
    public static OriginalPersonArmyCaptureWholeResult ApplyOrdinaryWinnerArmy(GameState state,
        OriginalEventInitializationContext current, WorldIndexContext pool,
        OriginalOwnedArmyGovernorComparatorInputs governorInputs, int personPhysical,
        OriginalArmyUnitReference winnerArmy, OriginalCaptureApplicationOwners presentation)
    {
        try
        {
            if (Known(presentation.TaskScenePresent, "current.capture_original_TaskScene"))
                throw Missing("branch.original_TaskScene_capture_presentation");
            if (!Known(current.ReadPersonNativeValidity(personPhysical), "current.capture_Person_validity") ||
                Known(current.ReadPersonStatus(personPhysical), "current.capture_PersonE6") is not (3 or 4) ||
                (Known(current.ReadArmyReturnPersonFlags196(personPhysical), "current.capture_Person15E") & 0x4000) != 0)
                throw Missing("branch.ordinary_status3_or4_capture_prefix");
            if (!Known(current.ReadArmyUnitNativeValidity(winnerArmy), "current.capture_winner_Army_validity"))
                throw Missing("branch.capture_nonvalid_winner_Army");
            ushort id = Known(current.ReadPersonIdentity(personPhysical), "current.capture_Person10");
            var originalForce = current.ReadPersonForce(personPhysical) ?? throw Missing("current.capture_saved_Force");
            byte rawDistrict = Known(current.ReadPersonDistrictSlot(personPhysical), "current.capture_saved_DistrictE0");
            var originalDistrict = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
                ?? throw Missing("current.capture_saved_District_pointer");
            var effects = new List<string>();

            var player = OriginalOwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("current.capture_PlayerForce32EFA0");
            int playerPhysical = Known(current.ReadForcePhysicalSlot(player), "current.capture_PlayerForce_ordinal");
            int originalPhysical = Known(current.ReadForcePhysicalSlot(originalForce), "current.capture_original_Force_ordinal");
            OriginalPersonCaptureWorldCandidatesReceipt? candidatesReceipt = null;
            if (playerPhysical != 0 && playerPhysical == originalPhysical)
            {
                var candidates = OriginalPersonCaptureWorldCandidates.ApplyCurrent(state, current, personPhysical,
                    OriginalPersonCaptureCandidatesPresentationAbsent.IndependentClient);
                state = candidates.State ?? throw Missing(candidates.UnresolvedOwner ?? "current.capture_29CCB0");
                candidatesReceipt = candidates.Receipt;
                effects.Add("2E155B.29CCB0_player_selected_Person_clear");
            }
            var appointments = OriginalPersonAssignmentPrecallSource.Apply29C430ForCapture(current, personPhysical);
            if (appointments.NativeReturn is null) throw Missing(appointments.UnresolvedOwner ?? "current.capture_29C430");
            state = OriginalPersonAssignmentPrecallInputs.PublishCurrent(state, current);
            effects.Add("2E1578.29C430_appointments_and_2783B0");
            var departments = OriginalPersonCaptureForceDepartments.ApplyCurrent(state, current, originalForce, personPhysical);
            state = departments.State ?? throw Missing(departments.UnresolvedOwner ?? "current.capture_2E8160");
            effects.Add("2E1597.2E8160_saved_original_Force");
            //R8=0: native-invalid District really returns0. The valid ordinary
            //primary branch executes actual temporary lists and governors;
            //secondary/reselection branches are not substituted by a return.
            if (Known(current.ReadDistrictNativeValidity(originalDistrict), "current.capture_District_validity"))
                OriginalArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(originalDistrict, current,
                    new GovernorQueries(state, governorInputs));
            effects.Add("2E15A5.2E9140_saved_original_District");
            state = OriginalOwnedArmyReturnCurrentFields.PublishCurrent(state, current);

            var begin = OriginalPersonArmyCaptureCustodySource.BeginContext(state, current, personPhysical);
            state = begin.State ?? throw Missing(begin.UnresolvedOwner ?? "current.capture_status6");
            if (begin.Receipt!.RequiresDistrictLeaderRepair)
                throw Missing("branch.capture_post_status2_2E9140_reselection");
            effects.Add("2E15B9.Office73_status6_168_zero");
            var prepared = OriginalPersonArmyCaptureCustodySource.PrepareWinnerArmy(state, current, personPhysical,
                winnerArmy, originalForce);
            var packet = prepared.Packet ?? throw Missing(prepared.UnresolvedOwner ?? "current.capture_winner_custody");
            OriginalPersonArmyCaptureCustodySource.ApplyContext(packet);
            state = OriginalPersonArmyCaptureCustodySource.PublishCurrent(state, packet);
            effects.Add("2E161F.winner_Army_home_E2_E4_saved_old_Force165");
            return new(state, new(personPhysical, id, originalForce, originalDistrict, candidatesReceipt,
                appointments, departments.Receipt, packet.Receipt, effects.ToArray()), null);
        }
        catch (InvalidDataException missing) { return new(null, null, missing.Message); }
    }

    private sealed class GovernorQueries(GameState state, OriginalOwnedArmyGovernorComparatorInputs inputs)
        : IOriginalArmyReturnGovernorCurrentQueries
    {
        public OriginalArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int physical) =>
            inputs.ReadCurrentComparatorFields(state, physical);
    }
    private static T Known<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
}

public sealed partial class OriginalOwnedArmyCampaignRules
{
    internal OriginalPersonArmyCaptureWholeResult ApplyOrdinaryWinnerArmyCapture(GameState state,
        OriginalEventInitializationContext current, WorldIndexContext pool, int personPhysical,
        OriginalArmyUnitReference winnerArmy) => OriginalPersonArmyCaptureWholeSource.ApplyOrdinaryWinnerArmy(
            state, current, pool, _governorInputs, personPhysical, winnerArmy, new(false));
}

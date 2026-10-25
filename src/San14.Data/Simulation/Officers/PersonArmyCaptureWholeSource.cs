using San14.Core;

namespace San14.Data;

public sealed record PersonArmyCaptureWholeReceipt(int PersonPhysical, ushort PersonId,
    ForceReference PriorForce, DistrictReference PriorDistrict,
    CommanderSelectionRemovalReceipt? Candidates,
    PersonAssignmentPrecallResult Appointments,
    PersonCaptureForceDepartmentsReceipt? Departments,
    PersonArmyCaptureCustodyReceipt Custody,
    IReadOnlyList<string> SourceEffectOrder);
public sealed record PersonArmyCaptureWholeResult(GameState? State,
    PersonArmyCaptureWholeReceipt? Receipt, string? UnresolvedOwner);

/// <summary>2E0FC0's ordinary employed status2/3/4 capture into a valid winner
/// Army, or the explicitly admitted single-City ruler. Runs the actual callbacks before
/// status6 and custody writes. The caller owns this private current world;
/// unknown discards the entire enclosing Army operation.</summary>
public static class PersonArmyCaptureWholeSource
{
    public static PersonArmyCaptureWholeResult ApplyOrdinaryWinnerArmy(GameState state,
        SourceWorldContext current, WorldIndexContext pool,
        OwnedArmyGovernorComparatorInputs governorInputs, int personPhysical,
        ArmyUnitReference winnerArmy, CaptureApplicationOwners presentation,
        ArmyCampaignRuleAdapter? package = null, NativeMapSeasonDate? operationDate = null,
        bool allowLastCityRuler = false)
    {
        try
        {
            if (Known(presentation.TaskScenePresent, "current.capture_original_TaskScene"))
                throw Missing("branch.original_TaskScene_capture_presentation");
            if (!Known(current.ReadPersonNativeValidity(personPhysical), "current.capture_Person_validity"))
                throw Missing("branch.invalid_capture_Person");
            byte status = Known(current.ReadPersonStatus(personPhysical), "current.capture_PersonE6");
            if (status is not (1 or 2 or 3 or 4) ||
                !Known(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(personPhysical)), "current.capture_Person15E"))
                throw Missing("branch.ordinary_status2_or3_or4_capture_prefix");
            if (!Known(current.ReadArmyUnitNativeValidity(winnerArmy), "current.capture_winner_Army_validity"))
                throw Missing("branch.capture_nonvalid_winner_Army");
            bool lastCityRuler = status == 1;
            if (lastCityRuler && (!allowLastCityRuler ||
                !PersonRulerCaptureBranchSource.IsLastCityRulerCurrent(current, personPhysical)))
                throw Missing("branch.ruler_requires_2F86F0_disposition");
            ushort id = Known(current.ReadPersonIdentity(personPhysical), "current.capture_Person10");
            var originalForce = current.ReadPersonForce(personPhysical) ?? throw Missing("current.capture_saved_Force");
            byte rawDistrict = Known(current.ReadPersonDistrictSlot(personPhysical), "current.capture_saved_DistrictE0");
            var originalDistrict = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
                ?? throw Missing("current.capture_saved_District_pointer");
            var effects = new List<string>();
            if (lastCityRuler)
            {
                state = PersonRulerCaptureBranchSource.WriteLastCityRulerTargetCurrent(state, current, personPhysical);
                effects.Add("2E1543.last_City_ruler_Person162_self_identity");
            }
            var associationEffects = new List<CampaignEffectRequest>();
            var changedGates = new HashSet<int>();
            var districtQueries = new ArmyReturnDistrictOperationQueries(new GovernorQueries(() => state, governorInputs),
                () => state, next => state = next, (person, assignment) =>
                {
                    if (assignment.NativeReturn is null) throw Missing(assignment.UnresolvedOwner ?? "capture_District_assignment");
                    if (assignment.Reports.Count == 0) return;
                    if (package is null) throw Missing("capture_District_assignment_report_owner");
                    state = package.ApplyPersonAssociationRemovalReports(state, current, person, assignment,
                        () => (package.ReadArmyDestructionSampler(state, current)
                            ?? throw Missing("capture_District_report_World450")).Day450, associationEffects);
                }, current, changedGates, package, operationDate);

            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("current.capture_PlayerForce32EFA0");
            int playerPhysical = Known(current.ReadForcePhysicalSlot(player), "current.capture_PlayerForce_ordinal");
            int originalPhysical = Known(current.ReadForcePhysicalSlot(originalForce), "current.capture_original_Force_ordinal");
            CommanderSelectionRemovalReceipt? candidatesReceipt = null;
            if (playerPhysical != 0 && playerPhysical == originalPhysical)
            {
                var candidates = CommanderSelectionSource.RemoveCurrentOfficer(state, current, personPhysical);
                state = candidates.State ?? throw Missing(candidates.UnresolvedOwner ?? "current.capture_29CCB0");
                candidatesReceipt = candidates.Receipt;
                effects.Add("2E155B.29CCB0_player_selected_Person_clear");
            }
            //These callbacks share this operation's latest world. Updating a
            //wrapper's separate state variable would lose the real refresh.
            var priorAdvisorReset = current.AssignmentAdvisorResetCallbacks;
            var priorAreaRemoval = current.AssignmentAreaOfficerRemoval;
            PersonAssignmentPrecallResult appointments;
            try
            {
                current.AssignmentAdvisorResetCallbacks = resetForce =>
                {
                    var reset = PersonAdvisorResetSource.ApplyWithinPersonCapture(state, current, pool, resetForce);
                    state = reset.State ?? throw Missing(reset.UnresolvedOwner ?? "current.capture_advisor_reset");
                    //Keep the real changed policy owners visible to later
                    //callbacks, rather than restoring a stale saved reader.
                    current.BaseForcePolicyEffectReader = (owner, effect) =>
                        ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, owner, effect, true).Value;
                    return true;
                };
                current.AssignmentAreaOfficerRemoval = (area, oldOfficer) =>
                {
                    var removal = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, oldOfficer);
                    if (removal.NativeReturn is null)
                        throw Missing(removal.UnresolvedOwner ?? "current.capture_area_officer_removal");
                    return removal.NativeReturn != 0;
                };
                appointments = PersonAssignmentPrecallSource.Apply29C430ForCapture(current, personPhysical);
                if (appointments.NativeReturn is null) throw Missing(appointments.UnresolvedOwner ?? "current.capture_29C430");
                state = PersonAssignmentPrecallInputs.PublishCurrent(state, current);
                //29C430 may set the player's advisor dirty bit even when
                //2E8160 later finds no department member to remove. Publish
                //that actual current write before the next saved-owner read.
                if (current.ReadWorldTransferServiceFlags() is uint serviceFlags &&
                    OwnedArmyReturnServiceInputs.ReadCurrent(state) != serviceFlags)
                {
                    var native = state.NativeArmySimulation ?? throw Missing("current.capture_service_flag_owner");
                    if (OwnedArmyReturnServiceInputs.ReadCurrent(state) is null)
                        throw Missing("current.capture_service_flag_knowledge");
                    state = state with { NativeArmySimulation = native with { Parameters = native.Parameters with
                    { Int32Values = new(native.Parameters.Int32Values)
                        { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)serviceFlags) } } } };
                }
            }
            finally
            {
                current.AssignmentAdvisorResetCallbacks = priorAdvisorReset;
                current.AssignmentAreaOfficerRemoval = priorAreaRemoval;
            }
            effects.Add("2E1578.29C430_appointments_and_2783B0");
            var departments = PersonCaptureForceDepartments.ApplyCurrent(state, current, originalForce, personPhysical);
            state = departments.State ?? throw Missing(departments.UnresolvedOwner ?? "current.capture_2E8160");
            effects.Add("2E1597.2E8160_saved_original_Force");
            //R8=0: native-invalid District really returns0. The valid ordinary
            //primary branch executes actual temporary lists and governors;
            //secondary/reselection branches are not substituted by a return.
            if (Known(current.ReadDistrictNativeValidity(originalDistrict), "current.capture_District_validity"))
                ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(originalDistrict, current,
                    districtQueries);
            effects.Add("2E15A5.2E9140_saved_original_District");
            state = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);

            var begin = PersonArmyCaptureCustodySource.BeginContext(state, current, personPhysical, allowLastCityRuler: lastCityRuler);
            state = begin.State ?? throw Missing(begin.UnresolvedOwner ?? "current.capture_status6");
            if (begin.Receipt!.RequiresDistrictLeaderRepair)
            {
                var at = current.ReadDistrictTableReference(begin.Receipt.DistrictForLeaderRepair is byte saved && saved < 52 ? saved : 0)
                    ?? throw Missing("capture_post_status2_District_pointer");
                if (Known(current.ReadDistrictNativeValidity(at), "capture_post_status2_District_validity"))
                    ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(at, current, districtQueries);
                state = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
                effects.Add("2E15D6.2E9140_post_status6_saved_District");
            }
            effects.Add("2E15B9.Office73_status6_168_zero");
            var prepared = PersonArmyCaptureCustodySource.PrepareWinnerArmy(state, current, personPhysical,
                winnerArmy, originalForce);
            var packet = prepared.Packet ?? throw Missing(prepared.UnresolvedOwner ?? "current.capture_winner_custody");
            PersonArmyCaptureCustodySource.ApplyContext(packet);
            state = PersonArmyCaptureCustodySource.PublishCurrent(state, packet);
            effects.Add("2E161F.winner_Army_home_E2_E4_saved_old_Force165");
            state = PersonReturnDistrictReorganizationSource.PublishCurrent(state, current, districtQueries);
            state = PersonReturnDistrictTransferSource.PublishAreaAssignments(state, current, [], changedGates);
            if (associationEffects.Count != 0)
                state = state with { CampaignEffectRequests = (state.CampaignEffectRequests
                    ?? throw Missing("capture_District_report_effect_owner")).Append(CampaignEffectSource.ForceAssociationRemoval,
                        state.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("capture_District_report_calendar"),
                        associationEffects.ToArray()) };
            return new(state, new(personPhysical, id, originalForce, originalDistrict, candidatesReceipt,
                appointments, departments.Receipt, packet.Receipt, effects.ToArray()), null);
        }
        catch (InvalidDataException missing) { return new(null, null, missing.Message); }
    }

    private sealed class GovernorQueries(Func<GameState> latest, OwnedArmyGovernorComparatorInputs inputs)
        : IArmyReturnGovernorCurrentQueries
    {
        public ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int physical) =>
            inputs.ReadCurrentComparatorFields(latest(), physical);
    }
    private static T Known<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal PersonArmyCaptureWholeResult ApplyOrdinaryWinnerArmyCapture(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int personPhysical,
        ArmyUnitReference winnerArmy, NativeMapSeasonDate? operationDate = null, bool allowLastCityRuler = false) => PersonArmyCaptureWholeSource.ApplyOrdinaryWinnerArmy(
            state, current, pool, _governorInputs, personPhysical, winnerArmy, new(false), this, operationDate, allowLastCityRuler);
}

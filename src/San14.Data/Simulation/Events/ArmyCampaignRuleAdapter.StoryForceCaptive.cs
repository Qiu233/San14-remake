using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class EventPersonMovementCurrent
    {
        /// <summary>330's actual no-Army2E0FC0 Force custody caller. It shares
        /// the established command/assignment/District writers, without the
        /// winner-Army kernel's status2..4 admission or an editor outer gate.</summary>
        internal GameState ApplyStoryForceCaptiveCurrent(int person, ForceReference winner)
        {
            storyImmediateRetirement = true;
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldArea = current.AssignmentAreaOfficerRemoval;
            var oldPolicy = current.BaseForcePolicyEffectReader;
            try
            {
                BindInputs();
                SplitForeignTravelGroup(person); //330 calls26C580 only, before2E0FC0.
                var originalForce = current.ReadPersonForce(person) ?? throw Missing("story_captive_original_Force");
                int rawDistrict = Need(current.ReadPersonDistrictSlot(person), "story_captive_original_District");
                var originalDistrict = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
                    ?? throw Missing("story_captive_original_District_pointer");
                ushort flags = Need(current.ReadArmyReturnPersonFlags196(person), "story_captive_Person_flags");
                if ((flags & 0x4000) != 0 && Need(current.ReadPersonStatus(person), "story_captive_special_status") != 5)
                {
                    FreeStoryPerson(person);
                }
                else
                {
                    //The wrapper has already rejected rulers and dead people.
                    //No optional history/announcement prefix is requested.
                    if (Need(current.ReadForceNativeValidity(originalForce), "story_captive_original_Force_player_validity") &&
                        rules.TalentForceIsPlayerCurrent(latest, current, originalForce))
                    {
                        var removed = CommanderSelectionSource.RemoveCurrentOfficer(latest, current, person);
                        latest = removed.State ?? throw Missing(removed.UnresolvedOwner ?? "story_captive_commander_selection");
                    }
                    var assignment = PersonAssignmentPrecallSource.Apply29C430ForCapture(current, person);
                    ReceiveAssignment(person, assignment);
                    latest = PersonAssignmentPrecallInputs.PublishCurrent(latest, current);
                    if (current.ReadWorldTransferServiceFlags() is uint serviceFlags &&
                        OwnedArmyReturnServiceInputs.ReadCurrent(latest) != serviceFlags)
                    {
                        var native = latest.NativeArmySimulation ?? throw Missing("story_captive_service_owner");
                        if (OwnedArmyReturnServiceInputs.ReadCurrent(latest) is null) throw Missing("story_captive_service_knowledge");
                        latest = latest with { NativeArmySimulation = native with { Parameters = native.Parameters with
                            { Int32Values = new(native.Parameters.Int32Values)
                                { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)serviceFlags) } } } };
                    }
                    //2E8160's gate reads the current flag after29C430.
                    if ((Need(current.ReadArmyReturnPersonFlags196(person), "story_captive_fresh_Person_flags") & 0x4000) == 0)
                    {
                        var departments = PersonCaptureForceDepartments.ApplyCurrent(latest, current, originalForce, person);
                        latest = departments.State ?? throw Missing(departments.UnresolvedOwner ?? "story_captive_departments");
                    }
                    var districtOperation = rules.CreateCaptureDistrictOperationQueries(current, () => latest,
                        state => latest = state, Date, effects);
                    if (Need(current.ReadDistrictNativeValidity(originalDistrict), "story_captive_old_District_validity"))
                        ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(originalDistrict, current, districtOperation);
                    latest = OwnedArmyReturnCurrentFields.PublishCurrent(latest, current);

                    var begun = PersonArmyCaptureCustodySource.BeginAfterCaptureCallbacksCurrent(latest, current, person);
                    latest = begun.State ?? throw Missing(begun.UnresolvedOwner ?? "story_captive_status6");
                    if (begun.Receipt!.RequiresDistrictLeaderRepair)
                    {
                        int saved = begun.Receipt.DistrictForLeaderRepair ?? throw Missing("story_captive_post_status_District");
                        var district = current.ReadDistrictTableReference(saved < 52 ? saved : 0)
                            ?? throw Missing("story_captive_post_status_District_pointer");
                        if (Need(current.ReadDistrictNativeValidity(district), "story_captive_post_status_District_validity"))
                            ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(district, current, districtOperation);
                        latest = OwnedArmyReturnCurrentFields.PublishCurrent(latest, current);
                    }
                    if (Need(current.ReadForceNativeValidity(winner), "story_captive_reached_winner_validity"))
                    {
                        var home = ForceHome(winner); //22EA10: fresh ruler's actual Home.
                        var city = CurrentGateMapQueries.FindCity(home, current); //22A940, not the original City.
                        current.WritePersonFirstLocation(person, Math.Min(Need(current.ReadBaseObjectLink(home), "story_captive_Home4E"), (ushort)61));
                        int cityId = Need(city.PhysicalSlot, "story_captive_current_City_identity");
                        current.WritePersonSecondLocation(person, (ushort)(cityId is >= 0 and <= 51 ? cityId : 0)); //241F20(kind5).
                    }
                    else
                        //The original later validity gate can fail after real
                        //callbacks. Its null Army/Person fallthrough copies E2.
                        current.WritePersonSecondLocation(person, Math.Min(Need(current.ReadPersonFirstLocation(person), "story_captive_fallback_Home"), (ushort)1061));
                    int count = Need(PersonArmyCaptureSource.ReadOwnedCityCount(current, originalForce), "story_captive_old_Force_city_count");
                    if (count > 1)
                        current.WriteFormerForceKnowledge(person, Math.Clamp(Need(originalForce.PhysicalSlot, "story_captive_old_Force_ordinal"), 0, 51));
                    int id = Need(current.ReadPersonIdentity(person), "story_captive_Person_identity");
                    latest = OfficerLocationSourceAdapter.PublishPosition(latest, id, current);
                    latest = current.PublishOfficerAftermath(latest, id);
                    latest = PersonReturnDistrictReorganizationSource.PublishCurrent(latest, current, districtOperation);
                    latest = PersonReturnDistrictTransferSource.PublishAreaAssignments(latest, current,
                        districtOperation.ChangedAreas, districtOperation.ChangedGates);
                }
                Publish();
                if (effects.Count != 0)
                    latest = latest with { CampaignEffectRequests = (latest.CampaignEffectRequests
                        ?? throw Missing("story_captive_effect_owner")).Append(CampaignEffectSource.ForceAssociationRemoval,
                            latest.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("story_captive_effect_calendar"), effects.ToArray()) };
                return latest;
            }
            finally
            {
                current.AssignmentAdvisorResetCallbacks = oldAdvisor;
                current.AssignmentAreaOfficerRemoval = oldArea;
                current.BaseForcePolicyEffectReader = oldPolicy;
            }
        }
    }
}

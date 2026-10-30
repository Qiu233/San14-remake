using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The same pending149/212 business opcode resumes without
    /// resolving selectors, rebuilding the original City list or advancing VM.</summary>
    internal (GameState State, int? NativeReturn) ResumeStoryForceTransferOuterCurrent(
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        Action<int, int>? recordJoined, AutomaticEmperorCaptureInputs? emperorInputs = null)
    {
        var outer = state.StoryForceTransferContinuation ?? throw StoryTerritoryMissing("Emperor_outer_owner");
        var working = ResumeForceTransferEmperorCurrent(state, current, pool, emperorInputs,
            suppressPrimary: false, eventPlaybackPresent: true);
        if (working.AutomaticEmperorCapture is not null) return (working, null);
        var effects = new List<CampaignEffectRequest>();
        var target = new BaseReference(BaseKind.City, outer.TargetCityPhysicalSlot);
        working = CompleteStoryFootholdTransferCurrent(working, current, target,
            outer.PreviousForcePhysicalSlot, effects, recordJoined);
        working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
        if (outer.Code == 149)
        {
            Flush();
            working = CompleteStoryFootholdRefreshCurrent(working, current);
        }
        else if (outer.Code == 212)
        {
            int person = outer.ExecutorPhysicalSlot ?? throw StoryTerritoryMissing("Emperor_list_executor");
            var destination = new DistrictReference(outer.DestinationDistrictPhysicalSlot
                ?? throw StoryTerritoryMissing("Emperor_list_destination"));
            for (int index = outer.CityIndex + 1; index < outer.CityPhysicalSlots.Length; index++)
            {
                target = new(BaseKind.City, outer.CityPhysicalSlots[index]);
                int previous = StoryTerritoryNeed(current.ReadBaseForceSlot(target), "Emperor_list_previous_Force");
                var date = working.NativeMapSeason?.ThirdDate ?? throw StoryTerritoryMissing("Emperor_list_date");
                working = ApplyForceBaseTransferCurrent(working, current, pool, target, destination, new(person), date, effects,
                    preserveResources: false, suppressMaintenance: false, fullCentralRadius: true,
                    transferDistrict: operation => TransferDistrictAdministrationBaseCurrent(current, target,
                        destination, operation, new(person)), unregisterFormerForce: true, automaticEmperorInputs: emperorInputs, allowAutomaticEmperorPause: true, eventPlaybackPresent: true);
                if (working.ForceTransferEmperorTail is not null)
                {
                    working = working with { StoryForceTransferContinuation = outer with
                        { TargetCityPhysicalSlot = outer.CityPhysicalSlots[index], PreviousForcePhysicalSlot = previous, CityIndex = index } };
                    Flush();
                    return (working, null);
                }
                working = CompleteStoryFootholdTransferCurrent(working, current, target, previous, effects, recordJoined);
                working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            }
            if (outer.ManagerPresent)
            {
                current.ReportBlockerState = new(true, 0);
                working = working with { CampaignEventEligibility = (working.CampaignEventEligibility
                    ?? throw StoryTerritoryMissing("Emperor_list_manager_owner")) with { HistoryRecordingSuppressed = false } };
            }
            Flush();
            working = MaintainEditedDeathArmyCommandsCurrent(working, current);
            var combat = TryRefreshNormalCombatEngagementsCurrent(working, current);
            working = combat.State ?? throw StoryTerritoryMissing(combat.UnresolvedOwner ?? "Emperor_list_combat_refresh");
            working = ApplyStoryFacilityAuraRefreshCurrent(working, current);
            var nearby = RefreshBaseNearbyArmiesCurrent(working, current);
            working = nearby.State ?? throw StoryTerritoryMissing(nearby.UnresolvedOwner ?? "Emperor_list_nearby_refresh");
            working = RefreshEditedEventRegionStatistics(working, current);
        }
        else throw StoryTerritoryMissing("Emperor_outer_opcode");
        working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
        working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
        working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
        working = working.WithArmyLifecycle((working.ReadArmyLifecycle() ?? throw StoryTerritoryMissing("Emperor_outer_lifecycle"))
            with { Indexes = pool.ProjectOwnedRoles() });
        working = working with { StoryForceTransferContinuation = null };
        Flush();
        return (working, 1);

        void Flush() => working = AppendForceTransferEmperorEffects(working, effects);
    }
}

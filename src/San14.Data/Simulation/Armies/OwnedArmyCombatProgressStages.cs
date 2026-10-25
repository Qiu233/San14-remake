using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Owns the ordinary Return manager0, finite appearance9/occupation11, Army stage13 and real19/20/21/25/26/29
    /// consumers. The caller still supplies every remaining reached stage;
    /// this adapter does not claim complete Base/Gate combat, transport or whole-world coverage.</summary>
    public IPlayableNativeArmyProgressStageSource CreateOrdinaryArmyCombatProgressStages(
        IPlayableNativeArmyProgressStageSource remainingStages,
        INormalArmyForceResultDefinitionQueries forceDefinitions)
    {
        ArgumentNullException.ThrowIfNull(remainingStages); ArgumentNullException.ThrowIfNull(forceDefinitions);
        return new OrdinaryArmyCombatStageSource(this, remainingStages, forceDefinitions);
    }

    private sealed class OrdinaryArmyCombatStageSource(ArmyCampaignRuleAdapter package,
        IPlayableNativeArmyProgressStageSource remaining,
        INormalArmyForceResultDefinitionQueries forceDefinitions) : IPlayableNativeArmyProgressStageSource
    {
        public IPlayableNativeArmyProgressStageRules BeginOperation() =>
            new OrdinaryArmyCombatStages(package, remaining.BeginOperation(), forceDefinitions);
    }

    private sealed class OrdinaryArmyCombatStages(ArmyCampaignRuleAdapter package,
        IPlayableNativeArmyProgressStageRules remaining,
        INormalArmyForceResultDefinitionQueries forceDefinitions) : ICampaignStageOperation
    {
        private NormalArmyCombatCurrentPhase? currentPhase;
        private readonly ArmyMapGroupingOwner mapGroups = package._mapArmyGrouping.Copy();
        private bool mapGroupsChanged;
        internal ArmyMapGroupingOwner CopyMapGroupsForTurn() => mapGroups.Copy();
        public NativeArmyProgressStageResolution? AdvanceStage(GameState state, NativeArmyProgressStageMoment moment)
        {
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Waiting)
            {
                if (moment.Boundary == NativeArmyProgressStageBoundary.StageEntry)
                {
                    var people = package.TryAdvanceOfficerRumorCounters(state, moment);
                    return people.State is { } completed ? new(completed, "army.progress.person-rumor-counters") : null;
                }
                var bases = package.TryAdvanceBaseStatusCounters(state, moment);
                if (bases.State is not { } afterBases) return null;
                var landmarks = package.TryAdvanceLandmarkInvestmentTimers(afterBases, moment);
                return landmarks.State is { } afterLandmarks ? new(afterLandmarks, "army.progress.base-and-landmark-counters") : null;
            }
            if (moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
                return remaining.AdvanceStage(state, moment);
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.DuelOpportunity)
                return moment.PreWriterThirdDate.Hour != 10
                    ? new(state, "army.progress.duel-not-due")
                    : new(package.AdvanceDuelOpportunity(state, moment), "army.progress.duel-opportunity");
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.ArmyMoraleFatigue)
            {
                var fatigue = package.TryAdvanceArmyMoraleFatigue(state, moment);
                return fatigue.State is { } continued ? new(continued, "army.progress.field-morale-fatigue") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.PersonnelReturn)
            {
                var returned = package.TryAdvancePersonReturnCommands(state, moment.PreWriterThirdDate);
                if (returned.AwaitingDiplomacyTaskId is not null)
                    throw new InvalidDataException("army.progress.diplomacy_requires_normal_period_host");
                return returned.State is { } continued
                    ? new(continued, "army.progress.personnel-return") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.MapMusic)
            {
                var music = package.TryAdvanceMapMusicSelection(state, moment);
                return music.State is { } continued ? new(continued, "army.progress.map-music-intent") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.SpecialArmyActions)
            {
                var actions = package.TryAdvanceSpecialArmyActions(state, moment);
                return actions.State is { } continued ? new(continued, "army.progress.false-report-actions") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.RestoreArmyCommands)
            {
                var restored = package.TryAdvanceArmyCommandRestoration(state, moment);
                if (restored.State is not { } continued)
                {
                    var failure = new InvalidDataException(restored.UnresolvedOwner ??
                        "army.progress.command-restoration.current-inputs");
                    failure.Data["RestorationLowerOwner"] = restored.UnresolvedOwner;
                    failure.Data["RestorationCalendarCycleOrdinal"] = moment.CalendarCycleOrdinal;
                    failure.Data["RestorationDate"] = moment.PreWriterThirdDate;
                    failure.Data["RestorationSchedulerStageIndex"] = moment.SchedulerStageIndex;
                    failure.Data["RestorationBoundary"] = moment.Boundary;
                    if (restored.FailureData is { } observed)
                        foreach (var field in observed) failure.Data[field.Key] = field.Value;
                    throw failure;
                }
                return new(continued, "army.progress.command-restoration");
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Appearance)
            {
                var appeared = package.TryAdvanceOrdinaryAppearance(state, moment);
                return appeared.State is { } continued
                    ? new(continued, "army.progress.ordinary-appearance") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Occupation)
            {
                var occupied = package.TryAdvanceOrdinaryOccupation(state, moment);
                if (occupied.State is not { } continued)
                    throw new InvalidDataException(occupied.UnresolvedOwner ?? "current.Army_occupation.unresolved_owner");
                return new(continued, "army.progress.ordinary-occupation");
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.ArmyNotifications)
            {
                var notices = package.TryAdvanceArmyProgressNotifications(state, moment);
                return notices.State is { } continued ? new(continued, "army.progress.army-notifications") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Demolition)
            {
                var demolition = package.TryAdvanceArmyDemolition(state, moment);
                return demolition.State is { } continued ? new(continued, "army.progress.demolition") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.MapObjectRetirement)
            {
                var retirement = package.TryAdvanceMapObjectRetirement(state, moment);
                return retirement.State is { } continued ? new(continued, "army.progress.map-object-retirement") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Combat)
            {
                if (moment.PreWriterThirdDate.Hour != 10)
                    return new(state, "army.progress.ordinary-combat-not-due");
                currentPhase = package.CaptureNormalArmyCombatPhase(state, moment);
                return currentPhase?.TryCompleteOrdinaryPhase(state, forceDefinitions) is { } completed
                    ? new(completed, "army.progress.ordinary-combat") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.TerrainDamage)
            {
                var terrain = package.TryAdvanceTerrainDamage(state, moment);
                return terrain.State is { } continued ? new(continued, "army.progress.terrain-damage") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.FireDamage)
            {
                var damage = package.TryAdvanceFireDamage(state, moment);
                return damage.State is { } completed ? new(completed, "army.progress.fire-damage") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.FacilityEffects)
            {
                var facilities = package.TryAdvanceFacilityEffects(state, moment);
                if (facilities.State is not { } completed)
                    throw new InvalidDataException(facilities.UnresolvedOwner ??
                        "army.progress.facility-effects.current-inputs");
                return new(completed, "army.progress.facility-effects-not-due");
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.FacilityRepair)
            {
                var repair = package.TryAdvanceArmyFacilityRepair(state, moment);
                return repair.State is { } completed
                    ? new(completed, "army.progress.facility-repair") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.FacilityConstruction)
            {
                var construction = package.TryAdvanceArmyFacilityConstruction(state, moment);
                return construction.State is { } completed
                    ? new(completed, "army.progress.facility-construction") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.PersonalityMaintenance)
            {
                var traits = package.TryAdvanceArmyPersonalityMaintenance(state, moment);
                return traits.State is { } completed
                    ? new(completed, "army.progress.personality-maintenance") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.BaseWoundedRecovery)
            {
                var recovered = package.TryAdvanceBaseWoundedRecovery(state, moment);
                return recovered.State is { } completed
                    ? new(completed, "army.progress.base-wounded-recovery") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.HexFire)
            {
                var fire = package.TryAdvanceOrdinaryHexFire(state, moment);
                return fire.State is { } completed
                    ? new(completed, "army.progress.ordinary-hex-fire") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.FacilityTraps)
            {
                var traps = package.TryAdvanceFacilityTraps(state, moment);
                return traps.State is { } completed ? new(completed, "army.progress.facility-traps") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.FireCommands)
            {
                var commands = package.TryAdvanceOrdinaryFireCommands(state, moment);
                return commands.State is { } completed ? new(completed, "army.progress.fire-commands") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Defeat)
            {
                var destroyed = new OwnedArmyDestructionStage(package).TryConsumeDeferredDefeatsWithGrouping(state,
                    currentPhase?.ReadOrdinaryCycleSampler(state), moment.PreWriterThirdDate, mapGroups);
                if (destroyed.State is not { } result) return null;
                if (destroyed.CityReceipts.Length != 0 || destroyed.BaseReceipts.Length != 0) mapGroupsChanged = true;
                var reports = destroyed.Receipts.Select(receipt => new NativeSubdayReport("field_destroyed",
                    result.NativeClock!.UpdateOrdinal, receipt.UnitId, receipt.ForceId, receipt.PhysicalSlot));
                result = result with { NativeSubdayReports = [.. result.NativeSubdayReports ?? [], .. reports] };
                return new(result, "army.progress.deferred-defeat");
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.ReleaseRetired)
            {
                currentPhase = null;
                var reset = OwnedArmyLifecycleAuxiliaries.ConsumePending(state, package._definitions, physical =>
                {
                    mapGroups.ResetReceiver(physical);
                    mapGroupsChanged = true;
                });
                if (reset is null) return null;
                var refreshed = package.TryRefreshBaseNearbyArmies(reset);
                return refreshed.State is { } completed
                    ? new(completed, "army.progress.release-retired-and-refresh-base-neighbors") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.MapArmyGrouping)
            {
                // TC44DCB7: only pre-writer half-hour11 (22:00). Use this
                // cycle's post-stage26/27/28 world, never a calendar catch-up.
                if (moment.PreWriterThirdDate.Hour != 22)
                    return new(state, "army.progress.map-groups-not-due");
                if (!package.RecomputeMapArmyGroups(state, mapGroups).Applied) return null;
                mapGroupsChanged = true;
                return new(state, "army.progress.map-groups");
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.MaintainArmyCommands)
            {
                var maintained = package.TryAdvanceArmyCommandMaintenance(state, moment);
                return maintained.State is { } completed ? new(completed, "army.progress.command-maintenance") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.OfficerGrowthNotifications)
            {
                var growth = package.TryAdvanceOfficerGrowthNotifications(state, moment);
                return growth.State is { } completed
                    ? new(completed, "army.progress.officer-growth-notifications") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.TaskSceneFinalization)
            {
                // The pure TaskScene pointer getter has no side effects. Known
                // signed WorldBC=-1 makes its active conjunction false regardless
                // of service presence; missing or active users stay unresolved.
                return state.CampaignEventWorld?.CurrentUserIndex == -1
                    ? new(state, "army.progress.task-scene-inactive") : null;
            }
            return remaining.AdvanceStage(state, moment);
        }
        public CampaignGroupingCheckpoint CaptureCheckpoint()
        {
            if (currentPhase is not null || remaining is not IStatelessNativeProgressStageRules)
                throw new InvalidDataException("Current phase or remaining provider cannot be suspended at stage2.");
            return new(mapGroups.Export(), mapGroupsChanged);
        }
        public void RestoreCheckpoint(CampaignGroupingCheckpoint checkpoint)
        {
            if (remaining is not IStatelessNativeProgressStageRules) throw new InvalidDataException("Remaining provider has no saved continuation contract.");
            mapGroups.Import(checkpoint.Roots); mapGroupsChanged = checkpoint.Changed; currentPhase = null;
        }
        public IReadOnlyList<int>? ReadCompleteWaitingArmyOrder(GameState state, NativeArmyProgressStageMoment moment) =>
            package.ReadOrdinaryWaitingArmyOrder(state);
        public void PublishDerivedState(GameState committedState)
        {
            remaining.PublishDerivedState(committedState);
            if (mapGroupsChanged) package.PublishMapArmyGroups(mapGroups);
            mapGroupsChanged = false;
        }
    }
}

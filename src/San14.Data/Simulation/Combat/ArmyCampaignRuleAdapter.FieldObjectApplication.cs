using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal NormalArmyApplicationBodyResult ApplyTrapTargetNumbers(GameState state, SourceWorldContext current,
        int target, int amount, NativeArmyProgressStageMoment moment,
        Func<GameState, UnitState, int, (GameState State, ArmyMoraleAdjustmentReceipt Receipt)> morale) =>
        ApplyEnvironmentTargetNumbers(state, current, target, amount, moment, morale);

    internal NormalArmyApplicationBodyResult ApplyEnvironmentTargetNumbers(GameState state, SourceWorldContext current,
        int target, int amount, NativeArmyProgressStageMoment moment,
        Func<GameState, UnitState, int, (GameState State, ArmyMoraleAdjustmentReceipt Receipt)> morale) =>
        TryApplyNativeDamageTargetBody(state, null, target, amount, _ => Context(current), current, morale,
            () => ReadCurrentEnvironmentSampler(state, moment, moment.SchedulerStageIndex));

    internal BattleRemakeCurrentFieldObjectTargetReceipt TrapTargetStats(GameState state,
        SourceWorldContext current, UnitState target) => Context(current)
            .ResolveFieldObjectTarget(state, target, _battleSeed, _regionSeed) ?? throw Missing("target_stats");
    internal BattleDamageSourceParameters TrapDamageParameters(GameState state, SourceWorldContext current) =>
        Context(current).ReadFieldObjectDamageParameters(state) ?? throw Missing("damage_parameters");
    internal ArmyMobilityCurrentProducer TrapMobility() => new(this, _mobilitySeed, _map, _terrain, _catalog);
    internal void BindTrapDestructionInputs(GameState state, SourceWorldContext current) =>
        BindActorlessDestructionInputs(state, current);

    internal void BindActorlessDestructionInputs(GameState state, SourceWorldContext current)
    {
        //Only reached by the actual Full one-request259360 consumer. Keep
        //the operation's existing pool, aliases, histories and Person manager.
        _personDestructionInputs.BindCurrent(state, current);
        _personRelationInputs.BindCurrent(state, current);
        _force164Inputs.BindCurrent(state, current);
        _moraleWaveInputs.BindCurrent(state, current);
        PersonArmyCaptureInputs.BindCurrent(state, current);
        RulerCaptureReleaseSource.BindCurrent(state, current);
        _personAssignmentPrecallInputs.BindCurrent(state, current);
        current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        _baseCapacityInputs.BindCurrent(state, current);
        PersonAdvisorResetSource.BindCurrent(state, current);
        ForceUnregisterInputs.BindCurrent(state, current);
        OwnedArmyReturnServiceInputs.BindCurrent(state, current);
        current.ArmyReturnTransferMode = new(false, null);
        current.TransferService ??= new();
    }
    internal TacticSourceSamplerWorld TrapSampler(GameState state, NativeArmyProgressStageMoment moment) =>
        ReadCurrentEnvironmentSampler(state, moment, (byte)NativeArmyProgressStage.FacilityTraps);

    /// <summary>One current calendar algorithm for the real stage22/23/24
    /// consumers. The caller supplies its actual expected stage; no rewritten
    /// moment or hour10 assumption supplies a different clock identity.</summary>
    internal TacticSourceSamplerWorld ReadCurrentEnvironmentSampler(GameState state,
        NativeArmyProgressStageMoment moment, byte expectedStage)
    {
        var season = state.NativeMapSeason ?? throw Missing("sampler_saved_calendar");
        var clock = state.NativeClock ?? throw Missing("sampler_current_clock");
        var epoch = state.NativeArmySimulation?.World.SamplerEpoch ?? throw Missing("sampler_epoch");
        if (expectedStage is not (22 or 23 or 24) || moment.SchedulerStageIndex != expectedStage ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.CycleWithinAtomicBlock is < 0 or >= NativeArmyProgressAtomicRuntime.CyclesPerSaveBlock ||
            moment.CalendarCycleOrdinal != checked(season.AdvancedCalendarSteps + moment.CycleWithinAtomicBlock) ||
            clock.Revision != season.NativeClockRevision || epoch.NativeClockRevision != clock.Revision ||
            epoch.SourceScenarioUri != season.SourceScenarioUri || epoch.SourceScenarioSha256 != season.SourceScenarioSha256 ||
            epoch.SourceWorldSha256 != season.SourceWorldSha256 ||
            state.ContentHashes.GetValueOrDefault(epoch.SourceScenarioUri) != epoch.SourceScenarioSha256)
            throw Missing("sampler_current_identity");
        MapSeasonRuntime.ValidateOwned(season);
        var date = season.ThirdDate;
        long days = season.AdvancedDays, waiting = 0;
        //Stage2 has already run in each reached cycle. Count its actual
        //waiting callbacks instead of the old hour10-only getter assumption.
        for (int cycle = 0; cycle <= moment.CycleWithinAtomicBlock; cycle++)
        {
            if (NativeArmyProgressAtomicRuntime.IsArmyWaitingDue(date)) waiting++;
            if (cycle == moment.CycleWithinAtomicBlock) break;
            var step = MapSeasonRuntime.StepCalendarWriter(date); date = step.Date;
            if (step.DayAdvanced) days++;
        }
        if (date != moment.PreWriterThirdDate || clock.UpdateOrdinal != checked(season.ProcessedNativeOrdinal + waiting))
            throw Missing("sampler_reached_clock");
        return new(unchecked(epoch.Day450 + (int)days), epoch.Salt45C);
    }
    internal int TrapAbility(GameState state, SourceWorldContext current, int officer, int dimension) =>
        new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language))
            .ResolveDimension(state, officer, dimension) ?? throw Missing("effective_ability" + dimension);
    internal GameState IgniteTrapHex(GameState state, SourceWorldContext current, int hex, int duration,
        NativeArmyProgressStageMoment moment)
    {
        var announcements = new List<ArmyFireAnnouncement>(state.PendingArmyFireAnnouncements);
        var reports = new List<PeriodReportEntry>(state.PendingCommandReports);
        state = IgniteCurrentHex(state, current, hex, duration, false, moment, [], announcements, reports);
        return BaseCaptureCurrentInputs.PublishCurrent(state, current) with
        { PendingArmyFireAnnouncements = announcements.ToArray(), PendingCommandReports = reports.ToArray() };
    }
    internal GameState RemoveTriggeredTrap(GameState state, SourceWorldContext current,
        ObjectReference facility, List<CampaignEffectRequest> sink) =>
        RemoveMapObjectWithoutActor(state, current, facility, sink);

    private GameState RemoveMapObjectWithoutActor(GameState state, SourceWorldContext current,
        ObjectReference facility, List<CampaignEffectRequest> sink)
    {
        var removal = ApplyDemolitionNumbers(state, current, facility, new ArmyUnitReference(null));
        if (removal is null) return state;
        string definition = MapObjectSourceDirectory.DefinitionKey(removal.DefinitionSlot);
        sink.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionVisual,
            removal.Receiver, removal.HexId, definition) });
        sink.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionSound,
            removal.Receiver, removal.HexId, definition) });
        //294FD0(report1) reaches visibility before its known-null actor gate.
        //Category4 returns before that read, preserving old trap laziness.
        if (removal.Category != 4)
            _ = ReadDemolitionAreaVisibility(removal.State, current, removal.AreaId);
        return removal.State;
    }
}

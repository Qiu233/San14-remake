using System.Text.Json.Serialization;
namespace San14.Core;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CampaignNormalPeriodIntent(PeriodDate StartDate, int StartPeriodNumber,
    int StartingUpdatesIntoPeriod);

public sealed record CampaignProgressRequest(long StartCalendarCycleOrdinal, int BlockCount)
{
    // Missing in old Session3 metadata means BlocksOnly. Present constructor
    // members remain required by the existing session JSON reader.
    public CampaignNormalPeriodIntent? NormalPeriod { get; init; }

    public void ValidateNormalPeriod(GameState committed)
    {
        if (NormalPeriod is not { } intent) return;
        if (intent.StartDate != committed.Date || intent.StartPeriodNumber != committed.PeriodNumber ||
            intent.StartDate.Month is < 1 or > 12 || intent.StartDate.Day is not (1 or 11 or 21) ||
            intent.StartPeriodNumber < 0 || intent.StartingUpdatesIntoPeriod is < 0 or >= 40 ||
            committed.NativeClock is not { } clock || clock.UpdatesIntoPeriod != intent.StartingUpdatesIntoPeriod ||
            BlockCount != 40 - intent.StartingUpdatesIntoPeriod ||
            committed.NativeMapSeason is not { } season || StartCalendarCycleOrdinal != season.AdvancedCalendarSteps)
            throw new InvalidDataException("normal.period.intent.start_identity");
    }
}
public sealed record CampaignOperationWorldContext(NativeMapSeasonState BlockStartSeason,
    NativeSubdayClock BlockStartClock, NativeArmyProgressStageMoment Moment)
{
    public void Validate(GameState state)
    {
        if (Moment?.SchedulerStageIndex == 6)
        {
            int cycle = Moment.CycleWithinAtomicBlock;
            int waited = cycle == 2 ? 1 : 0;
            if (BlockStartSeason is null || BlockStartClock is null || cycle is < 0 or > 2 ||
                Moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
                state.NativeMapSeason != BlockStartSeason || state.NativeClock is not { } actual ||
                actual.UpdateOrdinal != BlockStartClock.UpdateOrdinal + waited ||
                actual.UpdatesIntoPeriod != BlockStartClock.UpdatesIntoPeriod + waited ||
                actual.AnchorDate != BlockStartClock.AnchorDate || actual.Revision != BlockStartClock.Revision ||
                Moment.CalendarCycleOrdinal != BlockStartSeason.AdvancedCalendarSteps + cycle ||
                BlockStartSeason.ProcessedNativeOrdinal != BlockStartClock.UpdateOrdinal ||
                BlockStartSeason.ThirdDate.Hour % 6 != 0 ||
                Moment.PreWriterThirdDate != BlockStartSeason.ThirdDate with { Hour = checked((sbyte)(BlockStartSeason.ThirdDate.Hour + cycle * 2)) })
                throw new InvalidDataException("Manual tactic world is outside its exact stage6 cycle boundary.");
            return;
        }
        if (BlockStartSeason is null || BlockStartClock is null || Moment is null ||
            Moment.CycleWithinAtomicBlock != 2 || Moment.SchedulerStageIndex is not (2 or 13) ||
            (Moment.SchedulerStageIndex == 2 && Moment.Boundary != NativeArmyProgressStageBoundary.AfterArmyWaiting) ||
            (Moment.SchedulerStageIndex == 13 && Moment.Boundary != NativeArmyProgressStageBoundary.StageEntry) ||
            state.NativeMapSeason != BlockStartSeason || state.NativeClock is not { } clock ||
            clock.UpdateOrdinal != BlockStartClock.UpdateOrdinal + 1 ||
            clock.UpdatesIntoPeriod != BlockStartClock.UpdatesIntoPeriod + 1 ||
            clock.AnchorDate != BlockStartClock.AnchorDate || clock.Revision != BlockStartClock.Revision ||
            Moment.CalendarCycleOrdinal != BlockStartSeason.AdvancedCalendarSteps + 2 ||
            BlockStartSeason.ProcessedNativeOrdinal != BlockStartClock.UpdateOrdinal ||
            BlockStartSeason.ThirdDate.Hour % 6 != 0 ||
            Moment.PreWriterThirdDate.Year != BlockStartSeason.ThirdDate.Year ||
            Moment.PreWriterThirdDate.Month != BlockStartSeason.ThirdDate.Month ||
            Moment.PreWriterThirdDate.Day != BlockStartSeason.ThirdDate.Day ||
            Moment.PreWriterThirdDate.Hour != BlockStartSeason.ThirdDate.Hour + 4 ||
            Moment.PreWriterThirdDate.Hour is not (4 or 10 or 16 or 22))
            throw new InvalidDataException("In-flight world does not match its reached stage boundary.");
    }
}
public sealed record SavedArmyCombatCapture(CampaignOperationWorldContext Context, GameState State);
public sealed record CampaignGroupingCheckpoint(ushort?[] Roots, bool Changed);
public enum LandmarkStoryPhase { Event, CameraReturn, RewardNotice }
public sealed record LandmarkStoryCheckpoint(int[] Registry, int NextIndex, int CurrentLandmarkId,
    int EventId, CampaignEventOccurrenceDate Date, int EventStepCount, LandmarkStoryPhase Phase);
public sealed record CampaignAchievementDate(ushort Year, byte Month, byte Day, byte Hour, byte Reserved);
public sealed record CampaignAchievementWrite(int Id, CampaignAchievementDate Date);
public sealed record CampaignAchievementClockRead(int Id, int YearsSince1900, int ZeroBasedMonth, int Day, int Hour);
/// <summary>Monotone uncommitted service writes; loading never restores a historical global profile.</summary>
public sealed record CampaignServiceChanges(int[] Events, CampaignAchievementWrite[] Achievements, bool Dirty = false)
{
    [JsonRequired] public CampaignAchievementClockRead[] ClockReads { get; init; } = [];
    public static CampaignServiceChanges Empty => new([], []);
    public CampaignServiceChanges Copy() => this with { Events = Events.ToArray(), Achievements = Achievements.ToArray(), ClockReads = ClockReads.ToArray() };
    internal void Validate()
    {
        if (Events is null || Achievements is null || ClockReads is null || Events.Any(id => id is < 0 or >= 1024) ||
            Achievements.Any(a => a is null || a.Id is < 0 or > 56 || a.Date is null || a.Date.Month is < 1 or > 12 || a.Date.Day is < 1 or > 31 || a.Date.Hour > 23) ||
            ClockReads.Any(c => c is null || c.Id is < 0 or > 56) || ClockReads.Select(c => c.Id).Distinct().Count() != ClockReads.Length)
            throw new InvalidDataException("Campaign service continuation is incomplete.");
    }
}
public sealed record CampaignProgressOperation(string Revision, CampaignProgressRequest Request, int BlockIndex,
    GameState Candidate, CampaignOperationWorldContext Context, NativeNormalArmyCombatCalendarState Calendar,
    LandmarkStoryCheckpoint Landmark, CampaignStoryRequest Story, long LastStorySequence,
    SavedArmyCombatCapture[] Captures, CampaignGroupingCheckpoint Grouping, CampaignServiceChanges ServiceChanges)
{
    [JsonRequired] public bool ContinueAfterPresentation { get; init; }
    [JsonRequired] public bool LandmarkManagerCompleted { get; init; }
    [JsonRequired] public bool CurrentLandmarkCompleted { get; init; }
    [JsonRequired] public bool CurrentLandmarkServicePrepared { get; init; }
    public CampaignProgressOperation Copy() => this with
    {
        Candidate = GameStateCopy.Copy(Candidate),
        Landmark = Landmark with { Registry = Landmark.Registry.ToArray() },
        Story = Story.Copy(),
        Captures = Captures.Select(c => c with { State = GameStateCopy.Copy(c.State) }).ToArray(),
        Grouping = Grouping with { Roots = Grouping.Roots.ToArray() },
        ServiceChanges = ServiceChanges.Copy()
    };
    public void Validate(GameState committed)
    {
        if (Request is null || Context is null || Calendar is null || Landmark is null || Story is null ||
            Captures is null || Grouping is null || ServiceChanges is null || ServiceChanges.Events is null || ServiceChanges.Achievements is null || ServiceChanges.ClockReads is null)
            throw new InvalidDataException("Saved campaign progress owner is missing.");
        GameEngine.ValidateWorldIdentity(committed, Candidate);
        Request.ValidateNormalPeriod(committed);
        Context.Validate(Candidate);
        Story.Validate();
        if (string.IsNullOrWhiteSpace(Revision) || Request.BlockCount is < 1 or > 40 ||
            BlockIndex < 0 || BlockIndex >= Request.BlockCount ||
            Context.BlockStartSeason.AdvancedCalendarSteps != Request.StartCalendarCycleOrdinal + 3L * BlockIndex ||
            committed.NativeMapSeason is null || Request.StartCalendarCycleOrdinal != committed.NativeMapSeason.AdvancedCalendarSteps ||
            Calendar.NextCalendarCycleOrdinal != Context.BlockStartSeason.AdvancedCalendarSteps ||
            Context.Moment.SchedulerStageIndex != 2 ||
            LastStorySequence != Story.Sequence || Story.NextStep > Story.Steps.Length ||
            (!ContinueAfterPresentation && Story.NextStep == Story.Steps.Length) ||
            (LandmarkManagerCompleted && !CurrentLandmarkCompleted) ||
            (CurrentLandmarkCompleted && !CurrentLandmarkServicePrepared) ||
            (CurrentLandmarkServicePrepared && (!ContinueAfterPresentation || Story.NextStep != Story.Steps.Length || Landmark.Phase != LandmarkStoryPhase.RewardNotice)) ||
            Landmark.Registry is null || Landmark.NextIndex < 1 || Landmark.NextIndex > Landmark.Registry.Length ||
            (!CurrentLandmarkCompleted && Landmark.Registry[Landmark.NextIndex-1] != Landmark.CurrentLandmarkId) ||
            Landmark.EventStepCount < 0 || Landmark.EventStepCount > Story.Steps.Length || !Enum.IsDefined(Landmark.Phase) ||
            (Landmark.Phase == LandmarkStoryPhase.Event ? Story.NextStep > Landmark.EventStepCount ||
                !ContinueAfterPresentation && Story.NextStep == Landmark.EventStepCount : Story.NextStep < Landmark.EventStepCount) ||
            Landmark.EventId != Landmark.CurrentLandmarkId + 338 || Landmark.EventId != Story.Source.EventId ||
            Grouping.Roots is not { Length: 1503 } || Grouping.Roots.Any(r => r > 500) ||
            Captures is null || ServiceChanges.Events.Any(id => id is < 0 or >= 1024) ||
            ServiceChanges.Achievements.Any(a => a.Id is < 0 or > 56 || a.Date.Month is < 1 or > 12 ||
                a.Date.Day is < 1 or > 31 || a.Date.Hour > 23) ||
            ServiceChanges.ClockReads.Any(c => c is null || c.Id is < 0 or > 56) ||
            ServiceChanges.ClockReads.Select(c => c.Id).Distinct().Count() != ServiceChanges.ClockReads.Length)
            throw new InvalidDataException("Saved campaign progress continuation is incomplete.");
        long previousCapture = Request.StartCalendarCycleOrdinal - 1;
        foreach (var capture in Captures)
        {
            if (capture is null || capture.Context.Moment.SchedulerStageIndex != 13 ||
                capture.Context.Moment.CalendarCycleOrdinal <= previousCapture ||
                capture.Context.Moment.CalendarCycleOrdinal >= Context.Moment.CalendarCycleOrdinal)
                throw new InvalidDataException("Saved combat captures do not precede the pending story in order.");
            capture.Context.Validate(capture.State); GameEngine.ValidateWorldIdentity(committed, capture.State);
            previousCapture = capture.Context.Moment.CalendarCycleOrdinal;
        }
    }
}
public enum CampaignProgressOutcome { Committed, AwaitingStory, Unresolved, AwaitingManualTactic }
public sealed record CampaignProgressTransition(CampaignProgressOutcome Outcome, GameState? State,
    CampaignProgressOperation? Pending, string? UnresolvedOwner)
{
    public ManualTacticProgressOperation? ManualTactic { get; init; }
    public CampaignServiceChanges ServiceChanges { get; init; } = CampaignServiceChanges.Empty;
    public CampaignGroupingCheckpoint? Grouping { get; init; }
    public long LastStorySequence { get; init; }
}
public interface ICampaignProgressRules
{
    CampaignProgressTransition Begin(GameState candidate, CampaignProgressRequest request, long firstStorySequence);
    CampaignProgressTransition Resume(CampaignProgressOperation pending, CampaignStoryStepReceipt receipt);
    CampaignProgressTransition Retry(CampaignProgressOperation pending);
    void Publish(GameState committed, CampaignServiceChanges changes, CampaignGroupingCheckpoint? grouping);
}
public interface ICampaignOperationSaveCodec : ISaveCodec
{
    byte[] EncodeOperationWorld(GameState state, CampaignOperationWorldContext context);
    GameState DecodeOperationWorld(ReadOnlySpan<byte> bytes, CampaignOperationWorldContext context);
}

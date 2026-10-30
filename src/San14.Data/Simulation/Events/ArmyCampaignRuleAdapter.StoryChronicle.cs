using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed record StoryChronicleTransition(GameState State, CampaignStoryChronicleProgress Progress);

    //83C100 calls83D9F0 before the first business instruction for ordinary
    //events, retaining the actual returned slot throughout real UI waits.
    private StoryChronicleTransition BeginStoryChronicleCurrent(GameState state, SourceWorldContext current, int eventId)
    {
        bool deferred = CampaignStoryChronicleSource.IsDeferred(eventId);
        var progress = new CampaignStoryChronicleProgress(deferred, false, null);
        if (deferred) return new(state, progress);
        //The invocation constructor resets both selectors; no condition or
        //business instruction has yet had a chance to select a Force or City.
        var written = WriteStoryChronicleCurrent(state, current, eventId,
            new(new ForceReference(null), new BaseReference(BaseKind.City, null)));
        return new(written.State, progress with { WrittenEntry = written.Entry });
    }

    private StoryChronicleTransition FinishStoryChronicleCurrent(GameState state, SourceWorldContext current,
        int eventId, CampaignStoryExecutionCursor? cursor, CampaignStoryChronicleProgress? progress)
    {
        if (progress is null || progress.SourceTimeUnavailable)
            throw StoryChronicleMissing("source_time_unavailable");
        if (progress.Completed) throw StoryChronicleMissing("already_completed");
        if (progress.Deferred)
        {
            //Force selector-5 is condition bank slot4; City selector-9 is
            //the first independent selected City pointer. Keep reset vs
            //unknown distinct and never substitute an actor or current ruler.
            if (cursor is null || !cursor.IsCompleted) throw StoryChronicleMissing("completed_execution_cursor");
            var force = new ForceReference(cursor.ConditionForcePhysicalSlots[4]);
            BaseReference? city = cursor.SelectedCityPhysicalSlots is { } cities && cities[0] is { } selected
                ? new(BaseKind.City, selected.PhysicalSlot) : null;
            var written = WriteStoryChronicleCurrent(state, current, eventId, new(force, city));
            state = written.State;
            progress = progress with { WrittenEntry = written.Entry };
        }
        if (progress.WrittenEntry is { } entry)
            state = new CampaignChronicleWriter(HistoryWriter).RefreshStorySnapshots(state, current, entry);
        return new(state, progress with { Completed = true });
    }

    private CampaignChronicleWriteResult WriteStoryChronicleCurrent(GameState state, SourceWorldContext current,
        int eventId, CampaignStoryChronicleInputs inputs)
    {
        BindCurrentCampaignEventQueries(state, current);
        int day = (ReadArmyDestructionSampler(state, current)
            ?? throw StoryChronicleMissing("World450")).Day450;
        var commands = CampaignStoryChronicleSource.Prepare(state, current, eventId, day, inputs);
        var writer = new CampaignChronicleWriter(HistoryWriter);
        CampaignChronicleEntryReference? entry = null;
        foreach (var command in commands)
        {
            //The original helper passes snapshot=false. Its business tail
            //refreshes only the actual returned slot and qualifying predecessor.
            var written = writer.WriteTracked(state, current, command, false,
                ReadApplicationGameplay()?.UserSelectionServicePresent);
            state = written.State;
            entry = written.Entry;
        }
        return new(state, entry);
    }

    private static InvalidDataException StoryChronicleMissing(string field) =>
        new("campaign-story-chronicle." + field);
}

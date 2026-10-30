using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ManualEditedEventCandidate(int Slot, string Name, byte Category,
    bool CanExecute, S14ConditionStatus? Status, string? UnresolvedOwner)
{
    public bool AlreadyOccurred { get; init; }
}

/// <summary>One actual application title-library binding for manual Strategy
/// playback. Core owns the saved cursor; this adapter owns no parallel World.</summary>
public sealed class EditedEventPlaybackRules(ArmyCampaignRuleAdapter rules,
    EditedEventStore store, IS14AudioEventExecutionState audio) : IEditedEventPlaybackRules
{
    public IReadOnlyList<ManualEditedEventCandidate> ReadManualCandidates(GameState state) =>
        rules.ReadManualEditedEventCandidates(state, store);

    public AreaAdministrationQuery<bool> ReadManualConfirmationRequired(GameState state) =>
        rules.QueryManualEditedEventConfirmation(state);

    public EditedEventPlaybackPreparationResult PrepareEditedEventPlayback(GameState candidate,
        int slot, long sequence)
    {
        try
        {
            var selected = ReadManualCandidates(candidate).FirstOrDefault(row => row.Slot == slot);
            if (selected is null) throw Missing("manual_slot_not_enabled");
            if (!selected.CanExecute) throw new InvalidDataException(selected.UnresolvedOwner ??
                "edited_event.manual_condition_not_satisfied");
            var story = rules.PrepareEditedEventStoryCurrent(candidate, store.Compile(slot), sequence, audio);
            uint flags = OwnedArmyReturnServiceInputs.ReadCurrent(candidate)
                ?? throw Missing("manual_start_world_flags");
            var simulation = candidate.NativeArmySimulation ?? throw Missing("manual_start_simulation");
            //39B4E0 records the real manual-use flag before playback. Reuse
            //the existing saved current owner rather than introducing a bank.
            var started = candidate with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = new(simulation.Parameters.Int32Values)
                { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)(flags | 0x200000u)) } } } };
            return new(story, null) { State = started };
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    public CampaignStoryPreparationResult PrepareEditedEventPlaybackStep(GameState candidate,
        EditedEventPlayback playback)
    {
        try
        {
            RequireSource(playback);
            return rules.PrepareEditedEventStoryStep(candidate, playback.Story, store.Current);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    public EditedEventPlaybackCompletionResult CompleteEditedEventPlayback(GameState candidate,
        EditedEventPlayback playback)
    {
        try
        {
            var definition = RequireSource(playback);
            if (playback.Phase != EditedEventPlaybackPhase.Results ||
                playback.Story.NextStep != playback.Story.Steps.Length)
                throw Missing("actual_story_not_finished");
            //The edited result page describes the actual definition. Normal
            //event-manager merit/alliance lists are not this page's input.
            //Occurrence is written only after its real confirmation in Core.
            return rules.ApplyEditedEventResultsAndPrepareSummaryCurrent(candidate, definition, playback.Story.SelectionContext);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private EditedEventCompiledDefinition RequireSource(EditedEventPlayback playback)
    {
        if (playback.Source.ResourceUri != store.ResourceUri ||
            playback.Source.ResourceSha256 != store.Revision)
            throw Missing("title_library_revision_changed");
        var value = store.Compile(playback.Slot);
        if (value.Source != playback.Source || playback.Story.Source != value.Source ||
            playback.Story.ContentRevision != value.ContentProgram.Revision)
            throw Missing("compiled_story_identity");
        return value;
    }

    private static InvalidDataException Missing(string owner) => new("edited_event." + owner);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>39B4E0 asks Game28D6 only when3342B0 still permits the
    /// application collection service. Its existing predicate reads the same
    /// actual saved flags and configured tutorial service, with no World copy.</summary>
    public AreaAdministrationQuery<bool> QueryManualEditedEventConfirmation(GameState state)
    {
        try
        {
            var query = new EditedEventConfirmationCurrent(this, state);
            return AreaAdministrationQuery<bool>.Known(TransferServiceEligibility.IsEligible(query));
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<bool>.Unknown(error.Message); }
    }

    private sealed class EditedEventConfirmationCurrent(ArmyCampaignRuleAdapter package,
        GameState state) : ITransferServiceEligibilityQueries
    {
        public uint? ReadWorldTransferServiceFlags() => OwnedArmyReturnServiceInputs.ReadCurrent(state);
        public TransferModeState? ReadTransferModeState() => package.ReadApplicationTransferModeCurrent();
    }

    /// <summary>39C630/399450 prepare one enabled library roster. A single
    /// detached current is shared by its at most100 condition evaluations.</summary>
    public IReadOnlyList<ManualEditedEventCandidate> ReadManualEditedEventCandidates(
        GameState state, EditedEventStore store)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(store);
        var enabled = store.Current.Events.Where(item => item.Enabled).OrderBy(item => item.Slot).ToArray();
        if (enabled.Length == 0) return [];
        SourceWorldContext? current = null;
        string? currentMissing = null;
        try { current = CaptureCurrentCampaignEventQueries(state); }
        catch (InvalidDataException error) { currentMissing = error.Message; }
        return Array.AsReadOnly(enabled.Select(Read).ToArray());

        ManualEditedEventCandidate Read(EditedEventDefinition definition)
        {
            try
            {
                var history = state.CampaignEvents ?? throw new InvalidDataException("edited_event.world_occurrence_owner");
                bool occurred = history.Occurred[definition.OccurrenceId];
                if (occurred && !definition.Repeat)
                    return new(definition.Slot, definition.Name, definition.Category, false, null, null)
                        { AlreadyOccurred = true };
                if (current is null) throw new InvalidDataException(currentMissing ?? "edited_event.current");
                var compiled = store.Compile(definition.Slot);
                var condition = compiled.EvaluateConditions(new S14PlayerEventConditionRuntime(current,
                    _ => ReadOrdinaryEventDispatchInputs(state), current));
                string? unavailable = compiled.Results.Operations.Select(operation =>
                    EditedEventResultExecution.UnresolvedOwner(operation.Code)).FirstOrDefault(owner => owner is not null);
                return new(definition.Slot, definition.Name, definition.Category,
                    condition.Status == S14ConditionStatus.Satisfied &&
                        !history.Acknowledged[definition.OccurrenceId] && unavailable is null, condition.Status, unavailable)
                    { AlreadyOccurred = occurred };
            }
            catch (InvalidDataException error)
            { return new(definition.Slot, definition.Name, definition.Category, false, null, error.Message); }
        }
    }
}

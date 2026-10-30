using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Evaluate the compiled editor condition against the actual
    /// campaign current queries. This publishes no occurrence or completion.</summary>
    public S14EventConditionResult EvaluateEditedEventConditions(GameState state,
        EditedEventCompiledDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        var current = CaptureCurrentCampaignEventQueries(state);
        return definition.EvaluateConditions(new S14PlayerEventConditionRuntime(current,
            _ => ReadOrdinaryEventDispatchInputs(state), current));
    }

    /// <summary>Prepare the edited presentation using ordinary current query
    /// and camera owners. The caller retains the real story cursor and must
    /// execute the separate results transaction after actual playback.</summary>
    public CampaignStoryRequest PrepareEditedEventStoryCurrent(GameState state,
        EditedEventCompiledDefinition definition, long sequence,
        IS14AudioEventExecutionState audio)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(audio);
        var inputs = CaptureCampaignStoryInputs(state, false);
        var plan = definition.StageContents(inputs.Persons,
            _ => ReadOrdinaryEventDispatchInputs(state), audio, inputs);
        var selections = new CampaignStorySelectionContext(
            Enumerable.Range(0, 5).Select(index => plan.SelectedSpeakerIds.TryGetValue(-10 - index, out int id)
                ? (int?)id : null).ToImmutableArray(), plan.SelectedTargetForcePhysicalSlots.ToImmutableArray())
            { DialogueRoles = EditedEventResultSelectors.SaveDialogue(plan.FinalDialogueCast, inputs.Persons) };
        return CampaignStorySemantics.Bind(plan, sequence, allowEmpty: true) with { SelectionContext = selections };
    }
}

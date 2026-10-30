using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record CollectionEventStorySlice(GameState State,CampaignStoryRequest Story,
    CampaignStoryExecutionCursor Cursor);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The title's real mode3/variant invocation, continued with the
    ///existing ordinary resource cursor after its preceding presentation ends.</summary>
    internal CollectionEventStorySlice PrepareCollectionEventStorySliceCurrent(GameState state,
        CampaignEventExecutionProgram program,long sequence,int executionMode,int variantFlag,
        IS14AudioEventExecutionState audio,CampaignStoryRequest? previous=null,
        CampaignStoryExecutionCursor? cursor=null)
    {
        if(executionMode!=3)throw new InvalidDataException("collection.event.playback_mode_requires_collection_context");
        if(previous is not null&&(previous.NextStep!=previous.Steps.Length||
            previous.Source!=program.Source||previous.ContentRevision!=program.Revision||previous.Sequence!=sequence))
            throw new InvalidDataException("collection.event.resume_story_owner");
        var latest=state;
        var captured=CaptureCampaignStoryInputs(state,true);
        var current=captured.AreaListCurrent??throw new InvalidDataException("collection.event.current_owner");
        var inputs=captured with
        {
            //Context1D8/1DC changes the source If selection, not the settings
            //predicate, script return values or World.ScenarioMode.
            ForcedIfResult=variantFlag!=0,
            ResolveMessageStringParameters=(bank,roots)=>
            {
                var formatted=PrepareStoryStringParametersCurrent(latest,current,bank,roots);
                latest=formatted.State;return formatted.Names;
            }
        };
        var slice=CampaignEventExecution.StageUntilRelationship(program,inputs,
            _=>ReadOrdinaryEventDispatchInputs(latest) with {ExecutionGateEnabled=true},audio,cursor,
            (code,first,second,third)=>throw new InvalidDataException("collection.event.unexpected_relationship:"+code));
        var chunk=CampaignStorySemantics.Bind(slice.Plan,sequence,allowEmpty:true);
        var story=previous is null?chunk:previous with
        {
            Steps=previous.Steps.Concat(chunk.Steps).ToArray(),PreparedStep=null
            //Keep the actual consumed Scene/TextLog, actors, music, effects
            //and NextStep. A new resource chunk never replays the old steps.
        };
        return new(latest,story,slice.Cursor);
    }
}

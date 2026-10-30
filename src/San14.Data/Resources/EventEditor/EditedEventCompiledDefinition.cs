using System.Collections.Immutable;
using System.Security.Cryptography;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Once-compiled editor programs for one actual library definition.
/// This is preparation, not an occurrence, selected action or Story receipt.</summary>
public sealed class EditedEventCompiledDefinition
{
    public EditedEventDefinition Definition { get; }
    public EventSourceReference Source { get; }
    public S14EventConditionProgram Conditions { get; }
    public S14EventConditionProgram Contents { get; }
    public S14EventConditionProgram Results { get; }
    public CampaignEventExecutionProgram ContentProgram { get; }
    public S14EventExecutionProgram ResultsProgram { get; }

    public EditedEventCompiledDefinition(EditedEventDefinition definition,
        string libraryUri, string libraryRevision)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        Definition = definition;
        Source = new(libraryUri, libraryRevision, definition.Slot, definition.Slot);
        PlayableEventStateValidation.ValidateSource(Source);
        Conditions = EditedEventProgramCompiler.Compile(definition.Conditions, definition.Slot);
        var flattened = new EditedEventProgram(definition.Contents
            .SelectMany(content => content.Program.Instructions).ToImmutableArray());
        Contents = EditedEventProgramCompiler.Compile(flattened, definition.Slot);
        Results = EditedEventProgramCompiler.Compile(definition.Results, definition.Slot);
        ContentProgram = CampaignEventExecutionProgram.ReadEditedProgram(Source, Contents);
        ResultsProgram = new(Results,
            Convert.ToHexString(SHA256.HashData(Results.Operations.SelectMany(operation => operation.RawBytes).ToArray())),
            Source.ResourceSha256, Source.EventId);
    }

    public S14EventConditionResult EvaluateConditions(IS14EventConditionRuntime actualRuntime) =>
        EditedEventProgramCompiler.Evaluate(Conditions, actualRuntime);

    public CampaignEventExecutionPlan StageContents(IS14PlayerEventConditionQueries currentPeople,
        Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        IS14AudioEventExecutionState? audio, CampaignEventExecutionInputs inputs) =>
        CampaignEventExecution.Stage(ContentProgram,
            currentPeople, dispatchInputs, audio, storyInputs: inputs);

    /// <summary>Business result handlers retain their own ordinary mutation
    /// provider. This describes their actual statement language without
    /// pretending that presentation completion executed them.</summary>
    public S14EventExecutionProgram PrepareResults() => ResultsProgram;

    public S14EventExecutionResult ExecuteAreaResults(IS14AreaEventExecutionState actualCandidate,
        Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        IReadOnlyList<S14EventExecutionArea> initialAreas, IS14AudioEventExecutionState? audio = null) =>
        new S14AreaEventExecutionRuntime(actualCandidate, dispatchInputs, initialAreas, audio).Execute(ResultsProgram);
}

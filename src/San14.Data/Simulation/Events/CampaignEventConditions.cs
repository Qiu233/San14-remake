using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignEventConditionAssessment(EventSourceReference Source,
    S14EventEligibilityResult Eligibility, S14EventExecutionProgram? Execution,
    string? MissingExecutionRule, string? ExecutionReason)
{
    // Eligibility alone does not establish that the complete story can execute.
    public bool HasCompleteExecution => Execution is not null;
}

/// <summary>
/// Evaluates one actual source event with current explicit context queries and
/// preflights its entire execution directory. This creates no Core trigger or
/// completion stamp and does not replace native scan parameters with period count.
/// </summary>
public static class CampaignEventConditions
{
    public static CampaignEventConditionAssessment Assess(LoadedEvents loaded, int eventId,
        IS14EventEligibilityState eligibilityState, IS14PlayerEventConditionQueries currentQueries,
        Func<S14ConditionContext, S14ConditionDispatchInputs> dispatchInputs,
        IS14BasicEventConditionQueries? basicQueries = null)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(eligibilityState);
        ArgumentNullException.ThrowIfNull(currentQueries);
        ArgumentNullException.ThrowIfNull(dispatchInputs);
        S14EventDocument document = S14EventReader.Read(loaded.Document.RawBytes);
        if (document.SourceSha256 != loaded.Document.SourceSha256 ||
            !document.SourceSha256.Equals(loaded.Source.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Current event assessment source has a mismatched revision.");
        S14EventRecord[] records = document.Events.Where(row => row.Id == eventId).ToArray();
        if (records.Length != 1) throw new InvalidDataException("Assessment requires one actual source event record.");
        long directory = (long)eventId - document.NativeBaseId;
        if (directory < 0 || directory >= document.ConditionStreams.Count)
            throw new InvalidDataException("Assessment event ID is outside its source directory.");
        var source = new EventSourceReference(loaded.Source.ResourceUri,
            document.SourceSha256, (int)directory, eventId);
        PlayableEventStateValidation.ValidateSource(source);
        S14EventEligibilityResult result = S14EventEligibilityEvaluator.Evaluate(records[0], document,
            eligibilityState, _ => new S14PlayerEventConditionRuntime(currentQueries, dispatchInputs, basicQueries));
        try
        {
            S14EventExecutionProgram program = S14EventExecutionProgram.ReadEvent(document, eventId);
            return new(source, result, program, null, null);
        }
        catch (InvalidDataException error)
        {
            return new(source, result, null, "event.execution_opcode", error.Message);
        }
    }
}

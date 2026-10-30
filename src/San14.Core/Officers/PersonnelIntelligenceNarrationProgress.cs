namespace San14.Core;

public enum PersonnelIntelligenceNarrationBranch
{
    CancelAway, CancelInspect, AwayFailure, AwaySuccess, AwayTargetNotice, InspectSuccess
}

public enum PersonnelIntelligenceNarrationPhase { AwaitingNarration, ReadyToResume }

/// <summary>Known execution progress. A null task property retains unavailable
/// legacy knowledge; a known wrapper with no pending work has reached no wait.</summary>
public sealed record PersonnelIntelligenceNarrationProgress(PersonnelIntelligenceNarrationPending? Pending);

/// <summary>The actual local receivers and already determined outcome retained
/// across one intelligence narration. Current budget and health are read only
/// when their original tail reaches those writers.</summary>
public sealed record PersonnelIntelligenceAwayNarrationOutcome(int OwnForcePhysicalSlot,
    int TargetForcePhysicalSlot, int ActorDistrictPhysicalSlot, int OwnRulerPhysicalSlot,
    int TargetRulerPhysicalSlot, bool Succeeded, bool Injured, int Gain, int Loss, int Duration);

/// <summary>A reached narration wait. Text and voice in the presentation outbox
/// have already been selected; failure's supplemental text is still deferred.
/// Only an actual matching view receipt can make this continuation ready.</summary>
public sealed record PersonnelIntelligenceNarrationPending(PersonnelIntelligenceNarrationBranch Branch,
    PersonnelIntelligenceNarrationPhase Phase, NativeMapSeasonDate SourceDate,
    int ActorPhysicalSlot, long NarrationBatchSequence, string MainTextAtSource)
{
    public PersonnelIntelligenceAwayNarrationOutcome? AwayOutcome { get; init; }
}

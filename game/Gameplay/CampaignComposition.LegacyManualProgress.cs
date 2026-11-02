using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

public sealed record LegacyCampaignProgressResult(GameplaySession SourceSession, GameState SourceWorld,
    GameState? SourceManualCandidate, GameState? SourceStoryCandidate, long SourceStorySequence,
    GameplaySession Session, CampaignComposition Composition, CampaignPresentation Presentation)
{
    public GameState? SourceArmyProgressCandidate { get; init; }
    public bool IsCurrent(GameplaySession? active) => ReferenceEquals(active, SourceSession) &&
        ReferenceEquals(active.World, SourceWorld) && active.LastStorySequence == SourceStorySequence &&
        ReferenceEquals(active.PendingManualTacticCandidate, SourceManualCandidate) &&
        ReferenceEquals(active.PendingProgressCandidate, SourceStoryCandidate) &&
        ReferenceEquals(active.PendingArmyProgressCandidate, SourceArmyProgressCandidate) && active.PendingNormalPeriod is null;
}

public sealed partial class CampaignComposition
{
    /// <summary>Continues the saved operation after real presentation. The
    /// optional receipt is supplied only by the fully retired unique scene;
    /// ordinary nodes already retire through their own lifecycle API.</summary>
    public Task<LegacyCampaignProgressResult> ResumeLegacyManualProgressAsync(GameplaySession sourceSession,
        CampaignStoryBindings storyBindings, TacticPresentationReceipt? actualUniqueReceipt = null,
        CancellationToken cancellationToken = default, NarrationVoiceSettings? narrationVoiceSettings = null)
    {
        ArgumentNullException.ThrowIfNull(sourceSession);
        ArgumentNullException.ThrowIfNull(storyBindings);
        int player = sourceSession.PlayerForceId ?? throw new InvalidOperationException("The campaign has not started.");
        var sourceWorld = sourceSession.World;
        var sourceManual = sourceSession.PendingManualTacticCandidate;
        var sourceStory = sourceSession.PendingProgressCandidate;
        var sourceMovement = sourceSession.PendingArmyProgressCandidate;
        long sequence = sourceSession.LastStorySequence;
        if (sourceSession.PendingNormalPeriod is not null ||
            (sourceManual is null ? 0 : 1) + (sourceStory is null ? 0 : 1) + (sourceMovement is null ? 0 : 1) != 1)
            throw new InvalidOperationException("Legacy progress requires its exclusive saved operation.");
        cancellationToken.ThrowIfCancellationRequested();
        var simulation = sourceSession.CaptureSimulationFork();
        var repository = _repository.CreateReadScope();
        var voice = narrationVoiceSettings ?? _personnelNarrationVoiceSettings;
        voice?.Validate();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var composition = new CampaignComposition(repository, this);
            if (voice is not null) composition.ConfigurePersonnelNarrationVoice(voice);
            var candidate = simulation.PendingManualTacticCandidate ?? simulation.PendingArmyProgressCandidate ?? simulation.PendingProgressCandidate
                ?? throw new InvalidDataException("legacy.progress.candidate");
            var prepared = composition.PrepareRules(candidate, player);
            var army = prepared.Army ?? throw new InvalidDataException("legacy.progress.native_rules");
            composition._army = army;
            var session = GameplaySession.FromSimulationFork(simulation, prepared.Rules, player,
                Calendar(army), composition._saveCodec, sourceSession.CommandPlannerFactory);
            army.ConfigureNormalTurnStoryBindings(storyBindings);
            try
            {
                var rules = army.CreateLegacyCampaignProgressRules(candidate, storyBindings, cancellationToken.ThrowIfCancellationRequested);
                var result = actualUniqueReceipt is { } receipt
                    ? session.ResumeManualTactic(rules, receipt) : session.RetryProgress(rules);
                if (result.Outcome == CampaignProgressOutcome.Unresolved)
                    throw new InvalidDataException(result.UnresolvedOwner ?? "legacy.progress.unresolved");
                cancellationToken.ThrowIfCancellationRequested();
                return new LegacyCampaignProgressResult(sourceSession, sourceWorld, sourceManual, sourceStory,
                    sequence, session, composition, prepared.Presentation) { SourceArmyProgressCandidate = sourceMovement };
            }
            finally { army.ConfigureNormalTurnStoryBindings(null); }
        }, cancellationToken);
    }
}

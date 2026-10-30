using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

/// <summary>A completed private calculation. The host adopts this result on its
/// own thread only while the exact source session/world is still current.</summary>
public sealed record CampaignPeriodAdvanceResult(GameplaySession SourceSession,
    GameState SourceWorld, GameplaySession Session, CampaignComposition Composition,
    CampaignPresentation Presentation)
{
    public long SourceStorySequence { get; init; }
    public NormalPeriodOperation? SourceNormalPeriod { get; init; }

    public bool IsCurrent(GameplaySession? activeSession) =>
        ReferenceEquals(activeSession, SourceSession) &&
        ReferenceEquals(activeSession.World, SourceWorld) &&
        activeSession.LastStorySequence == SourceStorySequence &&
        ReferenceEquals(activeSession.PendingNormalPeriod, SourceNormalPeriod) &&
        (!activeSession.HasPendingOperation || activeSession.PendingNormalPeriod is not null);
}

public sealed partial class CampaignComposition
{
    private CampaignComposition(GameResourceRepository repository, CampaignComposition source)
    {
        _repository = repository;
        _originalAiStrategy = source._originalAiStrategy;
        _saveCodec = new ResourceSaveCodec(repository);
        _applicationGameplay = source._applicationGameplay;
        _applicationProfile = source._applicationProfile;
        _endingBindings = source._endingBindings;
        _baseCaptureTerrain = source._baseCaptureTerrain;
        _baseCaptureTerrainBound = source._baseCaptureTerrainBound;
        _armyTraitSpeechViews = source._armyTraitSpeechViews;
        _facilityPeriodicArmyViews = source._facilityPeriodicArmyViews;
        _armyFireViews = source._armyFireViews;
        _tacticPresentationViews = Volatile.Read(ref source._tacticPresentationViews);
        // Loaded definitions and these read-only views are shared. Mutable
        // resource caches, Army captures and rule services belong to the worker.
        Scenario = source.Scenario;
        Map = source.Map;
        Overview = source.Overview;
        Construction = source.Construction;
    }

    /// <summary>Advance on isolated rules/resource caches, leaving the visible
    /// session unchanged until the host explicitly adopts the returned result.
    /// This is not a load/save protocol and never acknowledges presentation.</summary>
    public Task<CampaignPeriodAdvanceResult> AdvancePeriodAsync(GameplaySession sourceSession,
        CancellationToken cancellationToken = default, CampaignAppRandomState? appRandomSnapshot = null,
        NarrationVoiceSettings? narrationVoiceSettings = null, Func<GameState, GameState>? periodStep = null,
        CampaignStoryBindings? storyBindings = null)
        => AdvanceNormalPeriodAsync(sourceSession, null, cancellationToken, appRandomSnapshot, narrationVoiceSettings, periodStep, storyBindings);

    /// <summary>Apply one explicit diplomatic choice and continue the same
    /// private normal period. Rejection or failure leaves the source session's
    /// waiting candidate and its random streams unchanged.</summary>
    public Task<CampaignPeriodAdvanceResult> ConfirmDiplomaticNegotiationAsync(GameplaySession sourceSession,
        ConfirmDiplomaticNegotiationOrder order, CancellationToken cancellationToken = default,
        CampaignAppRandomState? appRandomSnapshot = null, NarrationVoiceSettings? narrationVoiceSettings = null,
        Func<GameState, GameState>? periodStep = null, CampaignStoryBindings? storyBindings = null)
        => AdvanceNormalPeriodAsync(sourceSession, order, cancellationToken, appRandomSnapshot, narrationVoiceSettings, periodStep, storyBindings);

    /// <summary>Apply the player's response to the saved self-recommendation
    /// and continue its existing normal-turn candidate on the worker.</summary>
    public Task<CampaignPeriodAdvanceResult> ConfirmSelfRecommendationAsync(GameplaySession sourceSession,
        ConfirmSelfRecommendationOrder order, CancellationToken cancellationToken = default,
        CampaignAppRandomState? appRandomSnapshot = null, NarrationVoiceSettings? narrationVoiceSettings = null,
        Func<GameState, GameState>? periodStep = null, CampaignStoryBindings? storyBindings = null)
        => AdvanceNormalPeriodAsync(sourceSession, order, cancellationToken, appRandomSnapshot, narrationVoiceSettings, periodStep, storyBindings);

    public Task<CampaignPeriodAdvanceResult> ConfirmCoalitionInvitationAsync(GameplaySession sourceSession,
        ConfirmCoalitionInvitationOrder order, CancellationToken cancellationToken = default,
        CampaignAppRandomState? appRandomSnapshot = null, NarrationVoiceSettings? narrationVoiceSettings = null,
        Func<GameState, GameState>? periodStep = null, CampaignStoryBindings? storyBindings = null)
        => AdvanceNormalPeriodAsync(sourceSession, order, cancellationToken, appRandomSnapshot, narrationVoiceSettings, periodStep, storyBindings);

    public Task<CampaignPeriodAdvanceResult> ConfirmCapturedRulerTreatmentAsync(GameplaySession sourceSession,
        ConfirmCapturedRulerTreatmentOrder order, CancellationToken cancellationToken = default,
        CampaignAppRandomState? appRandomSnapshot = null, NarrationVoiceSettings? narrationVoiceSettings = null,
        Func<GameState, GameState>? periodStep = null, CampaignStoryBindings? storyBindings = null)
        => AdvanceNormalPeriodAsync(sourceSession, order, cancellationToken, appRandomSnapshot, narrationVoiceSettings, periodStep, storyBindings);

    public Task<CampaignPeriodAdvanceResult> ConfirmRulerSuccessionAsync(GameplaySession sourceSession,
        ConfirmRulerSuccessionOrder order, CancellationToken cancellationToken = default,
        CampaignAppRandomState? appRandomSnapshot = null, NarrationVoiceSettings? narrationVoiceSettings = null,
        Func<GameState, GameState>? periodStep = null, CampaignStoryBindings? storyBindings = null)
        => AdvanceNormalPeriodAsync(sourceSession, order, cancellationToken, appRandomSnapshot, narrationVoiceSettings, periodStep, storyBindings);

    private Task<CampaignPeriodAdvanceResult> AdvanceNormalPeriodAsync(GameplaySession sourceSession,
        GameOrder? confirmation, CancellationToken cancellationToken,
        CampaignAppRandomState? appRandomSnapshot, NarrationVoiceSettings? narrationVoiceSettings, Func<GameState, GameState>? periodStep,
        CampaignStoryBindings? storyBindings)
    {
        ArgumentNullException.ThrowIfNull(sourceSession);
        int player = sourceSession.PlayerForceId ?? throw new InvalidOperationException("The campaign has not started.");
        GameState sourceWorld = sourceSession.World;
        long sourceSequence = sourceSession.LastStorySequence;
        var sourceNormalPeriod = sourceSession.PendingNormalPeriod;
        if (sourceWorld.Phase != TurnPhase.Strategy)
            throw new InvalidOperationException("The campaign is not in strategy phase.");
        cancellationToken.ThrowIfCancellationRequested();
        // Capture application settings on the caller's thread, just like App32.
        // The worker never reads a later mutable host preference.
        var voiceSettings = narrationVoiceSettings ?? _personnelNarrationVoiceSettings;
        voiceSettings?.Validate();
        // Capture the trusted session generation before scheduling. Fork also
        // carries any pending continuation, so it cannot be silently discarded.
        GameSimulation simulation = sourceSession.CaptureSimulationFork(appRandomSnapshot);
        string sourceDirectory = _repository.SourceDirectory;
        string? overridesDirectory = _repository.OverrideDirectory;
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var repository = new GameResourceRepository(sourceDirectory, overridesDirectory);
            var composition = new CampaignComposition(repository, this);
            if (voiceSettings is not null) composition.ConfigurePersonnelNarrationVoice(voiceSettings);
            PreparedRules prepared = composition.PrepareRules(simulation.PendingNormalPeriod?.Candidate ?? simulation.State, player);
            var session = GameplaySession.FromSimulationFork(simulation, prepared.Rules, player,
                Calendar(prepared.Army), composition._saveCodec, sourceSession.CommandPlannerFactory);
            composition._army = prepared.Army;
            prepared.Army?.ConfigureNormalTurnStoryBindings(storyBindings);
            prepared.Army?.ConfigurePeriodPresentation(candidate =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return periodStep?.Invoke(candidate) ?? candidate;
            });
            try
            {
                if (confirmation is not null)
                {
                    var confirmed = session.Issue(confirmation);
                    if (!confirmed.Accepted)
                        throw new InvalidOperationException(confirmed.Error ?? "The campaign decision was rejected.");
                }
                bool stillNegotiating = confirmation is ConfirmDiplomaticNegotiationOrder diplomatic &&
                    session.PendingNormalPeriod?.Candidate.PersonnelCommands?.Diplomacies?
                        .GetValueOrDefault(diplomatic.TaskId)?.AwaitingNegotiation == true;
                bool hasResponse = confirmation is not null && session.PendingNormalPeriod is {} waiting &&
                    PersonnelPresentationQueue.Next(waiting.Candidate) is not null;
                bool awaitingRulerChoice = session.PendingNormalPeriod is { } rulerWaiting &&
                    (rulerWaiting.PendingRulerSuccession is not null || rulerWaiting.PendingCapturedRulerTreatment is not null);
                if (!stillNegotiating && !hasResponse && !awaitingRulerChoice && !session.AdvancePeriod())
                    throw new InvalidOperationException("The campaign has ended.");
                cancellationToken.ThrowIfCancellationRequested();
                return new CampaignPeriodAdvanceResult(sourceSession, sourceWorld,
                    session, composition, prepared.Presentation)
                    { SourceStorySequence = sourceSequence, SourceNormalPeriod = sourceNormalPeriod };
            }
            finally { prepared.Army?.ConfigurePeriodPresentation(null); }
        }, cancellationToken);
    }
}

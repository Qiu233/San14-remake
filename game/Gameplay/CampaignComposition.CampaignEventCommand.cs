using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

/// <summary>A manual event command calculated on isolated rules. The host
/// adopts it only while the exact originating session and invocation remain current.</summary>
public sealed record CampaignEventCommandAdvanceResult(GameplaySession SourceSession,
    GameState SourceWorld, GameplaySession Session, CampaignComposition Composition,
    CampaignPresentation Presentation)
{
    public long SourceStorySequence { get; init; }
    public CampaignEventCommandPlayback? SourceCommand { get; init; }

    public bool IsCurrent(GameplaySession? activeSession) =>
        ReferenceEquals(activeSession, SourceSession) &&
        ReferenceEquals(activeSession.World, SourceWorld) &&
        activeSession.LastStorySequence == SourceStorySequence &&
        ReferenceEquals(activeSession.PendingCampaignEventCommand, SourceCommand) &&
        (!activeSession.HasPendingOperation || activeSession.PendingCampaignEventCommand is not null);
}

public sealed partial class CampaignComposition
{
    /// <summary>Prepare a genuine Events selection on a private candidate.
    /// Entry audio remains a real main-thread barrier before its business stream.</summary>
    public Task<CampaignEventCommandAdvanceResult> BeginCampaignEventCommandAsync(GameplaySession sourceSession,
        int eventId, CancellationToken cancellationToken = default,
        CampaignAppRandomState? appRandomSnapshot = null, NarrationVoiceSettings? narrationVoiceSettings = null,
        CampaignStoryBindings? storyBindings = null) =>
        RunCampaignEventCommandAsync(sourceSession, eventId, null, cancellationToken,
            appRandomSnapshot, narrationVoiceSettings, storyBindings);

    /// <summary>Continue only this command after actual presentation or a real
    /// ruler-successor decision. No normal Army/Turn period or planner is advanced.</summary>
    public Task<CampaignEventCommandAdvanceResult> ContinueCampaignEventCommandAsync(GameplaySession sourceSession,
        CancellationToken cancellationToken = default, CampaignAppRandomState? appRandomSnapshot = null,
        NarrationVoiceSettings? narrationVoiceSettings = null, CampaignStoryBindings? storyBindings = null,
        ConfirmRulerSuccessionOrder? confirmation = null) =>
        RunCampaignEventCommandAsync(sourceSession, null, confirmation, cancellationToken,
            appRandomSnapshot, narrationVoiceSettings, storyBindings);

    private Task<CampaignEventCommandAdvanceResult> RunCampaignEventCommandAsync(GameplaySession sourceSession,
        int? eventId, ConfirmRulerSuccessionOrder? confirmation, CancellationToken cancellationToken,
        CampaignAppRandomState? appRandomSnapshot, NarrationVoiceSettings? narrationVoiceSettings,
        CampaignStoryBindings? storyBindings)
    {
        ArgumentNullException.ThrowIfNull(sourceSession);
        int player = sourceSession.PlayerForceId ?? throw new InvalidOperationException("The campaign has not started.");
        GameState sourceWorld = sourceSession.World;
        long sourceSequence = sourceSession.LastStorySequence;
        var sourceCommand = sourceSession.PendingCampaignEventCommand;
        if (sourceWorld.Phase != TurnPhase.Strategy)
            throw new InvalidOperationException("The campaign is not in strategy phase.");
        if (eventId.HasValue ? sourceSession.HasPendingOperation : sourceCommand is null)
            throw new InvalidOperationException("event_command.session_owner");
        if (!eventId.HasValue && confirmation is null && sourceWorld.PersonDeathContinuation?.PendingSuccession is not null)
            throw new InvalidOperationException("event_command.ruler_successor_pending");
        cancellationToken.ThrowIfCancellationRequested();
        var voiceSettings = narrationVoiceSettings ?? _personnelNarrationVoiceSettings;
        voiceSettings?.Validate();
        GameSimulation simulation = sourceSession.CaptureSimulationFork(appRandomSnapshot);
        // The worker owns a fresh reader/ledger while borrowing only the
        // process resource content and these captured application facts.
        var repository = _repository.CreateReadScope();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var composition = new CampaignComposition(repository, this);
            if (voiceSettings is not null) composition.ConfigurePersonnelNarrationVoice(voiceSettings);
            PreparedRules prepared = composition.PrepareRules(simulation.State, player);
            var rules = prepared.Army as ICampaignEventCommandRules ??
                throw new InvalidDataException("event_command.rules_owner");
            var session = GameplaySession.FromSimulationFork(simulation, prepared.Rules, player,
                Calendar(prepared.Army), composition._saveCodec, sourceSession.CommandPlannerFactory);
            composition._army = prepared.Army;
            prepared.Army!.ConfigureNormalTurnStoryBindings(storyBindings);

            if (eventId is int selected)
            {
                if (!session.TryBeginCampaignEventCommand(session.World, selected, rules, out var missing))
                    throw new InvalidDataException(missing ?? "event_command.begin_rejected");
            }
            else
            {
                if (confirmation is not null)
                {
                    var result = session.Issue(confirmation);
                    if (!result.Accepted)
                        throw new InvalidOperationException(result.Error ?? "The event's ruler decision was rejected.");
                }
                // A real decision can publish response effects or another
                // successor choice. Return those owners before running the leaf tail.
                bool awaitingResponse = confirmation is not null &&
                    (session.World.PersonDeathContinuation?.PendingSuccession is not null ||
                     PersonnelPresentationQueue.Next(session.World) is not null);
                if (!awaitingResponse && !session.TryContinueCampaignEventCommand(session.World, rules, out var missing))
                    throw new InvalidDataException(missing ?? "event_command.continue_rejected");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new CampaignEventCommandAdvanceResult(sourceSession, sourceWorld, session, composition, prepared.Presentation)
                { SourceStorySequence = sourceSequence, SourceCommand = sourceCommand };
        }, cancellationToken);
    }
}

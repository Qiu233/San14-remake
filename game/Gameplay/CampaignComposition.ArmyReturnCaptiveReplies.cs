using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

public sealed partial class CampaignComposition
{
    private Task<CampaignPeriodAdvanceResult> ReplyArmyReturnCaptiveAsync(GameplaySession sourceSession,
        GameOrder reply, CancellationToken cancellationToken, CampaignAppRandomState? appRandomSnapshot,
        NarrationVoiceSettings? narrationVoiceSettings,
        Func<GameState, NativeArmyProgressStageMoment?, GameState>? periodStep,
        CampaignStoryBindings? storyBindings,
        Func<GameState, NativeArmyProgressStageMoment, GameState>? stageAdmission)
    {
        ArgumentNullException.ThrowIfNull(sourceSession);
        return sourceSession.PendingNormalPeriod is not null
            ? AdvanceNormalPeriodAsync(sourceSession, reply, cancellationToken, appRandomSnapshot,
                narrationVoiceSettings, periodStep, storyBindings, stageAdmission)
            : ReplyArmyReturnCaptiveOutsideNormalPeriodAsync(sourceSession, reply, cancellationToken,
                appRandomSnapshot, narrationVoiceSettings, storyBindings);
    }

    private Task<CampaignPeriodAdvanceResult> ReplyArmyReturnCaptiveOutsideNormalPeriodAsync(
        GameplaySession sourceSession, GameOrder reply, CancellationToken cancellationToken,
        CampaignAppRandomState? appRandomSnapshot, NarrationVoiceSettings? narrationVoiceSettings,
        CampaignStoryBindings? storyBindings)
    {
        int player = sourceSession.PlayerForceId ?? throw new InvalidOperationException("The campaign has not started.");
        var sourceWorld = sourceSession.World;
        var sourceManual = sourceSession.PendingManualTacticCandidate;
        var sourceMovement = sourceSession.PendingArmyProgressCandidate;
        long sequence = sourceSession.LastStorySequence;
        if (sourceSession.PendingNormalPeriod is not null ||
            (sourceManual ?? sourceMovement ?? sourceWorld).ArmyReturnCaptiveContinuation is null && sourceMovement is null &&
                !IsManualRulerReply(sourceSession, reply) ||
            sourceSession.PendingProgressCandidate is not null)
            throw new InvalidOperationException("The captive reply has no direct or manual-tactic owner.");
        cancellationToken.ThrowIfCancellationRequested();
        var voiceSettings = narrationVoiceSettings ?? _personnelNarrationVoiceSettings;
        voiceSettings?.Validate();
        var simulation = sourceSession.CaptureSimulationFork(appRandomSnapshot);
        var repository = _repository.CreateReadScope();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var composition = new CampaignComposition(repository, this);
            if (voiceSettings is not null) composition.ConfigurePersonnelNarrationVoice(voiceSettings);
            var prepared = composition.PrepareRules(simulation.PendingManualTacticCandidate ?? simulation.PendingArmyProgressCandidate ?? simulation.State, player);
            composition._army = prepared.Army;
            var session = GameplaySession.FromSimulationFork(simulation, prepared.Rules, player,
                Calendar(prepared.Army), composition._saveCodec, sourceSession.CommandPlannerFactory);
            prepared.Army?.ConfigureNormalTurnStoryBindings(storyBindings);
            try
            {
                var confirmed = session.Issue(reply);
                if (!confirmed.Accepted)
                    throw new InvalidOperationException(confirmed.Error ?? "The captive reply was rejected.");
                // A direct caller finishes its saved return suffix inside the
                // command. A manual caller remains in its existing stage6
                // operation; only that real progress runner can retry it.
                // Neither path calls AdvancePeriod or invents a receipt.
                cancellationToken.ThrowIfCancellationRequested();
                return new CampaignPeriodAdvanceResult(sourceSession, sourceWorld,
                    session, composition, prepared.Presentation)
                { SourceStorySequence = sequence, SourceManualTacticCandidate = sourceManual, SourceArmyProgressCandidate = sourceMovement };
            }
            finally { prepared.Army?.ConfigureNormalTurnStoryBindings(null); }
        }, cancellationToken);
    }
}

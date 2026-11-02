using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private GameState? _automaticLegacyProgressAttempt;

    // Called after the real outbox head has been consumed. Save/Load and a
    // successful formal reply both retain the same candidate owner here.
    private bool TryRefreshLegacyManualProgress(GameplaySession session, CampaignComposition composition,
        MapSceneView map, int player)
    {
        if (session.PendingNormalPeriod is not null) return false;
        if (session.PendingArmyProgressCandidate is { } moving)
        {
            if (TryOpenRulerDecision(session, moving, composition, map, player)) return true;
            if (session.CanRetryLegacyProgress) TryContinueLegacyManualProgress(session, composition);
            return true;
        }
        if (session.PendingManualTacticCandidate is { } candidate)
        {
            if (TryOpenRulerDecision(session, candidate, composition, map, player)) return true;
            if (session.CanRetryLegacyProgress) TryContinueLegacyManualProgress(session, composition);
            return true;
        }
        if (session.PendingProgressCandidate is not null)
        {
            if (session.PendingLegacyStory is not null)
                return TryOpenStoryPresentation(session, null, composition, map);
            if (session.CanRetryLegacyProgress) TryContinueLegacyManualProgress(session, composition);
            return true;
        }
        return false;
    }

    private bool TryContinueLegacyManualProgress(GameplaySession session, CampaignComposition composition,
        TacticPresentationReceipt? actualUniqueReceipt = null)
    {
        if (_advancingPeriod || _failed || _shuttingDown || IsReturningToTitle ||
            !ReferenceEquals(_session, session) || !ReferenceEquals(_campaignComposition, composition)) return false;
        var owner = session.PendingManualTacticCandidate ?? session.PendingArmyProgressCandidate ?? session.PendingProgressCandidate;
        if (owner is null || actualUniqueReceipt is null && ReferenceEquals(_automaticLegacyProgressAttempt, owner)) return false;
        if (actualUniqueReceipt is null && !session.CanRetryLegacyProgress) return false;
        _automaticLegacyProgressAttempt = owner;
        _advancingPeriod = true;
        _periodAdvanceTask = ResumeLegacyManualProgressAsync(session, composition, actualUniqueReceipt);
        return true;
    }

    private async Task ResumeLegacyManualProgressAsync(GameplaySession source, CampaignComposition composition,
        TacticPresentationReceipt? actualUniqueReceipt)
    {
        var cancellation = new CancellationTokenSource();
        _periodAdvanceCancellation = cancellation;
        var map = _residentMap;
        GameplaySession? adopted = null;
        Exception? applicationFailure = null;
        try
        {
            if (map is null) throw new InvalidDataException("legacy.progress.map_owner");
            var bindings = CaptureNormalTurnStoryBindings(source, composition, map, cancellation.Token)
                ?? throw new InvalidDataException("legacy.progress.story_bindings_owner");
            if (map.SourceMainCameraTerrain is { } terrain) composition.ConfigureBaseCapturePresentationTerrain(terrain);
            composition.ConfigureTacticPresentationViews(map.TacticPresentationViews);
            var result = await composition.ResumeLegacyManualProgressAsync(source, bindings,
                actualUniqueReceipt, cancellation.Token, _preferences.Voice.Capture());
            if (_failed || _shuttingDown || !GodotObject.IsInstanceValid(this) || !IsInsideTree() ||
                !result.IsCurrent(_session) || !ReferenceEquals(_residentMap, map)) return;
            _session = result.Session;
            AdoptOrdinaryTacticSession(source, result.Session, map);
            // The previous legacy event has reached its real step boundary.
            // A returned event reopens from its saved scene, never a new seed.
            if (HasStoryPresentation) CloseStoryPresentation();
            adopted = result.Session;
            _sceneAudioBinding?.AdoptPlayableSession(source, result.Session);
            _campaignComposition = result.Composition;
            var owner = result.Session.PendingManualTacticCandidate ?? result.Session.PendingArmyProgressCandidate ?? result.Session.PendingProgressCandidate ?? result.Session.World;
            SynchronizeApplicationCrtRandom(owner);
            if (owner.CampaignAppRandom32 is { } app) _presentationRandom.InstallCommittedState(app.State);
            try
            {
                result.Composition.PublishApplicationServices(owner);
            }
            catch (Exception error) { applicationFailure = error; }
            _loadedMap = result.Composition.Map; _map = _loadedMap.Map; _mapOverview = result.Composition.Overview;
            PublishCampaignPresentation(result.Presentation); _playMessage.Text = "";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_shuttingDown && GodotObject.IsInstanceValid(this) && IsInsideTree() && ReferenceEquals(_session, source))
                ReportPlayFailure(error);
        }
        finally
        {
            bool owns = ReferenceEquals(_periodAdvanceCancellation, cancellation);
            if (owns)
            {
                _advancingPeriod = false; _periodAdvanceCancellation = null;
                if (adopted is not null) _automaticLegacyProgressAttempt = null;
            }
            cancellation.Dispose();
            if (owns && ReferenceEquals(_session, adopted ?? source) && GodotObject.IsInstanceValid(this) &&
                IsInsideTree() && !_shuttingDown && !_failed)
                RunUiAction(() =>
                {
                    RefreshCurrentPlayWorld();
                    if (applicationFailure is not null) ReportPlayFailure(applicationFailure);
                });
        }
    }
}

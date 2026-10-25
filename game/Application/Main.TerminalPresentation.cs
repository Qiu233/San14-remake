using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed record TerminalReturnWorkspace(GameplaySession Session, CampaignApplicationState Application);
    private TerminalReturnWorkspace? _terminalReturn;
    private bool HasTerminalReturn => _terminalReturn is { } pending &&
        ReferenceEquals(_session, pending.Session) && ReferenceEquals(_campaignApplication, pending.Application);

    private sealed class TerminalMusicWorkspace(GameplaySession session, GameState world, MapSceneView map)
    {
        internal readonly GameplaySession Session = session;
        internal readonly GameState World = world;
        internal readonly MapSceneView Map = map;
        internal readonly CancellationTokenSource Lifetime = new();
        internal bool Running = true;
    }
    private TerminalMusicWorkspace? _terminalMusic;
    private bool IsTerminalMusicCurrent => _terminalMusic is { } pending &&
        ReferenceEquals(_session, pending.Session) && ReferenceEquals(_residentMap, pending.Map) &&
        ReferenceEquals(PersonnelPresentationOwner(pending.Session), pending.World);

    private bool TryPresentMapMusic(PersonnelPresentationEntry? entry, MapMusicIntent music,
        GameplaySession session, GameState world, MapSceneView map)
    {
        var audio = _sceneAudioBinding ?? throw new InvalidOperationException("Map music requires its actual audio owner.");
        var pending = new TerminalMusicWorkspace(session, world, map);
        _terminalMusic = pending;
        RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
        map.SetDetailsOpen(true, keepMapVisible: true); SetMapFocus(false); HideDeploymentHud();
        _ = PresentAsync();
        return true;

        async Task PresentAsync()
        {
            try
            {
                await audio.PlayMapMusicIntentAsync(music, _ui.SourceLocale, _preferences.MusicSelection, pending.Lifetime.Token);
                if (_terminalMusic != pending || !IsTerminalMusicCurrent) return;
                bool accepted = entry is null
                    ? session.TryCompletePendingMapMusicIntent(world, music)
                    : session.TryCompleteMapMusicIntent(world, entry.Source, entry.BatchSequence, entry.RequestIndex);
                CloseTerminalMusic();
                if (accepted && !TryOpenPersonnelPresentation())
                { RefreshPlayControls(); RefreshDiplomaticNegotiation(); }
            }
            catch (OperationCanceledException) when (pending.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (_terminalMusic == pending && IsTerminalMusicCurrent)
                { CloseTerminalMusic(); ReportPlayFailure(error); }
            }
            finally
            {
                pending.Running = false;
                if (_terminalMusic != pending) pending.Lifetime.Dispose();
            }
        }
    }

    private bool TryPresentApplicationExit(PersonnelPresentationEntry entry, ApplicationExitRequest request,
        GameplaySession session, GameState world)
    {
        var application = _campaignApplication ?? throw new InvalidOperationException("Terminal presentation requires its campaign application.");
        switch (request.Operation)
        {
            case ApplicationExitOperation.ReturnToTitle:
                // The source only sets the game-state return latch here. Its
                // following music and special-mode calls still own live audio.
                application.RequestReturnToTitle();
                _terminalReturn = new(session, application);
                return session.TryCompleteApplicationExit(world, entry.Source, entry.BatchSequence,
                    entry.RequestIndex, request.Operation);
            case ApplicationExitOperation.ResetAudioSpecialMode:
                var audio = _sceneAudioBinding ?? throw new InvalidOperationException("Terminal audio reset requires its actual audio owner.");
                audio.ResetAudioSpecialMode();
                if (!session.TryCompleteApplicationExit(world, entry.Source, entry.BatchSequence,
                        entry.RequestIndex, request.Operation)) return false;
                if (HasTerminalReturn)
                {
                    audio.RetainMusicForTitle();
                    BeginRequestedReturnToTitle(session, application);
                }
                return true;
            default:
                throw new InvalidDataException("Unknown application exit operation.");
        }
    }

    private void CloseTerminalMusic()
    {
        var pending = _terminalMusic; _terminalMusic = null;
        if (pending is null) return;
        pending.Lifetime.Cancel();
        if (!pending.Running) pending.Lifetime.Dispose();
        if (GodotObject.IsInstanceValid(pending.Map) && ReferenceEquals(_residentMap, pending.Map))
            pending.Map.SetDetailsOpen(false);
    }
}

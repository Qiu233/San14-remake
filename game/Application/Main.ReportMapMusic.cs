using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed record PeriodReportMusicScene(GameplaySession Session, MapSceneView Map,
        MapSeasonRuntimeFrame Displayed);
    private PeriodReportMusicScene? _periodReportMusicScene;

    private sealed class ReportMapMusicWorkspace(GameplaySession session, MapSceneView map,
        MapSeasonRuntimeFrame before, NativeMapSeasonState expected)
    {
        internal readonly GameplaySession Session = session;
        internal readonly MapSceneView Map = map;
        internal readonly MapSeasonRuntimeFrame Before = before;
        internal readonly NativeMapSeasonState Expected = expected;
        internal readonly CancellationTokenSource Lifetime = new();
        internal Action? MapExited;
        internal bool Running = true;
    }
    private ReportMapMusicWorkspace? _reportMapMusic;

    private bool HasPeriodReportMusicScene(GameplaySession session, MapSceneView? map) =>
        _periodReportMusicScene is { } scene && ReferenceEquals(scene.Session, session) && ReferenceEquals(scene.Map, map);

    private async Task CapturePeriodReportMusicScene(GameplaySession session, MapSceneView? map,
        CancellationToken cancellation)
    {
        if (map is null) throw new InvalidOperationException("report.music.initial_map_owner");
        // This is the scene already on screen, which may legitimately be the
        // committed root or a Story scene while a different candidate waits.
        // Never await a not-yet-presented candidate before starting playback.
        while (ReferenceEquals(_session, session) && ReferenceEquals(_residentMap, map) && !_failed && !_shuttingDown)
        {
            cancellation.ThrowIfCancellationRequested();
            if (map.IsPlayableMapReady && map.DisplayedCampaignSeason is { } displayed)
            {
                _periodReportMusicScene = new(session, map, displayed);
                return;
            }
            await NextReportMapMusicFrame(cancellation);
        }
        throw new OperationCanceledException(cancellation);
    }

    private void AdoptPeriodReportMusicScene(GameplaySession previous, GameplaySession current)
    {
        if (_periodReportMusicScene is { } scene && ReferenceEquals(scene.Session, previous))
            _periodReportMusicScene = scene with { Session = current };
    }

    private MapSeasonRuntimeFrame? CaptureFinishedReportMusicScene(GameplaySession session, MapSceneView? map)
    {
        if (map is null) return null;
        if (_periodReportMusicScene is { } scene && ReferenceEquals(scene.Session, session) && ReferenceEquals(scene.Map, map))
            return scene.Displayed;
        // A loaded Reporting state has no previous live period scene. Its
        //actual currently accepted scene is the report's starting owner.
        return map.DisplayedCampaignSeason
            ?? throw new InvalidOperationException("report.music.loaded_displayed_season");
    }

    private bool CurrentReportMapMusic(ReportMapMusicWorkspace owner) =>
        ReferenceEquals(_reportMapMusic, owner) && ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(_residentMap, owner.Map) && !_failed && !_shuttingDown && !IsReturningToTitle &&
        !_advancingPeriod && owner.Session.World.Phase == TurnPhase.Strategy &&
        owner.Session.World.NativeMapSeason == owner.Expected;

    private void BeginReportMapMusic(GameplaySession session, MapSceneView map, MapSeasonRuntimeFrame? before)
    {
        RetireReportMapMusic();
        _periodReportMusicScene = null;
        if (before is null) return;
        var expected = session.World.NativeMapSeason
            ?? throw new InvalidOperationException("report.music.current_calendar");
        var audio = _sceneAudioBinding ?? throw new InvalidOperationException("report.music.audio_owner");
        var owner = new ReportMapMusicWorkspace(session, map, before, expected);
        _reportMapMusic = owner;
        owner.MapExited = () => { if (ReferenceEquals(_reportMapMusic, owner)) RetireReportMapMusic(); };
        map.TreeExiting += owner.MapExited;
        _ = RestoreAsync();

        async Task RestoreAsync()
        {
            try
            {
                MapSeasonRuntimeFrame? displayed;
                while (CurrentReportMapMusic(owner))
                {
                    owner.Lifetime.Token.ThrowIfCancellationRequested();
                    displayed = map.DisplayedCampaignSeason;
                    if (displayed is not null && displayed.State == owner.Expected)
                    {
                        bool changed = owner.Before.Selection.SeasonIndex != displayed.Selection.SeasonIndex;
                        await audio.RestoreMapMusicAfterReportAsync(session, displayed, changed,
                            _ui.SourceLocale, _preferences.MusicSelection, owner.Lifetime.Token);
                        return;
                    }
                    // A report can cover an older accepted map. Wait only for
                    //the real next frame, never infer a scene change from Date.
                    await NextReportMapMusicFrame(owner.Lifetime.Token);
                }
            }
            catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (CurrentReportMapMusic(owner)) ReportPlayFailure(error);
            }
            finally
            {
                if (ReferenceEquals(_reportMapMusic, owner)) RetireReportMapMusic();
                owner.Running = false;
                owner.Lifetime.Dispose();
            }
        }
    }

    private async Task NextReportMapMusicFrame(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var tree = GetTree();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Frame() => reached.TrySetResult();
        tree.ProcessFrame += Frame;
        using var canceled = cancellation.Register(() => reached.TrySetCanceled(cancellation));
        try { await reached.Task; }
        finally { if (GodotObject.IsInstanceValid(tree)) tree.ProcessFrame -= Frame; }
    }

    private void RetireReportMapMusic()
    {
        if (_reportMapMusic is not { } owner) return;
        _reportMapMusic = null;
        if (owner.MapExited is { } exited && GodotObject.IsInstanceValid(owner.Map)) owner.Map.TreeExiting -= exited;
        owner.Lifetime.Cancel();
        if (!owner.Running) owner.Lifetime.Dispose();
    }
}

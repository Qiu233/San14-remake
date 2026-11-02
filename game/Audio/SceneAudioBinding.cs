using Godot;
using San14.Data;
using San14.Client.Gameplay;
using San14.Core;

internal readonly record struct SceneAudioContext(string? ScenarioKey, int? PlayerForceId, bool IsSourcePreview)
{
    public string Key
    {
        get
        {
            string scenario = ScenarioKey ?? "source-catalogue";
            return IsSourcePreview ? "source-scene-preview:" + scenario :
                PlayerForceId is int force ? $"campaign:{scenario}:force:{force}" : "scenario-selection:" + scenario;
        }
    }
}

internal sealed record SceneAudioBindingState(string? SceneKey, long? SceneRevision, int ChannelCount);

/// <summary>Owns application attachment and subscription for a scene-audio node.
/// The session remains the sole channel owner. Context changes do not select or
/// play a cue; the cue graph and its supplied manager inputs remain separate.</summary>
internal sealed partial class SceneAudioBinding : IDisposable
{
    private readonly SceneAudioSession _session;
    private readonly SceneAudioSession _musicSession;
    private bool _retainMusicForTitle;
    private GameplaySession? _playableSession;
    private CancellationTokenSource? _mapEntry;
    internal MapMusicEntrySelection? MapMusicSelection { get; private set; }
    internal string? MapMusicError { get; private set; }
    internal SceneAudioChannel? MapMusicChannel => _musicSession.CurrentMapMusicChannel;
    internal string MapMusicStatus => MapMusicChannel?.Status ?? (MapMusicError is not null ? "unavailable" :
        _playableSession is not null ? "preparing" : "inactive");
    internal event Action? MusicChanged;

    public SceneAudioBinding(Node host, AudioPlaybackService playback, GameResourceRepository resourceSources)
    {
        _session = new SceneAudioSession { Name = "OriginalSceneAudioSession" };
        host.AddChild(_session);
        var repository = resourceSources.CreateReadScope();
        var streams = new AudioRepository(repository.SourceDirectory, repository.OverrideDirectory);
        _session.Configure(playback, repository, streams);
        _session.ChannelChanged += PublishChannel;
        _musicSession = new SceneAudioSession { Name = "OriginalApplicationMusicSession" };
        host.AddChild(_musicSession);
        _musicSession.Configure(playback, repository, streams);
        _musicSession.BeginScene("application-music");
        _musicSession.ChannelChanged += PublishChannel;
    }

    public event Action<SceneAudioBindingState, SceneAudioChannel>? ChannelChanged;

    public SceneAudioBindingState SetContext(SceneAudioContext context)
    {
        string key = context.Key;
        if (_session.SceneKey != key) { LeavePlayableMap(); _session.BeginScene(key); }
        return Capture();
    }

    public SceneAudioBindingState Leave()
    {
        LeavePlayableMap();
        long? revision = null;
        if (GodotObject.IsInstanceValid(_session) && _session.IsInsideTree())
        {
            _session.LeaveScene();
            revision = _session.SceneRevision;
        }
        return new(null, revision, 0);
    }

    private SceneAudioBindingState Capture() => new(_session.SceneKey, _session.SceneRevision, _session.ChannelCount);
    private void PublishChannel(SceneAudioChannel channel)
    {
        ChannelChanged?.Invoke(Capture(), channel);
        if (channel.Channel is "map-music" or "map-music:1") MusicChanged?.Invoke();
    }

    internal void EnterPlayableMap(GameplaySession campaign, string locale, MapMusicPreferences preferences,
        bool resumeNormalStory = false)
    {
        if (ReferenceEquals(_playableSession, campaign)) return;
        LeavePlayableMap();
        _retainMusicForTitle = false;
        _musicSession.ResetMapMusicPositions();
        _playableSession = campaign;
        //A loaded invocation restores its saved audio owner and Scene.Music.
        //Do not introduce a new map request before that reconstruction.
        if (resumeNormalStory)
        {
            MapMusicSelection = null; MapMusicError = null;
            MusicChanged?.Invoke();
            return;
        }
        var lifetime = new CancellationTokenSource(); _mapEntry = lifetime;
        try
        {
            int month = campaign.World.NativeMapSeason?.ThirdDate.Month ??
                throw new InvalidDataException("Current map music third-date month is unknown.");
            MapMusicSelection = preferences.SelectEntry(locale, month);
            _ = PlayEntryAsync(MapMusicSelection, lifetime);
        }
        catch (Exception error) { MapMusicError = error.Message; }
        MusicChanged?.Invoke();
    }

    // A completed period replaces the simulation session, while the map and
    // its playing music keep the same lifetime. Do not restart music per turn.
    internal void AdoptPlayableSession(GameplaySession previous, GameplaySession current)
    {
        if (ReferenceEquals(_playableSession, previous)) _playableSession = current;
    }

    private async Task PlayEntryAsync(MapMusicEntrySelection entry, CancellationTokenSource lifetime)
    {
        try
        {
            await _musicSession.PlayMapMusicCommitAsync("map-music", entry,
                [new(AudioSelectionResources.InstalledMusicBankId, 0x272C6EFB)], lifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!ReferenceEquals(_mapEntry, lifetime)) return;
            MapMusicError = error.Message;
            MusicChanged?.Invoke();
        }
    }

    internal void LeavePlayableMap()
    {
        CloseNormalStoryAudio();
        _ = EndEventTest(restore: false);
        CloseCollectionEventAudio();
        EndCollectionMovie(restore: false);
        EndPoemEvent(restore: false);
        EndCollectionMusic(restore: false);
        if (GodotObject.IsInstanceValid(_session) && _session.IsInsideTree())
        { _session.StopStorySounds(); _session.StopMonthlyTerritorySounds(); _session.StopArmySpeechVoices(); }
        StopPersonnelPresentationAudio();
        StopNarrationVoice();
        _musicEditActive = false; _musicReturnSelection = null;
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        _playableSession = null;
        if (!_retainMusicForTitle)
        {
            MapMusicSelection = null; MapMusicError = null;
            if (GodotObject.IsInstanceValid(_musicSession) && _musicSession.IsInsideTree()) _musicSession.StopMapMusic();
        }
        // Resolution can still be running before a channel exists. Leaving must
        // publish its cleared selection even when StopChannel has nothing to stop.
        MusicChanged?.Invoke();
    }

    public void Dispose()
    {
        _retainMusicForTitle = false;
        LeavePlayableMap();
        _session.ChannelChanged -= PublishChannel;
        _musicSession.ChannelChanged -= PublishChannel;
        if (GodotObject.IsInstanceValid(_musicSession) && _musicSession.IsInsideTree()) _musicSession.LeaveScene();
        ChannelChanged = null;
        MusicChanged = null;
    }

    internal void RetainMusicForTitle() => _retainMusicForTitle = true;

    internal async Task PlayMapMusicIntentAsync(MapMusicIntent intent, string locale,
        MapMusicPreferences preferences, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var entry = preferences.SelectSourceSlot(locale, intent.SourceSlot);
        await PlaySelectedMapMusicAsync(entry, intent.UseStoredPlaybackPosition,
            intent.FadeInMilliseconds, intent.FadeOutMilliseconds, false, cancellation);
    }

    internal Task RestoreMapMusicAfterReportAsync(GameplaySession campaign, MapSeasonRuntimeFrame displayed,
        bool sceneSeasonChanged, string locale, MapMusicPreferences preferences, CancellationToken cancellation)
    {
        if (!ReferenceEquals(_playableSession, campaign))
            throw new InvalidOperationException("report.music.playable_owner");
        var entry = preferences.SelectEntry(locale, displayed.ThirdDate.Month);
        //67DD80 resumes stored positions unless the report actually changed
        //the scene season. That change stops first, even for a remapped same cue.
        return PlaySelectedMapMusicAsync(entry, !sceneSeasonChanged, 1000, 1000,
            sceneSeasonChanged, cancellation);
    }

    private async Task PlaySelectedMapMusicAsync(MapMusicEntrySelection entry, bool useStoredPosition,
        int fadeInMilliseconds, int fadeOutMilliseconds, bool forceRestart, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        _mapEntry?.Cancel(); _mapEntry?.Dispose();
        var lifetime = new CancellationTokenSource(); _mapEntry = lifetime;
        MapMusicSelection = entry; MapMusicError = null; MusicChanged?.Invoke();
        try
        {
            // Cancellation of the presenting operation withholds its receipt;
            // the accepted music belongs to the application until replaced.
            await _musicSession.PlayMapMusicCommitAsync("map-music", entry,
                [new(AudioSelectionResources.InstalledMusicBankId, 0x272C6EFB)], lifetime.Token,
                useStoredPosition, fadeInMilliseconds, fadeOutMilliseconds, forceRestart).WaitAsync(cancellation);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            if (ReferenceEquals(_mapEntry, lifetime))
            { MapMusicError = error.Message; MusicChanged?.Invoke(); }
            throw;
        }
    }

    internal void ResetAudioSpecialMode() => _musicSession.ResetAudioSpecialMode();
}

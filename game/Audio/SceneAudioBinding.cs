using Godot;
using San14.Data;
using San14.Client.Gameplay;

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
    private GameplaySession? _playableSession;
    private CancellationTokenSource? _mapEntry;
    internal MapMusicEntrySelection? MapMusicSelection { get; private set; }
    internal string? MapMusicError { get; private set; }
    internal SceneAudioChannel? MapMusicChannel => _session.SceneKey is null ? null : _session.GetChannel("map-music");
    internal string MapMusicStatus => MapMusicChannel?.Status ?? (MapMusicError is not null ? "unavailable" :
        _playableSession is not null ? "preparing" : "inactive");
    internal event Action? MusicChanged;

    public SceneAudioBinding(Node host, AudioPlaybackService playback, string sourceDirectory, string? overridesDirectory)
    {
        _session = new SceneAudioSession { Name = "OriginalSceneAudioSession" };
        host.AddChild(_session);
        _session.Configure(playback, new GameResourceRepository(sourceDirectory, overridesDirectory),
            new AudioRepository(sourceDirectory, overridesDirectory));
        _session.ChannelChanged += PublishChannel;
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
        if (channel.Channel == "map-music") MusicChanged?.Invoke();
    }

    internal void EnterPlayableMap(GameplaySession campaign, string locale, MapMusicPreferences preferences)
    {
        if (ReferenceEquals(_playableSession, campaign)) return;
        LeavePlayableMap();
        _playableSession = campaign;
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
            await _session.PlayMapMusicCommitAsync("map-music", entry,
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
        if (GodotObject.IsInstanceValid(_session) && _session.IsInsideTree()) _session.StopMonthlyTerritorySounds();
        StopPersonnelPresentationAudio();
        StopNarrationVoice();
        _musicEditActive = false; _musicReturnSelection = null; _musicEditTemporaryScene = false;
        _mapEntry?.Cancel(); _mapEntry?.Dispose(); _mapEntry = null;
        _playableSession = null; MapMusicSelection = null; MapMusicError = null;
        if (GodotObject.IsInstanceValid(_session) && _session.IsInsideTree()) _session.StopChannel("map-music");
        // Resolution can still be running before a channel exists. Leaving must
        // publish its cleared selection even when StopChannel has nothing to stop.
        MusicChanged?.Invoke();
    }

    public void Dispose()
    {
        LeavePlayableMap();
        _session.ChannelChanged -= PublishChannel;
        ChannelChanged = null;
        MusicChanged = null;
    }
}

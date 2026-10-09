using Godot;
using San14.Data;
using System.Text.Json;

public partial class Main
{
    private SceneAudioSession? _sceneAudioSession;
    private Label _sceneAudioStatus = null!;
    private readonly Dictionary<string, SceneAudioChannel> _sceneAudioChannels = new(StringComparer.Ordinal);

    private void AttachSceneAudioSession(AudioPlaybackService playback)
    {
        if (_repository is null || _sceneAudioSession is not null) return;
        var session = new SceneAudioSession { Name = "OriginalSceneAudioSession" };
        AddChild(session);
        session.Configure(playback,
            new GameResourceRepository(_repository.SourceDirectory, _overrides.DirectoryPath),
            new AudioRepository(_repository.SourceDirectory, _overrides.DirectoryPath));
        session.ChannelChanged += SceneAudioChannelChanged;
        _sceneAudioSession = session;
        RefreshSceneAudioContext();
    }

    private void RefreshSceneAudioContext()
    {
        if (_sceneAudioSession is { } session)
        {
            string scenario = _world?.ScenarioKey ?? "source-catalogue";
            string key = _sceneView is not null ? "source-scene-preview:" + scenario :
                _playerForceId is int force ? $"campaign:{scenario}:force:{force}" : "scenario-selection:" + scenario;
            if (session.SceneKey != key) session.BeginScene(key);
            SetMeta("scene_audio_scene_key", session.SceneKey ?? "");
            SetMeta("scene_audio_scene_revision", session.SceneRevision);
            SetMeta("scene_audio_active_channels", _sceneAudioChannels.Count);
            // A complete selector query and manager bindings are still caller inputs.
            // Scene attachment never chooses a cue or creates a playing channel.
            SetMeta("scene_audio_selection_resolved", false);
        }
        UpdateSceneAudioStatus();
    }

    private void LeaveSceneAudioContext()
    {
        if (_sceneAudioSession is { } session && GodotObject.IsInstanceValid(session) && session.IsInsideTree())
        {
            session.LeaveScene();
            SetMeta("scene_audio_scene_revision", session.SceneRevision);
        }
        _sceneAudioChannels.Clear();
        SetMeta("scene_audio_scene_key", ""); SetMeta("scene_audio_active_channels", 0);
    }

    private void SceneAudioChannelChanged(SceneAudioChannel channel)
    {
        if (channel.Status == "stopped") _sceneAudioChannels.Remove(channel.Channel);
        else _sceneAudioChannels[channel.Channel] = channel;
        SetMeta("scene_audio_channel_snapshot_json", JsonSerializer.Serialize(channel));
        SetMeta("scene_audio_active_channels", _sceneAudioChannels.Count);
        UpdateSceneAudioStatus();
    }

    private void UpdateSceneAudioStatus()
    {
        if (_sceneAudioStatus is null || _failed || !GodotObject.IsInstanceValid(_sceneAudioStatus)) return;
        string status = _sceneAudioSession is not null ? "ready_unresolved" :
            _uiAudioAvailability == "unavailable" ? "unavailable" : "loading";
        _sceneAudioStatus.Text = _ui.Get("scene_audio_" + status);
        _sceneAudioStatus.TooltipText = _ui.Get("scene_audio_selection_unknown");
        SetMeta("scene_audio_status", status);
    }
}

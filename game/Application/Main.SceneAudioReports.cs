using Godot;
using System.Text.Json;

public partial class Main
{
    private SceneAudioBinding? _sceneAudioBinding;
    private Label _sceneAudioStatus = null!;

    private void AttachSceneAudioSession(AudioPlaybackService playback)
    {
        if (_repository is null || _sceneAudioBinding is not null) return;
        var binding = new SceneAudioBinding(this, playback, _repository.SourceDirectory, _overrides.DirectoryPath);
        binding.ChannelChanged += SceneAudioChannelChanged;
        binding.MusicChanged += UpdateSceneAudioStatus;
        _sceneAudioBinding = binding;
        RefreshSceneAudioContext();
    }

    private void RefreshSceneAudioContext()
    {
        if (_sceneAudioBinding is { } binding)
        {
            SceneAudioBindingState state = binding.SetContext(new(_world?.ScenarioKey, _playerForceId, _sceneView is not null));
            SetMeta("scene_audio_scene_key", state.SceneKey ?? "");
            SetMeta("scene_audio_scene_revision", state.SceneRevision!.Value);
            SetMeta("scene_audio_active_channels", state.ChannelCount);
            if (_session is { PlayerForceId: not null } campaign &&
                _residentMap is { IsPlayableMapReady: true } view && view.IsInsideTree() && !view.IsQueuedForDeletion())
                binding.EnterPlayableMap(campaign, _campaignComposition!.Snapshot.Locale, _preferences.MusicSelection);
            SetMeta("scene_audio_selection_resolved", binding.MapMusicSelection is not null);
        }
        UpdateSceneAudioStatus();
    }

    private void LeaveSceneAudioContext()
    {
        if (_sceneAudioBinding is { } binding && binding.Leave().SceneRevision is long revision)
            SetMeta("scene_audio_scene_revision", revision);
        SetMeta("scene_audio_scene_key", "");
        SetMeta("scene_audio_active_channels", 0);
    }

    private void SceneAudioChannelChanged(SceneAudioBindingState state, SceneAudioChannel channel)
    {
        SetMeta("scene_audio_channel_snapshot_json", JsonSerializer.Serialize(channel));
        SetMeta("scene_audio_active_channels", state.ChannelCount);
        UpdateSceneAudioStatus();
    }

    private void UpdateSceneAudioStatus()
    {
        if (_sceneAudioStatus is null || _failed || !GodotObject.IsInstanceValid(_sceneAudioStatus)) return;
        var binding = _sceneAudioBinding;
        string musicStatus = binding?.MapMusicStatus ?? "inactive";
        string status = musicStatus switch
        {
            "playing" or "paused" => "playing",
            "preparing" => "loading",
            "unavailable" => "unavailable",
            _ => binding is not null ? "ready_unresolved" :
            _uiAudioFeedback?.Status.Availability == UiAudioAvailability.Unavailable ? "unavailable" : "loading"
        };
        _sceneAudioStatus.Text = _ui.Get("scene_audio_" + status);
        _sceneAudioStatus.TooltipText = binding?.MapMusicError ?? binding?.MapMusicChannel?.Error ??
            (status == "ready_unresolved" ? _ui.Get("scene_audio_selection_unknown") : "");
        SetMeta("scene_audio_status", status);
        SetMeta("map_music_status", musicStatus);
        SetMeta("scene_audio_selection_resolved", binding?.MapMusicSelection is not null);
        if (binding?.MapMusicSelection is { } entry)
        {
            SetMeta("map_music_source_month", entry.SourceMonth);
            SetMeta("map_music_source_slot", entry.Selection.SourceSlot);
            SetMeta("map_music_index", entry.Commit.MusicIndex);
            SetMeta("map_music_cue_hash", entry.Commit.CueHash?.ToString("X8") ?? "");
        }
    }
}

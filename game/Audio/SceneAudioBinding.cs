using Godot;
using San14.Data;

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
internal sealed class SceneAudioBinding : IDisposable
{
    private readonly SceneAudioSession _session;

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
        if (_session.SceneKey != key) _session.BeginScene(key);
        return Capture();
    }

    public SceneAudioBindingState Leave()
    {
        long? revision = null;
        if (GodotObject.IsInstanceValid(_session) && _session.IsInsideTree())
        {
            _session.LeaveScene();
            revision = _session.SceneRevision;
        }
        return new(null, revision, 0);
    }

    private SceneAudioBindingState Capture() => new(_session.SceneKey, _session.SceneRevision, _session.ChannelCount);
    private void PublishChannel(SceneAudioChannel channel) => ChannelChanged?.Invoke(Capture(), channel);

    public void Dispose()
    {
        _session.ChannelChanged -= PublishChannel;
        ChannelChanged = null;
    }
}

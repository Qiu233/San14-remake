using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    private OriginalAudioOutputs? _originalOutputs;
    private OriginalAudioDeviceProfile? _originalDeviceProfile;
    internal OriginalAudioOutputs OriginalOutputs => _originalOutputs ??
        throw new InvalidOperationException("Original source output routes have not been configured.");

    internal void ConfigureOriginalOutputs(AudioMusicOutputGraph music, AudioMusicOutputGraph effects,
        OriginalAudioDeviceProfile profile)
    {
        RequireEngineThread();
        if (_originalOutputs is not null) throw new InvalidOperationException("Original output routes are already configured.");
        _originalOutputs = new(GetInstanceId(), music, effects, profile);
        _originalDeviceProfile = profile;
        InitializeSourceBgmScalar();
    }

    // A newly selected cue reads the current resource through the existing
    // repository. Replacing that resource retires all dependent playback before
    // removing its buses; unchanged selections reuse the same output owner.
    internal OriginalAudioOutputs RefreshOriginalOutputs(AudioMusicOutputGraph music, AudioMusicOutputGraph effects)
    {
        RequireEngineThread();
        var profile = _originalDeviceProfile ?? throw new InvalidOperationException("Original outputs need their initial device profile.");
        if (_originalOutputs is { } current && current.MusicGraph.Source.DecodedSha256 == music.Source.DecodedSha256)
        { current.ValidateHost(); return current; }
        foreach (string channel in _mapMusic.Keys.ToArray()) Stop(channel, "Original audio output resource was replaced.");
        ForceStopEffects();
        StopAllSpatial();
        foreach (string channel in _players.Keys.ToArray()) Stop(channel, "Original audio output resource was replaced.");
        AudioServer.Lock();
        try
        {
            RetireSourceBgmScalar();
            _originalOutputs?.Dispose(); _originalOutputs = null;
        }
        finally { AudioServer.Unlock(); }
        _originalOutputs = new(GetInstanceId(), music, effects, profile);
        InitializeSourceBgmScalar();
        return _originalOutputs;
    }
}

using Godot;

public partial class AudioPlaybackService
{
    private sealed record SpatialVoice(AudioStreamPlayer3D[] Players, AudioClip Clip, Func<float> SourceGain,
        Action? Stopped, AudioVolumeGroup Group)
    {
        internal AudioStreamPlayer3D Player => Players[0];
    }
    private readonly Dictionary<string, SpatialVoice> _spatialVoices = new(StringComparer.Ordinal);

    internal void PlaySpatial(AudioClip clip, string channel, Node3D world, Vector3 position,
        float playbackRate, Func<float> sourceGain, Action? stopped = null,
        AudioVolumeGroup volumeGroup = AudioVolumeGroup.Effects,
        AudioStreamPlayer3D.AttenuationModelEnum attenuation = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
        string? bus = null, IReadOnlyList<string>? additionalBuses = null)
    {
        RequireEngineThread();
        if (!GodotObject.IsInstanceValid(world) || !world.IsInsideTree())
            throw new InvalidOperationException("Spatial audio needs its live map world.");
        StopSpatial(channel);
        string primary = bus ?? (_originalDeviceProfile is null ? "Master" : OriginalOutputs.EffectsBus);
        string[] outputs = additionalBuses is null ? [primary] : [primary, ..additionalBuses];
        var players = outputs.Select(output => new AudioStreamPlayer3D
        {
            Name = "MapSpatialEffect", Stream = clip.Stream,
            AttenuationModel = attenuation,
            //One real Hex column interval is the project's spatial reference
            //distance. The original mixer attenuation curve remains separate.
            UnitSize = (float)San14.Data.CeHexWorldCoordinates.ColumnStep,
            PitchScale = playbackRate,
            Bus = output
        }).ToArray();
        var voice = new SpatialVoice(players, clip, sourceGain, stopped, volumeGroup);
        foreach (var player in players)
        { world.AddChild(player); player.GlobalPosition = position; }
        _spatialVoices.Add(channel, voice);
        foreach (var player in players)
        {
            player.Finished += () => ReleaseSpatial(channel, voice);
            player.TreeExiting += () => ReleaseSpatial(channel, voice);
        }
        ApplySpatialVolume(voice);
        AudioServer.Lock();
        try { foreach (var player in players) player.Play(); }
        finally { AudioServer.Unlock(); }
    }

    private void ApplySpatialVolume(SpatialVoice voice)
    {
        float gain = voice.SourceGain();
        float category = voice.Group == AudioVolumeGroup.Voice && !_voiceEnabled ? 0 : Volumes.Percent(voice.Group) / 100f;
        float volume = Mathf.LinearToDb(MathF.Min(gain * category, 4));
        foreach (var player in voice.Players) player.VolumeDb = volume;
    }

    private void ApplySpatialVolumes()
    {
        foreach (var voice in _spatialVoices.Values) ApplySpatialVolume(voice);
    }

    private void UpdateSpatialVoices() => ApplySpatialVolumes();

    internal bool HasSpatialVoice(string channel) => _spatialVoices.ContainsKey(channel);

    internal void StopSpatial(string channel)
    {
        RequireEngineThread();
        if (_spatialVoices.TryGetValue(channel, out var voice)) ReleaseSpatial(channel, voice);
    }

    private void ReleaseSpatial(string channel, SpatialVoice voice)
    {
        if (!_spatialVoices.TryGetValue(channel, out var active) || !ReferenceEquals(active, voice)) return;
        _spatialVoices.Remove(channel);
        foreach (var player in voice.Players)
        {
            if (!GodotObject.IsInstanceValid(player)) continue;
            player.Stop(); player.Stream = null;
            if (!player.IsQueuedForDeletion()) player.QueueFree();
        }
        voice.Clip.Dispose();
        voice.Stopped?.Invoke();
    }

    private void StopAllSpatial()
    {
        foreach (var row in _spatialVoices.ToArray()) ReleaseSpatial(row.Key, row.Value);
    }
}

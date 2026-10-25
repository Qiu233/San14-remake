using Godot;

public partial class AudioPlaybackService
{
    private sealed record SpatialVoice(AudioStreamPlayer3D Player, AudioClip Clip, Func<float> SourceGain, Action? Stopped);
    private readonly Dictionary<string, SpatialVoice> _spatialVoices = new(StringComparer.Ordinal);

    internal void PlaySpatial(AudioClip clip, string channel, Node3D world, Vector3 position,
        float playbackRate, Func<float> sourceGain, Action? stopped = null)
    {
        RequireEngineThread();
        if (!GodotObject.IsInstanceValid(world) || !world.IsInsideTree())
            throw new InvalidOperationException("Spatial audio needs its live map world.");
        StopSpatial(channel);
        var player = new AudioStreamPlayer3D
        {
            Name = "MapSpatialEffect", Stream = clip.Stream,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
            //One real Hex column interval is the project's spatial reference
            //distance. The original mixer attenuation curve remains separate.
            UnitSize = (float)San14.Data.CeHexWorldCoordinates.ColumnStep,
            PitchScale = playbackRate,
            Bus = _originalDeviceProfile is null ? "Master" : OriginalOutputs.EffectsBus
        };
        var voice = new SpatialVoice(player, clip, sourceGain, stopped);
        world.AddChild(player);
        player.GlobalPosition = position;
        _spatialVoices.Add(channel, voice);
        player.Finished += () => ReleaseSpatial(channel, voice);
        player.TreeExiting += () => ReleaseSpatial(channel, voice);
        ApplySpatialVolume(voice);
        player.Play();
    }

    private void ApplySpatialVolume(SpatialVoice voice)
    {
        float gain = voice.SourceGain();
        voice.Player.VolumeDb = Mathf.LinearToDb(MathF.Min(gain * Volumes.Percent(AudioVolumeGroup.Effects) / 100f, 4));
    }

    private void ApplySpatialVolumes()
    {
        foreach (var voice in _spatialVoices.Values) ApplySpatialVolume(voice);
    }

    private void UpdateSpatialVoices() => ApplySpatialVolumes();

    internal void StopSpatial(string channel)
    {
        RequireEngineThread();
        if (_spatialVoices.TryGetValue(channel, out var voice)) ReleaseSpatial(channel, voice);
    }

    private void ReleaseSpatial(string channel, SpatialVoice voice)
    {
        if (!_spatialVoices.TryGetValue(channel, out var active) || !ReferenceEquals(active, voice)) return;
        _spatialVoices.Remove(channel);
        if (GodotObject.IsInstanceValid(voice.Player))
        {
            voice.Player.Stop(); voice.Player.Stream = null;
            if (!voice.Player.IsQueuedForDeletion()) voice.Player.QueueFree();
        }
        voice.Clip.Dispose();
        voice.Stopped?.Invoke();
    }

    private void StopAllSpatial()
    {
        foreach (var row in _spatialVoices.ToArray()) ReleaseSpatial(row.Key, row.Value);
    }
}

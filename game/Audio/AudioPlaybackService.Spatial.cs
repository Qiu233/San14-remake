using Godot;

internal sealed record SpatialAudioLayer(AudioClip Clip, float PlaybackRate, Func<float> SourceGain,
    float StartOffsetSeconds = 0);
internal sealed record SpatialAudioSend(string Bus, float Gain = 1);

public partial class AudioPlaybackService
{
    private sealed class SpatialOutput(AudioStreamPlayer3D player, Func<float> sourceGain,
        float sendGain, float startOffset)
    {
        internal readonly AudioStreamPlayer3D Player = player;
        internal readonly Func<float> SourceGain = sourceGain;
        internal readonly float SendGain = sendGain, StartOffset = startOffset;
        internal bool Finished;
    }
    private sealed record SpatialVoice(SpatialOutput[] Outputs, AudioClip[] Clips,
        Action? Stopped, AudioVolumeGroup Group);
    private readonly Dictionary<string, SpatialVoice> _spatialVoices = new(StringComparer.Ordinal);

    internal void PlaySpatial(AudioClip clip, string channel, Node3D world, Vector3 position,
        float playbackRate, Func<float> sourceGain, Action? stopped = null,
        AudioVolumeGroup volumeGroup = AudioVolumeGroup.Effects,
        AudioStreamPlayer3D.AttenuationModelEnum attenuation = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
        string? bus = null, IReadOnlyList<string>? additionalBuses = null)
    {
        string primary = bus ?? (_originalDeviceProfile is null ? "Master" : OriginalOutputs.EffectsBus);
        var sends = new List<SpatialAudioSend> { new(primary) };
        if (additionalBuses is not null) sends.AddRange(additionalBuses.Select(value => new SpatialAudioSend(value)));
        PlaySpatialLayers([new(clip, playbackRate, sourceGain)], channel, world, position,
            sends, stopped, volumeGroup, attenuation);
    }

    //All parallel children belong to one cue. Ending a shorter child must not
    //retire its longer siblings or release the cue's one manager reservation.
    internal void PlaySpatialLayers(IReadOnlyList<SpatialAudioLayer> layers, string channel,
        Node3D world, Vector3 position, IReadOnlyList<SpatialAudioSend> sends, Action? stopped = null,
        AudioVolumeGroup volumeGroup = AudioVolumeGroup.Effects,
        AudioStreamPlayer3D.AttenuationModelEnum attenuation = AudioStreamPlayer3D.AttenuationModelEnum.Disabled)
    {
        RequireEngineThread();
        if (!GodotObject.IsInstanceValid(world) || !world.IsInsideTree())
            throw new InvalidOperationException("Spatial audio needs its live map world.");
        if (layers.Count == 0 || sends.Count == 0) throw new InvalidDataException("Spatial cue has no actual layer or audio send.");
        StopSpatial(channel);
        var outputs = layers.SelectMany(layer => sends.Select(send => new SpatialOutput(new AudioStreamPlayer3D
        {
            Name = "MapSpatialEffect", Stream = layer.Clip.Stream,
            AttenuationModel = attenuation,
            //One real Hex column interval is the project's spatial reference
            //distance. The original mixer attenuation curve remains separate.
            UnitSize = (float)San14.Data.CeHexWorldCoordinates.ColumnStep,
            PitchScale = layer.PlaybackRate,
            Bus = send.Bus
        }, layer.SourceGain, send.Gain, layer.StartOffsetSeconds))).ToArray();
        var voice = new SpatialVoice(outputs, layers.Select(layer => layer.Clip).Distinct(ReferenceEqualityComparer.Instance)
            .Cast<AudioClip>().ToArray(), stopped, volumeGroup);
        _spatialVoices.Add(channel, voice);
        try
        {
            foreach (var output in outputs)
            {
                var player = output.Player;
                world.AddChild(player); player.GlobalPosition = position;
                player.Finished += () =>
                {
                    output.Finished = true;
                    if (voice.Outputs.All(value => value.Finished)) ReleaseSpatial(channel, voice);
                };
                player.TreeExiting += () => ReleaseSpatial(channel, voice);
            }
            ApplySpatialVolume(voice);
            AudioServer.Lock();
            try { foreach (var output in outputs) output.Player.Play(output.StartOffset); }
            finally { AudioServer.Unlock(); }
        }
        catch { ReleaseSpatial(channel, voice); throw; }
    }

    private void ApplySpatialVolume(SpatialVoice voice)
    {
        float category = voice.Group == AudioVolumeGroup.Voice && !_voiceEnabled ? 0 : Volumes.Percent(voice.Group) / 100f;
        foreach (var output in voice.Outputs)
            output.Player.VolumeDb = Mathf.LinearToDb(MathF.Min(output.SourceGain() * category, 4) * output.SendGain);
    }

    private void ApplySpatialVolumes()
    {
        foreach (var voice in _spatialVoices.Values) ApplySpatialVolume(voice);
    }

    private void UpdateSpatialVoices() => ApplySpatialVolumes();

    internal bool HasSpatialVoice(string channel) => _spatialVoices.ContainsKey(channel);

    private San14.Data.NormalSoundHandleStatus QuerySpatialHandle(string channel)
    {
        if (!_spatialVoices.TryGetValue(channel, out var voice)) return default;
        bool paused = voice.Outputs.Any(output => !output.Finished && output.Player.StreamPaused);
        bool playing = voice.Outputs.Any(output => !output.Finished && output.Player.Playing && !output.Player.StreamPaused);
        return new(true, playing, paused);
    }

    internal void StopSpatial(string channel)
    {
        RequireEngineThread();
        if (_spatialVoices.TryGetValue(channel, out var voice)) ReleaseSpatial(channel, voice);
    }

    private void ReleaseSpatial(string channel, SpatialVoice voice)
    {
        if (!_spatialVoices.TryGetValue(channel, out var active) || !ReferenceEquals(active, voice)) return;
        _spatialVoices.Remove(channel);
        foreach (var output in voice.Outputs)
        {
            var player = output.Player;
            if (!GodotObject.IsInstanceValid(player)) continue;
            player.Stop(); player.Stream = null;
            if (!player.IsQueuedForDeletion()) player.QueueFree();
        }
        foreach (var clip in voice.Clips) clip.Dispose();
        voice.Stopped?.Invoke();
    }

    private void StopAllSpatial()
    {
        foreach (var row in _spatialVoices.ToArray()) ReleaseSpatial(row.Key, row.Value);
    }
}

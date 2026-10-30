using Godot;
using San14.Data;

internal sealed record SpatialAudioLayer(AudioClip Clip, float PlaybackRate, Func<float> SourceGain,
    float StartOffsetSeconds = 0, Func<float,float>? DistanceGain = null,
    Func<SpatialSoundCameraFrame,int,SpatialSoundSpeakerMatrix>? SourceMatrix = null)
{
    internal SpatialSoundSpeakerMatrix? ControlMatrix { get; init; }
}
internal sealed record SpatialAudioSend(string Bus, float Gain = 1, bool IsControl = false);

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
        Action? Stopped, AudioVolumeGroup Group, Func<float>? HandleGain)
    {
        internal MatrixSpatialOutput[] MatrixOutputs { get; init; } = [];
        internal OriginalAudioOutputs? OutputEpoch { get; init; }
        internal int LogicalOutputChannels { get; init; }
        internal Node3D? World { get; init; }
        internal Func<SpatialSoundCameraFrame>? ReadCamera { get; init; }
    }
    private readonly Dictionary<string, SpatialVoice> _spatialVoices = new(StringComparer.Ordinal);

    internal void PlaySpatial(AudioClip clip, string channel, Node3D world, Vector3 position,
        float playbackRate, Func<float> sourceGain, Action? stopped = null,
        AudioVolumeGroup volumeGroup = AudioVolumeGroup.Effects,
        AudioStreamPlayer3D.AttenuationModelEnum attenuation = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
        string? bus = null, IReadOnlyList<string>? additionalBuses = null,
        IReadOnlyList<SpatialAudioSend>? preparedSends = null, SpatialSoundSpeakerMatrix? controlMatrix = null)
    {
        string primary = bus ?? (_originalDeviceProfile is null ? "Master" : OriginalOutputs.EffectsBus);
        var sends = new List<SpatialAudioSend> { new(primary) };
        if (additionalBuses is not null) sends.AddRange(additionalBuses.Select(value => new SpatialAudioSend(value)));
        PlaySpatialLayers([new SpatialAudioLayer(clip, playbackRate, sourceGain) { ControlMatrix = controlMatrix }], channel, world, position,
            preparedSends ?? sends, stopped, volumeGroup, attenuation);
    }

    //All parallel children belong to one cue. Ending a shorter child must not
    //retire its longer siblings or release the cue's one manager reservation.
    internal void PlaySpatialLayers(IReadOnlyList<SpatialAudioLayer> layers, string channel,
        Node3D world, Vector3 position, IReadOnlyList<SpatialAudioSend> sends, Action? stopped = null,
        AudioVolumeGroup volumeGroup = AudioVolumeGroup.Effects,
        AudioStreamPlayer3D.AttenuationModelEnum attenuation = AudioStreamPlayer3D.AttenuationModelEnum.Disabled,
        Func<float>? handleGain = null)
    {
        RequireEngineThread();
        if (!GodotObject.IsInstanceValid(world) || !world.IsInsideTree())
            throw new InvalidOperationException("Spatial audio needs its live map world.");
        if (layers.Count == 0 || sends.Count == 0) throw new InvalidDataException("Spatial cue has no actual layer or audio send.");
        StopSpatial(channel);
        var outputs = layers.SelectMany(layer => sends.Where(send => !send.IsControl).Select(send => new SpatialOutput(new AudioStreamPlayer3D
        {
            Name = "MapSpatialEffect", Stream = layer.Clip.Stream,
            AttenuationModel = attenuation,
            //One real Hex column interval is the project's spatial reference
            //distance. The original mixer attenuation curve remains separate.
            UnitSize = (float)San14.Data.CeHexWorldCoordinates.ColumnStep,
            PitchScale = layer.PlaybackRate,
            Bus = send.Bus
        }, layer.SourceGain, send.Gain, layer.StartOffsetSeconds))).ToArray();
        var controlOutputs = layers.SelectMany(layer => sends.Where(send => send.IsControl).Select(send =>
            new MatrixSpatialOutput(new OriginalAudioPlayer { Name = "MapMonoControl", Bus = send.Bus, PitchScale = layer.PlaybackRate }, layer, send))).ToArray();
        var voice = new SpatialVoice(outputs, layers.Select(layer => layer.Clip).Distinct(ReferenceEqualityComparer.Instance)
            .Cast<AudioClip>().ToArray(), stopped, volumeGroup, handleGain)
            { MatrixOutputs = controlOutputs, OutputEpoch = controlOutputs.Length == 0 ? null : OriginalOutputs, World = world };
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
                    if (voice.Outputs.All(value => value.Finished) && voice.MatrixOutputs.All(value => value.Finished)) ReleaseSpatial(channel, voice);
                };
                player.TreeExiting += () => ReleaseSpatial(channel, voice);
            }
            foreach (var output in controlOutputs)
            {
                world.AddChild(output.Player);
                output.Player.SetSpatialMatrixClip(output.Layer.Clip,
                    output.Layer.ControlMatrix ?? throw new InvalidDataException("audio.spatial.control_matrix_source"), GameAudioSpeakerLayout.Stereo);
                output.Player.Finished += () =>
                {
                    output.Finished = true;
                    if (voice.Outputs.All(value => value.Finished) && voice.MatrixOutputs.All(value => value.Finished)) ReleaseSpatial(channel, voice);
                };
                output.Player.TreeExiting += () => ReleaseSpatial(channel, voice);
            }
            ApplySpatialVolume(voice);
            AudioServer.Lock();
            try
            {
                foreach (var output in outputs) output.Player.Play(output.StartOffset);
                foreach (var output in controlOutputs) output.Player.Play(output.Layer.StartOffsetSeconds);
            }
            finally { AudioServer.Unlock(); }
        }
        catch { ReleaseSpatial(channel, voice); throw; }
    }

    private void ApplySpatialVolume(SpatialVoice voice)
    {
        ApplySpatialMatrixVolume(voice);
        float category = voice.Group == AudioVolumeGroup.Voice && !_voiceEnabled ? 0 : Volumes.Percent(voice.Group) / 100f;
        foreach (var output in voice.Outputs)
            output.Player.VolumeDb = Mathf.LinearToDb(MathF.Min(output.SourceGain() * category, 4) * output.SendGain * (voice.HandleGain?.Invoke() ?? 1));
    }

    private void ApplySpatialVolumes()
    {
        foreach (var voice in _spatialVoices.Values) ApplySpatialVolume(voice);
    }

    private void UpdateSpatialVoices()
    {
        foreach(var (channel,voice) in _spatialVoices.ToArray())
        {
            try { ApplySpatialVolume(voice); }
            catch(Exception error) when(voice.MatrixOutputs.Length!=0)
            {
                // A changed device/output epoch cannot retain an apparently
                // playing manager handle against retired matrix buses.
                ReleaseSpatial(channel,voice); GD.PushWarning(error.Message);
            }
        }
    }

    internal bool HasSpatialVoice(string channel) => _spatialVoices.ContainsKey(channel);

    private San14.Data.NormalSoundHandleStatus QuerySpatialHandle(string channel)
    {
        if (!_spatialVoices.TryGetValue(channel, out var voice)) return default;
        bool paused = voice.Outputs.Any(output => !output.Finished && output.Player.StreamPaused) ||
            voice.MatrixOutputs.Any(output => !output.Finished && output.Player.StreamPaused);
        bool playing = voice.Outputs.Any(output => !output.Finished && output.Player.Playing && !output.Player.StreamPaused) ||
            voice.MatrixOutputs.Any(output => !output.Finished && output.Player.Playing && !output.Player.StreamPaused);
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
        foreach(var output in voice.MatrixOutputs)
        {
            var player=output.Player;
            if(!GodotObject.IsInstanceValid(player))continue;
            player.Stop();player.Stream=null;
            if(!player.IsQueuedForDeletion())player.QueueFree();
        }
        foreach (var clip in voice.Clips) clip.Dispose();
        voice.Stopped?.Invoke();
    }

    private void StopAllSpatial()
    {
        foreach (var row in _spatialVoices.ToArray()) ReleaseSpatial(row.Key, row.Value);
    }
}

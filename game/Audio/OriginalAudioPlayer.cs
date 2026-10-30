using Godot;

/// <summary>One logical voice and one gain stage. Six independent source lanes
/// use three engine-clock-synchronous players and explicit bus pair gates.
/// The players stay at unity: Godot's Surround target otherwise leaves its
/// LFE multiplier at one even when the player's volume is changed.</summary>
internal partial class OriginalAudioPlayer : Node
{
    private readonly List<AudioStreamPlayer> players = [];
    private readonly List<(string Name, AudioEffect Effect)> pairBuses = [];
    private string? gainBus;
    private string bus = "Master";
    private float volume = 1, pitch = 1;
    private bool paused;
    private long generation;
    private readonly HashSet<int> finished = [];
    internal event Action? Finished;
    internal AudioStream? Stream
    {
        get => players.Count == 0 ? null : players[0].Stream;
        set { if (value is not null) throw new InvalidOperationException("Use the owned clip setter."); ClearClip(); }
    }
    internal string Bus
    {
        get => bus;
        set { bus = value; if (gainBus is not null) AudioServer.SetBusSend(AudioServer.GetBusIndex(gainBus), bus); }
    }
    internal float VolumeLinear
    {
        get => volume;
        set { volume = value; if (gainBus is not null) AudioServer.SetBusVolumeLinear(AudioServer.GetBusIndex(gainBus), volume); }
    }
    internal float VolumeDb { get => Mathf.LinearToDb(volume); set => VolumeLinear = Mathf.DbToLinear(value); }
    internal float PitchScale
    {
        get => pitch;
        set { pitch = value; foreach (var player in players) player.PitchScale = pitch; }
    }
    internal bool StreamPaused
    {
        get => paused;
        set
        {
            AudioServer.Lock();
            try { paused = value; PauseSourceEnvelope(paused); foreach (var player in players) player.StreamPaused = paused; }
            finally { AudioServer.Unlock(); }
        }
    }
    internal bool Playing => players.Any(player => player.Playing);
    internal void SetClip(AudioClip clip)
    {
        ClearClip();
        AudioServer.Lock();
        try
        {
            gainBus = "OriginalVoiceGain" + GetInstanceId();
            int gain = AudioServer.BusCount; AudioServer.AddBus(); AudioServer.SetBusName(gain, gainBus);
            AudioServer.SetBusSend(gain, bus); AudioServer.SetBusVolumeLinear(gain, volume);
            var streams = clip.SpeakerPairs ?? [clip.Stream];
            for (int pair = 0; pair < streams.Count; pair++)
            {
                string send = gainBus;
                if (clip.SpeakerPairs is not null)
                {
                    AddDeviceSpeakerPair(clip,streams[pair],pair);
                    continue;
                }
                var player = new AudioStreamPlayer { Name = "SourcePair" + pair, Stream = streams[pair],
                    Bus = send, VolumeLinear = 1, PitchScale = pitch,
                    MixTarget = clip.SpeakerPairs is null ? AudioStreamPlayer.MixTargetEnum.Stereo : AudioStreamPlayer.MixTargetEnum.Surround };
                AddChild(player); players.Add(player);
                int identity = pair; long owner = generation;
                player.Finished += () =>
                {
                    if (owner != generation) return;
                    finished.Add(identity);
                    if (finished.Count == players.Count) Finished?.Invoke();
                };
            }
        }
        catch { ClearClip(); throw; }
        finally { AudioServer.Unlock(); }
    }
    internal void Play(float from = 0)
    {
        finished.Clear();
        AudioServer.Lock();
        try { foreach (var player in players) { player.StreamPaused = paused; player.Play(from); } }
        finally { AudioServer.Unlock(); }
    }
    internal void Stop()
    {
        AudioServer.Lock();
        try { foreach (var player in players) player.Stop(); }
        finally { AudioServer.Unlock(); }
    }
    internal float GetPlaybackPosition() => players.Count == 0 ? 0 : players[0].GetPlaybackPosition();
    private void ClearClip()
    {
        generation++; finished.Clear();
        AudioServer.Lock();
        try
        {
            ClearSpatialMatrixBindings();
            foreach (var player in players)
            { player.Stop(); player.Stream = null; RemoveChild(player); player.QueueFree(); }
            players.Clear();
            foreach (var (name, effect) in pairBuses.AsEnumerable().Reverse())
            { int index = AudioServer.GetBusIndex(name); if (index >= 0) AudioServer.RemoveBus(index); effect.Dispose(); }
            pairBuses.Clear();
            if (gainBus is not null) { int index = AudioServer.GetBusIndex(gainBus); if (index >= 0) AudioServer.RemoveBus(index); gainBus = null; }
            ClearSourceEnvelope();
        }
        finally { AudioServer.Unlock(); }
    }
    public override void _ExitTree() => ClearClip();
}

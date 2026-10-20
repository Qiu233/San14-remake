using Godot;

public partial class AudioPlaybackService
{
    private sealed class EffectVoice(uint cueId, string channel)
    {
        internal readonly uint CueId = cueId;
        internal readonly string Channel = channel;
        internal int UpdateAge;
    }
    // The source UI allocator tries eight shared slots. Remembered cue/age
    // belong to those actual voices, independently of the requesting view.
    private readonly EffectVoice?[] _effects = new EffectVoice?[8];
    private int _effectCursor;

    internal bool TryPlayEffect(AudioClip clip)
    {
        RequireEngineThread();
        uint cueId = clip.Cue?.CueId ?? throw new InvalidDataException("An Effects request needs its actual source cue identity.");
        // Admission precedes cleanup. A recently ended voice retains its
        // remembered cue until the audio lifecycle update clears that record.
        foreach (var effect in _effects)
            if (effect is not null && effect.CueId == cueId && effect.UpdateAge < 3) return false;
        for (int attempt = 0; attempt < _effects.Length; attempt++)
        {
            int slot = _effectCursor;
            _effectCursor = (_effectCursor + 1) % _effects.Length;
            if (_effects[slot] is { } occupied && IsEffectActive(occupied)) continue;
            string channel = "effects:" + slot;
            string bus = _originalDeviceProfile is null ? "Master" : OriginalOutputs.EffectsBus;
            _originalOutputs?.ValidateHost();
            Play(clip, channel, bus: bus, volumeGroup: AudioVolumeGroup.Effects);
            _effects[slot] = new(cueId, channel);
            return true;
        }
        return false;
    }

    private bool IsEffectActive(EffectVoice effect) =>
        _players.TryGetValue(effect.Channel, out var voice) && voice.Player.Stream is not null &&
        (voice.Player.Playing || voice.Player.StreamPaused);

    // One invocation from the existing audio service update owner. This is
    // neither wall time nor an Engine frame-number lookup; source scheduler
    // frequency equivalence remains separate from the three-update rule.
    private void UpdateEffects()
    {
        for (int slot = 0; slot < _effects.Length; slot++)
        {
            if (_effects[slot] is not { } effect) continue;
            if (!IsEffectActive(effect))
            {
                Stop(effect.Channel);
                _effects[slot] = null;
            }
            else effect.UpdateAge = unchecked(effect.UpdateAge + 1);
        }
    }

    internal void StopEffects()
    {
        RequireEngineThread();
        foreach (var effect in _effects)
            if (effect is not null) Stop(effect.Channel);
        Array.Clear(_effects);
    }
}

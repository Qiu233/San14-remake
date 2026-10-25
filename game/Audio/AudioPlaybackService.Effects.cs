using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    private sealed class EffectVoice(int managerId, string channel)
    {
        internal readonly int ManagerId = managerId;
        internal readonly string Channel = channel;
        internal int UpdateAge;
        internal float TimeoutUpdates;
        internal bool Reserved;
        internal object? Owner;
        internal CancellationTokenSource? Preparation;
        internal AudioClip? OwnedClip;
    }
    //One freshly constructed shared manager, including UI and script requests.
    //Ordinary requests allocate only0..7; all62 actual slots participate in
    //the duplicate and stop queries. Empty slots have no imported handle state.
    private readonly EffectVoice?[] _effects = new EffectVoice?[62];
    private int _effectCursor;

    internal bool TryPlayEffect(AudioClip clip)
    {
        RequireEngineThread();
        uint cueId = clip.Cue?.CueId ?? throw new InvalidDataException("An Effects request needs its actual source cue identity.");
        int managerId = NormalSoundSource.ManagerId(cueId);
        var effect = ReserveEffect(managerId, null, null, 0);
        if (effect is null) return false;
        try { InstallEffect(effect, clip, AudioVolumeGroup.Effects); return true; }
        catch { RemoveEffect(effect); throw; }
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
            if (effect.Preparation?.IsCancellationRequested == true ||
                effect.TimeoutUpdates != 0 && effect.UpdateAge >= effect.TimeoutUpdates ||
                !effect.Reserved && !IsEffectActive(effect))
            {
                RemoveEffect(effect);
            }
            else effect.UpdateAge = unchecked(effect.UpdateAge + 1);
        }
    }

    internal void StopEffects()
    {
        RequireEngineThread();
        foreach (var effect in _effects)
            if (effect is not null) RemoveEffect(effect);
    }

    private EffectVoice? ReserveEffect(int managerId, object? owner,
        CancellationTokenSource? preparation, float timeoutUpdates)
    {
        //Admission precedes cleanup, as in404E30. Recently ended requests
        //retain their remembered ID until the real manager update removes it.
        foreach (var effect in _effects)
            if (effect is not null && effect.ManagerId == managerId && effect.UpdateAge < 3) return null;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            int slot = _effectCursor; _effectCursor = (_effectCursor + 1) % 8;
            if (_effects[slot] is { } current && (current.Reserved || IsEffectActive(current))) continue;
            if (_effects[slot] is { } retired) RemoveEffect(retired);
            var selected = new EffectVoice(managerId, "effects:" + slot)
                { Owner = owner, Preparation = preparation, Reserved = true, TimeoutUpdates = timeoutUpdates };
            _effects[slot] = selected;
            return selected;
        }
        return null;
    }

    private void InstallEffect(EffectVoice effect, AudioClip clip, AudioVolumeGroup category)
    {
        string bus = _originalDeviceProfile is null ? "Master" : OriginalOutputs.EffectsBus;
        _originalOutputs?.ValidateHost();
        Play(clip, effect.Channel, bus: bus, volumeGroup: category);
        effect.Reserved = false;
    }

    private void RemoveEffect(EffectVoice effect)
    {
        int slot = Array.IndexOf(_effects, effect);
        if (slot < 0) return;
        _effects[slot] = null;
        effect.Preparation?.Cancel();
        Stop(effect.Channel);
        effect.OwnedClip?.Dispose(); effect.OwnedClip = null;
    }
}

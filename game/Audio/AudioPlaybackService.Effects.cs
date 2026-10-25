using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    private sealed class EffectVoice(int slot, int managerId, string channel)
    {
        internal readonly int Slot = slot;
        internal readonly int ManagerId = managerId;
        internal readonly string Channel = channel;
        internal bool Reserved;
        internal object? Owner;
        internal CancellationTokenSource? Preparation;
        internal AudioClip? OwnedClip;
    }
    //One freshly constructed shared manager, including UI and script requests.
    //Ordinary requests allocate only0..7; all62 actual slots participate in
    //the duplicate and stop queries. Empty slots have no imported handle state.
    private readonly EffectVoice?[] _effects = new EffectVoice?[NormalSoundManager.SlotCount];
    private readonly NormalSoundManager _normalSounds = new();

    internal bool TryPlayEffect(AudioClip clip)
    {
        RequireEngineThread();
        uint cueId = clip.Cue?.CueId ?? throw new InvalidDataException("An Effects request needs its actual source cue identity.");
        int managerId = NormalSoundSource.ManagerId(cueId);
        var effect = ReserveEffect(managerId, null, null);
        if (effect is null) return false;
        try { InstallEffect(effect, clip, AudioVolumeGroup.Effects); return true; }
        catch { RemoveEffect(effect); throw; }
    }

    private NormalSoundHandleStatus QueryEffectHandle(int slot) => QueryEffectHandle(slot, includeReservation: true);

    private NormalSoundHandleStatus QueryEffectHandle(int slot, bool includeReservation)
    {
        if (_effects[slot] is not { } effect) return default;
        if (effect.Preparation?.IsCancellationRequested == true) return new(true, false, false);
        bool playing = false, paused = false;
        if (_players.TryGetValue(effect.Channel, out var voice) && voice.Player.Stream is not null)
        { paused = voice.Player.StreamPaused; playing = voice.Player.Playing && !paused; }
        return new(true, playing || includeReservation && effect.Reserved, paused);
    }

    // One invocation from the existing audio service update owner. This is
    // neither wall time nor an Engine frame-number lookup; source scheduler
    // frequency equivalence remains separate from the three-update rule.
    private void UpdateEffects()
    {
        _normalSounds.Update(_repository is not null, QueryEffectHandle, RetireEffect);
    }

    internal void StopEffects()
    {
        RequireEngineThread();
        _normalSounds.StopAll(QueryEffectHandle, RetireEffect);
    }

    private EffectVoice? ReserveEffect(int managerId, object? owner,
        CancellationTokenSource? preparation)
    {
        //Admission precedes cleanup, as in404E30. Recently ended requests
        //retain their remembered ID until the real manager update removes it.
        EffectVoice? selected = null;
        var context = new NormalSoundManagerContext(_repository is not null, !_lifetime.IsCancellationRequested,
            IsInsideTree() && _decoder is not null, false);
        //UI and story callers supply the source's zero timeout. The shared
        //Data manager owns remembered IDs/cursors; this array owns real handles.
        var admission = _normalSounds.TryAllocateOrdinary(managerId, 0, context, QueryEffectHandle, request =>
        {
            selected = new(request.Slot, request.ManagerId, "effects:" + request.Slot)
                { Owner = owner, Preparation = preparation, Reserved = true };
            _effects[request.Slot] = selected;
            return true;
        }, RetireEffect);
        return admission.Allocated ? selected : null;
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
        if (!ReferenceEquals(_effects[effect.Slot], effect)) return;
        _normalSounds.StopSlot(effect.Slot, QueryEffectHandle, RetireEffect);
    }

    private void RetireEffect(NormalSoundRetirement retirement)
    {
        if (_effects[retirement.Slot] is not { } effect) return;
        _effects[retirement.Slot] = null;
        effect.Preparation?.Cancel();
        Stop(effect.Channel);
        effect.OwnedClip?.Dispose(); effect.OwnedClip = null;
    }
}

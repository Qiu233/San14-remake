using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    private MusicMixerClock? _effectClock;

    private void InstallEffectEnvelope(EffectVoice effect, AudioClip clip)
    {
        var group = clip.StaticPlayback?.Group;
        effect.ReleaseSeconds = group?.StopReleaseSeconds ?? 0;
        effect.ReleaseCurve = group?.ReleaseCurve ?? AudioCueEnvelopeCurve.Linear;
        float seconds = effect.FadeInMilliseconds is float milliseconds ? Math.Max(0, milliseconds / 1000f) :
            group?.StartEnvelopeSeconds ?? 0;
        var curve = effect.FadeInMilliseconds is null ? group?.AttackCurve ?? AudioCueEnvelopeCurve.Linear :
            AudioCueEnvelopeCurve.Linear;
        if (seconds > 0) BeginSourceEnvelope(effect, 1, seconds, curve, stop: false, fromZero: true);
    }

    private void BeginSourceEnvelope(EffectVoice effect, float target, float seconds,
        AudioCueEnvelopeCurve curve, bool stop, bool fromZero = false)
    {
        uint ticks = AudioCueEnvelope.DurationTicks(Math.Max(0, seconds));
        AudioServer.Lock();
        try
        {
            if (!effect.Spatial)
            {
                float from = fromZero ? 0 : effect.FadeGain;
                if (_players.TryGetValue(effect.Channel, out var current) && ReferenceEquals(current.Effect, effect))
                {
                    if (!fromZero && current.Player.HasSourceEnvelope) from = current.Player.ReadSourceEnvelope().Gain;
                    current.Player.BeginSourceEnvelope(from, target, ticks, curve);
                }
                effect.NativeEnvelopeActive = true;
                effect.NativeEnvelopeFrom = from; effect.NativeEnvelopeTarget = target;
                effect.NativeEnvelopeTicks = ticks; effect.NativeEnvelopeCurve = curve;
                effect.EnvelopeStops = stop; effect.FadeGain = ticks == 0 ? target : from;
                if (ticks == 0 && stop) RemoveEffect(effect);
                else RefreshEffectVolume(effect);
                return;
            }
            _effectClock ??= new(GetInstanceId(), "Effects");
            ulong frames = _effectClock.ReadFrames();
            if (effect.EnvelopeInterval is not null) DrainEffectEnvelope(effect, frames);
            if (!ReferenceEquals(_effects[effect.Slot], effect)) return;
            effect.Envelope ??= new();
            effect.Envelope.Begin(fromZero ? 0 : effect.FadeGain, target, ticks, curve);
            effect.EnvelopeInterval = new(_effectClock.MixRate, frames);
            effect.EnvelopeStops = stop;
            effect.FadeGain = effect.Envelope.Value;
            if (ticks == 0 && stop) RemoveEffect(effect);
            else RefreshEffectVolume(effect);
        }
        finally { AudioServer.Unlock(); }
    }

    // Source mode0/6 uses the authored release. Repeated stop while already
    // releasing does not restart its timer. Lifecycle retirement stays immediate.
    private void ReleaseEffect(EffectVoice effect, float? explicitSeconds = null)
    {
        if (!ReferenceEquals(_effects[effect.Slot], effect)) return;
        if (explicitSeconds <= 0) { RemoveEffect(effect); return; }
        if (effect.EnvelopeStops) return;
        if (effect.Reserved || effect.Spatial && explicitSeconds is null) { RemoveEffect(effect); return; }
        float seconds = explicitSeconds ?? effect.ReleaseSeconds;
        if (seconds <= 0) { RemoveEffect(effect); return; }
        BeginSourceEnvelope(effect, 0, seconds, explicitSeconds is null ? effect.ReleaseCurve :
            AudioCueEnvelopeCurve.Linear, stop: true);
    }

    private void DrainEffectEnvelope(EffectVoice effect, ulong frames)
    {
        if (effect.Envelope is not { } envelope || effect.EnvelopeInterval is not { } interval) return;
        var actual = QueryEffectHandle(effect.Slot, includeReservation: false);
        bool running = actual.Active && !actual.Paused && CanProcess();
        if (interval.Running != running)
        { interval.SetRunning(running, frames); return; }
        effect.FadeGain = envelope.Advance(interval.Drain(frames));
        if (envelope.Complete)
        {
            effect.EnvelopeInterval = null;
            if (effect.EnvelopeStops) { RemoveEffect(effect); return; }
        }
        RefreshEffectVolume(effect);
    }

    private void UpdateSourceEffectEnvelopes()
    {
        if ((_effectClock is null || !_effects.Any(effect => effect?.EnvelopeInterval is not null)) &&
            !_effects.Any(effect => effect?.NativeEnvelopeActive == true)) return;
        AudioServer.Lock();
        try
        {
            foreach (var nativeEffect in _effects)
            {
                if (nativeEffect is not { NativeEnvelopeActive: true } ||
                    !_players.TryGetValue(nativeEffect.Channel, out var voice) ||
                    !ReferenceEquals(voice.Effect, nativeEffect) || !voice.Player.HasSourceEnvelope) continue;
                var state = voice.Player.ReadSourceEnvelope();
                nativeEffect.FadeGain = state.Gain;
                if (!state.Complete) continue;
                nativeEffect.NativeEnvelopeActive = false;
                if (nativeEffect.EnvelopeStops) RemoveEffect(nativeEffect);
            }
            if (_effectClock is null) return;
            ulong frames = _effectClock.ReadFrames();
            foreach (var effect in _effects)
                if (effect?.EnvelopeInterval is not null) DrainEffectEnvelope(effect, frames);
        }
        catch (InvalidOperationException error)
        {
            // A changed output epoch cannot resume old envelope intervals.
            ForceStopEffects();
            GD.PushWarning(error.Message);
        }
        finally { AudioServer.Unlock(); }
    }

    private void ForceStopEffects()
    {
        _normalSounds.StopAll(QueryEffectHandle, RetireEffect);
        // A timeout may already have removed manager metadata while its actual
        // release voice still occupies the shared slot.
        foreach (var effect in _effects)
            if (effect is not null) RetireEffect(new(effect.Slot, NormalSoundRetirementKind.ExplicitStop));
        _effectClock?.Dispose(); _effectClock = null;
    }
}

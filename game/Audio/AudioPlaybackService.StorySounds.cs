using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    internal bool IsRecentOrdinarySound(int managerId)
    {
        RequireEngineThread();
        return _normalSounds.IsRecentOrdinarySound(managerId);
    }

    /// <summary>Accept a real source request into the shared manager. Its actual
    /// reservation occupies a slot while the decoder prepares the voice. A
    /// completed event does not cancel an already admitted sound.</summary>
    internal void QueueStorySound(int managerId, NormalSoundPreparation source, object owner, CancellationToken sceneToken, float? fadeInMilliseconds=null)
    {
        RequireEngineThread(); sceneToken.ThrowIfCancellationRequested();
        if (_decoder is null) throw new InvalidOperationException("story.audio.sound_decoder_owner");
        OriginalOutputs.ValidateHost();
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(sceneToken, _lifetime.Token);
        var effect = ReserveEffect(managerId, owner, lifetime);
        if (effect is null) { lifetime.Dispose(); return; } //Source duplicate/full-pool suppression.
        effect.FadeInMilliseconds=fadeInMilliseconds;
        _ = PrepareStorySoundAsync(effect, source, lifetime);
    }

    private async Task PrepareStorySoundAsync(EffectVoice effect, NormalSoundPreparation source,
        CancellationTokenSource lifetime)
    {
        AudioClip? clip = null;
        try
        {
            if (source.StereoPartner is { } right)
            {
                if (_decoder is null) throw new InvalidOperationException("story.audio.sound_decoder_owner");
                var leftDecoded = await DecodeResidentAsync(source.Input, lifetime.Token).ConfigureAwait(false);
                var rightDecoded = await DecodeResidentAsync(right.Input, lifetime.Token).ConfigureAwait(false);
                var leftAudio = leftDecoded.Audio; var rightAudio = rightDecoded.Audio;
                ValidateComponent(leftAudio, source.Playback.Sample);
                ValidateComponent(rightAudio, right.Playback.Sample);
                clip = await InvokeOnEngineThreadAsync(() => OriginalOutputs.SpeakerLayout == GameAudioSpeakerLayout.Surround51
                    ? AudioStreamFactory.CreateSynchronousSurroundSpeakerPair(leftAudio, source.Playback, rightAudio, right.Playback, OriginalOutputs.SurroundDeviceRoute)
                    : AudioStreamFactory.CreateSynchronousSpeakerPair(leftAudio, source.Playback, rightAudio, right.Playback, OriginalOutputs.EffectsStereoRoute),
                    lifetime.Token, cancelled => cancelled.Dispose()).ConfigureAwait(false);
                clip = clip with { Components = new[] {
                    new AudioClipComponent(Selection(source), leftAudio.Provenance),
                    new AudioClipComponent(Selection(right), rightAudio.Provenance) } };
            }
            else
                clip = await PrepareInputAsync(source.Input, AudioLoopPolicy.SourceIfPresent, lifetime.Token).ConfigureAwait(false);
            var timing = source.Playback.Sample;
            if (source.StereoPartner is null && (clip.SampleRate != timing.SampleRate ||
                clip.Channels != timing.Channels || clip.SampleFrames != timing.DeclaredSamples))
                throw new InvalidDataException("audio.normal_sound.decoded_timing");
            clip = clip with
            {
                // Synchronous component gains are applied to their own unchanged
                // stems. The shared voice owns only the parameter-group gain.
                StaticPlayback = source.StereoPartner is null ? source.Playback :
                    source.Playback with { LinearGain = source.Playback.Group.LinearGain },
                Selection = source.StereoPartner is null ? Selection(source) : null,
                Cue = new(source.Playback.Sound.CueName, source.Playback.Sound.CueId,
                    source.Playback.Sound.CueSectionOffset, source.StereoPartner is null ? source.Playback.Sound.LeafOffset : null)
            };
            AudioClip ready = clip;
            await InvokeOnEngineThreadAsync(() =>
            {
                if (!ReferenceEquals(_effects[effect.Slot], effect) || lifetime.IsCancellationRequested)
                    throw new OperationCanceledException(lifetime.Token);
                InstallEffect(effect, ready, source.GainCategoryId == 0xF4E09144 ? AudioVolumeGroup.Effects : AudioVolumeGroup.Ambient);
                effect.OwnedClip = ready; clip = null;
                return true;
            }, lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            try
            {
                await InvokeOnEngineThreadAsync(() =>
                {
                    if (ReferenceEquals(_effects[effect.Slot], effect))
                    { RemoveEffect(effect); GD.PushWarning("Story sound: " + error.Message); }
                    return true;
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }
        finally
        {
            clip?.Dispose();
            //The CTS remains the small slot's lifetime until removal. It is
            //disposed here only after the preparation itself can no longer use it.
            await RetireStoryPreparationAsync(effect, lifetime).ConfigureAwait(false);
        }
    }

    private static AudioClipSelection Selection(NormalSoundPreparation source) =>
        new(source.SampleName, source.BankUri, source.BankHash, source.Playback.Sound.SoundId,
            source.SampleOffset, source.SampleBytes);

    private static void ValidateComponent(DecodedAudio decoded, AudioCueStaticSampleTiming timing)
    {
        if (decoded.Wave.Channels != timing.Channels || decoded.Wave.SampleRate != timing.SampleRate ||
            decoded.Wave.SampleFrames != timing.DeclaredSamples)
            throw new InvalidDataException("audio.normal_sound.decoded_component_timing");
    }

    private async Task RetireStoryPreparationAsync(EffectVoice effect, CancellationTokenSource lifetime)
    {
        try
        {
            await InvokeOnEngineThreadAsync(() =>
            {
                if (ReferenceEquals(effect.Preparation, lifetime)) effect.Preparation = null;
                return true;
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        finally { lifetime.Dispose(); }
    }

    internal void StopOrdinarySoundSlot(int slot)
    {
        RequireEngineThread();
        if ((uint)slot >= (uint)_effects.Length) return; //Source out-of-range stop returns0.
        if (_effects[slot] is { } effect) ReleaseEffect(effect);
    }

    internal void StopActiveOrdinarySounds()
    {
        RequireEngineThread();
        //The reached -3 queries actual playing flags, excluding queued/reserved
        //requests. StopAllEffects has the distinct all62 semantics.
        foreach (var effect in _effects)
            if (effect is not null && QueryEffectHandle(effect.Slot, includeReservation: false).Active)
                ReleaseEffect(effect);
    }

    internal void StopOrdinarySoundsOwnedBy(object owner)
    {
        RequireEngineThread();
        foreach (var effect in _effects)
            if (effect is not null && ReferenceEquals(effect.Owner, owner)) RemoveEffect(effect);
    }
}

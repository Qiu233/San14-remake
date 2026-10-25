using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    internal bool IsRecentOrdinarySound(int managerId)
    {
        RequireEngineThread();
        return _effects.Any(effect => effect is not null && effect.ManagerId == managerId && effect.UpdateAge < 3);
    }

    /// <summary>Accept a real source request into the shared manager. Its actual
    /// reservation occupies a slot while the decoder prepares the voice. A
    /// completed event does not cancel an already admitted sound.</summary>
    internal void QueueStorySound(int managerId, NormalSoundPreparation source, object owner, CancellationToken sceneToken)
    {
        RequireEngineThread(); sceneToken.ThrowIfCancellationRequested();
        if (_decoder is null) throw new InvalidOperationException("story.audio.sound_decoder_owner");
        OriginalOutputs.ValidateHost();
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(sceneToken, _lifetime.Token);
        var effect = ReserveEffect(managerId, owner, lifetime, 0);
        if (effect is null) { lifetime.Dispose(); return; } //Source duplicate/full-pool suppression.
        _ = PrepareStorySoundAsync(effect, source, lifetime);
    }

    private async Task PrepareStorySoundAsync(EffectVoice effect, NormalSoundPreparation source,
        CancellationTokenSource lifetime)
    {
        AudioClip? clip = null;
        try
        {
            clip = await PrepareInputAsync(source.Input, AudioLoopPolicy.SourceIfPresent, lifetime.Token).ConfigureAwait(false);
            var timing = source.Playback.Sample;
            if (clip.SampleRate != timing.SampleRate || clip.Channels != timing.Channels || clip.SampleFrames != timing.DeclaredSamples)
                throw new InvalidDataException("audio.normal_sound.decoded_timing");
            clip = clip with
            {
                StaticPlayback = source.Playback,
                Selection = new(source.SampleName, source.BankUri, source.BankHash, source.Playback.Sound.SoundId,
                    source.SampleOffset, source.SampleBytes),
                Cue = new(source.Playback.Sound.CueName, source.Playback.Sound.CueId,
                    source.Playback.Sound.CueSectionOffset, source.Playback.Sound.LeafOffset)
            };
            AudioClip ready = clip;
            await InvokeOnEngineThreadAsync(() =>
            {
                if (Array.IndexOf(_effects, effect) < 0 || lifetime.IsCancellationRequested)
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
                    if (Array.IndexOf(_effects, effect) >= 0)
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
        if (_effects[slot] is { } effect) RemoveEffect(effect);
    }

    internal void StopActiveOrdinarySounds()
    {
        RequireEngineThread();
        //The reached -3 queries actual playing flags, excluding queued/reserved
        //requests. StopAllEffects has the distinct all62 semantics.
        foreach (var effect in _effects)
            if (effect is not null && IsEffectActive(effect)) RemoveEffect(effect);
    }

    internal void StopOrdinarySoundsOwnedBy(object owner)
    {
        RequireEngineThread();
        foreach (var effect in _effects)
            if (effect is not null && ReferenceEquals(effect.Owner, owner)) RemoveEffect(effect);
    }
}

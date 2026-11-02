using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    /// <summary>Prepare unchanged source PCM with an explicitly selected device
    /// route. Its actual decoder format supplies the lane order.</summary>
    internal async Task<AudioClip> PrepareStereoRoutedStreamAsync(LoadedAudioStream source,
        StereoAudioRoute route, AudioLoopPolicy loopPolicy, CancellationToken cancellationToken,
        GameAudioSpeakerLayout speakerLayout = GameAudioSpeakerLayout.Stereo, AudioDeviceSpeakerMatrix? deviceRoute = null)
    {
        if (_decoder is null) throw new InvalidOperationException("Configure the attached audio service first.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var decoded = await DecodeResidentAsync(source, linked.Token).ConfigureAwait(false);
        var audio = decoded.Audio;
        if (audio.SpeakerLayout is not { NativeChannelMask: 0x3f } layout || !layout.AppliesTo(audio))
            throw new NotSupportedException("Selected music decoder format is not FL/FR/FC/LFE/BL/BR.");
        if (speakerLayout == GameAudioSpeakerLayout.Surround51)
            return await InvokeOnEngineThreadAsync(() => AudioStreamFactory.CreateSurroundRouted(audio,
                loopPolicy, deviceRoute ?? throw new InvalidDataException("audio.output.music_device_route")),
                linked.Token, clip => clip.Dispose()).ConfigureAwait(false);
        // Full-length channel separation is static CPU work. It never runs
        // in the engine action that installs the immutable Godot resource.
        if (route.ResidentProfile is null)
        {
            // Arbitrary matrices may be dynamic. Preserve them without retaining
            // a history of their full-length output buffers or stream resources.
            var transient = await Task.Run(() => PreparedStereoAudio.Prepare(audio, route, loopPolicy), linked.Token).ConfigureAwait(false);
            return await InvokeOnEngineThreadAsync(() => AudioStreamFactory.CreateStereoRouted(transient),
                linked.Token, clip => clip.Dispose()).ConfigureAwait(false);
        }
        var prepared = await PrepareResidentStereoPcmAsync(decoded, route, loopPolicy, linked.Token).ConfigureAwait(false);
        return await InvokeOnEngineThreadAsync(() => BorrowResidentStereoStream(decoded, prepared, route, loopPolicy),
            linked.Token, clip => clip.Dispose()).ConfigureAwait(false);
    }
}

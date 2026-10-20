using Godot;
using San14.Data;

public partial class AudioPlaybackService
{
    /// <summary>Prepare unchanged source PCM with an explicitly selected device
    /// route. A six-channel resource header alone is not a decoded-lane proof.</summary>
    internal async Task<AudioClip> PrepareStereoRoutedStreamAsync(LoadedAudioStream source,
        StereoAudioRoute route, AudioLoopPolicy loopPolicy, CancellationToken cancellationToken)
    {
        IAudioDecoder decoder = _decoder ?? throw new InvalidOperationException("Configure the attached audio service first.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        DecodedAudio audio = await decoder.DecodeAsync(source, linked.Token).ConfigureAwait(false);
        if (audio.SpeakerLayout is not { NativeChannelMask: 0x3f } layout || !layout.AppliesTo(audio))
            throw new NotSupportedException("Selected music has no complete-stream proof of its decoded six-speaker order.");
        return await InvokeOnEngineThreadAsync(() => AudioStreamFactory.CreateStereoRouted(audio, route, loopPolicy),
            linked.Token, clip => clip.Dispose()).ConfigureAwait(false);
    }
}

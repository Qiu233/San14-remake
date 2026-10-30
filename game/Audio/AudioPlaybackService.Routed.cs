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
        IAudioDecoder decoder = _decoder ?? throw new InvalidOperationException("Configure the attached audio service first.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        DecodedAudio audio = await decoder.DecodeAsync(source, linked.Token).ConfigureAwait(false);
        if (audio.SpeakerLayout is not { NativeChannelMask: 0x3f } layout || !layout.AppliesTo(audio))
            throw new NotSupportedException("Selected music decoder format is not FL/FR/FC/LFE/BL/BR.");
        return await InvokeOnEngineThreadAsync(() => speakerLayout == GameAudioSpeakerLayout.Surround51
            ? AudioStreamFactory.CreateSurroundRouted(audio, loopPolicy, deviceRoute ?? throw new InvalidDataException("audio.output.music_device_route"))
            : AudioStreamFactory.CreateStereoRouted(audio, route, loopPolicy),
            linked.Token, clip => clip.Dispose()).ConfigureAwait(false);
    }
}

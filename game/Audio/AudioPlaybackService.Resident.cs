using San14.Data;

public partial class AudioPlaybackService
{
    private sealed record AudioSourceIdentity(SourceProvenance Source, SourceProvenance? Index,
        string DecoderInputSha256, string Codec, string Extension, int EncodedBytes,
        int DeclaredEncodedBytes, AudioSampleFormat? ExpectedFormat, long? LoopBegin, long? LoopEnd);
    private readonly record struct AudioSourceSlot(string ResourceUri, string Codec, string Extension);
    private readonly record struct StereoOutputIdentity(ResidentStereoOutputProfile Profile, AudioLoopPolicy LoopPolicy);
    private sealed class ResidentAudioSource(AudioSourceIdentity identity, Task<DecodedAudio> decoding)
    {
        internal readonly AudioSourceIdentity Identity = identity;
        internal readonly Task<DecodedAudio> Decoding = decoding;
        internal volatile bool Retired;
        // PCM preparation dictionaries are protected by _residentAudioGate.
        internal readonly Dictionary<StereoOutputIdentity, Task<PreparedStereoAudio>> StereoPcm = [];
        // Godot template dictionaries belong only to the engine thread.
        internal readonly Dictionary<AudioLoopPolicy, AudioClip> PlainStreams = [];
        internal readonly Dictionary<StereoOutputIdentity, AudioClip> StereoStreams = [];
    }
    private sealed record ResidentDecodedAudio(ResidentAudioSource Owner, DecodedAudio Audio);
    private readonly object _residentAudioGate = new();
    private readonly Dictionary<AudioSourceSlot, ResidentAudioSource> _residentAudioSources = [];
    private readonly HashSet<ResidentAudioSource> _retiringAudioSources = [];

    private Task<ResidentDecodedAudio> DecodeResidentAsync(AudioDecodeInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var decoder = _decoder ?? throw new InvalidOperationException("Configure the attached audio service first.");
        var key = new AudioSourceIdentity(input.EncodedSource, input.IndexSource,
            input.DecoderInputSha256, input.Codec, input.InputExtension, input.DecoderBytes.Length,
            input.DecoderBytes.Length, input.ExpectedFormat, input.LoopStartSample, input.LoopEndSample);
        return DecodeResidentAsync(key, token => decoder.DecodeInputAsync(input, token), cancellationToken);
    }

    private Task<ResidentDecodedAudio> DecodeResidentAsync(LoadedAudioStream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var decoder = _decoder ?? throw new InvalidOperationException("Configure the attached audio service first.");
        var shape = source.Stream.Ktac;
        var key = new AudioSourceIdentity(source.EncodedSource, source.Directory.IndexSource,
            source.EncodedSha256, source.Stream.Codec, "", source.Bytes.Length, source.Stream.EncodedBytes,
            shape is null ? null : new(shape.SampleRate, shape.Channels, shape.PlayableSamples),
            shape?.HasLoop == true ? shape.LoopStartSample : null,
            shape?.HasLoop == true ? shape.LoopEndSample : null);
        return DecodeResidentAsync(key, token => decoder.DecodeAsync(source, token), cancellationToken);
    }

    private async Task<ResidentDecodedAudio> DecodeResidentAsync(AudioSourceIdentity key,
        Func<CancellationToken, Task<DecodedAudio>> decode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); _lifetime.Token.ThrowIfCancellationRequested();
        var slot = new AudioSourceSlot(key.Source.ResourceUri, key.Codec, key.Extension);
        ResidentAudioSource owner;
        ResidentAudioSource? retired = null;
        bool created = false;
        lock (_residentAudioGate)
        {
            _lifetime.Token.ThrowIfCancellationRequested();
            if (!_residentAudioSources.TryGetValue(slot, out owner!) || owner.Identity != key ||
                owner.Decoding.IsFaulted || owner.Decoding.IsCanceled)
            {
                if (owner is not null)
                {
                    owner.Retired = true;
                    _retiringAudioSources.Add(retired = owner);
                }
                // Static preparation outlives an individual caller's wait.
                var task = Task.Run(() => decode(_lifetime.Token), _lifetime.Token);
                owner = new(key, task);
                _residentAudioSources[slot] = owner;
                created = true;
            }
        }
        if (created) _ = ObserveResidentPreparation(owner.Decoding);
        if (retired is not null) QueueResidentAudioRetirement(retired);
        return new(owner, await owner.Decoding.WaitAsync(cancellationToken).ConfigureAwait(false));
    }

    private AudioClip BorrowResidentPlainStream(ResidentDecodedAudio decoded, AudioLoopPolicy loopPolicy)
    {
        RequireEngineThread();
        var owner = decoded.Owner;
        // A still-running old-generation request remains valid, but cannot
        // recreate a resident pin after its source generation has retired.
        if (owner.Retired) return AudioStreamFactory.Create(decoded.Audio, loopPolicy);
        if (!owner.PlainStreams.TryGetValue(loopPolicy, out var template))
            owner.PlainStreams.Add(loopPolicy, template = AudioStreamFactory.Create(decoded.Audio, loopPolicy));
        return template.BorrowStaticResources();
    }

    private async Task<PreparedStereoAudio> PrepareResidentStereoPcmAsync(ResidentDecodedAudio decoded,
        StereoAudioRoute route, AudioLoopPolicy loopPolicy, CancellationToken cancellationToken)
    {
        var profile = route.ResidentProfile ??
            throw new InvalidOperationException("Only declared static Windows routes have resident output buffers.");
        var key = new StereoOutputIdentity(profile, loopPolicy);
        Task<PreparedStereoAudio> task;
        bool created = false;
        lock (_residentAudioGate)
        {
            _lifetime.Token.ThrowIfCancellationRequested();
            if (decoded.Owner.Retired ||
                !decoded.Owner.StereoPcm.TryGetValue(key, out task!) || task.IsFaulted || task.IsCanceled)
            {
                task = Task.Run(() => PreparedStereoAudio.Prepare(decoded.Audio, route, loopPolicy), _lifetime.Token);
                if (!decoded.Owner.Retired) decoded.Owner.StereoPcm[key] = task;
                created = true;
            }
        }
        if (created) _ = ObserveResidentPreparation(task);
        return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private AudioClip BorrowResidentStereoStream(ResidentDecodedAudio decoded, PreparedStereoAudio prepared,
        StereoAudioRoute route, AudioLoopPolicy loopPolicy)
    {
        RequireEngineThread();
        if (decoded.Owner.Retired) return AudioStreamFactory.CreateStereoRouted(prepared);
        var key = new StereoOutputIdentity(route.ResidentProfile ??
            throw new InvalidOperationException("A resident stereo stream needs its declared output profile."), loopPolicy);
        if (!decoded.Owner.StereoStreams.TryGetValue(key, out var template))
            decoded.Owner.StereoStreams.Add(key, template = AudioStreamFactory.CreateStereoRouted(prepared));
        return template.BorrowStaticResources();
    }

    private static async Task ObserveResidentPreparation(Task preparation)
    {
        // Awaiting active callers still receive the original failure. This
        // observer also covers a static worker after its last waiter cancels.
        try { await preparation.ConfigureAwait(false); }
        catch (Exception) { }
    }

    private void QueueResidentAudioRetirement(ResidentAudioSource owner)
    {
        lock (_postGate)
            if (!_lifetime.IsCancellationRequested)
                _engineActions.Enqueue(() => RetireResidentAudioSource(owner));
        // If the service is already exiting, _retiringAudioSources remains
        // owned until RetireResidentAudio takes its final snapshot.
    }

    private void RetireResidentAudioSource(ResidentAudioSource owner)
    {
        foreach (var template in owner.PlainStreams.Values) template.Dispose();
        foreach (var template in owner.StereoStreams.Values) template.Dispose();
        owner.PlainStreams.Clear(); owner.StereoStreams.Clear();
        lock (_residentAudioGate)
        {
            owner.StereoPcm.Clear();
            _retiringAudioSources.Remove(owner);
        }
        // Resident pins are gone. A still-playing channel's independent lease
        // keeps its old immutable stream alive until its own actual release.
    }

    private void RetireResidentAudio()
    {
        // _ExitTree has stopped every player and detached its Stream.
        ResidentAudioSource[] owners;
        lock (_residentAudioGate)
        {
            owners = _residentAudioSources.Values.Concat(_retiringAudioSources).Distinct().ToArray();
            foreach (var owner in owners) owner.Retired = true;
            _residentAudioSources.Clear(); _retiringAudioSources.Clear();
        }
        foreach (var owner in owners) RetireResidentAudioSource(owner);
    }
}

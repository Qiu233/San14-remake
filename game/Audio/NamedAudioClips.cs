using San14.Data;

/// <summary>Prepare explicitly named source samples without deciding which game
/// event should play them. The selection and stream bank relationship remains
/// explicit, including duplicate memory/streaming namespaces in voice banks.</summary>
public static class NamedAudioClips
{
    public static async Task<AudioClip> PrepareAsync(AudioPlaybackService service,
        GameResourceRepository repository, AudioRepository streamRepository,
        AudioSampleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(streamRepository);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExactSampleName);
        if (!Enum.IsDefined(request.Storage)) throw new ArgumentOutOfRangeException(nameof(request));
        var selected = await Task.Run(() =>
        {
            lock (repository)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AudioSelectionBank bank = AudioSelectionResources.Load(repository, request.SelectionBankId);
                if (request.Storage == AudioSampleStorage.Internal)
                {
                    AudioSelectedInternalSample sample = AudioSelectionResources.SelectInternalSample(bank, request.ExactSampleName);
                    return (Input: sample.AsDecodeInput(), Stream: (LoadedAudioStream?)null,
                        Identity: new AudioClipSelection(sample.Name, bank.Source.ResourceUri, bank.Source.DecodedSha256,
                            sample.SampleId, sample.SourceSectionOffset, sample.SourceSectionBytes));
                }
                if (request.StreamBankId is not uint streamBankId)
                    throw new InvalidDataException("An external sample needs its explicitly selected stream bank.");
                AudioExternalSampleReference external = AudioSelectionResources.ResolveExternalSample(bank,
                    request.ExactSampleName, streamRepository.ReadDirectory(streamBankId));
                LoadedAudioStream stream = streamRepository.LoadStreamBySectionId(external.StreamResourceId, external.SampleId);
                if (stream.Stream.PayloadOffset != external.StreamPayloadOffset || stream.Stream.EncodedBytes != external.StreamEncodedBytes)
                    throw new InvalidDataException("Named external audio source range changed between selection and loading.");
                AudioNamedSample named = bank.Samples.Single(s => s.SampleId == external.SampleId &&
                    s.Name == external.Name && (s.Flags & 0x10000) != 0);
                return (Input: (AudioDecodeInput?)null, Stream: stream,
                    Identity: new AudioClipSelection(named.Name!, bank.Source.ResourceUri, bank.Source.DecodedSha256,
                        named.SampleId, named.SectionOffset, named.SectionBytes));
            }
        }, cancellationToken).ConfigureAwait(false);
        AudioClip clip = selected.Input is not null
            ? await service.PrepareInputAsync(selected.Input, request.LoopPolicy, cancellationToken).ConfigureAwait(false)
            : await service.PrepareLoadedStreamAsync(selected.Stream!, request.LoopPolicy, cancellationToken).ConfigureAwait(false);
        return clip with { Selection = selected.Identity };
    }

    public static async Task<AudioClip> PrepareInternalAsync(AudioPlaybackService service,
        GameResourceRepository repository, uint selectionBankId, string exactSampleName,
        bool enableSourceLoop = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        AudioSelectedInternalSample sample = await Task.Run(() => AudioSelectionResources.SelectInternalSample(
            AudioSelectionResources.Load(repository, selectionBankId), exactSampleName), cancellationToken);
        AudioClip clip = await service.PrepareInputAsync(sample.AsDecodeInput(), enableSourceLoop, cancellationToken);
        return clip with { Selection = new(sample.Name, sample.Source.ResourceUri, sample.Source.DecodedSha256,
            sample.SampleId, sample.SourceSectionOffset, sample.SourceSectionBytes) };
    }

    public static async Task<AudioClip> PrepareExternalAsync(AudioPlaybackService service,
        GameResourceRepository repository, AudioRepository streamRepository,
        uint selectionBankId, uint streamBankId, string exactSampleName,
        bool enableSourceLoop = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(streamRepository);
        var loaded = await Task.Run(() =>
        {
            AudioSelectionBank bank = AudioSelectionResources.Load(repository, selectionBankId);
            AudioExternalSampleReference selected = AudioSelectionResources.ResolveExternalSample(
                bank, exactSampleName,
                streamRepository.ReadDirectory(streamBankId));
            LoadedAudioStream stream = streamRepository.LoadStreamBySectionId(selected.StreamResourceId, selected.SampleId);
            if (stream.Stream.PayloadOffset != selected.StreamPayloadOffset || stream.Stream.EncodedBytes != selected.StreamEncodedBytes)
                throw new InvalidDataException("Named external audio source range changed between selection and loading.");
            AudioNamedSample sample = bank.Samples.Single(s => s.SampleId == selected.SampleId &&
                s.Name == selected.Name && (s.Flags & 0x10000) != 0);
            return (Stream: stream, Identity: new AudioClipSelection(exactSampleName, bank.Source.ResourceUri,
                bank.Source.DecodedSha256, sample.SampleId, sample.SectionOffset, sample.SectionBytes));
        }, cancellationToken);
        AudioClip clip = await service.PrepareLoadedStreamAsync(loaded.Stream, enableSourceLoop, cancellationToken);
        return clip with { Selection = loaded.Identity };
    }
}

using San14.Data;

/// <summary>Prepare explicitly named source samples without deciding which game
/// event should play them. The selection and stream bank relationship remains
/// explicit, including duplicate memory/streaming namespaces in voice banks.</summary>
public static class OriginalNamedAudioClips
{
    public static async Task<OriginalAudioClip> PrepareAsync(OriginalAudioPlaybackService service,
        OriginalGameRepository repository, OriginalAudioRepository streamRepository,
        OriginalAudioSampleRequest request, CancellationToken cancellationToken = default)
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
                OriginalAudioSelectionBank bank = OriginalAudioSelectionResources.Load(repository, request.SelectionBankId);
                if (request.Storage == OriginalAudioSampleStorage.Internal)
                {
                    OriginalAudioSelectedInternalSample sample = OriginalAudioSelectionResources.SelectInternalSample(bank, request.ExactSampleName);
                    return (Input: sample.AsDecodeInput(), Stream: (LoadedOriginalAudioStream?)null,
                        Identity: new OriginalAudioClipSelection(sample.Name, bank.Source.ResourceUri, bank.Source.DecodedSha256,
                            sample.SampleId, sample.SourceSectionOffset, sample.SourceSectionBytes));
                }
                if (request.StreamBankId is not uint streamBankId)
                    throw new InvalidDataException("An external sample needs its explicitly selected stream bank.");
                OriginalAudioExternalSampleReference external = OriginalAudioSelectionResources.ResolveExternalSample(bank,
                    request.ExactSampleName, streamRepository.ReadDirectory(streamBankId));
                LoadedOriginalAudioStream stream = streamRepository.LoadStreamBySectionId(external.StreamResourceId, external.SampleId);
                if (stream.Stream.PayloadOffset != external.StreamPayloadOffset || stream.Stream.EncodedBytes != external.StreamEncodedBytes)
                    throw new InvalidDataException("Named external audio source range changed between selection and loading.");
                OriginalAudioNamedSample named = bank.Samples.Single(s => s.SampleId == external.SampleId &&
                    s.Name == external.Name && (s.Flags & 0x10000) != 0);
                return (Input: (OriginalAudioDecodeInput?)null, Stream: stream,
                    Identity: new OriginalAudioClipSelection(named.Name!, bank.Source.ResourceUri, bank.Source.DecodedSha256,
                        named.SampleId, named.SectionOffset, named.SectionBytes));
            }
        }, cancellationToken).ConfigureAwait(false);
        OriginalAudioClip clip = selected.Input is not null
            ? await service.PrepareInputAsync(selected.Input, request.LoopPolicy, cancellationToken).ConfigureAwait(false)
            : await service.PrepareLoadedStreamAsync(selected.Stream!, request.LoopPolicy, cancellationToken).ConfigureAwait(false);
        return clip with { Selection = selected.Identity };
    }

    public static async Task<OriginalAudioClip> PrepareInternalAsync(OriginalAudioPlaybackService service,
        OriginalGameRepository repository, uint selectionBankId, string exactSampleName,
        bool enableOriginalLoop = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        OriginalAudioSelectedInternalSample sample = await Task.Run(() => OriginalAudioSelectionResources.SelectInternalSample(
            OriginalAudioSelectionResources.Load(repository, selectionBankId), exactSampleName), cancellationToken);
        OriginalAudioClip clip = await service.PrepareInputAsync(sample.AsDecodeInput(), enableOriginalLoop, cancellationToken);
        return clip with { Selection = new(sample.Name, sample.Source.ResourceUri, sample.Source.DecodedSha256,
            sample.SampleId, sample.SourceSectionOffset, sample.SourceSectionBytes) };
    }

    public static async Task<OriginalAudioClip> PrepareExternalAsync(OriginalAudioPlaybackService service,
        OriginalGameRepository repository, OriginalAudioRepository streamRepository,
        uint selectionBankId, uint streamBankId, string exactSampleName,
        bool enableOriginalLoop = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(streamRepository);
        var loaded = await Task.Run(() =>
        {
            OriginalAudioSelectionBank bank = OriginalAudioSelectionResources.Load(repository, selectionBankId);
            OriginalAudioExternalSampleReference selected = OriginalAudioSelectionResources.ResolveExternalSample(
                bank, exactSampleName,
                streamRepository.ReadDirectory(streamBankId));
            LoadedOriginalAudioStream stream = streamRepository.LoadStreamBySectionId(selected.StreamResourceId, selected.SampleId);
            if (stream.Stream.PayloadOffset != selected.StreamPayloadOffset || stream.Stream.EncodedBytes != selected.StreamEncodedBytes)
                throw new InvalidDataException("Named external audio source range changed between selection and loading.");
            OriginalAudioNamedSample sample = bank.Samples.Single(s => s.SampleId == selected.SampleId &&
                s.Name == selected.Name && (s.Flags & 0x10000) != 0);
            return (Stream: stream, Identity: new OriginalAudioClipSelection(exactSampleName, bank.Source.ResourceUri,
                bank.Source.DecodedSha256, sample.SampleId, sample.SectionOffset, sample.SectionBytes));
        }, cancellationToken);
        OriginalAudioClip clip = await service.PrepareLoadedStreamAsync(loaded.Stream, enableOriginalLoop, cancellationToken);
        return clip with { Selection = loaded.Identity };
    }
}

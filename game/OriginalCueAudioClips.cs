using San14.Data;

/// <summary>Prepare one explicit static cue reference without executing its mixer
/// graph, selector or scheduling. Multiple roots/children and unsupported opcodes
/// are errors. The caller still decides when this cue should be selected.</summary>
public static class OriginalCueAudioClips
{
    /// <summary>Follows the verified single-reference graph into one internal or
    /// external sample. External references require the caller's stream bank.</summary>
    public static async Task<OriginalAudioClip> PrepareSingleSampleAsync(
        OriginalAudioPlaybackService service, OriginalGameRepository repository,
        OriginalAudioRepository streamRepository, OriginalAudioCueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(streamRepository);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExactCueName);
        var selected = await Task.Run(() =>
        {
            // A scene owns this repository. Serialize its cache access when several
            // independent audio channels prepare source cues at the same time.
            lock (repository)
            {
                cancellationToken.ThrowIfCancellationRequested();
                OriginalAudioSelectionBank bank = OriginalAudioSelectionResources.Load(repository, request.SelectionBankId);
                OriginalAudioCueSoundReference reference = OriginalAudioCueGraphResources.ReadSingleSoundReference(bank, request.ExactCueName);
                OriginalAudioNamedSample[] matches = bank.Samples.Where(s => s.SampleId == reference.SoundId).Take(2).ToArray();
                if (matches.Length != 1 || matches[0].Name is not string name)
                    throw new NotSupportedException("Static cue must identify one named source sample.");
                OriginalAudioNamedSample sample = matches[0];
                OriginalAudioDecodeInput? internalInput = null;
                LoadedOriginalAudioStream? stream = null;
                if ((sample.Flags & 0x10000) == 0)
                    internalInput = OriginalAudioSelectionResources.SelectInternalSample(bank, name).AsDecodeInput();
                else
                {
                    if (request.StreamBankId is not uint streamBankId)
                        throw new InvalidDataException("An external cue needs its explicitly selected stream bank.");
                    OriginalAudioExternalSampleReference external = OriginalAudioSelectionResources.ResolveExternalSample(bank,
                        name, streamRepository.ReadDirectory(streamBankId));
                    stream = streamRepository.LoadStreamBySectionId(external.StreamResourceId, external.SampleId);
                    if (stream.Stream.PayloadOffset != external.StreamPayloadOffset || stream.Stream.EncodedBytes != external.StreamEncodedBytes)
                        throw new InvalidDataException("Static cue's encoded stream range changed while it was being loaded.");
                }
                return (Internal: internalInput, Stream: stream,
                    Selection: new OriginalAudioClipSelection(name, bank.Source.ResourceUri, bank.Source.DecodedSha256,
                        sample.SampleId, sample.SectionOffset, sample.SectionBytes),
                    Cue: new OriginalAudioClipCue(reference.CueName, reference.CueId, reference.CueSectionOffset, reference.LeafOffset));
            }
        }, cancellationToken).ConfigureAwait(false);
        OriginalAudioClip clip = selected.Internal is not null
            ? await service.PrepareInputAsync(selected.Internal, request.LoopPolicy, cancellationToken).ConfigureAwait(false)
            : await service.PrepareLoadedStreamAsync(selected.Stream!, request.LoopPolicy, cancellationToken).ConfigureAwait(false);
        return clip with { Selection = selected.Selection, Cue = selected.Cue };
    }

    public static async Task<OriginalAudioClip> PrepareSingleExternalSampleAsync(
        OriginalAudioPlaybackService service, OriginalGameRepository repository,
        OriginalAudioRepository streamRepository, uint selectionBankId, uint explicitStreamBankId,
        string exactCueName, bool enableOriginalLoop = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(streamRepository);
        var selected = await Task.Run(() =>
        {
            OriginalAudioSelectionBank bank = OriginalAudioSelectionResources.Load(repository, selectionBankId);
            OriginalAudioCueSoundReference reference = OriginalAudioCueGraphResources.ReadSingleSoundReference(bank, exactCueName);
            OriginalAudioNamedSample[] matches = bank.Samples.Where(s => s.SampleId == reference.SoundId && (s.Flags & 0x10000) != 0).ToArray();
            if (matches.Length != 1 || matches[0].Name is not string name)
                throw new NotSupportedException("Static cue must reference one named external sound in the explicitly chosen source bank.");
            OriginalAudioNamedSample sample = matches[0];
            OriginalAudioExternalSampleReference external = OriginalAudioSelectionResources.ResolveExternalSample(bank,
                name, streamRepository.ReadDirectory(explicitStreamBankId));
            LoadedOriginalAudioStream stream = streamRepository.LoadStreamBySectionId(external.StreamResourceId, external.SampleId);
            if (stream.Stream.PayloadOffset != external.StreamPayloadOffset || stream.Stream.EncodedBytes != external.StreamEncodedBytes)
                throw new InvalidDataException("Static cue's encoded stream range changed while it was being loaded.");
            return (Stream: stream,
                Selection: new OriginalAudioClipSelection(name, bank.Source.ResourceUri, bank.Source.DecodedSha256,
                    sample.SampleId, sample.SectionOffset, sample.SectionBytes),
                Cue: new OriginalAudioClipCue(reference.CueName, reference.CueId, reference.CueSectionOffset, reference.LeafOffset));
        }, cancellationToken);
        OriginalAudioClip clip = await service.PrepareLoadedStreamAsync(selected.Stream, enableOriginalLoop, cancellationToken);
        return clip with { Selection = selected.Selection, Cue = selected.Cue };
    }
}

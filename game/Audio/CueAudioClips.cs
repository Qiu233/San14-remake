using San14.Data;

/// <summary>Prepare one explicit static cue reference without executing its mixer
/// graph, selector or scheduling. Multiple roots/children and unsupported opcodes
/// are errors. The caller still decides when this cue should be selected.</summary>
public static class CueAudioClips
{
    /// <summary>Follows the verified single-reference graph into one internal or
    /// external sample. External references require the caller's stream bank.</summary>
    public static Task<AudioClip> PrepareSingleSampleAsync(
        AudioPlaybackService service, GameResourceRepository repository,
        AudioRepository streamRepository, AudioCueRequest request,
        CancellationToken cancellationToken = default) =>
        PrepareAsync(service, repository, streamRepository, request, false, cancellationToken);

    /// <summary>The actual UI Effects request uses the non-spatial A0 path.
    /// Preserve its static parameters and require its real Effects category.</summary>
    public static Task<AudioClip> PrepareStaticNonSpatialEffectAsync(
        AudioPlaybackService service, GameResourceRepository repository,
        AudioRepository streamRepository, AudioCueRequest request,
        CancellationToken cancellationToken = default) =>
        PrepareAsync(service, repository, streamRepository, request, true, cancellationToken);

    private static async Task<AudioClip> PrepareAsync(
        AudioPlaybackService service, GameResourceRepository repository,
        AudioRepository streamRepository, AudioCueRequest request,
        bool staticEffect, CancellationToken cancellationToken)
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
                AudioSelectionBank bank = AudioSelectionResources.Load(repository, request.SelectionBankId);
                AudioCueStaticPlaybackReference? playback = staticEffect ?
                    AudioCueGraphResources.ReadSingleStaticNonSpatialReference(bank, request.ExactCueName) : null;
                AudioCueSoundReference reference = playback?.Sound ??
                    AudioCueGraphResources.ReadSingleSoundReference(bank, request.ExactCueName);
                if (playback is not null)
                {
                    var category = repository.LoadRdbAsset("RRPreview", 0x1CC2C167);
                    var gains = AudioCueGraphResources.ReadStaticGainCategoryIds(category, playback.Group.ClassificationId);
                    if (gains.Count != 1 || gains[0] != 0xF4E09144)
                        throw new NotSupportedException("This static Effects consumer requires the original single Effects gain category.");
                }
                AudioNamedSample[] matches = bank.Samples.Where(s => s.SampleId == reference.SoundId).Take(2).ToArray();
                if (matches.Length != 1 || matches[0].Name is not string name)
                    throw new NotSupportedException("Static cue must identify one named source sample.");
                AudioNamedSample sample = matches[0];
                AudioDecodeInput? internalInput = null;
                LoadedAudioStream? stream = null;
                if ((sample.Flags & 0x10000) == 0)
                    internalInput = AudioSelectionResources.SelectInternalSample(bank, name).AsDecodeInput();
                else
                {
                    if (request.StreamBankId is not uint streamBankId)
                        throw new InvalidDataException("An external cue needs its explicitly selected stream bank.");
                    AudioExternalSampleReference external = AudioSelectionResources.ResolveExternalSample(bank,
                        name, streamRepository.ReadDirectory(streamBankId));
                    stream = streamRepository.LoadStreamBySectionId(external.StreamResourceId, external.SampleId);
                    if (stream.Stream.PayloadOffset != external.StreamPayloadOffset || stream.Stream.EncodedBytes != external.StreamEncodedBytes)
                        throw new InvalidDataException("Static cue's encoded stream range changed while it was being loaded.");
                }
                return (Internal: internalInput, Stream: stream, Playback: playback,
                    Selection: new AudioClipSelection(name, bank.Source.ResourceUri, bank.Source.DecodedSha256,
                        sample.SampleId, sample.SectionOffset, sample.SectionBytes),
                    Cue: new AudioClipCue(reference.CueName, reference.CueId, reference.CueSectionOffset, reference.LeafOffset));
            }
        }, cancellationToken).ConfigureAwait(false);
        AudioClip clip = selected.Internal is not null
            ? await service.PrepareInputAsync(selected.Internal, request.LoopPolicy, cancellationToken).ConfigureAwait(false)
            : await service.PrepareLoadedStreamAsync(selected.Stream!, request.LoopPolicy, cancellationToken).ConfigureAwait(false);
        if (selected.Playback is { Sample: var timing } &&
            (clip.SampleRate != timing.SampleRate || clip.Channels != timing.Channels || clip.SampleFrames != timing.DeclaredSamples))
        {
            clip.Stream.Dispose();
            throw new InvalidDataException("Decoded static playback timing differs from the selected source sample.");
        }
        return clip with { Selection = selected.Selection, Cue = selected.Cue, StaticPlayback = selected.Playback };
    }

    public static async Task<AudioClip> PrepareSingleExternalSampleAsync(
        AudioPlaybackService service, GameResourceRepository repository,
        AudioRepository streamRepository, uint selectionBankId, uint explicitStreamBankId,
        string exactCueName, bool enableSourceLoop = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(streamRepository);
        var selected = await Task.Run(() =>
        {
            AudioSelectionBank bank = AudioSelectionResources.Load(repository, selectionBankId);
            AudioCueSoundReference reference = AudioCueGraphResources.ReadSingleSoundReference(bank, exactCueName);
            AudioNamedSample[] matches = bank.Samples.Where(s => s.SampleId == reference.SoundId && (s.Flags & 0x10000) != 0).ToArray();
            if (matches.Length != 1 || matches[0].Name is not string name)
                throw new NotSupportedException("Static cue must reference one named external sound in the explicitly chosen source bank.");
            AudioNamedSample sample = matches[0];
            AudioExternalSampleReference external = AudioSelectionResources.ResolveExternalSample(bank,
                name, streamRepository.ReadDirectory(explicitStreamBankId));
            LoadedAudioStream stream = streamRepository.LoadStreamBySectionId(external.StreamResourceId, external.SampleId);
            if (stream.Stream.PayloadOffset != external.StreamPayloadOffset || stream.Stream.EncodedBytes != external.StreamEncodedBytes)
                throw new InvalidDataException("Static cue's encoded stream range changed while it was being loaded.");
            return (Stream: stream,
                Selection: new AudioClipSelection(name, bank.Source.ResourceUri, bank.Source.DecodedSha256,
                    sample.SampleId, sample.SectionOffset, sample.SectionBytes),
                Cue: new AudioClipCue(reference.CueName, reference.CueId, reference.CueSectionOffset, reference.LeafOffset));
        }, cancellationToken);
        AudioClip clip = await service.PrepareLoadedStreamAsync(selected.Stream, enableSourceLoop, cancellationToken);
        return clip with { Selection = selected.Selection, Cue = selected.Cue };
    }
}

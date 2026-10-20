using San14.Data;

internal static class MapMusicClips
{
    internal static async Task<AudioClip> PrepareAsync(AudioPlaybackService service, GameResourceRepository repository,
        AudioRepository streams, AudioCueRequest request, CancellationToken token)
    {
        // Capture only the prepared host route on its owner thread. IO and decode
        // happen outside the audio lock, and installation checks the current host.
        var graphs = await Task.Run(() =>
        {
            lock (repository)
            {
                token.ThrowIfCancellationRequested();
                var category = repository.LoadRdbAsset("RRPreview", 0x1CC2C167);
                return (Music: AudioMusicOutputResources.Read(category, 0x2E7FE88E),
                    Effects: AudioMusicOutputResources.Read(category, 0x2CB51744));
            }
        }, token).ConfigureAwait(false);
        var outputs = await service.InvokeOnEngineThreadAsync(() =>
            service.RefreshOriginalOutputs(graphs.Music, graphs.Effects), token).ConfigureAwait(false);
        var selected = await Task.Run(() =>
        {
            lock (repository)
            {
                token.ThrowIfCancellationRequested();
                var bank = AudioSelectionResources.Load(repository, request.SelectionBankId);
                var reference = AudioLegacyMusicResources.ReadStaticReference(bank, request.ExactCueName);
                if (reference.Sample.SourceChannelMask != 0x3f || reference.Sample.Header.Channels != 6 ||
                    reference.Sample.Header.SampleRate != 48000)
                    throw new NotSupportedException("Selected music uses an unsupported original source speaker profile.");
                var group = reference.Group; var leaf = reference.Leaf;
                if (group.ClassificationId != outputs.MusicGraph.Classification.Id || group.Flags != 0xC00000 ||
                    group.SecondaryGain != 0 || group.AdditiveRate != 0 || group.DelaySeconds != 0 ||
                    group.FilterSource != new AudioCueNonSpatialFilterSource(0, 1, 1, 1, 0, 1) ||
                    leaf.AdditiveRate != 0 || leaf.MixMultiplier != 1 || leaf.DelaySeconds != 0 ||
                    leaf.Envelope2C != 0 || leaf.Envelope30 != 0 || leaf.Envelope34 != 0)
                    throw new NotSupportedException("Selected music uses an unimplemented legacy spatial, rate, delay or envelope profile.");
                if (request.StreamBankId is not uint streamBank || reference.Sample.Sample.Name is not string sampleName)
                    throw new InvalidDataException("Selected legacy music needs its exact external stream bank and sample name.");
                var external = AudioSelectionResources.ResolveExternalSample(bank, sampleName, streams.ReadDirectory(streamBank));
                var source = streams.LoadStreamBySectionId(external.StreamResourceId, external.SampleId);
                if (source.Stream.PayloadOffset != external.StreamPayloadOffset || source.Stream.EncodedBytes != external.StreamEncodedBytes)
                    throw new InvalidDataException("Selected music stream range changed during preparation.");
                if (source.Stream.Ktac?.EncoderDelay != reference.Sample.EncoderDelaySamples)
                    throw new InvalidDataException("Selected music resource and stream have different codec-delay inputs.");
                var header = reference.Sample.Header;
                var ktac = source.Stream.Ktac!;
                // The selection count excludes codec tail padding but still
                // includes encoder delay. Only compare normalized coordinates;
                // the decoder already removed delay from the PCM and loop.
                if ((long)header.DeclaredSamples - reference.Sample.EncoderDelaySamples != ktac.PlayableSamples ||
                    (long)header.LoopStartSample - reference.Sample.EncoderDelaySamples != ktac.LoopStartSample)
                    throw new InvalidDataException("Selected music timing differs between selection and stream banks.");
                return (Reference: reference, Stream: source,
                    Selection: new AudioClipSelection(sampleName, bank.Source.ResourceUri, bank.Source.DecodedSha256,
                        reference.Sound.SoundId, reference.Sample.Sample.SectionOffset, reference.Sample.Sample.SectionBytes),
                    Cue: new AudioClipCue(reference.Sound.CueName, reference.Sound.CueId,
                        reference.Sound.CueSectionOffset, reference.Sound.LeafOffset));
            }
        }, token).ConfigureAwait(false);
        AudioClip clip = await service.PrepareStereoRoutedStreamAsync(selected.Stream, outputs.MusicStereoRoute,
            request.LoopPolicy, token).ConfigureAwait(false);
        try
        {
            var header = selected.Reference.Sample.Header;
            if (clip.SampleRate != header.SampleRate || clip.Channels != header.Channels ||
                clip.SampleFrames != (long)header.DeclaredSamples - selected.Reference.Sample.EncoderDelaySamples)
                throw new InvalidDataException("Decoded legacy music timing differs from its source header.");
            return clip with { Selection = selected.Selection, Cue = selected.Cue,
                MapMusic = new(selected.Reference.Group.PrimaryGain * selected.Reference.Leaf.LinearGain,
                    selected.Reference.Group.StopReleaseSeconds, outputs) };
        }
        catch { clip.Dispose(); throw; }
    }
}

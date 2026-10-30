namespace San14.Data;

/// <summary>Maps an already selected narration request to its installed voice
/// sample. Variant and language selection belong to the message preparation.</summary>
public static class NarrationVoiceSelection
{
    public const uint OutputResourceId = 0x1CC2C167;
    public static AudioCueBank Bank(NarrationVoiceLanguage language) => language switch
    {
        NarrationVoiceLanguage.Japanese => new(0x84A15ACF, 0x71B7C0A2),
        NarrationVoiceLanguage.Chinese => new(0x77C47425, 0x64DAD9F8),
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    /// <summary>Memory and stream registries retain their actual event bank.
    /// Some Army families dispatch a selector key to a default memory cue.</summary>
    public static AudioCueBank Bank(NarrationVoiceLanguage language, int voiceId) => IsArmySpatialVoice(voiceId)
        ? PersonVoiceResourceBinding.Bank(language, voiceId) : Bank(language);

    public static AudioSampleRequest Resolve(NarrationVoiceRequest request, AudioSelectionBank bank)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bank);
        if (request.VoiceId == 45 &&
            (request.VoiceType >= 26 || (uint)request.VariantIndex >= 5 || request.SoundId == 0 ||
             NarrationDeathVoiceDefinitions.Slots(request.Language, request.VoiceType)[request.VariantIndex] != request.SoundId))
            throw new InvalidDataException("Death narration voice differs from its source definition.");
        var binding = Bank(request.Language, request.VoiceId);
        if (bank.Source.ResourceUri != GameResourceId.Rdb("RRPreview", binding.SelectionBankId).ToString())
            throw new InvalidDataException("Narration voice bank differs from the selected voice language.");
        bool spatial = IsArmySpatialVoice(request.VoiceId);
        var cue = PersonVoiceResourceBinding.ResolveCue(bank, request.SoundId, spatial);
        var matches = bank.Samples.Where(sample => sample.SampleId == cue.CueId &&
            ((sample.Flags & 0x10000) == 0) == spatial).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Name is not { Length: > 0 } name ||
            spatial && (matches[0].Headers.Count != 1 || matches[0].Headers[0].IsExternal))
            throw new InvalidDataException($"Narration voice {request.SoundId:X8} has no unique source sample.");
        return new(binding.SelectionBankId, spatial ? null : binding.StreamBankId, name,
            spatial ? AudioSampleStorage.Internal : AudioSampleStorage.External, AudioLoopPolicy.Disabled);
    }

    /// <summary>Read the selected Army voice's actual cue parameters and
    /// classification routes. Voice volume is a current host control gain;
    /// neither the Effects category nor a guessed distance curve is added.</summary>
    public static NarrationSpatialVoicePlayback ReadSpatialPlayback(NarrationVoiceRequest request,
        AudioSelectionBank bank, LoadedRdbAsset categories)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(categories);
        if (!IsArmySpatialVoice(request.VoiceId))
            throw new InvalidDataException("Spatial narration needs an Army voice family.");
        var sample = Resolve(request, bank);
        if (categories.ResourceUri != GameResourceId.Rdb("RRPreview", OutputResourceId).ToString())
            throw new InvalidDataException("Spatial narration has a different output resource.");
        var cue = PersonVoiceResourceBinding.ResolveCue(bank, request.SoundId, true);
        var playback = AudioCueGraphResources.ReadSingleStaticSpatialReference(bank, cue.Name!, AudioSampleStorage.Internal);
        if (playback.Sound.SoundId != bank.Samples.Single(row => row.Name == sample.ExactSampleName &&
            (row.Flags & 0x10000) == 0).SampleId)
            throw new InvalidDataException("Spatial narration cue differs from its selected sound.");
        var outputs = AudioMusicOutputResources.Read(categories, playback.Group.ClassificationId);
        return new(sample, playback.LinearGain, playback.PlaybackRate, outputs, playback.Group)
            { SourceChannelMask = playback.SourceChannelMask };
    }

    internal static bool IsArmySpatialVoice(int voiceId) => voiceId is >=2 and <=7 or 9 or >=14 and <=27;
}

public sealed record NarrationSpatialVoicePlayback(AudioSampleRequest NamedSample, float Gain,
    float PlaybackRate, AudioMusicOutputGraph Outputs, AudioCueStaticGroupParameters Group)
{
    public uint? SourceChannelMask { get; init; }
}

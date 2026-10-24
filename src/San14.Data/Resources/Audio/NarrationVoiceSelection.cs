namespace San14.Data;

/// <summary>Maps an already selected narration request to its installed voice
/// sample. Variant and language selection belong to the message preparation.</summary>
public static class NarrationVoiceSelection
{
    public static AudioCueBank Bank(NarrationVoiceLanguage language) => language switch
    {
        NarrationVoiceLanguage.Japanese => new(0x84A15ACF, 0x71B7C0A2),
        NarrationVoiceLanguage.Chinese => new(0x77C47425, 0x64DAD9F8),
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    public static AudioSampleRequest Resolve(NarrationVoiceRequest request, AudioSelectionBank bank)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bank);
        var binding = Bank(request.Language);
        if (bank.Source.ResourceUri != GameResourceId.Rdb("RRPreview", binding.SelectionBankId).ToString())
            throw new InvalidDataException("Narration voice bank differs from the selected voice language.");
        var matches = bank.Samples.Where(sample => sample.SampleId == request.SoundId).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Name is not { Length: > 0 } name ||
            (matches[0].Flags & 0x10000) == 0)
            throw new InvalidDataException($"Narration voice {request.SoundId:X8} has no unique external sample.");
        return new(binding.SelectionBankId, binding.StreamBankId, name,
            AudioSampleStorage.External, AudioLoopPolicy.Disabled);
    }
}

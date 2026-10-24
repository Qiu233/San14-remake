namespace San14.Data;

/// <summary>The header experience gain's already-selected non-spatial SE31 cue.
/// Runtime uses installed resources, not the original executable's index table.</summary>
public static class ForceExperienceGainSoundSource
{
    public const uint SelectionBankId = 0x17047C02;
    public const uint CueId = 0x6D9B17D0;

    public static AudioCueRequest Resolve(AudioSelectionBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (bank.Source.ResourceUri != GameResourceId.Rdb("RRPreview", SelectionBankId).ToString())
            throw new InvalidDataException("Force experience sound needs its selected Effects bank.");
        var matches = bank.Cues.Where(cue => cue.CueId == CueId).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Name is not { Length: > 0 } name)
            throw new InvalidDataException("Force experience sound has no unique named cue.");
        //This cue has a static gain/rate chain to another sample ID. The
        //consumer must retain its cue parameters rather than play a raw sample.
        return new(SelectionBankId, null, name, AudioLoopPolicy.Disabled);
    }
}

namespace San14.Data;

/// <summary>The stream bank relationship and repeat policy are caller inputs.</summary>
public enum OriginalAudioLoopPolicy { Disabled, SourceIfPresent, RequireSource }
public sealed record OriginalAudioCueBank(uint SelectionBankId, uint? StreamBankId);
public sealed record OriginalAudioCueRequest(uint SelectionBankId, uint? StreamBankId,
    string ExactCueName, OriginalAudioLoopPolicy LoopPolicy = OriginalAudioLoopPolicy.SourceIfPresent);
public enum OriginalAudioSampleStorage { Internal, External }
public sealed record OriginalAudioSampleRequest(uint SelectionBankId, uint? StreamBankId,
    string ExactSampleName, OriginalAudioSampleStorage Storage,
    OriginalAudioLoopPolicy LoopPolicy = OriginalAudioLoopPolicy.SourceIfPresent);
public sealed record OriginalAudioMusicCueSelection(OriginalAudioCueRequest Request,
    OriginalAudioNamedCue Cue, LoadedRdbAsset Source, OriginalAudioPreparedMusicCommit Commit);

/// <summary>Matches the prepared source cue hash in explicitly chosen banks. No
/// music index is turned into a name and no bank or native query is inferred.</summary>
public static class OriginalAudioCueSelection
{
    public static OriginalAudioMusicCueSelection ResolveMusicCommit(OriginalAudioPreparedMusicCommit commit,
        IReadOnlyList<(OriginalAudioCueBank Binding, OriginalAudioSelectionBank Bank)> banks,
        OriginalAudioLoopPolicy loopPolicy = OriginalAudioLoopPolicy.SourceIfPresent)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(banks);
        if (!Enum.IsDefined(loopPolicy)) throw new ArgumentOutOfRangeException(nameof(loopPolicy));
        if (!commit.ShouldCommit || commit.RequestOpcode != 0x62 || commit.CueHash is not uint hash)
            throw new InvalidDataException("Music selection has no prepared source cue request.");
        foreach (var pair in banks)
            if (pair.Bank.Source.ResourceUri != GameResourceId.Rdb("RRPreview", pair.Binding.SelectionBankId).ToString())
                throw new InvalidDataException("Music cue bank binding differs from its source resource URI.");
        var matches = banks.SelectMany(pair => pair.Bank.Cues.Where(cue => cue.CueId == hash)
            .Select(cue => (pair.Binding, pair.Bank, Cue: cue))).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Cue.Name is not string name)
            throw new InvalidDataException("Prepared music cue hash must identify exactly one named cue in the supplied banks.");
        var selected = matches[0];
        return new(new(selected.Binding.SelectionBankId, selected.Binding.StreamBankId, name, loopPolicy),
            selected.Cue, selected.Bank.Source, commit);
    }
}

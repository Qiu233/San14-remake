using San14.Data;

/// <summary>Two exact named samples from the installed UI bank. Loading prepares
/// resources without playing them. This preserves static source keys while the
/// original cue graph, mixer volume and UI callsite mapping remain separate work.</summary>
public sealed record OriginalUiAudioClips(OriginalAudioClip ButtonSelect, OriginalAudioClip Cancel,
    string SourceBankUri, string SourceBankSha256) : IDisposable
{
    public static async Task<OriginalUiAudioClips> LoadAsync(OriginalAudioPlaybackService service,
        OriginalGameRepository repository, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        var selected = await Task.Run(() =>
        {
            OriginalAudioSelectionBank bank = OriginalAudioSelectionResources.Load(repository,
                OriginalAudioSelectionResources.InstalledUiBankId);
            return (Bank: bank,
                Select: OriginalAudioSelectionResources.SelectInternalSample(bank, OriginalAudioSelectionResources.ButtonSelectName),
                Cancel: OriginalAudioSelectionResources.SelectInternalSample(bank, OriginalAudioSelectionResources.CancelName));
        }, cancellationToken);
        OriginalAudioClip? select = null;
        try
        {
            select = await service.PrepareInputAsync(selected.Select.AsDecodeInput(), cancellationToken: cancellationToken);
            OriginalAudioClip cancel = await service.PrepareInputAsync(selected.Cancel.AsDecodeInput(), cancellationToken: cancellationToken);
            select = select with { Selection = Identity(selected.Select) };
            cancel = cancel with { Selection = Identity(selected.Cancel) };
            return new(select, cancel, selected.Bank.Source.ResourceUri, selected.Bank.Source.DecodedSha256);
        }
        catch { select?.Stream.Dispose(); throw; }
    }

    public void Dispose()
    {
        ButtonSelect.Stream.Dispose(); Cancel.Stream.Dispose();
    }

    private static OriginalAudioClipSelection Identity(OriginalAudioSelectedInternalSample selected) =>
        new(selected.Name, selected.Source.ResourceUri, selected.Source.DecodedSha256, selected.SampleId,
            selected.SourceSectionOffset, selected.SourceSectionBytes);
}

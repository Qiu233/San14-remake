using San14.Data;

/// <summary>Two exact named samples from the installed UI bank. Loading prepares
/// resources without playing them. This preserves static source keys while the
/// original cue graph, mixer volume and UI callsite mapping remain separate work.</summary>
public sealed record UiAudioClips(AudioClip ButtonSelect, AudioClip Cancel,
    string SourceBankUri, string SourceBankSha256) : IDisposable
{
    public static async Task<UiAudioClips> LoadAsync(AudioPlaybackService service,
        GameResourceRepository repository, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        var selected = await Task.Run(() =>
        {
            AudioSelectionBank bank = AudioSelectionResources.Load(repository,
                AudioSelectionResources.InstalledUiBankId);
            return (Bank: bank,
                Select: AudioSelectionResources.SelectInternalSample(bank, AudioSelectionResources.ButtonSelectName),
                Cancel: AudioSelectionResources.SelectInternalSample(bank, AudioSelectionResources.CancelName));
        }, cancellationToken);
        AudioClip? select = null;
        try
        {
            select = await service.PrepareInputAsync(selected.Select.AsDecodeInput(), cancellationToken: cancellationToken);
            AudioClip cancel = await service.PrepareInputAsync(selected.Cancel.AsDecodeInput(), cancellationToken: cancellationToken);
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

    private static AudioClipSelection Identity(AudioSelectedInternalSample selected) =>
        new(selected.Name, selected.Source.ResourceUri, selected.Source.DecodedSha256, selected.SampleId,
            selected.SourceSectionOffset, selected.SourceSectionBytes);
}

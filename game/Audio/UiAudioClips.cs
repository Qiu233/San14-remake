using San14.Data;

/// <summary>Two exact named cues from the installed UI bank, prepared through
/// their static non-spatial parameters without starting playback. Per-widget
/// selection remains the responsibility of the UI feedback caller.</summary>
public sealed record UiAudioClips(AudioClip ButtonSelect, AudioClip Cancel,
    string SourceBankUri, string SourceBankSha256) : IDisposable
{
    public static async Task<UiAudioClips> LoadAsync(AudioPlaybackService service,
        GameResourceRepository repository, AudioRepository streamRepository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(streamRepository);
        AudioClip? select = null;
        AudioClip? cancel = null;
        try
        {
            select = await Prepare(AudioSelectionResources.ButtonSelectName);
            cancel = await Prepare(AudioSelectionResources.CancelName);
            return new(select, cancel, select.Selection!.BankUri, select.Selection.BankSha256);
        }
        catch { select?.Dispose(); cancel?.Dispose(); throw; }

        Task<AudioClip> Prepare(string name) => CueAudioClips.PrepareStaticNonSpatialEffectAsync(
            service, repository, streamRepository,
            new(AudioSelectionResources.InstalledUiBankId, null, name, AudioLoopPolicy.Disabled), cancellationToken);
    }

    public void Dispose()
    {
        ButtonSelect.Dispose(); Cancel.Dispose();
    }

}

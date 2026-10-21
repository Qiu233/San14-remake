using San14.Data;

/// <summary>Exact cues from the installed UI bank, prepared through
/// their static non-spatial parameters without starting playback. Per-widget
/// selection remains the responsibility of the UI feedback caller.</summary>
public sealed record UiAudioClips(AudioClip ButtonSelect, AudioClip Cancel, AudioClip Tab,
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
        AudioClip? tab = null;
        try
        {
            select = await Prepare(AudioSelectionResources.ButtonSelectName);
            cancel = await Prepare(AudioSelectionResources.CancelName);
            // Common tab behavior selects index1 in the original UI cue table.
            tab = await Prepare("SE_000_TAB_SELECT");
            if (tab.Cue?.CueId != 0x027AB6FE)
                throw new InvalidDataException("Original common tab cue identity differs from its selected source key.");
            return new(select, cancel, tab, select.Selection!.BankUri, select.Selection.BankSha256);
        }
        catch { select?.Dispose(); cancel?.Dispose(); tab?.Dispose(); throw; }

        Task<AudioClip> Prepare(string name) => CueAudioClips.PrepareStaticNonSpatialEffectAsync(
            service, repository, streamRepository,
            new(AudioSelectionResources.InstalledUiBankId, null, name, AudioLoopPolicy.Disabled), cancellationToken);
    }

    public void Dispose()
    {
        ButtonSelect.Dispose(); Cancel.Dispose(); Tab.Dispose();
    }

}

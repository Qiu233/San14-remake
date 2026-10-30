using Godot;
using San14.Data;
using San14.Data.Formats;

internal partial class EnvironmentSettingsView
{
    private readonly List<(UiSourceVolumeButton Button, AudioVolumeGroup Group, int Value)> audioChoices = [];
    private Action<AudioVolumeOptions>? previewAudio;
    private Action? previewEffects;
    internal AudioVolumeOptions AudioDraft { get; private set; } = new();

    private void ShowPage(int index)
    {
        for (int i = 0; i < pages.Length; i++) pages[i].Visible = i == index;
        for (int i = 0; i < tabs.Count; i++) tabs[i].Selected = i == index;
        ApplyHelp(CurrentPageHelp);
    }

    private void ConfigureAudioPage(GameResourceRepository repository, PreviewText text)
    {
        var page = pages[1];
        var source = new UiSourceLayout(repository, 0xC0ACC8D5);
        var rowSource = new UiSourceLayout(repository, 0x8D9566B0);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages;
        var narration = new AdministrationMessages(repository, text.SourceLocale);
        string Data(int table, int id) => messages.Sections[table].Records[id].Text ??
            throw new InvalidDataException($"Missing environment caption {table}/{id:X4}.");
        ConfigureDisplayPage(repository,text,source,rowSource,Data);
        var volume = new UiSourceLayout(repository,0x44F4B30D);
        int[] volumeCaptions = [0x04D5,0x06C3,0x04D6,0x04D7,0x04D8];
        for (int row = 0; row < volumeCaptions.Length; row++)
        {
            var group = (AudioVolumeGroup)row;
            var mount = source.Rectangle(row + 8).Position;
            volume.Text(page,3,mount,Data(1,volumeCaptions[row]),chrome.Font);
            string hint = narration.Format(0x274F + row);
            for (int i = 0; i <= 10; i++)
            {
                int value = i * 10;
                var box = volume.Rectangle(i == 0 ? 4 : i + 12,mount);
                var button = new UiSourceVolumeButton { Name = $"EnvironmentVolume{row}_{i}",
                    Position = box.Position, Size = box.Size };
                page.AddChild(button);
                button.Configure(repository,chrome.Assets,chrome.SpriteShader,
                    i == 0 ? UiSourceVolumePart.Mute : i == 1 ? UiSourceVolumePart.Left : UiSourceVolumePart.Right,
                    () =>
                    {
                        if (abandoned || busy) return;
                        ApplyAudioOptions(AudioDraft.WithPercent(group,value));
                        if (group == AudioVolumeGroup.Effects) previewEffects?.Invoke();
                        ApplyHelp(hint);
                    });
                button.MouseEntered += () => { if (!abandoned && !busy) ApplyHelp(hint); };
                button.MouseExited += () => { if (!abandoned && !busy) ApplyHelp(defaultHelp); };
                audioChoices.Add((button,group,value));
            }
        }
        page.SetMeta("source_layout",source.Source.Source.ResourceUri);
    }

    internal void ApplyAudioOptions(AudioVolumeOptions options, bool preview = true)
    {
        if (abandoned) return;
        options.Validate(); AudioDraft = options;
        foreach (var row in audioChoices)
        {
            int steps = options.Percent(row.Group) / 10;
            row.Button.Selected = row.Value == 0 ? steps == 0 : row.Value / 10 <= steps;
        }
        // Repeat release remains an event, including at the same gain.
        if (preview) previewAudio?.Invoke(options);
    }
}

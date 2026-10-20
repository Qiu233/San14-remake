using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record AreaAdministrationFocusValues(int? SelectedFocus,
    bool? CommerceEnabled, bool? AgricultureEnabled, bool? BarracksEnabled);

/// <summary>The region's three original development radios. Availability and
/// selection come from the same draft publication; this view never appoints an
/// officer, chooses a fallback focus, or writes campaign state.</summary>
internal partial class AreaAdministrationFocusView : Control
{
    private readonly List<UiSourceRadioButton> buttons = [];
    private Action<int>? released;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, PreviewText text, Action<int> released)
    {
        this.released = released;
        Name = "AreaAdministrationFocus";
        MouseFilter = MouseFilterEnum.Ignore;
        var panel = new UiSourceLayout(repository, 0xC4464DC9);
        Size = panel.Rectangle(0).Size;
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        int[] mounts = [101, 119, 137];
        int[] captions = [0x02B3, 0x02B4, 0x02B5];
        for (int i = 0; i < mounts.Length; i++)
        {
            int focus = i + 1;
            var rectangle = panel.Rectangle(mounts[i]);
            var button = new UiSourceRadioButton { Name = "Focus" + focus,
                Position = rectangle.Position, Size = rectangle.Size, Disabled = true };
            AddChild(button);
            button.Configure(repository, assets, shader, font, (UiSourceRadioSegment)i,
                messages.Records[captions[i]].Text ??
                    throw new InvalidDataException($"Missing regional focus caption {captions[i]:X4}."),
                () => this.released?.Invoke(focus));
            button.SetMeta("source_mount_node", mounts[i]);
            buttons.Add(button);
        }
        SetMeta("source_layout", panel.Source.Source.ResourceUri);
    }

    internal void Apply(AreaAdministrationFocusValues values)
    {
        if (abandoned) return;
        if (values.SelectedFocus is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(values));
        bool?[] enabled = [values.CommerceEnabled, values.AgricultureEnabled, values.BarracksEnabled];
        for (int i = 0; i < buttons.Count; i++)
        {
            buttons[i].Disabled = enabled[i] != true;
            // Original caller clears the check when this focus is disabled.
            buttons[i].Selected = !buttons[i].Disabled && values.SelectedFocus == i + 1;
        }
    }

    internal void Abandon()
    {
        abandoned = true;
        released = null;
        foreach (var button in buttons) button.Abandon();
        Hide();
    }
}

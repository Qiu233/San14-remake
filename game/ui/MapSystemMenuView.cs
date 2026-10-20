using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum MapSystemCommand { Save, Load, Exit, Environment }

/// <summary>The map Config menu. Only supported commands receive a callback;
/// the owner retains the parent while an ordinary save window is open.</summary>
internal partial class MapSystemMenuView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<(UiSourceCommandButton Button, bool Supported)> buttons = [];
    private Action<MapSystemCommand>? selected;
    private Action? returned;
    private bool abandoned, busy;

    internal void Configure(GameResourceRepository repository, PreviewText text, int titleMessage,
        Vector2 mount, Action returned, Action<MapSystemCommand> selected)
    {
        this.returned = returned; this.selected = selected;
        Name = "MapSystemMenu"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new AdministrationPageChrome(); AddChild(chrome);
        chrome.Configure(repository, text, titleMessage, () => { if (!abandoned && !busy) this.returned?.Invoke(); }, () => { }, returnOnly: true);
        chrome.ApplyDecision(false, false);
        var source = new UiSourceLayout(repository, 0x33EF187B);
        // Node2 writes destination alpha. Nodes3/4 are Type6 blur, whose
        // compositor remains separate; neither is ordinary RGB artwork.
        source.Art(chrome.Canvas, 5, source.Rectangle(5, mount), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        foreach (var binding in new[] { (6, 0x0037), (10, 0x002E), (14, 0x0031), (34, 0x007A),
            (35, 0x0075), (18, 0x0064), (19, 0x0038), (23, 0x00D1), (27, 0x0063), (31, 0x0039), (32, 0x0033) })
        {
            int node = binding.Item1;
            MapSystemCommand? command = node switch
            { 6 => MapSystemCommand.Save, 10 => MapSystemCommand.Load, 14 => MapSystemCommand.Environment,
                32 => MapSystemCommand.Exit, _ => null };
            var box = source.Rectangle(node, mount);
            var button = new UiSourceCommandButton { Name = command is { } namedCommand ? "System" + namedCommand : "SystemCommand" + node,
                Position = box.Position, Size = box.Size, Disabled = command is null };
            chrome.Canvas.AddChild(button); buttons.Add((button, command is not null));
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                messages.Records[binding.Item2].Text ?? throw new InvalidDataException("Missing map system command caption."),
                () => { if (!abandoned && !busy && command is { } selectedCommand) this.selected?.Invoke(selectedCommand); });
            button.SetMeta("source_node_index", node);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("blur_pending", true);
    }

    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; chrome.ApplyReturn(!value);
        foreach (var row in buttons) row.Button.Disabled = value || !row.Supported;
    }

    internal void Abandon()
    {
        abandoned = true; returned = null; selected = null;
        foreach (var row in buttons) row.Button.Abandon();
        chrome?.Abandon(); Hide();
    }
}

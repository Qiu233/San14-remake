using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The change page's space-only child. Source packed-byte usage is
/// optional; the project's archive file size is never substituted for it.</summary>
internal partial class EventEditorSpaceView : Control
{
    private UiSourceText percentage = null!;
    private UiSourceLayout bar = null!;
    private Control barRoot = null!;
    private Control? fill;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private string unknown = "", format = "";
    private int? current;
    private bool initialized;
    internal void Configure(GameResourceRepository repository, PreviewText text, UiAssets assets, Shader shader, MapFont font)
    {
        this.assets = assets; this.shader = shader;
        var source = new UiSourceLayout(repository, 0x8D5C4E58);
        Size = source.Rectangle(0).Size; MouseFilter = MouseFilterEnum.Ignore;
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing event space caption.");
        source.Art(this, 1, source.Rectangle(1), assets, shader);
        source.Text(this, 2, default, Message(0x2FB), font, MapTextPresets.Create()[13]);
        percentage = source.Text(this, 3, default, "", font, MapTextPresets.Create()[0]);
        unknown = text.Get("detail_unknown"); format = Message(0x2FC);
        bar = new UiSourceLayout(repository, 0xAFC125D8);
        var box = source.Rectangle(4);
        barRoot = new Control { Position = box.Position, Size = bar.Rectangle(0).Size,
            Scale = box.Size / bar.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(barRoot); bar.Art(barRoot, 1, bar.Rectangle(1), assets, shader);
        SetMeta("source_layout", source.Source.Source.ResourceUri); Apply(null);
    }
    internal void Apply(int? bytes)
    {
        if (initialized && current == bytes) return;
        initialized = true; current = bytes;
        if (fill is { } old) { barRoot.RemoveChild(old); old.QueueFree(); fill = null; }
        if (bytes is not int used)
        { percentage.Text = unknown; SetMeta("unresolved_owner", "event-editor.source-byte-usage"); return; }
        SetMeta("unresolved_owner", "");
        percentage.Text = format.Replace("%d", ((long)used * 100 / 512000).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        int width = (int)((long)bar.Nodes[1].Value44 * Math.Clamp(used, 0, 512000) / 512000);
        if (width == 0) return;
        var changed = new UiSourceLayout(bar.Source, new Dictionary<int, UiSourceNodeGeometry>
            { [2] = new(Size: new(width, bar.Nodes[2].Value46)) });
        fill = changed.Art(barRoot, 2, changed.Rectangle(2), assets, shader, MapSpritePalettes.Create()[2]);
    }
}

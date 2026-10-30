using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record EventEditorUsagePublication(int CaptionId, int Count, int Capacity,
    int? UsedBytes, int ByteCapacity);

/// <summary>An independently mounted original event/message statistics child.</summary>
internal partial class EventEditorUsageView : Control
{
    private UiSourceText used = null!, maximum = null!, percent = null!;
    private UiSourceLayout bar = null!;
    private Control barRoot = null!;
    private Control? fill;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private string unknown = "", percentage = "";

    internal void Configure(GameResourceRepository repository, PreviewText text, UiAssets assets,
        Shader shader, MapFont font, EventEditorUsagePublication publication)
    {
        this.assets = assets; this.shader = shader; unknown = text.Get("detail_unknown");
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_31_00_event_edit_offset_00"));
        Size = source.Rectangle(0).Size; MouseFilter = MouseFilterEnum.Ignore;
        var presets = MapTextPresets.Create();
        string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data)
            .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException($"Missing event usage Data1/{id:X}.");
        source.Art(this, 1, source.Rectangle(1), assets, shader);
        source.Text(this, 2, default, Message(publication.CaptionId), font, presets[13]);
        maximum = source.Text(this, 3, default, "", font, presets[0]);
        source.Text(this, 4, default, Message(0x60C), font, presets[0]);
        used = source.Text(this, 5, default, "", font, presets[0]);
        source.Text(this, 6, default, Message(0x2FB), font, presets[13]);
        percent = source.Text(this, 7, default, "", font, presets[0]);
        percentage = Message(0x2FC);
        bar = new UiSourceLayout(repository, 0xAFC125D8);
        var box = source.Rectangle(8);
        barRoot = new() { Name = "UsageBar", Position = box.Position, Size = bar.Rectangle(0).Size,
            Scale = box.Size / bar.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(barRoot);
        bar.Art(barRoot, 1, bar.Rectangle(1), assets, shader);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Apply(publication);
    }

    internal void Apply(EventEditorUsagePublication publication)
    {
        used.Text = publication.Count.ToString(CultureInfo.InvariantCulture);
        maximum.Text = publication.Capacity.ToString(CultureInfo.InvariantCulture);
        if (fill is { } old) { barRoot.RemoveChild(old); old.QueueFree(); fill = null; }
        SetMeta("byte_usage_available", publication.UsedBytes.HasValue);
        barRoot.Visible = publication.UsedBytes.HasValue;
        if (publication.UsedBytes is not int bytes)
        {
            percent.Text = unknown;
            SetMeta("unresolved_owner", "event-editor.source-byte-usage");
            return;
        }
        SetMeta("unresolved_owner", "");
        int value = publication.ByteCapacity > 0 ? (int)((double)bytes / publication.ByteCapacity * 100.0) : 0;
        percent.Text = percentage.Replace("%d", value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        if (publication.ByteCapacity <= 0) throw new InvalidDataException("Event usage capacity must be positive.");
        int width = (int)((long)bar.Nodes[1].Value44 * Math.Clamp(bytes, 0, publication.ByteCapacity) / publication.ByteCapacity);
        if (width == 0) return;
        var changed = new UiSourceLayout(bar.Source, new Dictionary<int, UiSourceNodeGeometry>
            { [2] = new(Size: new(width, bar.Nodes[2].Value46)) });
        fill = changed.Art(barRoot, 2, changed.Rectangle(2), assets, shader, MapSpritePalettes.Create()[2]);
    }
}

using Godot;
using San14.Data;

/// <summary>A prepared event condition at the original trigger-row mount.
/// Its producer supplies indentation and each mark's actual visibility.</summary>
internal partial class UiSourceEventConditionRow : Control
{
    internal void Configure(UiSourceLayout authored, UiAssets assets, Shader shader,
        MapFont font, string value, int indentPixels, bool showBox, bool showCheck, int textPreset)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var group = authored.Nodes[19];
        var source = new UiSourceLayout(authored.Source, new Dictionary<int, UiSourceNodeGeometry>
        {
            [19] = new(Offset: new(group.HeaderValues[0] + indentPixels, -group.HeaderValues[1]))
        });
        Size = source.Rectangle(0).Size;
        if (showBox) source.Art(this, 20, source.Rectangle(20), assets, shader);
        if (showCheck) source.Art(this, 21, source.Rectangle(21), assets, shader);
        var label = source.Text(this, 22, default, "", font, MapTextPresets.Create()[textPreset]);
        label.SetKnownColourLine(value);
        SetMeta("source_layout", authored.Source.Source.ResourceUri);
        SetMeta("indent_pixels", indentPixels); SetMeta("box_visible", showBox); SetMeta("check_visible", showCheck);
    }
}

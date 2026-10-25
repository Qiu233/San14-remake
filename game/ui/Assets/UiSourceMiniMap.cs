using Godot;
using San14.Data;

/// <summary>The original small-map background and current point publication.
/// Textures and the compositor are borrowed from the containing detail view.</summary>
internal partial class UiSourceMiniMap : Control
{
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        DetailMiniMapValues? values)
    {
        Size = new(228, 256);
        MouseFilter = MouseFilterEnum.Ignore;
        var map = new UiSourceLayout(repository, 0x9F7499D5);
        map.Art(this, 1, map.Rectangle(1), assets, shader);
        SetMeta("current_points_available", values is not null);
        if (values is null) return;
        var icon = new UiSourceLayout(repository, 0xE35D91DD);
        foreach (var marker in values.Bases) Add(marker, selected: false);
        if (values.Selection is { } selection) Add(selection, selected: true);
        // The source flare_loop is an animation, not the authored scale of
        // both circle nodes. Its playback is still pending.
        SetMeta("selected_flare_pending", values.Selection is not null);

        void Add(DetailMiniMapMarker marker, bool selected)
        {
            var root = new Control
            {
                Name = selected ? "SelectedMiniMapMarker" : "MiniMapBase" + marker.Identity,
                Position = new(marker.X - 50, marker.Y - 50), Size = new(100, 100),
                MouseFilter = MouseFilterEnum.Ignore
            };
            AddChild(root);
            string sprite = marker.Kind switch
            {
                DetailMiniMapIconKind.City => "cmn_icon_map_city",
                DetailMiniMapIconKind.Gate => "cmn_icon_map_gate",
                DetailMiniMapIconKind.Area or DetailMiniMapIconKind.Force or DetailMiniMapIconKind.Person => "cmn_icon_map_unit",
                _ => throw new InvalidDataException("Unknown small-map point kind.")
            };
            var fill = MapSpritePalettes.ForceFill(marker.ColorIndex);
            icon.Art(root, 1, icon.Rectangle(1), assets, shader, new(fill, fill, fill, fill), sprite);
            root.SetMeta("hex_id", marker.HexId);
            root.SetMeta("source_color_index", marker.ColorIndex);
            root.SetMeta("icon_sprite", sprite);
        }
    }
}

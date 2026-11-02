using Godot;
using San14.Data;

/// <summary>The event directory's original wide row. Selection previews its
/// resources; an achievement flag is not permission to invent playback state.</summary>
internal partial class UiSourceEventButton : UiSourceButton
{
    internal int RowId { get; private set; }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, CollectionEventRow row, Action selected)
    {
        RowId = row.RowId;
        var source = assets.SourceLayout("09_01_01_event_list_offset_00_cn");
        Size = source.Rectangle(0).Size; ActionMode = ActionModeEnum.Release;
        int[][] art = [[3], [7, 8], [10], [12], [14, 15]];
        var states = new Control[art.Length];
        for (int state = 0; state < states.Length; state++)
        {
            var group = new Control { Name = "EventRowState" + state, Size = Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group); states[state] = group;
            foreach (int node in art[state]) source.Art(group, node, source.Rectangle(node), assets, shader,
                blend: node is 8 or 15 ? UiSourceSprite.Blend.Additive : UiSourceSprite.Blend.Ordinary);
        }
        source.Text(this, 25, default,
            row.DisplayNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", font);
        source.Text(this, 26, default, row.Name, font);
        ConfigureStates(states);
        if (row.Thumbnail is { } thumbnail)
            source.Art(this, 17, source.Rectangle(17), assets, shader,
                commonSpriteOverride: thumbnail.SpriteName, commonTexturePack: thumbnail.TexturePack);
        if (row.Unlocked == false) source.Art(this, 18, source.Rectangle(18), assets, shader);
        foreach (var marker in row.Markers)
        {
            if (marker.Image is { } image)
                source.Art(this, marker.NodeIndex, source.Rectangle(marker.NodeIndex), assets, shader,
                    commonSpriteOverride: image.SpriteName, commonTexturePack: image.TexturePack);
            else if (marker.UnresolvedOwner is { } owner) SetMeta("unresolved_marker_" + marker.NodeIndex, owner);
            else source.Art(this, marker.NodeIndex, source.Rectangle(marker.NodeIndex), assets, shader);
        }
        if (row.UnresolvedOwner is { } missing) SetMeta("unresolved_owner", missing);
        SetMeta("event_row", row.RowId); SetMeta("event_id", row.EventId);
        SetMeta("profile_known", row.Unlocked.HasValue);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Pressed += () => { if (!Disabled && SourceInputAdmitted) selected(); };
    }
}

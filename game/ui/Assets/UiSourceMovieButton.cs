using Godot;
using San14.Data;

/// <summary>The actual movie gallery cell, with its five source artwork states.</summary>
internal partial class UiSourceMovieButton : UiSourceButton
{
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, CollectionMovieRow row, Action selected)
    {
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_07_03_offset_movie"));
        Size = source.Rectangle(0).Size; ActionMode = ActionModeEnum.Release;
        int[][] nodes = [[3], [5, 6], [8, 9], [11], [13, 14]];
        var states = new Control[nodes.Length];
        for (int state = 0; state < states.Length; state++)
        {
            var group = new Control { Name = "MovieButtonState" + state, Size = Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group); states[state] = group;
            foreach (int node in nodes[state])
                source.Art(group, node, source.Rectangle(node), assets, shader,
                    blend: node is 6 or 9 or 14 ? UiSourceSprite.Blend.Additive : UiSourceSprite.Blend.Ordinary);
        }
        ConfigureStates(states);
        if (row.Thumbnail is { } thumb)
        {
            var art = source.Art(this, 17, source.Rectangle(17), assets, shader,
                commonSpriteOverride: thumb.SpriteName, commonTexturePack: thumb.TexturePack);
            if (row.Unlocked == false) art.Modulate = new(1, 1, 1, 128 / 255f);
        }
        if (row.Unlocked == false) source.Art(this, 16, source.Rectangle(16), assets, shader);
        source.Text(this, 15, default, row.Unlocked is null ? row.Name + " ?" : row.Name,
            font, MapTextPresets.Create()[row.CanPlay ? 28 : 32]);
        if (row.BadgeImage is { } badge)
            source.Art(this, 18, source.Rectangle(18), assets, shader,
                commonSpriteOverride: badge.SpriteName, commonTexturePack: badge.TexturePack);
        Disabled = !row.CanPlay;
        if (row.UnresolvedOwner is { } unresolved) SetMeta("unresolved_owner", unresolved);
        SetMeta("movie_row", row.RowId); SetMeta("movie_id", row.MovieId);
        Pressed += () => { if (SourceInputAdmitted && !Disabled) selected(); };
    }
}

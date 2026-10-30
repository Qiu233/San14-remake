using Godot;
using San14.Data;

/// <summary>The title menu's common FT button. Its surrounding 510-pixel pane
/// belongs to the menu; only this control's authored HitArea accepts input.</summary>
internal partial class UiSourceTitleMenuButton : UiSourceButton
{
    private Rect2 hitArea;
    private Action? released;

    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hitArea.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string text, Action released)
    {
        this.released = released;
        var source = assets.SourceLayout(0x918FA91E).ResizedSubtree(0, Size, true);
        var rectangle = source.Rectangle(1);
        hitArea = new(new Vector2((int)rectangle.Position.X, (int)rectangle.Position.Y),
            new Vector2((int)rectangle.Size.X, (int)rectangle.Size.Y));
        ActionMode = ActionModeEnum.Release;
        int[][] layers = [[3,4,5], [7,8,9,10], [12,13,14,15], [17,18,19], [21,22,23,24]];
        var art = new UiSourceButtonStateArt(this,source,assets,shader,layers,"TitleButtonState");
        var caption = source.Text(this, 26, default, text, font);
        caption.Name = "TitleMenuCaption";
        var profile = MapFontProfile.FromNode(source.Nodes[26], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(art.States, state =>
        {
            art.ApplyState(state);
            caption.Configure(font, profile.WithPreset(presets[3 + state]));
            caption.QueueRedraw();
        });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    public override void _ExitTree() { released = null; InputAdmission = null; }
}

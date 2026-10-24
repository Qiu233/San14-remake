using Godot;
using San14.Data;

/// <summary>The common marker used by an army's three tactic slots. Focus
/// selects descriptive text locally; it never submits or executes a tactic.</summary>
internal partial class ArmyDetailsTacticMarker : UiSourceHitButton
{
    private Rect2 hit;
    public override bool _HasPoint(Vector2 point) => CanProcess() && hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string caption, bool occupied, Action focused, Action unfocused)
    {
        var source = new UiSourceLayout(repository, 0xD493EA98).ResizedSubtree(0, Size, true);
        Size = source.Rectangle(0).Size;
        var box = source.Rectangle(1);
        hit = new(new((int)box.Position.X, (int)box.Position.Y), new((int)box.Size.X, (int)box.Size.Y));
        source.Art(this, 2, source.Rectangle(2), assets, shader,
            commonSpriteOverride: occupied ? "000_cmn_mrk_base_yellow" : "000_cmn_mrk_base_empty");
        var label = source.Text(this, 3, default, caption, font); label.Name = "TacticName";
        // The static focus frame is separate from the marker's background.
        // Its pulsing animation can be added without changing selection.
        var focus = source.Art(this, 5, source.Rectangle(5), assets, shader); focus.Hide();
        FocusMode = FocusModeEnum.None; IgnoreTextureSize = true; ActionMode = ActionModeEnum.Release;
        MouseEntered += () => { if (CanProcess()) { focus.Show(); focused(); } };
        MouseExited += () => { focus.Hide(); unfocused(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_marker_sprite", occupied ? 0x222 : 0x21E);
    }
}

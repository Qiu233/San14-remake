using System.Globalization;
using Godot;
using San14.Data;

/// <summary>The ordinary three-slot damage child. Its source tick controls
/// the hold; the authored in/out clips retain their own curve clock.</summary>
internal partial class UnitActionDamageView : UiSourceAnimationView
{
    private MapFont font = null!;
    private UiSourceText amount = null!;
    private readonly Dictionary<(int Style, bool Positive), Control> modifierIcons = [];
    private UiAssets assets = null!;
    private Shader shader = null!;
    private int counter;
    private bool leaving;
    private double holdSeconds;
    internal bool Active => counter != 0 || IsPlaying;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont mapFont)
    {
        font = mapFont; this.assets = assets; this.shader = shader;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "00_81_09_unit_action_damage"));
        ConfigureAnimation(source, new HashSet<string> { "4_num_s_in", "4_num_s_00_out" }, Enumerable.Range(0, source.Nodes.Count));
        AutomaticAnimationClock = false;
        var node = source.Nodes[2];
        amount = new() { Name = "Amount", Size = new(node.Value44, node.Value46), MouseFilter = MouseFilterEnum.Ignore };
        AnimationPanes[2].AddChild(amount);
    }

    internal void Start(int gross, sbyte modifier, int style = 3, bool alternate = false)
    {
        amount.Configure(font, MapFontProfile.FromNode(AnimationLayout.Nodes[2], AnimationLayout.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[(modifier == 0 ? 45 : 74) - (alternate ? 1 : 0)]));
        amount.Text = gross.ToString(CultureInfo.InvariantCulture);
        HiddenAnimationPanes.Remove(3);
        if (modifier == 0) HiddenAnimationPanes.Add(3);
        foreach (var icon in modifierIcons.Values) icon.Hide();
        if (modifier != 0)
        {
            var key = (style, modifier > 0);
            if (!modifierIcons.TryGetValue(key, out var icon))
            {
                string sprite = style switch
                {
                    2 => "000_cmn_icon_bt_koujyou_fuchi",
                    3 => "000_cmn_icon_bt_bougyo_fuchi",
                    _ => "000_cmn_icon_bt_kougun_fuchi"
                };
                var colors = MapTextPresets.Create()[modifier > 0 ? 63 : 64];
                var node = AnimationLayout.Nodes[3];
                icon = AnimationLayout.Art(AnimationPanes[3], 3, new(Vector2.Zero, new(node.Value44, node.Value46)),
                    assets, shader, colors: new(colors.LT, colors.RT, colors.LB, colors.RB),
                    commonSpriteOverride: sprite, localCoordinates: true);
                modifierIcons.Add(key, icon);
            }
            icon.Show();
        }
        counter = 1; leaving = false; holdSeconds = 0;
        PlayAnimation("4_num_s_in");
    }

    internal void AdvanceSourceTick(uint mode)
    {
        if (counter == 0) return;
        counter++;
        int limit = mode switch { 0 => 90, 1 => 60, _ => 30 };
        if (counter < limit) return;
        counter = 0; leaving = true; PlayAnimation("4_num_s_00_out");
    }

    public override void _Process(double delta)
    {
        if (leaving) AdvanceAnimation(delta);
        else if (counter != 0) { holdSeconds += delta; ApplyAnimationAt("4_num_s_in", holdSeconds); }
    }
}

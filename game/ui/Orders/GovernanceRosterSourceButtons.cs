using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record GovernanceRosterSlotDisplay(int Mode, string Caption, int? Cost,
    LoadedSprite? Face, bool PolicyValid, bool PendingHighlight, bool Enabled,
    bool CostDiscount = false, bool Attention = false);

internal sealed record GovernanceRosterPolicyDisplay(string Name, LoadedSprite? Icon, int? Cost,
    bool CostDiscount, bool PolicyValid, bool PendingHighlight, bool Enabled,
    bool IconValid = true, bool CostVisible = true);

/// <summary>The original GovernmentLine wrapper owns an actual common mode3 button.</summary>
internal partial class GovernanceRosterLineButton : Control
{
    private GovernanceRosterLineHit button = null!;
    private UiSourceText caption = null!, count = null!;
    private Control attention = null!, on = null!;
    private readonly List<Control> categories = [];

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, Action released, Func<bool>? admission = null)
    {
        var source = new UiSourceLayout(repository, 0xDE8945A6);
        Size = source.Rectangle(0).Size; MouseFilter = MouseFilterEnum.Ignore;
        // Constructor6F5660 hides placeholder5, then mounts CCmnButton(mode3) there.
        foreach (int index in new[] { 2, 3 }) source.Art(this, index, source.Rectangle(index), assets, shader);
        var box = source.Rectangle(5);
        button = new() { Position = box.Position, Size = box.Size, InputAdmission = admission };
        AddChild(button);
        button.Configure(repository, assets, shader, font, "", () =>
        { if (button.SourceInputAdmitted && !button.Disabled) released(); });
        source.Art(this, 7, source.Rectangle(7), assets, shader);
        for (int node = 8; node <= 13; node++)
            categories.Add(source.Art(this, node, source.Rectangle(node), assets, shader));
        on = source.Art(this, 14, source.Rectangle(14), assets, shader);
        caption = source.Text(this, 4, default, "", font, MapTextPresets.Create()[13]);
        attention = source.Art(this, 15, source.Rectangle(15), assets, shader);
        count = source.Text(this, 16, default, "", font, MapTextPresets.Create()[0]);
        button.MouseEntered += Refresh; button.MouseExited += Refresh;
        button.ButtonDown += Refresh; button.ButtonUp += Refresh;
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Apply("", 0, 0, false);
    }

    internal void Apply(string text, int category, int attentionCount, bool enabled)
    {
        caption.Text = text; button.Disabled = !enabled;
        for (int i = 0; i < categories.Count; i++) categories[i].Visible = i == category;
        attention.Visible = count.Visible = attentionCount > 0;
        count.Text = attentionCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Refresh(); button.QueueRedraw();
    }

    private void Refresh() => on.Visible = !button.Disabled && button.IsHovered();

    private partial class GovernanceRosterLineHit : UiSourceCommandButton
    {
        public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && base._HasPoint(point);
    }
}

/// <summary>Resource clip endpoints supply background/focus/push opacity, including ancestors.</summary>
internal static class GovernanceRosterButtonStates
{
    internal static void Configure(UiSourceButton owner, UiSourceLayout source, UiAssets assets,
        Shader shader, int[] art, bool disabledClip, Action<int>? captionUpdater=null, bool policyBehavior=false)
    {
        string[] names = policyBehavior ? ["0_normal","1_focus","1_focus","0_normal","1_focus"] :
            ["0_normal", "1_focus", "2_push", disabledClip ? "3_disable" : "0_normal",
            disabledClip ? "3_disable" : "0_normal"];
        var clips = LcskAnimationReader.Read(source.Source.Layout, names.ToHashSet())
            .ToDictionary(clip => clip.Name);
        var states = new Control[5];
        for (int state = 0; state < states.Length; state++)
        {
            var clip = clips[names[state]];
            var alpha = clip.Sample(clip.EndFrame).Where(value => value.Channel == 10)
                .ToDictionary(value => value.NodeId, value => (float)Math.Clamp(value.Value, 0, 1));
            var layer = new Control { Name = "SourceState" + state, Size = owner.Size,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            owner.AddChild(layer); owner.MoveChild(layer,state); states[state] = layer;
            foreach (int index in art)
            {
                float opacity = 1;
                for (int? n = index; n is int current; n = source.Nodes[current].ParentIndex)
                    if (alpha.TryGetValue(current, out float value)) opacity *= value;
                if (opacity <= 0) continue;
                var pane = source.Art(layer, index, source.Rectangle(index), assets, shader);
                pane.Modulate = new(1, 1, 1, opacity);
            }
        }
        owner.ConfigureStates(states,captionUpdater);
    }
}

internal partial class GovernanceRosterSlotButton : UiSourceButton
{
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private MapFont font = null!;
    private UiSourceLayout source = null!;
    private Control silhouette = null!, costBase = null!, frame = null!, locked = null!, attention = null!;
    private UiSourceText cost = null!, caption = null!, centreCaption = null!;
    private UiSourcePortrait? portrait;
    private LoadedSprite? face;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, Action released, Func<bool>? admission = null)
    {
        this.repository = repository; this.assets = assets; this.shader = shader; this.font = font;
        source = new(repository, 0xEA0A352A); Size = source.Rectangle(1).Size;
        InputAdmission = admission; ActionMode = ActionModeEnum.Release;
        GovernanceRosterButtonStates.Configure(this, source, assets, shader,
            [3, 5, 7, 9, 21, 22, 24, 25], true);
        silhouette = source.Art(this, 12, source.Rectangle(12), assets, shader);
        costBase = source.Art(this, 13, source.Rectangle(13), assets, shader);
        cost = source.Text(this, 14, default, "", font, MapTextPresets.Create()[36]);
        caption = source.Text(this, 15, default, "", font);
        centreCaption = source.Text(this, 16, default, "", font);
        frame = source.Art(this, 17, source.Rectangle(17), assets, shader);
        locked = source.Art(this, 18, source.Rectangle(18), assets, shader);
        attention = source.Art(this, 26, source.Rectangle(26), assets, shader);
        Pressed += () => { if (SourceInputAdmitted && !Disabled) released(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Apply(new(3, "", 0, null, false, false, false));
    }

    internal void Apply(GovernanceRosterSlotDisplay display)
    {
        Visible = display.Mode is >= 0 and <= 2; Disabled = !display.Enabled;
        bool occupied = display.Mode == 0;
        silhouette.Visible = display.Mode == 1;
        costBase.Visible = cost.Visible = occupied;
        caption.Visible = occupied; centreCaption.Hide();
        frame.Visible = occupied && display.PolicyValid; locked.Visible = display.Mode == 2;
        attention.Visible = display.Attention;
        cost.Text = display.Cost?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
        cost.Configure(font, MapFontProfile.FromNode(source.Nodes[14], source.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[display.CostDiscount ? 15 : 36]));
        caption.Text = centreCaption.Text = display.Caption;
        if (!ReferenceEquals(face, display.Face))
        {
            if (portrait is not null) { RemoveChild(portrait); portrait.QueueFree(); portrait = null; }
            face = display.Face;
            if (face is not null)
            {
                var box = source.Rectangle(10);
                portrait = new() { Name = "GovernmentSlotPortrait", Position = box.Position, Size = box.Size };
                AddChild(portrait); MoveChild(portrait, 5);
                portrait.Configure(repository, assets, shader, face);
            }
        }
        if (portrait is not null) portrait.Visible = occupied;
        QueueRedraw();
    }
}

internal partial class GovernanceRosterPolicyButton : UiSourceButton
{
    private UiSourceLayout source = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private MapFont font = null!;
    private UiSourceText caption = null!, cost = null!;
    private Control frame = null!;
    private Control? icon;
    private LoadedSprite? iconSprite;
    private bool iconValid;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, Action released, Func<bool>? admission = null)
    {
        this.assets = assets; this.shader = shader; this.font = font;
        source = new(repository, 0x2729F949); Size = source.Rectangle(1).Size;
        InputAdmission = admission; ActionMode = ActionModeEnum.Release;

        source.Art(this, 5, source.Rectangle(5), assets, shader);
        cost = source.Text(this, 6, default, "", font, MapTextPresets.Create()[36]);
        caption = source.Text(this, 7, default, "", font, MapTextPresets.Create()[8]);
        frame = source.Art(this, 8, source.Rectangle(8), assets, shader);
        var profile=MapFontProfile.FromNode(source.Nodes[7],source.Source.Source.ResourceUri);
        int[] textStyles=[61,62,62,64,64];
        GovernanceRosterButtonStates.Configure(this,source,assets,shader,[3,11,12,14,15],false,
            state=>{caption.Configure(font,profile.WithPreset(MapTextPresets.Create()[textStyles[state]]));caption.QueueRedraw();},
            policyBehavior:true);
        Pressed += () => { if (SourceInputAdmitted && !Disabled) released(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void Apply(GovernanceRosterPolicyDisplay display)
    {
        Disabled = !display.Enabled; caption.Text = display.Name;
        cost.Text = display.Cost?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
        cost.Configure(font, MapFontProfile.FromNode(source.Nodes[6], source.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[display.CostDiscount ? 15 : 36]));
        frame.Visible = display.PolicyValid;
        cost.Visible = display.CostVisible;
        if (!ReferenceEquals(iconSprite, display.Icon) || iconValid != display.IconValid)
        {
            if (icon is not null) { RemoveChild(icon); icon.QueueFree(); }
            iconSprite = display.Icon; iconValid = display.IconValid; icon = null;
            if (iconSprite is not null)
            {
                var box = source.Rectangle(4);
                var part = LcskSpriteNodeReader.Read(source.Source.Layout, source.Nodes[4]).Groups.Single().Parts.Single();
                var sprite = new UiSourceSprite { Name = "GovernmentPolicyIcon", Position = box.Position, Size = box.Size };
                AddChild(sprite);
                sprite.Configure(shader, assets.SrgbSpritePart(iconSprite.Definition.Name, part.Uv, iconSprite),
                    UiSourceSprite.Blend.Ordinary, MapSpritePalettes.Create()[display.IconValid ? 46 : 45]);
                icon = sprite;
            }
            if (icon is not null) MoveChild(icon, 5);
        }
        QueueRedraw();
    }
}

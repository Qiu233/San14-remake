using Godot;
using San14.Data;

/// <summary>The event editor's actual 516 by 66 section button.</summary>
internal partial class EventEditorCommandButton : UiSourceButton
{
    private Rect2 hit;
    private UiSourceText caption = null!;
    private Action? released;
    internal string Caption { get => caption.Text; set => caption.Text = value; }
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string locale, string text, bool commandBadge, Action released)
    {
        this.released = released;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_00_btn_offset_00_new"))
            .ResizedSubtree(0, Size, true);
        var box = source.Rectangle(1);
        hit = new(new((int)box.Position.X, (int)box.Position.Y), new((int)box.Size.X, (int)box.Size.Y));
        ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        caption = source.Text(this, 15, default, text, font);
        var profile = MapFontProfile.FromNode(source.Nodes[15], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state =>
        { caption.Configure(font, profile.WithPreset(presets[3 + state])); caption.QueueRedraw(); });
        if (commandBadge)
        {
            string language = locale switch { "TC" => "tw", "SC" => "ch", _ => throw new InvalidDataException("event.menu.badge.locale") };
            source.Art(this, 16, source.Rectangle(16), assets, shader,
                commonSpriteOverride: language + "_020_sho_mrk_pk", commonTexturePack: "texture_lang_" + language);
        }
        // The unbound sample lock17 is not an eligibility fact.
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void Abandon() { released = null; InputAdmission = null; Disabled = true; }
}

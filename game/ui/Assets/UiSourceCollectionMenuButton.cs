using Godot;
using San14.Data;

/// <summary>The Collection menu's own command behavior and text presets.</summary>
internal partial class UiSourceCollectionMenuButton : UiSourceButton
{
    private Rect2 hitArea;
    private Action? released;
    internal string Caption { get; private set; } = "";
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hitArea.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string text, string locale, CollectionMenuCommand command, Action released)
    {
        this.released = released; Caption = text;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_00_btn_offset_00_new"))
            .ResizedSubtree(0, Size, true);
        hitArea = source.Rectangle(1);
        ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(index => source.Art(this, index, source.Rectangle(index), assets, shader)).ToArray();
        var caption = source.Text(this, 15, default, text, font);
        var profile = MapFontProfile.FromNode(source.Nodes[15], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state =>
        { caption.Configure(font, profile.WithPreset(presets[3 + state])); caption.QueueRedraw(); });
        if (command is CollectionMenuCommand.Events or CollectionMenuCommand.Poems or CollectionMenuCommand.Scenery)
        {
            string language = locale == "TC" ? "tw" : locale == "SC" ? "ch" :
                throw new ArgumentOutOfRangeException(nameof(locale));
            bool expansion = command is CollectionMenuCommand.Poems or CollectionMenuCommand.Scenery;
            source.Art(this, 16, source.Rectangle(16), assets, shader,
                commonSpriteOverride: language + "_020_sho_mrk_" + (expansion ? "ce" : "pk"),
                commonTexturePack: "texture_" + (expansion ? "ce_" : "") + "lang_" + language);
        }
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void Abandon() { released = null; InputAdmission = null; Disabled = true; }
}

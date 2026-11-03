using Godot;
using San14.Data;
using San14.Data.Formats;

// Immutable definitions are prepared by the existing application UI worker.
// A rail only owns live controls, source state and callbacks.
internal sealed record MapHudSources(MapCommandRailSources Rail, MainCommandLayoutSources Layouts,
    IReadOnlyList<MapMenuEntry>[] Menus, IReadOnlyDictionary<MapCommandCategory, LoadedSprite> Captions,
    Color SubmenuFocusTint, Color SubmenuDisabledFocusTint);

// This is part of Main's existing locale/resource generation, never a second
// registry. Sprite materials and render targets remain with their controls.
internal sealed class MapHudResources : IDisposable
{
    internal AdministrationPageChrome.Resources Shared { get; }
    internal MapHudSources Sources { get; }
    internal Shader CursorShader { get; }
    internal Shader FocusShader { get; }
    internal Shader SubmenuFocusShader { get; }

    internal MapHudResources(AdministrationPageChrome.Resources shared, MapHudSources sources)
    {
        Shared = shared; Sources = sources;
        var cursor = MapCommandRailShaders.Cursor();
        Shader? focus = null;
        try
        {
            focus = MapCommandRailShaders.Focus();
            var submenu = MapCommandRailShaders.Focus(submenu: true);
            CursorShader = cursor; FocusShader = focus; SubmenuFocusShader = submenu;
        }
        catch { focus?.Dispose(); cursor.Dispose(); throw; }
    }

    public void Dispose()
    { SubmenuFocusShader.Dispose(); FocusShader.Dispose(); CursorShader.Dispose(); }
}

internal sealed partial class MapSystemMenuPreparedResources
{
    private MapHudSources hudSources = null!;
    private MapHudResources? hudResources;
    internal bool HudCaptionsPrepared { get; private set; }

    internal void PrepareHudCaptions(PreviewText text)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (HudCaptionsPrepared) return;
        var header = Assets.SourceLayout(0xBB442620);
        var presets = MapTextPresets.Create();
        Prepare("cap_10_fc014_0000", text.Get("map_hud_command_books"), 13);
        foreach (string key in new[] { "map_hud_early_period", "map_hud_middle_period", "map_hud_late_period" })
            Prepare("val_4_fc001_0000", text.Get(key), 0);
        PrepareMapCommandPopupCaptions(text);
        HudCaptionsPrepared = true;

        void Prepare(string nodeName, string value, int preset)
        {
            var node = header.Nodes.Single(node => node.Name == nodeName);
            MapFont.PrepareText(FontSource, value,
                MapFontProfile.FromNode(node, header.Source.Source.ResourceUri).WithPreset(presets[preset]),
                node.Value44, textCaches);
        }
    }

    internal MapHudResources CreateHudResources()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return hudResources ??= new(CreateChromeResources(), hudSources);
    }

    private static MapHudSources PrepareHudSources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, string locale, Dictionary<MapFont.PreparedTextKey, MapTextCache> caches,
        CancellationToken cancellationToken)
    {
        var rail = new MapCommandRailSources(repository);
        var layouts = new MainCommandLayoutSources(repository, rail);
        var menus = MapCommandRailSources.ReadMenus(repository, locale);
        var captions = new Dictionary<MapCommandCategory, LoadedSprite>();
        string[] names = ["01shussei", "02seifu", "03jinzai", "04keiryaku", "05gaikou", "07gunjo", "06kessai"];
        for (int category = 0; category < names.Length; category++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var caption = assets.DeclaredSprites.LoadCommandCaption(locale, "000_cmn_btn_main_txt_" + names[category]);
            assets.PrepareSprite(caption, srgb: true);
            captions.Add((MapCommandCategory)category, caption);
        }
        var profile = MapFontProfile.FromNode(rail.SubmenuButton.Nodes[20], rail.SubmenuButton.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        foreach (var entry in menus.SelectMany(menu => menu))
            for (int state = 28; state < 33; state++)
                MapFont.PrepareText(font, entry.Caption, profile.WithPreset(presets[state]), 130, caches);

        PrepareLayout(layouts.Main.Source, [3, 5, 19]);
        PrepareLayout(layouts.Button.Source, [8, 18]);
        PrepareLayout(layouts.Fold.Source, [6, 13]);
        PrepareLayout(layouts.Cursor.Source, [2]);
        PrepareLayout(layouts.Link.Source, [2]);
        foreach (var source in new[] { layouts.Attention, layouts.Badge })
            PrepareLayout(source.Source, source.Nodes.Where(node => node.Type is 1 or 3).Select(node => node.Index));
        // All seven real categories for both six-row and seven-row styles.
        PrepareLayout(layouts.Submenu.Source, Enumerable.Range(3, 6).Concat(Enumerable.Range(14, 7)).Concat([27, 28]));
        PrepareImages(rail.Button, [3, 4, 6, 7, 10, 11, 13, 14, 16, 17]);
        PrepareImages(rail.Fold, [3, 5, 8, 10, 12]);
        PrepareImages(rail.SubmenuButton, [3, 4, 6, 7, 8, 10, 11, 13, 14, 16, 17, 18]);
        foreach (string icon in new[] { "000_cmn_com_icon_close", "000_cmn_com_icon_open" })
            assets.PrepareSprite(icon, srgb: true);

        PrepareHeaderSources(repository, assets, cancellationToken);
        PrepareTopButtonHintTexts(repository, assets, font, locale, caches, cancellationToken);
        PrepareCommandRailHintTexts(repository, assets, font, locale, caches, cancellationToken);
        PrepareCommanderTacticBalloonSources(repository, assets, font, locale, caches, cancellationToken);
        var focus = LcskAnimationReader.Read(rail.SubmenuButton.Layout,
            new HashSet<string> { "1_focus", "4_disable_forcus" });
        Color Tint(string name, int node)
        {
            var clip = focus.Single(clip => clip.Name == name);
            return MapCommandRail.SourceEffectTint(clip.Sample(clip.EndFrame), node);
        }
        return new(rail, layouts, menus, captions, Tint("1_focus", 8), Tint("4_disable_forcus", 18));

        void PrepareLayout(LoadedLayout layout, IEnumerable<int> nodes)
        { cancellationToken.ThrowIfCancellationRequested(); assets.PrepareLayoutArt(layout, nodes); }
        void PrepareImages(MapMarkerLayout layout, IEnumerable<int> nodes)
        {
            foreach (int node in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = MapCommandRailSources.Sprite(layout, node);
                assets.PrepareSprite(name, srgb: true);
            }
        }
    }

    private static void PrepareHeaderSources(GameResourceRepository repository, UiAssets assets,
        CancellationToken cancellationToken)
    {
        var header = assets.SourceLayout(0xBB442620);
        foreach (string suffix in new[] { "a", "b", "c" })
            Art(header, [header.Nodes.Single(node => node.Name == "000_cmn_main_haeder_flag_" + suffix).Index]);
        Gauge(header, [20, 21]);
        // These mounts provide geometry only, not their designer dummy art.
        _ = assets.SourceLayout(0x454A1BCC);
        _ = assets.SourceLayout(0x036BB544);
        _ = assets.SourceLayout(0xF28BE325);
        _ = assets.SourceLayout("00_02_00_mein_offset_60_pk");
        PrepareInformationToolbarSources(assets,cancellationToken);
        foreach (string name in new[] { "000_cmn_base_main_header", "000_cmn_icon_meirei",
            "000_cmn_txt_adv_shinko", "000_cmn_txt_adv_teishi", "000_cmn_icon_advance_play", "000_cmn_icon_advance_stop" })
            assets.PrepareSprite(name, srgb: false);
        for (int state = 0; state < 5; state++)
            assets.PrepareSprite("000_cmn_btn_adv_" + state, srgb: false);
        var progress = assets.SourceLayout(0xC7725FCB);
        Art(progress, [2]); Gauge(progress, [3, 4]);
        Art(assets.SourceLayout(0x40F993CE), [1]);
        Art(assets.SourceLayout(0x27447F69), [13, 14]);
        for (int speed = 1; speed <= 3; speed++)
            for (int state = 0; state < 4; state++)
                assets.PrepareCommonSprite($"000_cmn_btn_spd{speed}_{state}");
        int[] states = [3, 5, 6, 8, 9, 11, 13, 14];
        Art(assets.SourceLayout(0x2B7AA77D), states);
        Art(assets.SourceLayout("cmn_btn_fnc_check"), states.Concat([16]));
        foreach (string icon in new[] { "000_cmn_icon_china", "000_cmn_icon_north", "000_cmn_icon_help",
            "000_cmn_icon_info", "000_cmn_icon_opt", "000_cmn_icon_view" })
            assets.PrepareCommonSprite(icon);
        assets.PrepareCommonSprite("000_cmn_icon_shounin", "texture_pk_common");
        assets.PrepareCommonSprite("000_cmn_icon_gadvantage", "texture_pk_common");
        var tooltip = assets.SourceLayout(0xBE0DC488);
        _ = tooltip.Type6(1); Art(tooltip, [2]);
        var portrait = assets.SourceLayout(0xB639C019);
        var parts = LcskSpriteNodeReader.Read(portrait.Source.Layout, portrait.Nodes[1]).Groups.Single();
        if (parts.ComponentCount != 3 || parts.Parts.Count != 3)
            throw new InvalidDataException("Map header portrait requires its original three components.");
        foreach (var part in parts.Parts.Skip(1))
        {
            var source = assets.SourceLayoutSprite(portrait.Source, part.SpriteName);
            assets.PrepareSprite(source, MapPortraitQuad.UsesSrgbView(repository, assets, part.SpriteName, source));
        }
        // Actual character images use the existing portrait routing/version
        // owner. This declaration does not invent a fixed initial ruler.

        void Art(UiSourceLayout layout, IEnumerable<int> nodes)
        { cancellationToken.ThrowIfCancellationRequested(); assets.PrepareLayoutArt(layout.Source, nodes); }
        void Gauge(UiSourceLayout layout, IEnumerable<int> nodes)
        {
            foreach (int index in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parts = LcskSpriteNodeReader.Read(layout.Source.Layout, layout.Nodes[index]).Groups.Single().Parts;
                if (parts.Count != 2 || parts[0].SpriteName != parts[1].SpriteName)
                    throw new InvalidDataException("Map gauge requires its original two texture-mask components.");
                assets.PrepareSrgbLayoutSprite(layout.Source, parts[0].SpriteName);
            }
        }
    }
}

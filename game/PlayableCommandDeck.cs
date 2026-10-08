using Godot;

/// <summary>Presentation only; all pages keep using the same validated commands.</summary>
public partial class PlayableCommandDeck : TabContainer
{
    public enum Page { Administration, Military, Personnel, Governance, Diplomacy }
    private static readonly string[] TitleKeys =
        ["play_commands_administration", "play_commands_military", "play_commands_personnel", "play_commands_governance", "play_commands_diplomacy"];
    private readonly VBoxContainer[] _pages = new VBoxContainer[TitleKeys.Length];

    public PlayableCommandDeck()
    {
        Name = "PlayableCommandDeck";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        UseHiddenTabsForMinSize = false;
        Theme = BuildTheme();
        for (int index = 0; index < _pages.Length; index++)
        {
            var page = new VBoxContainer { Name = $"CommandPage{index}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill };
            page.AddThemeConstantOverride("separation", 8);
            _pages[index] = page;
            AddChild(page);
        }
    }

    public VBoxContainer Content(Page page) => _pages[(int)page];
    public void Select(Page page) => CurrentTab = (int)page;
    internal void UpdateLocale(PreviewText text)
    {
        for (int index = 0; index < TitleKeys.Length; index++)
            SetTabTitle(index, text.Get(TitleKeys[index]));
    }

    private static Theme BuildTheme()
    {
        var theme = new Theme { DefaultFontSize = 18,
            DefaultFont = new SystemFont { FontNames = ["KaiTi", "DFKai-SB", "Microsoft YaHei"] } };
        theme.SetStylebox("panel", "TabContainer", Box(new Color("#51432de8"), new Color("#b0a16a"), 12));
        theme.SetStylebox("tab_selected", "TabContainer", Box(new Color("#985026"), new Color("#d6bd74"), 10));
        theme.SetStylebox("tab_unselected", "TabContainer", Box(new Color("#d7d6bd"), new Color("#938962"), 10));
        theme.SetColor("font_selected_color", "TabContainer", new Color("#fff0b3"));
        theme.SetColor("font_unselected_color", "TabContainer", new Color("#514936"));
        theme.SetColor("font_color", "Label", new Color("#eee5ba"));
        theme.SetStylebox("normal", "Button", Box(new Color("#d7d6bd"), new Color("#afa27a"), 6));
        theme.SetStylebox("hover", "Button", Box(new Color("#ece2b3"), new Color("#dcc47a"), 6));
        theme.SetStylebox("pressed", "Button", Box(new Color("#985026"), new Color("#d6bd74"), 6));
        theme.SetColor("font_color", "Button", new Color("#514936"));
        theme.SetColor("font_pressed_color", "Button", new Color("#fff0b3"));
        return theme;
    }

    private static StyleBoxFlat Box(Color fill, Color border, float margin) => new()
    {
        BgColor = fill, BorderColor = border,
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        ContentMarginLeft = margin, ContentMarginRight = margin,
        ContentMarginTop = margin * .5f, ContentMarginBottom = margin * .5f
    };
}

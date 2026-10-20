using System.Globalization;
using Godot;
using San14.Client.Gameplay;
using San14.Data;
using San14.Data.Formats;

/// <summary>Selected decoded save, independently of the running campaign.
/// Unsupported settings stay unknown rather than borrowing native header bytes.</summary>
internal partial class CampaignSaveDetails : Control
{
    private GameResourceRepository repository = null!;
    private AdministrationPageChrome chrome = null!;
    private PreviewText text = null!;
    private UiSourceLayout shell = null!;
    private readonly Dictionary<int, UiSourceText> values = [];
    private UiSourceText ruler = null!;
    private Control? selectedArt;

    internal void Configure(GameResourceRepository repository, AdministrationPageChrome chrome, PreviewText text)
    {
        this.repository = repository; this.chrome = chrome; this.text = text;
        Size = new(1920, 1080); MouseFilter = MouseFilterEnum.Ignore;
        shell = new(repository, 0x761D3014);
        foreach (int node in new[] { 43, 44, 45 })
            shell.Art(this, node, shell.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        ruler = shell.Text(this, 49, default, "", chrome.Font, MapTextPresets.Create()[0]);
        var detail = new UiSourceLayout(repository, 0x844FA697);
        Vector2 mount = shell.Rectangle(50).Position;
        foreach (var node in detail.Nodes.Where(node => node.Type is 1 or 3))
            detail.Art(this, node.Index, detail.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
        var presets = MapTextPresets.Create();
        foreach (var binding in new[] { (4, 0x021B), (14, 0x0244), (19, 0x0207), (9, 0x0218),
            (24, 0x04C2), (29, 0x022A), (34, 0x0249), (39, 0x0240), (44, 0x0165),
            (49, 0x04C7), (64, 0x001B), (59, 0x054F), (54, 0x03FD), (69, 0x0861) })
        {
            int section = binding.Item1 == 44 ? 0 : 1;
            string caption = repository.LoadMessages(text.SourceLocale, MessageKind.Data)
                .Messages.Sections[section].Records[binding.Item2].Text
                ?? throw new InvalidDataException("Missing save detail caption.");
            detail.Text(this, binding.Item1, mount, caption, chrome.Font, presets[13]);
            var value = detail.Text(this, binding.Item1 + 1, mount, "", chrome.Font, presets[0]);
            value.Name = "SaveDetailValue" + (binding.Item1 + 1);
            values.Add(binding.Item1 + 1, value);
        }
    }

    internal void Apply(CampaignSaveSummary? summary, LoadedSprite? resolvedPortrait)
    {
        if (selectedArt is not null)
        { RemoveChild(selectedArt); selectedArt.QueueFree(); selectedArt = null; }
        ruler.Text = summary?.RulerName ?? "";
        foreach (var value in values.Values) value.Text = summary is null ? "" : text.Get("detail_unknown");
        if (summary is null) return;
        values[5].Text = summary.RankName ?? text.Get("detail_unknown");
        values[15].Text = summary.DoctrineName ?? text.Get("detail_unknown");
        values[20].Text = summary.Cities.ToString(CultureInfo.InvariantCulture);
        values[10].Text = summary.Officers.ToString(CultureInfo.InvariantCulture);
        if (summary.AdvancedSettings == false && summary.DifficultyId is
            "beginner" or "intermediate" or "advanced" or "super" or "top")
            values[25].Text = text.Get("new_game_difficulty_" + summary.DifficultyId);
        selectedArt = new Control { Name = "SelectedSaveArt", Size = Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(selectedArt);
        MoveChild(selectedArt, 0);
        if (resolvedPortrait is not null)
        {
            var box = shell.Rectangle(39);
            var portrait = new UiSourcePortrait { Name = "SelectedSavePortrait",
                Position = new((int)box.Position.X, (int)box.Position.Y),
                Size = new((int)box.Size.X, (int)box.Size.Y) };
            selectedArt.AddChild(portrait);
            portrait.ConfigureFace3(repository, chrome.Assets, chrome.SpriteShader, resolvedPortrait);
        }
        if (summary.Flag is { } appearance)
        {
            var flag = new UiSourceForceFlag { Name = "SelectedSaveFlag", Position = shell.Rectangle(46).Position };
            selectedArt.AddChild(flag); flag.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, appearance);
        }
        var map = new UiSourceMiniMap { Name = "SelectedSaveMiniMap", Position = shell.Rectangle(41).Position };
        selectedArt.AddChild(map); map.Configure(repository, chrome.Assets, chrome.SpriteShader, summary.MiniMap);
    }
}

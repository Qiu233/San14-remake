using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record GovernancePolicyBalloonContributor(LoadedSprite? Face, bool Pending);
internal sealed record GovernancePolicyBalloonDisplay(string Description, int? Pending, int? Level,
    int? AggregateLevel, IReadOnlyList<GovernancePolicyBalloonContributor> Contributors);

/// <summary>The three original policy balloons. The caller publishes the current
/// contribution set and levels; this view never queries or scans the world.</summary>
internal partial class GovernancePolicyBalloon : Control
{
    private GameResourceRepository repository = null!;
    private AdministrationPageChrome chrome = null!;
    private readonly List<Panel> panels = [];
    private readonly List<UiSourcePortrait> portraits = [];
    private string pendingFormat = null!;

    internal void Configure(GameResourceRepository repository, AdministrationPageChrome chrome)
    {
        this.repository = repository; this.chrome = chrome;
        Name = "GovernmentPolicyBalloon"; ZIndex = 20; MouseFilter = MouseFilterEnum.Ignore;
        chrome.Canvas.AddChild(this);
        uint[] ids = [0xFB8FA10B, 0xEC60A86A, 0xDD31AFC9];
        var messages = repository.LoadMessages(chrome.Font.Source.Locale, MessageKind.Data).Messages.Sections[1];
        var presets = MapTextPresets.Create();
        pendingFormat = (messages.Records[0x324].Text ?? throw new InvalidDataException("Missing government source caption."));
        foreach (uint id in ids)
        {
            var source = new UiSourceLayout(repository, id);
            var root = new Control { Name = "PolicyBalloon" + panels.Count,
                Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(root);
            source.Blur(root, 1, source.Rectangle(1));
            foreach (int node in panels.Count switch { 0 => new[] { 2, 3, 5, 6 },
                1 => new[] { 2, 3 }, _ => new[] { 2, 3, 8 } })
                source.Art(root, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
            var box = source.Rectangle(4);
            var description = new UiSourceParagraph { Position = box.Position, Size = box.Size };
            root.AddChild(description);
            description.Configure(chrome.Font, MapFontProfile.FromNode(source.Nodes[4], source.Source.Source.ResourceUri)
                .WithPreset(presets[0]));
            UiSourceText? delta = null;
            UiSourceText value;
            if (panels.Count == 0)
            {
                source.Text(root, 7, default, (messages.Records[0x3BF].Text ?? throw new InvalidDataException("Missing government source caption.")), chrome.Font, presets[13]);
                value = source.Text(root, 8, default, "", chrome.Font, presets[0]);
            }
            else
            {
                source.Text(root, 5, default, (messages.Records[0x250].Text ?? throw new InvalidDataException("Missing government source caption.")), chrome.Font, presets[0]);
                value = source.Text(root, 6, default, "", chrome.Font, presets[0]);
                if (panels.Count == 2)
                {
                    delta = source.Text(root, 7, default, "", chrome.Font, presets[15]);
                    source.Text(root, 9, default, (messages.Records[0x3BE].Text ?? throw new InvalidDataException("Missing government source caption.")), chrome.Font, presets[13]);
                }
            }
            root.SetMeta("source_layout", source.Source.Source.ResourceUri);
            panels.Add(new(source, root, description, value, delta)); root.Hide();
        }
        Hide(); SetProcess(false);
    }

    internal void Display(GovernancePolicyBalloonDisplay? display)
    {
        if (display is null) { Hide(); SetProcess(false); return; }
        int index = display.Pending is > 0 ? 0 : display.Contributors.Count < 2 ? 1 : 2;
        for (int i = 0; i < panels.Count; i++) panels[i].Root.Visible = i == index;
        var panel = panels[index]; Size = panel.Root.Size;
        panel.Description.Apply(display.Description);
        string Number(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
        panel.Value.Text = index == 0
            ? GovernanceRosterMessages.Numbers(pendingFormat, display.Pending!.Value)
            : Number(index == 1 ? display.Level : display.AggregateLevel);
        if (panel.Delta is not null)
            panel.Delta.Text = display.AggregateLevel is int total && display.Level is int level
                ? $"({(total - level).ToString("+0;-0;+0", System.Globalization.CultureInfo.InvariantCulture)})" : "";
        foreach (var previous in portraits) { previous.GetParent().RemoveChild(previous); previous.QueueFree(); }
        portraits.Clear();
        if (index == 2)
        {
            int count = Math.Min(display.Contributors.Count, 8);
            for (int i = 0; i < count; i++)
            {
                var contributor = display.Contributors[i];
                if (contributor.Face is not { } face) continue;
                int mount = 10 + i * 3;
                var box = panel.Source.Rectangle(mount);
                //6ED0B0 replaces each author placeholder with92F830(type10),
                //whose position and control dimensions use the integer mount API.
                var portrait = new UiSourcePortrait { Name = "PolicyContributor" + i,
                    Position = new((int)box.Position.X, (int)box.Position.Y),
                    Size = new((int)box.Size.X, (int)box.Size.Y),
                    Modulate = new(1, 1, 1, contributor.Pending ? .2f : 1f) };
                panel.Root.AddChild(portrait); portraits.Add(portrait);
                portrait.Configure(repository, chrome.Assets, chrome.SpriteShader, face);
            }
        }
        Show(); FollowPointer(); SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) FollowPointer();
    }

    private void FollowPointer()
    {
        Vector2 point = chrome.Canvas.GetLocalMousePosition() + new Vector2(20, 20);
        Position = new(Math.Clamp(point.X, 0, 1920 - Size.X), Math.Clamp(point.Y, 0, 1080 - Size.Y));
    }

    private sealed record Panel(UiSourceLayout Source, Control Root, UiSourceParagraph Description,
        UiSourceText Value, UiSourceText? Delta);
}

using Godot;
using San14.Data;

internal sealed record ArmyCommandMenuEntry(string Name, string Caption, string? Icon, string? Hint,
    bool Enabled, Action Released, int? Ordinal = null);

/// <summary>The source CArmyButtonMenu composition. Permissions and selected
/// targets belong to its owner; camera tracking only projects an anchor.</summary>
internal partial class ArmyCommandMenu : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private readonly List<(UiSourceListCheckboxButton Button, bool Enabled)> buttons = [];
    private Action<string?>? narration;
    private Func<Vector2?>? project;
    private Action<Vector2?>? policyPositioned;
    private int footerTop = 912, windowHeight, policyAvoidanceHeight = 240;
    private bool finished, busy;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<ArmyCommandMenuEntry> entries, Action<string?> hintChanged)
    {
        if (entries.Count is < 1 or > 7) throw new ArgumentOutOfRangeException(nameof(entries));
        Name = "ArmyCommandMenu"; MouseFilter = MouseFilterEnum.Ignore; Size = new(154, 338);
        narration = hintChanged;
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        // The root retains its authored height. The independent Window node
        // starts at14 and grows46 for each appended row (7D7150/7C4BF0).
        windowHeight = 14 + 46 * entries.Count;
        var source = new UiSourceLayout(repository, 0xA7513A25);
        var window = source.ResizedSubtree(1, new(154, windowHeight), true);
        window.Art(this, 1, window.Rectangle(1), assets, shader);
        for (int index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var button = new ArmyCommandMenuButton { Name = entry.Name, Position = source.Rectangle(2 + index * 2).Position };
            AddChild(button);
            button.Configure(repository, assets, shader, font, entry.Caption, entry.Icon, entry.Ordinal, () =>
            {
                if (finished || busy || !entry.Enabled) return;
                Finish(); entry.Released();
            });
            button.Disabled = !entry.Enabled; buttons.Add((button, entry.Enabled));
            button.MouseEntered += () => { if (!finished && !busy) narration?.Invoke(entry.Hint); };
            button.MouseExited += () => { if (!finished) narration?.Invoke(null); };
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void SetBusy(bool value)
    {
        busy = value;
        foreach (var row in buttons) row.Button.Disabled = finished || value || !row.Enabled;
        if (value) narration?.Invoke(null);
    }
    private void Finish()
    {
        finished = true; Hide(); policyPositioned?.Invoke(null); narration?.Invoke(null);
        foreach (var row in buttons) row.Button.Abandon();
    }
    internal void Abandon()
    {
        Finish(); project = null; narration = null; policyPositioned = null;
    }
    internal void TrackAnchor(Func<Vector2?> currentProjection, Action<Vector2?>? placePolicy = null, int footerBoundary = 912,
        int policyAvoidanceHeight = 240)
    {
        project = currentProjection; policyPositioned = placePolicy; footerTop = footerBoundary;
        this.policyAvoidanceHeight = policyAvoidanceHeight;
        ApplyProjectedAnchor(project());
    }
    public override void _Process(double delta)
    { if (!finished && project is { } current) ApplyProjectedAnchor(current()); }

    internal void ApplyProjectedAnchor(Vector2? projected)
    {
        if (projected is not Vector2 point)
        { Hide(); policyPositioned?.Invoke(null); narration?.Invoke(null); return; }
        int x = Math.Max((int)point.X, 0), y = Math.Max((int)point.Y, 0);
        bool flip = x + 525 > 1919;
        if (flip && x + 145 > 1919) x = 1775;
        if (footerTop <= y + windowHeight) y = footerTop - windowHeight;
        Position = new(x, y); Visible = !finished;
        int extra = Math.Min(0, footerTop - policyAvoidanceHeight - y);
        policyPositioned?.Invoke(finished ? null : new Vector2(flip ? x - 380 : x + 150, y + extra));
    }
    public override void _ExitTree()
    {
        narration?.Invoke(null); policyPositioned?.Invoke(null); project = null; narration = null; policyPositioned = null;
        foreach (var row in buttons) row.Button.Abandon();
        font?.Dispose(); assets?.Dispose(); shader?.Dispose();
    }
}

internal partial class ArmyCommandMenuButton : UiSourceListCheckboxButton
{
    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string caption, string? icon, int? ordinal, Action released)
    {
        var wrapper = new UiSourceLayout(repository, 0xA7DA0617);
        base.Configure(repository, assets, shader, font, new(124, 48), caption, released, wrapper.Rectangle(2));
        var palettes = MapSpritePalettes.Create();
        if (icon is not null)
        {
            wrapper.Art(this, 3, wrapper.Rectangle(3), assets, shader, palettes[9]);
            wrapper.Art(this, 4, wrapper.Rectangle(4), assets, shader, palettes[10], icon);
        }
        else if (ordinal is int number)
            wrapper.Text(this, 5, default, number.ToString(System.Globalization.CultureInfo.InvariantCulture), font,
                MapTextPresets.Create()[13]);
        CaptionControl.Name = Name.ToString() + "Caption";
        MoveChild(CaptionControl, GetChildCount() - 1);
    }
}

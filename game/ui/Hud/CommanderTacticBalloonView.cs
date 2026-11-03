using Godot;
using San14.Data;

/// <summary>The command card's compact tactic balloon. It borrows application
/// resources and consumes the card's publication, never a hover-time query.</summary>
internal partial class CommanderTacticBalloonView : Control
{
    private sealed record Body(Control Root, UiSourceLayout Layout, UiSourceText[] Lines);
    private sealed record Warning(Control Root, UiSourceLayout Layout, UiSourceText? Line, UiSourceParagraph? Paragraph);
    private readonly Body[] bodies = new Body[2];
    private readonly Warning[] warnings = new Warning[2];
    private IReadOnlyDictionary<int, string> reasons = null!;
    private IReadOnlyDictionary<int, CommanderTacticBalloonDescription> descriptions = null!;
    private int effectCount;
    private MapFont font = null!;
    private static readonly MapTextPresets Presets = MapTextPresets.Create();
    private int bodyIndex;
    private bool previousEnabled;
    private bool warningVisible;
    private int? currentTactic;

    internal static readonly int[] ReasonMessages = [0xC964, 0xC95F, 0xC962, 0xC963];
    internal static readonly string[] LayoutNames = [
        "11_00_01_offset_00", "11_00_01_offset_01", "11_00_01_offset_02", "11_00_01_offset_03"];

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font, string locale)
    {
        this.font = font;
        Name = "CommanderTacticBalloon"; MouseFilter = MouseFilterEnum.Ignore; ZIndex = 20;
        var messages = new AdministrationMessages(repository, locale);
        reasons = ReasonMessages.ToDictionary(id => id, id => messages.Format(id));
        // Labels follow this borrowed UI locale, including when the save was
        // created in the other language. These exact strings are prepared at startup.
        descriptions = CommanderTacticBalloonDescriptions.Read(repository, locale);
        for (int index = 0; index < LayoutNames.Length; index++)
        {
            var layout = assets.SourceLayout(LayoutNames[index]);
            var root = new Control { Name = "TacticTemplate" + index, Size = layout.Rectangle(0).Size,
                MouseFilter = MouseFilterEnum.Ignore, Visible = false };
            AddChild(root);
            layout.Blur(root, 1, layout.Rectangle(1));
            foreach (int node in layout.Nodes.Where(node => node.Type is 1 or 3).Select(node => node.Index))
                layout.Art(root, node, layout.Rectangle(node), assets, shader);
            if (index < 2)
            {
                int[] nodes = index == 0 ? [4] : [4, 6];
                bodies[index] = new(root, layout, nodes.Select(node => layout.Text(root, node, default, "", font)).ToArray());
            }
            else
            {
                var box = layout.Rectangle(4);
                if (index == 2)
                    warnings[0] = new(root, layout, layout.Text(root, 4, default, "", font, Presets[54]), null);
                else
                {
                    var paragraph = new UiSourceParagraph { Name = "CommanderTacticReason", Position = box.Position, Size = box.Size };
                    root.AddChild(paragraph);
                    paragraph.Configure(font, WarningProfile(layout));
                    warnings[1] = new(root, layout, null, paragraph);
                }
            }
        }
        Hide();
    }

    internal static MapFontProfile WarningProfile(UiSourceLayout layout) =>
        MapFontProfile.FromNode(layout.Nodes[4], layout.Source.Source.ResourceUri).WithPreset(Presets[54]);

    internal void Display(CommanderHudTacticSlot slot, bool newHover, Vector2 pointer)
    {
        if (slot.Balloon is not { } value) { Clear(); return; }
        bool entering = newHover || currentTactic != slot.TacticId || !Visible;
        bool enabled = slot.Enabled == true;
        if (entering)
        {
            ClearWarnings();
            foreach (var retained in bodies) retained.Root.Hide();
            effectCount = value.Effects.Count;
            bodyIndex = effectCount == 1 ? 0 : 1;
            var body = bodies[bodyIndex];
            // Opcode0 performs no text write in the original. The zero-effect
            // branch retains the two-row template and its previous text.
            for (int index = 0; index < effectCount; index++)
            {
                var line = body.Lines[index];
                if (index < value.Effects.Count)
                {
                    var effect = value.Effects[index];
                    int node = 4 + index * 2;
                    line.Configure(font, MapFontProfile.FromNode(body.Layout.Nodes[node], body.Layout.Source.Source.ResourceUri)
                        .WithPreset(Presets[effect.TextPreset]));
                    line.Text = descriptions[effect.Opcode].Text;
                }
                else line.Text = "";
            }
            body.Root.Show(); currentTactic = slot.TacticId;
        }
        // The live source updates a nonzero reason while disabled. A known
        // reason0 with unchanged enabled state retains its existing warning.
        // A fresh hover always starts without that previous warning.
        if (!enabled && value.ReasonMessageId is > 0)
        {
            if (effectCount != 0) ApplyWarning(value.ReasonMessageId.Value);
        }
        else if (!entering && previousEnabled != enabled) ClearWarnings();
        previousEnabled = enabled;
        var bodySize = bodies[bodyIndex].Root.Size;
        Size = bodySize + new Vector2(0, warningVisible ? CurrentWarningHeight() - 12 : 0);
        if (slot.BalloonUnresolvedInput is string issue) SetMeta("unresolved_owner", issue);
        else RemoveMeta("unresolved_owner");
        Show(); UpdatePointer(pointer);
    }

    private void ApplyWarning(int message)
    {
        if (!reasons.TryGetValue(message, out string? text))
            throw new InvalidDataException("Unsupported commander tactic reason message.");
        int index = text.Length * 2 > 16 ? 1 : 0;
        for (int i = 0; i < warnings.Length; i++) warnings[i].Root.Visible = i == index;
        var warning = warnings[index];
        warning.Root.Position = new(0, bodies[bodyIndex].Root.Size.Y - 12);
        if (warning.Line is { } line) line.Text = text;
        else warning.Paragraph!.Apply(text);
        warningVisible = true;
    }

    private float CurrentWarningHeight() => warnings[1].Root.Visible ? warnings[1].Root.Size.Y : warnings[0].Root.Size.Y;
    private void ClearWarnings()
    {
        foreach (var warning in warnings) warning.Root.Hide();
        warningVisible = false;
    }
    internal void Clear()
    {
        ClearWarnings(); currentTactic = null; Hide();
    }
    internal void UpdatePointer(Vector2 pointer) => Position = new(
        Math.Clamp((int)pointer.X + 20, 0, Math.Max(0, 1920 - Size.X)),
        Math.Clamp((int)pointer.Y + 20, 0, Math.Max(0, 1080 - Size.Y)));
}

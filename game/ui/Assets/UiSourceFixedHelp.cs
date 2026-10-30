using Godot;

/// <summary>The original fixed-height help cell used by ordinary settings.
/// Explicit LF rows share one nominal-line advance and block alignment.</summary>
internal partial class UiSourceFixedHelp : Control
{
    private MapFont font = null!;
    private MapFontProfile profile = null!;
    private string? current;
    internal void Configure(MapFont font, MapFontProfile profile)
    {
        if (profile.RawFlags != 4 || profile.RawAlign != 1 || profile.RawLineArrange != 1 || profile.WidthFitMode != 0)
            throw new InvalidDataException("Settings help requires its fixed-height centred source paragraph.");
        this.font = font; this.profile = profile; MouseFilter = MouseFilterEnum.Ignore;
    }
    internal void Apply(string message)
    {
        if (current == message) return;
        string[] lines = message.Split('\n');
        var lineProfile = profile with { RawAlign = 0, VerticalSpace = 0 };
        font.Prepare(lines, lineProfile);
        if (lines.Any(line => font.Measure(line, lineProfile).Width > Size.X))
            throw new InvalidDataException("Settings help requires its source wrapping branch.");
        int advance = (int)(font.Source.Record('\u3042', profile.Face).Height * profile.ScaleY + profile.VerticalSpace);
        int top = (int)((Size.Y - (lines.Length * advance - profile.VerticalSpace)) * .5f);
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        for (int index = 0; index < lines.Length; index++)
        {
            var line = new UiSourceText { Name = "HelpLine" + index, Position = new(0, top + index * advance),
                Size = new(Size.X, advance - profile.VerticalSpace) };
            AddChild(line); line.Configure(font, lineProfile); line.Text = lines[index];
        }
        current = message;
    }
}

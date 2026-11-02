using Godot;
using San14.Data;

/// <summary>The source movie's scenario-backed timed caption overlay. The
/// caption clock is the movie clock, independent of UI update frequency.</summary>
internal partial class CollectionMovieSubtitleView : UiSourceAnimationView
{
    private IReadOnlyList<CollectionMovieSubtitle> entries = [];
    private UiSourceMultilineText paragraph = null!;
    private MapFont? font;
    private bool ownsFont;
    private int selected = -1;

    internal void Configure(GameResourceRepository repository, PreviewText text, CollectionMoviePlayback source, AdministrationPageChrome.Resources? resources = null)
    {
        Name = "OriginalMovieSubtitles"; AutomaticAnimationClock = false; entries = source.Subtitles;
        ownsFont = resources is null; font = resources?.Font ?? new(repository, text.SourceLocale);
        var layout = resources?.Assets.SourceLayout(source.SubtitleLayoutName) ??
            new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, source.SubtitleLayoutName));
        ConfigureAnimation(layout, new HashSet<string> { "in", "loop", "out" }, Enumerable.Range(0, layout.Nodes.Count));
        // Node1 is the dynamic movie capture plane, supplied by the decoded
        // frame view below this overlay, rather than a static atlas image.
        var caption = layout.Nodes[source.SubtitleNodeIndex];
        paragraph = new() { Name = "MovieCaption", Size = new(caption.Value44, caption.Value46) };
        AnimationPanes[caption.Index].AddChild(paragraph);
        paragraph.Configure(font, MapFontProfile.FromNode(caption, layout.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[source.SubtitleTextPreset]));
        SetMeta("source_layout", layout.Source.Source.ResourceUri);
    }

    internal void AdvanceTo(double milliseconds)
    {
        int current = selected;
        if (current < 0) current = 0;
        while (current < entries.Count && milliseconds >= entries[current].EndMs) current++;
        if (current >= entries.Count) { Hide(); selected = current; return; }
        var line = entries[current];
        if (milliseconds < line.StartMs) { Hide(); return; }
        if (selected != current) { selected = current; paragraph.Apply(line.AlreadyExpandedText); }
        if (milliseconds >= line.EndMs - 800) ApplyAnimationAt("out", (milliseconds - (line.EndMs - 800)) / 1000);
        else if (milliseconds >= line.StartMs + 1200) ApplyAnimationAt("loop", (milliseconds - line.StartMs - 1200) / 1000, repeat: true);
        else ApplyAnimationAt("in", (milliseconds - line.StartMs) / 1000);
    }

    internal void Fit(Vector2 canvasSize)
    {
        float factor = Math.Min(canvasSize.X / 1920, canvasSize.Y / 1080);
        Scale = Vector2.One * factor; Position = (canvasSize - new Vector2(1920, 1080) * factor) / 2;
    }
    public override void _ExitTree() { if (ownsFont) font?.Dispose(); }
}

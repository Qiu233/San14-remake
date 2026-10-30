using Godot;
using San14.Data;

/// <summary>The retained original focus/press child of a commander card.</summary>
internal partial class MapCommanderFocusView : UiSourceAnimationView
{
    private readonly Dictionary<string, double> durations = [];
    private string? selected;
    private double elapsed;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, Vector2 size)
    {
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "00_82_02_jump_offset_22"))
            .ResizedSubtree(0, size, false);
        var names = new HashSet<string> { "1_forcus", "3_push" };
        foreach (var clip in LcskAnimationReader.Read(source.Source.Layout, names))
            durations.Add(clip.Name, clip.DurationSeconds);
        AutomaticAnimationClock = false;
        ConfigureAnimation(source, names, source.Nodes.Select(node => node.Index));
        foreach (var node in source.Nodes.Where(node => node.Type is 1 or 3))
            source.Art(AnimationPanes[node.Index], node.Index,
                new(Vector2.Zero, source.Rectangle(node.Index).Size), assets, shader, localCoordinates: true);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetProcess(false);
    }

    internal void ApplyInteraction(bool hovered, bool pressed)
    {
        string? next = pressed ? "3_push" : hovered ? "1_forcus" : null;
        if (next == selected) return;
        selected = next; elapsed = 0;
        if (selected is null) { Hide(); SetProcess(false); return; }
        ApplyAnimationAt(selected, elapsed);
        SetProcess(durations[selected] > 0);
    }

    public override void _Process(double delta)
    {
        if (selected is null) return;
        elapsed = Math.Min(elapsed + delta, durations[selected]);
        ApplyAnimationAt(selected, elapsed);
        if (elapsed >= durations[selected]) SetProcess(false);
    }
}

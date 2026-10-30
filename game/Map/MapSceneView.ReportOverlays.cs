using Godot;

public partial class MapSceneView
{
    // Strategic canvas overlays share one passive display parent. Native and
    // ordinary map images stay on their existing source-camera mount.
    private Control? strategicMapOverlays;
    private Control? reportMapOverlayOwner;
    private bool reportMapOverlayPriorVisible;

    private Control StrategicMapOverlayParent()
    {
        if (strategicMapOverlays is { } retained) return retained;
        var parent = _sourceMainCameraAspect as Control ?? _surface.GetParent() as Control ?? this;
        var group = new Control { Name = "StrategicMapOverlays", MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(group);
        group.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        strategicMapOverlays = group;
        return group;
    }

    // The one report-sequence background owns this temporary display scope.
    // A hidden parent preserves every child's independently updated Visible
    // state, including information cards and accepted army/Scenery labels.
    internal void SetReportMapOverlaysSuppressed(Control owner, bool suppressed)
    {
        if (suppressed)
        {
            if (reportMapOverlayOwner == owner) return;
            if (reportMapOverlayOwner is not null)
                throw new InvalidOperationException("Another report background owns the map overlays.");
            var group = StrategicMapOverlayParent();
            reportMapOverlayPriorVisible = group.Visible;
            reportMapOverlayOwner = owner;
            group.Hide();
        }
        else if (reportMapOverlayOwner == owner)
        {
            reportMapOverlayOwner = null;
            if (strategicMapOverlays is { } group && GodotObject.IsInstanceValid(group))
                group.Visible = reportMapOverlayPriorVisible;
        }
    }
}

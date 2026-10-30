using Godot;

/// <summary>The attached lesson observes its real command pages and world.
/// This host schedules observation; it never completes a paragraph itself.</summary>
internal partial class TutorialLessonHost : Node
{
    private Func<bool>? active;
    private Action? observe;
    private Action<Exception>? failed;

    internal void Configure(Func<bool> active, Action observe, Action<Exception> failed)
    {
        this.active = active; this.observe = observe; this.failed = failed;
        Name = "TutorialLessonHost";
    }

    public override void _Process(double delta)
    {
        if (active?.Invoke() != true) return;
        try { observe?.Invoke(); }
        catch (Exception error)
        {
            observe = null;
            failed?.Invoke(error);
        }
    }

    internal void Abandon() { active = null; observe = null; failed = null; }
    public override void _ExitTree() => Abandon();
}

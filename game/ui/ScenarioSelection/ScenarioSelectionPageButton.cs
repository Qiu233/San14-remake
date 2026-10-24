using Godot;
using San14.Data;

/// <summary>The scenario pager's source style-16/17 common button.</summary>
internal partial class ScenarioSelectionPageButton : UiSourceButton
{
    private Rect2 hit;
    private Action? released;

    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted &&
        point.X >= hit.Position.X && point.Y >= hit.Position.Y && point.X <= hit.End.X && point.Y <= hit.End.Y;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        bool right, Action released)
    {
        this.released = released;
        var source = new UiSourceLayout(repository, right ? 0x3E2AD14Fu : 0xE6446CC9u);
        Size = source.Rectangle(0).Size;
        // Unlike the scenario card, the common-button constructor binds its
        // named HitArea through the recursive resize path.
        source = source.ResizedSubtree(0, Size, true);
        var rectangle = source.Rectangle(1);
        hit = new(new Vector2((int)rectangle.Position.X, (int)rectangle.Position.Y),
            new Vector2((int)rectangle.Size.X, (int)rectangle.Size.Y));
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        // Independent additive tracks6/9/14 are deferred.
        ConfigureStates(states);
        ActionMode = ActionModeEnum.Release;
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    public override void _ExitTree() { released = null; InputAdmission = null; }
}

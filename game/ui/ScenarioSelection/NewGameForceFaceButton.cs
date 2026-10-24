using Godot;
using San14.Data;

// The row's force and the ruler's current force are distinct source owners.
// The list paints selection by Person identity and activates the latter force.
internal sealed record NewGameForceFaceContent(int ForceId, int RulerId,
    int? SelectionForceId, LoadedSprite? Portrait);

/// <summary>The original startup Face1 button. All five states share one
/// resolved portrait; masks and the authored overhanging geometry stay intact.</summary>
internal partial class NewGameForceFaceButton : UiSourceButton
{
    private readonly UiSourcePortrait?[] portraits = new UiSourcePortrait?[5];
    private readonly Control[] layers = new Control[5];
    private static readonly int[] portraitNodes = [4, 8, 14, 19, 23];
    private Rect2 hit;
    private UiSourceLayout source = null!;
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private LoadedSprite? currentPortrait;
    private Action<int?>? hovered;
    private Action<int>? activated;
    internal NewGameForceFaceContent? Content { get; private set; }

    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        Action<int?> hovered, Action<int> activated)
    {
        this.repository = repository; this.assets = assets; this.shader = shader;
        this.hovered = hovered; this.activated = activated;
        source = new(repository, 0x0CF6FDA2);
        Size = source.Rectangle(0).Size;
        hit = source.Rectangle(1);
        ClipContents = false;
        ActionMode = ActionModeEnum.Release;
        int[] frames = [5, 9, 15, 20, 24];
        for (int index = 0; index < layers.Length; index++)
        {
            var layer = new Control { Name = "FaceState" + index, Size = Size,
                MouseFilter = MouseFilterEnum.Ignore };
            AddChild(layer); layers[index] = layer;
            // The portrait precedes its frame. Animated additive tracks are
            // deferred with the other source button animations.
            var slot = new Control { Name = "PortraitSlot", Size = Size,
                MouseFilter = MouseFilterEnum.Ignore };
            layer.AddChild(slot);
            source.Art(layer, frames[index], source.Rectangle(frames[index]), assets, shader);
        }
        ConfigureStates(layers);
        MouseEntered += () => { if (SourceInputAdmitted && Content is { } content) this.hovered?.Invoke(content.SelectionForceId); };
        MouseExited += () => { if (SourceInputAdmitted) this.hovered?.Invoke(null); };
        Pressed += () =>
        {
            if (!Disabled && SourceInputAdmitted && Content?.SelectionForceId is int force)
                this.activated?.Invoke(force);
        };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        ApplyContent(null);
    }

    internal void ApplyContent(NewGameForceFaceContent? content)
    {
        Content = content;
        if (content is null) { Hide(); return; }
        Disabled = content.SelectionForceId is null;
        if (!ReferenceEquals(currentPortrait, content.Portrait))
        {
            currentPortrait = content.Portrait;
            for (int index = 0; index < portraits.Length; index++)
            {
                var previous = portraits[index];
                if (previous is not null)
                {
                    previous.GetParent()?.RemoveChild(previous);
                    previous.QueueFree(); portraits[index] = null;
                }
                if (content.Portrait is not { } sprite) continue;
                Rect2 rectangle = source.Rectangle(portraitNodes[index]);
                var portrait = new UiSourcePortrait { Name = "RulerFace", Position = rectangle.Position,
                    Size = rectangle.Size };
                try { portrait.ConfigureFace1(repository, assets, shader, sprite); }
                catch { portrait.Free(); throw; }
                layers[index].GetNode<Control>("PortraitSlot").AddChild(portrait);
                portraits[index] = portrait;
            }
        }
        SetMeta("force_id", content.ForceId); SetMeta("ruler_id", content.RulerId);
        Show();
    }

    internal void Abandon()
    { hovered = null; activated = null; InputAdmission = null; Disabled = true; Content = null; Hide(); }
    public override void _ExitTree() { hovered = null; activated = null; InputAdmission = null; }
}

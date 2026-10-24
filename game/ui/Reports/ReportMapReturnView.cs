using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Read-only map navigation from a report, with the original common
/// return controls. Returning resumes the same report without completing it.</summary>
internal partial class ReportMapReturnView : Control
{
    private Control canvas = null!;
    private UiSourceHeader? title;
    private UiSourceReportFooter? footer;
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Action? returned;

    internal void Configure(GameResourceRepository repository, string locale,
        UiSourceHeaderStack headers, Action returned)
    {
        this.returned = returned;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        canvas = new Control { Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        assets = new UiAssets(repository); font = new MapFont(repository, locale); shader = UiSourceSprite.CreateShader();
        footer = new UiSourceReportFooter { Name = "ReturnToReport" };
        canvas.AddChild(footer);
        footer.Configure(repository, assets, shader, locale, false, RequestBack,
            () => this.returned is not null && IsVisibleInTree() && !IsQueuedForDeletion());
        title = new UiSourceHeader { Name = "ReportMapTitle" };
        canvas.AddChild(title);
        title.Configure(repository, assets, shader, font,
            repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records[0x329].Text
                ?? throw new InvalidDataException("Missing report map caption."));
        title.Publish(headers);
        Resized += Fit;
        Fit();
    }

    internal void RequestBack() => returned?.Invoke();

    internal void Retire()
    {
        returned = null;
        Release(footer); footer = null;
        Release(title); title = null;
        Hide();
    }

    private static void Release(Control? control)
    {
        if (control is null || !GodotObject.IsInstanceValid(control)) return;
        if (control.GetParent() is Node parent) parent.RemoveChild(control);
        control.QueueFree();
    }

    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }

    public override void _ExitTree()
    {
        returned = null;
        font?.Dispose(); font = null;
        assets?.Dispose(); assets = null;
        shader?.Dispose(); shader = null;
    }
}

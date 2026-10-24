using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The false-report destination state has its own common heading and
/// return footer over the map. A legal map release belongs to its page owner.</summary>
internal partial class ArmyFalseReportTargetView : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!;
    private UiSourceHeader header = null!;
    private UiSourceRoundButton back = null!;
    private UiSourceText hint = null!;
    private string allowedHint = "", blockedHint = "";
    private Action? returned;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        UiSourceHeaderStack headers, Action returned)
    {
        this.returned = returned;
        Name = "ArmyFalseReportTarget"; ZIndex = 4; MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale);
        shader = UiSourceSprite.CreateShader();
        canvas = new Control { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        //30AE60(17) uses Data0. The parent command menu uses another bank.
        string caption = repository.LoadMessages(text.SourceLocale, MessageKind.Data)
            .Messages.Sections[0].Records[0x114].Text
            ?? throw new InvalidDataException("Missing false-report header caption.");
        header = new UiSourceHeader { Name = "FalseReportTitle" }; canvas.AddChild(header);
        header.Configure(repository, assets, shader, font, caption);
        header.Publish(headers);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        Vector2 mount = new(0, 904);
        back = new UiSourceRoundButton { Name = "ReturnFalseReportTarget",
            Position = footer.Rectangle(1, mount).Position, ActionMode = BaseButton.ActionModeEnum.Release };
        canvas.AddChild(back);
        back.Configure(repository, assets, shader, text.SourceLocale,
            "000_cmn_txt_decision_modoru", () => { if (!abandoned) this.returned?.Invoke(); }, UiAudioCue.Cancel);
        var narration = new AdministrationMessages(repository, text.SourceLocale);
        allowedHint = narration.Format(0x56C3); blockedHint = narration.Format(0x56C2);
        var hintSource = new UiSourceLayout(repository, 0xAB6ADAA7);
        Vector2 hintMount = footer.Rectangle(19, mount).Position;
        foreach (var node in hintSource.Nodes.Where(node => node.Type is 1 or 3))
            hintSource.Art(canvas, node.Index, hintSource.Rectangle(node.Index, hintMount), assets, shader);
        hint = hintSource.Text(canvas, hintSource.Nodes.Single(node => node.Type == 2).Index,
            hintMount, allowedHint, font); hint.Name = "FalseReportHint";
        Resized += Fit; Fit();
    }

    //7FC770 does not clear the last footer text when no valid Hex was read.
    internal void ApplyHover(bool? allowed)
    {
        if (!abandoned && allowed is bool known) hint.Text = known ? allowedHint : blockedHint;
    }

    internal void Abandon()
    {
        abandoned = true; returned = null; if (back is not null) back.Disabled = true; Hide();
    }

    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale;
        canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }

    public override void _ExitTree()
    {
        returned = null; font?.Dispose(); assets?.Dispose(); shader?.Dispose();
    }
}

using Godot;
using San14.Data;

/// <summary>The report's single style-3 footer. Its presentation owner supplies
/// guide visibility and owns the later report transition and shortcut policy.</summary>
internal partial class UiSourceReportFooter : Control
{
    private UiSourceRoundButton confirm = null!;
    private Control guide = null!;
    private Action? confirmed;

    internal bool ConfirmEnabled
    {
        get => !confirm.Disabled;
        set => confirm.Disabled = !value;
    }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, bool inputGuidesVisible, Action confirmed, Func<bool> inputAdmitted)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        this.confirmed = confirmed;
        ArgumentNullException.ThrowIfNull(inputAdmitted);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        Size = footer.Rectangle(0).Size;
        Position = new(0, 1080 - Size.Y);
        MouseFilter = MouseFilterEnum.Pass;
        var box = footer.Rectangle(1);
        confirm = new UiSourceRoundButton
        {
            Name = "ReportConfirm", Position = box.Position, Size = box.Size,
            ActionMode = BaseButton.ActionModeEnum.Release
        };
        AddChild(confirm);
        confirm.ConfigureReportConfirm(repository, assets, shader, locale, () => this.confirmed?.Invoke());
        confirm.InputAdmission = inputAdmitted;

        var button = new UiSourceLayout(repository, 0x2F276C90);
        var guideBox = button.Rectangle(18);
        guide = new Control
        {
            Name = "ConfirmInputGuide", Position = guideBox.Position, Size = guideBox.Size,
            MouseFilter = MouseFilterEnum.Ignore
        };
        confirm.AddChild(guide);
        var pad = new UiSourceLayout(repository, 0x95761708);
        pad.Art(guide, 1, pad.Rectangle(1), assets, shader,
            commonSpriteOverride: "base_pad", commonTexturePack: "texture_pad");
        pad.Art(guide, 2, pad.Rectangle(2), assets, shader,
            commonSpriteOverride: "pad_triangle", commonTexturePack: "texture_pad");
        guide.SetMeta("source_layout", pad.Source.Source.ResourceUri);
        SetInputGuidesVisible(inputGuidesVisible);
        SetMeta("source_layout", footer.Source.Source.ResourceUri);
    }

    internal void SetInputGuidesVisible(bool visible) => guide.Visible = visible;

    internal void RequestConfirmation() => confirm.ActivateFromSource();

    public override void _ExitTree() => confirmed = null;
}

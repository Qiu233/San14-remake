using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The source four-choice grain amount dialog. The command query
/// supplies its three quantities and calculator bounds; this page edits no world.</summary>
internal partial class MerchantAmountMenuView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<MerchantAmountChoiceButton> choices = [];
    private DeploymentCalculatorDialog? calculator;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private int current, minimum, maximum;
    private int calculatorTitleMessage;
    private Vector2 calculatorPosition;
    private bool abandoned;

    internal bool HasCalculator => calculator is not null;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<int> presetAmounts, int current, int minimum, int maximum,
        Func<bool> admitted, Action<int?> closed, int titleMessage=0x020D, Vector2? calculatorPosition=null,
        string? quantityIconPrefix = null, int? calculatorTitleMessage = null, string? hintText = null,
        string? quantityIconTexturePack = null)
    {
        if (presetAmounts.Count != 3)
            throw new ArgumentException("The source amount dialog requires three published quantities.", nameof(presetAmounts));
        this.repository = repository;
        this.text = text;
        this.current = current;
        this.minimum = minimum;
        this.maximum = maximum;
        this.admitted = admitted;
        this.closed = closed;
        this.calculatorTitleMessage = calculatorTitleMessage ?? titleMessage;
        this.calculatorPosition=calculatorPosition??new(860,22);
        Name = "MerchantAmountMenu";
        ZIndex = 6;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        chrome = new AdministrationPageChrome { Name = "MerchantAmountChrome" };
        AddChild(chrome);
        chrome.Configure(repository, text, titleMessage, RequestCancel, static () => { }, returnOnly: true);
        var narration = new AdministrationMessages(repository, text.SourceLocale);
        chrome.ApplyDefaultHint(hintText ?? narration.Format(titleMessage==0x020C?0x59B7:0x59B8));
        chrome.ApplyReturnHint(narration.Format(0x2733));

        var source = new UiSourceLayout(repository, 0x9E967696);
        Vector2 mount = new(460, 416);
        source.Art(chrome.Canvas, 1, source.Rectangle(1, mount), chrome.Assets, chrome.SpriteShader);
        var data = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string other = data.Records[0x0696].Text ?? throw new InvalidDataException("Missing source arbitrary-amount caption.");
        string[] icons =
        [
            "000_cmn_icon_sel_hyourou_1", "000_cmn_icon_sel_hyourou_2",
            "000_cmn_icon_sel_hyourou_3", "000_cmn_icon_sel_calc"
        ];
        if(titleMessage==0x020C)
            for(int index=0;index<3;index++)icons[index]="000_cmn_icon_sel_kin_"+(index+1);
        if (quantityIconPrefix is not null)
            for (int index = 0; index < 3; index++) icons[index] = quantityIconPrefix + (index + 1);
        for (int index = 0; index < 4; index++)
        {
            int choice = index;
            int amount = index < 3 ? presetAmounts[index] : 0;
            string caption = index == 3 ? other : amount > 0 ? amount.ToString(CultureInfo.InvariantCulture) : "";
            Rect2 box = source.Rectangle(index + 2, mount);
            var button = new MerchantAmountChoiceButton
            {
                Name = "MerchantAmountChoice" + index,
                Position = box.Position,
                Size = box.Size,
                Disabled = index < 3 && amount <= 0,
                InputAdmission = ReadAdmission
            };
            chrome.Canvas.AddChild(button);
            choices.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption, icons[index], () =>
            {
                if (!ReadAdmission()) return;
                if (choice == 3) OpenCalculator();
                else Finish(amount);
            }, index < 3 ? quantityIconTexturePack : null);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    private bool Current() => !abandoned && closed is not null && IsVisibleInTree() && CanProcess() &&
        (admitted?.Invoke() ?? false);
    private bool ReadAdmission() => calculator is null && Current();

    private void OpenCalculator()
    {
        var child = new DeploymentCalculatorDialog();
        calculator = child;
        AddChild(child);
        try
        {
            child.Configure(repository, text, current, minimum, maximum, result =>
            {
                if (calculator != child || !Current()) return;
                calculator = null;
                child.Abandon();
                child.QueueFree();
                // The original four-choice dialog has already returned Other.
                // Cancelling its calculator returns to the merchant unchanged.
                Finish(result);
            }, panelPosition: calculatorPosition, titleMessage: calculatorTitleMessage);
            child.Name = "MerchantAmountCalculator";
            chrome.Hide();
        }
        catch
        {
            calculator = null;
            child.Abandon();
            child.QueueFree();
            chrome.Show();
            throw;
        }
    }

    internal void RequestCancel()
    {
        if (!Current()) return;
        if (calculator is { } child) child.RequestCancel();
        else Finish(null);
    }

    private void Finish(int? amount)
    {
        var callback = closed;
        if (callback is null || !Current()) return;
        closed = null;
        admitted = null;
        foreach (var choice in choices) choice.Disabled = true;
        Hide();
        callback(amount);
    }

    internal void Abandon()
    {
        abandoned = true;
        closed = null;
        admitted = null;
        var child = calculator;
        calculator = null;
        if (child is not null) { child.Abandon(); child.QueueFree(); }
        foreach (var choice in choices) { choice.InputAdmission = null; choice.Abandon(); }
        choices.Clear();
        chrome?.Abandon();
        Hide();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!ReadAdmission()) return; // The child calculator owns its own keys.
        if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } ||
            inputEvent is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false })
        {
            RequestCancel();
            GetViewport().SetInputAsHandled();
        }
    }
}

/// <summary>CStrategySelectButtonBehavior's actual quantity artwork and icon
/// palettes. This is separate from the common small hanyou command button.</summary>
internal partial class MerchantAmountChoiceButton : UiSourceButton
{
    private Rect2 hit;
    private Action? released;
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string text, string icon, Action released, string? iconTexturePack = null)
    {
        this.released = released;
        var source = new UiSourceLayout(repository, 0x53C224C0);
        Size = source.Rectangle(0).Size;
        Rect2 box = source.Rectangle(1);
        hit = new(new Vector2((int)box.Position.X, (int)box.Position.Y),
            new Vector2((int)box.Size.X, (int)box.Size.Y));
        ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        var caption = source.Text(this, 16, default, text, font);
        caption.Name = "Caption";
        var profile = MapFontProfile.FromNode(source.Nodes[16], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        var palettes = MapSpritePalettes.Create();
        var picture = source.Art(this, 17, source.Rectangle(17), assets, shader,
            colors: palettes[25], commonSpriteOverride: icon, commonTexturePack: iconTexturePack);
        picture.Name = "QuantityIcon";
        var sprites = picture.FindChildren("*", "", true, false).OfType<UiSourceSprite>().ToArray();
        ConfigureStates(states, state =>
        {
            caption.Configure(font, profile.WithPreset(presets[3 + state]));
            caption.QueueRedraw();
            foreach (var sprite in sprites) sprite.SetColors(palettes[25 + state]);
        });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_sprite", icon);
    }

    internal void Abandon() { released = null; Disabled = true; }
}

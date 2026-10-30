using Godot;
using San14.Data;

/// <summary>The original labeled editing row: four function buttons, the
/// common slider and a separate calculator button. It owns no world state.</summary>
internal partial class UiSourceEditSlider : Control
{
    internal static IReadOnlyList<string> FunctionIcons { get; } = Array.AsReadOnly(new[]
        { "000_cmn_icon_sld_min", "000_cmn_icon_sld_l", "000_cmn_icon_sl_r", "000_cmn_icon_sl_max" });
    private readonly List<UiSourceFunctionButton> functions = [];
    private UiSourceSlider slider = null!;
    private UiSourceNumberButton number = null!;
    private Func<bool>? admitted;
    private Action<int>? changed;
    private Action? calculate;
    private int minimum, maximum, step, current;
    private bool enabled, abandoned;

    internal int Value => current;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string caption, int minimum, int maximum, int step, int value, bool enabled,
        Func<bool> admitted, Action<int> changed, Action calculate,
        IReadOnlyList<string> functionIcons, bool extended = false)
    {
        if (minimum > maximum || step <= 0 || functionIcons.Count != 4)
            throw new ArgumentException("Invalid editing-slider bounds or function artwork.");
        this.minimum = minimum; this.maximum = maximum; this.step = step; current = value;
        this.admitted = admitted; this.changed = changed; this.calculate = calculate;
        MouseFilter = MouseFilterEnum.Pass;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,
            extended ? "02_05_01_00_slider_offset_00_en" : "02_05_01_00_slider_offset_00"));
        Size = source.Rectangle(0).Size;
        if (caption.Length != 0)
        {
            source.Art(this, 1, source.Rectangle(1), assets, shader);
            source.Text(this, 2, default, caption, font, MapTextPresets.Create()[13]);
        }
        int[] nodes = [3, 6, 12, 15];
        for (int index = 0; index < nodes.Length; index++)
        {
            int action = index;
            var button = new UiSourceFunctionButton { Name = "EditSliderFunction" + index,
                Position = source.Rectangle(nodes[index]).Position, InputAdmission = CanEdit };
            AddChild(button); functions.Add(button);
            button.Configure(repository, assets, shader, functionIcons[index], () =>
            {
                if (!CanEdit()) return;
                long candidate = action switch { 0 => minimum, 1 => (long)current - step,
                    2 => (long)current + step, _ => maximum };
                ChangeValue((int)Math.Clamp(candidate, minimum, maximum));
            });
            button.SetMeta("source_node_index", nodes[index]);
        }
        var box = source.Rectangle(9);
        slider = new() { Name = "EditSliderTrack", Position = box.Position, Size = box.Size };
        AddChild(slider);
        slider.Configure(repository, assets, shader, minimum, maximum, step, value, enabled,
            amount => { if (CanEdit()) ChangeValue(amount); });
        box = source.Rectangle(18);
        number = new() { Name = "EditSliderCalculator", Position = box.Position, Size = box.Size,
            InputAdmission = CanEdit };
        AddChild(number);
        number.Configure(repository, assets, shader, font, current, () => { if (CanEdit()) this.calculate?.Invoke(); });
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetEnabled(enabled);
    }

    private bool CanEdit() => !abandoned && enabled && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void ChangeValue(int amount)
    {
        if (!CanEdit()) return;
        current = Math.Clamp(amount, minimum, maximum);
        slider.SetValue(current, maximum, enabled); number.Value = current;
        changed?.Invoke(current);
    }
    internal void SetValue(int amount, int maximum, bool enabled)
    {
        if (abandoned) return;
        this.maximum = maximum; current = amount; number.Value = current;
        SetEnabled(enabled);
    }
    internal void SetEnabled(bool value)
    {
        if (abandoned) return;
        enabled = value;
        foreach (var button in functions) button.Disabled = !value;
        if (number is not null) number.Disabled = !value;
        if (slider is not null) slider.SetValue(current, maximum, value);
    }
    internal void ApplyUnavailable(string caption)
    {
        SetEnabled(false); number.Caption = caption;
    }
    internal void Abandon()
    {
        if (abandoned) return;
        SetEnabled(false); abandoned = true; admitted = null; changed = null; calculate = null;
        foreach (var button in functions) button.InputAdmission = null;
        if (number is not null) number.InputAdmission = null;
    }
    public override void _ExitTree() => Abandon();
}

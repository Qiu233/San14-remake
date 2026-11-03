using Godot;
using San14.Data;

public partial class PlayableForceHud
{
    // CMainTopBtnGroupLayout owns one CSimpleBalloon for this finite group.
    // Bind only its actual controls; captions are not invented descriptions.
    internal void ConfigureTopButtonHints(string locale)
    {
        var bindings = new Dictionary<Control, Func<int>>();
        Add("MapRulerHomeButton", 0x2ADD);
        Add("MapNorthButton", 0x2ADE);
        Add("MapHelpButton", 0x2AD5);
        Add("MapInformationButton", 0x2AE5);
        Add("MapSystemButton", 0x2AE6);
        Add("MapMerchantButton", 0xA487);
        Add("MapGeographicAdvantagesButton", 0x2AE4);
        if (informationLayerButton is { } view)
            bindings.Add(view, () => informationLayerPopup?.Visible == true ? 0x2AE7 : 0x2AE8);
        int[] layerMessages = [0x2AE3, 0x2ADF, 0x2AE9, 0x2AE0, 0x2AE1, 0x2AE2];
        for (int index = 0; index < informationLayerChoices.Length; index++)
            if (informationLayerChoices[index] is { } button)
            { int message = layerMessages[index]; bindings.Add(button, () => message); }
        var tooltip = new MapTopButtonHints(); AddChild(tooltip);
        tooltip.ConfigureButtons(_repository, _assets!, _spriteShader!, _font!, locale, this, bindings);

        void Add(string name, int message)
        {
            if (GetNodeOrNull<Control>(name) is { } button) bindings.Add(button, () => message);
        }
    }
}

/// <summary>The top group's existing source tooltip with its actual hovered
/// control identity. It owns no world, resources, timer or global registry.</summary>
internal partial class MapTopButtonHints : UiSourceSimpleTooltip
{
    internal static readonly int[] MessageIds =
        [0x2ADD,0x2ADE,0x2AD5,0x2AE5,0x2AE6,0xA487,0x2AE3,0x2ADF,0x2AE9,0x2AE0,0x2AE1,0x2AE2,0x2AE7,0x2AE8,0x2AE4];
    private IReadOnlyDictionary<Control, Func<int>> bindings = null!;
    private IReadOnlyDictionary<int, string> texts = null!;
    private Control owner = null!;
    private Control? current;
    private int? currentMessage;

    internal void ConfigureButtons(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string locale, Control owner, IReadOnlyDictionary<Control, Func<int>> bindings)
    {
        this.owner = owner; this.bindings = bindings;
        var messages = new AdministrationMessages(repository, locale);
        texts = MessageIds.ToDictionary(id => id, id => messages.Format(id));
        Configure(repository, assets, shader, font);
        Name = "MapTopButtonHints";
        SetMeta("source_registered_button_count", bindings.Count);
    }

    public override void _Process(double delta)
    {
        var hovered = owner.IsVisibleInTree() && owner.CanProcess() ? GetViewport().GuiGetHoveredControl() : null;
        if (hovered is null || !bindings.TryGetValue(hovered, out var readMessage) ||
            hovered is UiSourceButton button && !button.SourceInputAdmitted)
        {
            current = null; currentMessage = null; DisplayText(null); return;
        }
        int message = readMessage();
        if (current != hovered || currentMessage != message)
        {
            current = hovered; currentMessage = message;
            DisplayText(texts[message]);
            SetMeta("source_game_message", message);
        }
        UpdatePointer(owner.GetLocalMousePosition());
    }

    public override void _ExitTree()
    { bindings = null!; texts = null!; owner = null!; current = null; currentMessage = null; }
}

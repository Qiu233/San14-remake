using Godot;
using San14.Data;

/// <summary>The common decide/return and report-confirm artwork, sharing the view's assets.</summary>
internal partial class UiSourceRoundButton : UiSourceButton
{
    private UiAudioCue cue;
    private Action? sourceActivation;
    internal override UiAudioCue PressedCue => cue;
    internal override bool SupportsDisabledFeedback => true;

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse &&
            _HasPoint(mouse.Position)) RequestDisabledFeedback();
    }

    // The source's nonphysical v68/v48 path shares feedback and the action,
    // without pretending that a Godot Pressed input event occurred.
    internal void ActivateFromSource()
    {
        if (Disabled || !IsInsideTree() || !IsVisibleInTree() || !CanProcess() || IsQueuedForDeletion()) return;
        if (RequestFeedback()) sourceActivation?.Invoke();
    }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, string caption, Action? pressed = null, UiAudioCue cue = UiAudioCue.ButtonSelect)
    {
        var declared = assets.PrepareCommonCaption(locale, caption);
        ConfigureSource(repository, assets, shader, 0xAD4A3EFA, declared, pressed, cue);
    }

    // The footer owns input guides and shortcut policy. This is the actual
    // style-3 caption and button artwork, with ordinary BaseButton input.
    internal void ConfigureReportConfirm(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, Action? pressed = null)
    {
        string language = locale switch
        {
            "TC" => "tw", "SC" => "ch",
            _ => throw new NotSupportedException($"UI artwork locale {locale} is not supported.")
        };
        var declared = assets.DeclaredSprites.LoadTexturePackSprite(
            $"texture_lang_{language}", $"{language}_000_cmn_txt_decision_kakunin");
        ConfigureSource(repository, assets, shader, 0x2F276C90, declared, pressed, UiAudioCue.ButtonSelect);
        SetMeta("source_style", 3);
        SetMeta("source_guide_message", 0x3CF);
    }

    internal void ConfigureScenarioReturn(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, Action pressed)
    {
        var declared = assets.DeclaredSprites.LoadCommonCaption(locale, "000_cmn_txt_decision_modoru");
        ConfigureSource(repository, assets, shader, 0x2F276C90, declared, pressed, UiAudioCue.Cancel);
        SetMeta("source_style", 1);
        SetMeta("source_guide_message", 0x3BC);
    }

    internal void ConfigureAbort(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, Action pressed)
    {
        string language = locale switch { "TC" => "tw", "SC" => "ch",
            _ => throw new NotSupportedException($"UI artwork locale {locale} is not supported.") };
        var declared = assets.DeclaredSprites.LoadTexturePackSprite(
            $"texture_lang_{language}", $"{language}_000_cmn_txt_decision_chushi");
        ConfigureSource(repository, assets, shader, 0xAD4A3EFA, declared, pressed, UiAudioCue.ButtonSelect);
        SetMeta("source_style", 2);
    }

    internal void ConfigureContinue(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, Action pressed)
    {
        var declared=assets.DeclaredSprites.LoadCommonCaption(locale,"000_cmn_txt_decision_next");
        ConfigureSource(repository,assets,shader,0xAD4A3EFA,declared,pressed,UiAudioCue.ButtonSelect,captionNode:17);
        SetMeta("source_style",10);
    }

    internal void ConfigureCollectionAppreciation(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, Action pressed)
    {
        string language = locale switch { "TC" => "tw", "SC" => "ch",
            _ => throw new NotSupportedException($"UI artwork locale {locale} is not supported.") };
        var declared = assets.DeclaredSprites.LoadTexturePackSprite(
            $"texture_ce_lang_{language}", $"{language}_000_cmn_txt_kansho");
        ConfigureSource(repository, assets, shader, 0xAD4A3EFA, declared, pressed, UiAudioCue.ButtonSelect);
        SetMeta("source_caption", 15);
    }

    internal void ConfigureDiplomaticReply(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, bool accepted, Action pressed)
    {
        var declared = assets.DeclaredSprites.LoadCommonCaption(locale,
            accepted ? "000_cmn_txt_decision_shoudaku" : "000_cmn_txt_decision_kyohi");
        ConfigureSource(repository, assets, shader, 0xAD4A3EFA, declared, pressed,
            accepted ? UiAudioCue.ButtonSelect : UiAudioCue.Cancel);
        SetMeta("source_style", accepted ? 6 : 8);
    }

    internal void ConfigureSelfRecommendationReply(GameResourceRepository repository, UiAssets assets, Shader shader,
        string locale, bool accepted, Action pressed)
    {
        var declared = assets.DeclaredSprites.LoadCommonCaption(locale,
            accepted ? "000_cmn_txt_decision_jyudaku" : "000_cmn_txt_decision_kyohi");
        ConfigureSource(repository, assets, shader, 0xAD4A3EFA, declared, pressed,
            accepted ? UiAudioCue.ButtonSelect : UiAudioCue.Cancel);
        SetMeta("source_style", accepted ? 7 : 8);
    }

    private void ConfigureSource(GameResourceRepository repository, UiAssets assets, Shader shader,
        uint file, LoadedSprite declared, Action? pressed, UiAudioCue cue, int captionNode=16)
    {
        this.cue = cue;
        sourceActivation = pressed;
        var layout = assets.SourceLayout(file);
        Size = new(layout.Nodes[0].Value44, layout.Nodes[0].Value46);
        Control[] states = new[] { 3, 5, 8, 12, 14 }
            .Select(index => layout.Art(this, index, layout.Rectangle(index), assets, shader)).ToArray();
        Texture2D texture = assets.SrgbSprite(declared.Definition.Name, declared);
        Rect2 rectangle = layout.Rectangle(captionNode);
        var label = new UiSourceSprite { Name = "Caption", Position = rectangle.Position, Size = rectangle.Size };
        AddChild(label);
        label.Configure(shader, texture, UiSourceSprite.Blend.Ordinary);
        var palettes = MapSpritePalettes.Create();
        ConfigureStates(states, state => label.SetSource(texture, palettes[25 + state]));
        SetMeta("source_layout", layout.Source.Source.ResourceUri);
        SetMeta("caption_sprite", declared.Definition.Name);
        if (pressed is not null) Pressed += pressed;
    }
}

using Godot;
using San14.Data;

/// <summary>A loading/cancel surface over the retained new-game page. It owns
/// no game transaction, progress percentage or narration acknowledgement.</summary>
internal partial class NewGamePreparationView : Control
{
    private bool ownsResources=true;
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!;
    private UiSourceParagraph message = null!;
    private Action? cancelled;
    private bool bodyDrawn, retired, frameSubscribed;
    private readonly TaskCompletionSource<bool> drawn = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task Drawn => drawn.Task;

    internal void Configure(GameResourceRepository repository, PreviewText text, Action cancel,
        AdministrationPageChrome.Resources? resources=null, string? messageText=null, bool showCancel=true)
    {
        Name = "NewGamePreparation"; ZIndex = 100; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); cancelled = cancel;
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);font=resources?.Font??new(repository,text.SourceLocale);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        canvas = new Control { Name = "PreparationCanvas", Size = new(1920,1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        var source = assets.SourceLayout(0xF6332D26);
        Vector2 mount = new((1920-source.Nodes[0].Value44)/2f, 980-source.Nodes[0].Value46);
        var mask = source.Art(canvas, 1, source.Rectangle(1,mount), assets, shader, blend:UiSourceSprite.Blend.AlphaMask);
        var blur = source.Rectangle(2,mount);
        source.Blur(mask, 2, new(blur.Position-mask.Position,blur.Size));
        source.Art(canvas, 3, source.Rectangle(3,mount), assets, shader);
        var box = source.Rectangle(4,mount);
        message = new UiSourceParagraph { Name="PreparationStatus",Position=box.Position,Size=box.Size };
        canvas.AddChild(message);
        message.ConfigureFormattedMessage(font, MapFontProfile.FromNode(source.Nodes[4],source.Source.Source.ResourceUri));
        message.Apply(messageText ?? text.Get("new_game_preparing"));
        message.Draw += () => bodyDrawn = true;
        var footer = assets.SourceLayout(0xD8913AB2);
        var back = new UiSourceRoundButton { Name="CancelNewGamePreparation",
            Position=footer.Rectangle(1,new(0,904)).Position,InputAdmission=()=>!retired };
        canvas.AddChild(back);
        back.Visible=showCancel;back.Disabled=!showCancel;
        back.Configure(repository, assets, shader, text.SourceLocale,"000_cmn_txt_decision_modoru",()=>cancelled?.Invoke(),UiAudioCue.Cancel);
        RenderingServer.FramePostDraw += FrameDrawn;frameSubscribed=true;
        Resized += Fit; Fit();
    }
    private void FrameDrawn()
    {
        if (!bodyDrawn || retired) return;
        ReleaseFrameSubscription();
        drawn.TrySetResult(true);
    }
    internal void ShowFailure(PreviewText text) => message.Apply(text.Get("new_game_prepare_failed"));
    private void Fit()
    {
        if (canvas is null) return;
        float scale=Math.Min(Size.X/1920,Size.Y/1080);
        canvas.Scale=Vector2.One*scale;canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    internal void Abandon()
    {
        if(retired)return;retired=true;cancelled=null;Hide();
        ReleaseFrameSubscription();drawn.TrySetCanceled();
    }
    private void ReleaseFrameSubscription()
    {
        if(!frameSubscribed)return;frameSubscribed=false;
        RenderingServer.FramePostDraw-=FrameDrawn;
    }
    public override void _ExitTree()
    { Abandon();if(ownsResources){font?.Dispose();assets?.Dispose();shader?.Dispose();} }
}

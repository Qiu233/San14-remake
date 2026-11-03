using Godot;
using San14.Data;

/// <summary>Ordinary EventView narration. A fresh manual advance or the active
/// page's automatic wait consumes a drawn page; the caller owns the transaction.</summary>
internal partial class DeploymentNarrationView : Control
{
    private UiAssets? assets;
    private bool ownsResources=true;
    private MapFont? font;
    private Shader? shader;
    private Control canvas=null!;
    private DeploymentNarrationBody body=null!;
    private UiSourceParagraph? announcement;
    private Action? completed;
    private bool ready,mouseArmed,finished;
    private Key keyArmed;
    private Func<AutomaticMessageAdvanceMode>? readAutomaticAdvance;
    private Func<bool>? readAdmission;
    private string sourceLocale="";
    private int? automaticThreshold;
    private int elapsedUpdates;
    private bool keyboardSkip, keyboardFastForward;
    private bool retainCompletedStory;
    private Control? storyContinueCursor;
    internal Func<InputEvent, bool>? KeyboardInput { get; set; }
    internal Func<InputEventMouseButton, bool>? MouseInputAdmission { get; set; }
    internal Func<int, bool>? MessageModeRequested { get; set; }
    internal Func<bool>? ReadStoryAccelerated { get; set; }
    internal int PageIndex {get;private set;}
    internal bool IsAwaitingAdvance=>ready&&!finished;

    internal void ConfigureAnnouncement(GameResourceRepository repository,PreviewText text,
        string expanded,Action consumed,Func<AutomaticMessageAdvanceMode>? automaticAdvance=null,
        Func<bool>? admitted=null,AdministrationPageChrome.Resources? resources=null)
        =>ConfigureCommonMessage(repository,text,expanded,null,null,consumed,automaticAdvance,admitted,resources);

    internal void ConfigurePersonMessage(GameResourceRepository repository,PreviewText text,
        string speaker,string expanded,LoadedSprite? portrait,Action consumed,
        Func<AutomaticMessageAdvanceMode>? automaticAdvance=null,Func<bool>? admitted=null,
        AdministrationPageChrome.Resources? resources=null)
        =>ConfigureCommonMessage(repository,text,expanded,speaker,portrait,consumed,automaticAdvance,admitted,resources);

    private void ConfigureCommonMessage(GameResourceRepository repository,PreviewText text,
        string expanded,string? speaker,LoadedSprite? portrait,Action consumed,
        Func<AutomaticMessageAdvanceMode>? automaticAdvance,Func<bool>? admitted,
        AdministrationPageChrome.Resources? resources)
    {
        Name="PersonnelAnnouncement";ZIndex=95;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);completed=consumed;
        readAutomaticAdvance=automaticAdvance;readAdmission=admitted;sourceLocale=text.SourceLocale;
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);font=resources?.Font??new(repository,text.SourceLocale);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        canvas=new(){Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        bool person=speaker is not null;
        var source=assets.SourceLayout(person?0x3B311A21u:0xF6332D26u);
        Vector2 mount=new((1920-source.Nodes[0].Value44)/2f,980-source.Nodes[0].Value46);
        var mask=source.Art(canvas,1,source.Rectangle(1,mount),assets,shader,blend:UiSourceSprite.Blend.AlphaMask);
        var blur=source.Rectangle(2,mount);
        source.Blur(mask,2,new(blur.Position-mask.Position,blur.Size));
        source.Art(canvas,3,source.Rectangle(3,mount),assets,shader);
        if(person)
        {
            source.Art(canvas,4,source.Rectangle(4,mount),assets,shader);
            source.Art(canvas,5,source.Rectangle(5,mount),assets,shader);
            if(portrait is not null)
            {
                var faceBox=source.Rectangle(9,mount);
                var face=new UiSourcePortrait{Position=faceBox.Position,Size=faceBox.Size};canvas.AddChild(face);
                face.ConfigureFace2(repository,assets,shader,portrait);
            }
            source.Art(canvas,11,source.Rectangle(11,mount),assets,shader);
            source.Text(canvas,7,mount,speaker!,font);
        }
        int bodyNode=person?6:4;
        var box=source.Rectangle(bodyNode,mount);
        announcement=new(){Name="AnnouncementMessage",Position=box.Position,Size=box.Size};canvas.AddChild(announcement);
        announcement.ConfigureFormattedMessage(font,MapFontProfile.FromNode(source.Nodes[bodyNode],source.Source.Source.ResourceUri));
        announcement.Draw+=()=>{if(!finished)ready=true;};announcement.Apply(expanded);
        var cursor=assets.SourceLayout(0xC09B0A33);
        cursor.Art(canvas,2,cursor.Rectangle(2,source.Rectangle(person?12:5,mount).Position),assets,shader);
        BeginPageWait();SetMeta("source_layout",source.Source.Source.ResourceUri);
        Resized+=Fit;Fit();
    }

    internal void Configure(GameResourceRepository repository,PreviewText text,string speaker,
        string expanded,IReadOnlyList<LoadedSprite> standingLayers,Action consumed,
        Func<AutomaticMessageAdvanceMode>? automaticAdvance=null,Func<bool>? admitted=null,
        AdministrationPageChrome.Resources? resources=null)
        =>ConfigureEvent(repository,text,speaker,expanded,[(2,standingLayers)],null,
            consumed,automaticAdvance,admitted,resources:resources);

    // Story keeps its background and already visible actors in a persistent
    // scene. This child supplies only the same source message/paging owner.
    internal void ConfigureStoryMessage(GameResourceRepository repository, PreviewText text,
        string speaker, string expanded, Action consumed,
        Func<AutomaticMessageAdvanceMode>? automaticAdvance = null, Func<bool>? admitted = null,
        bool singleWait = false, AdministrationPageChrome.Resources? resources = null)
    {
        retainCompletedStory = true;
        ConfigureEventCore(repository, text, speaker, expanded, [], null, consumed, automaticAdvance, admitted, singleWait, resources);
    }

    internal void RetainStoryPage()
    {
        if (!retainCompletedStory) return;
        if (announcement is null && body.PageCount > 0) { PageIndex = body.PageCount - 1; body.ShowPage(PageIndex); }
        Abandon(); Show(); storyContinueCursor?.Hide();
    }

    internal void ConfigureRecruitConversation(GameResourceRepository repository,PreviewText text,string speaker,
        string expanded,IReadOnlyList<LoadedSprite> executorLayers,IReadOnlyList<LoadedSprite> targetLayers,
        bool executorSpeaking,Action consumed,Func<AutomaticMessageAdvanceMode>? automaticAdvance=null,
        Func<bool>? admitted=null,AdministrationPageChrome.Resources? resources=null)
        //83A290's actual slot table maps Talk's slots3/4 to nodes6/5.
        //The side flag selects source animation names, not a guessed X flip.
        =>ConfigureEvent(repository,text,speaker,expanded,[(6,executorLayers),(5,targetLayers)],
            executorSpeaking?3:4,consumed,automaticAdvance,admitted,resources:resources);

    // The real route has already selected three parts or thirty frames.
    // Its application owner is required; the page owns only its controls.
    internal void Configure(GameResourceRepository repository,PreviewText text,string speaker,
        string expanded,LoadedStandingPortraitSources standingLayers,AdministrationPageChrome.Resources resources,
        Action consumed,Func<AutomaticMessageAdvanceMode>? automaticAdvance=null,Func<bool>? admitted=null)
        =>ConfigureEventCore(repository,text,speaker,expanded,
            [(2,portrait=>portrait.Configure(repository,resources.Assets,resources.SpriteShader,standingLayers))],
            null,consumed,automaticAdvance,admitted,resources:resources);

    private void ConfigureEvent(GameResourceRepository repository,PreviewText text,string speaker,
        string expanded,IReadOnlyList<(int Node,IReadOnlyList<LoadedSprite> Layers)> participants,
        int? activeSlot,Action consumed,Func<AutomaticMessageAdvanceMode>? automaticAdvance,Func<bool>? admitted,bool singleWait = false,
        AdministrationPageChrome.Resources? resources=null)
        =>ConfigureEventCore(repository,text,speaker,expanded,
            participants.Where(participant=>participant.Layers.Count>0).Select(participant=>(participant.Node,
                (Action<UiSourceStandingPortrait>)(portrait=>portrait.Configure(repository,assets!,shader!,participant.Layers)))).ToArray(),
            activeSlot,consumed,automaticAdvance,admitted,singleWait,resources);

    private void ConfigureEventCore(GameResourceRepository repository,PreviewText text,string speaker,
        string expanded,IReadOnlyList<(int Node,Action<UiSourceStandingPortrait> Configure)> participants,
        int? activeSlot,Action consumed,Func<AutomaticMessageAdvanceMode>? automaticAdvance,Func<bool>? admitted,
        bool singleWait=false,AdministrationPageChrome.Resources? resources=null)
    {
        Name="DeploymentNarration";ZIndex=10;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);completed=consumed;
        readAutomaticAdvance=automaticAdvance;readAdmission=admitted;sourceLocale=text.SourceLocale;
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);font=resources?.Font??new(repository,text.SourceLocale);shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        canvas=new Control{Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        var root=assets.SourceLayout(0xD8B39FC6);
        var characterParent=assets.SourceLayout(0x86BA499F);
        foreach(var participant in participants)
        {
            var portrait=new UiSourceStandingPortrait{Name="NarrationPortrait"+participant.Node,
                Position=characterParent.Rectangle(participant.Node).Position};
            canvas.AddChild(portrait);participant.Configure(portrait);
            portrait.SetMeta("character_parent_node",participant.Node);
        }
        var message=assets.SourceLayout(0x98D39EC1);
        message.Art(canvas,2,message.Rectangle(2),assets,shader);
        var name=new UiSourceText{Name="NarrationSpeaker",Position=message.Rectangle(3).Position,Size=message.Rectangle(3).Size};
        canvas.AddChild(name);
        name.Configure(font,MapFontProfile.FromNode(message.Nodes[3],message.Source.Source.ResourceUri) with
            {WidthFitMode=2,RawCharacterMode=2});
        name.Text=speaker;
        var rectangle=message.Rectangle(4);
        if(singleWait)
        {
            announcement=new(){Name="NarrationBody",Position=rectangle.Position,Size=rectangle.Size};canvas.AddChild(announcement);
            // Speech-only messages use the same formatted-message setters as
            // ordinary announcements, while retaining their one actual wait.
            announcement.ConfigureFormattedMessage(font,MapFontProfile.FromNode(message.Nodes[4],message.Source.Source.ResourceUri));
            announcement.Draw+=()=>{if(!finished)ready=true;};announcement.Apply(expanded);
        }
        else
        {
            body=new DeploymentNarrationBody{Name="NarrationBody",Position=rectangle.Position,Size=rectangle.Size};canvas.AddChild(body);
            body.PageDrawn+=publication=>{if(!finished&&publication==body.Publication)ready=true;};
            body.Configure(font,MapFontProfile.FromNode(message.Nodes[4],message.Source.Source.ResourceUri),expanded);
        }
        var cursor=assets.SourceLayout(0x0D12D720);
        storyContinueCursor=cursor.Art(canvas,2,cursor.Rectangle(2,message.Rectangle(5).Position),assets,shader);
        BeginPageWait();
        SetMeta("source_layout",root.Source.Source.ResourceUri);
        SetMeta("message_layout",message.Source.Source.ResourceUri);
        SetMeta("portrait_parent_layout",characterParent.Source.Source.ResourceUri);
        if(activeSlot is int slot)SetMeta("active_person_slot",slot);
        Resized+=Fit;Fit();
    }

    public override void _Input(InputEvent input)
    {
        if(!IsActiveWait()){mouseArmed=false;keyArmed=Key.None;return;}
        if (KeyboardInput?.Invoke(input) == true) { GetViewport().SetInputAsHandled(); return; }
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Right}||input is InputEventKey {Keycode:Key.Escape})
        {GetViewport().SetInputAsHandled();return;}
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left} mouse)
        {
            if (MouseInputAdmission?.Invoke(mouse) == false) { mouseArmed = false; return; }
            GetViewport().SetInputAsHandled();
            if(mouse.Pressed){mouseArmed=ready;return;}
            bool advance=mouseArmed&&ready;mouseArmed=false;if(advance)Advance();
        }
        else if(input is InputEventKey key&&key.Keycode is Key.Space or Key.Enter or Key.KpEnter)
        {
            GetViewport().SetInputAsHandled();if(key.Echo)return;
            if(key.Pressed){keyArmed=ready?key.Keycode:Key.None;return;}
            bool advance=ready&&keyArmed==key.Keycode;keyArmed=Key.None;if(advance)Advance();
        }
    }

    public override void _Process(double delta)
    {
        if(!IsActiveWait())
        {mouseArmed=false;keyArmed=Key.None;return;}
        //83D690 latches Skip and toggles Fast. 83D5D0 bypasses only the
        //active input wait; every next page still needs its actual Draw.
        if (ready && (keyboardSkip || keyboardFastForward || ReadStoryAccelerated?.Invoke() == true)) { Advance(); return; }
        // Snapshot the setting once per page. Manual does not even increment;
        // held input increments but delays consumption until a later release.
        if(!ready||automaticThreshold is not int threshold||threshold==int.MaxValue)return;
        elapsedUpdates=unchecked(elapsedUpdates+1);
        if(Input.IsMouseButtonPressed(MouseButton.Left)||Input.IsMouseButtonPressed(MouseButton.Right)||
            Input.IsKeyPressed(Key.Space)||Input.IsKeyPressed(Key.Enter)||Input.IsKeyPressed(Key.KpEnter)||
            Input.IsKeyPressed(Key.Escape))return;
        // These are the host's existing advance/back bindings, not an assumed
        // mapping of the original logical input-mask bit numbers to Windows VKs.
        if(elapsedUpdates>=threshold)Advance();
    }

    private bool IsActiveWait()=>!finished&&IsVisibleInTree()&&CanProcess()&&(readAdmission?.Invoke()??true);

    internal bool RequestKeyboardMessageMode(int action)
    {
        if (!IsActiveWait()) return false;
        if (MessageModeRequested is { } request) return request(action);
        if (action == 23) keyboardSkip = true;
        else if (action == 24) keyboardFastForward = !keyboardFastForward;
        else return false;
        SetMeta("keyboard_message_skip", keyboardSkip);
        SetMeta("keyboard_message_fast_forward", keyboardFastForward);
        return true;
    }

    private void BeginPageWait()
    {
        elapsedUpdates=0;
        automaticThreshold=AutomaticMessageAdvanceSource.ReadThreshold(sourceLocale,
            readAutomaticAdvance?.Invoke()??AutomaticMessageAdvanceMode.Manual);
    }

    private void Advance()
    {
        ready=false;mouseArmed=false;keyArmed=Key.None;
        automaticThreshold=null;
        if(announcement is null&&++PageIndex<body.PageCount){BeginPageWait();body.ShowPage(PageIndex);return;}
        var callback=completed;Abandon();
        if(retainCompletedStory){Show();storyContinueCursor?.Hide();}
        callback?.Invoke();
    }

    internal void Abandon()
    {
        finished=true;ready=false;mouseArmed=false;keyArmed=Key.None;automaticThreshold=null;
        completed=null;readAutomaticAdvance=null;readAdmission=null;KeyboardInput=null;MouseInputAdmission=null;MessageModeRequested=null;ReadStoryAccelerated=null;Hide();
    }

    private void Fit()
    {
        if(canvas is null)return;
        float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;
        canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }

    public override void _ExitTree()
    {
        completed=null;readAutomaticAdvance=null;readAdmission=null;KeyboardInput=null;MouseInputAdmission=null;finished=true;
        if(ownsResources){font?.Dispose();assets?.Dispose();shader?.Dispose();}
    }
}

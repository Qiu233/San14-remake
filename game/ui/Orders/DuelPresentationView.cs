using Godot;
using San14.Core;
using San14.Data;

/// <summary>Displays the registered rounds and result. Advancing a drawn
/// round is a UI action; it never evaluates a new combat or invents an animation receipt.</summary>
internal partial class DuelPresentationView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private DuelPresentationPage page = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private MapFont font = null!;
    private LocalizedUiSpriteSource sprites = null!;
    private UiSourceLayout gauge = null!;
    private Control canvas = null!, content = null!;
    private Func<bool> admitted = null!;
    private Action complete = null!;
    private DeploymentNarrationView? narration;
    private int round, injury;
    private bool drawn, abandoned, result, resultTextDrawn, mouseArmed;
    private Key keyArmed;
    private readonly HashSet<UiSourceFunctionButton> armedFunctionButtons=[];
    internal bool ResultVisible => result;

    internal void Configure(GameResourceRepository repository, PreviewText text, DuelPresentationPage page,
        Action complete, Func<bool> admitted)
    {
        this.repository = repository; this.text = text; this.page = page; this.complete = complete; this.admitted = admitted;
        assets = new(repository); shader = UiSourceSprite.CreateShader(); font = new(repository,text.SourceLocale);
        sprites=new(repository);
        if(page.Display)gauge=new(MapMarkerLayout.LoadNamed(repository,"07_02_04_02_gauge_offset_00"));
        Name = "DuelPresentation"; ZIndex = 105; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas = new() { Name = "DuelSourceCanvas", Size = new(1920,1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas); canvas.Draw += () => drawn = true;
        canvas.GuiInput += HandlePointerInput; GuiInput += HandlePointerInput;
        content = new() { Name = "DuelStage", Size = canvas.Size, MouseFilter = MouseFilterEnum.Ignore }; canvas.AddChild(content);
        if (page.Display) ShowRound(); else ShowNextInjury();
        Resized += Fit; Fit();
    }

    private void ShowRound()
    {
        ClearContent();
        var source = gauge;
        source.Art(content,5,source.Rectangle(5),assets,shader);
        var left = page.Request.Pair.PlayerSide == 0 ? page.Request.Result.Actor : page.Request.Result.Opponent;
        var right = page.Request.Pair.PlayerSide == 0 ? page.Request.Result.Opponent : page.Request.Result.Actor;
        Side(page.Left!,left,0); Side(page.Right!,right,26);
        if (round > 0) source.Art(content,26+round,source.Rectangle(26+round),assets,shader);
        Controls();
        void Side(DuelPresentationPerson person, CampaignDuelParticipant plan, int offset)
        {
            //The native caller supplies current name, strength and up to five
            //duel-category markers to these exact original nodes.
            source.Text(content,10+offset,default,person.Name ?? text.Get("detail_unknown"),font,MapTextPresets.Create()[27]);
            //Native sprite305 is exactly this authored07_base_buryoku node.
            source.Art(content,11+offset,source.Rectangle(11+offset),assets,shader);
            if(person.Strength is int strength) source.Text(content,12+offset,default,
                strength.ToString(System.Globalization.CultureInfo.InvariantCulture),font,MapTextPresets.Create()[55]);
            if(person.Flag is{} flag)
            {
                var view=new UiSourceForceFlag {Position=source.Rectangle(7+offset).Position};content.AddChild(view);
                view.Configure(repository,assets,shader,font,flag);
            }
            if(person.Portrait is{Kind:OfficerPortraitRouteKind.StockNamed,ResourceName:{} pack,SpriteName:{} sprite})
            {
                int node=offset==0?2:4;
                var box=source.Rectangle(node);
                var portrait=new UiSourcePortrait {Position=box.Position,Size=box.Size};content.AddChild(portrait);
                portrait.ConfigureLayoutPane(repository,assets,shader,sprites.LoadTexturePackSprite(pack,sprite),source,node);
            }
            for(int index=0;index<person.Traits.Count;index++)
            {
                int mount=13+offset+index*2;var box=source.Rectangle(mount);
                var marker=new AreaAdministrationTraitMarker {Position=box.Position,MouseFilter=MouseFilterEnum.Ignore};content.AddChild(marker);
                marker.Configure(repository,assets,shader,font,box.Size);
                var trait=person.Traits[index];marker.Apply(trait.SourceIndex,trait.Text,trait.Style);
            }
            source.Art(content,24+offset,source.Rectangle(24+offset),assets,shader);
            int current=page.Request.Result.InstantDefeat
                ? round==0?plan.PreparedInitialReadiness:plan.PreparedFinalReadiness
                :plan.ReadinessTrajectory[round];
            var boxFill=source.Rectangle(26+offset);int width=(int)(boxFill.Size.X*Math.Clamp(current,0,100)/100f);
            if(width>0)
            {
                //The actual source fill is left/right aligned, with its
                //authored UVs retained under a changing clipping rectangle.
                float origin=offset==0?boxFill.Position.X:boxFill.End.X-width;
                var clip=new Control {Position=new(origin,boxFill.Position.Y),Size=new(width,boxFill.Size.Y),ClipContents=true,
                    MouseFilter=MouseFilterEnum.Ignore};content.AddChild(clip);
                source.Art(clip,26+offset,new(new(boxFill.Position.X-origin,0),boxFill.Size),assets,shader,
                    MapSpritePalettes.Create()[offset==0?34:35]);
            }
        }
    }

    private void Controls()
    {
        var source=new UiSourceLayout(repository,0x96541935);
        Button(2,"000_cmn_icon_advance_spd2",Advance);
        Button(4,"000_cmn_icon_advance_skip",Skip);
        void Button(int node,string icon,Action action)
        {
            var button=new UiSourceFunctionButton {Position=source.Rectangle(node).Position};content.AddChild(button);
            button.ButtonDown += () =>
            {armedFunctionButtons.Remove(button);if(CanAcceptInput())armedFunctionButtons.Add(button);};
            button.Configure(repository,assets,shader,icon,()=>
            {bool armed=armedFunctionButtons.Remove(button);if(armed&&CanAcceptInput())action();});
        }
    }

    private void ShowResult()
    {
        result=true;resultTextDrawn=false;ClearContent();
        var root=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"07_02_04_04_result"));
        int mount=page.Request.Result.PlayerOutcome switch {CampaignDuelOutcome.Won=>3,CampaignDuelOutcome.Lost=>2,_=>1};
        string name=mount switch {3=>"07_02_04_04_result2_win_offset_00",2=>"07_02_04_04_result1_lose_offset_00",_=>"07_02_04_04_result3_draw_offset_00"};
        var source=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,name));
        Vector2 shift=root.Rectangle(mount).Position;
        //The ofsrender pane is a live 3D capture in the original. The current
        //host keeps the map behind the genuine static result artwork instead.
        int[] art=mount==3?[3,4]:mount==2?[2,4,5]:[3,4];
        //Native309/307/306 match the win/lose/draw caption nodes' authored
        //sprites, so their original sprite, texture routing and UVs stay intact.
        foreach(int node in art) source.Art(content,node,source.Rectangle(node,shift),assets,shader);
        var serif=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"07_02_04_00_ikkiuchi_offset_00"));
        Vector2 textMount=shift+source.Rectangle(mount==3?9:mount==2?6:5).Position;
        foreach(int node in new[]{2,3,4})serif.Art(content,node,serif.Rectangle(node,textMount),assets,shader);
        var person=page.Request.Result.PlayerOutcome==CampaignDuelOutcome.Lost?page.Right!:page.Left!;
        if(person.Flag is{} flag)
        {
            var view=new UiSourceForceFlag {Position=serif.Rectangle(6,textMount).Position};content.AddChild(view);
            view.Configure(repository,assets,shader,font,flag);
        }
        serif.Text(content,9,textMount,person.Name ?? text.Get("detail_unknown"),font);
        if(person.Portrait is{Kind:OfficerPortraitRouteKind.StockNamed,ResourceName:{} pack,SpriteName:{} sprite})
        {
            var box=serif.Rectangle(11,textMount);
            var face=new UiSourcePortrait {Position=box.Position,Size=box.Size};content.AddChild(face);
            face.ConfigureLayoutPane(repository,assets,shader,sprites.LoadTexturePackSprite(pack,sprite),serif,11);
        }
        var bodyBox=serif.Rectangle(5,textMount);
        var body=new UiSourceParagraph {Position=bodyBox.Position,Size=bodyBox.Size};content.AddChild(body);
        body.ConfigureFormattedMessage(font,MapFontProfile.FromNode(serif.Nodes[5],serif.Source.Source.ResourceUri));
        body.Apply(page.ResultText);body.Draw+=()=>resultTextDrawn=true;
    }

    private void ShowNextInjury()
    {
        if(injury>=page.InjuryTexts.Count){if(page.Display||drawn)Finish();else Callable.From(CompleteSuppressed).CallDeferred();return;}
        var view=new DeploymentNarrationView();narration=view;canvas.AddChild(view);
        view.ConfigureAnnouncement(repository,text,page.InjuryTexts[injury++],()=>{RetireNarration();ShowNextInjury();},admitted:ReadyForNarration);
    }
    //378A20's known skip branch needs no visual completion. Defer until the
    //view has been attached so the normal exact-owner callback can adopt it.
    private void CompleteSuppressed(){if(!abandoned)Finish();}
    private bool ReadyForNarration()=>!abandoned&&IsVisibleInTree()&&admitted();
    private bool CanAcceptInput()=>drawn&&(!result||resultTextDrawn)&&narration is null&&ReadyForNarration();
    private void Advance()
    {
        if(!CanAcceptInput())return;
        if(result){ShowNextInjury();return;}
        if(round>0&&(page.Request.Result.InstantDefeat||FinishedRound(round))||round==5){ShowResult();return;}
        round++;ShowRound();
    }
    private bool FinishedRound(int at)=>page.Request.Result.Actor.ReadinessTrajectory[at]==0||page.Request.Result.Opponent.ReadinessTrajectory[at]==0;
    private void Skip(){if(CanAcceptInput()){if(result)ShowNextInjury();else ShowResult();}}
    private void Finish(){if(abandoned)return;abandoned=true;complete();}
    internal void Abandon(){abandoned=true;RetireNarration();}
    private void RetireNarration(){if(narration is not{} old)return;narration=null;old.Abandon();old.GetParent()?.RemoveChild(old);old.QueueFree();}
    private void ClearContent(){drawn=false;ClearInputArms();foreach(Node child in content.GetChildren()){content.RemoveChild(child);child.QueueFree();}canvas.QueueRedraw();}
    private void ClearInputArms(){mouseArmed=false;keyArmed=Key.None;armedFunctionButtons.Clear();}
    public override void _Process(double delta){if(!CanAcceptInput())ClearInputArms();}
    private void HandlePointerInput(InputEvent input)
    {
        if(!CanAcceptInput()){ClearInputArms();return;}
        if(input is not InputEventMouseButton{ButtonIndex:MouseButton.Left} mouse)return;
        //Background GUI input is delivered after the function buttons have
        //handled their own event. A button click cannot also advance a round.
        if(mouse.Pressed)mouseArmed=true;
        else{bool armed=mouseArmed;mouseArmed=false;if(armed)Advance();}
        AcceptEvent();
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(!CanAcceptInput()){ClearInputArms();return;}
        if(input is InputEventKey key&&key.Keycode is Key.Space or Key.Enter or Key.KpEnter or Key.Escape)
        {
            if(key.Echo)return;
            if(key.Pressed)keyArmed=key.Keycode;
            else if(keyArmed==key.Keycode){keyArmed=Key.None;if(key.Keycode==Key.Escape)Skip();else Advance();}
            GetViewport().SetInputAsHandled();
        }
    }
    private void Fit(){if(canvas is null)return;float scale=Math.Min(Size.X/1920f,Size.Y/1080f);canvas.Scale=new(scale,scale);canvas.Position=(Size-canvas.Size*scale)*.5f;}
    public override void _ExitTree()
    {
        abandoned=true;ClearInputArms();narration?.Abandon();
        complete=null!;admitted=null!;
        if(GodotObject.IsInstanceValid(canvas))canvas.GuiInput-=HandlePointerInput;
        GuiInput-=HandlePointerInput;
        font?.Dispose();assets?.Dispose();shader?.Dispose();
    }
}

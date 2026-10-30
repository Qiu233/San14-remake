using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The actual three tactic slots of a commander card, followed by
/// the map target state. A target release submits; Back never queues a use.</summary>
internal partial class ManualTacticCommandView : Control
{
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private Control canvas=null!,cards=null!;
    private UiSourceHeader header=null!;
    private UiSourceText hint=null!;
    private Action<int>? casterChosen;
    private Action<int>? tacticChosen;
    private Action? returned;
    private bool abandoned;
    private string chooseTarget="";
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;

    internal void Configure(GameResourceRepository repository,PreviewText text,UiSourceHeaderStack headers,
        Action<int> casterChosen,Action<int> tacticChosen,Action returned)
    {
        this.repository=repository;this.text=text;this.casterChosen=casterChosen;
        this.tacticChosen=tacticChosen;this.returned=returned;
        Name="ManualTacticCommand";ZIndex=5;MouseFilter=MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new(){Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        header=new(){Name="ManualTacticTitle"};canvas.AddChild(header);
        header.Configure(repository,assets,shader,font,text.Get("play_tactic_active_title"));header.Publish(headers);
        cards=new(){Name="CommanderCards",MouseFilter=MouseFilterEnum.Ignore};canvas.AddChild(cards);
        var footer=new UiSourceLayout(repository,0xD8913AB2);Vector2 mount=new(0,904);
        var back=new UiSourceRoundButton{Name="ReturnManualTactic",Position=footer.Rectangle(1,mount).Position,
            ActionMode=BaseButton.ActionModeEnum.Release};canvas.AddChild(back);
        back.Configure(repository,assets,shader,text.SourceLocale,"000_cmn_txt_decision_modoru",
            ()=>{if(!abandoned)this.returned?.Invoke();},UiAudioCue.Cancel);
        var hintSource=new UiSourceLayout(repository,0xAB6ADAA7);
        Vector2 hintMount=footer.Rectangle(19,mount).Position;
        foreach(var node in hintSource.Nodes.Where(node=>node.Type is 1 or 3))
            hintSource.Art(canvas,node.Index,hintSource.Rectangle(node.Index,hintMount),assets,shader);
        hint=hintSource.Text(canvas,hintSource.Nodes.Single(node=>node.Type==2).Index,hintMount,"",font);
        //88C270's target state uses the original C965 message.
        chooseTarget=new AdministrationMessages(repository,text.SourceLocale).Format(0xC965);
        Resized+=Fit;Fit();
    }

    internal void Apply(ManualTacticCommandPublication publication,int? selectedTacticSlot,string? notice=null)
    {
        foreach(Node child in cards.GetChildren()){cards.RemoveChild(child);child.QueueFree();}
        var row=new UiSourceLayout(repository,0x1CDA8363);
        var body=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"00_10_01_saihai_offset_20"));
        int[] cardMounts=[6,9,10,11,12];
        Vector2 extent=row.Rectangle(0).Size;
        Vector2 mount=new(1920-extent.X-20,1080-extent.Y-24);
        for(int i=0;i<publication.Casters.Count&&i<cardMounts.Length;i++)
        {
            var caster=publication.Casters[i];Vector2 position=row.Rectangle(cardMounts[i],mount).Position;
            var card=new SourceButton{Name="Commander"+i,Position=position};cards.AddChild(card);
            card.Configure(body,0,0,2,4,caster.Name,assets,shader,font,
                ()=>{if(!abandoned)casterChosen?.Invoke(caster.UnitId);});
            card.Disabled=caster.UnitId<=0||caster.Enabled!=true;
            card.TooltipText=caster.UnresolvedInput??"";
            card.SetMeta("officer_id",caster.OfficerId);card.SetMeta("unit_id",caster.UnitId);
            if(publication.SelectedUnitId!=caster.UnitId)continue;
            //883010 creates dynamic-type5 children at exactly45/51/57.
            foreach(var choice in publication.Tactics)
            {
                if(choice.Slot is <0 or >2)continue;
                int root=45+choice.Slot*6;
                var tactic=new SourceButton{Name="ManualTacticSlot"+choice.Slot,
                    Position=position+body.Rectangle(root).Position};cards.AddChild(tactic);
                tactic.Configure(body,root,root+1,root+3,root+5,
                    choice.Name,
                    assets,shader,font,()=>{if(!abandoned)tacticChosen?.Invoke(choice.Slot);});
                tactic.Disabled=choice.Enabled!=true||choice.TargetHexIds is null||choice.TargetHexIds.Count==0;
                tactic.Visible=choice.TacticId!=0||choice.Enabled is null;
                tactic.Selected=selectedTacticSlot==choice.Slot;tactic.TooltipText=choice.UnresolvedInput??"";
                tactic.SetMeta("tactic_id",choice.TacticId);tactic.SetMeta("source_slot",choice.Slot);
            }
        }
        var selected=publication.Tactics.FirstOrDefault(choice=>choice.Slot==selectedTacticSlot);
        header.ApplyCaption(selected?.Name??text.Get("play_tactic_active_title"));
        hint.Text=notice??(selected is not null?chooseTarget:
            publication.UnresolvedInput is not null?text.Get("detail_unknown"):text.Get("play_tactic_choice"));
        SetMeta("selected_unit_id",publication.SelectedUnitId??-1);
        SetMeta("selected_tactic_id",selected?.TacticId??-1);
        SetMeta("candidate_count",selected?.TargetHexIds?.Count??-1);
    }

    internal void ApplyNotice(string notice){if(!abandoned)hint.Text=notice;}
    internal void Abandon(){abandoned=true;casterChosen=null;tacticChosen=null;returned=null;Hide();}
    private void Fit()
    {
        if(canvas is null)return;
        float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;
        canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    public override void _ExitTree(){font?.Dispose();assets?.Dispose();shader?.Dispose();}

    private partial class SourceButton : UiSourceButton
    {
        private Rect2 hit;
        public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);
        internal void Configure(UiSourceLayout source,int root,int hitNode,int artNode,int textNode,string caption,
            UiAssets assets,Shader shader,MapFont font,Action released)
        {
            Rect2 rectangle=source.Rectangle(root);Size=rectangle.Size;
            Vector2 local=-rectangle.Position;hit=source.Rectangle(hitNode,local);
            ActionMode=ActionModeEnum.Release;
            var states=new Control[5];
            for(int i=0;i<states.Length;i++)
            {
                var state=new Control{MouseFilter=MouseFilterEnum.Ignore};AddChild(state);states[i]=state;
                source.Art(state,artNode,source.Rectangle(artNode,local),assets,shader);
            }
            source.Text(this,textNode,local,caption,font);
            ConfigureStates(states);Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)released();};
            SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_root",root);
        }
    }
}

using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

internal partial class DiplomacyNegotiationView
{
    private bool additionalArrival;
    private bool additionalSpy;
    private UiSourceText? requestedConditionText;

    // These are the actual mounted controls, also used by the ordinary host's
    // input workspace. There is no second copy of their values in this view.
    internal IReadOnlyList<UiSourceRadioButton> ConditionButtons => conditionButtons;
    internal UiSourceNumberButton? MilitaryIntelligenceButton => intelligence;
    internal UiSourceSlider? MilitaryIntelligenceSlider => intelligenceSlider;
    internal UiSourceText? CurrentConditionText => conditionText;
    internal UiSourceText? RequestedConditionText => requestedConditionText;

    private void ConfigureAdditionalReply(GameResourceRepository repository, GameState candidate,
        DiplomacyNegotiationRequest request, int playerForceId, DiplomacyNegotiationPage page,
        PreviewText text, Func<bool> admitted, Action<bool> submit, Action showForce, Action showMap,
        DiplomacyPeopleVisuals? people)
    {
        this.admitted=admitted;this.submit=submit;canAccept=page.CanAccept;
        unknown=text.Get("detail_unknown");additionalArrival=true;
        bool prisoner=request.Kind==DiplomaticMissionKind.ReturnCaptives;
        bool exchange=request.Kind==DiplomaticMissionKind.Exchange;
        bool decide=prisoner&&request.ForceId==playerForceId;
        var (file,mount,title)=request.Kind switch
        {
            DiplomaticMissionKind.ReturnCaptives when decide => (0x279FE149u,new Vector2(460,374),0x0635),
            DiplomaticMissionKind.ReturnCaptives => (0x3EF64B01u,new Vector2(630,338),0x05C3),
            DiplomaticMissionKind.DemandSurrender => (0x5D428181u,new Vector2(535,453),0x0634),
            DiplomaticMissionKind.Exchange => (0x7D365450u,new Vector2(535,379),0x070B),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
        int forceButton=prisoner&&!decide?13:exchange||decide?21:20;
        int mapButton=prisoner&&!decide?19:exchange||decide?27:26;
        int relation=prisoner&&!decide?9:exchange||decide?17:16;
        var replaced=new HashSet<int>{forceButton,mapButton,relation};
        if(!prisoner||decide){replaced.Add(8);replaced.Add(11);}
        if(prisoner)
            foreach(int node in decide?new[]{40,44,51,55,56,60}:new[]{33,37,45,49,57,61})replaced.Add(node);
        // A readonly Exchange entry cannot activate the condition editor. Its
        // editable entry is ConfigureTerms below, with the same real layout.
        if(exchange)foreach(int node in new[]{48,52,56,66,60})replaced.Add(node);
        var source=ConfigureArrivalFrame(repository,text,file,mount,title,replaced);
        Label(5,decide?0x0632:0x0631);
        Value(prisoner&&!decide?7:14,candidate.Forces[page.DisplayedForceId].Name);
        Label(prisoner&&!decide?8:exchange||decide?16:15,0x0413);
        ArrivalRelation(repository,source,relation,mount,page.RelationLevel);
        if(!prisoner||decide)
            ArrivalRuler(repository,source,mount,page,people);
        var offer=request.Negotiation?.Proposal??request.InitialOffer;
        if(prisoner)
        {
            Label(decide?37:30,0x0633);Label(decide?65:72,0x03CE);
            conditionText=Value(decide?67:74,ArrivalOffer(offer,page.NamedCondition));
            ArrivalCaptives(repository,source,mount,people,decide);
        }
        else if(exchange)
        {
            Label(37,0x0710);Label(45,0x03CE);
            requestedConditionText=Value(39,ArrivalOffer(request.RequestedOffer,page.RequestedNamedCondition));
            conditionText=Value(47,ArrivalOffer(offer,page.NamedCondition));
        }
        ArrivalCommand(repository,source,mount,forceButton,0x05A2,showForce);
        ArrivalCommand(repository,source,mount,mapButton,0x05AD,showMap);
        ConfigureArrivalFooter(repository,text,twoReplies:true);
        CompleteArrival(source,request);

        UiSourceText Value(int node,string value)=>source.Text(canvas,node,mount,value,font!);
        void Label(int node,int caption)=>Value(node,termsMessage!(1,caption));
    }

    private void ConfigureAdditionalTerms(GameResourceRepository repository,GameState candidate,
        DiplomacyNegotiationDraft draft,int playerForceId,IReadOnlyDictionary<int,DetailForceAppearance?> appearances,
        PreviewText text,Func<bool> admitted,Action<bool> submit,Action showForce,Action showMap,
        Action<DiplomacyNegotiationConditionKind> chooseCondition,Action clear,
        Action calculateIntelligence,Action<int> setIntelligence,DiplomacyPeopleVisuals? people)
    {
        if(draft.Kind==DiplomaticMissionKind.DemandSurrender||
            draft.Kind==DiplomaticMissionKind.ReturnCaptives&&draft.Stage==DiplomaticNegotiationStage.RecipientDecision)
        {
            ConfigureAdditionalReply(repository,candidate,draft.Request,playerForceId,draft.Page,text,admitted,
                submit,showForce,showMap,people);return;
        }
        terms=draft;this.admitted=admitted;this.submit=submit;additionalArrival=true;
        additionalSpy=draft.Kind==DiplomaticMissionKind.ReturnCaptives;
        unknown=text.Get("detail_unknown");playerSender=playerForceId==draft.Request.ForceId;
        uint file=additionalSpy?0xDD9D64ABu:0x7D365450u;
        Vector2 mount=additionalSpy?new(630,88):new(535,379);
        int[] radioNodes=additionalSpy?[77,89,96,103]:[50,54,58,68];
        int[] radioRoots=additionalSpy?[75,87,94,101]:[48,52,56,66];
        var replaced=new HashSet<int>(radioRoots){additionalSpy?13:21,additionalSpy?19:27,
            additionalSpy?9:17,additionalSpy?106:60};
        if(additionalSpy)
            foreach(int node in new[]{33,37,45,49,57,61,84,86,91,98,105,112,114,122,128,131,135,139,141})replaced.Add(node);
        else{replaced.Add(8);replaced.Add(11);}
        var source=ConfigureArrivalFrame(repository,text,file,mount,additionalSpy?0x0851:0x070B,replaced);
        Label(5,additionalSpy&&playerSender?0x0632:0x0631);
        Value(additionalSpy?7:14,candidate.Forces[draft.Page.DisplayedForceId].Name);
        Label(additionalSpy?8:16,0x0413);
        ArrivalRelation(repository,source,additionalSpy?9:17,mount,draft.Page.RelationLevel);
        if(additionalSpy)
        {
            Label(30,0x0633);Label(72,0x03CE);conditionText=Value(74,"");
            ArrivalCaptives(repository,source,mount,people,false);
        }
        else
        {
            ArrivalRuler(repository,source,mount,draft.Page,people,
                appearances.GetValueOrDefault(draft.Page.DisplayedForceId));
            Label(37,0x0710);Label(45,0x03CE);
            requestedConditionText=Value(39,ArrivalOffer(draft.Request.RequestedOffer,draft.Page.RequestedNamedCondition));
            conditionText=Value(47,"");
        }
        ArrivalCommand(repository,source,mount,additionalSpy?13:21,0x05A2,showForce);
        ArrivalCommand(repository,source,mount,additionalSpy?19:27,0x05AD,showMap);
        for(int i=0;i<radioNodes.Length;i++)
        {
            var kind=(DiplomacyNegotiationConditionKind)i;var box=source.Rectangle(radioNodes[i],mount);
            var radio=new UiSourceRadioButton{Name="NegotiationCondition"+kind,Position=box.Position,
                Size=box.Size,InputAdmission=ReadAdmission};canvas.AddChild(radio);conditionButtons.Add(radio);
            radio.Configure(repository,assets!,shader!,font!,i==0?UiSourceRadioSegment.Left:
                i==3?UiSourceRadioSegment.Right:UiSourceRadioSegment.Centre,
                termsMessage!(1,new[]{0x020C,0x020D,0x0246,0x0202}[i]),()=>{if(ReadAdmission())chooseCondition(kind);});
        }
        clearCondition=ArrivalCommand(repository,source,mount,additionalSpy?106:60,0x029F,clear);
        if(additionalSpy)
        {
            Label(121,0x0835);Label(82,0x0847);
            var amountBox=source.Rectangle(122,mount);
            intelligence=new(){Name="NegotiationMilitaryIntelligence",Position=amountBox.Position,
                Size=amountBox.Size,InputAdmission=ReadAdmission};canvas.AddChild(intelligence);
            intelligence.Configure(repository,assets!,shader!,font!,0,()=>{if(ReadAdmission())calculateIntelligence();});
            intelligenceMax=Value(128,"");
            //139 is CEditSlider.135 is the separate clickable required-cost
            //marker installed by8100F0; it is not the slider itself.
            var sliderBox=source.Rectangle(139,mount);
            intelligenceSlider=new(){Name="NegotiationIntelligenceSlider",Position=sliderBox.Position,
                Size=sliderBox.Size};canvas.AddChild(intelligenceSlider);
            intelligenceSlider.Configure(repository,assets!,shader!,draft.MilitaryIntelligenceMinimum??0,
                draft.MilitaryIntelligenceMaximum??0,1,draft.MilitaryIntelligenceValue??0,false,
                amount=>{if(ReadAdmission())setIntelligence(amount);});
            var marker=source.Rectangle(135,mount);requiredOrigin=marker.Position+new Vector2(0,3);
            intelligenceRequired=new(){Name="NegotiationRequiredIntelligence",Position=requiredOrigin,Size=marker.Size,
                IgnoreTextureSize=true,FocusMode=FocusModeEnum.None};canvas.AddChild(intelligenceRequired);
            intelligenceRequired.Pressed+=()=>{if(ReadAdmission()&&draft.RequiredMilitaryIntelligence is int required)setIntelligence(required);};
            source.Art(intelligenceRequired,136,Relative(136,marker),assets!,shader!);
            requiredText=source.Text(intelligenceRequired,137,default,"",font!,rectangle:Relative(137,marker));
            var gaugeBox=source.Rectangle(114,mount);
            probabilityGauge=new(){Name="NegotiationProbabilityGauge",Position=gaugeBox.Position,
                Size=gaugeBox.Size};canvas.AddChild(probabilityGauge);probabilityGauge.Configure(repository,assets!,shader!);
            if(appearances.GetValueOrDefault(draft.Request.ForceId) is {} sender&&
                appearances.GetValueOrDefault(draft.Request.TargetForceId) is {} recipient)
                probabilityGauge.ApplyForceColors(sender.Fill,recipient.Fill);
            senderRate=Value(84,"");recipientRate=Value(86,"");
            Flag(draft.Request.ForceId,98);Flag(draft.Request.TargetForceId,91);
            endpointFill=appearances.GetValueOrDefault(playerForceId) is {IsValid:true} own?own.Fill:null;
        }
        ConfigureArrivalFooter(repository,text,twoReplies:!additionalSpy);
        CompleteArrival(source,draft.Request);ApplyTerms();

        UiSourceText Value(int node,string value)=>source.Text(canvas,node,mount,value,font!);
        void Label(int node,int caption)=>Value(node,termsMessage!(1,caption));
        Rect2 Relative(int node,Rect2 parent)
        {var box=source.Rectangle(node,mount);box.Position-=parent.Position;return box;}
        void Flag(int force,int node)
        {
            if(appearances.GetValueOrDefault(force) is not {} appearance)return;
            var flag=new UiSourceForceFlag{Position=source.Rectangle(node,mount).Position};canvas.AddChild(flag);
            flag.Configure(repository,assets!,shader!,font!,appearance);
        }
    }

    private UiSourceLayout ConfigureArrivalFrame(GameResourceRepository repository,PreviewText text,
        uint file,Vector2 mount,int title,HashSet<int> replaced)
    {
        Name="DiplomacyArrival";ZIndex=90;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new(){Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        var data=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages;
        termsMessage=(section,id)=>data.Sections[section].Records[id].Text??
            throw new InvalidDataException($"Missing diplomacy arrival text {section}:{id:X4}.");
        var source=new UiSourceLayout(repository,file);
        foreach(var node in source.Nodes)
        {
            bool hidden=false;
            for(int? cursor=node.Index;cursor is int at;cursor=source.Nodes[at].ParentIndex)
                if(replaced.Contains(at)){hidden=true;break;}
            if(hidden)continue;
            if(node.Type is 1 or 3)source.Art(canvas,node.Index,source.Rectangle(node.Index,mount),assets,shader);
            else if(node.Type==6)source.Blur(canvas,node.Index,source.Rectangle(node.Index,mount));
        }
        header=new(){Name="DiplomacyArrivalTitle"};canvas.AddChild(header);
        header.Configure(repository,assets,shader,font,termsMessage(1,title));
        return source;
    }

    private UiSourceCommandButton ArrivalCommand(GameResourceRepository repository,UiSourceLayout source,
        Vector2 mount,int node,int caption,Action action)
    {
        var box=source.Rectangle(node,mount);
        var button=new UiSourceCommandButton{Name="ArrivalCommand"+node,Position=box.Position,
            Size=box.Size,InputAdmission=ReadAdmission};canvas.AddChild(button);
        button.Configure(repository,assets!,shader!,font!,termsMessage!(1,caption),()=>{if(ReadAdmission())action();});
        details.Add(button);return button;
    }

    private void ArrivalRelation(GameResourceRepository repository,UiSourceLayout parent,int node,
        Vector2 mount,int level)
    {
        var source=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"cmn_mrker"));
        var marker=new Control{Name="ArrivalRelation",Position=parent.Rectangle(node,mount).Position,
            Size=source.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};canvas.AddChild(marker);
        int[] styles=[0x221,0x221,0x222,0x21F,0x21C,0x21C];
        if((uint)level>=(uint)styles.Length)throw new InvalidDataException("Invalid diplomacy relationship level.");
        string sprite=ArmyMapUiSources.Create().IconName(new ArmyMapIconSelection(styles[level],0));
        source.Art(marker,2,source.Rectangle(2),assets!,shader!,commonSpriteOverride:sprite);
        source.Text(marker,3,default,termsMessage!(0,0x0141+level),font!);
        marker.SetMeta("relation_level",level);
    }

    private void ArrivalRuler(GameResourceRepository repository,UiSourceLayout source,Vector2 mount,
        DiplomacyNegotiationPage page,DiplomacyPeopleVisuals? people,DetailForceAppearance? appearance=null)
    {
        var ruler=people?.Ruler;
        if(ruler?.Portrait is {} image)
        {
            var box=source.Rectangle(8,mount);var face=new UiSourcePortrait{Name="ArrivalRuler",
                Position=box.Position,Size=box.Size};canvas.AddChild(face);face.ConfigureFace2(repository,assets!,shader!,image);
        }
        else if(people is null&&page.Ruler is {Available:true,Value:{} card})
        {
            var values=new AreaAdministrationOfficerContent(repository).Create(card);
            if(values.Portrait is {} fallback)
            {
                var box=source.Rectangle(8,mount);var face=new UiSourcePortrait{Name="ArrivalRuler",
                    Position=box.Position,Size=box.Size};canvas.AddChild(face);face.ConfigureFace2(repository,assets!,shader!,fallback);
            }
            appearance??=values.Force;
        }
        appearance??=people?.DisplayedForceAppearance;
        if(appearance is not null)
        {
            var flag=new UiSourceForceFlag{Name="ArrivalForceFlag",Position=source.Rectangle(11,mount).Position};
            canvas.AddChild(flag);flag.Configure(repository,assets!,shader!,font!,appearance);
        }
        if(ruler?.UnresolvedInput is {} missing)SetMeta("ruler_unknown",missing);
        else if(people?.RulerUnresolvedInput is {} unresolved)SetMeta("ruler_unknown",unresolved);
    }

    private void ArrivalCaptives(GameResourceRepository repository,UiSourceLayout source,Vector2 mount,
        DiplomacyPeopleVisuals? people,bool decide)
    {
        int[] portraits=decide?[40,51,56]:[33,45,57];
        int[] names=decide?[43,54,59]:[36,48,60];
        int[] traits=decide?[44,55,60]:[37,49,61];
        for(int i=0;i<3;i++)
        {
            var person=people?.Captives.ElementAtOrDefault(i);
            string name=person?.Name??(people?.CaptivesUnresolvedInput is not null?unknown:"");
            source.Text(canvas,names[i],mount,name,font!);
            if(person?.Portrait is {} image)
            {
                var box=source.Rectangle(portraits[i],mount);var face=new UiSourcePortrait{Name="ArrivalCaptive"+i,
                    Position=box.Position,Size=box.Size};canvas.AddChild(face);face.ConfigureFace2(repository,assets!,shader!,image);
            }
            if(person?.Trait is {} trait)
            {
                var box=source.Rectangle(traits[i],mount);
                var marker=new AreaAdministrationTraitMarker{Name="ArrivalCaptiveTrait"+i,Position=box.Position};
                canvas.AddChild(marker);marker.Configure(repository,assets!,shader!,font!,size:box.Size);
                marker.Apply(trait.PhysicalTraitId,trait.Name,decide?(byte)0:trait.Style);
            }
            else if(person?.TraitUnresolvedInput is {} missing)
            {
                var box=source.Rectangle(traits[i],mount);
                var marker=new AreaAdministrationTraitMarker{Name="ArrivalCaptiveTrait"+i,Position=box.Position};
                canvas.AddChild(marker);marker.Configure(repository,assets!,shader!,font!,size:box.Size);marker.ApplyUnknown(unknown);
                marker.SetMeta("source_unknown",missing);
            }
        }
        if(people?.CaptivesUnresolvedInput is {} unresolved)SetMeta("captives_unknown",unresolved);
    }

    private void ConfigureArrivalFooter(GameResourceRepository repository,PreviewText text,bool twoReplies)
    {
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        if(twoReplies){decline=Reply(4,false);accept=Reply(5,true);}
        else
        {
            accept=new(){Name="NegotiationAccept",Position=footer.Rectangle(1,new(0,904)).Position,
                InputAdmission=ReadAdmission,ActionMode=BaseButton.ActionModeEnum.Release};canvas.AddChild(accept);
            accept.Configure(repository,assets!,shader!,text.SourceLocale,"000_cmn_txt_decision_kettei",
                ()=>{if(ReadAdmission()&&canAccept)submit?.Invoke(true);});
        }
        var hint=new UiSourceLayout(repository,0xAB6ADAA7);var hintMount=footer.Rectangle(19,new(0,904)).Position;
        foreach(var node in hint.Nodes.Where(node=>node.Type is 1 or 3))
            hint.Art(canvas,node.Index,hint.Rectangle(node.Index,hintMount),assets!,shader!);
        notice=hint.Text(canvas,hint.Nodes.Single(node=>node.Type==2).Index,hintMount,
            twoReplies?"":new AdministrationMessages(repository,text.SourceLocale).Format(0x34A1),font!);

        UiSourceRoundButton Reply(int node,bool accepted)
        {
            var button=new UiSourceRoundButton{Name=accepted?"NegotiationAccept":"NegotiationDecline",
                Position=footer.Rectangle(node,new(0,904)).Position,InputAdmission=ReadAdmission,
                ActionMode=BaseButton.ActionModeEnum.Release};canvas.AddChild(button);
            button.ConfigureDiplomaticReply(repository,assets!,shader!,text.SourceLocale,accepted,
                ()=>{if(ReadAdmission()&&(!accepted||canAccept))submit?.Invoke(accepted);});return button;
        }
    }

    private void CompleteArrival(UiSourceLayout source,DiplomacyNegotiationRequest request)
    {
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("proposal_task_id",request.TaskId);
        if(request.Negotiation is {} stage)SetMeta("negotiation_stage",(int)stage.Stage);
        SetBusy(false);Resized+=Fit;Fit();
    }

    private string ArrivalOffer(DiplomacyOffer offer,string? named)
    {
        if(offer.ItemId is >0)return named??unknown;
        if(offer.Gold!=0)return termsMessage!(1,0x05F8).Replace("%d",offer.Gold.ToString(CultureInfo.InvariantCulture));
        if(offer.Supplies!=0)return termsMessage!(1,0x059D).Replace("%d",offer.Supplies.ToString(CultureInfo.InvariantCulture));
        return offer.AreaId is >0?named??unknown:"";
    }

    private void ApplyAdditionalTerms()
    {
        if(terms is not {} draft)return;
        conditionText!.Text=ArrivalOffer(draft.Offer,draft.Page.NamedCondition);
        if(requestedConditionText is not null)
            requestedConditionText.Text=ArrivalOffer(draft.Request.RequestedOffer,draft.Page.RequestedNamedCondition);
        if(additionalSpy)
        {
            string Number(int? value)=>value?.ToString(CultureInfo.InvariantCulture)??unknown;
            intelligence!.Caption=Number(draft.MilitaryIntelligenceValue);intelligenceMax!.Text=Number(draft.MilitaryIntelligenceMaximum);
            requiredText!.Text=Number(draft.RequiredMilitaryIntelligence);
            int? probability=draft.Probability is int raw?Math.Clamp(raw,0,100):null;
            //Spy_Prisoner v59 uses Data1/085E for both84 and86, as does
            //the ordinary Spy percentage display.
            string Rate(int value)=>termsMessage!(1,0x085E)
                .Replace("%d",value.ToString(CultureInfo.InvariantCulture)).Replace("%%","%",StringComparison.Ordinal);
            senderRate!.Text=probability is int rate?Rate(rate):unknown;
            recipientRate!.Text=probability is int rate2?Rate(100-rate2):unknown;
            bool endpoint=draft.Probability is int p&&(playerSender?p==100:p==0);
            probabilityGauge!.Apply(draft.Probability,endpoint,endpointFill);
        }
        canAccept=draft.Page.CanAccept;SetBusy(busy);
    }

    private void SetAdditionalTermsBusy()
    {
        if(terms is not {} draft)return;
        bool unavailable=abandoned||busy;
        bool?[] allowed=[draft.CanChooseGold,draft.CanChooseSupplies,draft.CanChooseItem,draft.CanChooseArea];
        for(int i=0;i<conditionButtons.Count;i++)
        {conditionButtons[i].Disabled=unavailable||allowed[i]!=true;conditionButtons[i].Selected=(int)draft.ConditionKind==i;}
        if(clearCondition is not null)clearCondition.Disabled=unavailable||!draft.CanEditConditions;
        if(!additionalSpy)return;
        bool military=!unavailable&&draft.CanChooseMilitaryIntelligence==true;
        intelligence!.Disabled=!military||draft.MilitaryIntelligenceCalculatorMaximum is not int ceiling||ceiling<(draft.MilitaryIntelligenceMinimum??0);
        intelligenceSlider!.SetValue(draft.MilitaryIntelligenceValue??0,draft.MilitaryIntelligenceMaximum??0,military);
        intelligenceRequired!.Disabled=!military||draft.RequiredMilitaryIntelligence is not int amount||
            amount<(draft.MilitaryIntelligenceMinimum??0)||amount>(draft.MilitaryIntelligenceMaximum??0);
        intelligenceRequired.Visible=draft.RequiredMilitaryIntelligence is >0&&draft.RequiredMilitaryIntelligence<=draft.MilitaryIntelligenceMaximum;
        if(draft.RequiredMilitaryIntelligence is int position)
            intelligenceRequired.Position=new(intelligenceSlider.Position.X-15+intelligenceSlider.PositionForValue(position)+2,requiredOrigin.Y);
    }
}

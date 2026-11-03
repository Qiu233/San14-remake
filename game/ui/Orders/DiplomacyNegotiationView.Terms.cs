using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

internal partial class DiplomacyNegotiationView
{
    private DiplomacyNegotiationDraft? terms;
    private readonly List<UiSourceRadioButton> conditionButtons = [];
    private UiSourceCommandButton? clearCondition;
    private UiSourceNumberButton? intelligence;
    private UiSourceSlider? intelligenceSlider;
    private UiSourceHitButton? intelligenceRequired;
    private UiSourceText? conditionText, intelligenceMax, requiredText, senderRate, recipientRate;
    private UiSourceDiplomacyGauge? probabilityGauge;
    private Func<int,int,string>? termsMessage;
    private string unknown = "";
    private AreaMarkerColor? endpointFill;
    private bool playerSender;
    private Vector2 requiredOrigin;

    internal void ConfigureTerms(GameResourceRepository repository, GameState candidate,
        DiplomacyNegotiationDraft draft, int playerForceId,
        IReadOnlyDictionary<int,DetailForceAppearance?> appearances, PreviewText text,
        Func<bool> admitted, Action<bool> submit, Action showForce, Action showMap,
        Action<DiplomacyNegotiationConditionKind> chooseCondition, Action clear,
        Action calculateIntelligence, Action<int> setIntelligence,
        DiplomacyPeopleVisuals? people = null, AdministrationPageChrome.Resources? resources = null)
    {
        BorrowResources(resources);
        if (draft.Kind is DiplomaticMissionKind.ReturnCaptives or DiplomaticMissionKind.DemandSurrender or DiplomaticMissionKind.Exchange)
        {
            ConfigureAdditionalTerms(repository,candidate,draft,playerForceId,appearances,text,admitted,submit,
                showForce,showMap,chooseCondition,clear,calculateIntelligence,setIntelligence,people);
            return;
        }
        terms=draft;this.admitted=admitted;this.submit=submit;unknown=text.Get("detail_unknown");
        playerSender=playerForceId==draft.Request.ForceId;
        Name="DiplomacyTerms";ZIndex=90;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        canvas=new(){Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};AddChild(canvas);
        bool goodwill=draft.Kind==DiplomaticMissionKind.Goodwill;
        int offset=goodwill?0:14;
        Vector2 mount=new(630,goodwill?206:182);
        var source=new UiSourceLayout(repository,goodwill?0x09B68B2Au:0xA2C7C6AEu);
        var data=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages;
        termsMessage=(section,id)=>data.Sections[section].Records[id].Text
            ??throw new InvalidDataException($"Missing diplomacy terms message {section}:{id:X4}.");
        string Message(int id)=>termsMessage(1,id);
        int[] radios=goodwill?[33,37,41,45]:[47,51,55,59];
        var replaced=new HashSet<int>(radios){13,19,49+offset,60+offset,77+offset,98+offset,
            goodwill?107:118,goodwill?104:121,69+offset,73+offset};
        // The controller uses the mouse slider; the authored controller-guide
        // alternative is a separate, hidden subtree.
        replaced.Add(79+offset);
        if(draft.Kind==DiplomaticMissionKind.Alliance)replaced.Add(33);
        foreach(var node in source.Nodes)
        {
            bool hidden=false;
            for(int? at=node.Index;at is int index;at=source.Nodes[index].ParentIndex)
                if(replaced.Contains(index)){hidden=true;break;}
            if(hidden)continue;
            if(node.Type is 1 or 3)source.Art(canvas,node.Index,source.Rectangle(node.Index,mount),assets,shader);
            else if(node.Type==6)source.Blur(canvas,node.Index,source.Rectangle(node.Index,mount));
        }
        header=new(){Name="DiplomacyTermsTitle"};canvas.AddChild(header);
        header.Configure(repository,assets,shader,font,Message(draft.Kind switch
        {DiplomaticMissionKind.Goodwill=>0x084F,DiplomaticMissionKind.Alliance=>0x0850,_=>0x0852}));
        Label(5,playerSender?0x0632:0x0631);
        Value(7,candidate.Forces[draft.Page.DisplayedForceId].Name);
        Label(8,0x0413);Value(12,termsMessage(0,0x0141+draft.Page.RelationLevel));
        if(draft.Kind==DiplomaticMissionKind.Alliance)
        {Label(30,0x0414);Value(32,Number(0x045F,draft.AllianceMonths));}
        else if(draft.Kind==DiplomaticMissionKind.RequestAttack)
        {
            Label(30,0x0478);Label(37,0x01FD);
            var city=draft.TargetCityId is int id?candidate.Cities.GetValueOrDefault(id):null;
            Value(32,city?.Name??unknown);
            Value(39,city?.ForceId is int force&&candidate.Forces.TryGetValue(force,out var owner)?owner.Name:unknown);
        }
        Label(30+offset,0x03CE);conditionText=Value(32+offset,"");Label(59+offset,0x0835);
        Command(13,0x05A2,showForce);Command(19,0x05AD,showMap);
        for(int i=0;i<radios.Length;i++)
        {
            var kind=(DiplomacyNegotiationConditionKind)i;var box=source.Rectangle(radios[i],mount);
            var radio=new UiSourceRadioButton{Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission};
            canvas.AddChild(radio);conditionButtons.Add(radio);
            radio.Configure(repository,assets,shader,font,i==0?UiSourceRadioSegment.Left:
                i==3?UiSourceRadioSegment.Right:UiSourceRadioSegment.Centre,
                Message(new[]{0x020C,0x020D,0x0246,0x0202}[i]),()=>{if(ReadAdmission())chooseCondition(kind);});
        }
        clearCondition=Command(49+offset,0x029F,clear);
        var amountBox=source.Rectangle(60+offset,mount);
        intelligence=new(){Position=amountBox.Position,Size=amountBox.Size,InputAdmission=ReadAdmission};
        canvas.AddChild(intelligence);intelligence.Configure(repository,assets,shader,font,0,
            ()=>{if(ReadAdmission())calculateIntelligence();});
        intelligenceMax=Value(66+offset,"");
        var sliderBox=source.Rectangle(77+offset,mount);
        intelligenceSlider=new(){Position=sliderBox.Position,Size=sliderBox.Size};canvas.AddChild(intelligenceSlider);
        intelligenceSlider.Configure(repository,assets,shader,draft.MilitaryIntelligenceMinimum??0,
            draft.MilitaryIntelligenceMaximum??0,1,draft.MilitaryIntelligenceValue??0,false,
            amount=>{if(ReadAdmission())setIntelligence(amount);});
        var marker=source.Rectangle(73+offset,mount);requiredOrigin=marker.Position+new Vector2(0,3);
        intelligenceRequired=new(){Position=marker.Position,Size=marker.Size,IgnoreTextureSize=true,
            FocusMode=FocusModeEnum.None};canvas.AddChild(intelligenceRequired);
        intelligenceRequired.Pressed+=()=>{if(ReadAdmission()&&draft.RequiredMilitaryIntelligence is int required)setIntelligence(required);};
        source.Art(intelligenceRequired,74+offset,Relative(74+offset,marker),assets,shader);
        requiredText=source.Text(intelligenceRequired,75+offset,default,"",font,rectangle:Relative(75+offset,marker));
        var gaugeBox=source.Rectangle(98+offset,mount);
        probabilityGauge=new(){Position=gaugeBox.Position,Size=gaugeBox.Size};canvas.AddChild(probabilityGauge);
        probabilityGauge.Configure(repository,assets,shader);
        if(appearances.GetValueOrDefault(draft.Request.ForceId) is {} sender&&
            appearances.GetValueOrDefault(draft.Request.TargetForceId) is {} recipient)
            probabilityGauge.ApplyForceColors(sender.Fill,recipient.Fill);
        senderRate=Value(115+offset,"");recipientRate=Value(117+offset,"");
        Flag(draft.Request.ForceId,goodwill?107:118);Flag(draft.Request.TargetForceId,goodwill?104:121);
        endpointFill=appearances.GetValueOrDefault(playerForceId) is {IsValid:true} playerAppearance?playerAppearance.Fill:null;
        var footer=new UiSourceLayout(repository,0xD8913AB2);
        accept=new(){Position=footer.Rectangle(1,new(0,904)).Position,InputAdmission=ReadAdmission,
            ActionMode=BaseButton.ActionModeEnum.Release};canvas.AddChild(accept);
        accept.Configure(repository,assets,shader,text.SourceLocale,"000_cmn_txt_decision_kettei",
            ()=>{if(ReadAdmission()&&canAccept)this.submit?.Invoke(true);});
        var hint=new UiSourceLayout(repository,0xAB6ADAA7);var hintMount=footer.Rectangle(19,new(0,904)).Position;
        foreach(var node in hint.Nodes.Where(node=>node.Type is 1 or 3))
            hint.Art(canvas,node.Index,hint.Rectangle(node.Index,hintMount),assets,shader);
        notice=hint.Text(canvas,hint.Nodes.Single(node=>node.Type==2).Index,hintMount,
            new AdministrationMessages(repository,text.SourceLocale).Format(0x34A1),font);
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("negotiation_stage",(int)draft.Stage);
        ApplyTerms();Resized+=Fit;Fit();

        UiSourceText Value(int index,string value)=>source.Text(canvas,index,mount,value,font);
        void Label(int index,int id)=>Value(index,Message(id));
        string Number(int id,int number)=>Message(id).Replace("%d",number.ToString(CultureInfo.InvariantCulture));
        UiSourceCommandButton Command(int index,int caption,Action clicked)
        {
            var box=source.Rectangle(index,mount);
            var button=new UiSourceCommandButton{Position=box.Position,Size=box.Size,InputAdmission=ReadAdmission};
            canvas.AddChild(button);button.Configure(repository,assets,shader,font,Message(caption),
                ()=>{if(ReadAdmission())clicked();});details.Add(button);return button;
        }
        void Flag(int force,int index)
        {
            if(appearances.GetValueOrDefault(force) is not {} appearance)return;
            var flag=new UiSourceForceFlag{Position=source.Rectangle(index,mount).Position};canvas.AddChild(flag);
            flag.Configure(repository,assets,shader,font,appearance);
        }
        Rect2 Relative(int index,Rect2 parent)
        {var rectangle=source.Rectangle(index,mount);rectangle.Position-=parent.Position;return rectangle;}
    }

    internal void ApplyTerms()
    {
        if(terms is not {} draft||abandoned)return;
        if(additionalArrival){ApplyAdditionalTerms();return;}
        string Number(int? value)=>value?.ToString(CultureInfo.InvariantCulture)??unknown;
        string Format(int id,int amount)=>termsMessage!(1,id).Replace("%d",Number(amount));
        conditionText!.Text=draft.Page.NamedCondition??(draft.Offer.Gold!=0?Format(0x05F8,draft.Offer.Gold):
            draft.Offer.Supplies!=0?Format(0x059D,draft.Offer.Supplies):"");
        intelligence!.Caption=Number(draft.MilitaryIntelligenceValue);intelligenceMax!.Text=Number(draft.MilitaryIntelligenceMaximum);
        requiredText!.Text=Number(draft.RequiredMilitaryIntelligence);
        int? probability=draft.Probability is int raw?Math.Clamp(raw,0,100):null;
        senderRate!.Text=probability is int rate?Format(0x085E,rate).Replace("%%","%",StringComparison.Ordinal):unknown;
        recipientRate!.Text=probability is int rate2?Format(0x085E,100-rate2).Replace("%%","%",StringComparison.Ordinal):unknown;
        bool endpoint=draft.Probability is int p&&(playerSender?p==100:p==0);
        probabilityGauge!.Apply(draft.Probability,endpoint,endpointFill);
        canAccept=draft.Page.CanAccept;SetBusy(busy);
    }

    private void SetTermsBusy()
    {
        if(additionalArrival){SetAdditionalTermsBusy();return;}
        if(terms is not {} draft||clearCondition is null||intelligence is null||
            intelligenceSlider is null||intelligenceRequired is null)return;
        bool unavailable=abandoned||busy;
        bool?[] allowed=[draft.CanChooseGold,draft.CanChooseSupplies,draft.CanChooseItem,draft.CanChooseArea];
        for(int i=0;i<conditionButtons.Count;i++)
        {conditionButtons[i].Disabled=unavailable||allowed[i]!=true;conditionButtons[i].Selected=(int)draft.ConditionKind==i;}
        clearCondition!.Disabled=unavailable||!draft.CanEditConditions;
        bool military=!unavailable&&draft.CanChooseMilitaryIntelligence==true;
        intelligence!.Disabled=!military||draft.MilitaryIntelligenceCalculatorMaximum is not int ceiling||
            ceiling<(draft.MilitaryIntelligenceMinimum??0);
        intelligenceSlider!.SetValue(draft.MilitaryIntelligenceValue??0,draft.MilitaryIntelligenceMaximum??0,military);
        bool required=military&&draft.RequiredMilitaryIntelligence is int amount&&
            amount>=(draft.MilitaryIntelligenceMinimum??0)&&amount<=(draft.MilitaryIntelligenceMaximum??0);
        intelligenceRequired!.Disabled=!required;
        intelligenceRequired.Visible=draft.RequiredMilitaryIntelligence is >0&&
            draft.RequiredMilitaryIntelligence<=draft.MilitaryIntelligenceMaximum;
        if(draft.RequiredMilitaryIntelligence is int position)
        {
            float x=intelligenceSlider.Position.X-15+intelligenceSlider.PositionForValue(position)+2;
            intelligenceRequired.Position=new(x,requiredOrigin.Y);
        }
    }
}

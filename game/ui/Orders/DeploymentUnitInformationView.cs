using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The unit pane reads a selected local draft. Its caller owns every
/// edit and refresh; it never creates an Army or debits campaign resources.</summary>
internal partial class DeploymentUnitInformationView : Control
{
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private MapFont font=null!;
    private PreviewText text=null!;
    private Control modalHost=null!,formation=null!;
    private UiSourceLayout source=null!;
    private UiSourceSlider slider=null!;
    private UiSourceNumberButton number=null!;
    private UiSourceText maximumLabel=null!;
    private DeploymentCombatChart chart=null!;
    private UiSourcePortrait? portrait;
    private DeploymentCalculatorDialog? calculator;
    internal bool HasCalculator=>calculator is not null;
    private Action<int>? changeTroops;
    private Action<int?>? calculatorReturned;
    private int currentTroops;
    private int? maximumTroops;
    private bool abandoned;
    private UiSourceTroopThresholdButton primaryThreshold=null!,secondaryThreshold=null!;
    private int? primaryTroops,secondaryTroops;
    private Control? landChoices;
    private bool formationSelectionBound;
    internal bool FormationSelectionActive=>!abandoned&&!IsQueuedForDeletion()&&IsVisibleInTree()&&
        CanProcess()&&formationSelectionBound&&landChoices is { } choices&&
        !choices.IsQueuedForDeletion()&&choices.IsVisibleInTree();
    internal int? SelectedFormationId {get;private set;}
    internal DeploymentTroopMarker? TroopMarker {get;private set;}

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        PreviewText text,Control modalHost)
    {
        this.repository=repository;this.assets=assets;this.shader=shader;this.font=font;this.text=text;this.modalHost=modalHost;
        source=new UiSourceLayout(repository,0xB20B608C);Size=new(954,783);MouseFilter=MouseFilterEnum.Ignore;
        source.Art(this,1,source.Rectangle(1),assets,shader);
        var sliderBox=source.Rectangle(84);
        slider=new UiSourceSlider {Name="DeploymentTroopSlider",Position=sliderBox.Position,Size=sliderBox.Size};AddChild(slider);
        slider.Configure(repository,assets,shader,1000,0,100,0,false,value=>changeTroops?.Invoke(value));
        primaryThreshold=Threshold("PrimaryTroopThreshold",82,3,()=>primaryTroops);
        secondaryThreshold=Threshold("SecondaryTroopThreshold",80,5,()=>secondaryTroops);
        var functions=new UiSourceLayout(repository,0x2B7AA77D);var palettes=MapSpritePalettes.Create();
        foreach(int node in new[]{75,111})functions.Art(this,11,source.Rectangle(node),assets,shader);
        foreach(int node in new[]{77,113})source.Art(this,node,source.Rectangle(node),assets,shader,palettes[28]);
        var numberBox=source.Rectangle(86);
        number=new UiSourceNumberButton {Name="DeploymentTroopNumber",Position=numberBox.Position,Size=numberBox.Size,Disabled=true};AddChild(number);
        number.Configure(repository,assets,shader,font,0,OpenCalculator);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        var presets=MapTextPresets.Create();
        foreach(var binding in new[]{(20,0x0360),(49,0x022B),(79,0x020E),(90,0x0234)})
            source.Text(this,binding.Item1,default,messages.Records[binding.Item2].Text!,font,presets[13]);
        maximumLabel=source.Text(this,88,default,"/0",font);
        chart=new DeploymentCombatChart {Name="UnitCombatChart",Position=source.Rectangle(7).Position};AddChild(chart);
        chart.Configure(repository,assets,shader,font,text);
        ApplySelection(null,null,null,null,null);

        UiSourceTroopThresholdButton Threshold(string name,int node,int classification,Func<int?> value)
        {
            var button=new UiSourceTroopThresholdButton {Name=name,Position=source.Rectangle(node).Position};AddChild(button);
            button.Configure(repository,assets,shader,font,classification,()=>
            {if(value() is int troops)changeTroops?.Invoke(troops);});
            return button;
        }
    }
    internal void ApplySelection(DeploymentDraftRow? row,DeploymentDraftUnitInfo? info,DeploymentDraftFigures? figures,
        LoadedSprite? resolvedPortrait,Action<int>? changed,Action<int?>? calculatorReturned=null)
    {
        if(abandoned)return;
        changeTroops=changed;this.calculatorReturned=calculatorReturned;
        TroopMarker=row is null?null:info?.LandTroops?.Marker??figures?.TroopMarker;
        currentTroops=row?.Request.Troops??0;maximumTroops=info?.MaximumTroops;
        bool enabled=row is not null&&maximumTroops is >999&&changed is not null;
        slider.SetValue(currentTroops,maximumTroops??0,enabled);number.Value=currentTroops;number.Disabled=!enabled;
        primaryTroops=row is null?null:info?.LandTroops?.PrimaryThreshold;
        secondaryTroops=row is null?null:info?.LandTroops?.SecondaryThreshold;
        RefreshThreshold(primaryThreshold,primaryTroops);
        RefreshThreshold(secondaryThreshold,secondaryTroops);
        maximumLabel.Text="/"+(row is null?"0":maximumTroops?.ToString(System.Globalization.CultureInfo.InvariantCulture)??text.Get("detail_unknown"));
        chart.ApplyFigures(row is null?null:figures);
        if(formation is not null){formation.Hide();formation.Name="PreviousFormation";formation.QueueFree();}
        var frame=source.Rectangle(31);
        if(row is not null&&info is not null)
        {
            var selected=new DeploymentFormationSummary {Name="SelectedFormation",Position=frame.Position};AddChild(selected);
            selected.Configure(repository,assets,shader,font,text,info);formation=selected;
        }
        else
        {
            formation=new Control {Name="EmptyFormation",Position=frame.Position,Size=frame.Size,MouseFilter=MouseFilterEnum.Ignore};AddChild(formation);
            foreach(int node in new[]{32,33,34})
            {
                var box=source.Rectangle(node);box.Position-=frame.Position;source.Art(formation,node,box,assets,shader);
            }
        }
        if(portrait is not null){portrait.Hide();portrait.Name="PreviousPortrait";portrait.QueueFree();portrait=null;}
        if(row is not null&&resolvedPortrait is not null)
        {
            var box=source.Rectangle(3);portrait=new UiSourcePortrait {Name="SelectedPortrait",Position=box.Position,Size=box.Size};AddChild(portrait);
            portrait.ConfigureFace3(repository,assets,shader,resolvedPortrait);
        }
        if(row is null){calculator?.RequestCancel();ApplyLandFormationChoices(null,0,null);ApplyTacticChoices(null,null);}

        void RefreshThreshold(UiSourceTroopThresholdButton button,int? troops)
        {
            button.Visible=troops is >0&&maximumTroops is int maximum&&troops<=maximum;
            button.Disabled=changed is null;
            if(button.Visible)button.Position=new(slider.Position.X-11+slider.PositionForValue(troops!.Value),button.Position.Y);
        }
    }
    private void OpenCalculator()
    {
        if(abandoned||calculator is not null||number.Disabled||maximumTroops is not int maximum||maximum<=999)return;
        var dialog=new DeploymentCalculatorDialog();calculator=dialog;modalHost.AddChild(dialog);
        try
        {
            dialog.Configure(repository,text,currentTroops,1000,maximum,result=>
            {
                calculator=null;dialog.QueueFree();
                if(calculatorReturned is { } returned)returned(result);
                else if(result is int troops)changeTroops?.Invoke(troops);
            });
        }
        catch{calculator=null;dialog.QueueFree();throw;}
    }

    internal bool TryCancelCalculator()
    {
        if(calculator is not { } dialog)return false;
        dialog.RequestCancel();return true;
    }

    internal void AbandonInteraction()
    {
        abandoned=true;
        AbandonTacticInteraction();
        formationSelectionBound=false;
        changeTroops=null;calculatorReturned=null;
        number.Disabled=primaryThreshold.Disabled=secondaryThreshold.Disabled=true;
        slider.SetValue(currentTroops,maximumTroops??0,false);
        if(calculator is { } dialog){calculator=null;dialog.Abandon();dialog.QueueFree();}
    }

    internal void ApplyLandFormationChoices(DeploymentLandFormationChoices? query,int selectedFormation,Action<int>? choose)
    {
        if(abandoned)return;
        formationSelectionBound=false;SelectedFormationId=null;
        if(landChoices is not null){landChoices.Hide();landChoices.Name="PreviousLandFormationChoices";landChoices.QueueFree();landChoices=null;}
        if(query?.Choices is not { } choices)return;
        SelectedFormationId=selectedFormation;
        landChoices=new Control {Name="LandFormationChoices",MouseFilter=MouseFilterEnum.Ignore};AddChild(landChoices);
        // The source query owns six candidates, but this pane registers five
        // radio bodies. The remaining candidate belongs to formation details.
        int index=0;
        foreach(var choice in choices.Take(5))
        {
            var button=new DeploymentLandFormationButton {Name="LandFormation"+choice.FormationId,
                Position=new(96+index++*118,456),Selected=choice.FormationId==selectedFormation,Disabled=choose is null};
            landChoices.AddChild(button);button.Configure(repository,assets,shader,font,choice,id=>choose?.Invoke(id));
        }
        formationSelectionBound=choose is not null&&index>0;
    }
}

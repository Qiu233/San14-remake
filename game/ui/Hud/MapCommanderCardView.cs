using System.Globalization;
using Godot;
using San14.Data;

/// <summary>One retained original commander card; all current rules are supplied.</summary>
internal partial class MapCommanderCardView : Control
{
    private UiSourceLayout source=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private UiSourceHitButton focus=null!;
    private MapCommanderFocusView focusVisual=null!;
    private UiSourceText name=null!,troops=null!;
    private UiSourceMultilineText warning=null!,unlock=null!;
    private Control plus=null!,locked=null!,warningBacking=null!;
    private Control? formation,supply;
    private UiSourceBarGauge morale=null!;
    private MapCommanderGauge gauge=null!;
    private CommanderCardActionButton automatic=null!,remove=null!;
    private readonly UiSourceCaptionButton[] tactics=new UiSourceCaptionButton[3];
    private readonly string?[] tacticNames=new string?[3];
    private CommanderHudCard? current;
    private ArmyMapIconSelection? shownFormation;
    private int? shownSupply;
    private bool hasSupply;
    private Func<bool> admission=null!;
    private Action<int,int>? tacticSelected;
    private Action<int>? automaticSelected,removeSelected,focusSelected;
    internal Action<int?>? HoveredOfficer {get;set;}

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,MapMarkerLayout bar,
        Func<bool> admission,Action<int> focusSelected,Action<int,int> tacticSelected,
        Action<int> automaticSelected,Action<int> removeSelected)
    {
        this.assets=assets;this.shader=shader;this.admission=admission;this.focusSelected=focusSelected;
        this.tacticSelected=tacticSelected;this.automaticSelected=automaticSelected;this.removeSelected=removeSelected;
        source=new(repository,0x57BE88BD);Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;
        source.Art(this,2,source.Rectangle(2),assets,shader);
        focus=new UiSourceHitButton {Name="CommanderFocus",Position=source.Rectangle(1).Position,
            Size=source.Rectangle(1).Size,IgnoreTextureSize=true,FocusMode=FocusModeEnum.None};AddChild(focus);
        focus.Pressed+=()=>{if(admission()&&current is{} card)focusSelected(card.Slot);};
        focus.MouseEntered+=()=>HoveredOfficer?.Invoke(current?.OfficerId);
        focus.MouseExited+=()=>HoveredOfficer?.Invoke(null);
        name=source.Text(this,4,default,"",font);name.Name="CommanderName";
        troops=source.Text(this,5,default,"",font);troops.Name="CommanderTroops";
        plus=source.Art(this,7,source.Rectangle(7),assets,shader);
        locked=source.Art(this,8,source.Rectangle(8),assets,shader);
        unlock=StatusText(9,"CommanderUnlock");
        warningBacking=source.Art(this,11,source.Rectangle(11),assets,shader);
        warning=StatusText(12,"CommanderWarning");
        var mount=source.Rectangle(13);
        morale=new UiSourceBarGauge {Name="CommanderMorale",Position=mount.Position};AddChild(morale);
        morale.Configure(bar,assets,shader,3);
        morale.Scale=mount.Size/morale.Size;
        gauge=new MapCommanderGauge {Name="CommanderGauge",Position=source.Rectangle(16).Position};
        AddChild(gauge);gauge.Configure(repository,assets,shader);
        var focusMount=source.Rectangle(29);
        focusVisual=new MapCommanderFocusView {Name="CommanderFocusVisual",Position=focusMount.Position};
        AddChild(focusVisual);focusVisual.Configure(repository,assets,shader,focusMount.Size);
        focus.MouseEntered+=ApplyFocusInteraction;focus.MouseExited+=ApplyFocusInteraction;
        focus.ButtonDown+=ApplyFocusInteraction;focus.ButtonUp+=ApplyFocusInteraction;
        remove=new CommanderCardActionButton {Name="RemoveCommander",Position=source.Rectangle(33).Position,
            InputAdmission=admission};AddChild(remove);
        remove.Configure(repository,assets,shader,false,()=>{if(current is{} card)removeSelected(card.Slot);});
        automatic=new CommanderCardActionButton {Name="CommanderAutomatic",Position=source.Rectangle(39).Position,
            InputAdmission=admission};AddChild(automatic);
        automatic.Configure(repository,assets,shader,true,()=>{if(current is{} card)automaticSelected(card.Slot);});
        for(int i=0;i<tactics.Length;i++)
        {
            int slot=i;Rect2 place=source.Rectangle(45+i*6);
            var button=new UiSourceCaptionButton {Name="CommanderTactic"+i,Position=place.Position,Size=place.Size,
                InputAdmission=admission};AddChild(button);tactics[i]=button;
            button.Configure(repository,assets,shader,font,0x60C991A4,"",()=>
            {if(admission()&&current is{} card)tacticSelected(card.Slot,slot);});
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);

        UiSourceMultilineText StatusText(int node,string name)
        {
            var rectangle=source.Rectangle(node);
            var text=new UiSourceMultilineText {Name=name,Position=rectangle.Position,Size=rectangle.Size};AddChild(text);
            text.ConfigureCommanderStatus(font,MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri));
            return text;
        }
    }

    internal void Apply(CommanderHudCard card,OfficerPortraitRoute? portrait,string shortcut,string unknown,
        bool canSelect,int? selectedUnit,int? selectedSlot)
    {
        current=card;
        bool occupied=card.OfficerId.HasValue;
        bool army=card.State is CommanderHudCardState.ReadyArmy or CommanderHudCardState.WaitingArmy;
        focus.Disabled=!admission()||card.State is null||card.State==CommanderHudCardState.Locked||
            (card.State==CommanderHudCardState.Empty?!canSelect:card.HexId is null);
        focus.TooltipText=shortcut;
        ApplyFocusInteraction();
        name.Visible=gauge.Visible=automatic.Visible=occupied;
        name.Text=card.Name;automatic.Disabled=card.AutomaticEnabled is null||!admission();
        automatic.ApplyChecked(card.AutomaticEnabled);
        remove.Visible=card.CanRemove;remove.Disabled=!admission();
        plus.Visible=card.State==CommanderHudCardState.Empty;locked.Visible=card.State==CommanderHudCardState.Locked;
        unlock.Visible=locked.Visible;unlock.Apply(card.UnlockText??unknown);
        troops.Visible=morale.Visible=army;
        troops.Text=card.Troops?.ToString(CultureInfo.InvariantCulture)??unknown;
        if(card.Morale is int value&&card.MoraleMaximum is int maximum)morale.Apply(value,maximum);
        else morale.Visible=false;
        warningBacking.Visible=warning.Visible=occupied&&card.State is not CommanderHudCardState.ReadyArmy;
        warning.Apply(card.StatusText??unknown);
        gauge.Apply(card.GaugeFrames,portrait);
        var icon=army?card.FormationIcon:null;
        if(icon!=shownFormation)
        {
            if(formation is not null){RemoveChild(formation);formation.QueueFree();formation=null;}
            shownFormation=icon;
            if(icon is{} selected)formation=source.Art(this,3,source.Rectangle(3),assets,shader,
                MapSpritePalettes.Create()[selected.Palette],commonSpriteOverride:ArmyMapUiSources.Create().IconName(selected));
        }
        if(formation is not null)formation.Visible=army;
        if(!hasSupply||shownSupply!=card.SupplyPalette)
        {
            if(supply is not null){RemoveChild(supply);supply.QueueFree();supply=null;}
            shownSupply=card.SupplyPalette;hasSupply=true;
            if(card.SupplyPalette is int palette)supply=source.Art(this,6,source.Rectangle(6),assets,shader,
                MapSpritePalettes.Create()[palette]);
        }
        if(supply is not null)supply.Visible=army;
        for(int i=0;i<tactics.Length;i++)
        {
            var row=card.Tactics.FirstOrDefault(value=>value.Slot==i);
            bool visible=card.State==CommanderHudCardState.ReadyArmy&&row?.TacticId is not null;
            tactics[i].Visible=visible;tactics[i].Disabled=!visible||row?.Enabled!=true||!admission();
            if(row?.Name!=tacticNames[i])
            {tacticNames[i]=row?.Name;tactics[i].ApplyCaption(row?.Name??unknown);}
            tactics[i].Selected=visible&&card.UnitId==selectedUnit&&selectedSlot==i;
        }
        SetMeta("commander_slot",card.Slot);SetMeta("officer_id",card.OfficerId??-1);
        SetMeta("source_card_state",card.State?.ToString()??"Unknown");
        SetMeta("source_current_issues",string.Join(";",card.UnresolvedFields.Select(row=>row.Key+":"+row.Value)));
    }

    private void ApplyFocusInteraction() => focusVisual.ApplyInteraction(focus.IsHovered(),
        !focus.Disabled && focus.GetDrawMode() is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed);

    internal void ApplySelection(int? unit,int? slot)
    {
        for(int i=0;i<tactics.Length;i++)
            tactics[i].Selected=unit.HasValue&&current?.UnitId==unit&&slot==i&&tactics[i].Visible;
    }
}

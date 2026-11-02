using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record DeploymentTargetRowContent(int OfficerId,string ArmyName,int Troops,
    ArmyMapIconSelection? FormationIcon,int? GrainPalette,(string Pack,string Name)? Portrait,int? EstimatedDays,bool HasResolvedTarget)
{
    internal bool RetainInitialFormation {get;init;}
}

internal sealed record DeploymentTargetProgress(int OfficerId,int? EstimatedDays,bool HasResolvedTarget);

/// <summary>The original target-list wrapper and its independently selectable
/// Face5 card. Its values belong to an unsubmitted deployment draft.</summary>
internal partial class DeploymentTargetRow : Control
{
    private UiSourceButton card=null!;
    private UiSourceText status=null!;
    private DeploymentTargetRowContent content=null!;
    private string daysFormat="",unknown="";
    internal bool Selected=>card.Selected;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        PreviewText text,DeploymentTargetRowContent value,bool selected,Action toggled)
    {
        content=value;Size=new(406,86);MouseFilter=MouseFilterEnum.Pass;
        var wrapper=new UiSourceLayout(repository,0xACEC3A70);
        var source=new UiSourceLayout(repository,0x4BC1E64C);
        var overlay=new UiSourceLayout(repository,0x26B05ED2);
        var palettes=MapSpritePalettes.Create();
        daysFormat=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1]
            .Records[0x0324].Text??throw new InvalidDataException("Missing target ETA format.");
        unknown=text.Get("detail_unknown");
        status=wrapper.Text(this,4,default,"",font);
        status.Name="TargetEta";
        // Static ETA is one of the source's two alternating states. The action
        // icon is hidden in this state; no new destination-name field is added.
        var mount=wrapper.Rectangle(1);
        card=new UiSourceButton {Name="TargetCard",Position=mount.Position,Size=mount.Size,
            ActionMode=BaseButton.ActionModeEnum.Release};
        AddChild(card);
        source.Art(card,2,source.Rectangle(2),assets,shader);
        if(content.FormationIcon is { } formation)
        {
            string name=ArmyMapUiSources.Create().IconName(formation);
            source.Art(card,3,source.Rectangle(3),assets,shader,palettes[formation.Palette],
                commonSpriteOverride:name,expansionCommonSprite:formation.SpriteId==0x3F0);
            SetMeta("formation_sprite",name);SetMeta("formation_palette",formation.Palette);
        }
        else if(content.RetainInitialFormation)
        {
            // A known invalid Formation makes930300 leave the initialized
            // node untouched. Missing current input must not take this branch.
            source.Art(card,3,source.Rectangle(3),assets,shader,palettes[9]);
            SetMeta("formation_retains_initial",true);
        }
        source.Text(card,4,default,content.ArmyName,font).Name="TargetArmyName";
        source.Art(card,5,source.Rectangle(5),assets,shader,palettes[10]);
        source.Text(card,6,default,content.Troops.ToString(CultureInfo.InvariantCulture),font).Name="TargetTroops";
        if(content.GrainPalette is int grain)
            source.Art(card,7,source.Rectangle(7),assets,shader,palettes[grain]);
        // The real card retains this authored base. Its constructor and row
        // writer never replace node8 with a gauge or publish a fill value.
        source.Art(card,9,source.Rectangle(9),assets,shader).Name="TargetGrainBase";
        if(content.Portrait is { } portrait)
        {
            var face=new UiSourcePortrait {Name="TargetPortrait",Position=new(7,0),Size=new(122,129)};
            card.AddChild(face);face.ConfigureFace5(repository,assets,shader,assets.Portraits.RequireSource(portrait.Pack,portrait.Name));
        }
        var states=new Control[5];
        for(int state=0;state<states.Length;state++)
        {
            states[state]=new Control {Name="TargetState"+state,MouseFilter=MouseFilterEnum.Ignore};
            card.AddChild(states[state]);
            if(state is 1 or 2)
            {
                int node=state==1?2:5;
                overlay.Art(states[state],node,overlay.Rectangle(node),assets,shader);
            }
        }
        // Focus's additive flare is deferred; its ordinary main image and the
        // persistent push layer use the actual replacement resource.
        card.ConfigureStates(states);
        card.Pressed+=()=>{if(!card.Disabled)toggled();};
        ApplySelected(selected);
        SetMeta("source_layout",wrapper.Source.Source.ResourceUri);
        SetMeta("officer_id",value.OfficerId);
        SetMeta("formation_available",value.FormationIcon.HasValue);
        SetMeta("grain_available",value.GrainPalette.HasValue);
    }

    internal void ApplySelected(bool selected)
    {
        card.Selected=selected;
        status.Text=selected||content.HasResolvedTarget
            ?content.EstimatedDays is int days
                ?daysFormat.Replace("%d",Math.Min(days,999).ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal)
                :unknown
            :"";
    }

    internal void ApplyProgress(DeploymentTargetProgress progress)
    {
        if(progress.OfficerId!=content.OfficerId)throw new ArgumentException("Target progress belongs to another row.",nameof(progress));
        content=content with{EstimatedDays=progress.EstimatedDays,HasResolvedTarget=progress.HasResolvedTarget};
        ApplySelected(Selected);
    }
}

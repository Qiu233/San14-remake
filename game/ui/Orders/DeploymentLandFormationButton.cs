using Godot;
using San14.Data;

/// <summary>The original radio body with a fixed-style outer name and icon.</summary>
internal partial class DeploymentLandFormationButton : UiSourceButton
{
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        DeploymentLandFormationChoice choice,Action<int> choose)
    {
        Size=new(100,86);ActionMode=ActionModeEnum.Release;
        var body=new UiSourceLayout(repository,0x5794E0A4).ResizedSubtree(0,Size,true);
        var states=new int[]{3,5,8,11,13}.Select(node=>body.Art(this,node,body.Rectangle(node),assets,shader)).ToArray();
        var outer=new UiSourceLayout(repository,0x2AF6B1CB);
        var names=ArmyMapUiSources.Create();
        outer.Art(this,3,outer.Rectangle(3),assets,shader,MapSpritePalettes.Create()[25],
            commonSpriteOverride:names.IconName(choice.Icon),expansionCommonSprite:choice.Icon.SpriteId==0x3EF);
        outer.Text(this,4,default,choice.Name,font,MapTextPresets.Create()[3]);
        ConfigureStates(states);
        Pressed+=()=>{if(!Disabled&&!Selected)choose(choice.FormationId);};
        SetMeta("formation_id",choice.FormationId);
        SetMeta("source_layout",outer.Source.Source.ResourceUri);
    }
}

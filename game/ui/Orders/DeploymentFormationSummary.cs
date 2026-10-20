using Godot;
using San14.Data;

/// <summary>The selected draft's two formation rows and compact compatible
/// tactic names. The special naval caption belongs to the enclosing pane.</summary>
internal partial class DeploymentFormationSummary : Control
{
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        PreviewText text,DeploymentDraftUnitInfo info)
    {
        var source=new UiSourceLayout(repository,0xB20B608C);
        Rect2 frame=source.Rectangle(31);Size=frame.Size;MouseFilter=MouseFilterEnum.Ignore;
        var palettes=MapSpritePalettes.Create();var names=ArmyMapUiSources.Create();
        string unknown=text.Get("detail_unknown");
        Rect2 Box(int node){var box=source.Rectangle(node);box.Position-=frame.Position;return box;}
        foreach(int node in new[]{32,33,34})source.Art(this,node,Box(node),assets,shader);
        Formation(35,36,37,info.LandFormation,info.LandTroops);
        Formation(42,43,44,info.NavalFormation,info.NavalTroops);
        Tactics(39,info.LandTactics,true);
        Tactics(46,info.NavalTactics,info.UsesSpecialNavalTacticText!=true);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("source_frame_node",31);

        void Formation(int iconNode,int nameNode,int massNode,DeploymentFormationPresentation? formation,
            DeploymentTroopClassification? classification)
        {
            if(formation is not null)
                source.Art(this,iconNode,Box(iconNode),assets,shader,palettes[formation.Icon.Palette],
                    commonSpriteOverride:names.IconName(formation.Icon),expansionCommonSprite:formation.Icon.SpriteId==0x3F0);
            source.Text(this,nameNode,default,formation?.Name??unknown,font,rectangle:Box(nameNode));
            var mass=new DeploymentTroopMass {Name="Mass"+massNode,Position=Box(massNode).Position};
            AddChild(mass);mass.Configure(repository,assets,shader,classification?.Marker);
        }
        void Tactics(int first,IReadOnlyList<DeploymentTacticPresentation>? tactics,bool visible)
        {
            for(int index=0;index<3;index++)
            {
                int node=first+index;
                var label=source.Text(this,node,default,"",font,rectangle:Box(node));
                label.Configure(font,MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri) with
                    {RawAlign=0x11,RawLineArrange=2});
                label.Text=tactics is null?unknown:index<tactics.Count?tactics[index].Name:"";
                label.Visible=visible;
            }
        }
    }
}

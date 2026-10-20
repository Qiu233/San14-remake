using Godot;
using San14.Data;

/// <summary>The source one/three/five troop marker with recursive integer resize.</summary>
internal partial class DeploymentTroopMass : Control
{
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,DeploymentTroopMarker? marker)
    {
        MouseFilter=MouseFilterEnum.Ignore;
        var source=new UiSourceLayout(repository,0x88F70FD8).ResizedSubtree(0,new(64,31),false);
        Size=new(64,31);
        Visible=marker.HasValue;
        source.Art(this,1,source.Rectangle(1),assets,shader,commonSpriteOverride:"000_cmn_mrk_mass_base");
        foreach(int node in new[]{2,3,4,5,6})
        {
            var art=source.Art(this,node,source.Rectangle(node),assets,shader,commonSpriteOverride:"000_cmn_mrk_mass");
            art.Visible=node==2||node<=4&&marker is DeploymentTroopMarker.Default or DeploymentTroopMarker.AtSecondaryThreshold||
                node>=5&&marker==DeploymentTroopMarker.AtSecondaryThreshold;
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
}

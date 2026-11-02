using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum DeploymentPolicySetting { EnemyApproach, Retreat, Pursuit, AfterAction }
internal sealed record DeploymentPolicyPair(bool? FirstSelected,bool? FirstAllowed,bool? SecondAllowed);

/// <summary>The ordinary target menu's four independently mixed policy pairs.
/// The presenter owns policies and the separate save-defaults transaction.</summary>
internal partial class DeploymentPolicyView : Control
{
    private readonly List<(UiSourceCaptionButton First,UiSourceCaptionButton Second,Control FirstLock,Control SecondLock)> pairs=[];
    private UiSourceCaptionButton save=null!;
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private bool ownsResources;
    private Action<DeploymentPolicySetting,bool>? choose;
    private Action? saveDefaults;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository,PreviewText text,
        Action<DeploymentPolicySetting,bool> selected,Action saveRequested,bool showSaveDefaults=true,
        AdministrationPageChrome.Resources? resources=null)
    {
        Name="DeploymentPolicies";MouseFilter=MouseFilterEnum.Stop;
        choose=selected;saveDefaults=saveRequested;
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);font=resources?.Font??new(repository,text.SourceLocale);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        var source=assets.SourceLayout(0x96B33F19);Size=source.Rectangle(0).Size;
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string Message(int id)=>messages.Records[id].Text??throw new InvalidDataException("Missing deployment policy caption.");
        source.Blur(this,1,source.Rectangle(1));
        // The authored hidden decision button has no ordinary visible draw.
        source.Art(this,2,source.Rectangle(2),assets,shader);
        var presets=MapTextPresets.Create();
        foreach(var binding in new[]{(3,4,0x0278),(8,9,0x0279),(17,18,0x025A),(26,27,0x027B),(35,36,0x027C)})
        {
            source.Art(this,binding.Item1,source.Rectangle(binding.Item1),assets,shader);
            source.Text(this,binding.Item2,default,Message(binding.Item3),font,presets[13]);
        }
        int[][] labels=[[0x027D,0x027E],[0x027F,0x0280],[0x027F,0x0280],[0x025A,0x0282]];
        for(int group=0;group<4;group++)
        {
            int index=group;int firstNode=10+9*group,secondNode=12+9*group;
            var first=Button("Policy"+group+"First",firstNode,0xA97E37BE,Message(labels[group][0]),()=>Choose(index,true));
            var second=Button("Policy"+group+"Second",secondNode,0x01649C44,Message(labels[group][1]),()=>Choose(index,false));
            var firstLock=Lock(14+9*group);
            var secondLock=Lock(15+9*group);
            firstLock.Name="Policy"+group+"FirstLock";secondLock.Name="Policy"+group+"SecondLock";
            pairs.Add((first,second,firstLock,secondLock));
        }
        save=Button("SaveDeploymentDefaults",5,0x60C991A4,Message(0x0367),()=>
        {if(!abandoned)saveDefaults?.Invoke();});
        save.Visible=showSaveDefaults;
        Apply(false,Enumerable.Repeat(new DeploymentPolicyPair(null,null,null),4).ToArray(),false);
        SetMeta("source_layout",source.Source.Source.ResourceUri);

        UiSourceCaptionButton Button(string name,int node,uint layout,string caption,Action released)
        {
            Rect2 rectangle=source.Rectangle(node);
            var record=source.Nodes[node];
            // The original replacement takes short dimensions times the
            // source scale, not a float bounding rectangle's subtraction.
            var size=new Vector2((int)(record.Value44*record.HeaderValues[7]),(int)(record.Value46*record.HeaderValues[8]));
            var button=new UiSourceCaptionButton{Name=name,Position=rectangle.Position,Size=size};AddChild(button);
            button.Configure(repository,assets,shader,font,layout,caption,released);return button;
        }
        void Choose(int group,bool first){if(!abandoned)choose?.Invoke((DeploymentPolicySetting)group,first);}
        UiSourceSprite Lock(int node)
        {
            var record=source.Nodes[node];var parent=source.Rectangle(record.ParentIndex!.Value).Position;
            // 92DC70 truncates the centered anchor before adding the parent's
            // integer origin. 92E030 then copies 22x22 into a unit-scale sprite.
            var position=new Vector2((int)parent.X+(int)record.HeaderValues[0]-(int)(record.Value44*record.HeaderValues[7]*.5f),
                (int)parent.Y-(int)record.HeaderValues[1]-(int)(record.Value46*record.HeaderValues[8]*.5f));
            var size=new Vector2((int)(record.Value44*record.HeaderValues[7]),(int)(record.Value46*record.HeaderValues[8]));
            var icon=new UiSourceSprite{Position=position,Size=size};AddChild(icon);
            var uv=new LcskUvQuad(new(0,0),new(1,0),new(0,1),new(1,1));
            icon.Configure(shader,assets.SrgbCommonSpritePart("000_cmn_icon_lock",uv),UiSourceSprite.Blend.Ordinary,
                MapSpritePalettes.Create()[10]);
            icon.SetMeta("source_common_sprite","000_cmn_icon_lock");icon.SetMeta("source_palette",10);
            return icon;
        }
    }

    internal void Apply(bool enabled,IReadOnlyList<DeploymentPolicyPair> values,bool canSaveDefaults)
    {
        if(abandoned)return;
        if(values.Count!=4)throw new ArgumentException("The policy panel requires four pairs.",nameof(values));
        for(int i=0;i<pairs.Count;i++)
        {
            var pair=pairs[i];var value=values[i];
            pair.First.Selected=enabled&&value.FirstSelected==true;
            pair.Second.Selected=enabled&&value.FirstSelected==false;
            pair.First.Disabled=!enabled||value.FirstAllowed!=true;
            pair.Second.Disabled=!enabled||value.SecondAllowed!=true;
            pair.FirstLock.Visible=enabled&&value.FirstAllowed==false;
            pair.SecondLock.Visible=enabled&&value.SecondAllowed==false;
        }
        save.Disabled=!enabled||!canSaveDefaults;
    }

    internal void Abandon()
    {
        if(abandoned)return;abandoned=true;choose=null;saveDefaults=null;Hide();
        foreach(var pair in pairs){pair.First.Disabled=true;pair.Second.Disabled=true;}
        if(save is not null)save.Disabled=true;
    }
    public override void _ExitTree(){choose=null;saveDefaults=null;if(ownsResources){assets?.Dispose();font?.Dispose();shader?.Dispose();}}
}

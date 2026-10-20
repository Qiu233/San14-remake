using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original large combat chart and its two indexed radar layers.</summary>
internal partial class DeploymentCombatChart : Control
{
    private readonly List<UiSourceText> values=[];
    private uint increaseColour,decreaseColour;
    private string unknown="";
    private UiSourceRadarMesh current=null!,comparison=null!;
    private int[] maxima=[];

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,PreviewText text)
    {
        var source=new UiSourceLayout(repository,0x8EE99810);
        Size=new(source.Nodes[0].Value44,source.Nodes[0].Value46);MouseFilter=MouseFilterEnum.Ignore;
        var presets=MapTextPresets.Create();var palettes=MapSpritePalettes.Create();
        increaseColour=presets[38].LT.CpuPacked;decreaseColour=presets[40].LT.CpuPacked;
        unknown=text.Get("detail_unknown");
        var profile=DeploymentRadarPresentation.ForLocale(text.SourceLocale);
        var max=profile.Maximums;maxima=[max.Mobility,max.FieldAttack,max.SiegeTroopAttack,max.WallAttack,max.Defense];
        source.Art(this,1,source.Rectangle(1),assets,shader);
        current=Layer(2,"CurrentRadar",profile.LargeCurrentPalette);
        comparison=Layer(3,"ComparisonRadar",profile.LargeComparisonMinimumPalette);
        foreach(int node in new[]{4,15,16,17,18,19})
            source.Art(this,node,source.Rectangle(node),assets,shader,node>=15?palettes[10]:null);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        int[] captions=[0x022F,0x022C,0x022D,0x0230,0x022E];
        for(int axis=0;axis<5;axis++)
        {
            source.Text(this,5+axis*2,default,messages.Records[captions[axis]].Text!,font,presets[13]);
            var label=source.Text(this,6+axis*2,default,"",font,presets[0]);values.Add(label);label.Visible=false;
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        ApplyFigures(null);

        UiSourceRadarMesh Layer(int node,string name,int palette)
        {
            var mesh=new UiSourceRadarMesh {Name=name};AddChild(mesh);mesh.Configure(source,node,palettes[palette].LT);return mesh;
        }
    }

    internal void ApplyFigures(DeploymentDraftFigures? figures)
    {
        int[]? before=Axes(figures?.WithoutConditionalModifiers),after=Axes(figures?.WithConditionalModifiers);
        // Empty data collapse to the authored off-centre point; unresolved
        // nonempty data hide the corresponding geometry instead of making zeros.
        current.ApplyValues(figures is null?[0,0,0,0,0]:after,maxima);
        comparison.ApplyValues(figures is null?[0,0,0,0,0]:before is not null&&after is not null?
            before.Select((value,axis)=>Math.Min(value,after[axis])).ToArray():null,maxima);
        for(int axis=0;axis<values.Count;axis++)
        {
            var label=values[axis];label.Visible=figures is not null;
            if(before is null||after is null){label.Text=unknown;continue;}
            if(before[axis]<=0||before[axis]==after[axis])label.Text=Number(after[axis]);
            else if(before[axis]<after[axis])label.SetRuns(new(Number(before[axis])),
                new("+"+Number(unchecked(after[axis]-before[axis])),increaseColour));
            else label.SetRuns(new(Number(before[axis])),new("-"+Number(unchecked(before[axis]-after[axis])),decreaseColour));
        }
    }

    private static int[]? Axes(DeploymentCombatAxes? axes)=>axes is null?null:
        [axes.Mobility,axes.FieldAttack,axes.SiegeTroopAttack,axes.WallAttack,axes.Defense];
    private static string Number(int value)=>value.ToString(CultureInfo.InvariantCulture);
}

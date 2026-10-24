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

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,PreviewText text,
        IReadOnlyList<int?>? signedModifiers=null)
    {
        if(signedModifiers is not null&&signedModifiers.Count!=5)throw new ArgumentException("Five radar modifiers are required.",nameof(signedModifiers));
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
        source.Art(this,4,source.Rectangle(4),assets,shader);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        int[] captions=[0x022F,0x022C,0x022D,0x0230,0x022E];
        int[] decorations=[15,16,17,19,18];
        for(int axis=0;axis<5;axis++)
        {
            int? modifier=signedModifiers?[axis];
            int decoration=decorations[axis];
            source.Art(this,decoration,source.Rectangle(decoration),assets,shader,palettes[modifier>0?63:modifier<0?64:10]);
        }
        for(int axis=0;axis<5;axis++)
        {
            int? modifier=signedModifiers?[axis];
            source.Text(this,5+axis*2,default,messages.Records[captions[axis]].Text!,font,presets[modifier>0?73:modifier<0?74:13]);
            var label=source.Text(this,6+axis*2,default,"",font,presets[0]);values.Add(label);label.Visible=false;
            SetMeta($"modifier_known_{axis}",signedModifiers is null||modifier.HasValue);
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
        ApplyComparison(Axes(figures?.WithoutConditionalModifiers),Axes(figures?.WithConditionalModifiers),figures is null);
    }

    internal void ApplyComparison(IReadOnlyList<int?>? before,IReadOnlyList<int?>? after)=>ApplyComparison(before,after,false);

    private void ApplyComparison(IReadOnlyList<int?>? before,IReadOnlyList<int?>? after,bool empty)
    {
        if(before is not null&&before.Count!=5||after is not null&&after.Count!=5)
            throw new ArgumentException("Five radar values are required.");
        int[]? knownBefore=Complete(before),knownAfter=Complete(after);
        // Empty data collapse to the authored off-centre point; unresolved
        // nonempty data hide the corresponding geometry instead of making zeros.
        current.ApplyValues(empty?[0,0,0,0,0]:knownAfter,maxima);
        comparison.ApplyValues(empty?[0,0,0,0,0]:knownBefore is not null&&knownAfter is not null?
            knownBefore.Select((value,axis)=>Math.Min(value,knownAfter[axis])).ToArray():null,maxima);
        for(int axis=0;axis<values.Count;axis++)
        {
            var label=values[axis];label.Visible=!empty;
            if(before?[axis] is not int previous||after?[axis] is not int next){label.Text=unknown;continue;}
            if(previous<=0||previous==next)label.Text=Number(next);
            else if(previous<next)label.SetRuns(new(Number(previous)),
                new("+"+Number(unchecked(next-previous)),increaseColour));
            else label.SetRuns(new(Number(previous)),new("-"+Number(unchecked(previous-next)),decreaseColour));
        }
    }

    private static int[]? Complete(IReadOnlyList<int?>? axes)=>axes is null||axes.Any(value=>value is null)?null:
        axes.Select(value=>value!.Value).ToArray();
    private static int?[]? Axes(DeploymentCombatAxes? axes)=>axes is null?null:
        [axes.Mobility,axes.FieldAttack,axes.SiegeTroopAttack,axes.WallAttack,axes.Defense];
    private static string Number(int value)=>value.ToString(CultureInfo.InvariantCulture);
}

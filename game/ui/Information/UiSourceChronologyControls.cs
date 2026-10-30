using Godot;
using San14.Data;

internal partial class UiSourceChronologyPlaybackButton : UiSourceButton
{
    private Rect2 hit;
    private UiSourceLayout source=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private Control? icon;
    private string? iconName;
    public override bool _HasPoint(Vector2 point)=>SourceInputAdmitted&&hit.HasPoint(point);
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,string sprite,Action action)
    {
        this.assets=assets;this.shader=shader;
        source=new(MapMarkerLayout.LoadNamed(repository,"11_05_00_history_offset_01"));
        Size=source.Rectangle(0).Size;hit=source.Rectangle(1);ActionMode=ActionModeEnum.Release;
        int[][] art=[[3],[5,6],[8,9],[11],[13,14]];
        var states=new Control[5];
        for(int state=0;state<5;state++)
        {
            var layer=new Control{MouseFilter=MouseFilterEnum.Ignore};AddChild(layer);states[state]=layer;
            foreach(int node in art[state])source.Art(layer,node,source.Rectangle(node),assets,shader);
        }
        ApplyIcon(sprite);ConfigureStates(states);
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)action();};
    }
    internal void ApplyIcon(string sprite)
    {
        if(iconName==sprite)return;iconName=sprite;
        if(icon is { } previous){previous.Hide();previous.QueueFree();}
        // History uses both Common and PK Common icons. The replacement's
        // declaration owns its atlas; the authored placeholder does not.
        string? pack=sprite is "000_cmn_icon_vew_old" or "000_cmn_icon_vew_onoff"
            ? "texture_pk_common" : null;
        icon=source.Art(this,15,source.Rectangle(15),assets,shader,
            commonSpriteOverride:sprite,commonTexturePack:pack);
    }
}

internal partial class UiSourceChronologyRow : UiSourceButton
{
    private readonly Dictionary<int,UiSourceText> fields=[];
    private Control flag=null!,family=null!;
    private MapSpritePalettes palettes=null!;
    internal int BoundIndex {get;private set;}=-1;
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font)
    {
        var source=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"11_05_00_history_offset_00"));
        Size=source.Rectangle(0).Size;palettes=MapSpritePalettes.Create();
        int[][] art=[[4],[6,7],[9,10],[12],[14,15]];
        var states=new Control[5];
        for(int state=0;state<5;state++)
        {
            var layer=new Control{MouseFilter=MouseFilterEnum.Ignore};AddChild(layer);states[state]=layer;
            foreach(int node in art[state])source.Art(layer,node,source.Rectangle(node),assets,shader);
        }
        flag=source.Art(this,21,source.Rectangle(21),assets,shader);
        family=source.Art(this,24,source.Rectangle(24),assets,shader);
        foreach(int node in new[]{17,18,19,22,23})fields[node]=source.Text(this,node,default,"",font);
        var presets=MapTextPresets.Create();
        ConfigureStates(states,state=>
        {
            // The five source behavior callbacks explicitly select8..12.
            foreach(int node in new[]{17,18,19,23})
                fields[node].Configure(font,MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri).WithPreset(presets[8+state]));
        });
    }
    internal void Apply(int index,ChronologyInformationRow row,string unknown)
    {
        BoundIndex=index;
        fields[17].Text=row.YearText??unknown;fields[18].Text=row.MonthText??unknown;fields[19].Text=row.PeriodText??unknown;
        fields[23].SetKnownColourLine(row.Narrative??unknown);
        flag.Visible=fields[22].Visible=row.ShowForceMarker;family.Visible=row.ShowFamilyMarker;
        if(row.ShowForceMarker)
        {
            var fill=MapSpritePalettes.ForceFill(row.ColourIndex);
            foreach(var sprite in flag.GetChildren().OfType<UiSourceSprite>())sprite.SetColors(new(fill,fill,fill,fill));
            fields[22].SetRuns(new UiSourceTextRun(row.MainLabel,palettes.ForceLetter(row.ColourIndex).CpuPacked));
        }
    }
}

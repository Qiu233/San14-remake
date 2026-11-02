using Godot;
using San14.Data;

/// <summary>One card's three source waiting rings and bank10 masked face.</summary>
internal partial class MapCommanderGauge : Control
{
    private readonly UiSourceRadialGauge[] rings=new UiSourceRadialGauge[3];
    private UiSourceLayout source=null!;
    private UiSourcePortrait? portrait;
    private (string Pack,string Sprite)? shownPortrait;
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private int portraitVersion=-1;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader)
    {
        this.repository=repository;this.assets=assets;this.shader=shader;
        source=new(repository,0x3C47C086);Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;
        source.Art(this,1,source.Rectangle(1),assets,shader);
        var palettes=MapSpritePalettes.Create();
        for(int i=0;i<rings.Length;i++)
        {
            var ring=new UiSourceRadialGauge {Name="CommanderCharge"+i};AddChild(ring);
            ring.Configure(source,assets,shader,"gauge_"+(i+1)+"_ani",3+i*3,4+i*3,false);
            ring.SetColors(palettes[22+i]);rings[i]=ring;
        }
        // Source Lv nodes11/12 have no current setter evidence. Do not publish
        // their designer example as an actual level.
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }

    internal void Apply(IReadOnlyList<int?> frames,OfficerPortraitRoute? route)
    {
        if(portraitVersion!=repository.PortraitResourceVersion)
        {
            shownPortrait=null;portraitVersion=repository.PortraitResourceVersion;
            if(portrait is not null){RemoveChild(portrait);portrait.QueueFree();portrait=null;}
        }
        for(int i=0;i<rings.Length;i++)
        {
            int? frame=i<frames.Count?frames[i]:null;
            rings[i].Visible=frame.HasValue;
            if(frame is int value)rings[i].ApplySourceFrame(rings[i].StartFrame+value);
        }
        var key=route is{Kind:OfficerPortraitRouteKind.StockNamed}
            ?((string Pack,string Sprite)?)(route.ResourceName!,route.SpriteName!):null;
        if(key!=shownPortrait)
        {
            if(portrait is not null){RemoveChild(portrait);portrait.QueueFree();portrait=null;}
            shownPortrait=key;
            if(key is {} named)
            {
                var image=assets.Portraits.RequireSource(named.Pack,named.Sprite);
                Rect2 pane=source.Rectangle(13);
                portrait=new UiSourcePortrait {Name="CommanderPortrait",Position=pane.Position,Size=pane.Size};AddChild(portrait);
                portrait.ConfigureLayoutPane(repository,assets,shader,image,source,13);
            }
        }
        SetMeta("portrait_route_known",route is not null);
        SetMeta("portrait_route_kind",route?.Kind.ToString()??"");
    }
}

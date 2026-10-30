using Godot;
using San14.Data;
using System.Text;

/// <summary>The original Object information root and its two owned children.
/// Its position is supplied only by an accepted native SourceUnit.</summary>
internal partial class FacilityMapInfoView : Control
{
    private FacilityInfoChild textChild=null!;
    private FacilityInfoChild? iconChild;
    private Rect2 textHit,iconHit;
    private FacilityMapInfo info=null!;
    internal int ReceiverId=>info.ReceiverId;
    internal int HexId=>info.HexId;
    internal int DefinitionKind=>info.DefinitionKind;
    internal int DefinitionCategory=>info.DefinitionCategory;
    internal string FacilityName=>info.Name;
    internal int? ForceColor49=>info.ForceColor49;
    internal ArmySimpleOutlineSelection? Outline=>info.Outline;
    internal float ProjectionLeftOffset=>iconChild is null?0:iconChild.Size.X*.5f+iconChild.Position.X;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        MapTextPresets textPresets,MapSpritePalettes palettes,CityGateMapMarkerSources bars,
        MapMarkerSlices barBackground,FacilityMapInfo value,string? fillSprite,string? outlineSprite)
    {
        Name="FacilityInfo"+value.ReceiverId;MouseFilter=MouseFilterEnum.Ignore;info=value;
        var root=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"00_81_07_building_flag"));
        Size=root.Rectangle(0).Size;
        textChild=new() {Name="ObjectText",Position=root.Rectangle(2).Position};AddChild(textChild);
        textChild.Configure(repository,assets,shader,font,textPresets,palettes,bars,barBackground,value,false,null,null);
        textHit=textChild.HitRectangle;
        if(value.DefinitionCategory!=3)
        {
            iconChild=new() {Name="ObjectIcon",Position=root.Rectangle(13).Position};AddChild(iconChild);
            iconChild.Configure(repository,assets,shader,font,textPresets,palettes,bars,barBackground,value,true,
                fillSprite,outlineSprite);
            iconHit=iconChild.HitRectangle;
        }
        SetMeta("source_layout",root.Source.Source.ResourceUri);
        SetMeta("source_Object_receiver",value.ReceiverId);
        SetMeta("source_world_height_offset",900);
        Hide();
    }

    internal void Apply(FacilityMapInfo value)
    { info=value;textChild.Apply(value);iconChild?.Apply(value); }

    internal void Present(int mode,byte textAlpha,byte iconAlpha,bool hover,bool pressed)
    {
        int state=pressed?2:hover?1:0;
        textChild.Visible=mode==0;
        textChild.ApplyInteraction(state);textChild.Modulate=new(1,1,1,hover||pressed?1:textAlpha/255f);
        if(iconChild is { } icon)
        { icon.Visible=mode!=-1;icon.ApplyInteraction(state);icon.Modulate=new(1,1,1,hover||pressed?1:iconAlpha/255f); }
    }

    internal bool HasPoint(Vector2 point,int mode)
    {
        if(!Visible||mode<0)return false;
        Rect2 box=mode==0?new(textHit.Position+textChild.Position,textHit.Size):
            iconChild is { } icon?new(iconHit.Position+icon.Position,iconHit.Size):default;
        return box.HasArea()&&point.X>=box.Position.X&&point.Y>=box.Position.Y&&point.X<=box.End.X&&point.Y<=box.End.Y;
    }
}

/// <summary>Authored normal/focus/push/disabled clips sampled at their settled
/// ends. Animation timing is deferred; sprite identity and pane geometry are not.</summary>
internal partial class FacilityInfoChild : UiSourceAnimationView
{
    private UiSourceText? caption;
    private MapFont? font;
    private MapTextPresets presets=null!;
    private MapFontProfile? captionProfile;
    private FacilityInfoGauge? gauge;
    private int interaction=-1;
    private bool activated;
    private readonly List<UiSourceSprite> forceSprites=[];
    private readonly List<UiSourceSprite> outlineSprites=[];
    private readonly List<UiSourceSprite> disabledOutlineSprites=[];
    private MapSpritePalettes palettes=null!;
    internal Rect2 HitRectangle {get;private set;}

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        MapTextPresets textPresets,MapSpritePalettes palettes,CityGateMapMarkerSources bars,
        MapMarkerSlices barBackground,FacilityMapInfo value,bool icon,string? fillSprite,string? outlineSprite)
    {
        this.font=font;presets=textPresets;this.palettes=palettes;activated=value.Activated;AutomaticAnimationClock=false;
        var loaded=MapMarkerLayout.LoadNamed(repository,icon?"00_81_07_btn_building_offset_00":"00_81_07_btn_building_offset_01");
        var raw=new UiSourceLayout(loaded);
        Dictionary<int,UiSourceNodeGeometry>? geometry=null;
        if(!icon)
        {
            // AC35B0 counts source full-width units. The authored24px cell
            // is extended by the actual name, preserving each source left edge.
            int units=value.Name.EnumerateRunes().Sum(r=>r.Value<0x80?1:2);
            float width=raw.Nodes[23].Value44*units/2f;
            float delta=width-raw.Nodes[23].Value44;
            geometry=new() { [23]=new(Size:new(width,raw.Nodes[23].Value46)) };
            foreach(int i in new[]{1,6,7,13,14})geometry[i]=new(Size:new(raw.Nodes[i].Value44+delta,raw.Nodes[i].Value46));
            if(value.DefinitionCategory==3)
            {
                float shift=raw.Nodes[3].Value44;
                foreach(int i in new[]{23,6,7,13,14})
                {
                    var current=geometry.GetValueOrDefault(i)??new();
                    geometry[i]=current with {Offset=new(raw.Nodes[i].HeaderValues[0]-shift,-raw.Nodes[i].HeaderValues[1])};
                }
                geometry[1]=geometry[1] with {Size=new(geometry[1].Size!.Value.X-shift,raw.Nodes[1].Value46)};
            }
        }
        var source=new UiSourceLayout(loaded,geometry);
        ConfigureAnimation(source,new HashSet<string>{"0_normal","1_focus","2_push","3_disable"},source.Nodes.Select(n=>n.Index));
        HitRectangle=source.Rectangle(1);
        int[] hidden=icon?[19,20,21]:[3,4,8,9,10,11,15,16,17,18,20,21,25,26];
        foreach(int index in hidden)HiddenAnimationPanes.Add(index);
        foreach(var node in source.Nodes)
        {
            if(node.Type is not (1 or 3)||hidden.Contains(node.Index))continue;
            string? replacement=icon?(node.Index is 4 or 8 or 9 or 13 or 14 or 17?fillSprite:outlineSprite):null;
            MapCornerColors? colors=node.Index is 6 or 7 or 8 or 9?palettes[5]:
                node.Index is 11 or 12 or 13 or 14?palettes[6]:null;
            Vector2 artSize=geometry?.GetValueOrDefault(node.Index)?.Size??new(node.Value44,node.Value46);
            var art=source.Art(AnimationPanes[node.Index],node.Index,new(Vector2.Zero,artSize),
                assets,shader,colors,commonSpriteOverride:replacement,
                expansionCommonSprite:icon&&value.DefinitionKind is 15 or 17,localCoordinates:true);
            if(icon&&node.Index is 4 or 17)
                forceSprites.AddRange(art.GetChildren().OfType<UiSourceSprite>());
            if(icon&&node.Index==3)outlineSprites.AddRange(art.GetChildren().OfType<UiSourceSprite>());
            if(icon&&node.Index==16)disabledOutlineSprites.AddRange(art.GetChildren().OfType<UiSourceSprite>());
        }
        if(!icon)
        {
            var host=new Control {MouseFilter=MouseFilterEnum.Ignore};AnimationPanes[23].AddChild(host);
            captionProfile=MapFontProfile.FromNode(source.Nodes[23],source.Source.Source.ResourceUri);
            caption=source.Text(host,23,default,value.Name,font,rectangle:new(Vector2.Zero,geometry![23].Size!.Value));
            gauge=new() {Size=source.Rectangle(24).Size,MouseFilter=MouseFilterEnum.Ignore};AnimationPanes[24].AddChild(gauge);
            gauge.Configure(bars,barBackground,assets,shader,palettes);
        }
        Apply(value);ApplyInteraction(0);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("source_clip_animation_deferred",true);
    }

    internal void Apply(FacilityMapInfo value)
    {
        bool changed=activated!=value.Activated;activated=value.Activated;
        if(caption is { } text)text.Text=value.Name;
        var fill=MapSpritePalettes.ForceFill(value.ForceColor49??60);
        foreach(var sprite in forceSprites)sprite.SetColors(new(fill,fill,fill,fill));
        if(value.Outline is { } outline)
        {
            ApplyOutline(outline.Primary,outlineSprites);
            ApplyOutline(outline.Disabled,disabledOutlineSprites);
        }
        gauge?.Apply(value.Durability,value.MaximumDurability);
        if(changed&&interaction>=0)ApplyCaption(interaction);
    }

    private void ApplyOutline(ArmySimpleOutlineColorChoice choice,IEnumerable<UiSourceSprite> sprites)
    {
        if(choice.Kind==ArmySimpleOutlineColorKind.Preserve)return;
        MapCornerColors color=choice.Kind switch
        {
            ArmySimpleOutlineColorKind.ForceFill=>Solid(MapSpritePalettes.ForceFill((int)choice.Value)),
            ArmySimpleOutlineColorKind.SpritePalette=>palettes[(int)choice.Value],
            ArmySimpleOutlineColorKind.PackedLiteral=>Solid(new(choice.Value,choice.Value)),
            _=>throw new InvalidDataException("facility.info.unexpected_player_District_outline")
        };
        if(choice.RgbMultiplier!=1)color=new(Darken(color.LT),Darken(color.RT),Darken(color.LB),Darken(color.RB));
        foreach(var sprite in sprites)sprite.SetColors(color);
        MapCornerColors Solid(AreaMarkerColor c)=>new(c,c,c,c);
        AreaMarkerColor Darken(AreaMarkerColor c)
        {
            uint packed=(uint)((byte)(c.AuthoredRed*choice.RgbMultiplier)|
                (byte)(c.AuthoredGreen*choice.RgbMultiplier)<<8|(byte)(c.AuthoredBlue*choice.RgbMultiplier)<<16|c.AuthoredAlpha<<24);
            return MapSpritePalettes.FromAuthored(packed);
        }
    }

    internal void ApplyInteraction(int state)
    {
        if(interaction==state)return;interaction=state;
        ApplyAnimationAt(state switch {1=>"1_focus",2=>"2_push",3=>"3_disable",_=>"0_normal"},1e6);
        ApplyCaption(state);
    }
    private void ApplyCaption(int state)
    {
        if(caption is null)return;
        int preset=state switch {1=>4,2=>5,3=>23,_=>activated?19:75};
        caption.Configure(font!,captionProfile!.WithPreset(presets[preset]));caption.QueueRedraw();
    }
}

internal partial class FacilityInfoGauge : Control
{
    private CityGateMapMarkerSources sources=null!;
    private MapMarkerSlices background=null!;
    private MapSpritePalettes palettes=null!;
    private UiSourceSprite fill=null!;
    private int current,capacity;
    private int? retainedWidth;
    internal void Configure(CityGateMapMarkerSources sources,MapMarkerSlices background,UiAssets assets,
        Shader shader,MapSpritePalettes palettes)
    {
        this.sources=sources;this.background=background;this.palettes=palettes;
        fill=new();AddChild(fill);fill.Configure(shader,assets.SrgbSprite("000_cmn_bar"),UiSourceSprite.Blend.Ordinary);
    }
    internal void Apply(int value,int maximum)
    {
        current=value;capacity=maximum;Visible=value<maximum;
        if(!Visible)return;
        var geometry=sources.BarGeometry(current,capacity,new(0,0,Size.X,Size.Y),retainedWidth);
        retainedWidth=geometry.SourceFillWidth;
        fill.Position=new(geometry.Fill.Left,geometry.Fill.Top);fill.Size=new(geometry.Fill.Width,geometry.Fill.Height);
        fill.Visible=geometry.SourceFillWidth>0;
        int style=current>palettes.DurabilityStylePercent*(float)capacity/100f?1:2;
        fill.SetColors(palettes[style]);QueueRedraw();
    }
    public override void _Draw()
    { if(Visible)background.Draw(this,new(Vector2.Zero,Size),Colors.White,Size.X/sources.Bar.Nodes[0].Value44,Size.Y/sources.Bar.Nodes[0].Value46); }
}

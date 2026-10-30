using Godot;
using San14.Data;

/// <summary>The force page's flags-only child. It borrows the page's font,
/// textures and shader; every sprite retains its own material. Map projection
/// and actual Force affiliation remain with the map/page publication owner.</summary>
internal partial class NewGameForceMapFlag : UiSourceAnimationView
{
    private UiSourceText letter = null!;
    private MapFont font = null!;
    private MapFontProfile letterProfile = null!, currentLetterProfile = null!;
    private MapTextPresets textStyles = null!;
    private MapSpritePalettes spriteStyles = null!;
    private readonly Dictionary<string,double> endTimes = [];
    private int forceColor, state = -1;
    internal Rect2 SourceHitRectangle { get; private set; }

    internal void Configure(MapMarkerLayout definition, UiAssets assets, Shader shader, MapFont font,
        int actualForceColor49, string actualForceLetter, bool forceValid,
        ArmySimpleOutlineSelection actualOutline, bool titleSpecialForce)
    {
        this.font=font;forceColor=actualForceColor49;
        textStyles=MapTextPresets.Create();spriteStyles=MapSpritePalettes.Create();
        var layout=new UiSourceLayout(new LoadedLayout(definition.Layout,definition.Source));
        string[] names=["0_normal","1_focus","2_push","3_disable"];
        foreach(var clip in LcskAnimationReader.Read(definition.Layout,names.ToHashSet()))
            endTimes.Add(clip.Name,clip.DurationSeconds);
        AutomaticAnimationClock=false;
        ConfigureAnimation(layout,names.ToHashSet(),layout.Nodes.Select(node=>node.Index));
        SetProcess(false);SourceHitRectangle=layout.Rectangle(1);
        // 1A09F0 hides the independent lock art before the normal title page.
        HiddenAnimationPanes.Add(19);
        var actualNormalOutline=ResolveOutline(actualOutline.Primary);
        var actualDisabledOutline=ResolveOutline(actualOutline.Disabled);
        string actualOutlineSprite=actualOutline.UsesOutline?TitleForceFlagStyleSource.HighlightOutlineSprite:
            TitleForceFlagStyleSource.NormalOutlineSprite;
        AreaMarkerColor fill=forceValid?MapSpritePalettes.ForceFill(forceColor):MapSpritePalettes.FromAuthored(0xFFFFFFFF);
        AreaMarkerColor disabled=Dim(fill);
        if(titleSpecialForce)fill=disabled=Dim(MapSpritePalettes.FromAuthored(0x80FFFFFF));
        foreach(var node in layout.Nodes.Where(node=>node.Type is 1 or 3))
        {
            MapCornerColors? colors=node.Index switch
            {
                4 => Solid(fill),17 => Solid(disabled),3 => actualNormalOutline,16 => actualDisabledOutline,
                >=6 and <=9 => spriteStyles[5],>=11 and <=14 => spriteStyles[6],_ => null
            };
            string? outline=node.Index is 3 or 6 or 7 or 11 or 12 or 16 ? actualOutlineSprite:null;
            layout.Art(AnimationPanes[node.Index],node.Index,
                new(Vector2.Zero,new(node.Value44,node.Value46)),assets,shader,
                colors:colors,commonSpriteOverride:outline,localCoordinates:true);
        }
        var text=layout.Nodes[18];
        letterProfile=currentLetterProfile=MapFontProfile.FromNode(text,definition.Source.ResourceUri);
        letter=new UiSourceText { Name=text.Name,Size=new(text.Value44,text.Value46) };
        AnimationPanes[18].AddChild(letter);
        letter.Configure(font,letterProfile);letter.Text=actualForceLetter;
        SetState(0);
        SetMeta("source_layout",definition.Source.ResourceUri);
        SetMeta("source_force_map_mode",2);
    }

    internal void ApplyInteraction(bool selected,bool hovered,bool enabled)
        => SetState(!enabled?4:selected?2:hovered?1:0);

    private void SetState(int next)
    {
        if(state==next)return;state=next;
        // UI animation is deferred; sample the authored terminal state rather
        // than inventing visibility groups or substituting normal map labels.
        string clip=next switch {0=>"0_normal",1=>"1_focus",2=>"2_push",4=>"3_disable",
            _=>throw new InvalidDataException("Unsupported force flag state.")};
        ApplyAnimationAt(clip,endTimes[clip]);
        var normalLetter=spriteStyles.ForceLetter(forceColor is 41 or 45 or 46 or 48 or 49?45:forceColor);
        currentLetterProfile=next switch
        {
            1=>letterProfile.WithPreset(textStyles[4]),
            2=>letterProfile.WithPreset(textStyles[5]),
            _=>WithColour(currentLetterProfile,next==4?Dim(normalLetter):normalLetter)
        };
        letter.Configure(font,currentLetterProfile);
    }

    private MapCornerColors ResolveOutline(ArmySimpleOutlineColorChoice choice)
    {
        var colors=choice.Kind switch
        {
            ArmySimpleOutlineColorKind.SpritePalette=>spriteStyles[(int)choice.Value],
            ArmySimpleOutlineColorKind.PackedLiteral=>Solid(new(choice.Value,choice.Value)),
            _=>throw new InvalidDataException("Unsupported normal static title outline owner.")
        };
        return choice.RgbMultiplier==.4f?new(Dim(colors.LT),Dim(colors.RT),Dim(colors.LB),Dim(colors.RB)):colors;
    }

    private static MapFontProfile WithColour(MapFontProfile profile,AreaMarkerColor color)
        => profile with { ColourLT=color.CpuPacked,ColourRT=color.CpuPacked,
            ColourLB=color.CpuPacked,ColourRB=color.CpuPacked };
    private static MapCornerColors Solid(AreaMarkerColor color)=>new(color,color,color,color);
    private static AreaMarkerColor Dim(AreaMarkerColor color)=>new(DimPacked(color.AuthoredPacked),DimPacked(color.CpuPacked));
    private static uint DimPacked(uint color)=>(color&0xFF000000u)|(uint)(int)((byte)color*.4f)|
        (uint)(int)((byte)(color>>8)*.4f)<<8|(uint)(int)((byte)(color>>16)*.4f)<<16;
}

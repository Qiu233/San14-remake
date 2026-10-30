using Godot;
using San14.Data;

/// <summary>The actual diplomacy flag owner and its replacement child layouts.
/// Its caller projects the current capital; no world is queried while drawing.</summary>
internal partial class MapDiplomacyCardView : Control
{
    private readonly List<Control> states=[];
    private readonly List<Control> flagStates=[];
    private readonly List<Control> relationshipTags=[];
    private readonly List<(Control Ink,UiSourceText Text)> intelligenceTags=[];
    internal void SetIntelligenceForceMarkers(bool spy,IReadOnlyList<MapDiplomacyMarker>? markers,string? unresolved)
    {
        if(markers?.Count>3)throw new InvalidDataException("Intelligence map tag capacity exceeded.");
        foreach(var tag in relationshipTags)tag.Visible=!spy;
        for(int slot=0;slot<intelligenceTags.Count;slot++)
        {
            var tag=intelligenceTags[slot];tag.Ink.Visible=spy&&slot<(markers?.Count??0);
            tag.Text.Text=slot<(markers?.Count??0)?markers![slot].Text:"";
        }
        if(spy&&unresolved is not null)SetMeta("intelligence_spy_marker_missing",unresolved);
        else if(HasMeta("intelligence_spy_marker_missing"))RemoveMeta("intelligence_spy_marker_missing");
        SourceOrderChanged?.Invoke();
    }
    private UiSourcePortrait? portrait;
    private UiSourceText? name;
    private UiSourceText? letter;
    private MapFontProfile? nameProfile,letterProfile;
    private MapFont font=null!;
    private MapTextPresets presets=null!;
    private Control detail=null!,flag=null!;
    private TextureButton? hit;
    private AreaMarkerColor letterColour;
    private int display=int.MinValue,interaction=-1;
    private bool configured;
    private bool sourceAdmitted=true;
    private Action? selectionAction;
    internal void SetSelectionAction(bool? enabled,Action? action)
    {
        selectionAction=action;SetSelectionAdmission(enabled);
    }
    internal void SetSelectionAdmission(bool? enabled)
    {
        if(hit is null)return;
        bool disabled=enabled==false;
        if(hit.Disabled==disabled){if(disabled)SetInteraction(4);return;}
        hit.Disabled=disabled;SetInteraction(disabled?4:0);
    }
    private readonly List<(Control Ink,int KeyOffset)> sourceDrawingParts=[];
    internal IReadOnlyList<(Control Ink,int KeyOffset)> SourceDrawingParts=>sourceDrawingParts;
    internal Func<Vector2,bool>? SourceHitAdmitted;
    private sealed partial class DiplomacyHitButton(MapDiplomacyCardView owner):TextureButton
    {
        public override bool _HasPoint(Vector2 point)
        {
            Vector2 cursor=owner.Position+Position+point;
            return owner.ContainsCanonicalPoint(cursor)&&(owner.SourceHitAdmitted?.Invoke(cursor)??false);
        }
    }
    internal event Action? SourceOrderChanged;
    internal uint SourceInteractionFlags=>(hit?.IsHovered()==true?0x40000000u:0u)|
        (hit?.ButtonPressed==true?0x80000000u:0u);
    internal void SetSourceAdmitted(bool value)
    {
        sourceAdmitted=value;
        bool visible=configured&&sourceAdmitted&&display!=-1;
        if(Visible==visible)return;
        Visible=visible;SourceOrderChanged?.Invoke();
    }
    internal Rect2? SourceHitRectangle => Visible&&hit is {Disabled:false}?new Rect2(Position+hit.Position,hit.Size):null;
    internal bool ContainsCanonicalPoint(Vector2 cursor)
    {
        if(!Visible||hit is null||hit.Disabled)return false;
        var p=Position+hit.Position;var end=p+hit.Size;
        return cursor.X>=p.X&&cursor.Y>=p.Y&&cursor.X<=end.X&&cursor.Y<=end.Y;
    }
    internal Vector2 ProjectionOffset { get; private set; }

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        MapDiplomacyLayouts resources,MapDiplomacyInformation publication)
    {
        this.font=font;presets=resources.Presets;
        MouseFilter=MouseFilterEnum.Ignore;
        Size=resources.Parent.Rectangle(0).Size;
        var cityMount=resources.Parent.Rectangle(85);
        ProjectionOffset=new(-(int)(cityMount.Size.X/2+cityMount.Position.X),-40);
        if(publication.IsCurrentPlayerForce is not bool own||publication.Markers is null||publication.ForceName is null)
        { SetMeta("unresolved_input",publication.UnresolvedInput??"map-diplomacy.current-fields");Hide();return; }
        var loaded=own?resources.WithName:resources.NoName;
        var source=new UiSourceLayout(loaded);
        if(own)
        {
            var profile=MapFontProfile.FromNode(source.Nodes[23],loaded.Source.ResourceUri).WithPreset(presets[0]);
            int delta=Math.Clamp((int)font.Measure(publication.ForceName,profile).Width-(int)(profile.ScaleX*128),0,60);
            var geometry=new Dictionary<int,UiSourceNodeGeometry>();
            foreach(int node in new[]{1,4,6,7,9,10,12,23})
                geometry[node]=new(Size:new(source.Nodes[node].Value44+delta,source.Nodes[node].Value46));
            var nameOffset=source.Nodes[23].HeaderValues;
            geometry[23]=geometry[23] with{Offset=new(nameOffset[0]+delta/2f,-nameOffset[1])};
            var marker=source.Nodes[14].HeaderValues;
            geometry[14]=new(Offset:new(marker[0]+delta,-marker[1]));
            source=new(loaded,geometry);
        }
        detail=new(){Name=own?"DiplomacyWithName":"DiplomacyNoName",Position=resources.Parent.Rectangle(own?2:44).Position,
            Size=source.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};AddChild(detail);
        for(int i=0;i<4;i++)
        {
            var group=new Control{Name="State"+i,MouseFilter=MouseFilterEnum.Ignore};detail.AddChild(group);states.Add(group);
        }
        source.Art(states[0],4,source.Rectangle(4),assets,shader,resources.Palettes[4]);
        foreach(int n in new[]{6,7})source.Art(states[1],n,source.Rectangle(n),assets,shader,resources.Palettes[5]);
        foreach(int n in new[]{9,10})source.Art(states[2],n,source.Rectangle(n),assets,shader,resources.Palettes[6]);
        source.Art(states[3],12,source.Rectangle(12),assets,shader,Darken(resources.Palettes[4]));
        if(publication.Portrait is{Kind:OfficerPortraitRouteKind.StockNamed,ResourceName:{} pack,SpriteName:{} sprite})
        {
            var box=source.Rectangle(2);
            portrait=new(){Name="DiplomacyRulerPortrait",Position=box.Position,Size=box.Size};detail.AddChild(portrait);
            portrait.ConfigureLayoutPane(repository,assets,shader,new LocalizedUiSpriteSource(repository).LoadTexturePackSprite(pack,sprite),source,2);
        }
        else SetMeta("unresolved_portrait",publication.Portrait?.UnresolvedInput??"map-diplomacy.portrait-route");
        if(own)
        {
            nameProfile=MapFontProfile.FromNode(source.Nodes[23],source.Source.Source.ResourceUri);
            name=source.Text(detail,23,default,publication.ForceName,font,presets[0]);name.Name="DiplomacyForceName";
        }
        sourceDrawingParts.Add((detail,own?5:1));
        int slot=0;
        foreach(var row in publication.Markers)
        {
            if(slot>=3)throw new InvalidDataException("map-diplomacy.marker-capacity");
            var box=source.Rectangle(new[]{14,17,20}[slot]);
            var marker=new Control{Name="DiplomacyMarker"+slot,Position=box.Position,Size=resources.Marker.Rectangle(0).Size,
                Scale=box.Size/resources.Marker.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};
            marker.Position+=detail.Position;AddChild(marker);
            resources.Marker.Art(marker,2,resources.Marker.Rectangle(2),assets,shader,commonSpriteOverride:row.BaseSpriteName);
            resources.Marker.Text(marker,3,default,row.Text,font,presets[0]);
            relationshipTags.Add(marker);sourceDrawingParts.Add((marker,(own?6:2)+slot));slot++;
        }
        // The original manager reuses three tag slots. Reserve the alternate
        // source221 artwork once so toggling only publishes labels/visibility.
        string spyBackground=ArmyMapUiSources.Create().IconName(new(0x221,0));
        for(int i=0;i<3;i++)
        {
            var box=source.Rectangle(new[]{14,17,20}[i]);
            var tag=new Control{Name="IntelligenceMarker"+i,Position=box.Position+detail.Position,
                Size=resources.Marker.Rectangle(0).Size,Scale=box.Size/resources.Marker.Rectangle(0).Size,
                MouseFilter=MouseFilterEnum.Ignore,Visible=false};AddChild(tag);
            resources.Marker.Art(tag,2,resources.Marker.Rectangle(2),assets,shader,commonSpriteOverride:spyBackground);
            var caption=resources.Marker.Text(tag,3,default,"",font,presets[0]);
            intelligenceTags.Add((tag,caption));sourceDrawingParts.Add((tag,(own?6:2)+i));
        }
        var hitBox=source.Rectangle(1);
        hit=new DiplomacyHitButton(this){Name="DiplomacyHitArea",Position=detail.Position+hitBox.Position,Size=hitBox.Size,
            IgnoreTextureSize=true,FocusMode=FocusModeEnum.None,MouseFilter=MouseFilterEnum.Stop};AddChild(hit);
        hit.MouseEntered+=()=>SetInteraction(1);
        hit.MouseExited+=()=>SetInteraction(0);
        hit.ButtonDown+=()=>SetInteraction(2);
        hit.ButtonUp+=()=>SetInteraction(hit.IsHovered()?1:0);
        // The source child action toggles its portrait pane; it does not
        // issue a world command or open an unrelated force-details page.
        hit.Pressed+=()=>{if(selectionAction is {} choose)choose();else if(portrait is not null)portrait.Visible=!portrait.Visible;};
        BuildFlag(repository,assets,shader,resources,publication,cityMount.Position);
        sourceDrawingParts.Add((flag,9));
        configured=true;SetInteraction(0);SetDisplay(-1);
    }

    private void BuildFlag(GameResourceRepository repository,UiAssets assets,Shader shader,
        MapDiplomacyLayouts resources,MapDiplomacyInformation publication,Vector2 position)
    {
        flag=new(){Name="DiplomacyCityForceFlag",Position=position,Size=resources.Flag.Rectangle(0).Size,
            MouseFilter=MouseFilterEnum.Ignore};AddChild(flag);
        if(publication.Flag is not{} appearance||publication.FlagOutline is not{} outline)
        {flag.Hide();SetMeta("unresolved_flag",publication.UnresolvedInput??"map-diplomacy.flag-inputs");return;}
        for(int i=0;i<4;i++)
        {var group=new Control{Name="FlagState"+i,MouseFilter=MouseFilterEnum.Ignore};flag.AddChild(group);flagStates.Add(group);}
        string edge=outline.UsesOutline?"000_cmn_prt_flag_default_outline":"000_cmn_prt_flag_default_base";
        var primary=Materialize(outline.Primary,resources.Palettes);
        var inactive=Materialize(outline.Disabled,resources.Palettes);
        var fill=Solid(appearance.Fill);
        resources.Flag.Art(flagStates[0],3,resources.Flag.Rectangle(3),assets,shader,primary,commonSpriteOverride:edge);
        resources.Flag.Art(flagStates[0],4,resources.Flag.Rectangle(4),assets,shader,fill);
        foreach(int n in new[]{6,7,8,9})resources.Flag.Art(flagStates[1],n,resources.Flag.Rectangle(n),assets,shader,
            resources.Palettes[5],commonSpriteOverride:n is 6 or 7?edge:null);
        foreach(int n in new[]{11,12,13,14})resources.Flag.Art(flagStates[2],n,resources.Flag.Rectangle(n),assets,shader,
            resources.Palettes[6],commonSpriteOverride:n is 11 or 12?edge:null);
        resources.Flag.Art(flagStates[3],16,resources.Flag.Rectangle(16),assets,shader,inactive,commonSpriteOverride:edge);
        resources.Flag.Art(flagStates[3],17,resources.Flag.Rectangle(17),assets,shader,Darken(fill));
        letterColour=appearance.Letter;
        letterProfile=MapFontProfile.FromNode(resources.Flag.Nodes[18],resources.Flag.Source.Source.ResourceUri);
        letter=resources.Flag.Text(flag,18,default,appearance.Glyph,font);letter.Name="DiplomacyFlagLetter";
    }

    internal void SetDisplay(int value)
    {
        bool visible=configured&&sourceAdmitted&&value!=-1;
        bool changed=Visible!=visible;Visible=visible;
        if(changed)SourceOrderChanged?.Invoke();
        if(display==value)return;
        display=value;
        if(portrait is not null)portrait.Visible=value is 0 or 1;
        SetMeta("source_display",value);
    }
    private void SetInteraction(int value)
    {
        if(interaction==value)return;interaction=value;
        int state=value==4?3:value;
        for(int i=0;i<states.Count;i++)states[i].Visible=i==state;
        for(int i=0;i<flagStates.Count;i++)flagStates[i].Visible=i==state;
        if(name is not null)name.Configure(font,nameProfile!.WithPreset(presets[value switch{0=>0,1=>4,2=>5,_=>6}]));
        if(letter is not null)
        {
            var profile=letterProfile!;
            if(value is 1 or 2)profile=profile.WithPreset(presets[value==1?4:5]);
            else
            {
                uint colour=value==4?AreaMapMarkerSources.DarkenRgb(letterColour.CpuPacked):letterColour.CpuPacked;
                profile=profile with{ColourLT=colour,ColourRT=colour,ColourLB=colour,ColourRB=colour};
            }
            letter.Configure(font,profile);letter.QueueRedraw();
        }
        name?.QueueRedraw();SourceOrderChanged?.Invoke();
    }
    private static MapCornerColors? Materialize(ArmySimpleOutlineColorChoice choice,MapSpritePalettes palettes)
    {
        MapCornerColors? colour=choice.Kind switch
        {
            ArmySimpleOutlineColorKind.Preserve=>null,
            ArmySimpleOutlineColorKind.ForceFill=>Solid(MapSpritePalettes.ForceFill((int)choice.Value)),
            ArmySimpleOutlineColorKind.SpritePalette=>palettes[(int)choice.Value],
            ArmySimpleOutlineColorKind.PackedLiteral=>Solid(new(choice.Value,choice.Value)),
            _=>throw new InvalidDataException("map-diplomacy.configured-outline-profile")
        };
        return colour is{} c&&choice.RgbMultiplier==0.4f?Darken(c):colour;
    }
    private static MapCornerColors Solid(AreaMarkerColor c)=>new(c,c,c,c);
    private static MapCornerColors Darken(MapCornerColors c)=>new(Darken(c.LT),Darken(c.RT),Darken(c.LB),Darken(c.RB));
    private static AreaMarkerColor Darken(AreaMarkerColor c)=>new(AreaMapMarkerSources.DarkenRgb(c.AuthoredPacked),AreaMapMarkerSources.DarkenRgb(c.CpuPacked));
}

internal sealed class MapDiplomacyLayouts(GameResourceRepository repository)
{
    internal UiSourceLayout Parent{get;}=new(MapMarkerLayout.LoadNamed(repository,"00_81_07_diplomacy_flag"));
    internal LoadedLayout NoName{get;}=MapMarkerLayout.LoadNamed(repository,"00_81_07_btn_diplomacy_offset_00");
    internal LoadedLayout WithName{get;}=MapMarkerLayout.LoadNamed(repository,"00_81_07_btn_diplomacy_offset_01");
    internal UiSourceLayout Flag{get;}=new(MapMarkerLayout.LoadNamed(repository,"00_81_07_0110_btn_city_offset_00"));
    internal UiSourceLayout Marker{get;}=new(MapMarkerLayout.LoadNamed(repository,"cmn_mrker"));
    internal MapTextPresets Presets{get;}=MapTextPresets.Create();
    internal MapSpritePalettes Palettes{get;}=MapSpritePalettes.Create();
}

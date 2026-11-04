using System.Collections.Immutable;
using Godot;
using San14.Data;

internal partial class D3D11MapMarkers
{
    // These are the actual retained Canvas drawing inputs, rather than a
    // fingerprint of the World or the mutable marker/controller. Ordinary
    // paint contains no live queries into the accepted presentation owner.
    // Font copies address the viewport, not just the local command geometry.
    // These are their actual layout dependencies. Font still observes their
    // final values at draw time, after any deferred container layout.
    private readonly record struct MarkerPaintPlacement(Transform2D Parent,Rect2 Viewport);
    private sealed record MarkerPaintPacket(MapFont Font,Vector2 Scale,MarkerPaintPlacement Placement,ImmutableArray<MarkerPaint> Commands)
    {
        internal bool SameDrawing(MarkerPaintPacket other)=>
            ReferenceEquals(Font,other.Font)&&Scale==other.Scale&&Placement==other.Placement&&
            Commands.AsSpan().SequenceEqual(other.Commands.AsSpan());

        internal void Draw(CanvasItem canvas)
        {
            var text=Font.BeginDraw(canvas,Scale);
            canvas.DrawSetTransform(Vector2.Zero,0,Scale);
            try {foreach(var command in Commands)command.Draw(canvas,Font);}
            finally {text.End();}
        }
    }

    private abstract record MarkerPaint
    {
        internal abstract void Draw(CanvasItem canvas,MapFont font);
    }
    private sealed record MarkerTexturePaint(Texture2D Texture,Rect2 Rectangle,Color Tint):MarkerPaint
    {
        internal override void Draw(CanvasItem canvas,MapFont font)=>canvas.DrawTextureRect(Texture,Rectangle,false,Tint);
    }
    private sealed record MarkerPalettePaint(Texture2D Texture,Rect2 Rectangle,MapCornerColors Palette,byte Alpha):MarkerPaint
    {
        internal override void Draw(CanvasItem canvas,MapFont font)=>PaletteSprite(canvas,Texture,Rectangle,Palette,Alpha);
    }
    private sealed record MarkerPanelPaint(MapMarkerSlices Panel,Rect2 Rectangle,Color Tint):MarkerPaint
    {
        internal override void Draw(CanvasItem canvas,MapFont font)=>Panel.Draw(canvas,Rectangle,Tint);
    }
    private sealed record MarkerPalettePanelPaint(MapMarkerSlices Panel,Rect2 Rectangle,MapCornerColors Palette,byte Alpha):MarkerPaint
    {
        internal override void Draw(CanvasItem canvas,MapFont font)=>Panel.Draw(canvas,Rectangle,Palette,Alpha);
    }
    // These are distinct original text controls, even when zoom modes emit
    // one of them at the same ordinal formerly occupied by another control.
    private enum MarkerTextSlot { ForceGlyph, LocationName, SimpleTroops, FullTroops }
    private sealed record MarkerTextPaint(string Value,Rect2 Cell,MapFontProfile Profile,
        HorizontalAlignment Alignment,float Width,Color Tint,MarkerTextSlot Slot):MarkerPaint
    {
        internal override void Draw(CanvasItem canvas,MapFont font)=>font.Draw(canvas,Value,
            Cell.Position+new Vector2(0,font.CellBaselineY(Profile,Cell.Size.Y)),Profile,Alignment,Width,Tint,drawingSlot:(int)Slot);
    }
    private enum MarkerBarWidth { Hp,FullMorale,SimpleMorale }
    private sealed record MarkerBarPaint(Marker Target,MarkerBarWidth WidthSlot,MapMarkerSlices Panel,
        Texture2D Fill,MapBarGeometry Geometry,Vector2 Position,Color PanelTint,MapCornerColors Palette,byte Alpha):MarkerPaint
    {
        internal override void Draw(CanvasItem canvas,MapFont font)
        {
            Panel.Draw(canvas,AreaRectangle(Geometry.Background,Position),PanelTint,Geometry.ScaleX,Geometry.ScaleY);
            if(Geometry.SourceFillWidth>0)
                PaletteSprite(canvas,Fill,AreaRectangle(Geometry.Fill,Position),Palette,Alpha);
            // The retained width belongs to this actual draw, exactly as in
            // DrawMarker. Preparing/queuing an unseen packet never stores it.
            switch(WidthSlot)
            {
                case MarkerBarWidth.Hp:Target.HpFillWidth=Geometry.SourceFillWidth;break;
                case MarkerBarWidth.FullMorale:Target.FullMoraleFillWidth=Geometry.SourceFillWidth;break;
                case MarkerBarWidth.SimpleMorale:Target.SimpleMoraleFillWidth=Geometry.SourceFillWidth;break;
            }
        }
    }

    // Compare each current command against the last complete immutable packet.
    // No retained packet is edited: the first difference copies its full prefix
    // into a fresh builder, and publication happens only after capture completes.
    private struct MarkerPaintCapture(MarkerPaintPacket? prior)
    {
        private readonly ImmutableArray<MarkerPaint> previous=prior?.Commands??[];
        private ImmutableArray<MarkerPaint>.Builder? changed;
        private int count;

        private T? Previous<T>() where T:MarkerPaint=>count<previous.Length?previous[count] as T:null;
        private static bool Same<T>(T a,T b)=>EqualityComparer<T>.Default.Equals(a,b);
        private void Add(MarkerPaint command)
        {
            if(changed is null&&(count>=previous.Length||!ReferenceEquals(command,previous[count])))
            {
                changed=ImmutableArray.CreateBuilder<MarkerPaint>(Math.Max(previous.Length,count+1));
                for(int i=0;i<count;i++)changed.Add(previous[i]);
            }
            changed?.Add(command);
            count++;
        }
        internal void Texture(Texture2D texture,Rect2 rectangle,Color tint)
        {
            var p=Previous<MarkerTexturePaint>();
            Add(p is not null&&Same(p.Texture,texture)&&Same(p.Rectangle,rectangle)&&Same(p.Tint,tint)
                ?p:new MarkerTexturePaint(texture,rectangle,tint));
        }
        internal void Palette(Texture2D texture,Rect2 rectangle,MapCornerColors palette,byte alpha)
        {
            var p=Previous<MarkerPalettePaint>();
            Add(p is not null&&Same(p.Texture,texture)&&Same(p.Rectangle,rectangle)&&Same(p.Palette,palette)&&p.Alpha==alpha
                ?p:new MarkerPalettePaint(texture,rectangle,palette,alpha));
        }
        internal void Panel(MapMarkerSlices panel,Rect2 rectangle,Color tint)
        {
            var p=Previous<MarkerPanelPaint>();
            Add(p is not null&&Same(p.Panel,panel)&&Same(p.Rectangle,rectangle)&&Same(p.Tint,tint)
                ?p:new MarkerPanelPaint(panel,rectangle,tint));
        }
        internal void PalettePanel(MapMarkerSlices panel,Rect2 rectangle,MapCornerColors palette,byte alpha)
        {
            var p=Previous<MarkerPalettePanelPaint>();
            Add(p is not null&&Same(p.Panel,panel)&&Same(p.Rectangle,rectangle)&&Same(p.Palette,palette)&&p.Alpha==alpha
                ?p:new MarkerPalettePanelPaint(panel,rectangle,palette,alpha));
        }
        internal void Text(string value,Rect2 cell,MapFontProfile profile,HorizontalAlignment alignment,float width,Color tint,MarkerTextSlot slot)
        {
            var p=Previous<MarkerTextPaint>();
            Add(p is not null&&Same(p.Value,value)&&Same(p.Cell,cell)&&Same(p.Profile,profile)&&p.Alignment==alignment&&
                Same(p.Width,width)&&Same(p.Tint,tint)&&p.Slot==slot?p:new MarkerTextPaint(value,cell,profile,alignment,width,tint,slot));
        }
        internal void Bar(Marker target,MarkerBarWidth widthSlot,MapMarkerSlices panel,Texture2D fill,
            MapBarGeometry geometry,Vector2 position,Color panelTint,MapCornerColors palette,byte alpha)
        {
            var p=Previous<MarkerBarPaint>();
            Add(p is not null&&Same(p.Target,target)&&p.WidthSlot==widthSlot&&Same(p.Panel,panel)&&Same(p.Fill,fill)&&
                Same(p.Geometry,geometry)&&Same(p.Position,position)&&Same(p.PanelTint,panelTint)&&
                Same(p.Palette,palette)&&p.Alpha==alpha
                ?p:new MarkerBarPaint(target,widthSlot,panel,fill,geometry,position,panelTint,palette,alpha));
        }
        internal MarkerPaintPacket Finish(MapFont font,Vector2 scale,MarkerPaintPlacement placement)
        {
            ImmutableArray<MarkerPaint> commands;
            if(changed is not null)commands=changed.ToImmutable();
            else if(count==previous.Length)commands=previous;
            else
            {
                // The new drawing ended before the old one (including empty ink).
                var prefix=ImmutableArray.CreateBuilder<MarkerPaint>(count);
                for(int i=0;i<count;i++)prefix.Add(previous[i]);
                commands=prefix.MoveToImmutable();
            }
            if(prior is not null&&commands==prior.Commands&&ReferenceEquals(font,prior.Font)&&
                scale==prior.Scale&&placement==prior.Placement)return prior;
            return new(font,scale,placement,commands);
        }
    }

    private MarkerPaintPacket? CaptureOrdinaryMarkerPaint(Marker marker,MarkerPaintPlacement? actualPlacement,MarkerPaintPacket? previous,out Vector2 anchor)
    {
        anchor=Vector2.Zero;
        if(actualPlacement is not {} placement)return null;
        if(_frame is null||_namePanel is null||_numberPanel is null||_barPanel is null||_font is null)return null;
        // These branches have their own live consumers and animation rules.
        // In particular, Area interaction is refreshed after information
        // order. Its current/hovered marker must keep the original late read.
        if(campaignMarkerHexes is not null||campaignSelectedHexes is not null||campaignPresentation is not null||
            diplomacyTargetKind is not null||strategicTargetSelection is not null||
            _chronologyCityFlags is not null||directionSuppression.Count!=0||
            marker.Kind==Kind.Area&&(_hoveredHex==marker.Hex||AreaInteractionReplaces(marker)))return null;

        Vector2 scale=Size/CanonicalSize;
        anchor=marker.Screen*scale;
        var commands=new MarkerPaintCapture(previous);
        if(!_baseInformationVisible||!marker.OnScreen)return commands.Finish(_font,scale,placement);
        Vector2 p=Vector2.Zero;
        var palettes=_spritePalettes!;
        if(marker.Kind==Kind.Area)
        {
            var area=marker.AreaPresentation;
            Rect2 icon=AreaRectangle(_areaLayout!.FlagRectangle,p);
            if(marker.OutlineTint is Color outline)
                commands.Texture(_areaFlag[0],icon,Alpha(outline,area.FlagAlpha));
            commands.Texture(_areaFlag[1],icon,Alpha(marker.Tint,area.FlagAlpha));
            if(area.ShowName)
                commands.Text(marker.Name,AreaRectangle(_areaLayout.NameCellRectangle,p),_areaFont!,
                    HorizontalAlignment.Left,-1,Alpha(Colors.White,area.TextAlpha),MarkerTextSlot.LocationName);
            return commands.Finish(_font,scale,placement);
        }

        var city=marker.CityPresentation;
        int interaction=_hoveredHex==marker.Hex?(_pressedHex==marker.Hex?2:1):0;
        bool numbers=city.Display==1;
        string simpleText=numbers?marker.Troops:marker.Name;
        var geometry=CityGateMapMarkerSources.SimpleGeometry(simpleText,numbers,marker.Kind==Kind.Gate);
        Rect2 flag=AreaRectangle(geometry.Flag,p);
        Color panelTint=Alpha(Colors.White,city.PanelAlpha);
        if(city.Display!=2)
            commands.PalettePanel(_namePanel,AreaRectangle(geometry.Plate,p),palettes[4+interaction],city.PanelAlpha);
        if(marker.Kind==Kind.City)
        {
            var outlineTexture=_cityFlag[marker.OutlineTint is not null?1:0];
            if(interaction==0)
            {
                commands.Texture(outlineTexture,flag,Alpha(marker.OutlineTint??Colors.White,city.FlagAlpha));
                commands.Texture(_cityFlag[2],flag,Alpha(marker.Tint,city.FlagAlpha));
            }
            else
            {
                commands.Palette(outlineTexture,flag,palettes[4+interaction],city.FlagAlpha);
                commands.Palette(_cityFlag[2],flag,palettes[4+interaction],city.FlagAlpha);
            }
            if(marker.FlagGlyph.Length!=0)
                Text(marker.FlagGlyph,new(-12,-30,24,24),_cityGlyphStates[interaction],HorizontalAlignment.Center,
                    city.FlagAlpha,interaction==0?marker.FlagLetterColor:Colors.White,MarkerTextSlot.ForceGlyph);
        }
        else
        {
            commands.Texture(_gateFlag[0],flag,Alpha(Colors.White,city.FlagAlpha));
            if(marker.OutlineTint is Color outline)
                commands.Texture(_gateFlag[1],flag,Alpha(outline,city.FlagAlpha));
            commands.Texture(_gateFlag[2],flag,Alpha(marker.Tint,city.FlagAlpha));
        }
        if(city.Display==2)return commands.Finish(_font,scale,placement);
        Text(simpleText,geometry.TextCell,numbers?_simpleNumberStates[interaction]:_locationStates[interaction],
            numbers?HorizontalAlignment.Left:HorizontalAlignment.Center,city.PanelAlpha,slot:numbers?MarkerTextSlot.SimpleTroops:MarkerTextSlot.LocationName);
        if(city.Display==0)
        {
            commands.Panel(_numberPanel,new(p+new Vector2(-6,-1),new Vector2(98,24)),panelTint);
            commands.Texture(_soldier!,new(p+new Vector2(-7,-2),new Vector2(24,24)),
                Alpha(CanvasColor(palettes[10].LT),city.PanelAlpha));
            Text(marker.Troops,new(19,-2,156,24),_numberFont!,HorizontalAlignment.Left,city.PanelAlpha,slot:MarkerTextSlot.FullTroops);
        }
        if(marker.DurabilityCurrent is int hp&&marker.DurabilityCapacity is int capacity&&hp<capacity)
            Bar(hp,capacity==0?6000:capacity,new(-15,-48,108,12),
                hp>palettes.DurabilityStylePercent*capacity/100?1:2,marker.HpFillWidth,MarkerBarWidth.Hp);
        if(marker.Morale44 is byte morale&&marker.MoraleCapacity is int moraleCapacity)
        {
            int style=morale>=palettes.MoraleStyleThreshold?3:2;
            if(city.Display==0)Bar(morale,moraleCapacity,new(-6,21,98,8),style,marker.FullMoraleFillWidth,MarkerBarWidth.FullMorale);
            else Bar(morale,moraleCapacity,new(18,-10,72,8),style,marker.SimpleMoraleFillWidth,MarkerBarWidth.SimpleMorale);
        }
        return commands.Finish(_font,scale,placement);

        void Text(string value,AreaMarkerRectangle cell,MapFontProfile profile,HorizontalAlignment alignment,byte alpha,Color? tint=null,MarkerTextSlot slot=MarkerTextSlot.LocationName)
            =>commands.Text(value,AreaRectangle(cell,p),profile,alignment,cell.Width,Alpha(tint??Colors.White,alpha),slot);
        void Bar(int current,int capacity,AreaMarkerRectangle control,int style,int? retained,MarkerBarWidth slot)
            =>commands.Bar(marker,slot,_barPanel,_barFill!,_citySources!.BarGeometry(current,capacity,control,retained),
                p,panelTint,palettes[style],city.PanelAlpha);
    }
}

using Godot;

/// <summary>The kind9 custom rectangle advances by its actual scaled sprite
/// width, without a glyph-space or a separate leading column.</summary>
internal static class UiSourceInlineIconLayout
{
    internal static void Apply(UiSourceText label, UiSourceSprite icon, Rect2 bounds,
        MapFont font, MapFontProfile profile, Vector2 sourceSize)
    {
        //E14940 rounds the source UV extent before the text writer scales it.
        int width=(int)(sourceSize.X+0.5f), height=(int)(sourceSize.Y+0.5f);
        int available=(int)bounds.Size.X-(int)profile.WidthReserve;
        int measured=(int)(width*profile.ScaleX)+font.Measure(label.Text,profile).Width;
        if (profile.WidthFitMode!=0 && measured>available && measured>0)
        {
            float factor=available/(float)measured*0.98f;
            profile=profile with { ScaleX=profile.ScaleX*factor,
                ScaleY=profile.WidthFitMode==1?profile.ScaleY*factor:profile.ScaleY,
                HorizontalSpace=unchecked((short)(int)(profile.HorizontalSpace*factor)) };
        }
        int w=(int)(width*profile.ScaleX), h=(int)(height*profile.ScaleY);
        int textWidth=font.Measure(label.Text,profile).Width;
        float x=(profile.RawAlign>>4) switch
        { 1=>(bounds.Size.X-w-textWidth)*0.5f,2=>bounds.Size.X-w-textWidth,_=>0 };
        icon.Position=bounds.Position+new Vector2(x,(bounds.Size.Y-h)*0.5f);
        icon.Size=new(w,h);icon.Show();
        label.Position=bounds.Position+new Vector2(x+w,0);
        label.Size=new(Math.Max(0,bounds.Size.X-x-w),bounds.Size.Y);
        label.Configure(font,profile with { WidthFitMode=0,RawAlign=(byte)(profile.RawAlign&15) });
    }
}

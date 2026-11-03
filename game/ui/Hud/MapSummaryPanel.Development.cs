using San14.Data;

internal partial class MapSummaryPanel
{
    private void ApplyDevelopmentArrow(MapSummaryPage page)
    {
        // Only development publishes these logical texts. The tactic summary
        // uses the same source layout and must retain its authored geometry.
        if(page.DevelopmentMeasure is not {} measure||!images.TryGetValue(7,out var arrow))return;
        var prefix=source!.Rectangle(9);
        var level=source.Rectangle(11);
        var authoredArrow=source.Rectangle(7);
        float gap=level.Position.X-2*level.Size.X-authoredArrow.Position.X;
        int width=DevelopmentTextWidth(9,measure.LevelPrefixText)+DevelopmentTextWidth(11,measure.LevelValueText);
        arrow.View.Position=new(prefix.Position.X+prefix.Size.X-width-gap,authoredArrow.Position.Y);
    }

    private int DevelopmentTextWidth(int node,string text)
    {
        // The source writer measures at unit scale, narrows to signed Int16,
        // then truncates each label's final scaled width independently. The
        // fitted drawing profile and glyph pixel bounds are not this measure.
        var profile=MapFontProfile.FromNode(source!.Nodes[node],source.Source.Source.ResourceUri);
        int advance=resources.Font.Source.Measure(text,profile with {ScaleX=1f,HorizontalSpace=0}).Width;
        return (int)(unchecked((short)advance)*profile.ScaleX);
    }
}

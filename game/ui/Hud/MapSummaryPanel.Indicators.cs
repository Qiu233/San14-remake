using Godot;
using San14.Data;

internal partial class MapSummaryPanel
{
    private readonly Dictionary<int,Dictionary<string,Control>> indicatorImages=[];
    private IReadOnlyList<MapSummaryIndicator> indicators=[];
    private MapSummaryPage? indicatorPage;

    private void ApplyIndicators(MapSummaryPage page)
    {
        indicators=page.Indicators;indicatorPage=page;
        foreach(var images in indicatorImages.Values)foreach(var image in images.Values)image.Hide();
        RefreshOwnIndicators();
    }
    internal void RefreshIndicators()
    {
        RefreshOwnIndicators();
        foreach(var child in nested.Values)child.View.RefreshIndicators();
    }
    private void RefreshOwnIndicators()
    {
        if(indicatorPage is not{} page)return;
        foreach(var indicator in indicators)
        {
            if(!indicatorImages.TryGetValue(indicator.Node,out var images))
                indicatorImages.Add(indicator.Node,images=[]);
            var state=resources.IndicatorPlayback.Read(indicator.ArmyPhysicalSlot);
            if(!images.TryGetValue(state.SpriteName,out var image))
            {
                image=source!.Art(this,indicator.Node,source.Rectangle(indicator.Node),resources.Assets,
                    resources.Shader,commonSpriteOverride:state.SpriteName);
                image.ZIndex=indicator.Node;images.Add(state.SpriteName,image);
            }
            foreach(var value in images.Values)value.Visible=ReferenceEquals(value,image)&&Shown(indicator.Node,page);
            ApplyColors(image,resources.Palettes[state.Palette]);
            image.SetMeta("source_indicator_index",state.Index);
            image.SetMeta("source_army_physical_slot",indicator.ArmyPhysicalSlot);
        }
    }
}

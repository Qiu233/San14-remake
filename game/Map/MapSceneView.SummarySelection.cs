using San14.Data;

//These values belong to the original selection workspace, independently of map overlays.
internal enum MapSummarySelectionMode
{
    Normal=0,NewDistrictCities=1,Foothold=2,Domestic=3,Force=4,District=5,Province=6
}

public partial class MapSceneView
{
    private MapSummarySelectionMode _summaryMode;
    private object? _summaryHoverOwner;
    private bool? _summaryHoverValid;
    private static uint SummaryUiMilliseconds=>unchecked((uint)Godot.Time.GetTicksMsec());
    private MapSummaryHoverTarget? _summaryHoverTarget;
    private readonly MapSummaryIndicatorPlayback _summaryIndicators=new();
    private Func<San14.Core.GameState,SourceWorldContext,MapSummaryIndicatorFrame>? _querySummaryIndicators;

    internal void SetMapSummaryMode(MapSummarySelectionMode mode)
    {
        if(_summaryMode==mode)return;
        _summaryMode=mode;InvalidateMapSummaries();
    }
    internal void SetMapSummaryHover(object owner,MapSummaryHoverTarget? target)
    {
        if(target is null)
        {
            if(!ReferenceEquals(_summaryHoverOwner,owner))return;
            _summaryHoverOwner=null;
        }
        else _summaryHoverOwner=owner;
        if(_summaryHoverTarget==target)return;
        _summaryHoverTarget=target;_summaryHoverValid=null;InvalidateMapSummaries();
    }
    private void AdvanceMapSummaryIndicators(int framesPerSecond)
    {
        if(!_summaryIndicators.Advance(framesPerSecond))return;
        foreach(var view in _summaryViews.Values)
            if(view.Panel is{} panel&&Godot.GodotObject.IsInstanceValid(panel)&&panel.IsVisibleInTree())panel.RefreshIndicators();
    }
}

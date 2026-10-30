using San14.Data;

/// <summary>The map manager's source callback counter and 501 display indexes.
/// Candidates change with a real current publication; visibility does not reset them.</summary>
internal sealed class MapSummaryIndicatorPlayback
{
    private readonly int[] indexes=Enumerable.Repeat(-1,501).ToArray();
    private MapSummaryIndicatorFrame? frame;
    private int counter;
    internal void Bind(MapSummaryIndicatorFrame current)=>frame=current;
    internal MapSummaryIndicatorState Read(int armyPhysicalSlot)=>
        MapSummaryIndicatorRules.Describe(indexes[armyPhysicalSlot]);
    internal bool Advance(int framesPerSecond)
    {
        int callbacks=MapSummaryIndicatorRules.RefreshCallbackCount(framesPerSecond);
        if(++counter<callbacks)return false;
        counter=0;
        if(frame is null)return false;
        foreach(var(army,states) in frame.ArmyStates)
            indexes[army]=MapSummaryIndicatorRules.NextIndex(indexes[army],states);
        return true;
    }
    internal void Reset(){Array.Fill(indexes,-1);counter=0;frame=null;}
}

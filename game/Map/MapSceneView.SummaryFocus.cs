using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private readonly D3D11MapDisplayPublicationOwner mapDisplayPublications=new();
    private GameState? summaryFocusWorld;
    private D3D11CommandMapData? summaryCityFocus,summaryAreaFocus,summaryFocusData;
    private uint summaryFocusHex=D3D11CommandMapData.CellCount;
    private bool summaryFocusActive;

    // 925FD0's City/Gate receiver gate precedes panel delay/visibility. The
    // original manager+168 is enabled by its normal constructor. Mode6 has no
    // ordinary City focus; explicit command displays keep their own consumer.
    private void UpdateSummaryFocus(GameState world,SourceWorldContext current,
        MapSummarySelection selection,int band)
    {
        summaryFocusActive=false;
        if(_summaryMode!=MapSummarySelectionMode.Normal || band==6)return;
        // The source handles Area first, then lets City/Gate select5 instead.
        var target=selection.Receivers.FirstOrDefault(row=>row.Slot is 2 or 3)??
            selection.Receivers.FirstOrDefault(row=>row.Slot==1);
        if(target is null)return;
        if(!ReferenceEquals(summaryFocusWorld,world))
        {
            (summaryCityFocus,summaryAreaFocus)=D3D11CommandMapData.CaptureSummaryFocus(current);
            summaryFocusWorld=world;
        }
        if(target.Slot==1)
        {
            var area=current.ReadAreaTableReference(target.ReceiverId)??throw new InvalidDataException("summary-focus.Area_target");
            summaryFocusHex=current.ReadAreaHexIndex(area)??throw new InvalidDataException("summary-focus.Area3A");
            summaryFocusData=summaryAreaFocus;
        }
        else
        {
            BaseReference basis=target.Slot==2?new(BaseKind.City,target.ReceiverId):new(BaseKind.Gate,target.ReceiverId-51);
            summaryFocusHex=current.ReadBaseCurrentHexIndex(basis);
            summaryFocusData=summaryCityFocus;
        }
        summaryFocusActive=summaryFocusHex<D3D11CommandMapData.CellCount;
    }
    private D3D11CommandMapPublication? CaptureMapDisplay()=>mapDisplayPublications.Capture(_commandMapDisplay,
        summaryFocusActive&&!_summarySuspended&&!_detailsOpen&&!_periodMapActive&&IsVisibleInTree()?summaryFocusData:null,
        summaryFocusHex);
}

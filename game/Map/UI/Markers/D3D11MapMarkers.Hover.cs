using Godot;
using San14.Data;

internal partial class D3D11MapMarkers
{
    private readonly MapInformationHitSnapshot informationHits=new();

    internal void PrepareInformationHits(MainMapCameraFrame frame)
    {
        if(!ReferenceEquals(_frame,frame))throw new InvalidOperationException("map-information.prepared-frame");
        var hits=new List<MapInformationHit>();
        foreach(var entry in informationOrder)
            if(TryReadInformationHitRectangle(entry,frame,out var rect))
            {
                OrdinaryTacticViewIdentity? identity=entry.Marker is {} marker&&marker.Kind!=Kind.Area?
                    new(marker.Kind==Kind.City?OrdinaryTacticViewKind.City:OrdinaryTacticViewKind.Gate,marker.Id):null;
                hits.Add(new(rect,entry.Marker?.Hex,entry.Ordinal,entry.Card is not null,identity));
            }
        informationHits.Prepare(frame,hits.ToArray());
    }

    internal void AcceptInformationHits(MainMapCameraFrame frame)=>informationHits.Accept(frame);

    // Cards consume the point without selecting the terrain behind them.
    // A future prepared packet must never replace the currently accepted one.
    internal bool TryReadHoverPointer(Vector2 point,MainMapCameraFrame frame,out int? hex)
    {
        hex=null;
        if(!IsVisibleInTree()||Size.X<=0||Size.Y<=0)return false;
        if(informationHits.TryPick(point/Size*CanonicalSize,frame,
            _baseInformationVisible||campaignMarkerHexes is not null,out var hit,campaignMarkerHexes,directionSuppression))
        {hex=hit.Hex;AcceptPointerHex(hex);return true;}
        if((_baseInformationVisible||campaignMarkerHexes is not null)&&TryPickFacility(point,frame,out int facility))
        {hex=facility;AcceptPointerHex(hex);return true;}
        AcceptPointerHex(null);return false;
    }
}

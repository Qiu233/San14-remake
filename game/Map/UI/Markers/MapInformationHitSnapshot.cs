using Godot;
using San14.Data;

// Finite hit values in the actual paint order. Ordinal retains the existing
// UI owner's identity; no marker, card, node or world state is read at pick.
internal readonly record struct MapInformationHit(Rect2 Rectangle,int? Hex,long Ordinal,bool IsCard,
    OrdinaryTacticViewIdentity? DirectionIdentity=null);

internal sealed class MapInformationHitSnapshot
{
    private MainMapCameraFrame? preparedFrame,acceptedFrame;
    private MapInformationHit[] prepared=[],accepted=[];

    internal void Prepare(MainMapCameraFrame frame,MapInformationHit[] hits)
    {preparedFrame=frame;prepared=hits;}

    internal void Accept(MainMapCameraFrame frame)
    {
        if(!ReferenceEquals(frame,preparedFrame))throw new InvalidOperationException("map-information.accepted-frame");
        acceptedFrame=frame;accepted=prepared;
    }

    internal bool TryPick(Vector2 cursor,MainMapCameraFrame frame,bool baseAdmitted,out MapInformationHit hit,
        IReadOnlySet<int>? campaignHexes=null,
        IReadOnlyDictionary<OrdinaryTacticViewIdentity,int>? directionSuppression=null)
    {
        hit=default;
        if(!ReferenceEquals(frame,acceptedFrame))return false;
        for(int index=accepted.Length-1;index>=0;index--)
        {
            var candidate=accepted[index];
            if(!candidate.IsCard)
            {
                if(!baseAdmitted)continue;
                if(campaignHexes is not null&&(candidate.DirectionIdentity is null||
                    candidate.Hex is not int hex||!campaignHexes.Contains(hex)))continue;
                if(candidate.DirectionIdentity is {} identity&&directionSuppression?.ContainsKey(identity)==true)continue;
            }
            var rect=candidate.Rectangle;
            if(cursor.X>=rect.Position.X&&cursor.Y>=rect.Position.Y&&cursor.X<=rect.End.X&&cursor.Y<=rect.End.Y)
            {hit=candidate;return true;}
        }
        return false;
    }
}

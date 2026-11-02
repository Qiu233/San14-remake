using Godot;
using San14.Data;
using NVector4=System.Numerics.Vector4;

internal partial class D3D11MapMarkers
{
    // The existing marker owner owns this actual draw bucket. Entries retain
    // insertion identity when hidden; no world/registry is queried by sorting.
    private sealed class InformationControl(CanvasItem ink,long ordinal,Marker? marker=null,MapDiplomacyCardView? card=null,int keyOffset=0)
    {
        internal CanvasItem Ink{get;}=ink;
        internal long Ordinal{get;}=ordinal;
        internal Marker? Marker{get;}=marker;
        internal MapDiplomacyCardView? Card{get;}=card;
        internal int KeyOffset{get;}=keyOffset;
        internal uint Key;
    }
    private readonly Dictionary<Marker,InformationControl> baseInformationInk=[];
    private readonly Dictionary<MapDiplomacyCardView,InformationControl> diplomacyInformationInk=[];
    private readonly Dictionary<MapDiplomacyCardView,List<InformationControl>> diplomacyParts=[];
    private readonly List<InformationControl> informationOrder=[];
    private UiChildOrder<InformationControl>? informationChildren;
    private UiChildOrder<InformationControl> InformationChildren=>informationChildren??=
        new((entry,index)=>markerCanvas.MoveChild(entry.Ink,index));
    private long informationOrdinal;
    private int? informationPlayer;
    private int informationPresentationDepth;
    private bool informationOrderPending;

    // A prepared map frame changes marker positions and diplomacy cards
    // together. Publish their final order before preparing that frame's hits.
    internal void BeginInformationPresentation() => informationPresentationDepth++;
    internal void EndInformationPresentation()
    {
        if (--informationPresentationDepth == 0 && informationOrderPending)
        {
            informationOrderPending = false;
            RefreshInformationOrder();
        }
    }

    // Base ink needs the actual current-player reference for its source order,
    // including command-map modes that hide the ordinary information panels.
    internal bool RequiresInformationPublication => _baseInformationVisible || campaignMarkerHexes is not null;

    internal void SetInformationPlayer(int? actualPlayer)
    {informationPlayer=actualPlayer;}

    internal bool TryReadBaseUnitPosition(BaseReference? reference,MainMapCameraFrame frame,out NVector4 unit)
    {
        unit=default;
        if(!ReferenceEquals(_frame,frame)||reference?.PhysicalSlot is not int physical)return false;
        Kind kind=reference.Kind==BaseKind.City?Kind.City:Kind.Gate;
        // BaseReference keeps native Gate1..10; the existing marker owner and
        // accepted primary publication keep world location52..61.
        int location=kind==Kind.Gate?physical+51:physical;
        if(!_markers.TryGetValue((kind,location),out var marker)||!marker.PositionReady)return false;
        var p=marker.CityUnitPosition;
        unit=new(p,1);return true;
    }

    internal void AddDiplomacyCard(MapDiplomacyCardView card)
    {
        var mount=new Node2D{Name=card.Name+"SourceInk",Scale=Size/CanonicalSize};
        markerCanvas.AddChild(mount);mount.AddChild(card);
        var entry=new InformationControl(mount,informationOrdinal++,card:card);
        diplomacyInformationInk.Add(card,entry);InformationChildren.Add(entry);
        diplomacyParts.Add(card,[]);
        card.SourceHitAdmitted=cursor=>ReferenceEquals(InformationHitAt(cursor,_frame)?.Card,card);
        card.SourceOrderChanged+=RefreshInformationOrder;
    }

    internal void AddDiplomacyDrawingParts(MapDiplomacyCardView card)
    {
        foreach(var part in card.SourceDrawingParts)
        {
            var mount=new Node2D{Name=part.Ink.Name+"SourceInk",Scale=Size/CanonicalSize};
            markerCanvas.AddChild(mount);
            part.Ink.GetParent().RemoveChild(part.Ink);mount.AddChild(part.Ink);
            var entry=new InformationControl(mount,informationOrdinal++,card:card,keyOffset:part.KeyOffset);
            diplomacyParts[card].Add(entry);InformationChildren.Add(entry);
        }
    }

    internal void SetDiplomacyOrder(MapDiplomacyCardView card,uint sourceKey)
    {
        if(!diplomacyInformationInk.TryGetValue(card,out var entry))return;
        entry.Key=sourceKey;
        foreach(var part in diplomacyParts[card])part.Key=unchecked(sourceKey+(uint)part.KeyOffset);
    }

    internal void RemoveDiplomacyCard(MapDiplomacyCardView card)
    {
        card.SourceOrderChanged-=RefreshInformationOrder;
        card.SourceHitAdmitted=null;
        if(!diplomacyInformationInk.Remove(card,out var entry))return;
        foreach(var part in diplomacyParts[card])
        {informationOrder.Remove(part);InformationChildren.Remove(part);markerCanvas.RemoveChild(part.Ink);part.Ink.Free();}
        diplomacyParts.Remove(card);
        informationOrder.Remove(entry);
        InformationChildren.Remove(entry);
        markerCanvas.RemoveChild(entry.Ink);entry.Ink.Free();
    }

    internal void RefreshInformationOrder()
    {
        if (informationPresentationDepth > 0)
        {
            informationOrderPending = true;
            return;
        }
        // All base ink has identity local transforms inside this bucket.
        // Track the two actual inputs to MapFont's viewport backbuffer copy
        // once per owner; Font retains its original late read inside _Draw.
        MarkerPaintPlacement? paintPlacement=_frame is not null&&_font is not null&&markerCanvas.IsInsideTree()
            ?new(markerCanvas.GetGlobalTransformWithCanvas(),GetViewport().GetVisibleRect()):null;
        // Current marker references are retained objects. Deletion retires
        // only their ink owner and its MapFont RID slots.
        var live=new HashSet<Marker>(_markers.Values);
        foreach(var marker in baseInformationInk.Keys.Where(marker=>!live.Contains(marker)).ToArray())
        {
            var old=baseInformationInk[marker];baseInformationInk.Remove(marker);
            informationOrder.Remove(old);InformationChildren.Remove(old);markerCanvas.RemoveChild(old.Ink);old.Ink.QueueFree();
        }
        foreach(var marker in _markers.Values)
        {
            if(!baseInformationInk.TryGetValue(marker,out var entry))
            {
                var ink=new MarkerInk(this,marker){Name=$"{marker.Kind}{marker.Id}SourceInk"};
                markerCanvas.AddChild(ink);
                entry=new(ink,informationOrdinal++,marker);baseInformationInk.Add(marker,entry);
                InformationChildren.Add(entry);
            }
            bool known=_frame is not null&&informationPlayer is not null&&marker.PositionReady&&marker.SourceSortForce is not null;
            bool visible=known&&TargetMarkerVisible(marker);
            entry.Ink.Visible=visible;
            if(known)
            {
                var p=marker.Kind==Kind.Area?marker.AreaUnitPosition:marker.CityUnitPosition;
                entry.Key=MapLabelPresentation.ControlSortKey(_frame!,p,marker.Kind==Kind.Area?8:32,
                    informationPlayer!=0&&marker.SourceSortForce==informationPlayer,
                    _hoveredHex==marker.Hex,_pressedHex==marker.Hex||campaignSelectedHexes?.Contains(marker.Hex)==true);
            }
            ((MarkerInk)entry.Ink).RefreshPaint(visible,paintPlacement);
        }
        informationOrder.Clear();informationOrder.AddRange(baseInformationInk.Values);
        foreach(var entry in diplomacyInformationInk.Values)
        {
            ((Node2D)entry.Ink).Scale=Size/CanonicalSize;
            informationOrder.Add(entry);
            foreach(var part in diplomacyParts[entry.Card!])
            {
                var mount=(Node2D)part.Ink;
                mount.Scale=Size/CanonicalSize;mount.Position=entry.Card!.Position*mount.Scale;
                mount.Visible=entry.Card.Visible;
                informationOrder.Add(part);
            }
        }
        informationOrder.Sort((a,b)=>
        {
            uint x=a.Key|(a.Card?.SourceInteractionFlags??0),y=b.Key|(b.Card?.SourceInteractionFlags??0);
            int order=x.CompareTo(y);return order!=0?order:a.Ordinal.CompareTo(b.Ordinal);
        });
        InformationChildren.Apply(informationOrder);
    }

    private InformationControl? InformationHitAt(Vector2 cursor,MainMapCameraFrame? frame)
    {
        if(frame is null)return null;
        foreach(var entry in informationOrder.AsEnumerable().Reverse())
        {
            if(!TryReadInformationHitRectangle(entry,frame,out var rect))continue;
            if(cursor.X>=rect.Position.X&&cursor.Y>=rect.Position.Y&&cursor.X<=rect.End.X&&cursor.Y<=rect.End.Y)return entry;
        }
        return null;
    }
    // Both live GUI admission and the prepared packet use the source draw
    // bucket's exact visibility and geometry, including the Area name pane.
    private bool TryReadInformationHitRectangle(InformationControl entry,MainMapCameraFrame frame,out Rect2 rect)
    {
        rect=default;
        if(!entry.Ink.Visible)return false;
        if(entry.Card is {} card)
        {
            if(entry.KeyOffset!=0||card.SourceHitRectangle is not {} box)return false;
            rect=box;return true;
        }
        if(entry.Marker is not {} marker||(!_baseInformationVisible&&campaignMarkerHexes is null))return false;
        if(campaignPresentation is not null)return TryReadCampaignHitRectangle(marker,frame,out rect);
        if(!marker.PositionReady||!CampaignMarkerVisible(marker)||!TargetMarkerVisible(marker)||!TargetMarkerEnabled(marker)||DirectionSuppresses(marker))return false;
        AreaMapLabelPresentation area=default;
        if(marker.Kind==Kind.Area)
        {area=ReadTargetAreaPresentation(marker,frame);if(!area.Visible)return false;}
        else if(!ReadTargetCityPresentation(marker,frame).Visible)return false;
        if(!TryProject(marker.Position,frame.Camera.Matrices.WorldToProjection100,out var p))return false;
        rect=marker.Kind==Kind.Area?AreaHitRectangle(marker,p,area.ShowName):
            AreaRectangle(CityGateMapMarkerSources.NormalHitRectangle(
                ReadTargetCityPresentation(marker,frame).Display,
                marker.Troops,marker.Kind==Kind.Gate),p);
        return true;
    }

}

using Godot;
using San14.Core;
using San14.Data;
using NVector3=System.Numerics.Vector3;
using NVector4=System.Numerics.Vector4;
using NMatrix=System.Numerics.Matrix4x4;

internal sealed record NewGameForceMapContent(MapSeasonScenarioPreviewFrame Calendar,
    SourceWorldContext Current,IReadOnlyList<NewGameFootholdVisualMarker> Markers,
    IReadOnlyDictionary<int,ForcePreviewRecord> Forces);

/// <summary>The original mode2 Foothold collection, over the same accepted
/// native map output. No flag is an independent button or terrain substitute.</summary>
internal partial class NewGameForceMapOverlay : Control
{
    private sealed record Entry(NewGameFootholdVisualMarker Source,NewGameForceMapFlag Flag,int Hex)
    { internal Rect2 Hit; internal bool OnScreen; internal NVector3 Position; internal uint Key; internal float Fade; internal int Level; }
    private readonly List<Entry> entries=[],ordered=[];
    private Entry? pointer,pressed;
    private MainMapCameraFrame? acceptedCamera;
    private NewGameForceMapContent content=null!;
    private UiAssets assets=null!;private Shader shader=null!;private MapFont font=null!;
    private int? selected,hovered;
    internal void Configure(NewGameForceMapContent content,UiAssets assets,Shader shader,MapFont font)
    {
        this.content=content;this.assets=assets;this.shader=shader;this.font=font;
        MouseFilter=MouseFilterEnum.Ignore;Size=new(1920,1080);
    }
    internal void ApplyInteraction(int? selectedForce,int? hoveredForce)
    {
        selected=selectedForce;hovered=hoveredForce;
        foreach(var item in entries)
        {
            item.Flag.ApplyInteraction(item.Source.Force==selected,item.Source.Force==hovered,true);
            byte alpha=item.Source.Force==selected||item.Source.Force==hovered?(byte)255:
                MapLabelPresentation.FlagAlpha(item.Level,item.Fade);
            item.Flag.Modulate=new(1,1,1,alpha/255f);
        }
    }
    internal void Present(MapSceneView map,D3D11MapInput input,CityGateMapMarkerSources sources)
    {
        var frame=input.SourceFrame??throw new InvalidDataException("The accepted source map camera is absent.");
        var cities=input.CityGateWorld?.Source??throw new InvalidDataException("The accepted source map Units are absent.");
        if(entries.Count==0)
        {
            foreach(var marker in content.Markers)
            {
                var owner=cities.Primary.Owners.Single(unit=>unit.ObjectLink4E==marker.ObjectLink&&unit.CitySelection is not null);
                var material=cities.Accessories.Single(unit=>unit.Index==owner.Index).Material??
                    throw new InvalidDataException("The actual force flag material is unavailable.");
                if(material.Force!=marker.Force)throw new InvalidDataException("Foothold and created Unit have different Force owners.");
                var force=content.Forces.GetValueOrDefault(marker.Force)??throw new InvalidDataException("The source Force record is absent.");
                bool valid=content.Current.ReadForceNativeValidity(new(marker.Force))??
                    throw new InvalidDataException("The source Force validity is unavailable.");
                var flag=new NewGameForceMapFlag();AddChild(flag);
                flag.Configure(sources.CityFlag,assets,shader,font,material.Color49,((char)material.Glyph).ToString(),
                    valid,TitleForceFlagStyleSource.ReadNormalStaticTitle(force),marker.Force is >=46 and <=50);
                int hex=content.Current.ReadBaseCurrentHexIndex(marker.Base);
                var entry=new Entry(marker,flag,hex);entries.Add(entry);ordered.Add(entry);
            }
            ApplyInteraction(selected,hovered);
        }
        acceptedCamera=frame;
        var eye=MapCameraController.DerivePose(frame.ControllerState).Eye10;
        int level=MapLabelPresentation.PrimaryCameraLevel(frame.ZoomSelection);
        var viewport=new NMatrix(960,0,0,0,0,-540,0,0,0,0,1,0,960,540,0,1);
        bool specialAnchorUsed=false;
        foreach(var entry in entries)
        {
            var owner=cities.Primary.Owners.Single(unit=>unit.ObjectLink4E==entry.Source.ObjectLink&&unit.CitySelection is not null);
            var position=owner.Instance.SourceWorldState?.Position18??
                throw new InvalidDataException("The actual City Unit position is absent.");
            if(entry.Source.Force==46&&!specialAnchorUsed)
            {
                specialAnchorUsed=true;
                var point=CeHexWorldCoordinates.Project(3011);float x=(float)point.X,z=(float)point.Z;
                float height=map.SourceMainCameraTerrain?.Query(x,z)??
                    throw new InvalidDataException("The source Landscape for the title's special Hex is absent.");
                position=new(x,Math.Max(445,height),z);
            }
            var screen=MapControlProjection.Project(new(position,1),new(0,1000,0),
                frame.Camera.SourceWorldToProjection,viewport,false,new(1920,1080));
            entry.Position=position;
            float ratio=MapLabelPresentation.DistanceFade(position,new(eye.X,eye.Y,eye.Z),frame.ControllerState.Distance90);
            entry.Level=level;entry.Fade=ratio;
            bool visible=screen is not null&&!(level<4&&ratio>1);
            entry.OnScreen=visible;entry.Flag.Visible=visible;
            if(!visible)continue;
            var pixel=screen!.Value;
            entry.Flag.Position=new(pixel.X-20,pixel.Y-40);
            var hit=CityGateMapMarkerSources.NormalHitRectangle(2,"",false);
            entry.Hit=new(new(pixel.X+hit.Left,pixel.Y+hit.Top),new(hit.Width,hit.Height));
            byte alpha=MapLabelPresentation.FlagAlpha(level,ratio);
            if(entry.Source.Force==selected||entry.Source.Force==hovered)alpha=255;
            entry.Flag.Modulate=new(1,1,1,alpha/255f);
        }
        RefreshOrder();
    }
    internal bool ReadPointer(Vector2 canonical,bool? press,out int hex)
    {
        Entry? hit=null;
        for(int i=ordered.Count-1;i>=0;i--)
        {
            var entry=ordered[i];
            if(entry.OnScreen&&canonical.X>=entry.Hit.Position.X&&canonical.X<=entry.Hit.End.X&&
                canonical.Y>=entry.Hit.Position.Y&&canonical.Y<=entry.Hit.End.Y){hit=entry;break;}
        }
        // Generic Cmn hover is not drag-locked. Leaving clears raw100; a
        // held sample never re-arms the entered control's pressed flag.
        if(!ReferenceEquals(pointer,hit)){pressed=null;pointer=hit;}
        if(press==true)pressed=hit;
        else if(press==false)pressed=null;
        RefreshOrder();hex=hit?.Hex??-1;return hit is not null;
    }
    internal void ClearPointer(){pointer=pressed=null;RefreshOrder();}
    private void RefreshOrder()
    {
        if(acceptedCamera is not {} frame)return;
        foreach(var entry in ordered)
            entry.Key=MapLabelPresentation.ControlSortKey(frame,entry.Position,32,false,
                ReferenceEquals(pointer,entry),ReferenceEquals(pressed,entry));
        // 865FA0 is stable insertion in the existing linked order. Equal keys
        // retain LAST round's order, not a recreated creation-order tie-break.
        for(int i=1;i<ordered.Count;i++)
        {
            var item=ordered[i];int j=i-1;
            while(j>=0&&ordered[j].Key>item.Key){ordered[j+1]=ordered[j];j--;}
            ordered[j+1]=item;
        }
        for(int i=0;i<ordered.Count;i++)if(ordered[i].Flag.GetIndex()!=i)MoveChild(ordered[i].Flag,i);
    }
}

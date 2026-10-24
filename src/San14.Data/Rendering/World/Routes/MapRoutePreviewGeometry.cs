using System.Numerics;
using F = San14.Data.UnitWorldGroundHeightFloat;

namespace San14.Data;

/// <summary>One actual cached segment. Identity is the page's segment object;
/// it is replaced by the page's real route writer, not a serialized fingerprint.
/// Hexes are the search's ordered grid indices, including both endpoints.</summary>
public sealed record MapRoutePreviewPath(object Identity, DeploymentDraftRouteStatus Status,
    IReadOnlyList<int>? Hexes, int? StartSurfaceCode, int? EndSurfaceCode,
    int ListBank, int EndpointChannel, string? UnresolvedInput)
{
    public int? ControlStartHex { get; init; }
    public int? ControlGoalHex { get; init; }
    public int? ControlStartSurfaceCode { get; init; }
    public int? ControlGoalSurfaceCode { get; init; }
}

public sealed record MapRoutePreviewMesh(MapRoutePreviewPath Path, MapRouteSpine? Spine,
    MapRouteRibbon? Ribbon, float FullWidth, string? UnresolvedInput)
{
    public ReadOnlyMemory<MapRouteRibbonVertex> CornerVertices { get; init; }
    public ReadOnlyMemory<uint> CornerIndices { get; init; }
    public IReadOnlyList<MapRoutePreviewEndpoint> Endpoints { get; init; } = Array.Empty<MapRoutePreviewEndpoint>();
}

public sealed record MapRoutePreviewEndpoint(int Hex, Vector3 Center, int Surface, int ListBank, int Channel)
{
    public float Radius => Surface == 16 ? 2500f : Surface is >= 15 and <= 18 ? 800f : 350f;
    public bool Rotates => Surface is >= 15 and <= 18;
}

/// <summary>A page-owned1932E0 geometry cache. Accepted camera matrices consume
/// its world-space vertices; only relevant zoom/terrain/path changes rebuild.
/// Corners retain the terminal entry; the native decal consumer draws Count-1.
/// Endpoint registration consumes the actual first/last Hex, bank and channel.
/// Native owns the special endpoint's real per-draw rotation and shared refs.</summary>
public sealed class MapRoutePreviewGeometry
{
    private IReadOnlyList<MapRoutePreviewPath>? paths;
    private MainMapCameraTerrainSource? terrain;
    private bool? simplified;
    private float? width;
    public IReadOnlyList<MapRoutePreviewMesh> Meshes { get; private set; } = Array.Empty<MapRoutePreviewMesh>();
    public long Revision { get; private set; }

    public bool Update(IReadOnlyList<MapRoutePreviewPath> currentPaths, MainMapCameraTerrainSource currentTerrain,
        MainMapCameraFrame camera, MainMapCameraControllerParameters parameters)
    {
        var selection = CameraSelection(camera, parameters);
        bool shortPath = selection.DisplayBand >= 3;
        bool sameSpines = ReferenceEquals(terrain, currentTerrain) && simplified == shortPath;
        bool sameWidth = width == selection.FullWidth;
        if (ReferenceEquals(paths, currentPaths) && sameSpines && sameWidth) return false;
        var next = new List<MapRoutePreviewMesh>(currentPaths.Count);
        foreach (var path in currentPaths)
        {
            MapRoutePreviewMesh? previous = null;
            if (sameSpines)
                foreach (var mesh in Meshes)
                    if (ReferenceEquals(mesh.Path.Identity, path.Identity)) { previous = mesh; break; }
            if (previous is not null)
            {
                var endpoints=previous.Path.ListBank==path.ListBank&&previous.Path.EndpointChannel==path.EndpointChannel
                    ?previous.Endpoints:previous.Endpoints.Select(endpoint=>endpoint with
                        {ListBank=path.ListBank,Channel=path.EndpointChannel}).ToArray();
                next.Add(previous with { Path = path, FullWidth = selection.FullWidth, Endpoints=endpoints,
                    Ribbon = sameWidth || previous.Spine is null ? previous.Ribbon :
                        MapRouteRibbon.Build(previous.Spine.Spine.Span, selection.FullWidth) });
                continue;
            }
            if (path.Status != DeploymentDraftRouteStatus.Found || path.Hexes is not { Count: > 1 } hexes)
            {
                next.Add(new(path, null, null, selection.FullWidth, path.UnresolvedInput));
                continue;
            }
            int? firstSurface=shortPath?path.ControlStartSurfaceCode:path.StartSurfaceCode;
            int? lastSurface=shortPath?path.ControlGoalSurfaceCode:path.EndSurfaceCode;
            int? firstHex=shortPath?path.ControlStartHex:hexes[0];
            int? lastHex=shortPath?path.ControlGoalHex:hexes[^1];
            if (firstSurface is not int startSurface || lastSurface is not int endSurface ||
                firstHex is not int startHex || lastHex is not int goalHex)
            {
                next.Add(new(path, null, null, selection.FullWidth, path.UnresolvedInput ?? "route.preview.endpoint_surface"));
                continue;
            }
            try
            {
                //7F6C90's distant display uses two control points only after a
                //real computed path has more than one entry. NoPath stays empty.
                var centers = new Vector3[shortPath ? 2 : hexes.Count];
                for (int i = 0; i < centers.Length; i++)
                {
                    int hex = shortPath ? i == 0 ? startHex : goalHex : hexes[i];
                    if (hex is < 0 or >= 48400) throw new InvalidDataException("route.preview.path_hex");
                    var point = CeHexWorldCoordinates.Project(hex);
                    centers[i] = new((float)point.X, 0f, (float)point.Z);
                }
                var spine = MapRouteSpine.Build(centers, startSurface, endSurface, selection.DisplayBand, currentTerrain.Query);
                var (cornerVertices,cornerIndices)=BuildCorners(spine.Corners.Span);
                var endpoints=new List<MapRoutePreviewEndpoint>(2);
                if(spine.StartEndpoint is Vector3 start)endpoints.Add(new(startHex,start,startSurface,path.ListBank,path.EndpointChannel));
                if(spine.EndEndpoint is Vector3 end)endpoints.Add(new(goalHex,end,endSurface,path.ListBank,path.EndpointChannel));
                next.Add(new(path, spine, MapRouteRibbon.Build(spine.Spine.Span, selection.FullWidth), selection.FullWidth, null)
                {CornerVertices=cornerVertices,CornerIndices=cornerIndices,Endpoints=endpoints.AsReadOnly()});
            }
            catch (InvalidDataException missing) { next.Add(new(path, null, null, selection.FullWidth, missing.Message)); }
        }
        paths = currentPaths; terrain = currentTerrain; simplified = shortPath; width = selection.FullWidth;
        Meshes = next.AsReadOnly(); Revision++;
        return true;
    }

    //00F910 publishes four vertices per original bend.012D20 submits the
    //two native triangles, omitting the retained terminal corner entry.
    private static (MapRouteRibbonVertex[],uint[]) BuildCorners(ReadOnlySpan<Vector3> corners)
    {
        int count=Math.Max(0,corners.Length-1);
        var vertices=new MapRouteRibbonVertex[checked(count*4)];
        var indices=new uint[checked(count*6)];
        var (sin,cos)=ArmyAnimationSampler.SourceSinCos(BitConverter.Int32BitsToSingle(0x3FC90FDB));
        for(int i=0;i<count;i++)
        {
            float x=1f,z=0f;var center=corners[i];
            for(int j=0;j<4;j++)
            {
                float u=j is 1 or 2?1f:0f,v=j>=2?1f:0f;
                vertices[i*4+j]=new(F.Add(F.Multiply(x,200f),center.X),F.Add(center.Y,10f),
                    F.Add(F.Multiply(z,200f),center.Z),u,v);
                float oldX=x;
                x=F.Add(F.Multiply(sin,z),F.Multiply(cos,x));
                z=F.Subtract(F.Multiply(cos,z),F.Multiply(oldX,sin));
            }
            uint first=checked((uint)(i*4));int offset=i*6;
            indices[offset]=first;indices[offset+1]=first+1;indices[offset+2]=first+2;
            indices[offset+3]=first+2;indices[offset+4]=first+3;indices[offset+5]=first;
        }
        return(vertices,indices);
    }

    //187C60 consumes floor00ECB0, not the rounded camera band at an anchor.
    public static MapRouteCameraSelection CameraSelection(MainMapCameraFrame camera, MainMapCameraControllerParameters parameters)
    {
        float index = camera.ZoomSelection.NativeReturnedIndex;
        if (!float.IsFinite(index) || index < 0 || index > 6)
            throw new InvalidDataException("route.preview.camera_display_band");
        int band = (int)MathF.Floor(index), previous = Math.Max(band - 1, 0);
        float distance = (float)camera.ControllerState.Distance90;
        float upperWidth = Width(band);
        if (MathF.Abs(F.Subtract(distance, parameters.ZoomAnchor(band))) < 1.1920929e-05f) return new(band, upperWidth);
        float lowerWidth = Width(previous);
        if (MathF.Abs(F.Subtract(lowerWidth, upperWidth)) < 1.1920929e-05f) return new(band, upperWidth);
        float ratio = F.Divide(F.Subtract(distance, parameters.ZoomAnchor(previous)),
            F.Subtract(parameters.ZoomAnchor(band), parameters.ZoomAnchor(previous)));
        return new(band, F.Add(F.Multiply(ratio, F.Subtract(upperWidth, lowerWidth)), lowerWidth));

        static float Width(int b) => F.Multiply(F.Divide(b switch
        { 0 or 1 => 100f, 2 => 150f, 3 => 250f, 4 => 450f, _ => 600f }, 100f), 100f);
    }
}

public readonly record struct MapRouteCameraSelection(int DisplayBand, float FullWidth);

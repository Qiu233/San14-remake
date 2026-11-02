using San14.Data;

public partial class MapSceneView
{
    private IReadOnlyList<MapRoutePreviewPath> _deploymentRoutePaths = Array.Empty<MapRoutePreviewPath>();
    private MapRoutePreviewGeometry _deploymentRouteGeometry = new();
    private D3D11RoutePublication? _deploymentRoutePublication;
    private D3D11RoutePublication? _preparedRouteSource, _preparedRoutePublication;
    private ulong _deploymentRouteRevision;
    private ulong? _routeResourceContextRevision;
    private string? _routeResourceRoot;
    private string? _routeResourceDirectory;
    private MapRouteStaticResources? _routeStaticResources;

    internal void SetDeploymentRoutePreviews(IReadOnlyList<MapRoutePreviewPath> paths)
    {
        if (ReferenceEquals(paths, _deploymentRoutePaths)) return;
        _deploymentRoutePaths = paths;
        ScheduleD3D11MapFrame();
    }

    private D3D11RoutePublication? CaptureDeploymentRoutes(MainMapCameraFrame frame)
    {
        if (_deploymentRoutePaths.Count == 0 && _deploymentRoutePublication is null) return null;
        if (_sceneTerrain?.WorldGroundHeight is not { } terrain || _sourceMainCamera is not { } camera)
            throw new InvalidOperationException("Route drawing requires the Scene Landscape world Field and current camera owner.");
        bool changed = _deploymentRouteGeometry.Update(_deploymentRoutePaths, terrain, frame, camera.Parameters);
        var meshes = _deploymentRouteGeometry.Meshes;
        if (changed)
        {
            SetMeta("route_preview_unresolved", string.Join(";", meshes
                .Select(mesh => mesh.UnresolvedInput).Where(value => value is not null).Distinct()));
            _deploymentRoutePublication = new(++_deploymentRouteRevision,
                _routeResourceContextRevision ?? 0, _routeResourceDirectory, meshes);
        }
        return _deploymentRoutePublication;
    }

    // Resolve against this request's accepted season namespace, after its
    // registration. Retained requests keep their own immutable media directory.
    private D3D11RoutePublication? PrepareRouteResources(D3D11RoutePublication? publication)
    {
        if (publication is null || publication.IsEmpty) return publication;
        if (_routeResourceContextRevision != _d3d11ResourceContext!.Revision)
        {
            if (_routeStaticResources is { } loaded)
            {
                _routeResourceDirectory = loaded.Require(_d3d11ResourceContext).Directory;
                _routeResourceContextRevision = _d3d11ResourceContext.Revision;
            }
            else
            {
                // Explicit standalone consumers retain their independent import
                // path. The product opening requires its startup owner above.
                var resources = MapRouteDrawResources.Load(_repository, _d3d11ResourceContext);
                _routeResourceRoot ??= Path.Combine(Path.GetDirectoryName(_d3d11ResourceContextDirectory!)!,
                    "route-resources", Guid.NewGuid().ToString("N"));
                string root = Path.Combine(_routeResourceRoot,
                    resources.ContextRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
                MapRouteShaderNativeInputs.WriteNativeInputs(resources, root);
                _routeResourceDirectory = Path.Combine(root, "route-program-input");
                _routeResourceContextRevision = resources.ContextRevision;
            }
        }
        if (publication.ResourceGeneration == _routeResourceContextRevision &&
            publication.ResourceDirectory == _routeResourceDirectory) return publication;
        if (ReferenceEquals(publication, _preparedRouteSource) &&
            _preparedRoutePublication!.ResourceGeneration == _routeResourceContextRevision)
            return _preparedRoutePublication;
        _preparedRouteSource = publication;
        _preparedRoutePublication = publication.WithResources(++_deploymentRouteRevision,
            _routeResourceContextRevision!.Value, _routeResourceDirectory!);
        if (ReferenceEquals(publication, _deploymentRoutePublication))
            _deploymentRoutePublication = _preparedRoutePublication;
        return _preparedRoutePublication;
    }

    private void ReleaseDeploymentRoutes()
    {
        _deploymentRoutePaths = Array.Empty<MapRoutePreviewPath>();
        _deploymentRouteGeometry = new(); _deploymentRoutePublication = null;
        _preparedRouteSource = null; _preparedRoutePublication = null;
        _routeResourceContextRevision = null; _routeResourceRoot = null; _routeResourceDirectory = null;
        _routeStaticResources = null;
    }
}

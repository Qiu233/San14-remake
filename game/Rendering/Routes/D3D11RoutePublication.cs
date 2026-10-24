using System.Runtime.InteropServices;
using San14.Data;

/// <summary>Immutable geometry retained by a draw request. The page and terrain
/// cache produce it only when a route or its relevant zoom inputs change.</summary>
public sealed class D3D11RoutePublication
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct DrawSpan(uint First, uint Count, uint Topology, int ListBank, uint Role);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct Endpoint(int Hex, float X, float Y, float Z, int Surface, int ListBank, int Channel);

    internal MapRouteRibbonVertex[] Vertices { get; }
    internal DrawSpan[] Spans { get; }
    internal Endpoint[] Endpoints { get; }
    public ulong Revision { get; }
    public ulong ResourceGeneration { get; }
    public string? ResourceDirectory { get; }
    public bool IsEmpty => Spans.Length == 0 && Endpoints.Length == 0;

    private D3D11RoutePublication(D3D11RoutePublication source, ulong revision,
        ulong resourceGeneration, string resourceDirectory)
    {
        Vertices = source.Vertices; Spans = source.Spans; Endpoints = source.Endpoints;
        Revision = revision; ResourceGeneration = resourceGeneration; ResourceDirectory = resourceDirectory;
    }

    internal D3D11RoutePublication WithResources(ulong revision, ulong generation, string directory)
        => new(this, revision, generation, directory);

    internal D3D11RoutePublication(ulong revision, ulong resourceGeneration, string? resourceDirectory,
        IReadOnlyList<MapRoutePreviewMesh> meshes)
    {
        var vertices = new List<MapRouteRibbonVertex>();
        var spans = new List<DrawSpan>();
        var endpoints = new List<Endpoint>();
        foreach (var mesh in meshes)
        {
            if (mesh.UnresolvedInput is not null) continue;
            if (mesh.Ribbon is { } ribbon && !ribbon.Vertices.IsEmpty)
            {
                uint first = checked((uint)vertices.Count);
                vertices.AddRange(ribbon.Vertices.ToArray());
                spans.Add(new(first, checked((uint)vertices.Count - first), 5, mesh.Path.ListBank, 0));
            }
            if (!mesh.CornerIndices.IsEmpty)
            {
                uint first = checked((uint)vertices.Count);
                var corners = mesh.CornerVertices.Span;
                foreach (uint index in mesh.CornerIndices.Span)
                    vertices.Add(corners[checked((int)index)]);
                spans.Add(new(first, checked((uint)vertices.Count - first), 4, mesh.Path.ListBank, 1));
            }
            foreach (var endpoint in mesh.Endpoints)
                endpoints.Add(new(endpoint.Hex, endpoint.Center.X, endpoint.Center.Y, endpoint.Center.Z,
                    endpoint.Surface, endpoint.ListBank, endpoint.Channel));
        }
        Revision = revision;
        ResourceGeneration = resourceGeneration;
        ResourceDirectory = resourceDirectory;
        Vertices = vertices.ToArray(); Spans = spans.ToArray(); Endpoints = endpoints.ToArray();
    }
}

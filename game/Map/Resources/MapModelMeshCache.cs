using San14.Data;

/// <summary>Retains uploaded model geometry across scene reloads. Materials belong to each scene.</summary>
internal sealed class MapModelMeshCache : IDisposable
{
    private readonly record struct ResourceKey(string Uri, string DecodedHash);
    private readonly Dictionary<ResourceKey, ModelMeshResult> _meshes = [];

    public ModelMeshResult Get(LoadedModelTransforms source)
    {
        var key = new ResourceKey(source.Source.ResourceUri, source.Source.DecodedSha256);
        if (!_meshes.TryGetValue(key, out var mesh))
            _meshes.Add(key, mesh = ModelMesh.Build(source));
        return mesh;
    }

    public void Dispose()
    {
        foreach (var mesh in _meshes.Values) mesh.Mesh.Dispose();
        _meshes.Clear();
    }
}

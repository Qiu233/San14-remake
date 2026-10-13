using San14.Data;
using San14.Data.Formats;

/// <summary>Limits for retained static model uploads. Geometry counts one full
/// vertex/index buffer set per model/program combination, conservatively
/// allowing different programs to require separate device input layouts.</summary>
public sealed record NativeMapModelPopulationUploadLimits(long MaximumTextureBytes,
    long MaximumGeometryBytes, int MaximumPrograms, int MaximumVisibleDrawPackets);

/// <summary>Reserves a complete attempted upload before GPU work. Failed draws
/// may leave successful prefix uploads, so reservations remain until the cache
/// retires. A rejected reservation leaves all earlier accounting unchanged.</summary>
internal sealed class ModelUploadBudget(NativeMapModelPopulationUploadLimits limits)
{
    private readonly record struct GeometryKey(ModelResourceVersion Model, ModelProgramCacheKey Program);
    private readonly Dictionary<ModelTextureCacheKey, long> textures = [];
    private readonly Dictionary<GeometryKey, long> geometry = [];
    private readonly HashSet<ModelProgramCacheKey> programs = [];
    public long TextureBytes { get; private set; }
    public long GeometryBytes { get; private set; }

    public void ReserveAttempt(IReadOnlyList<ModelGbufferInstancePacket> packets)
    {
        if (packets.Count > limits.MaximumVisibleDrawPackets)
            throw new InvalidDataException("Complete visible model population exceeds the explicit draw-packet limit.");
        var nextTextures = new Dictionary<ModelTextureCacheKey, long>();
        var nextGeometry = new Dictionary<GeometryKey, long>();
        var nextPrograms = new HashSet<ModelProgramCacheKey>();
        foreach (var packet in packets)
        {
            var program = new ModelProgramCacheKey(ModelResourceVersion.From(packet.Shader.Source),
                packet.ExplicitCombinationIndex, packet.ArmySkinProfile);
            if (!programs.Contains(program)) nextPrograms.Add(program);
            var geometryKey = new GeometryKey(ModelResourceVersion.From(packet.Model.Source), program);
            if (!geometry.ContainsKey(geometryKey) && !nextGeometry.ContainsKey(geometryKey))
            {
                var model = packet.Model.Geometry;
                nextGeometry.Add(geometryKey, checked(model.VertexBuffers.Sum(b => b.Data.LongLength) +
                    model.IndexBuffers.Sum(b => b.Data.LongLength)));
            }
            foreach (var use in packet.Vertex.OwnedTextures.Concat(packet.Pixel.OwnedTextures)
                .Where(t => t.DescriptorKind == "Texture"))
            {
                var key = new ModelTextureCacheKey(ModelResourceVersion.From(use.Source.Source), use.ImageIndex, use.NativeViewFormat);
                if (textures.ContainsKey(key) || nextTextures.ContainsKey(key)) continue;
                var image = use.Source.Container.Textures[use.ImageIndex];
                if (image.LayerCount != 1 || image.SubresourceLayout != G1TextureSubresourceLayout.SingleLayerMipChain)
                    throw new InvalidDataException("Native population source texture requires its complete original single-layer mip chain.");
                nextTextures.Add(key, image.Mips.Sum(m => (long)m.BytesPerLayer));
            }
        }
        long textureBytes = checked(TextureBytes + nextTextures.Values.Sum());
        long geometryBytes = checked(GeometryBytes + nextGeometry.Values.Sum());
        if (textureBytes > limits.MaximumTextureBytes || geometryBytes > limits.MaximumGeometryBytes ||
            checked(programs.Count + nextPrograms.Count) > limits.MaximumPrograms)
            throw new InvalidDataException("Complete model source uploads exceed the explicit cache limits; no population subset was substituted.");
        foreach (var item in nextTextures) textures.Add(item.Key, item.Value);
        foreach (var item in nextGeometry) geometry.Add(item.Key, item.Value);
        programs.UnionWith(nextPrograms);
        TextureBytes = textureBytes; GeometryBytes = geometryBytes;
    }

    public void Clear()
    {
        textures.Clear(); geometry.Clear(); programs.Clear(); TextureBytes = 0; GeometryBytes = 0;
    }
}

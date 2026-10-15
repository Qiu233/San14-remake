using San14.Data;

public sealed record MapMaterialTerrainConsumer(string DrawKey, MapMaterialIdSource Material,
    TerrainGbufferConstantPacket PixelConstants);
public sealed record MapMaterialCacheConsumer(string DrawKey, string ShaderUri, int CombinationIndex,
    int MaterialId, int TableIndex, uint PackedTableWord, MapMaterialIdBranch Branch);
public sealed record MapMaterialCachePrepared(MapMaterialCacheSource Cache,
    IReadOnlyList<MapMaterialCacheConsumer> Consumers)
{
    public LoadedDeferredMaterialTable Table => Cache.Table;
    public LoadedMapMaterialCacheSelectedSource? SelectedSource { get; init; }
}

/// <summary>Validates the initialized original uint cache against the actual
/// selected model and terrain G-buffer constant packets. The returned table is
/// directly consumed by OriginalNativeDeferredResources/its t25 uploader.</summary>
public static class MapMaterialCacheProvider
{
    public static MapMaterialCachePrepared PrepareSourceNativeMap(GameResourceRepository repository,
        LoadedDeferredTexturePlan deferred, string freshCacheIdentity,
        IReadOnlyList<ModelGbufferInstancePacket> modelPackets,
        IReadOnlyList<MapMaterialTerrainConsumer> terrainPackets,
        Func<ModelGbufferInstancePacket, MapMaterialIdSource?>? explicitCurrentModelMaterial = null)
    {
        var selected = MapMaterialCacheSelectedSources.LoadNativeMap(repository, deferred);
        var cache = MapMaterialCacheSelectedSources.CreateFreshSelected(freshCacheIdentity, selected);
        return Prepare(cache, modelPackets, terrainPackets, explicitCurrentModelMaterial) with { SelectedSource = selected };
    }

    public static MapMaterialCachePrepared PrepareFreshDefault(LoadedShaderContainer deferredShader,
        int explicitDeferredCombination, string freshCacheIdentity,
        IReadOnlyList<ModelGbufferInstancePacket> modelPackets,
        IReadOnlyList<MapMaterialTerrainConsumer> terrainPackets,
        Func<ModelGbufferInstancePacket, MapMaterialIdSource?>? explicitCurrentModelMaterial = null)
        => Prepare(MapMaterialCacheSources.CreateFreshDefault(deferredShader,
            explicitDeferredCombination, freshCacheIdentity), modelPackets, terrainPackets, explicitCurrentModelMaterial);

    public static MapMaterialCachePrepared Prepare(MapMaterialCacheSource initializedCache,
        IReadOnlyList<ModelGbufferInstancePacket> modelPackets,
        IReadOnlyList<MapMaterialTerrainConsumer> terrainPackets,
        Func<ModelGbufferInstancePacket, MapMaterialIdSource?>? explicitCurrentModelMaterial = null)
    {
        ArgumentNullException.ThrowIfNull(initializedCache); ArgumentNullException.ThrowIfNull(modelPackets);
        ArgumentNullException.ThrowIfNull(terrainPackets);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var consumers = new List<MapMaterialCacheConsumer>(modelPackets.Count + terrainPackets.Count);
        foreach (var packet in modelPackets)
        {
            if (packet is null || string.IsNullOrWhiteSpace(packet.InstanceKey) || !keys.Add(packet.InstanceKey))
                throw new InvalidDataException("Original material cache requires unique actual model draw packet keys.");
            var plan = packet.Shader.ReadModelMaterialPlan(packet.Model, packet.ExplicitCombinationIndex, packet.DrawIndex);
            var source = plan.Source;
            var current = explicitCurrentModelMaterial?.Invoke(packet);
            if (current is not null)
            {
                MatchShader(current, packet.Shader, packet.ExplicitCombinationIndex);
                if (current.Branch != MapMaterialIdBranch.ExplicitCurrentValue)
                    throw new InvalidDataException("Model material override requires an explicit current binding source.");
                source = current;
            }
            if (source.MaterialId is null)
                throw new InvalidDataException($"{packet.InstanceKey}: absent named nMtrID requires its explicit retained/current source.");
            int actual = plan.Layout.ReadCurrent(packet.Pixel.Constants);
            consumers.Add(Consume(initializedCache, packet.InstanceKey, source, actual));
        }
        foreach (var packet in terrainPackets)
        {
            if (packet is null || string.IsNullOrWhiteSpace(packet.DrawKey) || !keys.Add(packet.DrawKey))
                throw new InvalidDataException("Original material cache requires unique actual terrain draw packet keys.");
            var source = packet.Material;
            if (source.MaterialId is null || source.Branch == MapMaterialIdBranch.SourceNamedParameter)
                throw new InvalidDataException("Terrain material ID requires a fresh original GBuffer default or explicit current value.");
            int actual = source.Shader.ReadMaterialIdLayout(source.CombinationIndex)
                .ReadCurrent(new Dictionary<uint, ReadOnlyMemory<byte>> { [0] = packet.PixelConstants.Bytes });
            consumers.Add(Consume(initializedCache, packet.DrawKey, source, actual));
        }
        return new(initializedCache, consumers.AsReadOnly());
    }

    private static MapMaterialCacheConsumer Consume(MapMaterialCacheSource cache,
        string drawKey, MapMaterialIdSource source, int actual)
    {
        if (actual != source.MaterialId)
            throw new InvalidDataException($"{drawKey}: actual G-buffer nMtrID {actual} differs from its prepared source {source.MaterialId}.");
        // The supported source writes an8-bit table index in the upper byte of
        // the normalized16-bit G-buffer W. Avoid aliasing or out-of-bounds loads.
        if ((uint)actual > byte.MaxValue)
            throw new InvalidDataException($"{drawKey}: material ID {actual} exceeds the supported source G-buffer index range.");
        uint word = cache.ReadWord(actual);
        return new(drawKey, source.Shader.Source.ResourceUri, source.CombinationIndex, actual, actual, word, source.Branch);
    }

    private static void MatchShader(MapMaterialIdSource material, LoadedShaderContainer shader, int combination)
    {
        if (material.Shader.Source.ResourceUri != shader.Source.ResourceUri ||
            material.Shader.Source.DecodedSha256 != shader.Source.DecodedSha256 || material.CombinationIndex != combination ||
            string.IsNullOrWhiteSpace(material.OwnerIdentity) || string.IsNullOrWhiteSpace(material.EvidenceUri) ||
            material.EvidenceSha256.Length != 64 || !material.EvidenceSha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Explicit current material binding belongs to another original shader/owner or lacks evidence.");
    }
}

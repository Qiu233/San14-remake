using System.Buffers.Binary;
using San14.Data;

public sealed record OriginalMapMaterialTerrainConsumer(string DrawKey, OriginalMapMaterialIdSource Material,
    TerrainGbufferConstantPacket PixelConstants);
public sealed record OriginalMapMaterialCacheConsumer(string DrawKey, string ShaderUri, int CombinationIndex,
    int MaterialId, int TableIndex, uint PackedTableWord, OriginalMapMaterialIdBranch Branch);
public sealed record OriginalMapMaterialCachePrepared(OriginalMapMaterialCacheSource Cache,
    IReadOnlyList<OriginalMapMaterialCacheConsumer> Consumers)
{
    public LoadedOriginalDeferredMaterialTable Table => Cache.Table;
    public LoadedOriginalMapMaterialCacheSelectedSource? SelectedSource { get; init; }
}

/// <summary>Validates the initialized original uint cache against the actual
/// selected model and terrain G-buffer constant packets. The returned table is
/// directly consumed by OriginalNativeDeferredResources/its t25 uploader.</summary>
public static class OriginalMapMaterialCacheProvider
{
    public static OriginalMapMaterialCachePrepared PrepareSourceNativeMap(OriginalGameRepository repository,
        LoadedOriginalDeferredTexturePlan deferred, string freshCacheIdentity,
        IReadOnlyList<OriginalModelGbufferInstancePacket> modelPackets,
        IReadOnlyList<OriginalMapMaterialTerrainConsumer> terrainPackets,
        Func<OriginalModelGbufferInstancePacket, OriginalMapMaterialIdSource?>? explicitCurrentModelMaterial = null)
    {
        var selected = OriginalMapMaterialCacheSelectedSources.LoadNativeMap(repository, deferred);
        var cache = OriginalMapMaterialCacheSelectedSources.CreateFreshSelected(freshCacheIdentity, selected);
        return Prepare(cache, modelPackets, terrainPackets, explicitCurrentModelMaterial) with { SelectedSource = selected };
    }

    public static OriginalMapMaterialCachePrepared PrepareFreshDefault(LoadedShaderContainer deferredShader,
        int explicitDeferredCombination, string freshCacheIdentity,
        IReadOnlyList<OriginalModelGbufferInstancePacket> modelPackets,
        IReadOnlyList<OriginalMapMaterialTerrainConsumer> terrainPackets,
        Func<OriginalModelGbufferInstancePacket, OriginalMapMaterialIdSource?>? explicitCurrentModelMaterial = null)
        => Prepare(OriginalMapMaterialCacheSources.CreateFreshDefault(deferredShader,
            explicitDeferredCombination, freshCacheIdentity), modelPackets, terrainPackets, explicitCurrentModelMaterial);

    public static OriginalMapMaterialCachePrepared Prepare(OriginalMapMaterialCacheSource initializedCache,
        IReadOnlyList<OriginalModelGbufferInstancePacket> modelPackets,
        IReadOnlyList<OriginalMapMaterialTerrainConsumer> terrainPackets,
        Func<OriginalModelGbufferInstancePacket, OriginalMapMaterialIdSource?>? explicitCurrentModelMaterial = null)
    {
        ArgumentNullException.ThrowIfNull(initializedCache); ArgumentNullException.ThrowIfNull(modelPackets);
        ArgumentNullException.ThrowIfNull(terrainPackets);
        var sources = new Dictionary<(string Model, string ModelSha, int Draw, string Shader, string ShaderSha, int Combination), OriginalMapMaterialIdSource>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var consumers = new List<OriginalMapMaterialCacheConsumer>(modelPackets.Count + terrainPackets.Count);
        foreach (var packet in modelPackets)
        {
            if (packet is null || string.IsNullOrWhiteSpace(packet.InstanceKey) || !keys.Add(packet.InstanceKey))
                throw new InvalidDataException("Original material cache requires unique actual model draw packet keys.");
            var key = (packet.Model.Source.ResourceUri, packet.Model.Source.DecodedSha256, packet.DrawIndex,
                packet.Shader.Source.ResourceUri, packet.Shader.Source.DecodedSha256, packet.ExplicitCombinationIndex);
            if (!sources.TryGetValue(key, out var source))
                sources[key] = source = OriginalMapMaterialIdSources.ReadModel(packet.Shader,
                    packet.ExplicitCombinationIndex, packet.Model, packet.DrawIndex);
            var current = explicitCurrentModelMaterial?.Invoke(packet);
            if (current is not null)
            {
                MatchShader(current, packet.Shader, packet.ExplicitCombinationIndex);
                if (current.Branch != OriginalMapMaterialIdBranch.ExplicitCurrentValue)
                    throw new InvalidDataException("Model material override requires an explicit current binding source.");
                source = current;
            }
            if (source.MaterialId is null)
                throw new InvalidDataException($"{packet.InstanceKey}: absent named nMtrID requires its explicit retained/current source.");
            var stage = packet.Shader.ReadG1ResourceBindings(packet.ExplicitCombinationIndex).Combination.Stages
                .Single(s => s.StageCode == 0 && s.Entry is not null);
            var definitions = packet.Shader.ReadResourceDefinitions(stage.Entry!.ProgramIndex);
            int actual = ReadId(definitions, packet.Pixel.Constants);
            consumers.Add(Consume(initializedCache, packet.InstanceKey, source, actual));
        }
        foreach (var packet in terrainPackets)
        {
            if (packet is null || string.IsNullOrWhiteSpace(packet.DrawKey) || !keys.Add(packet.DrawKey))
                throw new InvalidDataException("Original material cache requires unique actual terrain draw packet keys.");
            var source = packet.Material;
            if (source.MaterialId is null || source.Branch == OriginalMapMaterialIdBranch.SourceNamedParameter)
                throw new InvalidDataException("Terrain material ID requires a fresh original GBuffer default or explicit current value.");
            var stage = source.Shader.ReadG1ResourceBindings(source.CombinationIndex).Combination.Stages
                .Single(s => s.StageCode == 0 && s.Entry is not null);
            var definitions = source.Shader.ReadResourceDefinitions(stage.Entry!.ProgramIndex);
            int actual = ReadId(definitions, new Dictionary<uint, ReadOnlyMemory<byte>> { [0] = packet.PixelConstants.Bytes });
            consumers.Add(Consume(initializedCache, packet.DrawKey, source, actual));
        }
        return new(initializedCache, consumers.AsReadOnly());
    }

    private static OriginalMapMaterialCacheConsumer Consume(OriginalMapMaterialCacheSource cache,
        string drawKey, OriginalMapMaterialIdSource source, int actual)
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

    private static int ReadId(San14.Data.Formats.DxbcResourceDefinitions definitions,
        IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> constants)
    {
        var buffers = definitions.Buffers.Where(b => b.Variables.Any(v => v.Name.Text == "nMtrID")).ToArray();
        if (buffers.Length != 1) throw new InvalidDataException("Material packet nMtrID buffer is ambiguous.");
        var buffer = buffers[0];
        var binding = definitions.Bindings.Single(b => b.InputType == 0 && b.Name.Text == buffer.Name.Text);
        if (!constants.TryGetValue(binding.BindPoint, out var bytes) || bytes.Length != buffer.ByteSize)
            throw new InvalidDataException("Material cache requires the actual complete selected PS constant buffer.");
        var id = buffer.Variables.Single(v => v.Name.Text == "nMtrID");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.Span.Slice(checked((int)id.StartOffset), 4));
    }

    private static void MatchShader(OriginalMapMaterialIdSource material, LoadedShaderContainer shader, int combination)
    {
        if (material.Shader.Source.ResourceUri != shader.Source.ResourceUri ||
            material.Shader.Source.DecodedSha256 != shader.Source.DecodedSha256 || material.CombinationIndex != combination ||
            string.IsNullOrWhiteSpace(material.OwnerIdentity) || string.IsNullOrWhiteSpace(material.EvidenceUri) ||
            material.EvidenceSha256.Length != 64 || !material.EvidenceSha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Explicit current material binding belongs to another original shader/owner or lacks evidence.");
    }
}

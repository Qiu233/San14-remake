using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using San14.Data;
using San14.Data.Formats;

/// <summary>Scene-revision validator for immutable source programs/materials.
/// Owns canonical source byte snapshots; checks each used blob once per frame.
/// Plans include source, program combination, draw, index and override identities.
/// Dynamic PS IDs are still read from every actual complete packet each frame.</summary>
public sealed class MapMaterialCacheFrameValidator
{
    private readonly string _revision;
    private readonly Dictionary<string, byte[]> _sources = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Model, string Shader, int Draw, int Combination), Plan> _plans = [];
    private readonly ConditionalWeakTable<LoadedRdbAsset, SourceKey> _keys = new();
    private sealed record SourceKey(string Identity);
    private sealed record Plan(MapMaterialIdSource Material, uint Register, int BufferBytes, int IdOffset);
    public int CachedPlanCount => _plans.Count;
    public int CachedSourceCount => _sources.Count;
    public long SourceValidationCount { get; private set; }
    public MapMaterialCacheFrameValidator(string explicitSourceRevision)
    { ArgumentException.ThrowIfNullOrWhiteSpace(explicitSourceRevision); _revision = explicitSourceRevision; }

    public MapMaterialCachePrepared Prepare(MapMaterialCacheSource initializedCache,
        IReadOnlyList<ModelGbufferInstancePacket> packets)
    {
        ArgumentNullException.ThrowIfNull(initializedCache); ArgumentNullException.ThrowIfNull(packets);
        var checkedSources = new HashSet<(string Key, byte[] Bytes)>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var consumers = new List<MapMaterialCacheConsumer>(packets.Count);
        foreach (var packet in packets)
        {
            if (packet is null || string.IsNullOrWhiteSpace(packet.InstanceKey) || !keys.Add(packet.InstanceKey))
                throw new InvalidDataException("Cached material validation requires unique actual draw keys.");
            string modelKey = Key(packet.Model.Source), shaderKey = Key(packet.Shader.Source);
            byte[] modelBytes = Source(packet.Model.Source, modelKey, checkedSources);
            byte[] shaderBytes = Source(packet.Shader.Source, shaderKey, checkedSources);
            var key = (modelKey, shaderKey, packet.DrawIndex, packet.ExplicitCombinationIndex);
            if (!_plans.TryGetValue(key, out var plan))
            {
                // Never parse caller-mutable geometry/container metadata into a
                // plan. The owned byte snapshots are the canonical identities.
                var model = new LoadedModelGeometry(G1ModelGeometryReader.Read(modelBytes, modelBytes.LongLength),
                    packet.Model.Source with { Bytes = modelBytes });
                var shader = new LoadedShaderContainer(G2ShaderContainerReader.Read(shaderBytes),
                    packet.Shader.Source with { Bytes = shaderBytes });
                var material = MapMaterialIdSources.ReadModel(shader, packet.ExplicitCombinationIndex, model, packet.DrawIndex);
                if (material.MaterialId is null)
                    throw new InvalidDataException("Cached model source has no explicit named material ID.");
                var stage = shader.ReadG1ResourceBindings(packet.ExplicitCombinationIndex).Combination.Stages
                    .Single(s => s.StageCode == 0 && s.Entry is not null);
                var definitions = shader.ReadResourceDefinitions(stage.Entry!.ProgramIndex);
                var buffer = definitions.Buffers.Single(b => b.Variables.Any(v => v.Name.Text == "nMtrID"));
                var binding = definitions.Bindings.Single(b => b.InputType == 0 && b.Name.Text == buffer.Name.Text);
                var id = buffer.Variables.Single(v => v.Name.Text == "nMtrID");
                _plans.Add(key, plan = new(material, binding.BindPoint, checked((int)buffer.ByteSize), checked((int)id.StartOffset)));
            }
            if (!packet.Pixel.Constants.TryGetValue(plan.Register, out var bytes) || bytes.Length != plan.BufferBytes)
                throw new InvalidDataException("Cached material validation requires the actual complete selected PS buffer.");
            int actual = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span.Slice(plan.IdOffset, 4));
            if (actual != plan.Material.MaterialId || (uint)actual > byte.MaxValue)
                throw new InvalidDataException("Actual model material ID differs from its immutable source plan or supported index range.");
            consumers.Add(new(packet.InstanceKey, packet.Shader.Source.ResourceUri, packet.ExplicitCombinationIndex,
                actual, actual, initializedCache.ReadWord(actual), MapMaterialIdBranch.SourceNamedParameter));
        }
        return new(initializedCache, consumers.AsReadOnly());
    }

    private byte[] Source(LoadedRdbAsset source, string key, HashSet<(string Key, byte[] Bytes)> checkedSources)
    {
        if (!_sources.TryGetValue(key, out var owned))
        {
            owned = source.Bytes.ToArray();
            if (!Convert.ToHexString(SHA256.HashData(owned)).Equals(source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Cached material source bytes differ from their declared hash.");
            _sources.Add(key, owned);
        }
        if (checkedSources.Add((key, source.Bytes)))
        {
            if (!source.Bytes.AsSpan().SequenceEqual(owned))
                throw new InvalidDataException("Caller changed a cached immutable material/program source blob.");
            SourceValidationCount++;
        }
        return owned;
    }

    private string Key(LoadedRdbAsset source) => _keys.GetValue(source, s => new(string.Join("|", _revision, s.ResourceUri,
        s.DecodedSha256, s.FileId.ToString("X8"), s.Bytes.LongLength,
        s.IndexSource.ResourceUri, s.IndexSource.PhysicalPath, s.IndexSource.Sha256,
        s.OverrideSource?.ResourceUri, s.OverrideSource?.PhysicalPath, s.OverrideSource?.Sha256))).Identity;
    public void Invalidate() { _plans.Clear(); _sources.Clear(); }
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum OriginalMapMaterialCacheBranch
{
    FreshOptionalNullProvider,
    FreshExternalSource,
    ExplicitPreparedExistingCache
}

/// <summary>The four byte source records consumed by D59F70/D59240. These are
/// explicit prepared CPU-source records, not model 10001 specular records.</summary>
public sealed record OriginalMapMaterialCacheExternalSource(string SourceIdentity,
    string EvidenceUri, string EvidenceSha256, ReadOnlyMemory<byte> FourByteRecords);

/// <summary>An initialized native uint table. A current cache is imported only
/// with its identity and complete explicitly prepared contents.</summary>
public sealed class OriginalMapMaterialCacheSource
{
    private readonly byte[] _bytes;
    private readonly LoadedOriginalDeferredMaterialTable _declaration;
    public string CacheIdentity { get; }
    public OriginalMapMaterialCacheBranch Branch { get; }
    public string? ExternalSourceIdentity { get; }
    public int EntryCount => _bytes.Length / 4;
    public string UploadSha256 { get; }
    public bool Initialized => true;
    public LoadedOriginalDeferredMaterialTable Table
    {
        get
        {
            OriginalMapMaterialCacheSources.CanonicalShader(_declaration.Shader);
            return _declaration with { Bytes = _bytes.ToArray() };
        }
    }

    internal OriginalMapMaterialCacheSource(string identity, OriginalMapMaterialCacheBranch branch,
        string? sourceIdentity, LoadedOriginalDeferredMaterialTable table)
    {
        CacheIdentity = identity; Branch = branch; ExternalSourceIdentity = sourceIdentity;
        _bytes = table.Bytes.ToArray(); _declaration = table with { Bytes = [], EntryCount = _bytes.Length / 4 };
        UploadSha256 = Convert.ToHexString(SHA256.HashData(_bytes));
    }

    public uint ReadWord(int tableIndex)
    {
        if ((uint)tableIndex >= EntryCount)
            throw new InvalidDataException($"Material table index {tableIndex} exceeds initialized cache {CacheIdentity} ({EntryCount} rows).");
        return BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(tableIndex * 4, 4));
    }

    public int ExternalRecordTableIndex(int sourceRecordIndex)
    {
        if (Branch != OriginalMapMaterialCacheBranch.FreshExternalSource ||
            (uint)sourceRecordIndex >= EntryCount - 1)
            throw new InvalidDataException("Material index allocation requires a record of this exact external source.");
        // D59F70 starts the original ordered source copy at table index1.
        return checked(sourceRecordIndex + 1);
    }
}

/// <summary>EC7B60/EC6FB0 optional-null cache, D59F70/D59240 external-source
/// constructor and A00760/9FF740 uint packing. Existing cached buffers are
/// returned without initialization; their unknown contents cannot become row0.</summary>
public static class OriginalMapMaterialCacheSources
{
    public static OriginalMapMaterialCacheSource CreateFreshDefault(LoadedShaderContainer deferredShader,
        int explicitCombination, string freshCacheIdentity)
    {
        Identity(freshCacheIdentity);
        CanonicalShader(deferredShader);
        // Models' material records do not allocate this cache. The original
        // optional-null provider requests one row and initializes row0 to0.
        return new(freshCacheIdentity, OriginalMapMaterialCacheBranch.FreshOptionalNullProvider, null,
            OriginalDeferredMaterialTable.Create(deferredShader, explicitCombination, []));
    }

    public static OriginalMapMaterialCacheSource CreateFreshExternal(LoadedShaderContainer deferredShader,
        int explicitCombination, string freshCacheIdentity, OriginalMapMaterialCacheExternalSource source)
    {
        Identity(freshCacheIdentity); ArgumentNullException.ThrowIfNull(source);
        Identity(source.SourceIdentity); Evidence(source.EvidenceUri, source.EvidenceSha256);
        CanonicalShader(deferredShader);
        byte[] records = source.FourByteRecords.ToArray();
        if ((records.Length & 3) != 0)
            throw new InvalidDataException("Original external material source requires complete four byte records.");
        var entries = new OriginalDeferredMaterialEntry[records.Length / 4];
        for (int i = 0; i < entries.Length; i++)
        {
            int at = i * 4;
            entries[i] = new(records[at], records[at + 1], records[at + 2], records[at + 3]);
        }
        return new(freshCacheIdentity, OriginalMapMaterialCacheBranch.FreshExternalSource, source.SourceIdentity,
            OriginalDeferredMaterialTable.Create(deferredShader, explicitCombination, entries));
    }

    public static OriginalMapMaterialCacheSource ReadExplicitExisting(LoadedShaderContainer deferredShader,
        int explicitCombination, string currentCacheIdentity, bool? explicitlyInitialized,
        ReadOnlyMemory<byte> completePreparedUintRows)
    {
        Identity(currentCacheIdentity); CanonicalShader(deferredShader);
        if (explicitlyInitialized != true || completePreparedUintRows.Length < 4 ||
            (completePreparedUintRows.Length & 3) != 0)
            throw new InvalidDataException("Existing original material cache requires explicitly initialized complete uint rows.");
        var declaration = OriginalDeferredMaterialTable.Create(deferredShader, explicitCombination, []);
        byte[] rows = completePreparedUintRows.ToArray();
        return new(currentCacheIdentity, OriginalMapMaterialCacheBranch.ExplicitPreparedExistingCache, null,
            declaration with { Bytes = rows, EntryCount = rows.Length / 4 });
    }

    internal static void CanonicalShader(LoadedShaderContainer shader)
    {
        ArgumentNullException.ThrowIfNull(shader);
        if (!Convert.ToHexString(SHA256.HashData(shader.Source.Bytes)).Equals(
            shader.Source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Material shader source bytes differ from their declared hash.");
        G2ShaderContainer parsed = G2ShaderContainerReader.Read(shader.Source.Bytes);
        if (!parsed.RawHeader.AsSpan().SequenceEqual(shader.Container.RawHeader))
            throw new InvalidDataException("Material shader source header differs from its container.");
    }

    internal static void Identity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            throw new InvalidDataException("Original material cache or current owner identity is unresolved.");
    }

    internal static void Evidence(string uri, string sha)
    {
        Identity(uri);
        if (sha.Length != 64 || !sha.All(Uri.IsHexDigit))
            throw new InvalidDataException("Original material binding requires its explicit evidence SHA256.");
    }
}

public enum OriginalMapMaterialIdBranch { SourceNamedParameter, FreshGbufferDefault, ExplicitCurrentValue }
public sealed record OriginalMapMaterialIdSource(LoadedShaderContainer Shader,
    G1ShaderConfigurationRecord Gbuffer, int CombinationIndex, int? MaterialId,
    OriginalMapMaterialIdBranch Branch, string OwnerIdentity, string EvidenceUri, string EvidenceSha256);

/// <summary>Connects the original GBuffer plugin declaration to its successful
/// initialization source, or to the model's separately applied named parameter.
/// Shader config flags remain raw; none are reinterpreted as lighting values.</summary>
public static class OriginalMapMaterialIdSources
{
    public static OriginalMapMaterialIdSource ReadModel(LoadedShaderContainer shader, int explicitCombination,
        LoadedModelGeometry model, int drawIndex)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!Convert.ToHexString(SHA256.HashData(model.Source.Bytes)).Equals(
            model.Source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Material-ID model bytes differ from their declared hash.");
        var geometry = G1ModelGeometryReader.Read(model.Source.Bytes, model.Source.Bytes.LongLength);
        var materials = G1ModelDrawMaterialReader.Read(geometry);
        if ((uint)drawIndex >= materials.Draws.Count) throw new ArgumentOutOfRangeException(nameof(drawIndex));
        var material = OriginalModelGbufferMaterialSourceReader.Read(shader, materials.Draws[drawIndex]);
        var config = ReadGbuffer(shader, explicitCombination);
        return new(shader, config, explicitCombination, material.MaterialId, OriginalMapMaterialIdBranch.SourceNamedParameter,
            model.Source.ResourceUri + "|draw:" + drawIndex, model.Source.ResourceUri, model.Source.DecodedSha256);
    }

    public static OriginalMapMaterialIdSource CreateFreshTerrainDefault(LoadedTerrainShaderResources terrain,
        int explicitCombination, string freshInterfaceIdentity)
    {
        ArgumentNullException.ThrowIfNull(terrain); OriginalMapMaterialCacheSources.Identity(freshInterfaceIdentity);
        var config = ReadGbuffer(terrain.Shader, explicitCombination);
        // 9A7D70/9A6E40 successful default callback writes the first plugin
        // field nMtrID=0. The drawID callback only writes the second field.
        return new(terrain.Shader, config, explicitCombination, 0, OriginalMapMaterialIdBranch.FreshGbufferDefault,
            freshInterfaceIdentity, terrain.Shader.Source.ResourceUri, terrain.Shader.Source.DecodedSha256);
    }

    public static OriginalMapMaterialIdSource FromExplicitCurrent(LoadedShaderContainer shader,
        int explicitCombination, string currentInterfaceIdentity, int materialId, string evidenceUri, string evidenceSha256)
    {
        OriginalMapMaterialCacheSources.Identity(currentInterfaceIdentity);
        OriginalMapMaterialCacheSources.Evidence(evidenceUri, evidenceSha256);
        return new(shader, ReadGbuffer(shader, explicitCombination), explicitCombination, materialId,
            OriginalMapMaterialIdBranch.ExplicitCurrentValue, currentInterfaceIdentity, evidenceUri, evidenceSha256);
    }

    private static G1ShaderConfigurationRecord ReadGbuffer(LoadedShaderContainer shader, int combination)
    {
        OriginalMapMaterialCacheSources.CanonicalShader(shader);
        var config = G1ShaderConfigurationReader.Read(shader.Source.Bytes, shader.Container).Records
            .FirstOrDefault(r => r.NameHash == 0xA3D05FFB && r.Name == "GBuffer") ??
            throw new InvalidDataException("Material-ID source requires the exact original GBuffer configuration.");
        var selected = shader.ReadG1ResourceBindings(combination).Combination;
        var pixel = selected.Stages.SingleOrDefault(s => s.StageCode == 0 && s.Entry is not null) ??
            throw new InvalidDataException("Material-ID source requires one selected original PS.");
        var definitions = shader.ReadResourceDefinitions(pixel.Entry!.ProgramIndex);
        var ids = definitions.Buffers.SelectMany(b => b.Variables).Where(v => v.Name.Text == "nMtrID").ToArray();
        if (ids.Length != 1 || ids[0].ByteSize != 4)
            throw new InvalidDataException("Material-ID source requires one complete nMtrID scalar.");
        var type = definitions.Types.Single(t => t.Offset == ids[0].TypeOffset);
        if (type.Class != 0 || type.BaseType != 2 || type.Rows != 1 || type.Columns != 1 || type.Elements != 0)
            throw new InvalidDataException("Material-ID source nMtrID is not the original int32 scalar.");
        return config;
    }
}

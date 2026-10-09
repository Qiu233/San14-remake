using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedMapMaterialCacheSelectedSource(LoadedDeferredTexturePlan Deferred,
    CompiledGraphNode Node, TaskGraphArgument MaterialObjectArgument, LoadedObjects MaterialObjects,
    DokObject MaterialObject, DokProperty ResourceReference, LoadedRdbAsset MaterialRecordsSource);

/// <summary>The actual F103 material-table named source. Ordinal19 selects an
/// F6F3AFBD object, whose KTGLGBufferMaterialIDTableResourceHash selects its CSV
/// source. The deferred constructor retains this resolved optional source.</summary>
public static class MapMaterialCacheSelectedSources
{
    public const uint MaterialObjectArgumentHash = 0x9826997D;
    public const uint MaterialObjectClass = 0xF6F3AFBD;
    public const uint MaterialResourcePropertyHash = 0x6143C32F;
    public const uint MaterialResourceType = 0x0BD05B27;

    public static MapMaterialCacheSource CreateFreshSelected(string freshCacheIdentity,
        LoadedMapMaterialCacheSelectedSource selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        Verify(selected.Deferred.Graph.Source); Verify(selected.MaterialObjects.Source);
        var actualNode = RenderGraphReader.Read(selected.Deferred.Graph.Source.Bytes).Nodes
            .SingleOrDefault(n => n.Index == selected.Deferred.Node.Index);
        var actualArgument = actualNode?.Call.Arguments.SingleOrDefault(a => a.NameHash == MaterialObjectArgumentHash);
        if (actualNode?.Call.FactoryClassId != RenderGraphShaderReferences.ClusteredDeferredFactoryClassId ||
            actualNode.Index != selected.Node.Index || actualArgument is null || actualArgument.DescriptorWord != 0x05000001 ||
            actualArgument.RawValue.Length != 4)
            throw new InvalidDataException("Prepared material selection differs from its canonical F103 graph node.");
        uint actualObjectId = BinaryPrimitives.ReadUInt32LittleEndian(actualArgument.RawValue);
        var actualObject = DokReader.Read(selected.MaterialObjects.Source.Bytes).Objects
            .SingleOrDefault(o => o.ObjectId == actualObjectId);
        var actualReference = actualObject?.Properties.SingleOrDefault(p => p.NameHash == MaterialResourcePropertyHash);
        if (actualObject is null || actualObject.Kind != DokObjectKind.Inline || actualObject.ClassId != MaterialObjectClass ||
            actualObjectId != selected.MaterialObject.ObjectId || actualReference is null || actualReference.TypeCode != 5 ||
            actualReference.ElementCount != 1 || actualReference.RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(actualReference.RawValue) != selected.MaterialRecordsSource.FileId)
            throw new InvalidDataException("Prepared material records differ from the canonical named object's declared resource.");
        var entries = MapMaterialCacheCodeSources.ReadSourceRecords(selected.MaterialRecordsSource);
        byte[] rows = new byte[checked(entries.Count * 4)];
        for (int i = 0; i < entries.Count; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(rows.AsSpan(i * 4, 4), entries[i].PackedWord);
        return MapMaterialCacheSources.CreateFreshExternal(selected.Deferred.Shader,
            selected.Deferred.ResourceBindings.Combination.Index, freshCacheIdentity,
            new(selected.MaterialObjects.Source.ResourceUri + "#" + selected.MaterialObject.ObjectId.ToString("X8"),
                selected.MaterialRecordsSource.ResourceUri, selected.MaterialRecordsSource.DecodedSha256, rows));
    }

    public static LoadedMapMaterialCacheSelectedSource LoadNativeMap(GameResourceRepository repository,
        LoadedDeferredTexturePlan deferred)
        => LoadDeclared(repository, deferred, repository.LoadObjects("KIDSSystemResource", 0xD956E4A2), "RRPreview");

    public static LoadedMapMaterialCacheSelectedSource LoadDeclared(GameResourceRepository repository,
        LoadedDeferredTexturePlan deferred, LoadedObjects declaredMaterialObjects, string explicitResourceArchive)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(deferred);
        ArgumentNullException.ThrowIfNull(declaredMaterialObjects);
        MapMaterialCacheSources.Identity(explicitResourceArchive);
        Verify(deferred.Graph.Source); Verify(declaredMaterialObjects.Source);
        MapMaterialCacheSources.CanonicalShader(deferred.Shader);
        var graph = RenderGraphReader.Read(deferred.Graph.Source.Bytes);
        var nodes = graph.Nodes.Where(n => n.Index == deferred.Node.Index).ToArray();
        if (nodes.Length != 1 || nodes[0].Call.FactoryClassId != RenderGraphShaderReferences.ClusteredDeferredFactoryClassId)
            throw new InvalidDataException("Material source selection requires this exact original F103 graph node.");
        var args = nodes[0].Call.Arguments.Where(a => a.NameHash == MaterialObjectArgumentHash).ToArray();
        if (args.Length != 1 || args[0].DescriptorWord != 0x05000001 || args[0].RawValue.Length != 4)
            throw new InvalidDataException("F103 source requires the original material-table object hash scalar.");
        uint objectId = BinaryPrimitives.ReadUInt32LittleEndian(args[0].RawValue);
        if (objectId == 0)
            throw new InvalidDataException("This source node has no external material object; select its explicit optional-null provider branch.");
        var document = DokReader.Read(declaredMaterialObjects.Source.Bytes);
        var matches = document.Objects.Where(o => o.ObjectId == objectId).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].ClassId != MaterialObjectClass)
            throw new InvalidDataException($"Selected original material table object {objectId:X8} is missing, ambiguous or inherited.");
        var fields = matches[0].Properties.Where(p => p.NameHash == MaterialResourcePropertyHash).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != 5 || fields[0].ElementCount != 1 || fields[0].RawValue.Length != 4)
            throw new InvalidDataException("Original material table requires its exact KTGLGBufferMaterialIDTableResourceHash scalar.");
        uint fileId = BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
        if (fileId == 0) throw new InvalidDataException("Original material table source resource is zero.");
        LoadedRdbAsset resource;
        try { resource = repository.LoadRdbAsset(explicitResourceArchive, fileId); }
        catch (KeyNotFoundException error)
        { throw new InvalidDataException($"Selected material source {explicitResourceArchive}/{fileId:X8} is missing.", error); }
        byte[] indexBytes = File.ReadAllBytes(resource.IndexSource.PhysicalPath);
        if (indexBytes.LongLength != resource.IndexSource.Bytes ||
            !Convert.ToHexString(SHA256.HashData(indexBytes)).Equals(resource.IndexSource.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Original material resource index changed after source resolution.");
        var records = RdbIndexReader.Read(indexBytes).Records.Where(r => r.FileId == fileId).ToArray();
        if (records.Length != 1 || records[0].TypeId != MaterialResourceType)
            throw new InvalidDataException("Original material table source requires its declared native resource type0BD05B27.");
        Verify(resource);
        return new(deferred, nodes[0], args[0], new(document, declaredMaterialObjects.Source), matches[0], fields[0], resource);
    }

    private static void Verify(LoadedRdbAsset asset)
    {
        if (!Convert.ToHexString(SHA256.HashData(asset.Bytes)).Equals(asset.DecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Selected original material source bytes differ from their declared hash.");
    }
}

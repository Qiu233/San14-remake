using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedModelShaderBindings(LoadedObjects Dictionary, DokObject ModelObject,
    LoadedModelMaterials Model, DokProperty BindingReference, LoadedRdbAsset BindingSource,
    OriginalShaderBindingCatalog Bindings);

/// <summary>Source declarations only. ClassId is retained; identical field names do not establish a creator.</summary>
public sealed record OriginalNamedModelShader(LoadedObjects Objects, DokObject Object,
    DokProperty G1SReference, uint ShaderFileHash, DokProperty DependencyReference,
    IReadOnlyList<uint> DependencyObjectHashes)
{
    // These are exact independently checked creators, not acceptance by field shape
    // or shared CPU vtable. 18EB/840 custom callbacks also publish type 0x10.
    public bool HasVerifiedType16Creator => Object.ClassId is
        0x8687085E or 0x048161D1 or 0x18EB1E56 or 0x840460B9 or 0x39E93192 or 0xCF1FF0C9;
}

public sealed record ResolvedModelShaderBinding(LoadedModelShaderBindings Model,
    uint PreparedPrimitiveIdentifier, OriginalShaderBindingRow Binding,
    OriginalNamedModelShader Shader)
{
    // Native F543E0 resolves the ObjectDB column. The ResourceDB column is retained
    // for provenance and comparison, without overriding the named provider's G1S field.
    public bool ResourceNameMatchesDeclaredG1S => Binding.ResourceNameHash == Shader.ShaderFileHash;
}

/// <summary>A type16 entry identifies an own renderer or dependency renderer, not a physical shader program.</summary>
public sealed record OriginalModelShaderInterfaceSource(OriginalNamedModelShader Owner,
    int StateIndex, int? DependencyIndex, OriginalNamedModelShader? Provider)
{
    public bool IsNull => Provider is null;
}

public sealed record ResolvedModelDrawShaderSource(G1DrawShaderIdentifier Identifier,
    ResolvedModelShaderBinding Binding);

public sealed record OriginalModelRenderGraphShaderSource(LoadedRenderGraph Graph,
    CompiledGraphNode Node, TaskGraphArgument StateOffsetArgument,
    ResolvedModelDrawShaderSource Draw, OriginalModelShaderInterfaceSource Interface);

/// <summary>Resolves model shader source declarations and explicit type16 interface dependencies.
/// Material overrides, prepared primitive creation and current feature/pass selection are separate inputs.</summary>
public static class OriginalModelShaderResources
{
    public const uint ModelClass = 0xD40B3C8F;
    public const uint BindingPropertyHash = 0xAD260326;
    public const uint G1SPropertyHash = 0x193025DF;
    public const uint DependencyPropertyHash = 0xDB59AAFC;
    public const uint QueueFactoryClass = 0xFC64DCB6;
    public const uint StateOffsetPropertyHash = 0xA98962A5;
    private sealed record BindingCacheEntry(LoadedRdbAsset Source, OriginalShaderBindingCatalog Catalog);
    private sealed class RepositoryBindingCache
    {
        public readonly object Gate = new();
        public readonly Dictionary<string, BindingCacheEntry> Entries = new(StringComparer.Ordinal);
    }
    private static readonly ConditionalWeakTable<OriginalGameRepository, RepositoryBindingCache> BindingCaches = new();

    public static LoadedModelShaderBindings LoadBindings(OriginalGameRepository repository,
        LoadedModelTextureResources model) =>
        LoadBindings(repository, model.Dictionary, model.ModelObject.ObjectId, model.Model);

    public static LoadedModelShaderBindings LoadBindings(OriginalGameRepository repository,
        LoadedObjects dictionary, uint modelObjectHash, LoadedModelMaterials model)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(model);
        DokObject item = Unique(dictionary, modelObjectHash);
        string context = $"{dictionary.Source.ResourceUri} model {modelObjectHash:X8}";
        if (item.Kind != DokObjectKind.Inline || item.ClassId != ModelClass)
            throw Error(context, "requires an inline D40B3C8F model declaration");
        uint modelFile = Scalar(Field(item, 0x8AB68B3F, context), context);
        if (model.Source.FileId != modelFile)
            throw Error(context, $"model file {model.Source.ResourceUri} differs from declared {modelFile:X8}");
        DokProperty binding = Field(item, BindingPropertyHash, context);
        uint csvFile = Scalar(binding, context);
        if (csvFile == 0) throw Error(context, "has a zero shader binding source");
        try
        {
            BindingCacheEntry cached = LoadBindingCatalog(repository, csvFile);
            return new(dictionary, item, model, binding, cached.Source, cached.Catalog);
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidDataException)
        { throw Error(context, $"shader binding {GameResourceId.Rdb("system", csvFile)}: {error.Message}", error); }
    }

    private static BindingCacheEntry LoadBindingCatalog(OriginalGameRepository repository, uint fileId)
    {
        // Read the selected original/override every time before consulting the parse
        // cache. Changed bytes or source index provenance cannot reuse a stale catalog.
        LoadedRdbAsset source = repository.LoadRdbAsset("system", fileId);
        RepositoryBindingCache cache = BindingCaches.GetValue(repository, _ => new());
        lock (cache.Gate)
        {
            if (cache.Entries.TryGetValue(source.ResourceUri, out BindingCacheEntry? found) &&
                found.Source.DecodedSha256 == source.DecodedSha256 &&
                found.Source.IndexSource.Sha256 == source.IndexSource.Sha256 &&
                found.Source.SourceContainerPath == source.SourceContainerPath &&
                found.Source.OverrideSource == source.OverrideSource)
                return found;
            // Keeping one entry per URI lets Steam/override revisions replace the
            // cache without retaining every past 3MB CSV and 23644-row parse tree.
            var result = new BindingCacheEntry(source, OriginalShaderBindingCsvReader.Read(source.Bytes));
            cache.Entries[source.ResourceUri] = result;
            return result;
        }
    }

    /// <summary>Accepts the original prepared primitive identifier explicitly.
    /// This method does not reinterpret disk draw +12/+20/+24 as that identifier.</summary>
    public static ResolvedModelShaderBinding ResolvePreparedIdentifier(LoadedModelShaderBindings model,
        uint preparedPrimitiveIdentifier, LoadedObjects shaderObjects)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(shaderObjects);
        OriginalShaderBindingRow binding;
        try { binding = model.Bindings.RequireIdentifier(preparedPrimitiveIdentifier); }
        catch (InvalidDataException error)
        { throw Error(model.BindingSource.ResourceUri, error.Message, error); }
        OriginalNamedModelShader shader = ReadDeclaredShader(shaderObjects, binding.ObjectNameHash);
        return new(model, preparedPrimitiveIdentifier, binding, shader);
    }

    /// <summary>Resolves the original ModelData group label to this draw's base shader declaration.
    /// Later material record overrides and runtime feature/pass selection are not applied.</summary>
    public static ResolvedModelDrawShaderSource ResolveDrawBaseSource(LoadedModelShaderBindings model,
        int drawIndex, LoadedObjects shaderObjects)
    {
        ArgumentNullException.ThrowIfNull(model);
        G1ModelShaderIdentifiers identifiers;
        try { identifiers = G1ModelShaderIdentifierReader.Read(model.Model.Geometry); }
        catch (InvalidDataException error)
        { throw Error(model.Model.Source.ResourceUri, error.Message, error); }
        if ((uint)drawIndex >= identifiers.Draws.Count) throw new ArgumentOutOfRangeException(nameof(drawIndex));
        G1DrawShaderIdentifier identifier = identifiers.Draws[drawIndex];
        return new(identifier, ResolvePreparedIdentifier(model, identifier.ShaderIdentifier, shaderObjects));
    }

    public static G1DrawMaterialBinding ResolveDrawMaterials(LoadedModelShaderBindings model, int drawIndex)
    {
        ArgumentNullException.ThrowIfNull(model);
        try
        {
            G1ModelDrawMaterials materials = G1ModelDrawMaterialReader.Read(model.Model.Geometry);
            if ((uint)drawIndex >= materials.Draws.Count) throw new ArgumentOutOfRangeException(nameof(drawIndex));
            return materials.Draws[drawIndex];
        }
        catch (InvalidDataException error)
        { throw Error(model.Model.Source.ResourceUri, error.Message, error); }
    }

    /// <summary>Follows one caller-selected original FC64 queue node's state-array offset.
    /// This source projection does not execute CheckSkip, choose a view, apply material
    /// overrides, or establish that the queue/node ran in the original current frame.</summary>
    public static OriginalModelRenderGraphShaderSource ResolveRenderGraphNodeBaseSource(
        LoadedModelShaderBindings model, int drawIndex, LoadedObjects shaderObjects,
        LoadedRenderGraph graph, int nodeIndex)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.Document.GraphKind != OriginalCompiledGraphKind.RenderGraph)
            throw Error(graph.Source.ResourceUri, "requires an original RenderGraph");
        CompiledGraphNode[] nodes = graph.Document.Nodes.Where(node => node.Index == nodeIndex).ToArray();
        if (nodes.Length != 1 || nodes[0].Call.FactoryClassId != QueueFactoryClass)
            throw Error(graph.Source.ResourceUri, $"node {nodeIndex} requires one FC64DCB6 queue declaration");
        CompiledGraphNode node = nodes[0];
        TaskGraphArgument[] arguments = node.Call.Arguments.Where(arg =>
            arg.NameHash == StateOffsetPropertyHash && arg.DescriptorWord == 0x01000001).ToArray();
        if (arguments.Length != 1 || arguments[0].RawValue.Length != 1 || arguments[0].RawValue[0] == byte.MaxValue)
            throw Error(graph.Source.ResourceUri, $"node {nodeIndex} requires an explicit supported KTGLShaderRenderStateOffset byte");
        ResolvedModelDrawShaderSource draw = ResolveDrawBaseSource(model, drawIndex, shaderObjects);
        OriginalModelShaderInterfaceSource source = ResolveType16Interface(draw.Binding.Shader, arguments[0].RawValue[0]);
        return new(graph, node, arguments[0], draw, source);
    }

    public static OriginalNamedModelShader ReadDeclaredShader(LoadedObjects objects, uint objectHash)
    {
        ArgumentNullException.ThrowIfNull(objects);
        DokObject item = Unique(objects, objectHash);
        string context = $"{objects.Source.ResourceUri} shader {objectHash:X8}";
        if (item.Kind != DokObjectKind.Inline || item.ClassId is null)
            throw Error(context, "requires an inline shader source declaration; reference inheritance is unresolved");
        DokProperty file = Field(item, G1SPropertyHash, context);
        uint id = Scalar(file, context);
        if (id == 0) throw Error(context, "has a zero G1S source reference");
        DokProperty dependencies = Field(item, DependencyPropertyHash, context);
        return new(objects, item, file, id, dependencies,
            Array.AsReadOnly(Values(dependencies, context)));
    }

    public static OriginalModelShaderInterfaceSource ResolveType16Interface(
        OriginalNamedModelShader owner, int stateIndex)
    {
        ArgumentNullException.ThrowIfNull(owner);
        string context = $"{owner.Objects.Source.ResourceUri} shader {owner.Object.ObjectId:X8}";
        if (!owner.HasVerifiedType16Creator)
            throw Error(context, $"class {owner.Object.ClassId:X8} has no verified type16 creator");
        if (stateIndex < 0 || stateIndex > owner.DependencyObjectHashes.Count)
            throw new ArgumentOutOfRangeException(nameof(stateIndex), $"type16 entry exceeds {context}'s declared interface count");
        if (stateIndex == 0) return new(owner, stateIndex, null, owner);
        int index = stateIndex - 1;
        uint hash = owner.DependencyObjectHashes[index];
        // A literal zero source dependency stays null; no neighbouring slot is substituted.
        if (hash == 0) return new(owner, stateIndex, index, null);
        OriginalNamedModelShader provider = ReadDeclaredShader(owner.Objects, hash);
        if (!provider.HasVerifiedType16Creator)
            throw Error(context, $"dependency {index} object {hash:X8} class {provider.Object.ClassId:X8} has no verified type16 own-interface creator");
        return new(owner, stateIndex, index, provider);
    }

    public static LoadedShaderContainer LoadDeclaredG1S(OriginalGameRepository repository,
        OriginalNamedModelShader provider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(provider);
        try { return repository.LoadShaderContainer("system", provider.ShaderFileHash); }
        catch (Exception error) when (error is KeyNotFoundException or InvalidDataException)
        {
            throw Error($"{provider.Objects.Source.ResourceUri} shader {provider.Object.ObjectId:X8}",
                $"G1S {GameResourceId.Rdb("system", provider.ShaderFileHash)}: {error.Message}", error);
        }
    }

    private static DokObject Unique(LoadedObjects objects, uint id)
    {
        DokObject[] rows = objects.Document.Objects.Where(item => item.ObjectId == id).ToArray();
        if (id == 0 || rows.Length != 1)
            throw Error(objects.Source.ResourceUri, $"object {id:X8} requires one exact source; found {rows.Length}");
        return rows[0];
    }
    private static DokProperty Field(DokObject item, uint hash, string context)
    {
        DokProperty[] rows = item.Properties.Where(property => property.NameHash == hash).ToArray();
        if (rows.Length != 1) throw Error(context, $"property {hash:X8} requires one source field; found {rows.Length}");
        return rows[0];
    }
    private static uint Scalar(DokProperty field, string context)
    {
        uint[] values = Values(field, context);
        if (values.Length != 1) throw Error(context, $"property {field.NameHash:X8} requires one scalar");
        return values[0];
    }
    private static uint[] Values(DokProperty field, string context)
    {
        if (field.TypeCode != 5 || field.ElementCount < 0 || field.RawValue.Length != (long)field.ElementCount * 4)
            throw Error(context, $"property {field.NameHash:X8} requires a complete type5 array");
        var values = new uint[field.ElementCount];
        for (int i = 0; i < values.Length; i++)
            values[i] = BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue.AsSpan(i * 4, 4));
        return values;
    }
    private static InvalidDataException Error(string context, string message, Exception? inner = null) =>
        new($"Original model shader {context}: {message}.", inner);
}

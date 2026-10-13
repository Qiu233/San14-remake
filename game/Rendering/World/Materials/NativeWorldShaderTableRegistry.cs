using San14.Data;

/// <summary>Shared owned allocation lifetime for full type16 resources.
/// Separate parses of the same resource version alias its tables. A new
/// catalogue version owns distinct tables and interfaces while accepted work
/// retains the earlier version. Army and static callers share allocation order.</summary>
public sealed class NativeWorldShaderTableRegistry
{
    private sealed class Catalogue(LoadedObjects objects)
    {
        public LoadedObjects Objects { get; } = objects;
        public Dictionary<uint, NativeWorldShaderTableState> Tables { get; } = [];
    }
    private readonly Dictionary<(string Resource, string Version), Catalogue> catalogues = [];
    private readonly Dictionary<(NativeWorldShaderTableState Table, int Index), ModelShaderInterfaceSource> interfaces = [];
    private ulong allocationOrdinal;
    private bool alive = true;
    private NativeWorldShaderTableRegistry(LoadedObjects actualLoadedCatalogue)
        => _ = RetainCatalogue(actualLoadedCatalogue);
    public static NativeWorldShaderTableRegistry CreateLoaded(LoadedObjects actualLoadedCatalogue)
    {
        ArgumentNullException.ThrowIfNull(actualLoadedCatalogue);
        return new(actualLoadedCatalogue);
    }

    /// <summary>Actual resource creation or import/reuse boundary. ObjectId
    /// identifies a record within its imported resource version. The source
    /// loader already supplies that version; no payload is hashed here. Only
    /// ordered allocation identity becomes field3, never a resource key.</summary>
    public NativeWorldShaderTableState RetainImportedTable(NamedModelShader actualTable)
    {
        ArgumentNullException.ThrowIfNull(actualTable);
        if (!alive) throw new ObjectDisposedException(nameof(NativeWorldShaderTableRegistry));
        var catalogue = RetainCatalogue(actualTable.Objects);
        if (catalogue.Tables.TryGetValue(actualTable.Object.ObjectId, out var state)) return state;
        var canonical = ModelShaderResources.ReadDeclaredShader(catalogue.Objects, actualTable.Object.ObjectId);
        if (!canonical.HasVerifiedType16Creator)
            throw new NotSupportedException("Owned type16 allocation needs its actual verified resource creator.");
        state = new(checked(++allocationOrdinal), canonical, this);
        catalogue.Tables.Add(canonical.Object.ObjectId, state);
        return state;
    }

    private Catalogue RetainCatalogue(LoadedObjects objects)
    {
        var key = (objects.Source.ResourceUri, objects.Source.DecodedSha256);
        if (!catalogues.TryGetValue(key, out var catalogue))
            catalogues.Add(key, catalogue = new(objects));
        return catalogue;
    }

    public void RequireCurrent(NativeWorldShaderTableState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        // This registry creates every state and retains all versions until
        // retirement. A table ID lookup would conflate different versions.
        if (!alive || !ReferenceEquals(state.Registry, this))
            throw new InvalidDataException("Typed16 table allocation has been retired or belongs to another resource owner.");
    }

    public ModelShaderInterfaceSource ReadSelectedInterface(NativeWorldShaderTableState state, int stateIndex)
    {
        RequireCurrent(state);
        var key = (state, stateIndex);
        if (!interfaces.TryGetValue(key, out var selected))
        {
            selected = ModelShaderResources.ResolveType16Interface(state.TableOwner, stateIndex);
            interfaces.Add(key, selected);
        }
        return selected;
    }

    public void Invalidate(string actualResourceWriter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualResourceWriter);
        alive = false; catalogues.Clear(); interfaces.Clear();
    }
}

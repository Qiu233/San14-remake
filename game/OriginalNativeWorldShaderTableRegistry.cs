using San14.Data;

/// <summary>Shared owned allocation lifetime for full type16 resources.
/// Imports may have separately parsed declaration wrappers for the same
/// actual catalogue record; those wrappers alias this retained table. Army
/// and static callers share this owner rather than inventing separate ranks.</summary>
public sealed class OriginalNativeWorldShaderTableRegistry
{
    private readonly LoadedObjects catalogue;
    private readonly Dictionary<uint, OriginalNativeWorldShaderTableState> tables = [];
    private readonly Dictionary<(OriginalNativeWorldShaderTableState Table, int Index), OriginalModelShaderInterfaceSource> interfaces = [];
    private ulong allocationOrdinal;
    private bool alive = true;
    private OriginalNativeWorldShaderTableRegistry(LoadedObjects actualLoadedCatalogue)
        => catalogue = actualLoadedCatalogue;
    public static OriginalNativeWorldShaderTableRegistry CreateLoaded(LoadedObjects actualLoadedCatalogue)
    {
        ArgumentNullException.ThrowIfNull(actualLoadedCatalogue);
        return new(actualLoadedCatalogue);
    }

    /// <summary>Actual resource creation or import/reuse boundary. ObjectId
    /// identifies the loaded record; only ordered allocation identity becomes
    /// field3. No file/draw hash or lexical rank becomes a sort key.</summary>
    public OriginalNativeWorldShaderTableState RetainImportedTable(OriginalNamedModelShader actualTable)
    {
        ArgumentNullException.ThrowIfNull(actualTable);
        if (!alive) throw new ObjectDisposedException(nameof(OriginalNativeWorldShaderTableRegistry));
        if (actualTable.Objects.Source.ResourceUri != catalogue.Source.ResourceUri ||
            actualTable.Objects.Source.DecodedSha256 != catalogue.Source.DecodedSha256)
            throw new InvalidDataException("Typed16 table import belongs to another loaded shader catalogue generation.");
        if (tables.TryGetValue(actualTable.Object.ObjectId, out var state)) return state;
        var canonical = OriginalModelShaderResources.ReadDeclaredShader(catalogue, actualTable.Object.ObjectId);
        if (!canonical.HasVerifiedType16Creator)
            throw new NotSupportedException("Owned type16 allocation needs its actual verified resource creator.");
        state = new(checked(++allocationOrdinal), canonical, this);
        tables.Add(canonical.Object.ObjectId, state);
        return state;
    }

    public void RequireCurrent(OriginalNativeWorldShaderTableState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!alive || !ReferenceEquals(state.Registry, this) ||
            !tables.TryGetValue(state.TableOwner.Object.ObjectId, out var retained) || !ReferenceEquals(retained, state))
            throw new InvalidDataException("Typed16 table allocation has been retired or belongs to another resource owner.");
    }

    public OriginalModelShaderInterfaceSource ReadSelectedInterface(OriginalNativeWorldShaderTableState state, int stateIndex)
    {
        RequireCurrent(state);
        var key = (state, stateIndex);
        if (!interfaces.TryGetValue(key, out var selected))
        {
            selected = OriginalModelShaderResources.ResolveType16Interface(state.TableOwner, stateIndex);
            interfaces.Add(key, selected);
        }
        return selected;
    }

    public void Invalidate(string actualResourceWriter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualResourceWriter);
        alive = false; tables.Clear(); interfaces.Clear();
    }
}

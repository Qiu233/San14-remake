using San14.Data;

/// <summary>An actual source-created shader's retained full type16 table.
/// Identity follows this program's allocation order, not a file/draw hash.
/// Draws that share this table alias its state; provider selection is later.</summary>
public sealed class NativeWorldShaderTableState
{
    internal NativeWorldShaderTableState(ulong orderedAllocationIdentity,
        NamedModelShader tableOwner, NativeWorldShaderTableRegistry registry)
        => (OrderedAllocationIdentity, TableOwner, Registry) = (orderedAllocationIdentity, tableOwner, registry);
    public ulong OrderedAllocationIdentity { get; }
    public NamedModelShader TableOwner { get; }
    internal NativeWorldShaderTableRegistry Registry { get; }
}

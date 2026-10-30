using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The actual per-View sphere input allocation. Absence of initial
/// bytes means a native allocation with no initial subresource data, not a
/// zero-filled buffer and not a missing resource.</summary>
public sealed record Tree2ViewInteractionDeclaration(uint Capacity, string SourceProvenance)
{
    public bool HasResource => Capacity != 0;
    public uint ByteWidth => checked(Capacity * 20u);
    public uint SourceNativeFormat => 0x17;
    public uint SourceNativeUsage => 2;
    public uint D3DUsage => 2;
    public uint CpuAccessFlags => 0x10000;
    public uint BindFlags => 8;
    public uint ShaderViewFormat => 41;
    public uint ShaderElementByteCount => 4;
    public uint ShaderElementCount => ByteWidth / ShaderElementByteCount;
    public bool HasInitialSubresourceData => false;

    /// <summary>Version,capacity,bytes,usage,CPU access,bind,SRV format,
    /// typed element count,initial-data presence. No payload follows.</summary>
    public string ExportNativeDeclaration() => FormattableString.Invariant(
        $"1 {Capacity} {ByteWidth} {D3DUsage} {CpuAccessFlags} {BindFlags} {ShaderViewFormat} {ShaderElementCount} 0\n");
}

public static class Tree2ViewInteractionSource
{
    /// <summary>Bind only the actual fresh Host recipe. BF58A0 writes qwordEA
    /// to all ones, so the signed Host754 capacity override is exactly-1.</summary>
    public static Tree2ViewInteractionDeclaration LoadFreshNormal(
        LoadedDefaultView0Source actualDefaultView, MainMapContextOwnerSource actualFreshHost)
    {
        ArgumentNullException.ThrowIfNull(actualFreshHost);
        return ReadDeclaration(actualDefaultView, -1,
            actualFreshHost.SourceProvenance + ";fresh-Host754=-1:BF58A0-qwordEA");
    }

    /// <summary>A reached later Host override must be supplied by its current
    /// owner. A fresh constructor value is never substituted for that state.</summary>
    public static Tree2ViewInteractionDeclaration ReadDeclaration(
        LoadedDefaultView0Source actualDefaultView, int actualHostCapacityOverride754,
        string actualHostSource)
    {
        ArgumentNullException.ThrowIfNull(actualDefaultView);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualHostSource);
        //The arena producer already uses this same actual View object for
        //ordinals6/7/14. Ordinal40 is the independent sphere pool capacity.
        var view = actualDefaultView.ViewDefinitions.Document.Objects.Single(value =>
            value.ObjectId == DefaultView0Source.ViewObjectId && value.ClassId == 0x97AF26FD);
        var property = view.Properties[40];
        if (property.Index != 40 || property.NameHash != 0x0355FAD4 || property.TypeCode != 5 ||
            property.ElementCount != 1 || property.ElementSize != 4 || property.RawValue.Length != 4)
            throw new InvalidDataException("Tree2 View interaction capacity source declaration differs.");
        uint declared = BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
        uint capacity = actualHostCapacityOverride754 >= 0 ? (uint)actualHostCapacityOverride754 : declared;
        _ = checked(capacity * 20u);
        return new(capacity, actualDefaultView.SourceIdentity + ";" + actualHostSource +
            ";D21E51-View40/Host754->D5FB60-arg35->D5F170->D1B160-pool348;" +
            "D12A80/D0A110->D12830/D06FA0-Aux1B58;119BC70/94B250-native17-usage2-bind8-no-initial-data");
    }
}

using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Owned current ModelCPU+B0..EC words. Public bytes are copies; the
/// ordinary draw consumer tests the explicitly selected current View slot >0.
/// Constructor provenance applies before any later explicit slot setter.</summary>
public sealed class OriginalMapModelNativeCurrentVisibilityWords
{
    private readonly byte[] _bytes;
    internal OriginalMapModelNativeCurrentVisibilityWords(string nodeIdentity, string publicationIdentity,
        byte[] bytes, string basis)
    { CurrentSourceIdentity = nodeIdentity; PublicationIdentity = publicationIdentity; _bytes = bytes.ToArray(); Basis = basis; }
    public string CurrentSourceIdentity { get; }
    public string PublicationIdentity { get; }
    public string Basis { get; }
    public int SlotCount => 16;
    public ReadOnlyMemory<byte> Bytes => _bytes.ToArray();
    public float ReadSlot(int currentViewIndex)
    {
        if ((uint)currentViewIndex >= 16) throw new InvalidDataException("Native Model visibility requires an explicit current View index0..15.");
        return BinaryPrimitives.ReadSingleLittleEndian(_bytes.AsSpan(currentViewIndex * 4, 4));
    }
    public bool IsDrawEligible(int currentViewIndex) => ReadSlot(currentViewIndex) > 0f;
}

/// <summary>The full typed constructor's final positive writer, followed by
/// separately published current overrides. The earlier zero stores are an
/// intermediate initialization and are not the returned source state.</summary>
public static class OriginalMapModelNativeCurrentVisibilitySources
{
    public const string ContractId = "ce-20261002-model-worldpq-final-constructor-visibility";
    public const string PairedFinalWindowSha256 = "03B597B00CAD6245E0A60E9B633EAB3F6DFDCC90A3807BF832C9006002C57850";
    public const uint ConstructorVisibilityBits = 0x3F800000;
    public const int VisibilityOffset = 0xB0;

    public static OriginalMapModelNativeCurrentVisibilityWords ConstructFresh(
        OriginalMapModelNativeFreshVisibilityPrefix verifiedTypedPrefix, string currentPublicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(verifiedTypedPrefix); ArgumentException.ThrowIfNullOrWhiteSpace(currentPublicationIdentity);
        var identity = verifiedTypedPrefix.BoundsPublication.CurrentSourceIdentity;
        if (string.IsNullOrWhiteSpace(identity) || verifiedTypedPrefix.BoundsPublication.ParentBound != false ||
            verifiedTypedPrefix.BoundsPublication.ModelResourceBindingIsCurrent != true || !verifiedTypedPrefix.UseModelBounds)
            throw new InvalidDataException("Source visibility constructor requires the verified current typed ModelWorldPQ prefix.");
        byte[] bytes = new byte[64];
        for (int i = 0; i < 16; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4, 4), ConstructorVisibilityBits);
        return new(identity, currentPublicationIdentity, bytes, ContractId + "; ModelWorldPQ VT SC16CBC50/TC16CAF98; " +
            "SC:D9F950..D9F9D3 / TC:D9ECA0..D9ED23 SHA256=" + PairedFinalWindowSha256 + "; final eight qword stores B0..E8");
    }

    public static OriginalMapModelNativeCurrentVisibilityWords ReadExplicitCurrent(string currentNodeIdentity,
        string currentWriterIdentity, ReadOnlySpan<byte> completeCurrent64Bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentNodeIdentity); ArgumentException.ThrowIfNullOrWhiteSpace(currentWriterIdentity);
        if (completeCurrent64Bytes.Length != 64)
            throw new InvalidDataException("Current native Model visibility publication requires all16 actual float words.");
        return new(currentNodeIdentity, currentWriterIdentity, completeCurrent64Bytes.ToArray(),
            "explicit current ModelCPU+B0..EC slot publication; caller owns later setter provenance");
    }

    /// <summary>D14A80/D13D50 chooses Context+23B8[slot]; D07F76/D072B6
    /// forwards that exact Data/null to View+1C08. Returns an empty complete
    /// result only when the supplied current selection proves the null branch.
    /// Non-null Data requires its actual complete applicable culler results.</summary>
    public static IReadOnlyList<bool> PrepareCurrentNullCustomSelection(string currentSelectionIdentity,
        bool? currentTableDataPresent, bool? currentOptionalStatePresent, bool? currentOptionalStateBit6)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSelectionIdentity);
        if (currentTableDataPresent is null) throw new InvalidDataException("Current custom-culler table selection is unresolved.");
        if (!currentTableDataPresent.Value) return Array.Empty<bool>();
        if (currentOptionalStatePresent is null) throw new InvalidDataException("Current custom-culler optional state presence is unresolved.");
        if (currentOptionalStatePresent.Value)
        {
            if (currentOptionalStateBit6 is null) throw new InvalidDataException("Current custom-culler optional state bit6 is unresolved.");
            if (!currentOptionalStateBit6.Value) return Array.Empty<bool>();
        }
        throw new InvalidDataException("Selected current custom-culler Data is non-null; actual complete applicable results are required.");
    }
}

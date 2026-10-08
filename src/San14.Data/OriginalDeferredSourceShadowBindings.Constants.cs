using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Complete retained bytes, including native padding and unused array tails.
/// A template is explicit source state; an absent template never requests a zero buffer.</summary>
public sealed record OriginalDeferredShadowConstantTemplate(uint ConstantRegister,
    ReadOnlyMemory<byte> Bytes, string SourceProvenance);
public sealed record OriginalDeferredShadowConstantWrite(string Name, int Offset, ReadOnlyMemory<byte> Bytes);
public sealed record OriginalDeferredPreparedShadowConstants(OriginalDeferredShadowDeclaration Declaration,
    uint ClampedCount, ReadOnlyMemory<byte> Bytes, IReadOnlyList<OriginalDeferredShadowConstantWrite> Writes,
    string SourceProvenance, string Sha256);
public sealed record OriginalDeferredSourceShadowConstantsPacket(LoadedOriginalDeferredSourceShadowBindings Source,
    OriginalDeferredPreparedShadowConstants Point, OriginalDeferredPreparedShadowConstants Spot)
{
    public bool ActiveOriginalBindingObserved => false;
}

/// <summary>Snapshot of the exact global shadow owner fields consumed by the
/// native CB updater. Source data is packed as owner+850 transforms and
/// owner+2850/+4850 sizes. No matrix conversion or projected size is invented.</summary>
public sealed class OriginalDeferredShadowOwnerSnapshot
{
    private readonly byte[] size, transforms, shadowSizes;
    private OriginalDeferredShadowOwnerSnapshot(uint register, uint count, uint softShadowType,
        uint ownerField834, ReadOnlySpan<byte> size, ReadOnlySpan<byte> transforms,
        ReadOnlySpan<byte> shadowSizes, string identity, string provenance, OriginalDeferredShadowOwnerScope scope)
    {
        PixelRegister = register; Count = count; SoftShadowType = softShadowType; OwnerField834 = ownerField834;
        this.size = size.ToArray(); this.transforms = transforms.ToArray(); this.shadowSizes = shadowSizes.ToArray();
        OwnerIdentity = identity; SourceProvenance = provenance; Scope = scope;
    }
    public uint PixelRegister { get; }
    public uint Count { get; }
    public uint SoftShadowType { get; }
    public uint OwnerField834 { get; }
    public string OwnerIdentity { get; }
    public string SourceProvenance { get; }
    public OriginalDeferredShadowOwnerScope Scope { get; }
    public ReadOnlyMemory<byte> Size => size;
    public ReadOnlyMemory<byte> PackedTransforms => transforms;
    public ReadOnlyMemory<byte> PackedShadowSizes => shadowSizes;

    /// <summary>Successful original global data construction before any setters:
    /// header+82C/+830/+834=(0,1,0), size+840 is the original zero16B literal,
    /// point+850..2C50 or spot+850..5850 is explicitly memset zero.</summary>
    public static OriginalDeferredShadowOwnerSnapshot FromFreshConstruction(uint pixelRegister,
        string ownerIdentity, OriginalDeferredShadowOwnerScope explicitScope)
    {
        if (explicitScope != OriginalDeferredShadowOwnerScope.FreshConstructionBeforeAssignments)
            throw OriginalDeferredSourceShadowBindings.Error("fresh shadow construction does not cover current or edited owners");
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerIdentity);
        if (pixelRegister is not (3 or 4)) throw OriginalDeferredSourceShadowBindings.Error("shadow register must be3 or4");
        int transformBytes = pixelRegister == 3 ? 8192 : 16384, sizeBytes = pixelRegister == 3 ? 1024 : 4096;
        return new(pixelRegister, 0, 1, 0, new byte[16], new byte[transformBytes], new byte[sizeBytes],
            ownerIdentity, pixelRegister == 3 ? "source-constructor:TC9B7BF0/SC9B8B80;header0,1,0;size14BB8A0/14BC780;memset850:2400;before-assignments" :
                "source-constructor:TC9B8A70/SC9B9B30;header0,1,0;size14BB8A0/14BC780;memset850:5000;before-assignments", explicitScope);
    }

    public static OriginalDeferredShadowOwnerSnapshot FromPreparedSourceOwner(uint pixelRegister,
        uint count, uint softShadowType, uint ownerField834, ReadOnlySpan<byte> completeSize16,
        ReadOnlySpan<byte> completePackedTransforms, ReadOnlySpan<byte> completePackedSizes,
        string ownerIdentity, string sourceProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerIdentity); ArgumentException.ThrowIfNullOrWhiteSpace(sourceProvenance);
        if (pixelRegister is not (3 or 4) || completeSize16.Length != 16 ||
            completePackedTransforms.Length != (pixelRegister == 3 ? 8192 : 16384) ||
            completePackedSizes.Length != (pixelRegister == 3 ? 1024 : 4096))
            throw OriginalDeferredSourceShadowBindings.Error("prepared shadow owner lacks its complete exact packed source extents");
        return new(pixelRegister, count, softShadowType, ownerField834, completeSize16,
            completePackedTransforms, completePackedSizes, ownerIdentity, sourceProvenance,
            OriginalDeferredShadowOwnerScope.CurrentPreparedOwner);
    }
}

public static partial class OriginalDeferredSourceShadowBindings
{
    /// <summary>Apply only TC9B8310/9B9090's actual successful dirty-update writes.
    /// Count0 writes header12B and size16B; all array tails and padding retain the
    /// explicit complete template. The selected source owner CB capacity must match
    /// the shader's MaxShadows, as checked by the original plugin. False update
    /// gate does not run a constructor substitute.</summary>
    public static OriginalDeferredSourceShadowConstantsPacket ApplySuccessfulPreparedUpdates(
        LoadedOriginalDeferredSourceShadowBindings source, OriginalDeferredShadowOwnerSnapshot point,
        OriginalDeferredShadowOwnerSnapshot spot, OriginalDeferredShadowConstantTemplate pointTemplate,
        OriginalDeferredShadowConstantTemplate spotTemplate, bool pointDirtyUpdateSucceeded,
        bool spotDirtyUpdateSucceeded, uint pointOwnerConstantCapacity, uint spotOwnerConstantCapacity)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(point); ArgumentNullException.ThrowIfNull(spot);
        ArgumentNullException.ThrowIfNull(pointTemplate); ArgumentNullException.ThrowIfNull(spotTemplate);
        if (!pointDirtyUpdateSucceeded || !spotDirtyUpdateSucceeded)
            throw Error("shadow CB writes require their actual dirty gate and successful Map/update; retained current buffers must be supplied separately");
        if (point.PixelRegister != 3 || spot.PixelRegister != 4) throw Error("shadow owners are assigned to a different consumer");
        if (pointOwnerConstantCapacity != source.Point.Maximum.RawValue || spotOwnerConstantCapacity != source.Spot.Maximum.RawValue)
            throw Error("shadow source owner CB capacities do not match the plugin MaxShadows; the separate native parameter-array route is required");
        OriginalDeferredPreparedShadowConstants Apply(OriginalDeferredShadowDeclaration declaration,
            OriginalDeferredShadowOwnerSnapshot owner, OriginalDeferredShadowConstantTemplate template, uint ownerConstantCapacity)
        {
            if (template.ConstantRegister != declaration.ConstantBinding.BindPoint ||
                template.Bytes.Length != declaration.ConstantDefinition.ByteSize || string.IsNullOrWhiteSpace(template.SourceProvenance))
                throw Error("shadow constant template lacks its complete source extent/provenance");
            uint count = Math.Min(owner.Count, ownerConstantCapacity);
            var writes = new List<OriginalDeferredShadowConstantWrite>();
            byte[] header = new byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(header, count);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), owner.SoftShadowType);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), owner.OwnerField834);
            writes.Add(new(declaration.PixelRegister == 3 ? "vSplsNumSS" : "vSslsNumSS", 0, header));
            writes.Add(new(declaration.PixelRegister == 3 ? "vSubCubSz" : "vSubSMSz", 16, owner.Size.ToArray()));
            if (count > 0)
            {
                int transformBytes = checked((int)count * (declaration.PixelRegister == 3 ? 32 : 64));
                writes.Add(new(declaration.PixelRegister == 3 ? "vSubCbW2S" : "mSubSptW2S", 32,
                    owner.PackedTransforms.Slice(0, transformBytes).ToArray()));
                if (declaration.PixelRegister == 3)
                {
                    for (int index = 0; index < count; index++)
                        writes.Add(new($"fSplSize[{index}]", checked(8224 + index * 16),
                            owner.PackedShadowSizes.Slice(index * 4, 4).ToArray()));
                }
                else writes.Add(new("vSslSize", 16416, owner.PackedShadowSizes.Slice(0, checked((int)count * 16)).ToArray()));
            }
            byte[] bytes = template.Bytes.ToArray();
            foreach (var write in writes) write.Bytes.Span.CopyTo(bytes.AsSpan(write.Offset, write.Bytes.Length));
            return new(declaration, count, bytes, writes.AsReadOnly(),
                owner.SourceProvenance + ";owner=" + owner.OwnerIdentity + ";retained-template=" + template.SourceProvenance +
                ";owner-cb-capacity=" + ownerConstantCapacity + ";successful-dirty-update;unwritten-padding-and-array-tails-retained", Convert.ToHexString(SHA256.HashData(bytes)));
        }
        return new(source, Apply(source.Point, point, pointTemplate, pointOwnerConstantCapacity),
            Apply(source.Spot, spot, spotTemplate, spotOwnerConstantCapacity));
    }
}

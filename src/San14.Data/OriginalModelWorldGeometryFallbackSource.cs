using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The loaded default370 ModelWorldPQ's canonical ModelCPU provider.
/// A declared destruction displayset is retained as a declaration; this source
/// does not turn an unresolved dependency into a null current resource.</summary>
public sealed class LoadedOriginalModelWorldGeometryFallbackSource
{
    internal LoadedOriginalModelWorldGeometryFallbackSource(
        LoadedOriginalDeferredEarlyShadowWorldCallerSource world,
        OriginalModelPrimitiveInventorySource model, uint destructionDeclaration)
        => (World, Model, DestructionDisplaysetDeclarationHash) = (world, model, destructionDeclaration);

    public LoadedOriginalDeferredEarlyShadowWorldCallerSource World { get; }
    public OriginalModelPrimitiveInventorySource Model { get; }
    public uint DestructionDisplaysetDeclarationHash { get; }
    public string SourceEvidence => World.SourceIdentity + ";" + Model.SourceProvenance +
        ";TC:DA7DF1/default370->D9E890;D9EB84/2B0=0;D9EB92/2C0=0;" +
        "DA85C4/loader-only-OR-bit0;DC20A0/material280;DC1D50/V78->DB4410/V418;" +
        "DB684E/bit1-clear->DB6AE5/true;DB84E5/bit14-clear;" +
        "C24D72/G1ii-null-false;DB84AA/retain-null;C392F9/ModelData20";

    /// <summary>Capture from the caller's actual owned default370 node after
    /// source creation, while its fresh geometry state is retained. Registration,
    /// matrix and transform refresh preserve the mode fields. DAB860 destruction
    /// mode changes, DAB680/DC2F40 runtime-provider publication, a replacement
    /// World type or Model G1ii binding require their actual current provider.
    /// This method is not a claim about an arbitrary current original process.</summary>
    public OriginalModelCurrentPrimitiveBinding CaptureRetainedCurrentModelCpu(string actualNodeIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualNodeIdentity);
        return new(actualNodeIdentity, true, SourceEvidence + ";owned-retained-default370:" + actualNodeIdentity);
    }
}

/// <summary>Import once, then retain the actual source provider. The fallback
/// does not require World property26 zero: source loading can publish bit0 and
/// 2A8, but never activates bit1 or the transient bit14.</summary>
public static class OriginalModelWorldGeometryFallbackSource
{
    private static readonly ConditionalWeakTable<OriginalMapStaticModelAsset, LoadedOriginalModelWorldGeometryFallbackSource> Statics = new();
    private static readonly ConditionalWeakTable<OriginalArmyPrimitivePublicationSource, LoadedOriginalModelWorldGeometryFallbackSource> Armies = new();

    public static LoadedOriginalModelWorldGeometryFallbackSource LoadStatic(OriginalMapStaticModelAsset actualAsset)
    {
        ArgumentNullException.ThrowIfNull(actualAsset);
        return Statics.GetValue(actualAsset, static asset =>
        {
            var world = OriginalDeferredEarlyShadowCasterAdmissionSource.LoadStatic(asset);
            var model = OriginalModelPrimitiveInventorySource.LoadStatic(asset);
            return ReadDeclared(asset.TextureResources.Catalog ??
                throw new InvalidDataException("ModelWorld GeometryInput catalog is absent."), world, model);
        });
    }

    public static LoadedOriginalModelWorldGeometryFallbackSource LoadArmy(OriginalArmyPrimitivePublicationSource actualSource)
    {
        ArgumentNullException.ThrowIfNull(actualSource);
        return Armies.GetValue(actualSource, static source => ReadDeclared(source.Catalog.Catalog,
            OriginalDeferredEarlyShadowCasterAdmissionSource.LoadArmy(source),
            OriginalModelPrimitiveInventorySource.LoadArmy(source)));
    }

    private static LoadedOriginalModelWorldGeometryFallbackSource ReadDeclared(LoadedObjects actualCatalog,
        LoadedOriginalDeferredEarlyShadowWorldCallerSource actualWorld, OriginalModelPrimitiveInventorySource actualModel)
    {
        // The two actual import owners above verify their original bytes and
        // default370/Model joins at this load boundary. Parse the same bytes
        // once, rather than trusting an unrelated caller-supplied document.
        var world = DokReader.Read(actualCatalog.Source.Bytes).Objects.Single(value =>
            value.Kind == DokObjectKind.Inline && value.ClassId == OriginalScenePlacements.ModelWorldPqClass &&
            value.ObjectId == actualWorld.DefinitionObjectId);
        var property = world.Properties[26];
        if (property.Index != 26 || property.NameHash != 0xB2B6E876 ||
            property.TypeCode != 5 || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException("ModelWorld GeometryInput requires the exact destruction displayset declaration binding.");
        return new(actualWorld, actualModel, BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue));
    }
}

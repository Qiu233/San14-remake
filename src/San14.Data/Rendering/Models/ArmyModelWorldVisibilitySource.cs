using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual same-frame selected View's finite null custom-culler branch.
/// Caller presence inputs are required; an unknown list never becomes empty.</summary>
public sealed class ArmyCurrentWorldVisibilityView
{
    private ArmyCurrentWorldVisibilityView(long frame, string view, uint commonType,
        IReadOnlyList<bool> customRejects, string source)
        => (FrameNumber, ViewIdentity, CurrentCommonFrustumType, CompleteCustomCullingRejects, SourceEvidence) =
            (frame, view, commonType, customRejects, source);
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public uint CurrentCommonFrustumType { get; }
    public IReadOnlyList<bool> CompleteCustomCullingRejects { get; }
    public string SourceEvidence { get; }

    public static ArmyCurrentWorldVisibilityView PrepareActualNullCustomSelection(long frameNumber,
        string viewIdentity, MapModelNativeFrustumCullingSource actualRetainedCommonView,
        bool? actualSelectedTableDataPresent, bool? actualOptionalCullingStatePresent,
        bool? actualOptionalStateBit6, string actualCurrentSelectionSource)
    {
        ArgumentNullException.ThrowIfNull(actualRetainedCommonView);
        ArgumentException.ThrowIfNullOrWhiteSpace(viewIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualCurrentSelectionSource);
        if (frameNumber <= 0) throw new InvalidDataException("Army culling View has no current frame.");
        var custom = MapModelNativeCurrentVisibilitySources.PrepareCurrentNullCustomSelection(
            actualCurrentSelectionSource, actualSelectedTableDataPresent, actualOptionalCullingStatePresent, actualOptionalStateBit6);
        uint type = MapModelNativeVisibilitySources.ResolveFrustumCullingType(uint.MaxValue, actualRetainedCommonView.Value);
        if (type is not (0 or 3))
            throw new NotSupportedException("Army current CommonView needs the detailed mode1/2 box culler.");
        return new(frameNumber, viewIdentity, type, custom,
            actualCurrentSelectionSource + ";" + actualRetainedCommonView.Basis +
            ";TC:D13D50/D13EFD/D1299E/D072B6;actual-selected-null-View1C08");
    }
}

/// <summary>One original ordinary Model's computed current visibility. The
/// source inventory remains complete when this actor is outside the View.</summary>
public sealed class ArmyCurrentWorldActorVisibility
{
    internal ArmyCurrentWorldActorVisibility(long frame, string view, string actor,
        ArmyModelWorldVisibilitySource source, UnitWorldTransformState world,
        MapModelNativeOrientedBounds bounds, float scalar, bool slotEligible, bool culled,
        string evidence)
        => (FrameNumber, ViewIdentity, ActorIdentity, Source, CurrentWorld, Bounds, CurrentViewScalar,
            CurrentSlotEligible, RejectedByCulling, SourceEvidence) =
            (frame, view, actor, source, world, bounds, scalar, slotEligible, culled, evidence);
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public string ActorIdentity { get; }
    public ArmyModelWorldVisibilitySource Source { get; }
    public UnitWorldTransformState CurrentWorld { get; }
    public MapModelNativeOrientedBounds Bounds { get; }
    public float CurrentViewScalar { get; }
    public bool CurrentSlotEligible { get; }
    public bool RejectedByCulling { get; }
    public bool CurrentOrdinaryView0Eligible => CurrentSlotEligible && !RejectedByCulling;
    public string SourceEvidence { get; }
}

/// <summary>Actual default370 ModelWorldPQ source recipe. Army
/// Flag00 is accepted independently of the static Flag01 profile. It neither
/// disables the actor nor replaces source model bounds with a preview AABB.</summary>
public sealed class ArmyModelWorldVisibilitySource
{
    private ArmyModelWorldVisibilitySource(ArmyPrimitivePublicationSource source,
        MapModelNativeFlagSource flags, MapModelNativeFrustumCullingSource type,
        MapModelNativePreparedBounds bounds)
    {
        (PrimitiveSource, DeclaredFlags, DeclaredFrustumType, PreparedModelBounds) = (source, flags, type, bounds);
        SourceIdentity = source.Identity;
        SourceEvidence = source.SourceProvenance +
            ";Army-default370;TC:D9E890-no-parent1C0/D9EC00-328/D9EC0B-334/D9ECA0-finalB0;" +
            "TC:DB2ED0-negative334-skips278;F573DE/C21FC0/DAD880;Army-Flag00-model-bound-no-child-union";
    }
    public ArmyPrimitivePublicationSource PrimitiveSource { get; }
    public MapModelNativeFlagSource DeclaredFlags { get; }
    public MapModelNativeFrustumCullingSource DeclaredFrustumType { get; }
    public MapModelNativePreparedBounds PreparedModelBounds { get; }
    // D9F5B8 writes Flag/bit31 at44; D9F5BC writes DynamicFlag separately
    // at48. Constructor arg7 supplies another byte at44 bits8..15. Only the
    // proved low-byte model-bound/child-union gates are used here.
    public byte ConstructorLowByteFlags44 => DeclaredFlags.Flag;
    public string SourceIdentity { get; }
    public string SourceEvidence { get; }

    /// <summary>Load boundary only: consume imported source
    /// flags, default geometry and ModelCPU bounds once per actual source.</summary>
    public static ArmyModelWorldVisibilitySource Read(ArmyPrimitivePublicationSource actualSource)
    {
        ArgumentNullException.ThrowIfNull(actualSource);
        var catalog = actualSource.Catalog.Catalog;
        uint definition = actualSource.Category.DefinitionId;
        var flags = MapModelNativeVisibilitySources.ReadWorldPqSourceFlags(catalog, definition);
        var type = MapModelNativeVisibilitySources.ReadWorldPqDeclaration(catalog, definition);
        if (flags.Flag != 0 || flags.DynamicFlag != 0 || type.Value != uint.MaxValue)
            throw new NotSupportedException("This Army source requires another current flags/bounds/frustum recipe.");
        var geometry = actualSource.Model.Geometry;
        var header = geometry.RawGeometryHeader.AsSpan();
        if (header.Length < 44) throw new InvalidDataException("Army source ModelCPU bounds header is incomplete.");
        var minimum = new System.Numerics.Vector3(BinaryPrimitives.ReadSingleLittleEndian(header[20..]),
            BinaryPrimitives.ReadSingleLittleEndian(header[24..]), BinaryPrimitives.ReadSingleLittleEndian(header[28..]));
        var maximum = new System.Numerics.Vector3(BinaryPrimitives.ReadSingleLittleEndian(header[32..]),
            BinaryPrimitives.ReadSingleLittleEndian(header[36..]), BinaryPrimitives.ReadSingleLittleEndian(header[40..]));
        var bounds = MapModelNativeBoundsSources.PrepareHeader(minimum, maximum,
            actualSource.Model.Source.ResourceUri, actualSource.Model.Source.DecodedSha256);
        return new(actualSource, flags, type, bounds);
    }

    /// <summary>The owned source-created node retains its exact completed
    /// constructor words until a real visibility/parent/flags writer occurs.</summary>
    public MapModelNativeCurrentVisibilityWords ConstructOwnedVisibility(string actorIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorIdentity);
        var prefix = new MapModelNativeFreshVisibilityPrefix(DeclaredFlags, DeclaredFrustumType,
            MapModelNativeBoundsPublication.CurrentSingleUnparented(actorIdentity), SourceEvidence);
        return MapModelNativeCurrentVisibilitySources.ConstructFresh(prefix, actorIdentity + "/Army-constructor-B0");
    }

    public ArmyCurrentWorldActorVisibility PrepareOwnedUnparentedCurrent(long frameNumber,
        string actorIdentity, UnitWorldTransformState actualWorld,
        MapModelNativeCurrentVisibilityWords actualCurrentVisibility,
        MapStaticModelCamera actualCamera, ArmyCurrentWorldVisibilityView actualView)
    {
        ArgumentNullException.ThrowIfNull(actualWorld); ArgumentNullException.ThrowIfNull(actualCurrentVisibility);
        ArgumentNullException.ThrowIfNull(actualCamera); ArgumentNullException.ThrowIfNull(actualView);
        if (frameNumber != actualView.FrameNumber || string.IsNullOrWhiteSpace(actorIdentity) ||
            actualCurrentVisibility.CurrentSourceIdentity != actorIdentity ||
            actualWorld.EvidenceUri != PrimitiveSource.Catalog.Catalog.Source.ResourceUri ||
            actualWorld.EvidenceSha256 != PrimitiveSource.Catalog.Catalog.Source.DecodedSha256 ||
            !actualWorld.Basis.Contains(UnitWorldTransform.NativeContractId, StringComparison.Ordinal))
            throw new InvalidDataException("Army current visibility needs its actual owned source transform, View and constructor/explicit slot words.");
        // The source-created ordinary/linked flag lifecycle publishes only
        // +68/+70 (and flag +78); it has no parent-bind or child-union writer.
        var bounds = MapModelNativeBoundsSources.BuildSingleNode(PreparedModelBounds, actualWorld, false, false);
        float scalar = actualCurrentVisibility.ReadSlot(0);
        if (!float.IsFinite(scalar))
            throw new NotSupportedException("Army finite current visibility does not cover a nonfinite slot scalar.");
        bool eligible = scalar > 0f; // DB7A18, separate from source Flag00.
        bool culled = eligible && MapModelNativeVisibilitySources.IsCulled(actualCamera.Frustum,
            bounds, actualView.CurrentCommonFrustumType, actualView.CompleteCustomCullingRejects);
        return new(frameNumber, actualView.ViewIdentity, actorIdentity, this, actualWorld, bounds, scalar, eligible, culled,
            SourceEvidence + ";" + actualCurrentVisibility.Basis + ";" + actualView.SourceEvidence +
            ";actual-OBB/planes0,1,4,2,3,5;owned-unparented-ordinary-setters-retained");
    }
}

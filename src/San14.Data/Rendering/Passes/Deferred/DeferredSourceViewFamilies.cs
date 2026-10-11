using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum DeferredSourceViewFamilyScope
{
    FreshSourceMainNoExternalFamilyAssignments,
    ImportedOrEdited
}

/// <summary>One actual ordinary CPU candidate. A null state16 skips its shader
/// submission; it does not remove the candidate from the source collection.</summary>
public sealed record DeferredSourceMode16Candidate(string Family, string InstanceKey,
    int SourceDrawIndex, string ModelResourceUri, string ModelSha256,
    NamedModelShader FinalParent, string CurrentOwnerProvenance);

/// <summary>Source constructor and supported retained generation for the Main
/// View families outside the captured Camera3C0 prefix. Later external family
/// writers invalidate this owner; they are never replaced with fresh zeros.</summary>
public sealed class DeferredSourceViewFamilyOwner
{
    private const string CatalogSha = "67115E16E1B21065B4F04E25109F8182AB027B7DBD38B98979CB02BCFD1291B9";
    private readonly LoadedDeferredSamplerNativeSource native;
    private readonly MapLightingBinding binding;
    private readonly LoadedMapCameraConstructionSource cameraSource;
    // Catalog and null state imports belong to this View owner generation.
    // External family writers invalidate the owner before further publication.
    private readonly Dictionary<LoadedRdbAsset, LoadedObjects> finalCatalogs =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<NamedModelShader> nullFinalOwners = new(ReferenceEqualityComparer.Instance);
    private bool retained = true;
    private long revision;
    public string ViewGenerationIdentity { get; }
    public string OwnerIdentity { get; } = "owned-source-Main-View-families:" + Guid.NewGuid().ToString("N");
    public string SourceProvenance { get; }

    private DeferredSourceViewFamilyOwner(LoadedDeferredSamplerNativeSource native,
        LoadedMapCameraConstructionSource source, MapLightingBinding binding, string generation)
    {
        this.native = native; this.binding = binding; ViewGenerationIdentity = generation;
        cameraSource = source;
        SourceProvenance = native.RenderRuleIdentity + ";Main38D/source-Camera1550-zero-retained;" +
            "TC:C3E1AD/C3E1BF/C3E1C6/C3F000/AB5990/C409C0/D05D54/D05D5A;" +
            "View1C18-null=TC:D072CB;fresh-owned/no-external-family-assignments;ViewGeneration=" + generation;
    }

    public static DeferredSourceViewFamilyOwner CreateFresh(LoadedDeferredSamplerNativeSource native,
        LoadedMapCameraConstructionSource source, MapLightingBinding binding,
        string actualViewGenerationIdentity, DeferredSourceViewFamilyScope scope)
    {
        ArgumentNullException.ThrowIfNull(native); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(binding); ArgumentException.ThrowIfNullOrWhiteSpace(actualViewGenerationIdentity);
        if (scope != DeferredSourceViewFamilyScope.FreshSourceMainNoExternalFamilyAssignments)
            throw new NotSupportedException("Imported or edited View families require their current source publishers.");
        if (source.Camera.ObjectId != 0x38D04576 ||
            source.Camera.Kind != DokObjectKind.Inline || source.Camera.ClassId != 0x23E7C64E ||
            source.Resource.Source.ResourceUri != "san14://rdb/system/908AB6E2" || source.Resource.Source.FileId != 0x908AB6E2 ||
            string.IsNullOrWhiteSpace(binding.ViewIdentity) || string.IsNullOrWhiteSpace(binding.CameraOwnerIdentity))
            throw new InvalidDataException("Fresh View families require the imported Main camera and its actual owner generation.");
        return new(native, source, binding, actualViewGenerationIdentity);
    }

    public void MarkImportedEditedOrUnresolved(string actualWriterProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWriterProvenance);
        retained = false; revision = checked(revision + 1);
    }

    public DeferredSourceViewFamilyPacket Prepare(MapSourceCreatedF103CurrentFrame frame,
        IReadOnlyList<DeferredSourceMode16Candidate> completeOrdinaryCandidates,
        string actualCompleteCandidatePublicationProvenance)
    {
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(completeOrdinaryCandidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualCompleteCandidatePublicationProvenance);
        ValidateFrame(frame);
        var candidates = completeOrdinaryCandidates.ToArray();
        if (candidates.Any(c => c is null || c.SourceDrawIndex < 0 || c.Family is not ("StaticModel" or "ArmyOrdinary" or "ArmyFlagBase" or "ArmyFlagCloth") ||
                string.IsNullOrWhiteSpace(c.InstanceKey) || string.IsNullOrWhiteSpace(c.CurrentOwnerProvenance)) ||
            candidates.Select(c => (c.Family, c.InstanceKey, c.SourceDrawIndex)).Distinct().Count() != candidates.Length)
            throw new InvalidDataException("Ordinary mode16 candidates require unique complete current owner identities.");
        var expectedStatic = frame.CpuPopulation!.Visibility.Visible.SelectMany(instance => instance.Asset.Draws.Select(draw =>
            (instance.InstanceKey, draw.Index, instance.Asset.RawTransforms.Source.ResourceUri, instance.Asset.RawTransforms.Source.DecodedSha256))).ToHashSet();
        var currentStatic = candidates.Where(c => c.Family == "StaticModel").Select(c =>
            (c.InstanceKey, c.SourceDrawIndex, c.ModelResourceUri, c.ModelSha256)).ToHashSet();
        if (!expectedStatic.SetEquals(currentStatic))
            throw new InvalidDataException("Mode16 publication omits or replaces an actual visible static source draw.");
        ImportNullFinalOwners(candidates);
        return new(this, frame, revision, candidates, actualCompleteCandidatePublicationProvenance);
    }

    internal void ValidateFrame(MapSourceCreatedF103CurrentFrame frame)
    {
        if (!retained || frame.FrameNumber <= 0 || frame.Camera.ExecutableLocale != native.Locale ||
            frame.ViewFields.Binding != binding || frame.CpuPopulation is null ||
            frame.Camera.Source.Camera.ObjectId != 0x38D04576 ||
            !ReferenceEquals(frame.Camera.Source, cameraSource))
            throw new InvalidDataException("Current View family publication differs from its retained source Main generation.");
        frame.ViewFields.Camera.Validate(frame.Camera);
    }
    internal long Revision => revision;
    private void ImportNullFinalOwners(DeferredSourceMode16Candidate[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var owner = candidate.FinalParent ?? throw new InvalidDataException("Current final primitive parent is unknown.");
            if (candidate.Family == "StaticModel" && owner.Object.ObjectId == 0x739D10B8 ||
                candidate.Family.StartsWith("Army", StringComparison.Ordinal) && owner.Object.ObjectId is not (0x739D10B8 or 0x2F9960EF))
                throw new InvalidDataException("Current primitive final owner differs from its actual source family.");
            if (nullFinalOwners.Contains(owner)) continue;
            var source = owner.Objects.Source;
            if (!finalCatalogs.TryGetValue(source, out var catalog))
            {
                ValidateCatalog(source);
                catalog = owner.Objects;
                finalCatalogs.Add(source, catalog);
            }
            if (owner.Object.ClassId != 0x18EB1E56 ||
                owner.Object.ObjectId is not (0x3E8912BC or 0x2F9960EF or 0xE83A2EF7 or 0x739D10B8))
                throw new InvalidDataException("Mode16 final owner is outside the supported original primitive creators.");
            var declared = ModelShaderResources.ReadDeclaredShader(catalog, owner.Object.ObjectId);
            if (declared.ShaderFileHash != owner.ShaderFileHash || declared.DependencyObjectHashes.Count != 24 ||
                !declared.DependencyObjectHashes.SequenceEqual(owner.DependencyObjectHashes) ||
                !ModelShaderResources.ResolveType16Interface(declared, 16).IsNull)
                throw new InvalidDataException("Current primitive has no declared null source state16.");
            nullFinalOwners.Add(owner);
        }
    }

    private static void ValidateCatalog(LoadedRdbAsset source)
    {
        if (source.OverrideSource is not null || source.ResourceUri != "san14://rdb/system/718CCB8B" ||
            source.DecodedSha256 != CatalogSha || Hash(source.Bytes) != CatalogSha)
            throw new InvalidDataException("Mode16 final owner differs from its exact original catalog source.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

/// <summary>Same-frame source family publication. Ordinary candidates remain
/// visible in the receipt while all selected state16 shaders are null. Terrain
/// can be nonempty but RenderType0 cannot enter its separate dispatch family.</summary>
public sealed class DeferredSourceViewFamilyPacket
{
    private readonly MapSourceCreatedF103CurrentFrame frame;
    private readonly MapCameraConstructionPacket camera;
    private readonly long revision;
    private readonly DeferredSourceMode16Candidate[] candidates;
    internal DeferredSourceViewFamilyPacket(DeferredSourceViewFamilyOwner owner,
        MapSourceCreatedF103CurrentFrame frame, long revision,
        DeferredSourceMode16Candidate[] candidates, string provenance)
    {
        Owner = owner; this.frame = frame; this.revision = revision; camera = frame.Camera;
        this.candidates = candidates.ToArray(); CurrentCandidatePublicationProvenance = provenance;
        LightDataF20 = frame.Lighting.LocalLightData.Span[0xF20];
    }
    public DeferredSourceViewFamilyOwner Owner { get; }
    public long FrameNumber => frame.FrameNumber;
    public string ViewGenerationIdentity => Owner.ViewGenerationIdentity;
    public bool Shapes1C18Absent => true;
    public uint SpecializedFullscreenCount1550 => 0;
    public byte LightDataF20 { get; }
    public bool IndexedBankGetterNullForBothHeaderStates => (LightDataF20 & 1) == 0;
    public int OrdinaryCandidateCount => candidates.Length;
    public int SelectedMode16ShaderSubmissions => 0;
    public bool TerrainExcludedByRenderType0 => true;
    public string CurrentCandidatePublicationProvenance { get; }
    public IReadOnlyList<DeferredSourceMode16Candidate> OrdinaryCandidates => Array.AsReadOnly(candidates.ToArray());
    public void ValidateCurrent(MapSourceCreatedF103CurrentFrame acceptedFrame)
    {
        if (!ReferenceEquals(frame, acceptedFrame) || revision != Owner.Revision ||
            !ReferenceEquals(camera, acceptedFrame.Camera) ||
            acceptedFrame.Lighting.LocalLightData.Span[0xF20] != LightDataF20)
            throw new InvalidDataException("View family receipt is stale or belongs to another accepted Camera/LightData frame.");
        Owner.ValidateFrame(acceptedFrame);
    }
}

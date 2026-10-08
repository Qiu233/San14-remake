using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum OriginalDeferredSourceViewFamilyScope
{
    FreshSourceMainNoExternalFamilyAssignments,
    ImportedOrEdited
}

/// <summary>One actual ordinary CPU candidate. A null state16 skips its shader
/// submission; it does not remove the candidate from the source collection.</summary>
public sealed record OriginalDeferredSourceMode16Candidate(string Family, string InstanceKey,
    int SourceDrawIndex, string ModelResourceUri, string ModelSha256,
    OriginalNamedModelShader FinalParent, string CurrentOwnerProvenance);

/// <summary>Source constructor and supported retained generation for the Main
/// View families outside the captured Camera3C0 prefix. Later external family
/// writers invalidate this owner; they are never replaced with fresh zeros.</summary>
public sealed class OriginalDeferredSourceViewFamilyOwner
{
    private const string CatalogSha = "67115E16E1B21065B4F04E25109F8182AB027B7DBD38B98979CB02BCFD1291B9";
    private readonly LoadedOriginalDeferredSamplerNativeSource native;
    private readonly OriginalMapLightingBinding binding;
    private readonly byte[] cameraSource;
    // The original catalog is immutable for this owner generation. Keep its
    // canonical parse, while validating its mutable source bytes before reuse.
    private readonly Dictionary<LoadedRdbAsset, LoadedObjects> finalCatalogs =
        new(ReferenceEqualityComparer.Instance);
    private bool retained = true;
    private long revision;
    public string ViewGenerationIdentity { get; }
    public string OwnerIdentity { get; } = "owned-source-Main-View-families:" + Guid.NewGuid().ToString("N");
    public string SourceProvenance { get; }

    private OriginalDeferredSourceViewFamilyOwner(LoadedOriginalDeferredSamplerNativeSource native,
        LoadedOriginalMapCameraConstructionSource source, OriginalMapLightingBinding binding, string generation)
    {
        this.native = native; this.binding = binding; ViewGenerationIdentity = generation;
        cameraSource = source.Resource.Source.Bytes.ToArray();
        SourceProvenance = native.ResourceUri + "@" + native.ExecutableSha256 + ";Main38D/source-Camera1550-zero-retained;" +
            "TC:C3E1AD/C3E1BF/C3E1C6/C3F000/AB5990/C409C0/D05D54/D05D5A;" +
            "View1C18-null=TC:D072CB;fresh-owned/no-external-family-assignments;ViewGeneration=" + generation;
    }

    public static OriginalDeferredSourceViewFamilyOwner CreateFresh(LoadedOriginalDeferredSamplerNativeSource native,
        LoadedOriginalMapCameraConstructionSource source, OriginalMapLightingBinding binding,
        string actualViewGenerationIdentity, OriginalDeferredSourceViewFamilyScope scope)
    {
        ArgumentNullException.ThrowIfNull(native); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(binding); ArgumentException.ThrowIfNullOrWhiteSpace(actualViewGenerationIdentity);
        if (scope != OriginalDeferredSourceViewFamilyScope.FreshSourceMainNoExternalFamilyAssignments)
            throw new NotSupportedException("Imported or edited View families require their current source publishers.");
        string exe = native.Locale switch
        {
            "SC" => "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
            "TC" => "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
            _ => throw new InvalidDataException("View families require the current pinned SC/TC native source.")
        };
        if (native.ExecutableSha256 != exe || source.Camera.ObjectId != 0x38D04576 ||
            source.Resource.Source.ResourceUri != "san14://rdb/system/908AB6E2" || source.Resource.Source.OverrideSource is not null ||
            source.Resource.Source.DecodedSha256 != OriginalDeferredAuxiliaryPrefixSource.CameraSha256 ||
            Hash(source.Resource.Source.Bytes) != source.Resource.Source.DecodedSha256 ||
            string.IsNullOrWhiteSpace(binding.ViewIdentity) || string.IsNullOrWhiteSpace(binding.CameraOwnerIdentity))
            throw new InvalidDataException("Fresh View families require the exact source Main and its actual owner generation.");
        return new(native, source, binding, actualViewGenerationIdentity);
    }

    public void MarkImportedEditedOrUnresolved(string actualWriterProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWriterProvenance);
        retained = false; revision = checked(revision + 1);
    }

    public OriginalDeferredSourceViewFamilyPacket Prepare(OriginalMapSourceCreatedF103CurrentFrame frame,
        IReadOnlyList<OriginalDeferredSourceMode16Candidate> completeOrdinaryCandidates,
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
        var finalOwners = CaptureNullFinalOwners(candidates);
        return new(this, frame, revision, candidates, actualCompleteCandidatePublicationProvenance, finalOwners);
    }

    internal void ValidateFrame(OriginalMapSourceCreatedF103CurrentFrame frame)
    {
        if (!retained || frame.FrameNumber <= 0 || frame.Camera.ExecutableLocale != native.Locale ||
            frame.ViewFields.Binding != binding || frame.CpuPopulation is null ||
            frame.Camera.Source.Camera.ObjectId != 0x38D04576 ||
            !frame.Camera.Source.Resource.Source.Bytes.AsSpan().SequenceEqual(cameraSource))
            throw new InvalidDataException("Current View family publication differs from its retained source Main generation.");
        frame.ViewFields.Camera.Validate(frame.Camera);
    }
    internal long Revision => revision;
    private NullFinalOwnerValidation CaptureNullFinalOwners(OriginalDeferredSourceMode16Candidate[] candidates)
    {
        var sources = new HashSet<LoadedRdbAsset>(ReferenceEqualityComparer.Instance);
        var owners = new Dictionary<OriginalNamedModelShader, OriginalNamedModelShader>(ReferenceEqualityComparer.Instance);
        foreach (var candidate in candidates)
        {
            var owner = candidate.FinalParent ?? throw new InvalidDataException("Current final primitive parent is unknown.");
            var source = owner.Objects.Source;
            if (sources.Add(source)) ValidateCatalog(source);
            if (owner.Object.ClassId != 0x18EB1E56 ||
                owner.Object.ObjectId is not (0x3E8912BC or 0x2F9960EF or 0xE83A2EF7 or 0x739D10B8))
                throw new InvalidDataException("Mode16 final owner is outside the exact supported original primitive creators.");
            if (!owners.ContainsKey(owner))
            {
                if (!finalCatalogs.TryGetValue(source, out var catalog))
                {
                    catalog = new LoadedObjects(DokReader.Read(source.Bytes), source);
                    finalCatalogs.Add(source, catalog);
                }
                var canonical = OriginalModelShaderResources.ReadDeclaredShader(catalog, owner.Object.ObjectId);
                if (canonical.ShaderFileHash != owner.ShaderFileHash || canonical.DependencyObjectHashes.Count != 24 ||
                    !canonical.DependencyObjectHashes.SequenceEqual(owner.DependencyObjectHashes) ||
                    !OriginalModelShaderResources.ResolveType16Interface(canonical, 16).IsNull)
                    throw new InvalidDataException("Current primitive has no exact literal-null source state16.");
                // Do not expose the cached canonical parse through the packet.
                owners.Add(owner, canonical with { DependencyObjectHashes = canonical.DependencyObjectHashes.ToArray() });
            }
            if (candidate.Family == "StaticModel" && owner.Object.ObjectId == 0x739D10B8 ||
                candidate.Family.StartsWith("Army", StringComparison.Ordinal) && owner.Object.ObjectId is not (0x739D10B8 or 0x2F9960EF))
                throw new InvalidDataException("Current primitive final owner differs from its actual source family.");
        }
        return new(sources.ToArray(), owners.ToArray());
    }

    private static void ValidateCatalog(LoadedRdbAsset source)
    {
        if (source.OverrideSource is not null || source.ResourceUri != "san14://rdb/system/718CCB8B" ||
            source.DecodedSha256 != CatalogSha || Hash(source.Bytes) != CatalogSha)
            throw new InvalidDataException("Mode16 final owner differs from its exact original catalog source.");
    }

    internal sealed class NullFinalOwnerValidation
    {
        private readonly LoadedRdbAsset[] sources;
        private readonly KeyValuePair<OriginalNamedModelShader, OriginalNamedModelShader>[] owners;
        internal NullFinalOwnerValidation(LoadedRdbAsset[] sources,
            KeyValuePair<OriginalNamedModelShader, OriginalNamedModelShader>[] owners)
            => (this.sources, this.owners) = (sources, owners);

        internal void ValidateCurrent()
        {
            // Bytes and dependency lists are externally mutable. Check them,
            // once per source/owner, without rebuilding the full DOK for each
            // visible instance every time a current-frame lease is required.
            foreach (var source in sources) ValidateCatalog(source);
            foreach (var pair in owners)
            {
                var actual = pair.Key;
                var canonical = pair.Value;
                if (!ReferenceEquals(actual.Objects.Source, canonical.Objects.Source) ||
                    actual.Object.ClassId != canonical.Object.ClassId || actual.Object.ObjectId != canonical.Object.ObjectId ||
                    actual.ShaderFileHash != canonical.ShaderFileHash ||
                    !actual.DependencyObjectHashes.SequenceEqual(canonical.DependencyObjectHashes))
                    throw new InvalidDataException("Current primitive has changed its accepted literal-null source state16 owner.");
            }
        }
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

/// <summary>Same-frame source family publication. Ordinary candidates remain
/// visible in the receipt while all selected state16 shaders are null. Terrain
/// can be nonempty but RenderType0 cannot enter its separate dispatch family.</summary>
public sealed class OriginalDeferredSourceViewFamilyPacket
{
    private readonly OriginalMapSourceCreatedF103CurrentFrame frame;
    private readonly byte[] camera;
    private readonly long revision;
    private readonly OriginalDeferredSourceMode16Candidate[] candidates;
    private readonly OriginalDeferredSourceViewFamilyOwner.NullFinalOwnerValidation finalOwners;
    internal OriginalDeferredSourceViewFamilyPacket(OriginalDeferredSourceViewFamilyOwner owner,
        OriginalMapSourceCreatedF103CurrentFrame frame, long revision,
        OriginalDeferredSourceMode16Candidate[] candidates, string provenance,
        OriginalDeferredSourceViewFamilyOwner.NullFinalOwnerValidation finalOwners)
    {
        Owner = owner; this.frame = frame; this.revision = revision; camera = frame.Camera.Prefix.ToArray();
        this.candidates = candidates.ToArray(); CurrentCandidatePublicationProvenance = provenance;
        this.finalOwners = finalOwners;
        LightDataF20 = frame.Lighting.LocalLightData.Span[0xF20];
    }
    public OriginalDeferredSourceViewFamilyOwner Owner { get; }
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
    public IReadOnlyList<OriginalDeferredSourceMode16Candidate> OrdinaryCandidates => Array.AsReadOnly(candidates.ToArray());
    public void ValidateCurrent(OriginalMapSourceCreatedF103CurrentFrame acceptedFrame)
    {
        if (!ReferenceEquals(frame, acceptedFrame) || revision != Owner.Revision ||
            !camera.AsSpan().SequenceEqual(acceptedFrame.Camera.Prefix.Span) ||
            acceptedFrame.Lighting.LocalLightData.Span[0xF20] != LightDataF20)
            throw new InvalidDataException("View family receipt is stale or belongs to another accepted Camera/LightData frame.");
        Owner.ValidateFrame(acceptedFrame);
        // Source resources retain mutable byte arrays. A later edited final
        // provider must invalidate the receipt before its no-op is submitted.
        finalOwners.ValidateCurrent();
    }
}

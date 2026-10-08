using System.Buffers.Binary;
using San14.Core;
using San14.Data;

/// <summary>One loaded season and the five actual registered Directional roots.
/// The CPU declarations are borrowed; their GPU resources remain with the
/// native resource owner. Installing a generation belongs between requests.</summary>
public sealed class OriginalD3D11CurrentSeasonResources
{
    internal sealed record Light(uint ObjectId, uint GroupMask,
        OriginalDirectionalLightProviderTemporary DefaultCaller);
    internal IReadOnlyList<Light> Lights { get; }
    internal IReadOnlyDictionary<int, uint> SelectedShadowWords { get; }
    public OriginalSceneViewNativeMapSeasonResourceBundle Source { get; }
    public OriginalMapSeasonSelection Selection => Source.Selection;
    public OriginalD3D11CurrentSeasonCpuResources? CpuResources { get; private set; }
    public float ControllerGeometry40 { get; }

    private OriginalD3D11CurrentSeasonResources(
        OriginalSceneViewNativeMapSeasonResourceBundle source, Light[] lights)
    {
        Source = source; Lights = lights;
        if (source.CameraSource.Selection.SecondPackage is not null)
            throw new NotSupportedException("Current Main needs its actual paired selection writer.");
        var selected = source.CameraSource.Selection.FirstPackage;
        SelectedShadowWords = OriginalDeferredEarlyShadowCurrentMainSource.LoadSelectedWords(selected);
        var geometry = selected.Camera.Properties[614];
        if (geometry.Index != 614 || geometry.NameHash != 0x59633E4E || geometry.TypeCode != 8 ||
            geometry.ElementCount != 1 || geometry.ElementSize != 4 || geometry.RawValue.Length != 4)
            throw new InvalidDataException("Current Main Cloud controller40 selected loader binding changed.");
        ControllerGeometry40 = BinaryPrimitives.ReadSingleLittleEndian(geometry.RawValue);
    }

    /// <summary>Bind the actual fresh Main/Aux default caller: force=-1,
    /// pair0/0, weight0, no external selector. Cache the static root/root
    /// temporary once; CBC770 still applies this request's prepared Camera.
    /// Other current selector/temporary branches require their own writer.</summary>
    public static OriginalD3D11CurrentSeasonResources BindFreshDefaultCaller(
        OriginalGameRepository repository, OriginalMainMapCameraFrame initialMain,
        OriginalSceneViewNativeMapSeasonResourceBundle source,
        IReadOnlyList<OriginalDirectionalLightConstructionPacket> actualOrderedRoots)
    {
        var roots = source.DirectionalDeclarations.Roots;
        if (source.Locale != initialMain.Camera.ExecutableLocale ||
            actualOrderedRoots.Count != roots.Count ||
            actualOrderedRoots.Where((root, index) =>
                root.SourceObjectId != roots[index].Root.ObjectId ||
                root.SourceUri != source.DirectionalDeclarations.Source.ResourceUri ||
                root.SourceSha256 != source.DirectionalDeclarations.Source.DecodedSha256).Any())
            throw new InvalidDataException("Season lighting requires its five ordered actual registered roots.");

        // Resolve this caller scope once at its load boundary. The current Eye
        // is not involved in the actual force=-1, pair0/0, weight0 branch.
        var commonSource = OriginalMapFreshCommonResources.ReadSourceTemplate(
            repository.LoadObjects("KIDSSystemResource", OriginalMapFreshCommonResources.SourceFileId));
        var common = OriginalMapFreshCommonResources.PrepareSourceCreatedBeforeAmbientPublication(
            commonSource, initialMain.Camera.FrameNumber, "d3d11-season/settings",
            "d3d11-season/common", "d3d11-season/view", "fresh-source-before-ambient");
        _ = OriginalDirectionalLightProviderCaller.CaptureFreshOwnedCaller(repository,
            initialMain, common, OriginalDirectionalProviderCallerScope.FreshOwnedCameraClearedLateControlsNoLaterSelectorWriter);
        var lights = actualOrderedRoots.Select(root => new Light(root.SourceObjectId,
            root.Metadata.SourceGroupMask,
            OriginalDirectionalLightConstruction.ConstructProviderRootZeroPairTemporary(root))).ToArray();
        return new(source, lights);
    }

    /// <summary>Load at initial creation or a real World-third-date season
    /// transition. The constructor callback supplies its actual retained
    /// allocation and ready resource bindings; this loader invents neither.</summary>
    public static OriginalD3D11CurrentSeasonResources Load(
        OriginalGameRepository repository, NativeMapSeasonState ownedSeason,
        OriginalMainMapCameraFrame initialMain,
        Func<OriginalMapSeasonDirectionalLightSourceSet,
            IReadOnlyList<OriginalDirectionalLightConstructionPacket>> createActualRoots)
    {
        var source = OriginalSceneViewNativeMapSeasonResourceBundle.Load(repository,
            ownedSeason, initialMain.Camera.ExecutableLocale, initialMain.Camera.FrameNumber);
        return BindFreshDefaultCaller(repository, initialMain, source,
            createActualRoots(source.DirectionalDeclarations));
    }

    /// <summary>Convenience for the native resource route. The actual source
    /// white/diffuse and provider Data are loaded CPU owners, not GPU handles.
    /// These identifiers stay in the constructor and never enter the numeric
    /// 328-byte upload. Native creates its own actual GPU resource owners.
    /// Retained constructor holes use the existing named-consumer backend
    /// representation; they are not original allocation byte observations.</summary>
    public static OriginalD3D11CurrentSeasonResources LoadSourceReadyCpu(
        OriginalGameRepository repository, NativeMapSeasonState ownedSeason,
        OriginalMainMapCameraFrame initialMain)
    {
        var source = OriginalSceneViewNativeMapSeasonResourceBundle.Load(repository,
            ownedSeason, initialMain.Camera.ExecutableLocale, initialMain.Camera.FrameNumber);
        var declarations = source.DirectionalDeclarations;
        var providers = OriginalMapDirectionalLightProviderOwner.Load(repository, declarations);
        var white = SourceResourceRegistrationFixed.LoadGameDefault(repository)
            .Single(binding => binding.Definition.ObjectId == 0x80F9DB7B);
        var diffuse = SourceResourceRegistrationDirectionalDiffuse.Load(repository);
        var representation = OriginalDirectionalLightRetainedNormalization.LoadKnownConsumers(
            repository, "native-current-season/CPU-numeric-consumers");
        // The IDs address these retained CPU owners only. Their distinct keys
        // resolve each actual lookup; they are never interpreted as pointers.
        var whiteBinding = new OriginalDirectionalLightResourceBinding(0x80F9DB7B, 1,
            white.Resource.Source.ResourceUri, true, SourceResourceRegistrationFixed.TextureInterfaceClassId);
        var diffuseBinding = new OriginalDirectionalLightResourceBinding(
            SourceResourceRegistrationDirectionalDiffuse.ObjectKey, 2,
            diffuse.Resource.Source.ResourceUri, true, SourceResourceRegistrationDirectionalDiffuse.TextureInterfaceClassId);
        var roots = declarations.Roots.Select(root =>
        {
            var available = new[] { providers.GetBinding(root.Root.ObjectId), whiteBinding, diffuseBinding };
            var requests = OriginalDirectionalLightConstruction.RequiredResourceKeys(declarations,
                root.Root.ObjectId, 1, available);
            var used = available.Where(binding => requests.Contains(binding.SourceObjectKey)).ToArray();
            if (requests.Count != used.Length)
                throw new NotSupportedException("Current season needs another actual source CPU resource owner.");
            return representation.ApplyConstructor(declarations, root.Root.ObjectId, 1, used);
        }).ToArray();
        var result = BindFreshDefaultCaller(repository, initialMain, source, roots);
        result.CpuResources = new(providers, white, diffuse, representation);
        return result;
    }
}

public sealed record OriginalD3D11CurrentSeasonCpuResources(
    OriginalMapDirectionalLightProviderOwner Providers,
    OriginalStaticTextureBinding White, OriginalStaticTextureBinding Diffuse,
    OriginalDirectionalLightRetainedNormalization RetainedRepresentation);

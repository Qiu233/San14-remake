using San14.Core;
using San14.Data;

/// <summary>One loaded season and the five actual registered Directional roots.
/// The CPU declarations are borrowed; their GPU resources remain with the
/// native resource owner. Installing a generation belongs between requests.</summary>
public sealed class D3D11CurrentSeasonResources
{
    internal sealed class Light
    {
        internal Light(uint objectId, uint groupMask, DirectionalLightProviderTemporary defaultCaller)
        {
            ObjectId = objectId; GroupMask = groupMask; DefaultCaller = defaultCaller;
            SelectedConstruction=defaultCaller.Root;
            GameControls=MapGameLightQualityOverlay.ReadCurrentControls(SelectedConstruction.Bytes.Span);
            var bytes=DirectionalLightConstruction.PrepareSelectedGameLightData(SelectedConstruction,GameControls);
            CurrentSource=DirectionalLightCurrentSource.FromPreparedConstruction(SelectedConstruction,bytes);
            NumericEncoding = new(bytes);
        }
        internal Light(Light registered,DirectionalLightConstructionPacket selected,MapGameLightQualityWrites controls)
        {
            ObjectId=registered.ObjectId;GroupMask=selected.Metadata.SourceGroupMask;
            DefaultCaller=registered.DefaultCaller;SelectedConstruction=selected;GameControls=controls;
            var bytes=DirectionalLightConstruction.PrepareSelectedGameLightData(selected,controls);
            CurrentSource=DirectionalLightCurrentSource.FromPreparedConstruction(selected,bytes);
            NumericEncoding=new(bytes);
        }
        internal DirectionalLightConstructionPacket SelectedConstruction {get;}
        internal uint ObjectId { get; }
        internal uint GroupMask { get; }
        internal DirectionalLightProviderTemporary DefaultCaller { get; }
        internal DirectionalLightCurrentSource CurrentSource { get; }
        internal D3D11CurrentLightEncoding NumericEncoding { get; }
        internal MapGameLightQualityWrites GameControls { get; }
    }
    internal IReadOnlyList<Light> Lights { get; }
    private readonly DirectionalLightConstructionPacket[][] modeLights;
    internal MapSeasonMainCameraModeSources CameraModes {get;}
    internal IReadOnlyList<D3D11CurrentSeasonCameraSettings> ModeSettings {get;}
    internal DirectionalLightConstructionPacket SelectModeLight(int registeredIndex,int successfulMode)
        =>modeLights[registeredIndex][successfulMode];
    internal CameraViewSettings ViewDefaults { get; }
    internal OceanWaveSettings OceanWaves { get; }
    internal OceanSurfaceSettings OceanSurface { get; }
    internal OceanOpticsSettings OceanOptics { get; }
    internal OceanTextureMaterialSettings OceanTextureMaterial { get; }
    internal OceanBubbleMaterialSettings OceanBubbleMaterial { get; }
    internal SkyStarSettings Stars { get; }
    internal HeightFogSettings HeightFog { get; }
    internal CommonRenderPolicy CommonPolicy { get; }
    public CommonRenderSettings InitialCommonSettings { get; }
    internal SkyCloudShaderSettings CloudShaderSettings { get; }
    internal PatternCameraSettings PatternSettings { get; }
    internal RainDropCameraSettings RainSettings { get; }
    internal IReadOnlyList<PatternLightTextureSource> PatternTextures { get; }
    public SceneViewNativeMapSeasonResourceBundle Source { get; private set; }
    public MapSeasonSelection Selection => Source.Selection;
    public D3D11CurrentSeasonCpuResources? CpuResources { get; private set; }
    public float ControllerGeometry40 { get; }
    public byte FreshExposureSelector { get; }

    private D3D11CurrentSeasonResources(GameResourceRepository repository,
        SceneViewNativeMapSeasonResourceBundle source, Light[] lights, byte exposureSelector,
        CommonRenderPolicy commonPolicy,CommonRenderSettings initialCommonSettings)
    {
        Source = source; Lights = lights; FreshExposureSelector = exposureSelector;
        CameraModes=new(repository,source.CameraSource);
        ModeSettings=source.CameraSource.Selection.Provider.CameraChildren.Prepend(
            source.CameraSource.Selection.Provider.Root).Select(package=>new D3D11CurrentSeasonCameraSettings(package)).ToArray();
        // Each child borrows actual named bindings retained by its registered
        // family. No guessed handle or synthetic resource readiness is added.
        modeLights=lights.Select((registered,index)=>
        {
            var root=registered.DefaultCaller.Root;
            var family=source.DirectionalDeclarations.Roots[index];
            var result=new DirectionalLightConstructionPacket[family.Children.Count+1];result[0]=root;
            for(int child=0;child<family.Children.Count;child++)
            {
                uint id=family.Children[child].ObjectId;
                var requests=DirectionalLightConstruction.RequiredResourceKeys(root.Source,id,root.ContextFlags3E4,root.ResourceBindings);
                var actual=root.ResourceBindings.Where(binding=>requests.Contains(binding.SourceObjectKey)).ToArray();
                if(requests.Count!=actual.Length)
                    throw new NotSupportedException("Selected Directional child requires another actual named resource owner.");
                result[child+1]=DirectionalLightCpuConstruction.Create(root.Source,id,root.ContextFlags3E4,actual,
                    DirectionalLightCpuInitialization.Default);
                DirectionalLightConstruction.RequireSharedPhysicalTextureSlots(root,result[child+1]);
            }
            return result;
        }).ToArray();
        PatternTextures = Array.AsReadOnly(lights.Select(light =>
        {
            var root = light.DefaultCaller.Root;
            var slot = root.ResourceSlots.Single(value => value.LightDataOffset == 0xFE8);
            return new PatternLightTextureSource(light.ObjectId, slot.ResolvedObjectKey);
        }).ToArray());
        CommonPolicy = commonPolicy;
        InitialCommonSettings = initialCommonSettings;
        if (source.CameraSource.Selection.SecondPackage is not null)
            throw new NotSupportedException("Current Main needs its actual paired selection writer.");
        var selected = ModeSettings[0];
        CloudShaderSettings = selected.Cloud;
        PatternSettings = selected.Pattern;
        RainSettings = selected.Rain;
        ViewDefaults = selected.View;
        OceanWaves = selected.Waves;
        OceanSurface = selected.Surface;
        OceanOptics = selected.Optics;
        OceanTextureMaterial = selected.Texture;
        OceanBubbleMaterial = selected.Bubble;
        Stars = selected.Stars;
        HeightFog = selected.Fog;
        ControllerGeometry40 = Stars.ExposureScale;
    }

    /// <summary>Adopt only the real started calendar, retaining all loaded CPU
    /// owners and settings. The caller installs it between native requests.</summary>
    public void AdoptStartedCampaignCalendar(NativeMapSeasonState actualNativeState,
        MainMapCameraFrame actualMain)
    {
        ArgumentNullException.ThrowIfNull(actualMain);
        if(actualMain.Camera.ExecutableLocale!=Source.Locale)
            throw new InvalidDataException("Calendar adoption cannot change the loaded season locale.");
        var camera=Source.CameraSource.AdoptStartedCampaignCalendar(actualNativeState,actualMain);
        var adopted=Source.WithAdoptedCamera(camera);
        CameraModes.AdoptStartedCampaignCalendar(actualNativeState,actualMain);
        Source=adopted;
    }

    /// <summary>Bind the actual fresh Main/Aux default caller: force=-1,
    /// pair0/0, weight0, no external selector. Cache the static root/root
    /// temporary once; CBC770 still applies this request's prepared Camera.
    /// Other current selector/temporary branches require their own writer.</summary>
    public static D3D11CurrentSeasonResources BindFreshDefaultCaller(
        GameResourceRepository repository, MainMapCameraFrame initialMain,
        SceneViewNativeMapSeasonResourceBundle source,
        IReadOnlyList<DirectionalLightConstructionPacket> actualOrderedRoots)
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
        var commonSource = CommonRenderResources.ImportSettings(
            repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId));
        var common = MapCommonRenderState.FromSettings(
            commonSource, initialMain.Camera.FrameNumber, "d3d11-season/settings",
            "d3d11-season/common", "d3d11-season/view", "fresh-source-before-ambient");
        DirectionalLightProviderCaller.RequireFreshDefaultCaller(initialMain, common);
        var lights = actualOrderedRoots.Select(root => new Light(root.SourceObjectId,
            root.Metadata.SourceGroupMask,
            DirectionalLightConstruction.ConstructProviderRootZeroPairTemporary(root))).ToArray();
        return new(repository,source, lights, common.ViewGroupSelector ?? 0,
            CommonRenderPolicy.FromAuxiliary(common.Auxiliary),commonSource);
    }

    /// <summary>Load at initial creation or a real World-third-date season
    /// transition. The constructor callback supplies its actual retained
    /// allocation and ready resource bindings; this loader invents neither.</summary>
    public static D3D11CurrentSeasonResources Load(
        GameResourceRepository repository, NativeMapSeasonState ownedSeason,
        MainMapCameraFrame initialMain,
        Func<MapSeasonDirectionalLightSourceSet,
            IReadOnlyList<DirectionalLightConstructionPacket>> createActualRoots)
    {
        var source = SceneViewNativeMapSeasonResourceBundle.Load(repository,
            ownedSeason, initialMain.Camera.ExecutableLocale, initialMain.Camera.FrameNumber);
        return BindFreshDefaultCaller(repository, initialMain, source,
            createActualRoots(source.DirectionalDeclarations));
    }

    /// <summary>Convenience for the native resource route. The actual source
    /// white/diffuse and provider Data are loaded CPU owners, not GPU handles.
    /// These identifiers stay in the constructor and never enter the numeric
    /// 808-byte upload. Native creates its own actual GPU resource owners.
    /// CPU compatibility storage and hemisphere fourth components have explicit
    /// project initialization, independent of selected shader programs.</summary>
    public static D3D11CurrentSeasonResources LoadSourceReadyCpu(
        GameResourceRepository repository, NativeMapSeasonState ownedSeason,
        MainMapCameraFrame initialMain)
    {
        var source = SceneViewNativeMapSeasonResourceBundle.Load(repository,
            ownedSeason, initialMain.Camera.ExecutableLocale, initialMain.Camera.FrameNumber);
        return LoadSourceReadyCpu(repository, source, initialMain);
    }

    public static D3D11CurrentSeasonResources LoadScenarioPreviewSourceReadyCpu(
        GameResourceRepository repository, MapSeasonScenarioPreviewFrame preview,
        MainMapCameraFrame initialMain)
    {
        ArgumentNullException.ThrowIfNull(preview); MapSeasonRuntime.ValidateFrame(preview);
        var source = SceneViewNativeMapSeasonResourceBundle.LoadScenarioPreview(repository,
            preview.Source, initialMain.Camera.ExecutableLocale, initialMain.Camera.FrameNumber);
        return LoadSourceReadyCpu(repository, source, initialMain);
    }

    private static D3D11CurrentSeasonResources LoadSourceReadyCpu(
        GameResourceRepository repository, SceneViewNativeMapSeasonResourceBundle source,
        MainMapCameraFrame initialMain)
    {
        var declarations = source.DirectionalDeclarations;
        var providers = MapDirectionalLightProviderOwner.Load(repository, declarations);
        var white = SourceResourceRegistrationFixed.LoadWhite(repository);
        var diffuse = SourceResourceRegistrationDirectionalDiffuse.Load(repository);
        // The IDs address these retained CPU owners only. Their distinct keys
        // resolve each actual lookup; they are never interpreted as pointers.
        var whiteBinding = new DirectionalLightResourceBinding(SourceResourceRegistrationFixed.WhiteObjectKey, 1,
            white.Resource.Source.ResourceUri, true, SourceResourceRegistrationFixed.TextureInterfaceClassId);
        var diffuseBinding = new DirectionalLightResourceBinding(
            SourceResourceRegistrationDirectionalDiffuse.ObjectKey, 2,
            diffuse.Resource.Source.ResourceUri, true, SourceResourceRegistrationDirectionalDiffuse.TextureInterfaceClassId);
        var roots = declarations.Roots.Select(root =>
        {
            var available = new[] { providers.GetBinding(root.Root.ObjectId), whiteBinding, diffuseBinding };
            var requests = DirectionalLightConstruction.RequiredResourceKeys(declarations,
                root.Root.ObjectId, 1, available);
            var used = available.Where(binding => requests.Contains(binding.SourceObjectKey)).ToArray();
            if (requests.Count != used.Length)
                throw new NotSupportedException("Current season needs another actual source CPU resource owner.");
            return DirectionalLightCpuConstruction.Create(declarations, root.Root.ObjectId, 1, used,
                DirectionalLightCpuInitialization.Default);
        }).ToArray();
        var result = BindFreshDefaultCaller(repository, initialMain, source, roots);
        result.CpuResources = new(providers, white, diffuse);
        return result;
    }
}

public sealed record D3D11CurrentSeasonCpuResources(
    MapDirectionalLightProviderOwner Providers,
    StaticTextureBinding White, StaticTextureBinding Diffuse);

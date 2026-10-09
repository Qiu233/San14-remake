using San14.Data;

/// <summary>Owns the actual five source resources and their constructed Game
/// default lighting generation. Fresh normal BGRA view87 and source cube95 are
/// selected here; subsequent sRGB or modifier bindings use their separate route.
/// Shader/scene callbacks must retire before this device owner is disposed.</summary>
public sealed class MapSourceCreatedLightingDeviceOwner : IAsyncDisposable
{
    public SourceResourceRegistrationAmbientCubeDevice Cube { get; }
    public SourceResourceRegistrationDevice Registration { get; }
    public MapSourceCreatedLightingSource Source { get; }
    public MapSourceCreatedLightingRetainedNormalization? RetainedNormalization { get; }
    private readonly object lifetimeGate = new();
    private Task? disposal;
    private MapSourceCreatedLightingDeviceOwner(SourceResourceRegistrationAmbientCubeDevice cube,
        SourceResourceRegistrationDevice registration, MapSourceCreatedLightingSource source,
        MapSourceCreatedLightingRetainedNormalization? normalization=null)
        => (Cube, Registration, Source, RetainedNormalization) = (cube, registration, source, normalization);

    /// <summary>Choose the bounded backend representation only for the actual
    /// default late767/early95/Sky3/4 consumers. The playable host must validate
    /// each current consumer before drawing. Native retained values remain
    /// unresolved; actual five resource owners and4078 source writes are kept.</summary>
    public static async Task<MapSourceCreatedLightingDeviceOwner> CreateFreshHostNormalizedGameDefaultAsync(
        GameResourceRepository repository,int setterCameraSlot,string ownedBackendIdentity,
        string executableLocale,bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if(setterCameraSlot is not(3 or 4))throw new ArgumentOutOfRangeException(nameof(setterCameraSlot));
        var context=MainMapContextOwnerSource.LoadFreshHostDefaults(executableLocale);
        var normalization=MapSourceCreatedLightingRetainedNormalization.LoadKnownConsumers(
            repository,ownedBackendIdentity,includeOrdinaryEarly126:true);
        var cube=await SourceResourceRegistrationAmbientCubeDevice.BuildAsync(
            SourceResourceRegistrationAmbientCube.LoadGameDefault(repository),allowReadback);
        SourceResourceRegistrationDevice? registration=null;
        try
        {
            registration=await SourceResourceRegistrationDevice.BuildAsync(repository,cube,87,allowReadback);
            var actual=await registration.GetConstructionInputsAsync(context.ContextFlags3E4,
                normalization.SourceProvenance+";"+context.SourceProvenance);
            var constructed=normalization.ApplyConstructor(MainLightBindingSource.LoadGameDefault(repository),
                context.ContextFlags3E4,actual.ResourceBindings);
            var source=MapSourceCreatedLightingSource.FromGameDefault(repository,setterCameraSlot,constructed);
            registration.ValidateConstructionSource(source);
            return new(cube,registration,source,normalization);
        }
        catch
        {
            if(registration is not null)await registration.DisposeAsync();
            await cube.DisposeAsync();throw;
        }
    }

    /// <summary>Select the proved native Host constructor context. Runtime
    /// mutations use CreateGameDefaultAsync with their explicit current flags.</summary>
    public static Task<MapSourceCreatedLightingDeviceOwner> CreateFreshHostGameDefaultAsync(
        GameResourceRepository repository, int setterCameraSlot,
        IReadOnlyList<MapSourceCreatedLightingRetainedRange> explicitRetained210,
        string retainedProvenance, string executableLocale, bool allowReadback=false)
    {
        var context=MainMapContextOwnerSource.LoadFreshHostDefaults(executableLocale);
        return CreateGameDefaultAsync(repository,setterCameraSlot,explicitRetained210,
            retainedProvenance+";"+context.SourceProvenance,context.ContextFlags3E4,allowReadback);
    }

    /// <summary>Consume the detached current allocation capture and its exact
    /// context metadata without repeating ranges or their provenance.</summary>
    public static Task<MapSourceCreatedLightingDeviceOwner> CreateGameDefaultAsync(
        GameResourceRepository repository,int setterCameraSlot,
        MapSourceCreatedLightingRetainedAllocation retained,bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(retained);
        return CreateGameDefaultAsync(repository,setterCameraSlot,retained.RetainedRanges,
            retained.SourceProvenance,retained.ContextFlags3E4,allowReadback);
    }

    public static Task<MapSourceCreatedLightingDeviceOwner> CreateFreshHostGameDefaultAsync(
        GameResourceRepository repository,int setterCameraSlot,
        MapSourceCreatedLightingRetainedAllocation retained,string executableLocale,bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(retained);
        var context=MainMapContextOwnerSource.LoadFreshHostDefaults(executableLocale);
        if(retained.ContextFlags3E4!=context.ContextFlags3E4)
            throw new InvalidDataException("Current retained allocation belongs to a different context than the fresh source Host.");
        return CreateGameDefaultAsync(repository,setterCameraSlot,retained.RetainedRanges,
            retained.SourceProvenance+";"+context.SourceProvenance,context.ContextFlags3E4,allowReadback);
    }

    /// <summary>Loads and creates the exact5 Static sources, forms actual ready
    /// receipts, applies all proved source constructor/default fields and selects
    /// the actual Game setter Camera3/4. Only the native210 retained bytes and
    /// current context/provenance are supplied; no caller resource-key plumbing.</summary>
    public static async Task<MapSourceCreatedLightingDeviceOwner> CreateGameDefaultAsync(
        GameResourceRepository repository, int setterCameraSlot,
        IReadOnlyList<MapSourceCreatedLightingRetainedRange> explicitRetained210,
        string retainedProvenance, uint contextFlags3E4, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(explicitRetained210);
        ArgumentException.ThrowIfNullOrWhiteSpace(retainedProvenance);
        if (setterCameraSlot is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(setterCameraSlot));
        var declaration = MainLightBindingSource.LoadGameDefault(repository);
        byte[] template = MapSourceCreatedLightingRetainedSource.BuildGameDefaultTemplate(
            declaration, explicitRetained210, contextFlags3E4);
        var cube = await SourceResourceRegistrationAmbientCubeDevice.BuildAsync(
            SourceResourceRegistrationAmbientCube.LoadGameDefault(repository), allowReadback);
        SourceResourceRegistrationDevice? registration = null;
        try
        {
            registration = await SourceResourceRegistrationDevice.BuildAsync(repository, cube, 87, allowReadback);
            var inputs = await registration.GetConstructionInputsAsync(contextFlags3E4,
                retainedProvenance + ";explicit-native-retained210;source-writes4078");
            var packet = SourceResourceRegistration.ConstructGameDefault(repository, registration.Source, template, inputs);
            var source = MapSourceCreatedLightingSource.FromGameDefault(repository, setterCameraSlot, packet);
            registration.ValidateConstructionSource(source);
            return new(cube, registration, source);
        }
        catch
        {
            if (registration is not null) await registration.DisposeAsync();
            await cube.DisposeAsync(); throw;
        }
    }

    public ValueTask DisposeAsync()
    { lock (lifetimeGate) return new(disposal ??= RetireAsync()); }
    private async Task RetireAsync()
    { await Registration.DisposeAsync(); await Cube.DisposeAsync(); }
}

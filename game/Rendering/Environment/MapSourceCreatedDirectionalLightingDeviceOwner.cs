using System.Collections.ObjectModel;
using San14.Data;

public sealed record DirectionalLightingAllocation(ReadOnlyMemory<byte> CompleteTemplate,
    string RetainedProvenance, IReadOnlyList<DirectionalLightResourceBinding> OtherLookupResults);

/// <summary>Owns the seasonal source73 GPU texture and five
/// independent constructor snapshots. Provider scheduling and retained values
/// are explicit inputs; constructing roots does not select their providers.</summary>
public sealed partial class MapSourceCreatedDirectionalLightingDeviceOwner : IAsyncDisposable
{
    public RegisteredTextureDevice Diffuse { get; }
    public IReadOnlyList<MapSourceCreatedDirectionalLightingSource> Sources { get; private set; }
    public DirectionalLightRetainedNormalization? RetainedNormalization { get; private set; }
    public MapDirectionalLightProviderOwner? Providers { get; private set; }
    private RegisteredTextureDevice? fixedWhite;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetimeGate = new();
    private volatile bool retiring;
    private Task? disposal;

    private MapSourceCreatedDirectionalLightingDeviceOwner(
        RegisteredTextureDevice diffuse,
        MapSourceCreatedDirectionalLightingSource[] sources)
        => (Diffuse, Sources) = (diffuse, Array.AsReadOnly(sources));

    /// <summary>Create the actual ready seasonal CPU providers and original
    /// white/diffuse GPU resources. Each constructor receives only the lookup
    /// results consumed on its actual ready-resource branch. Provider current
    /// selection remains a separate caller operation.</summary>
    public static async Task<MapSourceCreatedDirectionalLightingDeviceOwner> CreateSourceReadyNormalizedAsync(
        GameResourceRepository repository, MapSeasonDirectionalLightSourceSet season,
        LoadedMapCameraConstructionSource mainCamera, uint contextFlags3E4,
        string backendOwnerIdentity, uint? explicitDiffuseViewFormat = null,
        uint? explicitWhiteViewFormat = null, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(season);
        ArgumentNullException.ThrowIfNull(mainCamera); ArgumentException.ThrowIfNullOrWhiteSpace(backendOwnerIdentity);
        var actual = MapSeasonDirectionalLightSources.Read(season.Database);
        if (mainCamera.Camera.ObjectId != MainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Ready seasonal resources require the actual Main38D Camera constructor.");
        var providers = MapDirectionalLightProviderOwner.Load(repository, actual);
        var normalization = DirectionalLightRetainedNormalization.LoadKnownConsumers(repository, backendOwnerIdentity);
        RegisteredTextureDevice? white = null;
        RegisteredTextureDevice? diffuse = null;
        try
        {
            white = await RegisteredTextureDevice.BuildAsync(SourceResourceRegistrationFixed.LoadWhite(repository), explicitWhiteViewFormat, allowReadback);
            diffuse = await RegisteredTextureDevice.BuildAsync(SourceResourceRegistrationDirectionalDiffuse.Load(repository), explicitDiffuseViewFormat, allowReadback);
            var whiteBinding = white.GetBinding();
            var diffuseBinding = diffuse.GetBinding();
            var sources = actual.Roots.Select(root =>
            {
                var available = new[] { providers.GetBinding(root.Root.ObjectId), whiteBinding, diffuseBinding };
                var requested = DirectionalLightConstruction.RequiredResourceKeys(actual, root.Root.ObjectId,
                    contextFlags3E4, available);
                var used = available.Where(binding => requested.Contains(binding.SourceObjectKey)).ToArray();
                if (!requested.Order().SequenceEqual(used.Select(b => b.SourceObjectKey).Order()))
                    throw new NotSupportedException("Seasonal Directional requires another actual ready resource owner.");
                var template = normalization.CreateTemplate(actual, root.Root.ObjectId);
                var construction = DirectionalLightConstruction.Apply(actual, root.Root.ObjectId,
                    template.CompleteTemplate.Span, new(contextFlags3E4, template.SourceProvenance, used));
                normalization.AdoptConstruction(construction);
                return MapSourceCreatedDirectionalLightingSource.FromMainCamera(mainCamera, construction);
            }).ToArray();
            return new(diffuse, sources) { fixedWhite = white, Providers = providers, RetainedNormalization = normalization };
        }
        catch
        {
            try { if (diffuse is not null) await diffuse.DisposeAsync(); }
            finally { if (white is not null) await white.DisposeAsync(); }
            throw;
        }
    }

    public async Task ValidateResourcesAsync()
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            Diffuse.RequireAvailable();
            fixedWhite?.RequireAvailable();
        }
        finally { gate.Release(); }
    }

    /// <summary>Create the bounded backend representation for the audited
    /// named consumers. Their actual per-frame receipts remain mandatory;
    /// this factory does not claim native retained byte identity or select
    /// provider lookup outcomes.</summary>
    public static async Task<MapSourceCreatedDirectionalLightingDeviceOwner> CreateNormalizedAsync(
        GameResourceRepository repository, MapSeasonDirectionalLightSourceSet season,
        LoadedMapCameraConstructionSource mainCamera, uint contextFlags3E4,
        IReadOnlyDictionary<uint, IReadOnlyList<DirectionalLightResourceBinding>> actualOtherLookupResults,
        string backendOwnerIdentity, uint? explicitNativeViewFormat = null, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(actualOtherLookupResults);
        var actual = MapSeasonDirectionalLightSources.Read(season.Database);
        if (!actual.Roots.Select(r => r.Root.ObjectId).Order().SequenceEqual(actualOtherLookupResults.Keys.Order()))
            throw new InvalidDataException("Normalized seasonal lighting requires all five explicit lookup result sets.");
        var normalization = DirectionalLightRetainedNormalization.LoadKnownConsumers(repository, backendOwnerIdentity);
        var allocations = actual.Roots.ToDictionary(r => r.Root.ObjectId, r =>
        {
            var template = normalization.CreateTemplate(actual, r.Root.ObjectId);
            return new DirectionalLightingAllocation(template.CompleteTemplate, template.SourceProvenance,
                actualOtherLookupResults[r.Root.ObjectId]);
        });
        var owner = await CreateAsync(repository, actual, mainCamera, contextFlags3E4,
            allocations, explicitNativeViewFormat, allowReadback);
        try
        {
            foreach (var source in owner.Sources) normalization.AdoptConstruction(source.Construction);
            owner.RetainedNormalization = normalization;
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
    }

    public static async Task<MapSourceCreatedDirectionalLightingDeviceOwner> CreateAsync(
        GameResourceRepository repository, MapSeasonDirectionalLightSourceSet season,
        LoadedMapCameraConstructionSource mainCamera, uint contextFlags3E4,
        IReadOnlyDictionary<uint, DirectionalLightingAllocation> allocations,
        uint? explicitNativeViewFormat = null, bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(season);
        ArgumentNullException.ThrowIfNull(mainCamera); ArgumentNullException.ThrowIfNull(allocations);
        var actual = MapSeasonDirectionalLightSources.Read(season.Database);
        uint[] roots = actual.Roots.Select(r => r.Root.ObjectId).ToArray();
        if (!roots.Order().SequenceEqual(allocations.Keys.Order()) ||
            mainCamera.Camera.ObjectId != MainMapCameraAdapter.MainCameraObjectId)
            throw new InvalidDataException("Seasonal GPU lighting needs all five actual roots and the independent Main38D Camera.");
        // Snapshot and validate caller inputs before creating GPU resources.
        var inputs = roots.ToDictionary(id => id, id =>
        {
            var allocation = allocations[id];
            if (allocation is null || allocation.CompleteTemplate.Length != DirectionalLightConstruction.LightDataBytes ||
                string.IsNullOrWhiteSpace(allocation.RetainedProvenance) || allocation.OtherLookupResults is null ||
                allocation.OtherLookupResults.Any(b => b is null || b.SourceObjectKey == SourceResourceRegistrationDirectionalDiffuse.ObjectKey))
                throw new InvalidDataException("Each Directional needs its own explicit retained template and other lookup owners; FE8 belongs to this device owner.");
            return allocation with { CompleteTemplate = allocation.CompleteTemplate.ToArray(),
                OtherLookupResults = Array.AsReadOnly(allocation.OtherLookupResults.ToArray()) };
        });
        var diffuse = await RegisteredTextureDevice.BuildAsync(SourceResourceRegistrationDirectionalDiffuse.Load(repository),
            explicitNativeViewFormat, allowReadback);
        try
        {
            var binding = diffuse.GetBinding();
            var sources = roots.Select(id =>
            {
                var allocation = inputs[id];
                var requests = DirectionalLightConstruction.RequiredResourceKeys(actual, id, contextFlags3E4,
                    allocation.OtherLookupResults.Append(binding).ToArray());
                bool usesDiffuse = requests.Contains(binding.SourceObjectKey);
                var lookups = usesDiffuse ? allocation.OtherLookupResults.Append(binding).ToArray()
                    : allocation.OtherLookupResults.ToArray();
                var construction = DirectionalLightConstruction.Apply(actual, id,
                    allocation.CompleteTemplate.Span,
                    new(contextFlags3E4, allocation.RetainedProvenance, Array.AsReadOnly(lookups)));
                if (usesDiffuse && construction.ResourceSlots[1].EffectiveDataHandle != binding.DataHandle)
                    throw new InvalidDataException("Directional FE8 differs from its actual ready GPU texture.");
                return MapSourceCreatedDirectionalLightingSource.FromMainCamera(mainCamera, construction);
            }).ToArray();
            return new(diffuse, sources);
        }
        catch { await diffuse.DisposeAsync(); throw; }
    }

    /// <summary>Prepare all roots against a single accepted Main seasonal frame.
    /// Each root retains its own caller binding and constructor identity.</summary>
    public async Task<IReadOnlyDictionary<uint, MapSourceCreatedLightingPacket>> PrepareFramesAsync(
        IReadOnlyDictionary<uint, MapSourceCreatedLightingFrameInputs> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            if (!Sources.Select(s => s.Construction.SourceObjectId).Order().SequenceEqual(inputs.Keys.Order()))
                throw new InvalidDataException("Current seasonal frame must include all five independent root inputs.");
            Diffuse.RequireAvailable();
            fixedWhite?.RequireAvailable();
            var frames = Sources.ToDictionary(s => s.Construction.SourceObjectId,
                s => s.PrepareFrame(inputs[s.Construction.SourceObjectId]));
            var first = frames.Values.First();
            if (frames.Values.Any(f => f.FrameNumber != first.FrameNumber ||
                f.Binding.ViewIdentity != first.Binding.ViewIdentity ||
                f.Binding.CameraOwnerIdentity != first.Binding.CameraOwnerIdentity ||
                !f.PreparedCameraPrefix.Span.SequenceEqual(first.PreparedCameraPrefix.Span)))
                throw new InvalidDataException("Seasonal root frames must share the actual current View and Main Camera preparation.");
            return new ReadOnlyDictionary<uint, MapSourceCreatedLightingPacket>(frames);
        }
        finally { gate.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (lifetimeGate)
        {
            retiring = true;
            if (disposal is null || disposal.IsFaulted || disposal.IsCanceled)
                disposal = RetireAsync();
            return new(disposal);
        }
    }
    private async Task RetireAsync()
    {
        await gate.WaitAsync();
        try
        {
            try { await Diffuse.DisposeAsync(); }
            finally { if (fixedWhite is not null) await fixedWhite.DisposeAsync(); }
        }
        finally { gate.Release(); }
    }
}

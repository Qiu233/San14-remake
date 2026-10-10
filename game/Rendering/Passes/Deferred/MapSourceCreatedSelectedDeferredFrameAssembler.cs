using System.Buffers.Binary;
using San14.Data;

/// <summary>Actual newly constructed F103 selection and source sampler keys for
/// this accepted attempt. Geometry, complete CB templates and graph production
/// come from the same accepted frame; missing current producers are rejected.</summary>
public sealed record MapSourceCreatedSelectedDeferredFrameInputs(
    DeferredF103SelectedSource CurrentSelection,
    MapSelectedSourceSamplerPlan CurrentSamplers,
    MapSourceCreatedDeferredFrameInputs Geometry,
    IReadOnlyList<DeferredAuxiliaryTargetDevice>? ProducedAuxiliaryDevices = null);

/// <summary>Complete selected original frame assembly with borrowed GPU owners.
/// Owns its CPU lighting provider. It does not create geometry, current graph
/// contents, shadow caches or cluster membership as an implicit fallback.</summary>
public sealed class MapSourceCreatedSelectedDeferredFrameAssembler : IAsyncDisposable
{
    public MapSourceCreatedLightingDeferredProvider LightingProvider { get; }
    public NativeDeferredResources Resources { get; }
    public MapSelectedSourceSamplerOwner Samplers { get; }
    public MapSelectedSourceImageOwner Images { get; }
    public NativeDeferredBufferOwner? Buffers { get; }
    public MapMaterialCachePrepared InitializedMaterials { get; }
    public NativeMapModelPopulationRenderer PopulationRenderer { get; }
    public IReadOnlyList<uint> RequiredRemainingImageRegisters { get; }
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetimeGate = new();
    private bool retiring;
    private Task? disposal;
    private readonly SortedDictionary<long, MapSelectedSourceImageBindings> auxiliaryLeases = [];
    private long begunAttempt;

    /// <summary>F103 EADEE8 passes the literal directional capacity4. The three
    /// identities name the owned current View/Camera/light binding; source
    /// setter slots do not infer the active Main Camera owner.</summary>
    public static MapLightingBinding CreateSourceBinding(DeferredPixelProgram program,
        string viewIdentity, string cameraOwnerIdentity, string lightOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(program);
        if (program.SourceSelection is null) throw new InvalidDataException("F103 source binding requires its selected original program.");
        foreach (string identity in new[] { viewIdentity, cameraOwnerIdentity, lightOwnerIdentity })
            ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        return new(viewIdentity, cameraOwnerIdentity, lightOwnerIdentity,
            program.Source.Source.ResourceUri + "@" + program.Source.Source.DecodedSha256,
            program.Combination.Index, 4, MapLightingBasePath.UnmodifiedOrdinary);
    }

    private MapSourceCreatedSelectedDeferredFrameAssembler(
        MapSourceCreatedLightingDeferredProvider lighting,
        NativeDeferredResources resources, MapSelectedSourceSamplerOwner samplers,
        MapSelectedSourceImageOwner images, NativeDeferredBufferOwner? buffers,
        MapMaterialCachePrepared initializedMaterials, NativeMapModelPopulationRenderer populationRenderer)
    {
        LightingProvider = lighting; Resources = resources; Samplers = samplers;
        Images = images; Buffers = buffers; InitializedMaterials = initializedMaterials; PopulationRenderer = populationRenderer;
        var known = images.Source.GraphTargets.Select(t => t.PixelBinding.BindPoint)
            .Append(images.Source.RequireRole("AmbientSpecular").Declaration.BindPoint).ToHashSet();
        RequiredRemainingImageRegisters = Array.AsReadOnly(resources.RequiredExplicitInputs
            .Where(d => d.DescriptorKind == "Texture" && !known.Contains(d.Source.BindPoint))
            .Select(d => d.Source.BindPoint).Order().ToArray());
    }

    public static async Task<MapSourceCreatedSelectedDeferredFrameAssembler> FromSourceOwnersAsync(
        DeferredPixelProgram program, SourceResourceRegistrationDevice registration,
        MapSourceCreatedLightingSource source, MapLightingBinding binding,
        NativeDeferredResources resources, MapSelectedSourceSamplerOwner samplers,
        MapSelectedSourceImageOwner images, NativeDeferredBufferOwner? buffers,
        MapMaterialCachePrepared initializedMaterials, NativeMapModelPopulationRenderer populationRenderer)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(resources); ArgumentNullException.ThrowIfNull(samplers);
        ArgumentNullException.ThrowIfNull(images); ArgumentNullException.ThrowIfNull(initializedMaterials);
        ArgumentNullException.ThrowIfNull(populationRenderer);
        var selected = program.SourceSelection ?? throw new InvalidDataException("Selected frame requires its full compiled current F103 selection.");
        if (binding.DirectionalShaderCapacity != 4)
            throw new InvalidDataException("Actual source node166 uses its proved F103 directional capacity4.");
        _ = program.ReadValidatedSelectedSourceDeviceBindings();
        await registration.ValidateAsync(); registration.ValidateConstructionSource(source);
        if (!ReferenceEquals(resources.Program, program) || buffers is not null && !ReferenceEquals(buffers.Program, program) ||
            !ReferenceEquals(images.Source.Selection, selected) || !ReferenceEquals(samplers.Source.Selection, selected) ||
            !SameSourceGraph(resources.SourcePlan, selected.SourcePlan))
            throw new InvalidDataException("Selected frame owners differ from the same source program, Camera generation and graph.");
        var materialSource = initializedMaterials.SelectedSource;
        if (materialSource is null || initializedMaterials.Cache.Branch != MapMaterialCacheBranch.FreshExternalSource ||
            materialSource.MaterialObject.ObjectId != 0x2C7443D2 || materialSource.MaterialRecordsSource.FileId != 0x6F1BEDE7 ||
            !SameSourceGraph(resources.SourcePlan, materialSource.Deferred) || initializedMaterials.Table.EntryCount != 2 ||
            !initializedMaterials.Table.Bytes.AsSpan().SequenceEqual(Convert.FromHexString("0000000002000000")) ||
            !resources.MaterialTableSource.Bytes.AsSpan().SequenceEqual(initializedMaterials.Table.Bytes))
            throw new InvalidDataException("Selected source frame requires the actual node166 external cache and complete two-row table [0,2].");
        if (populationRenderer.MaterialCache.CacheIdentity != initializedMaterials.Cache.CacheIdentity ||
            populationRenderer.MaterialCache.UploadSha256 != initializedMaterials.Cache.UploadSha256 ||
            populationRenderer.MaterialCache.ExternalSourceIdentity != initializedMaterials.Cache.ExternalSourceIdentity)
            throw new InvalidDataException("Selected frame must borrow the single population owner's initialized source material cache.");
        var lighting = new MapSourceCreatedLightingDeferredProvider(program, source, binding, registration);
        try
        {
            var owner = new MapSourceCreatedSelectedDeferredFrameAssembler(lighting, resources, samplers,
                images, buffers, initializedMaterials, populationRenderer);
            owner.ValidateCurrentProgramScope(samplers.Source);
            owner.ValidateStaticAmbientSampler(samplers.Source);
            owner.ValidateOwnedGroups(samplers.Source);
            return owner;
        }
        catch { lighting.Dispose(); throw; }
    }

    /// <summary>Run in the serial callback using the actual attempt identity.
    /// Source-selected LAS constructor fields are automatic only while its
    /// exact fresh Common/module proof is present and unassigned.</summary>
    public MapSourceCreatedLightingPacket PrepareLightingFrame(NativeMapFrameContext context,
        MapSourceCreatedLightingFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(input);
        lock (lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            CompletePreviousReads(context);
            if (context.FrameNumber <= 0 || context.PreparedCamera is null ||
                PreparedCameraSceneSource.ReadCompletePrefix(input.PriorPreparedCameraPrefix.Span) != context.PreparedCamera)
                throw new InvalidDataException("Source lighting requires the actual attempt and Camera already accepted by geometry.");
            if (LightingProvider.Program.SelectedSourceNullSpecialization?.SourceLocalAmbient is { } las)
            {
                if (input.AdditionalSceneInputs.LocalAmbient is { } current && current != las.DataDefaults)
                    throw new InvalidDataException("Fresh source LAS fields were edited; actual current LocalAmbient producers are required.");
                input = input with { AdditionalSceneInputs = input.AdditionalSceneInputs with { LocalAmbient = las.DataDefaults } };
            }
            var packet = LightingProvider.PrepareFrame(input with { FrameNumber = context.FrameNumber });
            if (packet.SceneInputs.Camera != context.PreparedCamera)
                throw new InvalidDataException("Source lighting preparation changed the Camera already accepted by geometry.");
            return packet;
        }
    }

    public MapSourceCreatedLightingPacket PrepareLightingFrame(NativeMapFrameContext context,
        MapSourceCreatedLightingFrameInputs input, MapSelectedSourceSamplerPlan currentSamplers)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(currentSamplers);
        lock (lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            ValidateCurrentProgramScope(currentSamplers);
            if (currentSamplers.Selection.Current.FrameNumber != context.FrameNumber || context.PreparedCamera is null ||
                currentSamplers.Selection.Camera.SceneInputs != context.PreparedCamera ||
                PreparedCameraSceneSource.ReadCompletePrefix(input.PriorPreparedCameraPrefix.Span) != context.PreparedCamera)
                throw new InvalidDataException("Selected lighting and source selection require the actual accepted terrain Camera and attempt.");
            if (currentSamplers.SourceNullSpecialization?.SourceLocalAmbient is { } las)
            {
                if (input.AdditionalSceneInputs.LocalAmbient is { } current && current != las.DataDefaults)
                    throw new InvalidDataException("Fresh source LAS fields were edited; actual current LocalAmbient producers are required.");
                input = input with { AdditionalSceneInputs = input.AdditionalSceneInputs with { LocalAmbient = las.DataDefaults } };
            }
            var packet = LightingProvider.PrepareFrame(input with { FrameNumber = context.FrameNumber });
            if (packet.SceneInputs.Camera != context.PreparedCamera)
                throw new InvalidDataException("Source lighting preparation changed the Camera already accepted by geometry.");
            return packet;
        }
    }

    public async Task<MapSourceCreatedDeferredFramePacket> PrepareDeferredAsync(
        NativeMapFrameContext accepted, MapSourceCreatedLightingPacket lighting,
        MapSourceCreatedSelectedDeferredFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(accepted); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Geometry);
        var geometry = input.Geometry;
        MapSelectedSourceImageBindings? pendingAuxiliary = null;
        await gate.WaitAsync();
        try
        {
            lock (lifetimeGate) ObjectDisposedException.ThrowIf(retiring, this);
            ValidateCurrentProgramScope(input.CurrentSamplers);
            var population = PopulationRenderer.LastAcceptedFrame;
            if (population is null || !ReferenceEquals(population.Frame, geometry.Models) ||
                !ReferenceEquals(population.Context, geometry.ModelAcceptedContext))
                throw new InvalidDataException("Selected frame must borrow the single model owner's actual successful accepted result.");
            var ownedFeature = input.CurrentSelection.OwnedFeatureSource ??
                throw new InvalidDataException("Production selected frame requires its actual owned kind1 feature producer.");
            ownedFeature.ValidateFrame(input.CurrentSelection.Camera, lighting);
            if (!ReferenceEquals(input.CurrentSelection, input.CurrentSamplers.Selection) ||
                input.CurrentSelection.Current.FrameNumber != accepted.FrameNumber || !geometry.Models.Complete ||
                geometry.ModelAcceptedContext.FrameNumber != accepted.FrameNumber ||
                !ReferenceEquals(geometry.ModelAcceptedContext.TerrainFrame, accepted.TerrainFrame) ||
                !ReferenceEquals(geometry.ModelAcceptedContext.OutputFrame, accepted.OutputFrame) ||
                geometry.ModelAcceptedContext.PreparedCamera != accepted.PreparedCamera || accepted.PreparedCamera is null ||
                lighting.FrameNumber != accepted.FrameNumber || lighting.SceneInputs.Camera != accepted.PreparedCamera ||
                input.CurrentSelection.Camera.SceneInputs != accepted.PreparedCamera)
                throw new InvalidDataException("Selected Deferred requires successful same-frame geometry, actual targets, current selection and source Camera.");
            uint[] remaining = geometry.RemainingSourceImages.Select(i => i?.SourceBindPoint ?? uint.MaxValue).Order().ToArray();
            if (!remaining.SequenceEqual(RequiredRemainingImageRegisters) || geometry.RemainingSourceImages.Any(i =>
                i is null || i.DescriptorKind != "Texture" || !i.Resource.IsValid || i.OwnerTexture is not { IsValid: true } ||
                string.IsNullOrWhiteSpace(i.SourceResourceUri)))
                throw new InvalidDataException("Selected original frame lacks the actual remaining image owners at t" + string.Join(",t", RequiredRemainingImageRegisters));
            if (geometry.TransparencySelection.SourceOverrideEd8 !=
                BinaryPrimitives.ReadInt32LittleEndian(lighting.LocalLightData.Span.Slice(0xED8, 4)))
                throw new InvalidDataException("Selected transparency must consume this same current local LightData+ED8.");
            var materials = MapMaterialCacheProvider.Prepare(InitializedMaterials.Cache,
                geometry.Models.Packets, geometry.TerrainMaterials, geometry.ExplicitCurrentModelMaterial) with
                { SelectedSource = InitializedMaterials.SelectedSource };
            var currentImages = input.CurrentSamplers.ImagePlan;
            IReadOnlyList<DeferredDeviceBinding> auxiliary;
            if (input.ProducedAuxiliaryDevices is { } devices)
            {
                pendingAuxiliary = await Images.BorrowTargetBindingsAsync(currentImages, geometry.TransparencySelection,
                    devices, explicitRefractionEnabled: true);
                auxiliary = pendingAuxiliary.Bindings;
            }
            else auxiliary = await Images.ReadTargetBindingsAsync(currentImages, geometry.TransparencySelection,
                geometry.ProducedAuxiliaryTargets, explicitRefractionEnabled: true);
            var registration = LightingProvider.Registration!;
            await registration.ValidateAsync(); registration.ValidateConstructionSource(LightingProvider.Source);
            var cube = registration.Entries[SourceResourceRegistrationAmbientCube.ObjectKey];
            if (BinaryPrimitives.ReadUInt64LittleEndian(lighting.LocalLightData.Span.Slice(0xFE0, 8)) != cube.TextureRid.Id)
                throw new InvalidDataException("Selected kind1 ambient must retain the actual constructed FE0 static cube generation.");
            ValidateStaticAmbientSampler(input.CurrentSamplers); ValidateOwnedGroups(input.CurrentSamplers);
            var ambient = new DeferredDeviceBinding("Texture", currentImages.RequireRole("AmbientSpecular").Declaration.BindPoint,
                cube.TextureRid, cube.TextureRid, cube.OwnerProvenance + ";kind1-LightDataFE0");
            var typed = Buffers is null ? [] : Buffers.Combine([]);
            var combined = auxiliary.Append(ambient).Concat(Samplers.GetBindings(input.CurrentSamplers))
                .Concat(typed).Concat(geometry.RemainingSourceImages).ToArray();
            if (combined.Select(i => (i.DescriptorKind, i.SourceBindPoint)).Distinct().Count() != combined.Length)
                throw new InvalidDataException("Selected source producers override another owned resource role.");
            var template = await Resources.AssemblePacketAsync(accepted.TerrainFrame, geometry.CompleteConstantBuffers,
                Array.AsReadOnly(combined), geometry.Raster);
            var deferred = await LightingProvider.PrepareDeferredAsync(accepted, template, lighting, input.CurrentSelection);
            if (pendingAuxiliary is not null)
            {
                lock (lifetimeGate)
                {
                    if (auxiliaryLeases.ContainsKey(accepted.FrameNumber))
                        throw new InvalidDataException("Selected auxiliary dependencies were already prepared for this render attempt.");
                    auxiliaryLeases.Add(accepted.FrameNumber, pendingAuxiliary); pendingAuxiliary = null;
                }
            }
            return new(accepted, lighting, materials, deferred);
        }
        finally { if (pendingAuxiliary is not null) await pendingAuxiliary.DisposeAsync(); gate.Release(); }
    }

    /// <summary>Call at the scene's BeforeFrame boundary, after the attempt is
    /// assigned and before any terrain/auxiliary writer. Serial queue order
    /// ends failed prior reads; lifetime dependencies remain retained.</summary>
    public void CompletePreviousReads(NativeMapFrameContext current)
    {
        ArgumentNullException.ThrowIfNull(current);
        lock (lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            if (current.FrameNumber <= 0 || current.FrameNumber < begunAttempt)
                throw new InvalidDataException("Selected auxiliary reads require monotonically ordered actual scene attempts.");
            begunAttempt = current.FrameNumber;
            foreach (var pair in auxiliaryLeases.Where(p => p.Key < current.FrameNumber)) pair.Value.CompleteRead();
        }
    }

    /// <summary>Call only after this attempt's actual Deferred draw completed.
    /// Release its read phase and retire previous cached-draw dependencies;
    /// the current lifetime stays until replacement or target retirement.</summary>
    public async Task CompleteDeferredReadsAsync(NativeMapFrameContext accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted); await gate.WaitAsync();
        try
        {
            MapSelectedSourceImageBindings[] stale;
            lock (lifetimeGate)
            {
                if (auxiliaryLeases.TryGetValue(accepted.FrameNumber, out var current)) current.CompleteRead();
                var keys = auxiliaryLeases.Keys.Where(k => k < accepted.FrameNumber).ToArray();
                stale = keys.Select(k => auxiliaryLeases[k]).ToArray();
                foreach (long key in keys) auxiliaryLeases.Remove(key);
            }
            foreach (var lease in stale) await lease.DisposeAsync();
        }
        finally { gate.Release(); }
    }

    /// <summary>The scene retires its cached Deferred draw first. Run this
    /// before the auxiliary source device owners retire their actual textures.</summary>
    public async Task RetireTargetDependenciesAsync()
    {
        await gate.WaitAsync();
        try
        {
            MapSelectedSourceImageBindings[] leases;
            lock (lifetimeGate) { leases = auxiliaryLeases.Values.ToArray(); auxiliaryLeases.Clear(); }
            foreach (var lease in leases) await lease.DisposeAsync();
        }
        finally { gate.Release(); }
    }

    /// <summary>Preserve the complete existing stage chain. Source read phases
    /// end before the next attempt writes; texture lifetimes retire after the
    /// scene's cached Deferred draw and before borrowed producers dispose.</summary>
    public NativeMapSceneStages Stages(NativeMapSceneStages? previous = null) =>
        (previous ?? new NativeMapSceneStages()) with
    {
        BeforeTargetRetirement = async context =>
        {
            await RetireTargetDependenciesAsync();
            if (previous?.BeforeTargetRetirement is { } retire) await retire(context);
        },
        AfterDeferred = async context =>
        {
            try { return previous?.AfterDeferred is { } after ? await after(context) : null; }
            finally { await CompleteDeferredReadsAsync(context); }
        },
        BeforeFrame = async context =>
        {
            CompletePreviousReads(context);
            if (previous?.BeforeFrame is { } before) await before(context);
        }
    };

    private void ValidateCurrentProgramScope(MapSelectedSourceSamplerPlan current)
    {
        current.Validate(); LightingProvider.Program.ValidateCompatibleCurrentSelection(current.Selection);
        if (current.Selection.OwnedFeatureSource is not { } feature ||
            feature.LightingSourceIdentity != LightingProvider.Source.Identity || feature.DirectionalCapacity4 != 4)
            throw new InvalidDataException("Selected production frame requires the exact owned kind1 Camera/light/View feature generation.");
        if (current.EffectiveBindings.Interface.Sha256 != LightingProvider.Program.Translation.SpirvSha256 ||
            !current.EffectiveBindings.Bindings.Select(DeclarationIdentity).SequenceEqual(
                LightingProvider.Program.DescriptorBindings.Bindings.Select(DeclarationIdentity)) ||
            current.SourceNullSpecialization?.ProofPairs.Any(pair => !pair.SourceEvidence.StartsWith(
                LightingProvider.Source.ConstructionProvenance + ";", StringComparison.Ordinal)) == true)
            throw new InvalidDataException("Current selected source sampler/null proof differs from the compiled bytecode or constructed light generation.");
    }

    private void ValidateStaticAmbientSampler(MapSelectedSourceSamplerPlan current)
    {
        var cube = LightingProvider.Registration!.Entries[SourceResourceRegistrationAmbientCube.ObjectKey];
        byte[] key = RenderSamplerSourceAdapters.AmbientSpecularKey(SourceResourceRegistrationAmbientCube.GetSamplerKey(cube.Source));
        var consumer = current.ImagePlan.Samplers.Single(s => s.KeySource.Role == "AmbientSpecular");
        var binding = current.Bindings.SingleOrDefault(b => b.Register == consumer.Declaration.BindPoint);
        byte[] descriptor = RenderSamplerSourceAdapters.DecodeKey(current.Native, key).EncodeD3D11Descriptor();
        if (binding is null || !binding.Recipe.RawKey.AsSpan().SequenceEqual(key) ||
            !current.SourceDescriptors.TryGetValue(binding.Register, out var actual) || !actual.Span.SequenceEqual(descriptor))
            throw new InvalidDataException("Selected static ambient requires its real FE0 cube and final native key159430.");
    }

    private void ValidateOwnedGroups(MapSelectedSourceSamplerPlan current)
    {
        var neededSamplers = Resources.RequiredExplicitInputs.Where(d => d.DescriptorKind == "Sampler").Select(d => d.Source.BindPoint).Order();
        if (!neededSamplers.SequenceEqual(Samplers.GetBindings(current).Select(b => b.SourceBindPoint).Order()))
            throw new InvalidDataException("Selected sampler owner does not cover every remaining declared source sampler.");
        var neededBuffers = Resources.RequiredExplicitInputs.Where(d => d.DescriptorKind == "TextureBuffer").Select(d => d.Source.BindPoint).Order();
        var actual = Buffers is null ? [] : Buffers.Combine([]);
        if (!neededBuffers.SequenceEqual(actual.Select(b => b.SourceBindPoint).Order()))
            throw new InvalidDataException("Selected frame requires every active actual typed-buffer producer group.");
    }

    private static (string, uint, uint, uint?, uint?) DeclarationIdentity(TranslatedShaderBinding d) =>
        (d.DescriptorKind, d.Source.BindPoint, d.Device.VariableId, d.Device.DescriptorSet, d.Device.Binding);
    private static bool SameSourceGraph(LoadedDeferredTexturePlan a, LoadedDeferredTexturePlan b) =>
        a.Node.Index == 166 && b.Node.Index == 166 && a.Graph.Source.ResourceUri == b.Graph.Source.ResourceUri &&
        a.Graph.Source.DecodedSha256 == b.Graph.Source.DecodedSha256 && a.Shader.Source.ResourceUri == b.Shader.Source.ResourceUri &&
        a.Shader.Source.DecodedSha256 == b.Shader.Source.DecodedSha256;
    public ValueTask DisposeAsync()
    { lock (lifetimeGate) { retiring = true; return new(disposal ??= RetireAsync()); } }
    private async Task RetireAsync()
    { await RetireTargetDependenciesAsync(); LightingProvider.Dispose(); }
}

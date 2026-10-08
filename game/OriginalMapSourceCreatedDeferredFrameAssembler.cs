using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data;

/// <summary>Current accepted geometry and graph producers for one source-created
/// frame. Auxiliary target allocation alone does not establish produced contents.
/// Current callbacks and full CB templates are explicit producer inputs.</summary>
public sealed record OriginalMapSourceCreatedDeferredFrameInputs(
    OriginalNativeMapFrameContext ModelAcceptedContext,
    OriginalMapStaticModelPreparedFrame Models,
    IReadOnlyList<OriginalMapMaterialTerrainConsumer> TerrainMaterials,
    IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> CompleteConstantBuffers,
    IReadOnlyList<OriginalRenderTargetFrame> ProducedAuxiliaryTargets,
    OriginalDeferredTransparencyImageSelection TransparencySelection,
    IReadOnlyList<OriginalDeferredDeviceBinding> RemainingSourceImages,
    OriginalDeferredRasterState Raster,
    Func<OriginalModelGbufferInstancePacket, OriginalMapMaterialIdSource?>? ExplicitCurrentModelMaterial = null)
{
    /// <summary>Borrow the population owner's actual successful result without
    /// rebuilding its model layer or repeating its accepted frame/Camera.</summary>
    public static OriginalMapSourceCreatedDeferredFrameInputs FromAcceptedPopulation(
        OriginalNativeMapModelPopulationAcceptedFrame population,
        IReadOnlyList<OriginalMapMaterialTerrainConsumer> terrainMaterials,
        IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> completeConstantBuffers,
        IReadOnlyList<OriginalRenderTargetFrame> producedAuxiliaryTargets,
        OriginalDeferredTransparencyImageSelection transparencySelection,
        IReadOnlyList<OriginalDeferredDeviceBinding> remainingSourceImages,
        OriginalDeferredRasterState raster,
        Func<OriginalModelGbufferInstancePacket, OriginalMapMaterialIdSource?>? explicitCurrentModelMaterial = null)
    {
        ArgumentNullException.ThrowIfNull(population);
        return new(population.Context, population.Frame, terrainMaterials, completeConstantBuffers,
            producedAuxiliaryTargets, transparencySelection, remainingSourceImages, raster, explicitCurrentModelMaterial);
    }
}

public sealed record OriginalMapSourceCreatedDeferredFramePacket(
    OriginalNativeMapFrameContext AcceptedContext,
    OriginalMapSourceCreatedLightingPacket Lighting,
    OriginalMapMaterialCachePrepared Materials,
    OriginalNativeDeferredPacket Deferred);

/// <summary>Combines the actual source-created kind1 Game main light, five Static
/// owners, native sampler keys, graph auxiliary targets, typed buffers and the
/// actual selected material table with the accepted terrain/model Camera. Owns
/// only its CPU lighting provider; GPU stages, target frames and model layer are
/// borrowed. Current IES, volumetric fog, shadow and local ambient image producers
/// remain mandatory unless their exact compiled source-null adapter removes them.</summary>
public sealed class OriginalMapSourceCreatedDeferredFrameAssembler : IAsyncDisposable
{
    public OriginalMapSourceCreatedLightingDeferredProvider LightingProvider { get; }
    public OriginalNativeDeferredResources Resources { get; }
    public OriginalNativeDeferredSamplerOwner Samplers { get; }
    public OriginalNativeDeferredImageOwner Images { get; }
    public OriginalNativeDeferredBufferOwner? Buffers { get; }
    public OriginalMapMaterialCachePrepared InitializedMaterials { get; }
    public IReadOnlyList<uint> RequiredRemainingImageRegisters { get; }
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetimeGate = new();
    private bool retiring;
    private Task? disposal;

    private OriginalMapSourceCreatedDeferredFrameAssembler(
        OriginalMapSourceCreatedLightingDeferredProvider lighting,
        OriginalNativeDeferredResources resources, OriginalNativeDeferredSamplerOwner samplers,
        OriginalNativeDeferredImageOwner images, OriginalNativeDeferredBufferOwner? buffers,
        OriginalMapMaterialCachePrepared initializedMaterials)
    {
        LightingProvider = lighting; Resources = resources; Samplers = samplers;
        Images = images; Buffers = buffers; InitializedMaterials = initializedMaterials;
        RequiredRemainingImageRegisters = Array.AsReadOnly(resources.RequiredExplicitInputs
            .Where(d => d.DescriptorKind == "Texture" && d.Source.BindPoint is not (2 or 7 or 8 or 9))
            .Select(d => d.Source.BindPoint).Order().ToArray());
    }

    public static async Task<OriginalMapSourceCreatedDeferredFrameAssembler> FromSourceOwnersAsync(
        OriginalDeferredPixelProgram program, SourceResourceRegistrationDevice registration,
        OriginalMapSourceCreatedLightingSource source, OriginalMapLightingBinding binding,
        OriginalNativeDeferredResources resources, OriginalNativeDeferredSamplerOwner samplers,
        OriginalNativeDeferredImageOwner images, OriginalNativeDeferredBufferOwner? buffers,
        OriginalMapMaterialCachePrepared initializedMaterials)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(resources); ArgumentNullException.ThrowIfNull(samplers);
        ArgumentNullException.ThrowIfNull(images); ArgumentNullException.ThrowIfNull(initializedMaterials);
        await registration.ValidateAsync(); registration.ValidateConstructionSource(source);
        if (!ReferenceEquals(resources.Program, program) ||
            buffers is not null && !ReferenceEquals(buffers.Program, program) ||
            !SamePlan(resources.SourcePlan, images.Source.Deferred) ||
            !SamePlan(resources.SourcePlan, samplers.Source.TexturePlan))
            throw new InvalidDataException("Source-created frame owners require the same original shader, node166 and texture plan.");
        var selected = initializedMaterials.SelectedSource;
        if (selected is null || initializedMaterials.Cache.Branch != OriginalMapMaterialCacheBranch.FreshExternalSource ||
            selected.MaterialObject.ObjectId != 0x2C7443D2 || selected.MaterialRecordsSource.FileId != 0x6F1BEDE7 ||
            !SamePlan(resources.SourcePlan, selected.Deferred) || initializedMaterials.Table.EntryCount != 2 ||
            !initializedMaterials.Table.Bytes.AsSpan().SequenceEqual(Convert.FromHexString("0000000002000000")) ||
            !resources.MaterialTableSource.Bytes.AsSpan().SequenceEqual(initializedMaterials.Table.Bytes))
            throw new InvalidDataException("Source-created frame requires the actual F103 selected two-row material cache [0,2].");
        var nativeNulls = program.NullImageSpecialization?.NullPixelRegisters.Order().ToArray() ?? [];
        if (!nativeNulls.SequenceEqual(images.FreshSourceNulls.Select(n => n.PixelRegister).Order()) ||
            !nativeNulls.SequenceEqual(samplers.Source.SourceNulls.Select(n => n.PixelRegister).Order()) ||
            images.FreshSourceNulls.Concat(samplers.Source.SourceNulls).Any(n =>
                !n.SourceEvidence.StartsWith(source.ConstructionProvenance + ";", StringComparison.Ordinal)))
            throw new InvalidDataException("Frame shader, sampler and image owners require the same constructed fresh-null generation.");
        if ((program.IesSourceNull is null) != (samplers.Source.FreshSourceIesNull is null) ||
            program.IesSourceNull is { } ies && samplers.Source.FreshSourceIesNull?.SourceEvidence != ies.SourceEvidence)
            throw new InvalidDataException("Frame shader and source sampler owner require the same selected Camera's fresh IES null scope.");
        var lighting = new OriginalMapSourceCreatedLightingDeferredProvider(program, source, binding, registration);
        try
        {
            var owner = new OriginalMapSourceCreatedDeferredFrameAssembler(lighting, resources, samplers,
                images, buffers, initializedMaterials);
            owner.ValidateStaticAmbientSampler();
            owner.ValidateOwnedRegisterGroups();
            return owner;
        }
        catch { lighting.Dispose(); throw; }
    }

    /// <summary>Prepare first; use its SceneInputs.Camera for terrain, population,
    /// armies, territory and every accepted native callback of this same frame.</summary>
    public OriginalMapSourceCreatedLightingPacket PrepareLightingFrame(OriginalMapSourceCreatedLightingFrameInputs input)
    {
        lock (lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            if (LightingProvider.Program.LocalAmbientNullSpecialization is { } las)
            {
                las.Source.Validate();
                if (input.AdditionalSceneInputs.LocalAmbient is { } current && current != las.Source.DataDefaults)
                    throw new InvalidDataException("Fresh unassigned LAS data requires its proved constructor CB defaults; edited values need the current source route.");
                input = input with { AdditionalSceneInputs = input.AdditionalSceneInputs with { LocalAmbient = las.Source.DataDefaults } };
            }
            return LightingProvider.PrepareFrame(input);
        }
    }

    /// <summary>Prepare under the scene serial callback using its actual attempt
    /// identity. The supplied full Camera must be the Camera already used by
    /// terrain; a completed scene count cannot predict failed/retried attempts.</summary>
    public OriginalMapSourceCreatedLightingPacket PrepareLightingFrame(
        OriginalNativeMapFrameContext context, OriginalMapSourceCreatedLightingFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(input);
        if (context.FrameNumber <= 0 || context.PreparedCamera is null ||
            OriginalPreparedCameraSceneSource.ReadCompletePrefix(input.PriorPreparedCameraPrefix.Span) != context.PreparedCamera)
            throw new InvalidDataException("Source lighting requires the actual serial attempt and the accepted full Camera already used by terrain.");
        var packet = PrepareLightingFrame(input with { FrameNumber = context.FrameNumber });
        if (packet.SceneInputs.Camera != context.PreparedCamera)
            throw new InvalidDataException("Source lighting preparation changed the accepted terrain Camera.");
        return packet;
    }

    public async Task<OriginalMapSourceCreatedDeferredFramePacket> PrepareDeferredAsync(
        OriginalNativeMapFrameContext accepted, OriginalMapSourceCreatedLightingPacket lighting,
        OriginalMapSourceCreatedDeferredFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(accepted); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.ModelAcceptedContext);
        ArgumentNullException.ThrowIfNull(input.Models); ArgumentNullException.ThrowIfNull(input.TerrainMaterials);
        ArgumentNullException.ThrowIfNull(input.CompleteConstantBuffers); ArgumentNullException.ThrowIfNull(input.ProducedAuxiliaryTargets);
        ArgumentNullException.ThrowIfNull(input.RemainingSourceImages); ArgumentNullException.ThrowIfNull(input.Raster);
        await gate.WaitAsync();
        try
        {
            lock (lifetimeGate) ObjectDisposedException.ThrowIf(retiring, this);
            if (!input.Models.Complete || input.ModelAcceptedContext.FrameNumber != accepted.FrameNumber ||
                !ReferenceEquals(input.ModelAcceptedContext.TerrainFrame, accepted.TerrainFrame) ||
                !ReferenceEquals(input.ModelAcceptedContext.OutputFrame, accepted.OutputFrame) ||
                input.ModelAcceptedContext.PreparedCamera != accepted.PreparedCamera ||
                accepted.FrameNumber != lighting.FrameNumber || accepted.PreparedCamera is null ||
                accepted.PreparedCamera != lighting.SceneInputs.Camera)
                throw new InvalidDataException("Source-created Deferred requires successful geometry from the same accepted frame, targets and prepared Camera.");
            uint[] remaining = input.RemainingSourceImages.Select(i => i?.SourceBindPoint ?? uint.MaxValue).Order().ToArray();
            if (!remaining.SequenceEqual(RequiredRemainingImageRegisters) ||
                input.RemainingSourceImages.Any(i => i is null || i.DescriptorKind != "Texture" ||
                    !i.Resource.IsValid || i.OwnerTexture is not { IsValid: true } || string.IsNullOrWhiteSpace(i.SourceResourceUri)))
                throw new InvalidDataException("Source-created Deferred requires actual image producers at t" +
                    string.Join(",t", RequiredRemainingImageRegisters) + "; missing/null/duplicate descriptors are not substitutes.");
            int sourceOverride = BinaryPrimitives.ReadInt32LittleEndian(lighting.LocalLightData.Span.Slice(0xED8, 4));
            if (input.TransparencySelection.SourceOverrideEd8 != sourceOverride)
                throw new InvalidDataException("Current auxiliary transparency selection must consume this same local LightData+ED8.");
            var materials = OriginalMapMaterialCacheProvider.Prepare(InitializedMaterials.Cache,
                input.Models.Packets, input.TerrainMaterials, input.ExplicitCurrentModelMaterial) with
            { SelectedSource = InitializedMaterials.SelectedSource };
            var auxiliary = await Images.ReadTargetBindingsAsync(input.TransparencySelection,
                input.ProducedAuxiliaryTargets, explicitRefractionEnabled: true);
            var registration = LightingProvider.Registration!;
            await registration.ValidateAsync(); registration.ValidateConstructionSource(LightingProvider.Source);
            var cube = registration.Entries[SourceResourceRegistrationAmbientCube.ObjectKey];
            if (BinaryPrimitives.ReadUInt64LittleEndian(lighting.LocalLightData.Span.Slice(0xFE0, 8)) != cube.TextureRid.Id)
                throw new InvalidDataException("Kind1 ambient must retain the same actual constructed LightData+FE0 source cube owner.");
            ValidateStaticAmbientSampler();
            var ambient = new OriginalDeferredDeviceBinding("Texture", 7, cube.TextureRid, cube.TextureRid,
                cube.OwnerProvenance + ";kind1-LightDataFE0;D77162/D189D0");
            var bufferBindings = Buffers is null ? [] : Buffers.Combine([]);
            var combined = auxiliary.Concat(new[] { ambient }).Concat(Samplers.GetBindings())
                .Concat(bufferBindings).Concat(input.RemainingSourceImages).ToArray();
            if (combined.Select(i => (i.DescriptorKind, i.SourceBindPoint)).Distinct().Count() != combined.Length)
                throw new InvalidDataException("Source producers override another source-owned Deferred register.");
            var template = await Resources.AssemblePacketAsync(accepted.TerrainFrame,
                input.CompleteConstantBuffers, Array.AsReadOnly(combined), input.Raster);
            var deferred = await LightingProvider.PrepareDeferredAsync(accepted, template, lighting);
            return new(accepted, lighting, materials, deferred);
        }
        finally { gate.Release(); }
    }

    private void ValidateStaticAmbientSampler()
    {
        var cube = LightingProvider.Registration!.Entries[SourceResourceRegistrationAmbientCube.ObjectKey];
        byte[] key = OriginalDeferredSamplerSources.AmbientSpecularKey(
            SourceResourceRegistrationAmbientCube.GetSamplerKey(cube.Source));
        var binding = Samplers.Source.Bindings.SingleOrDefault(b => b.Register == 7);
        byte[] descriptor = OriginalDeferredSamplerSources.DecodeKey(Samplers.Source.Native, key).EncodeD3D11Descriptor();
        if (binding is null || !binding.Recipe.RawKey.AsSpan().SequenceEqual(key) ||
            !binding.Recipe.EncodeD3D11Descriptor().AsSpan().SequenceEqual(descriptor) ||
            !Samplers.OriginalDescriptors.TryGetValue(7, out var actual) || !actual.Span.SequenceEqual(descriptor))
            throw new InvalidDataException("Static kind1 ambient requires the original final s7 key159430; the source file's WRAP sampler is a different phase.");
    }

    private void ValidateOwnedRegisterGroups()
    {
        var samplers = Resources.RequiredExplicitInputs.Where(d => d.DescriptorKind == "Sampler")
            .Select(d => d.Source.BindPoint).Order();
        if (!samplers.SequenceEqual(Samplers.GetBindings().Select(b => b.SourceBindPoint).Order()))
            throw new InvalidDataException("Frame sampler owner must cover every remaining original sampler register.");
        var buffers = Resources.RequiredExplicitInputs.Where(d => d.DescriptorKind == "TextureBuffer")
            .Select(d => d.Source.BindPoint).Order();
        var actual = Buffers is null ? [] : Buffers.Combine([]);
        if (!buffers.SequenceEqual(actual.Select(b => b.SourceBindPoint).Order()))
            throw new InvalidDataException("Frame requires the complete actual typed-buffer producer groups or their proved source-null shader path.");
    }

    private static bool SamePlan(LoadedOriginalDeferredTexturePlan a, LoadedOriginalDeferredTexturePlan b) =>
        a.Node.Index == 166 && b.Node.Index == 166 && a.Graph.Source.ResourceUri == b.Graph.Source.ResourceUri &&
        a.Graph.Source.DecodedSha256 == b.Graph.Source.DecodedSha256 &&
        a.Shader.Source.ResourceUri == b.Shader.Source.ResourceUri && a.Shader.Source.DecodedSha256 == b.Shader.Source.DecodedSha256 &&
        a.ResourceBindings.Combination.Index == b.ResourceBindings.Combination.Index;

    public ValueTask DisposeAsync()
    {
        lock (lifetimeGate) { retiring = true; return new(disposal ??= RetireAsync()); }
    }
    private async Task RetireAsync()
    {
        await gate.WaitAsync();
        try { LightingProvider.Dispose(); }
        finally { gate.Release(); }
    }
}

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using San14.Data;

/// <summary>Projects each actual ordered World F103 pass into the source
/// PS383 GPU packet. Program, images and seasonal resource owners are borrowed
/// and must outlive this provider and its scene callbacks.</summary>
public sealed class MapSourceCreatedDirectionalDeferredProvider : IDisposable
{
    public DeferredPixelProgram Program { get; }
    public MapSourceCreatedDirectionalLightingSource Source => Resources.Sources.Single(source=>source.Identity.DeclaredGroupMask==0x80);
    public MapSourceCreatedDirectionalLightingDeviceOwner Resources { get; }
    private readonly object lifetimeGate = new();
    private readonly ConditionalWeakTable<MapSourceCreatedDirectionalLightingSource,
        IReadOnlyList<DeferredSourceNull2D>> currentNullSources=new();
    private bool retired;

    public MapSourceCreatedDirectionalDeferredProvider(DeferredPixelProgram program,
        MapSourceCreatedDirectionalLightingDeviceOwner resources,
        MapSourceCreatedDirectionalLightingSource source)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(source);
        if (!resources.Sources.Contains(source) || source.Identity.DeclaredGroupMask != 0x80 ||
            program.SourceSelection is not { } selected || selected.Combination.Index != 510 ||
            selected.PixelProgramIndex != 383 || selected.PixelProgramSha256 != DeferredSelected383Constants.PixelProgramSha256 ||
            selected.OwnedFeatureSource?.LightingSourceIdentity != source.Identity)
            throw new InvalidDataException("World deferred provider requires its actual owned outer80 source and the complete source510/PS383 program.");
        var device = program.ReadValidatedSelectedSourceDeviceBindings();
        if (device.Interface.Sha256 != program.Translation.SpirvSha256 ||
            !device.Bindings.Select(d => (d.DescriptorKind, d.Source.BindPoint, d.Device.VariableId, d.Device.DescriptorSet, d.Device.Binding))
                .SequenceEqual(program.DescriptorBindings.Bindings.Select(d =>
                    (d.DescriptorKind, d.Source.BindPoint, d.Device.VariableId, d.Device.DescriptorSet, d.Device.Binding))))
            throw new InvalidDataException("World deferred program differs from its verified selected device interface.");
        (Program, Resources) = (program, resources);
    }

    public async Task<NativeDeferredPacket> PrepareDeferredAsync(NativeMapFrameContext frame,
        NativeDeferredPacket explicitTemplate, MapSourceCreatedLightingPacket lighting,
        DeferredF103SelectedSource currentSelection, DeferredSelected383AdditionalInputs additional)
    {
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(explicitTemplate);
        ArgumentNullException.ThrowIfNull(lighting); ArgumentNullException.ThrowIfNull(currentSelection);
        ArgumentNullException.ThrowIfNull(additional);
        lock (lifetimeGate) ObjectDisposedException.ThrowIf(retired, this);
        await Resources.ValidateResourcesAsync();
        lock (lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(retired, this);
            if (lighting.SourceIdentity != Source.Identity || frame.FrameNumber != lighting.FrameNumber ||
                frame.PreparedCamera is null || frame.PreparedCamera != lighting.SceneInputs.Camera)
                throw new InvalidDataException("World deferred pass needs this source's same accepted Main Camera and frame.");
            Program.ValidateCompatibleCurrentSelection(currentSelection);
            _=CaptureCurrentSourceNullProofs(currentSelection);
            var feature = currentSelection.OwnedFeatureSource ?? throw new InvalidDataException("World deferred pass is missing its actual ordered F103 feature.");
            feature.ValidateFrame(currentSelection.Camera, lighting);
            ValidateTemplate(explicitTemplate);
            var constants = DeferredSelected383Constants.Create(currentSelection, lighting, additional);
            return explicitTemplate with
            {
                Constants = new ReadOnlyDictionary<uint, ReadOnlyMemory<byte>>(
                    constants.Constants.ToDictionary(p => p.Key, p => (ReadOnlyMemory<byte>)p.Value.ToArray())),
                Inputs = Array.AsReadOnly(explicitTemplate.Inputs.ToArray()),
                TemporaryTranslucencyViewport = additional.TranslucencyViewport
            };
        }
    }

    /// <summary>Borrow the installed season's actual outer80 constructor
    /// proof. The compiled program retains its import proof; that earlier
    /// construction does not establish the current season's null1008 field.</summary>
    public IReadOnlyList<DeferredSourceNull2D> CaptureCurrentSourceNullProofs(
        DeferredF103SelectedSource currentSelection)
    {
        ArgumentNullException.ThrowIfNull(currentSelection);
        lock(lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(retired,this);
            var actualSource=Source;
            var nulls=currentNullSources.GetValue(actualSource,static source=>
                DeferredSourceNull2D.FromFreshDirectionalConstruction(source.Construction,
                    DeferredImageStateScope.FreshSourceConstructionNoAssignments));
            if(Program.SelectedSourceNullSpecialization is not{} adapter||
                !adapter.ProofPairs.Select(pair=>pair.PixelRegister).SequenceEqual(nulls.Select(pair=>pair.PixelRegister)))
                throw new InvalidDataException("Seasonal deferred source differs from its compiled actual null-role interface.");
            foreach(var pair in nulls)pair.ValidateForSelection(currentSelection);
            return nulls;
        }
    }

    /// <summary>Retain the source nested-loop order when handing packets to
    /// NativeMapScene.RenderOrderedAsync. No pass is drawn during preparation.</summary>
    public async Task<IReadOnlyList<NativeDeferredPacket>> PrepareOrderedDeferredAsync(
        NativeMapFrameContext frame, MapSourceCreatedWorldF103Frames world,
        Func<MapSourceCreatedWorldF103Pass, Task<(NativeDeferredPacket Template,
            DeferredSelected383AdditionalInputs Additional)>> prepareInputs)
    {
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(prepareInputs);
        var passes = world.Passes.ToArray();
        if (passes.Length == 0 || world.View.Camera.FrameNumber != frame.FrameNumber ||
            !passes.Select(p => p.Association).SequenceEqual(world.Plan.Associations) ||
            passes.Any(p => !ReferenceEquals(p.Frame.ViewFields, world.View) || p.Frame.FrameNumber != frame.FrameNumber))
            throw new InvalidDataException("Ordered deferred input must preserve the actual counted World View, frame and nested-loop associations.");
        var common = passes[0].Frame.Common;
        var actualPlan = DeferredF103WorldPassAssociations.Prepare(world.Plan.Source, world.Plan.Queue,
            world.View, world.Plan.CurrentView.ConstructorFlags594Retained,
            common.CommonViewResource240Present, common.CurrentViewAux3F2);
        if (!actualPlan.Associations.SequenceEqual(world.Plan.Associations))
            throw new InvalidDataException("Ordered deferred input differs from the actual source World association plan.");
        var packets = new List<NativeDeferredPacket>(passes.Length);
        foreach (var pass in passes)
        {
            var inputs = await prepareInputs(pass);
            packets.Add(await PrepareDeferredAsync(frame, inputs.Template, pass.Frame.Lighting,
                pass.Frame.Selection, inputs.Additional));
        }
        return Array.AsReadOnly(packets.ToArray());
    }

    private void ValidateTemplate(NativeDeferredPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet.Constants); ArgumentNullException.ThrowIfNull(packet.Inputs);
        ArgumentNullException.ThrowIfNull(packet.Raster);
        var declarations = Program.DescriptorBindings.Bindings;
        var buffers = declarations.Where(d => d.DescriptorKind == "UniformBuffer").ToArray();
        if (buffers.Length != 1 || buffers[0].Source.BindPoint != 0 || buffers[0].ConstantBuffer!.ByteSize != 736 ||
            packet.Constants.Count != 1 || !packet.Constants.TryGetValue(0, out var bytes) || bytes.Length != 736)
            throw new InvalidDataException("PS383 requires its one complete736-byte original constant buffer.");
        var keys = declarations.Where(d => d.DescriptorKind != "UniformBuffer")
            .Select(d => (d.DescriptorKind, d.Source.BindPoint)).ToHashSet();
        if (packet.Inputs.Count != keys.Count || packet.Inputs.Any(i => i is null) ||
            packet.Inputs.Select(i => (i.DescriptorKind, i.SourceBindPoint)).Distinct().Count() != keys.Count ||
            packet.Inputs.Any(i => !keys.Contains((i.DescriptorKind, i.SourceBindPoint)) || !i.Resource.IsValid ||
                string.IsNullOrWhiteSpace(i.SourceResourceUri) || (i.DescriptorKind == "Texture") != i.OwnerTexture.HasValue ||
                i.OwnerTexture is { } parent && !parent.IsValid))
            throw new InvalidDataException("PS383 requires every effective source image, buffer and sampler with its live owner.");
    }

    public void Dispose() { lock (lifetimeGate) retired = true; }
}

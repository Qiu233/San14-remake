using Godot;
using San14.Data;

public sealed record ReflectionCaptureFilterSamplers(Rid Octmap, Rid Radiance);
public sealed record ReflectionCaptureFilterShadow(Rid Texture, Rid Storage, Rid Sampler,
    ReflectionCaptureFilterLighting Lighting, string SourceIdentity);

/// <summary>Shader-global retained words shared by this actual RefProbe node's
/// wrappers. Source BD76D0 indices are not per-field progress or ready flags.</summary>
public sealed class ReflectionCaptureFilterSharedShaderState(LoadedReflectionProducerPlan source)
{
    internal readonly SemaphoreSlim Gate = new(1, 1);
    public LoadedReflectionProducerPlan Source { get; } = source ?? throw new ArgumentNullException(nameof(source));
    public int CurrentInputIndex { get; private set; }
    public ReflectionCaptureFilterTuning Tuning { get; private set; } = ReflectionCaptureFilterTuning.FreshShader;
    public void ApplyCurrentScreenTuning(ReflectionComputeTuning current) =>
        Tuning = new(current.IndirectWeight, current.SpecularWeight, current.TraceBias);
    internal void RecordSubmittedInput(int index) => CurrentInputIndex = index;
}

/// <summary>Complete FD7E00/FD8D50 filter work: original raster6/7/11,
/// compute2/8/9 and persistent wrapper progress. Inputs remain real captures,
/// the actual field shadow/light preparation and loaded source samplers.</summary>
public sealed class ReflectionCaptureFilterOwner : IAsyncDisposable
{
    private sealed record Program(ReflectionShaderStage Stage, ReflectionCaptureFilterShaderInterface Interface,
        Rid Shader, Rid Pipeline, Rid Constants, bool Compute);
    public ReflectionCaptureFilterResources Resources { get; }
    public ReflectionCaptureFilterProgress Progress { get; } = new();
    public ReflectionCaptureFilterSharedShaderState ShaderState { get; }
    private readonly IReadOnlyDictionary<uint, Program> programs;
    private readonly IReadOnlyList<Rid> owned;
    private Task? disposal;

    private ReflectionCaptureFilterOwner(ReflectionCaptureFilterResources resources,
        ReflectionCaptureFilterSharedShaderState shared, Dictionary<uint, Program> programs, List<Rid> owned)
    {
        (Resources, ShaderState, this.programs, this.owned) = (resources, shared, programs, owned.AsReadOnly());
        // EC9960's successful shader/storage preparation writes mode1. The
        // underlying fresh wrapper initialization FD8340 starts at mode0.
        Progress.SetMode(1);
    }

    public static async Task<ReflectionCaptureFilterOwner> BuildAsync(ReflectionCaptureFieldResources field,
        ReflectionCaptureFilterSharedShaderState shared, Vkd3dShaderCompiler compiler,
        ReflectionCaptureFilterConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(field); ArgumentNullException.ThrowIfNull(shared); ArgumentNullException.ThrowIfNull(compiler);
        field.RequireCurrent(); var source = shared.Source;
        var translated = new Dictionary<uint, (ReflectionShaderStage Stage, ReflectionCaptureFilterShaderInterface Interface, byte[] Bytes)>();
        foreach (uint pass in new uint[] { 2, 6, 7, 8, 9, 11 })
        {
            var stage = source.Passes[(int)pass].Stages.Single(value => value.StageCode != 1);
            int expected = pass switch { 2 => 9, 6 => 3, 7 => 4, 8 => 12, 9 => 13, _ => 5 };
            if (stage.ProgramIndex != expected) throw new InvalidDataException("reflection.filter.source_program");
            var spirv = compiler.Compile(source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes, stage.ProgramIndex),
                bufferUavsAsStorageBuffers: true);
            if (spirv.SourceProgramSha256 != stage.ProgramSha256) throw new InvalidDataException("reflection.filter.source_identity");
            var deviceBytes = ReflectionCaptureFilterShader.PrepareDeviceBytes(stage, spirv.Bytes);
            translated.Add(pass, (stage, ReflectionCaptureFilterShader.Read(stage, deviceBytes), deviceBytes));
        }
        var vs = source.Passes[6].Stages.Single(value => value.StageCode == 1);
        if (vs.ProgramIndex != 1) throw new InvalidDataException("reflection.filter.vertex_program");
        var vertex = compiler.Compile(source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes, vs.ProgramIndex), 1);
        var adapter = VertexShaderAdapter.Adapt(vertex.Bytes, 0, 0, 0);
        var vertexBindings = TranslatedShaderBindingReader.Read(vs.Definitions, adapter.Bytes);
        if (vertexBindings.Bindings.Count != 0 || vertexBindings.Interface.StageVariables.Any(value => value.StorageClass == 1 && value.Location is not null))
            throw new InvalidDataException("reflection.filter.fullscreen_vertex");
        var resources = await ReflectionCaptureFilterResources.BuildAsync(field, configuration);
        try
        {
            return await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                resources.RequireCurrent(); var owned = new List<Rid>(); var programs = new Dictionary<uint, Program>();
                Rid Own(Rid value) { if (!value.IsValid) throw new InvalidDataException("reflection.filter.pipeline_resource"); owned.Add(value); return value; }
                try
                {
                    foreach (var item in translated)
                    {
                        bool compute = item.Value.Stage.StageCode == 5;
                        using var stages = new RDShaderSpirV();
                        if (compute) stages.SetStageBytecode(RenderingDevice.ShaderStage.Compute, item.Value.Bytes);
                        else { stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapter.Bytes); stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, item.Value.Bytes); }
                        Rid shader = Own(rd.ShaderCreateFromSpirV(stages, "OriginalReflectionFilterPass" + item.Key)); Rid pipeline;
                        if (compute) pipeline = Own(rd.ComputePipelineCreate(shader));
                        else
                        {
                            using var raster = new RDPipelineRasterizationState { CullMode = RenderingDevice.PolygonCullMode.Disabled };
                            using var multi = new RDPipelineMultisampleState();
                            using var depth = new RDPipelineDepthStencilState { EnableDepthTest = false, EnableDepthWrite = false };
                            using var color = new RDPipelineColorBlendStateAttachment { EnableBlend = false, WriteR = true, WriteG = true, WriteB = true, WriteA = false };
                            using var blend = new RDPipelineColorBlendState { Attachments = [color] };
                            pipeline = Own(rd.RenderPipelineCreate(shader, rd.FramebufferGetFormat(resources.ScratchSurfaces[0].Framebuffer), -1,
                                RenderingDevice.RenderPrimitive.Triangles, raster, multi, depth, blend));
                        }
                        if (compute ? !rd.ComputePipelineIsValid(pipeline) : !rd.RenderPipelineIsValid(pipeline))
                            throw new InvalidDataException("reflection.filter.pipeline");
                        int size = item.Value.Stage.Definitions.Buffers.Single(value => value.Name.Text == "$Globals").ByteSize;
                        Rid constants = Own(rd.UniformBufferCreate((uint)size));
                        programs.Add(item.Key, new(item.Value.Stage, item.Value.Interface, shader, pipeline, constants, compute));
                    }
                    return new ReflectionCaptureFilterOwner(resources, shared, programs, owned);
                }
                catch { for (int index = owned.Count - 1; index >= 0; index--) rd.FreeRid(owned[index]); throw; }
            });
        }
        catch { await resources.DisposeAsync(); throw; }
    }

    public async Task SetSchedulingModeAsync(uint mode)
    {
        await ShaderState.Gate.WaitAsync();
        try { RequireCurrent(); Progress.SetMode(mode); }
        finally { ShaderState.Gate.Release(); }
    }

    public async Task AdvanceDirectAsync(ReflectionCaptureFilterShadow actualShadow, ReflectionCaptureFilterSamplers actualSamplers)
    {
        ArgumentNullException.ThrowIfNull(actualShadow); ArgumentNullException.ThrowIfNull(actualSamplers);
        await ShaderState.Gate.WaitAsync();
        try
        {
            RequireCurrent();
            await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                RequireCurrent();
                do
                {
                    var steps = Progress.PrepareDirect(Resources.Field.Plan.AtlasLayers);
                    if (steps.Count == 0) break;
                    foreach (var step in steps) Submit(rd, step, actualShadow, actualSamplers);
                    Progress.CommitDirect(Resources.Field.Plan.AtlasLayers);
                } while (Progress.Mode == 0 && Progress.Phase == ReflectionCaptureFilterPhase.DirectLighting);
                return true;
            });
        }
        finally { ShaderState.Gate.Release(); }
    }

    public async Task AdvanceFilteringAsync(ReflectionCaptureFilterSamplers actualSamplers)
    {
        ArgumentNullException.ThrowIfNull(actualSamplers); await ShaderState.Gate.WaitAsync();
        try
        {
            RequireCurrent();
            await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                RequireCurrent();
                var initial = Progress.Phase;
                do
                {
                    if (Progress.Mode is not (0 or 1)) break;
                    if (Progress.Phase == ReflectionCaptureFilterPhase.Propagation)
                    {
                        foreach (var step in Progress.PreparePropagation(Resources.Field.Plan.AtlasLayers, Resources.Configuration))
                            Submit(rd, step, null, actualSamplers);
                        Progress.CommitPropagation(Resources.Field.Plan.AtlasLayers, Resources.Configuration);
                    }
                    else if (Progress.Phase == ReflectionCaptureFilterPhase.Publication)
                    {
                        if (Progress.Mode == 1 && initial != ReflectionCaptureFilterPhase.Publication) break;
                        foreach (var step in Progress.PreparePublication(Resources.Field.Plan.AtlasLayers, Resources.Field.Plan.RadianceMipLevels))
                            Submit(rd, step, null, actualSamplers);
                        Progress.CommitPublication();
                    }
                    else break;
                } while (Progress.Mode == 0 && Progress.Phase is ReflectionCaptureFilterPhase.Propagation or ReflectionCaptureFilterPhase.Publication);
                return true;
            });
        }
        finally { ShaderState.Gate.Release(); }
    }

    private void Submit(RenderingDevice rd, ReflectionCaptureFilterStep step,
        ReflectionCaptureFilterShadow? shadow, ReflectionCaptureFilterSamplers samplers)
    {
        var field = Resources.Field; var program = programs[step.Pass]; var plan = field.Plan;
        ReflectionFilterSurface? target = step.Kind switch {
            ReflectionCaptureFilterStepKind.DirectRaster => Resources.ScratchSurfaces[(int)step.Layer],
            ReflectionCaptureFilterStepKind.PropagationRaster => Resources.PropagationSurface,
            ReflectionCaptureFilterStepKind.RadianceMip => Resources.RequireMipTarget(step.Layer, step.Mip), _ => null };
        uint inputWidth = step.Kind == ReflectionCaptureFilterStepKind.IntegratePropagation ? Resources.PropagationExtent : plan.AtlasWidth;
        var bytes = ReflectionCaptureFilterParameters.Encode(program.Stage, plan, field.CurrentGeometry(), step,
            target?.Width ?? 0, target?.Height ?? 0, inputWidth, ShaderState.CurrentInputIndex, ShaderState.Tuning, shadow?.Lighting);
        var bindings = new Dictionary<(string, uint), Rid>();
        void Bind(string kind, uint index, Rid rid) { if (!rid.IsValid) throw new InvalidDataException("reflection.filter.actual_binding"); bindings.Add((kind, index), rid); }
        if (step.Pass is 6 or 7)
        {
            Bind("Texture", 2, field.CaptureAtlas);
            if (step.Pass == 6)
            {
                if (shadow is null || shadow.Storage != field.ProbeShadowMap || string.IsNullOrWhiteSpace(shadow.SourceIdentity) ||
                    !rd.TextureIsValid(shadow.Texture) || !rd.TextureIsValid(shadow.Storage)) throw new InvalidDataException("reflection.filter.actual_capture_shadow");
                Bind("Texture", 0, shadow.Texture); Bind("Sampler", 0, shadow.Sampler);
            }
            else
            {
                Bind("Sampler", 2, samplers.Octmap);
                Bind("StorageBuffer", 2, Progress.PropagationIteration == 0 ? Resources.WorkingCoefficients : Resources.PropagatedCoefficients);
            }
        }
        else if (step.Pass == 2)
        {
            bool propagation = step.Kind == ReflectionCaptureFilterStepKind.IntegratePropagation;
            Bind("Texture", 10, propagation ? Resources.PropagationRaster : Resources.ScratchRadiance);
            Bind("StorageBuffer", 2, propagation ? Resources.PropagatedCoefficients : Resources.WorkingCoefficients);
        }
        else if (step.Pass == 8)
        {
            bool final = step.Kind == ReflectionCaptureFilterStepKind.PublishCoefficients;
            Bind("StorageBuffer", 2, final ? field.SphericalHarmonics : Resources.WorkingCoefficients);
            Bind("StorageBuffer", 3, final ? Resources.WorkingCoefficients : Resources.PropagatedCoefficients);
        }
        else if (step.Pass == 9)
        {
            Bind("Texture", 9, field.CaptureAtlas); Bind("Sampler", 9, samplers.Octmap);
            Bind("Texture", 10, Resources.ScratchRadiance); Bind("Image", 1, field.Radiance); Bind("StorageBuffer", 2, Resources.WorkingCoefficients);
        }
        else if (step.Pass == 11)
        { Bind("Texture", 3, Resources.BorrowPreviousMip(rd, step.Mip - 1)); Bind("Sampler", 3, samplers.Radiance); }
        if (rd.BufferUpdate(program.Constants, 0, (uint)bytes.Length, bytes) != Error.Ok) throw new InvalidDataException("reflection.filter.constant_update");
        var uniforms = new Godot.Collections.Array<RDUniform>(); Rid set = default;
        try
        {
            foreach (var binding in program.Interface.Bindings)
            {
                Rid resource = binding.DescriptorKind == "UniformBuffer" ? program.Constants : bindings[(binding.DescriptorKind, binding.Source.BindPoint)];
                var uniform = new RDUniform { Binding = checked((int)binding.Device.Binding!.Value), UniformType = binding.DescriptorKind switch {
                    "UniformBuffer" => RenderingDevice.UniformType.UniformBuffer, "StorageBuffer" => RenderingDevice.UniformType.StorageBuffer,
                    "Texture" => RenderingDevice.UniformType.Texture, "Sampler" => RenderingDevice.UniformType.Sampler,
                    "Image" => RenderingDevice.UniformType.Image, _ => throw new InvalidDataException("reflection.filter.descriptor") } };
                uniform.AddId(resource); uniforms.Add(uniform);
            }
            set = rd.UniformSetCreate(uniforms, program.Shader, 0);
            if (!set.IsValid || !rd.UniformSetIsValid(set)) throw new InvalidDataException("reflection.filter.uniform_set");
            if (program.Compute)
            {
                uint columns = plan.AtlasWidth / field.Source.TileSize, rows = plan.AtlasHeight / field.Source.TileSize;
                uint x = step.Pass == 8 ? checked(columns * rows * plan.AtlasLayers) : step.Pass == 9 ? (plan.AtlasWidth + 7) / 8 : columns;
                uint y = step.Pass == 8 ? 1 : step.Pass == 9 ? (plan.AtlasHeight + 7) / 8 : rows;
                uint z = step.Pass == 9 ? plan.AtlasLayers : 1;
                long list = rd.ComputeListBegin();
                try { rd.ComputeListBindComputePipeline(list, program.Pipeline); rd.ComputeListBindUniformSet(list, set, 0); rd.ComputeListDispatch(list, x, y, z); }
                finally { rd.ComputeListEnd(); }
            }
            else
            {
                if (target is null || !rd.FramebufferIsValid(target.Framebuffer)) throw new InvalidDataException("reflection.filter.current_target");
                long list = rd.DrawListBegin(target.Framebuffer, RenderingDevice.DrawFlags.ClearColorAll, [new Color(0, 0, 0, 0)]);
                try { rd.DrawListBindRenderPipeline(list, program.Pipeline); rd.DrawListBindUniformSet(list, set, 0); rd.DrawListDraw(list, false, 1, 3); }
                finally { rd.DrawListEnd(); }
            }
            if (step.Pass is 6 or 7 or 11) ShaderState.RecordSubmittedInput((int)step.Layer);
            if (step.Pass == 2) ShaderState.RecordSubmittedInput(step.Kind == ReflectionCaptureFilterStepKind.IntegratePropagation ? 0 : (int)step.Layer);
            if (step.Pass == 9) Resources.MarkRadianceWrite(0);
            if (step.Pass == 11) Resources.MarkRadianceWrite(step.Mip);
        }
        finally { foreach (var uniform in uniforms) uniform.Dispose(); if (set.IsValid) rd.FreeRid(set); }
    }

    private void RequireCurrent() { ObjectDisposedException.ThrowIf(disposal is not null, this); Resources.RequireCurrent(); }
    public ValueTask DisposeAsync() => new(disposal ??= RetireAsync());
    private async Task RetireAsync()
    {
        await ShaderState.Gate.WaitAsync();
        try
        {
            await TerrainDeviceTexture.OnRenderThreadAsync(rd => { for (int index = owned.Count - 1; index >= 0; index--) rd.FreeRid(owned[index]); return true; });
            await Resources.DisposeAsync();
        }
        finally { ShaderState.Gate.Release(); }
    }
}

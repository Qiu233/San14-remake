using Godot;
using San14.Data;

/// <summary>One actual rendered capture attachment and its actual sampler.
/// Source order is capture color2, color3, color1; each image is one Cube,
/// not the ordinary Main screen GBuffer or an invented six-view result.</summary>
public sealed record ReflectionCapturedCube(Rid Texture, Rid Storage, Rid Sampler, string SourceIdentity);

/// <summary>FD7160 -> FD9A10 -> FD7FA0 content submission. The caller first
/// renders the actual capture View into its real cube attachments. This owner
/// converts those inputs with the original programs and writes sky scalars;
/// it does not replace the capture Scene renderer or the later SH/filter work.
/// Retire this borrower before its Field and capture-attachment owners.</summary>
public sealed class ReflectionCaptureContentOwner : IAsyncDisposable
{
    private sealed record Program(ReflectionShaderStage Source, ReflectionComputeBindings Bindings,
        Rid Shader, Rid Pipeline, Rid Constants);
    private readonly ReflectionCaptureFieldResources field;
    private readonly IReadOnlyDictionary<uint, Program> programs;
    private readonly IReadOnlyList<Rid> owned;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Task? disposal;

    private ReflectionCaptureContentOwner(ReflectionCaptureFieldResources field,
        Dictionary<uint, Program> programs, List<Rid> owned)
        => (this.field, this.programs, this.owned) = (field, programs, owned.AsReadOnly());

    public static Task<ReflectionCaptureContentOwner> BuildAsync(LoadedReflectionProducerPlan source,
        Vkd3dShaderCompiler compiler, ReflectionCaptureFieldResources actualField)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(actualField); actualField.RequireCurrent();
        var stages = new Dictionary<uint, (ReflectionShaderStage Source, ReflectionComputeBindings Bindings, byte[] Bytes)>();
        foreach (uint pass in new uint[] { 0, 1, 10 })
        {
            var stage = source.Passes[(int)pass].Stages.Single();
            if (stage.ProgramIndex != (pass == 0 ? 7 : pass == 1 ? 8 : 14))
                throw new InvalidDataException("reflection.capture.source_program");
            var translated = compiler.Compile(source.Shader.Container.GetProgramBytes(source.Shader.Source.Bytes, stage.ProgramIndex),
                bufferUavsAsStorageBuffers: pass == 10);
            if (translated.SourceProgramSha256 != stage.ProgramSha256) throw new InvalidDataException("reflection.capture.source_identity");
            var bindings = ReflectionCaptureContentShader.ReadBindings(stage, translated.Bytes);
            byte[] bytes = translated.Bytes;
            stages.Add(pass, (stage, bindings, bytes));
        }
        return TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            actualField.RequireCurrent(); var owned = new List<Rid>(); var programs = new Dictionary<uint, Program>();
            Rid Own(Rid value) { if (!value.IsValid) throw new InvalidDataException("reflection.capture.device_resource"); owned.Add(value); return value; }
            try
            {
                using var atlas = rd.TextureGetFormat(actualField.CaptureAtlas);
                if (!atlas.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.StorageBit))
                    throw new InvalidDataException("reflection.capture.octmap_storage_usage");
                foreach (var item in stages)
                {
                    using var spirv = new RDShaderSpirV(); spirv.SetStageBytecode(RenderingDevice.ShaderStage.Compute, item.Value.Bytes);
                    Rid shader = Own(rd.ShaderCreateFromSpirV(spirv, "OriginalReflectionCapturePass" + item.Key));
                    Rid pipeline = Own(rd.ComputePipelineCreate(shader));
                    if (!rd.ComputePipelineIsValid(pipeline)) throw new InvalidDataException("reflection.capture.compute_pipeline");
                    int size = item.Value.Source.Definitions.Buffers.Single(value => value.Name.Text == "$Globals").ByteSize;
                    Rid constants = Own(rd.UniformBufferCreate((uint)size));
                    programs.Add(item.Key, new(item.Value.Source, item.Value.Bindings, shader, pipeline, constants));
                }
                return new ReflectionCaptureContentOwner(actualField, programs, owned);
            }
            catch { for (int index = owned.Count - 1; index >= 0; index--) rd.FreeRid(owned[index]); throw; }
        });
    }

    /// <summary>Submit actual capture conversions in source0/1/0 order. Null
    /// attachment slots retain the source skip. Cursor advancement follows the
    /// successful submissions; reaching -1 submits sky for every radiance layer.
    /// No capture/SH/filter/GPU-completion flag is manufactured.</summary>
    public async Task ConvertCapturedCellAsync(int actualSourceOrdinal,
        IReadOnlyList<ReflectionCapturedCube?> actualCaptureColor2Color3Color1)
    {
        ArgumentNullException.ThrowIfNull(actualCaptureColor2Color3Color1);
        var cubes = actualCaptureColor2Color3Color1.ToArray();
        if (cubes.Length != 3 || actualSourceOrdinal < 0) throw new InvalidDataException("reflection.capture.actual_inputs");
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposal is not null, this); field.RequireCurrent();
            await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                ObjectDisposedException.ThrowIf(disposal is not null, this); field.RequireCurrent();
                foreach (var cube in cubes.Where(value => value is not null)) ValidateCube(rd, cube!);
                for (uint channel = 0; channel < 3; channel++)
                {
                    if (cubes[channel] is not { } cube) continue;
                    uint pass = channel == 1 ? 1u : 0u; var program = programs[pass];
                    using var input = rd.TextureGetFormat(cube.Texture);
                    var constants = ReflectionCaptureContentParameters.Capture(program.Source, field.Plan, actualSourceOrdinal, channel, input.Width);
                    uint tile = field.Source.TileSize;
                    Submit(rd, program, constants, cube, (tile + 7) / 8, (tile + 7) / 8);
                }
                // Real FD9A10 write, after the conversion work has been
                // submitted. It is distinct from a GPU completion receipt.
                field.Instance.AdvanceAfterCaptureSubmission(actualSourceOrdinal);
                if (field.Instance.WrapperNextIndex < 0)
                {
                    var program = programs[10];
                    for (uint layer = 0; layer < field.Plan.AtlasLayers; layer++)
                    {
                        var constants = ReflectionCaptureContentParameters.Sky(program.Source, field.Plan, layer);
                        Submit(rd, program, constants, null, field.Plan.AtlasWidth / field.Source.TileSize,
                            field.Plan.AtlasHeight / field.Source.TileSize);
                    }
                }
                return true;
            });
        }
        finally { gate.Release(); }
    }

    private void Submit(RenderingDevice rd, Program program, byte[] constants, ReflectionCapturedCube? cube, uint x, uint y)
    {
        if (!rd.ComputePipelineIsValid(program.Pipeline) || x == 0 || y == 0 ||
            !rd.TextureIsValid(field.CaptureAtlas)) throw new InvalidDataException("reflection.capture.current_resources");
        if (rd.BufferUpdate(program.Constants, 0, (uint)constants.Length, constants) != Error.Ok)
            throw new InvalidDataException("reflection.capture.constant_update");
        var uniforms = new Godot.Collections.Array<RDUniform>(); Rid set = default;
        try
        {
            foreach (var binding in program.Bindings.Bindings)
            {
                Rid resource = binding.DescriptorKind switch
                {
                    "UniformBuffer" => program.Constants,
                    "Sampler" => cube?.Sampler ?? throw new InvalidDataException("reflection.capture.sampler"),
                    "Texture" when binding.Source.BindPoint == 8 => cube?.Texture ?? throw new InvalidDataException("reflection.capture.cube"),
                    "Texture" when binding.Source.BindPoint == 9 => field.CaptureAtlas,
                    "Image" when binding.Source.BindPoint == 0 => field.CaptureAtlas,
                    "ImageBuffer" when binding.Source.BindPoint == 4 => field.SkyVisibility,
                    _ => throw new InvalidDataException("reflection.capture.binding")
                };
                var uniform = new RDUniform { Binding = checked((int)binding.Device.Binding!.Value), UniformType = binding.DescriptorKind switch
                {
                    "UniformBuffer" => RenderingDevice.UniformType.UniformBuffer, "Sampler" => RenderingDevice.UniformType.Sampler,
                    "Texture" => RenderingDevice.UniformType.Texture, "Image" => RenderingDevice.UniformType.Image,
                    "ImageBuffer" => RenderingDevice.UniformType.StorageBuffer, _ => throw new InvalidDataException("reflection.capture.descriptor")
                } };
                uniform.AddId(resource); uniforms.Add(uniform);
            }
            set = rd.UniformSetCreate(uniforms, program.Shader, 0);
            if (!set.IsValid || !rd.UniformSetIsValid(set)) throw new InvalidDataException("reflection.capture.uniform_set");
            long list = rd.ComputeListBegin();
            try { rd.ComputeListBindComputePipeline(list, program.Pipeline); rd.ComputeListBindUniformSet(list, set, 0); rd.ComputeListDispatch(list, x, y, 1); }
            finally { rd.ComputeListEnd(); }
        }
        finally { foreach (var uniform in uniforms) uniform.Dispose(); if (set.IsValid) rd.FreeRid(set); }
    }

    private static void ValidateCube(RenderingDevice rd, ReflectionCapturedCube cube)
    {
        if (!cube.Texture.IsValid || !cube.Storage.IsValid || !cube.Sampler.IsValid || string.IsNullOrWhiteSpace(cube.SourceIdentity) ||
            !rd.TextureIsValid(cube.Texture) || !rd.TextureIsValid(cube.Storage)) throw new InvalidDataException("reflection.capture.borrowed_cube");
        using var format = rd.TextureGetFormat(cube.Texture);
        if (format.TextureType != RenderingDevice.TextureType.Cube || format.ArrayLayers != 6 || format.Width == 0 ||
            format.Width != format.Height || format.Samples != RenderingDevice.TextureSamples.Samples1 ||
            format.Format != RenderingDevice.DataFormat.R16G16B16A16Sfloat || !format.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
            throw new InvalidDataException("reflection.capture.actual_cube_shape");
    }

    public ValueTask DisposeAsync() => new(disposal ??= RetireAsync());
    private async Task RetireAsync()
    {
        await gate.WaitAsync();
        try { await TerrainDeviceTexture.OnRenderThreadAsync(rd => { for (int index = owned.Count - 1; index >= 0; index--) rd.FreeRid(owned[index]); return true; }); }
        finally { gate.Release(); }
    }
}

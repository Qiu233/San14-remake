using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Generates the GGX ambient lookup with the project's HLSL integral,
/// retaining the map's 256 samples, two-channel format and consumer sampler.</summary>
public sealed class GgxAmbientLut : IAsyncDisposable
{
    public Rid TextureRid { get; }
    public Rid SamplerRid { get; }
    public byte[] SourceSamplerKey { get; } = Convert.FromHexString("1514300000000000");
    public byte[] SourceSamplerDescriptor { get; }
    public ShaderSpirv Translation { get; }
    public DxbcResourceDefinitions PixelDefinitions { get; }
    public uint DeviceConstantBytes { get; }
    public byte[] ConstantBytes { get; }
    public CompiledGgxShader? NativeSource { get; private set; }
    private readonly RenderDeviceResources resources;
    private volatile bool retiring;

    private GgxAmbientLut(Rid texture, Rid sampler, ShaderSpirv translation,
        DxbcResourceDefinitions definitions, uint deviceBytes, byte[] constants, byte[] samplerDescriptor, RenderDeviceResources resources)
        => (TextureRid, SamplerRid, Translation, PixelDefinitions, DeviceConstantBytes, ConstantBytes, SourceSamplerDescriptor, this.resources) =
            (texture, sampler, translation, definitions, deviceBytes, constants, samplerDescriptor, resources);

    public static async Task<GgxAmbientLut> BuildAsync(CompiledGgxShader source,
        Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = await BuildAsync(source.Dxbc, compiler);
        result.NativeSource = source;
        return result;
    }

    public static async Task<GgxAmbientLut> BuildAsync(ReadOnlyMemory<byte> originalPixel,
        Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(compiler);
        var translated = compiler.Compile(originalPixel.Span);
        var definitions = DxbcResourceDefinitionReader.Read(originalPixel.Span);
        var module = SpirvShaderInterfaceReader.Read(translated.Bytes);
        // The HLSL declaration retains two unused thread variables in a 32B
        // buffer, while the program consumes only the first 16B register.
        if (definitions.Buffers.Count != 1 || definitions.Buffers[0].ByteSize != 32 ||
            definitions.Bindings.Count != 1 || definitions.Bindings[0].InputType != 0 ||
            definitions.Bindings[0].BindPoint != 0 || module.Descriptors.Count != 1)
            throw new InvalidDataException("Ggx generator requires one 32B constant buffer.");
        var descriptor = module.Descriptors[0];
        if (module.EntryPoints[0].ExecutionModel != 4 || descriptor.DescriptorSet != 0 ||
            descriptor.Binding != 0 || descriptor.Name != "cb0_0" || descriptor.RequiredBufferBytes != 16 ||
            descriptor.Type is not { Kind: "struct", Block: true })
            throw new InvalidDataException("Ggx generator requires the first 16B constant register.");
        byte[] constants = new byte[32];
        BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(0, 4), 256);
        BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(4, 4), 256);
        BinaryPrimitives.WriteUInt32LittleEndian(constants.AsSpan(8, 4), 256);
        BinaryPrimitives.WriteSingleLittleEndian(constants.AsSpan(12, 4), 2);
        // Native generated-texture key1514300000000000: linear, clamp U/V/W,
        // no comparison, one mip. This is the final consumer state, not the PS generator.
        byte[] samplerDescriptor=new byte[52];
        uint[] samplerWords=[0x14,3,3,3,0,1,4,0,0,0,0,0,0];
        for(int i=0;i<samplerWords.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(samplerDescriptor.AsSpan(i*4,4),samplerWords[i]);
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            var resources=new RenderDeviceResources("Ggx lookup device allocation failed.");
            Rid Own(Rid rid)=>resources.Own(rid);
            try
            {
                using var vertex = new RDShaderSource { SourceVertex = DeferredFullscreenShader.VertexSource };
                using var adapter = rd.ShaderCompileSpirVFromSource(vertex);
                string error = adapter.GetStageCompileError(RenderingDevice.ShaderStage.Vertex);
                if (error.Length > 0) throw new InvalidDataException("Ggx fullscreen adapter failed: " + error);
                using var stages = new RDShaderSpirV();
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, adapter.GetStageBytecode(RenderingDevice.ShaderStage.Vertex));
                stages.SetStageBytecode(RenderingDevice.ShaderStage.Fragment, translated.Bytes);
                Rid shader = Own(rd.ShaderCreateFromSpirV(stages, "OriginalNativeGgxLut"));
                using var format = new RDTextureFormat {
                    Format = RenderingDevice.DataFormat.R16G16Sfloat, TextureType = RenderingDevice.TextureType.Type2D,
                    Width = 256, Height = 256, Depth = 1, ArrayLayers = 1, Mipmaps = 1,
                    UsageBits = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                        RenderingDevice.TextureUsageBits.CanCopyFromBit };
                using var view = new RDTextureView();
                Rid texture = Own(rd.TextureCreate(format, view));
                Rid sampler=Own(DeviceSampler.Create(rd,samplerDescriptor));
                Rid framebuffer = Own(rd.FramebufferCreate([texture]));
                Rid buffer = Own(rd.UniformBufferCreate((uint)constants.Length, constants));
                using var uniform = new RDUniform { UniformType = RenderingDevice.UniformType.UniformBuffer, Binding = 0 };
                uniform.AddId(buffer);
                Rid set = Own(rd.UniformSetCreate([uniform], shader, 0));
                using var raster = new RDPipelineRasterizationState { CullMode = RenderingDevice.PolygonCullMode.Disabled };
                using var multisample = new RDPipelineMultisampleState();
                using var depth = new RDPipelineDepthStencilState { EnableDepthTest = false, EnableDepthWrite = false };
                using var attachment = new RDPipelineColorBlendStateAttachment {
                    EnableBlend = false, WriteR = true, WriteG = true, WriteB = true, WriteA = false };
                using var blend = new RDPipelineColorBlendState { Attachments = [attachment] };
                Rid pipeline = Own(rd.RenderPipelineCreate(shader, rd.FramebufferGetFormat(framebuffer), -1,
                    RenderingDevice.RenderPrimitive.Triangles, raster, multisample, depth, blend));
                if (!rd.UniformSetIsValid(set) || !rd.RenderPipelineIsValid(pipeline))
                    throw new InvalidDataException("Ggx generator uniform/pipeline validation failed.");
                long list = rd.DrawListBegin(framebuffer, RenderingDevice.DrawFlags.ClearColorAll, [Colors.Transparent]);
                try {
                    rd.DrawListBindRenderPipeline(list, pipeline); rd.DrawListBindUniformSet(list, set, 0);
                    rd.DrawListDraw(list, false, 1, 3);
                }
                finally { rd.DrawListEnd(); }
                return new GgxAmbientLut(texture, sampler, translated, definitions, descriptor.RequiredBufferBytes.Value,
                    constants, samplerDescriptor, resources);
            }
            catch(Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd,failure,"Ggx-lut-partial");
                throw;
            }
        });
    }

    public Task<byte[]> ReadEncodedAsync()
    {
        ObjectDisposedException.ThrowIf(retiring, this);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd => {
            ObjectDisposedException.ThrowIf(retiring,this);
            return rd.TextureGetData(TextureRid, 0);
        });
    }
    public ValueTask DisposeAsync()
    {
        retiring=true;
        return resources.DisposeAsync();
    }
}

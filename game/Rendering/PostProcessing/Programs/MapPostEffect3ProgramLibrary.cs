using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

public enum MapPostEffect3CompiledPassKind { Graphics, Compute }

/// <summary>Original counted stage plus its complete source reflection and exact
/// translated device interface. A vertex adapter changes only backend position
/// conventions. ConstantBuffer retains the native RDEF upload size, including
/// any inactive tail; Device.RequiredBufferBytes is the actual shader prefix.</summary>
public sealed record MapPostEffect3CompiledStage(MapPostEffect3Program Source,
    ShaderSpirv Translation, ShaderSpirv DeviceTranslation,
    TranslatedShaderBindings Bindings, AdaptedVertexShader? VertexAdapter)
{
    public bool RequiresZeroBaseDraw => VertexAdapter?.RemovedBaseVariableId is > 0;
    public MapPostEffect3DofSpriteStorageAdaptation? DofSpriteStorageAdaptation{get;init;}
    public MapPostEffect3DofImageStorageAdaptation? DofImageStorageAdaptation{get;init;}
    public MapPostEffect3FxaaFetchBoundsAdaptation? FxaaFetchBoundsAdaptation { get; init; }
    public bool RequiresUnsupportedDofImageExecutionOwner=>DofImageStorageAdaptation is not null;
}

/// <summary>Borrowed shader owned by the library. Pipelines, uniform sets and
/// draws must retire before the library; this does not execute or schedule a pass.</summary>
public sealed record MapPostEffect3CompiledPass(MapPostEffect3Pass Source,
    string ContainerSha256, MapPostEffect3CompiledPassKind Kind,
    IReadOnlyList<MapPostEffect3CompiledStage> Stages, Rid ShaderRid,
    Vector3I? ComputeLocalSize)
{
    /// <summary>The native automatic screen draw's verified embedded vertex
    /// stage. It has no counted3SPK stage index and never replaces a selected
    /// source VS. Stages retains only the actual container references.</summary>
    public MapPostEffect3CompiledStockScreenVertex? StockScreenVertex { get; init; }
}

/// <summary>Lazy independent 3SPK program library. Only an explicitly selected
/// native pass creates a shader. Creation drains before disposal; shared shader
/// tuples own one RID, even when multiple source pass records reference it.</summary>
public sealed partial class MapPostEffect3ProgramLibrary : IAsyncDisposable
{
    public MapPostEffect3Container Source { get; }
    public string SourceSha256 => Source.Sha256;
    private readonly Vkd3dShaderCompiler compiler;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object lifetime = new();
    private readonly Dictionary<(uint Stage, int Index), MapPostEffect3CompiledStage> stages = [];
    private readonly Dictionary<int, MapPostEffect3CompiledPass> passes = [];
    private readonly Dictionary<string, Rid> shaders = new(StringComparer.Ordinal);
    private bool retiring;
    private int accepted;
    private TaskCompletionSource? drained;
    private Task? disposal;

    private MapPostEffect3ProgramLibrary(MapPostEffect3Container source,
        Vkd3dShaderCompiler compiler, LoadedMapFinalOutputToneShaderSource? stockSource = null)
        => (Source, this.compiler, stockScreenSource) = (source, compiler, stockSource);

    public static MapPostEffect3ProgramLibrary Create(MapPostEffect3Container source,
        Vkd3dShaderCompiler borrowedCompiler)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(borrowedCompiler);
        // Detach all programs/pass records from caller arrays through the actual
        // counted reader; never infer programs from DXBC magic occurrences.
        var owned = MapPostEffect3ContainerReader.Read(source.Bytes.Span);
        if (owned.Sha256 != source.Sha256)
            throw new InvalidDataException("3SPK container differs from its declared source identity.");
        return new(owned, borrowedCompiler);
    }

    public static Task<MapPostEffect3ProgramLibrary> BuildAsync(MapPostEffect3Container source,
        Vkd3dShaderCompiler borrowedCompiler) => Task.FromResult(Create(source, borrowedCompiler));

    public Task<MapPostEffect3CompiledPass> GetCompiledPassAsync(int sourcePassIndex)
    {
        lock (lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring, this);
            if ((uint)sourcePassIndex >= Source.Passes.Count)
                throw new ArgumentOutOfRangeException(nameof(sourcePassIndex));
            accepted++;
        }
        return GetAcceptedAsync(sourcePassIndex);
    }

    private async Task<MapPostEffect3CompiledPass> GetAcceptedAsync(int index)
    {
        await gate.WaitAsync();
        try
        {
            if (passes.TryGetValue(index, out var existing)) return existing;
            var source = Source.Passes[index];
            var selected = source.ProgramReferences.Where(r => r.StageIndex is not null).ToArray();
            if (selected.Any(r => r.StageCode is not (0 or 1 or 5)))
                throw new NotSupportedException($"3SPK pass{index} selects GS/HS/DS or another unsupported stage.");
            bool compute = selected is [{ StageCode: 5 }];
            bool graphics = selected.Length == 2 && selected.Count(r => r.StageCode == 1) == 1 &&
                selected.Count(r => r.StageCode == 0) == 1;
            bool stockGraphics = selected is [{ StageCode: 0 }] && IsSourceStockScreenPass(index);
            if (stockGraphics && stockScreenSource is null)
                throw new NotSupportedException($"3SPK pass{index} requires its verified embedded stock VS source.");
            graphics |= stockGraphics;
            if (!compute && !graphics)
                throw new NotSupportedException($"3SPK pass{index} needs exactly VS+PS or an isolated CS; mixed or missing stages are unsupported.");
            var compiled = selected.OrderBy(r => r.StageCode).Select(r => CompileStage(r.StageCode, r.StageIndex!.Value)).ToArray();
            Vector3I? localSize = compute ? ReadLocalSize(compiled[0].Bindings.Interface) : null;
            var stock = stockGraphics ? CompileStockScreenVertex() : null;
            if (graphics) ValidateGraphicsBindings(stock?.Bindings ?? compiled.Single(s => s.Source.StageCode == 1).Bindings,
                compiled.Single(s => s.Source.StageCode == 0).Bindings);
            string key = string.Join(';', compiled.Select(s => $"{s.Source.StageCode}:{s.DeviceTranslation.SpirvSha256}"));
            if (stock is not null) key += ";embedded-stock-VS:" + stock.DeviceTranslation.SpirvSha256;
            if (!shaders.TryGetValue(key, out var shader))
            {
                shader = await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
                {
                    using var spv = new RDShaderSpirV();
                    foreach (var stage in compiled)
                        spv.SetStageBytecode(DeviceStage(stage.Source.StageCode), stage.DeviceTranslation.Bytes);
                    if (stock is not null) spv.SetStageBytecode(RenderingDevice.ShaderStage.Vertex, stock.DeviceTranslation.Bytes);
                    var rid = rd.ShaderCreateFromSpirV(spv, "Original3SPK/pass" + index);
                    if (!rid.IsValid) throw new InvalidDataException($"RenderingDevice rejected original 3SPK pass{index}.");
                    return rid;
                });
                shaders.Add(key, shader);
            }
            var pass = new MapPostEffect3CompiledPass(source, SourceSha256,
                compute ? MapPostEffect3CompiledPassKind.Compute : MapPostEffect3CompiledPassKind.Graphics,
                Array.AsReadOnly(compiled), shader, localSize) { StockScreenVertex = stock };
            passes.Add(index, pass); return pass;
        }
        finally
        {
            gate.Release();
            lock (lifetime) if (--accepted == 0 && retiring) drained?.TrySetResult();
        }
    }

    private MapPostEffect3CompiledStage CompileStage(uint stageCode, int index)
    {
        if (stages.TryGetValue((stageCode, index), out var cached)) return cached;
        var source = Source.GetProgram(stageCode, index);
        if (Convert.ToHexString(SHA256.HashData(source.Bytes.Span)) != source.Sha256)
            throw new InvalidDataException("3SPK counted program changed before translation.");
        var reflected = DxbcResourceDefinitionReader.Read(source.Bytes.Span);
        // Match the actual emitted interface, not merely RDEF declarations.
        // vkd3d's default structured/raw buffer representation is an R32_UINT
        // texel buffer; its byte addressing and source bitcasts stay in shader.
        // Compile defaults preserve that representation and every original ID.
        bool originalEyeHistogram=IsSourceEyeHistogramStorageProgram(source,reflected);
        bool originalDofSprites=IsSourceDofSpriteStorageProgram(source,reflected);
        bool originalDofImages=IsSourceDofImageStorageProgram(source,reflected);
        bool storageBufferUavs=reflected.Bindings.Any(b=>b.InputType is 6 or 8)||originalEyeHistogram;
        if(storageBufferUavs&&reflected.Bindings.Any(b=>b.InputType is 9 or 10 or 11||b.InputType==4&&b.Dimension==1&&!originalEyeHistogram&&!originalDofSprites&&!originalDofImages))
            throw new NotSupportedException("3SPK storage buffer profile does not implement typed/append/counter buffer UAVs.");
        // The official global SSBO option writes only typed-buffer source.x.
        // Original CS46 and shader-creation-only CS47..49 need full
        // float4→RGBA16F, so retain the default complete image operations
        // and apply each exact source-closed storage adapter.
        var translation = compiler.Compile(source.Bytes.Span,bufferUavsAsStorageBuffers:storageBufferUavs&&!originalDofSprites&&!originalDofImages);
        if (translation.SourceProgramSha256 != source.Sha256)
            throw new InvalidDataException("3SPK compiler input identity differs from the selected native program.");
        AdaptedVertexShader? adapter = null;
        byte[] vertexDeviceBytes = stageCode == 1 ? MoveVertexDescriptorSet(translation.Bytes) : translation.Bytes;
        if (stageCode == 1)
        {
            adapter = MapPostEffect3VertexAdapter.Adapt(vertexDeviceBytes);
        }
        var dofStorage=originalDofSprites?MapPostEffect3DofSpriteStorageAdapter.Adapt(source,translation):null;
        var dofImageStorage=originalDofImages?MapPostEffect3DofImageStorageAdapter.Adapt(Source,source,translation):null;
        var fxaaBounds = stageCode == 0 && index == 55
            ? MapPostEffect3FxaaFetchBoundsAdapter.Adapt(source, translation) : null;
        var device = dofStorage is not null?translation with{Bytes=dofStorage.Bytes,SpirvSha256=dofStorage.AdaptedSpirvSha256}:
            dofImageStorage is not null?translation with{Bytes=dofImageStorage.Bytes,SpirvSha256=dofImageStorage.AdaptedSpirvSha256}:
            fxaaBounds is not null ? translation with { Bytes = fxaaBounds.Bytes, SpirvSha256 = fxaaBounds.AdaptedSpirvSha256 } :
            adapter is null ? translation : translation with
            { Bytes = adapter.Bytes, SpirvSha256 = adapter.AdaptedSha256 };
        var bindings = ReadBindings(reflected, device.Bytes, stageCode,translation.BufferUavsAsStorageBuffers||dofStorage is not null||dofImageStorage is not null,originalEyeHistogram,
            originalDofSprites||originalDofImages);
        var result = new MapPostEffect3CompiledStage(source, translation, device, bindings, adapter)
            {DofSpriteStorageAdaptation=dofStorage,DofImageStorageAdaptation=dofImageStorage,FxaaFetchBoundsAdaptation=fxaaBounds};
        stages.Add((stageCode, index), result); return result;
    }

    private static TranslatedShaderBindings ReadBindings(DxbcResourceDefinitions original,
        ReadOnlySpan<byte> bytes, uint stageCode,bool bufferUavsAsStorageBuffers=false,bool originalEyeHistogram=false,bool originalDofSprites=false)
    {
        var module = SpirvShaderInterfaceReader.Read(bytes); var entry = module.EntryPoints.Single();
        uint model = stageCode switch { 1 => 0u, 0 => 4u, 5 => 5u, _ => throw new NotSupportedException() };
        if (entry.ExecutionModel != model || entry.Name != "main" ||
            stageCode == 0 && !module.ExecutionModes.Any(m => m.FunctionId == entry.FunctionId && m.Mode == 7))
            throw new InvalidDataException("3SPK translated entry point differs from its selected native stage.");
        var result = new List<TranslatedShaderBinding>(); var matched = new HashSet<int>();
        foreach (var variable in module.Descriptors)
        {
            string name = variable.Name ?? throw new InvalidDataException("3SPK descriptor has no original compiler register name.");
            char registerClass; string register;
            if (name.StartsWith("cb", StringComparison.Ordinal))
            {
                int split = name.IndexOf('_', 2);
                if (split < 0 || !uint.TryParse(name[(split + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    throw new InvalidDataException("3SPK CB descriptor register representation is unsupported.");
                registerClass = 'b'; register = name[2..split];
            }
            else
            {
                registerClass = name.Length == 0 ? '?' : name[0];
                register = name.Length > 0 ? name[1..] : "";
            }
            if (!uint.TryParse(register, NumberStyles.None, CultureInfo.InvariantCulture, out uint slot))
                throw new InvalidDataException("3SPK descriptor register is not a single original slot.");
            bool RegisterMatches(uint type) => registerClass switch
                { 'b' => type == 0, 's' => type == 3, 't' => type is 2 or 5 or 7,
                  'u' => type is 4 or 6 or 8 or 9 or 10 or 11, _ => false };
            var candidates = original.Bindings.Where(b => RegisterMatches(b.InputType) && b.BindPoint == slot).ToArray();
            if (candidates.Length != 1 || candidates[0].BindCount != 1 || !matched.Add(candidates[0].Index) ||
                variable.DescriptorSet != (stageCode == 1 ? 1u : 0u) || variable.Binding is null)
                throw new InvalidDataException("3SPK descriptor does not match one original declaration and stage device set.");
            var source = candidates[0]; uint inputType = source.InputType;
            var type = variable.Type; DxbcRdefBuffer? cb = null; string kind;
            if (inputType == 0)
            {
                var cbRows = original.Buffers.Where(b => b.Name.Text == source.Name.Text).ToArray();
                if (cbRows.Length != 1 || variable.StorageClass != 2 || type is not { Kind: "struct", Block: true } ||
                    variable.RequiredBufferBytes is not uint active || active == 0 || active % 16 != 0 ||
                    active > checked((cbRows[0].ByteSize + 15) / 16 * 16) ||
                    cbRows[0].Variables.Any(v => (v.Flags & 2) != 0 && (long)v.StartOffset + v.ByteSize > active))
                    throw new InvalidDataException("3SPK uniform layout cannot hold every original active RDEF field.");
                cb = cbRows[0]; kind = "UniformBuffer";
            }
            else if (inputType == 3)
            {
                if (variable.StorageClass != 0 || type.Kind != "sampler")
                    throw new InvalidDataException("3SPK sampler translation has an incompatible descriptor.");
                kind = "Sampler";
            }
            else if(inputType==4&&source.Dimension==1&&bufferUavsAsStorageBuffers&&(originalEyeHistogram||originalDofSprites))
            {
                // Pinned Eye histogram uint UAV0 uses the official scalar
                // option. Exact DOF float4 profiles use the complete two-word
                // RGBA16F adapter; no generic typed/append/counter admission.
                bool admitted=originalEyeHistogram&&source.BindPoint==0&&source.ReturnType==4||
                    originalDofSprites&&source.BindPoint is 1 or 2&&source.ReturnType==5;
                if(stageCode!=5||!admitted||variable.StorageClass!=2||
                    type is not{Kind:"struct",BufferBlock:true,Block:false}||type.Members is not{Count:1}||
                    type.Members[0].Offset!=0||type.Members[0].Type is not{Kind:"runtime-array",ArrayStride:4,
                        ElementType:{Kind:"int",Bits:32,Signed:false}})
                    throw new InvalidDataException("Original typed histogram/sprite UAV did not adapt to its exact uint32 physical storage BufferBlock.");
                kind="StorageBuffer";
            }
            else if(inputType is 5 or 6 or 7 or 8)
            {
                bool write=inputType is 6 or 8;
                if(write&&bufferUavsAsStorageBuffers)
                {
                    if(source.Dimension!=1||source.ReturnType!=6||variable.StorageClass!=2||
                        type is not{Kind:"struct",BufferBlock:true,Block:false}||type.Members is not{Count:1}||
                        type.Members[0].Offset!=0||type.Members[0].Type is not{Kind:"runtime-array",ArrayStride:4,
                            ElementType:{Kind:"int",Bits:32,Signed:false}})
                        throw new InvalidDataException("Actual raw/structured UAV storage translation is not the explicit uint32 runtime BufferBlock.");
                    kind="StorageBuffer";
                }
                else
                {
                if(source.Dimension!=1||source.ReturnType!=6||variable.StorageClass!=0||
                    type is not {Kind:"image",Dimension:5,Arrayed:false,Multisampled:false,
                        ComponentType:{Kind:"int",Bits:32,Signed:false}}||
                    type.SampledCode!=(write?2u:1u)||!write&&type.ImageFormat!=0)
                    throw new NotSupportedException("Selected structured/raw resource does not use the verified original R32_UINT texel-buffer interface.");
                kind=write?"StorageTextureBuffer":"TextureBuffer";
                }
            }
            else if(inputType is 9 or 10 or 11)
                throw new NotSupportedException("Selected live append/consume/counter interface requires an explicit counter producer.");
            else
            {
                (uint dimension, bool arrayed) = source.Dimension switch
                    { 1 => (5u, false), 4 => (1u, false), 5 => (1u, true), 8 => (2u, false),
                      9 => (3u, false), 10 => (3u, true), _ => throw new NotSupportedException("3SPK texture dimension is unsupported.") };
                bool component = source.ReturnType switch
                    { 3 => type.ComponentType is { Kind: "int", Bits: 32, Signed: true },
                      4 => type.ComponentType is { Kind: "int", Bits: 32, Signed: false },
                      5 => type.ComponentType is { Kind: "float", Bits: 32 },
                      _ => throw new NotSupportedException("3SPK typed texture return type is unsupported.") };
                if (variable.StorageClass != 0 || type.Kind != "image" || type.Dimension != dimension ||
                    type.Arrayed != arrayed || type.Multisampled != false || !component ||
                    type.SampledCode != (inputType == 4 ? 2u : 1u) || inputType == 2 && type.ImageFormat != 0)
                    throw new InvalidDataException("3SPK typed image translation differs from its original RDEF shape.");
                kind = inputType == 4 ? dimension == 5 ? "StorageTextureBuffer" : "StorageImage" :
                    dimension == 5 ? "TextureBuffer" : "Texture";
            }
            result.Add(new(variable, source, cb, kind));
        }
        // OriginalDefinitions retain every source RDEF declaration. Declarations
        // without any translated descriptor do not need a device binding; all
        // descriptors that were actually emitted are uniquely matched above.
        return new(module, original, result.AsReadOnly());
    }

    private bool IsSourceEyeHistogramStorageProgram(MapPostEffect3Program program,DxbcResourceDefinitions reflected)
    {
        if(SourceSha256!=MapFinalOutputPostEffect3Source.PayloadSha256||program.StageCode!=5||
            program.StageIndex is not(50 or 51))return false;
        string expected=program.StageIndex==50?
            "04A6897BAD89D45E45F69CE3F8DA444975F5D693906D810059879EBEADD46D63":
            "761B0FCD3090CEB80F40B94774C72098076BD1110E996D4E96D6D4CF71093A28";
        if(program.Sha256!=expected||reflected.Bindings.Where(b=>b.InputType==4).ToArray() is not
            [{Dimension:1,ReturnType:4,BindPoint:0,BindCount:1}])
            throw new InvalidDataException("Original Eye histogram storage specialization differs from pinned source uint UAV0.");
        return true;
    }

    private bool IsSourceDofSpriteStorageProgram(MapPostEffect3Program program,DxbcResourceDefinitions reflected)
    {
        if(SourceSha256!=MapFinalOutputPostEffect3Source.PayloadSha256||program.StageCode!=5||
            program.StageIndex is not(45 or 46))return false;
        string expected=program.StageIndex==45?
            "A3C90ECCB018FCFC8A34FDFD81755CA7FB84AFC667D852F1BD60511CC440063B":
            "04CB59EB2DDD04998EB07CAC1721512B4866A4863DB48B3559AB7B3C345D9149";
        var raw=reflected.Bindings.Where(b=>b.InputType is 6 or 8).ToArray();
        var typed=reflected.Bindings.Where(b=>b.InputType==4).OrderBy(b=>b.BindPoint).ToArray();
        if(program.Sha256!=expected||raw is not[{InputType:8,Dimension:1,ReturnType:6,BindPoint:0,BindCount:1}]||
            (program.StageIndex==45?typed.Length!=0:typed is not[
                {Dimension:1,ReturnType:5,BindPoint:1,BindCount:1},{Dimension:1,ReturnType:5,BindPoint:2,BindCount:1}]))
            throw new InvalidDataException("Original DOF2 storage specialization differs from exact source raw counters and float4 sprite UAVs.");
        return true;
    }

    private static byte[] MoveVertexDescriptorSet(ReadOnlySpan<byte> source)
    {
        var before=SpirvShaderInterfaceReader.Read(source);byte[] bytes=source.ToArray();int changed=0;
        for(int at=20;at<bytes.Length;)
        {
            uint word=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));int count=(int)(word>>16);
            if(count==0||(long)at+count*4>bytes.Length)throw new InvalidDataException("3SPK vertex descriptor instruction exceeds its module.");
            if((word&65535)==71&&count==4&&BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at+8))==34)
            {
                if(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at+12))!=0)
                    throw new NotSupportedException("3SPK default vertex descriptor set differs from the verified compiler output.");
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at+12),1);changed++;
            }
            at+=count*4;
        }
        var after=SpirvShaderInterfaceReader.Read(bytes);
        if(changed!=before.Descriptors.Count||after.IdBound!=before.IdBound||
            !before.Descriptors.Select(d=>(d.VariableId,d.Binding)).SequenceEqual(after.Descriptors.Select(d=>(d.VariableId,d.Binding)))||
            after.Descriptors.Any(d=>d.DescriptorSet!=1))
            throw new InvalidDataException("3SPK vertex set adapter changed an original descriptor ID or binding.");
        return bytes;
    }

    private static Vector3I ReadLocalSize(SpirvShaderInterface module)
    {
        var sizes = module.ExecutionModes.Where(m => m.FunctionId == module.EntryPoints.Single().FunctionId && m.Mode == 17).ToArray();
        if (sizes.Length != 1 || sizes[0].Operands.Count != 3 || sizes[0].Operands.Any(n => n == 0 || n > int.MaxValue))
            throw new NotSupportedException("3SPK compute program requires one explicit positive literal LocalSize.");
        return new((int)sizes[0].Operands[0], (int)sizes[0].Operands[1], (int)sizes[0].Operands[2]);
    }

    private static void ValidateGraphicsInterface(MapPostEffect3CompiledStage vertex,
        MapPostEffect3CompiledStage pixel)
        => ValidateGraphicsBindings(vertex.Bindings, pixel.Bindings);
    private static void ValidateGraphicsBindings(TranslatedShaderBindings vertex,
        TranslatedShaderBindings pixel)
    {
        static bool Compatible(SpirvInterfaceType a, SpirvInterfaceType b) => a.Kind == b.Kind &&
            (a.Kind == "vector" ? a.ComponentCount >= b.ComponentCount && Compatible(a.ComponentType!, b.ComponentType!) :
             a.Kind is "float" or "int" && a.Bits == b.Bits && a.Signed == b.Signed);
        foreach (var input in pixel.Interface.StageVariables.Where(v => v.StorageClass == 1 && v.Location is not null))
        {
            var type = input.Type.Kind == "array" ? input.Type.ElementType! : input.Type;
            uint count = input.Type.Kind == "array" ? input.Type.Count!.Value : 1;
            for (uint i = 0; i < count; i++)
            {
                var output = vertex.Interface.StageVariables.Where(v => v.StorageClass == 3 && v.Location == input.Location + i).ToArray();
                if (output.Length != 1 || !Compatible(output[0].Type, type))
                    throw new NotSupportedException("Selected 3SPK VS/PS varying interface has no verified device pairing.");
            }
        }
    }

    private static RenderingDevice.ShaderStage DeviceStage(uint code) => code switch
        { 0 => RenderingDevice.ShaderStage.Fragment, 1 => RenderingDevice.ShaderStage.Vertex,
          5 => RenderingDevice.ShaderStage.Compute, _ => throw new NotSupportedException("3SPK stage is unsupported.") };

    public ValueTask DisposeAsync()
    {
        lock (lifetime)
        {
            if (disposal is not null) return new(disposal);
            retiring = true;
            Task pending = accepted == 0 ? Task.CompletedTask :
                (drained = new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            return new(disposal = RetireAsync(pending));
        }
    }

    private async Task RetireAsync(Task pending)
    {
        await pending; await gate.WaitAsync();
        try
        {
            await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
                { foreach (var shader in shaders.Values) rd.FreeRid(shader); return true; });
            shaders.Clear(); passes.Clear(); stages.Clear();
            stockScreenVertex = null;
        }
        finally { gate.Release(); }
    }
}

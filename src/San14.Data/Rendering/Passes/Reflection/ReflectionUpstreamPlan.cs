using System.Buffers.Binary;
using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

public enum ReflectionUpstreamImage { IndirectResult, ReflectionResult, Scratch }
public sealed record ReflectionUpstreamStep(uint PassIndex, uint SourceListIndex,
    ReflectionUpstreamImage Output, ReflectionUpstreamImage PreviousResult);
public sealed record ReflectionUpstreamProbeGate(bool ObjectPresent, bool PreparedProbePresent,
    bool IndirectShaderReady, bool ReflectionShaderReady);
public sealed record ReflectionUpstreamState(DeferredReflectionState Gate,
    bool ProviderListPresent, bool ComputeShaderPresent, uint ProviderCount38, uint ProviderCount20,
    bool IntermediateClearBindSucceeded, bool IndirectScratchClearBindSucceeded,
    bool ReflectionScratchClearBindSucceeded, bool FinalTargetBindSucceeded);
public sealed record ReflectionUpstreamSchedule(bool Active, bool ClearIntermediateResults,
    bool ClearScratchBeforeIndirect, bool ClearScratchBeforeReflection, bool DrawFinal,
    IReadOnlyList<ReflectionUpstreamStep> Steps);
public sealed record ReflectionComputeBinding(SpirvInterfaceVariable Device,
    DxbcRdefBinding Source, DxbcRdefBuffer? ConstantBuffer, string DescriptorKind);
public sealed record ReflectionComputeBindings(SpirvShaderInterface Interface,
    IReadOnlyList<ReflectionComputeBinding> Bindings, uint GroupSizeX, uint GroupSizeY, uint GroupSizeZ);

/// <summary>Source screen evaluation: native EC8633 calls FD77B0 (pass4),
/// EC865F calls FD7AE0 (pass5), then EC87B7 calls the existing final pass12.
/// Provider lists and their prepared resources remain explicit caller inputs.
/// List index determines ping-pong parity even when an entry is skipped.</summary>
public static class ReflectionUpstreamPlan
{
    public const byte IntermediatePixelFormat = 3;
    public const uint IntermediateShaderFormat = 10; // R16G16B16A16_FLOAT.
    public const uint StructuredCoefficientStride = 108;
    public static ReflectionUpstreamSchedule Schedule(ReflectionUpstreamState state,
        IReadOnlyList<ReflectionUpstreamProbeGate> explicitProbeGates)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(explicitProbeGates);
        var decision=DeferredExtraLayerPlan.Reflection(state.Gate);
        if(!decision.RequiresActiveProducer)
        {
            if(decision.RequiresTargetDescriptorFallback || decision.Steps.Any(s=>s.Action==DeferredExtraAction.ClearReflectionSlice))
                throw new InvalidOperationException("Reflection conditional fallback belongs to OriginalDeferredExtraLayerOwner.");
            return new(false,false,false,false,false,Array.Empty<ReflectionUpstreamStep>());
        }
        uint needed=state.ProviderListPresent && state.ComputeShaderPresent
            ?Math.Max(state.ProviderCount38,state.ProviderCount20):0;
        if(needed>int.MaxValue || explicitProbeGates.Count!=needed || explicitProbeGates.Any(p=>p is null))
            throw new InvalidDataException("Reflection requires the complete explicit native provider list, including skipped entries.");
        var steps=new List<ReflectionUpstreamStep>();
        if(state.ProviderListPresent && state.ComputeShaderPresent)
        {
            // Both native routines initialize parity from provider+38. Their
            // loop bounds differ: first +38, second +20; do not normalize them.
            for(uint pass=4;pass<=5;pass++)
            {
                uint count=pass==4?state.ProviderCount38:state.ProviderCount20;
                var result=pass==4?ReflectionUpstreamImage.IndirectResult:ReflectionUpstreamImage.ReflectionResult;
                for(uint i=0;i<count;i++)
                {
                    var gate=explicitProbeGates[(int)i];
                    if(!gate.ObjectPresent || !gate.PreparedProbePresent || !(pass==4?gate.IndirectShaderReady:gate.ReflectionShaderReady))continue;
                    bool outputIsResult=((state.ProviderCount38&1)!=(i&1));
                    steps.Add(new(pass,i,outputIsResult?result:ReflectionUpstreamImage.Scratch,
                        outputIsResult?ReflectionUpstreamImage.Scratch:result));
                }
            }
        }
        bool scratch=state.ProviderListPresent && state.ComputeShaderPresent && state.ProviderCount38>1;
        return new(true,state.IntermediateClearBindSucceeded,scratch && state.IndirectScratchClearBindSucceeded,
            scratch && state.ReflectionScratchClearBindSucceeded,state.FinalTargetBindSucceeded,steps.AsReadOnly());
    }

    public static ReflectionShaderStage RequireStage(LoadedReflectionProducerPlan source,uint pass)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(pass is not (4 or 5))throw new InvalidDataException("Reflection screen evaluation requires original pass4 or pass5.");
        var stage=source.Passes[(int)pass].Stages.Single();
        string hash=pass==4?"44F49D484953C6144A1845C7D1B7111F2E60CDC9E87B31335D687D629222AC3B":
            "74A304816FF31BD1D7A39AC3B31454FFDAD862032086DE5A332126AA013123FA";
        if(stage.StageCode!=5 || stage.ProgramIndex!=(pass==4?10:11) || stage.ProgramSha256!=hash)
            throw new InvalidDataException("Original Reflection upstream stage selection differs.");
        return stage;
    }

    /// <summary>Complete translated compute/UAV interface, bounded to original
    /// passes4/5. vkd3d represents the 108-byte SH structure as an R32_UINT
    /// image buffer; this preserves source DWORD addressing and float bitcasts.</summary>
    public static ReflectionComputeBindings ReadBindings(ReflectionShaderStage source,ReadOnlySpan<byte> spirv)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.StageCode!=5 || source.ProgramIndex is not (10 or 11))throw new InvalidDataException("Unexpected Reflection compute source.");
        var module=SpirvShaderInterfaceReader.Read(spirv);
        var entry=module.EntryPoints.Single();
        var mode=module.ExecutionModes.SingleOrDefault(m=>m.FunctionId==entry.FunctionId && m.Mode==17);
        if(entry.ExecutionModel!=5 || entry.Name!="main" || mode is null || !mode.Operands.SequenceEqual(new uint[]{8,8,1}))
            throw new InvalidDataException("Reflection translation requires original Compute/main and 8x8x1 threads.");
        var rows=new List<ReflectionComputeBinding>();var matched=new HashSet<int>();
        foreach(var variable in module.Descriptors)
        {
            string name=variable.Name??throw new InvalidDataException("Compute descriptor lacks its source register.");
            uint inputType,index;string kind;DxbcRdefBuffer? cb=null;
            if(name.StartsWith("cb",StringComparison.Ordinal))
            {
                int separator=name.IndexOf('_',2);
                if(separator<0 || !uint.TryParse(name.AsSpan(2,separator-2),NumberStyles.None,CultureInfo.InvariantCulture,out index))
                    throw new InvalidDataException("Invalid compute CB register.");
                inputType=0;kind="UniformBuffer";
            }
            else
            {
                if(name.Length<2 || !uint.TryParse(name.AsSpan(1),NumberStyles.None,CultureInfo.InvariantCulture,out index))
                    throw new InvalidDataException("Invalid compute resource register.");
                inputType=name[0] switch{'s'=>3u,'t'=>2u,'u'=>index==2?6u:4u,_=>throw new InvalidDataException("Unknown compute register.")};
                kind=inputType switch{3=>"Sampler",2=>"Texture",6=>"ImageBuffer",_=>index==4?"ImageBuffer":"Image"};
            }
            var sourceRows=source.Definitions.Bindings.Where(b=>b.InputType==inputType && b.BindPoint==index).ToArray();
            if(sourceRows.Length!=1 || sourceRows[0].BindCount!=1 || !matched.Add(sourceRows[0].Index) || variable.DescriptorSet!=0 || variable.Binding is null)
                throw new InvalidDataException("Reflection descriptor does not uniquely match its original declaration.");
            var row=sourceRows[0];var type=variable.Type;
            if(kind=="UniformBuffer")
            {
                cb=source.Definitions.Buffers.Single(b=>b.Name.Text==row.Name.Text);
                if(variable.StorageClass!=2 || type is not {Kind:"struct",Block:true} || variable.RequiredBufferBytes!=cb.ByteSize)
                    throw new InvalidDataException("Reflection compute CB layout differs from original.");
            }
            else if(kind=="Sampler")
            {
                if(variable.StorageClass!=0 || type.Kind!="sampler")throw new InvalidDataException("Reflection sampler type differs.");
            }
            else
            {
                bool arrayed=row.Dimension==5;
                uint dimension=row.Dimension==1?5u:row.Dimension is 4 or 5?1u:throw new InvalidDataException("Unsupported Reflection image dimension.");
                uint sampled=kind=="Texture"?1u:2u;
                uint format=kind=="ImageBuffer"?(inputType==6?33u:3u):0u;
                bool component=inputType==6?type.ComponentType is {Kind:"int",Bits:32,Signed:false}:type.ComponentType is {Kind:"float",Bits:32};
                if(variable.StorageClass!=0 || type.Kind!="image" || type.Dimension!=dimension || type.Arrayed!=arrayed ||
                    type.Multisampled!=false || type.SampledCode!=sampled || type.ImageFormat!=format || !component)
                    throw new InvalidDataException("Reflection compute image/UAV format differs from original translation.");
                if(inputType==6 && (row.Name.Text!="shCoeff" || source.Definitions.Buffers.Single(b=>b.Name.Text=="shCoeff").ByteSize!=StructuredCoefficientStride))
                    throw new InvalidDataException("Reflection SH coefficient structure stride differs.");
            }
            rows.Add(new(variable,row,cb,kind));
        }
        if(matched.Count!=source.Definitions.Bindings.Count)throw new InvalidDataException("Reflection compute declaration is missing from translated descriptors.");
        return new(module,rows.AsReadOnly(),8,8,1);
    }

    public static byte[] SnapshotConstants(ReflectionShaderStage stage,ReadOnlyMemory<byte> supplied,uint width,uint height,uint sourceListIndex)
    {
        var cb=stage.Definitions.Buffers.Single(b=>b.Name.Text=="$Globals");
        if(width==0 || height==0 || supplied.Length!=cb.ByteSize)throw new InvalidDataException("Reflection requires the complete explicit source CB and nonzero target extent.");
        var output=cb.Variables.Single(v=>v.Name.Text=="gv_info_out");
        var bytes=supplied.ToArray();int at=checked((int)output.StartOffset);
        float w=BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at,4));
        float h=BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at+4,4));
        float previous=BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at+8,4));
        if(w!=width || h!=height || previous!=(sourceListIndex==0?0:1))
            throw new InvalidDataException("Reflection gv_info_out must retain native extent and original list-index previous-result flag.");
        return bytes;
    }
}

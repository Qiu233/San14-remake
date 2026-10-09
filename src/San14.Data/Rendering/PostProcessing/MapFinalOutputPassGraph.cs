using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapFinalOutputSpkParameter(int Index,string Name,string Semantic,
    uint NameHash,uint NativeType,int ByteSize,ReadOnlyMemory<byte> DefaultBytes);
public sealed record MapFinalOutputSpkTexture(int Index,string Name,string Semantic,
    uint NativeKind,int AllocationIndex);
public sealed record MapFinalOutputSpkSampler(int Index,string Name,int TextureIndex,
    uint MagFilter,uint MinFilter,uint AddressU,uint AddressV);
public sealed record MapFinalOutputSpkTechnique(int Index,string Name,int FirstPass,int PassCount);
public sealed record MapFinalOutputPassInput(uint StageCode,uint TextureRegister,
    uint SamplerRegister,int SamplerIndex,int TextureIndex,string TextureName,uint NativeKind);
public sealed record MapFinalOutputPassOutput(uint Register,int TextureIndex,byte Mask)
{
    public bool UsesCurrentTarget=>TextureIndex==-1;
    public bool UsesUnresolvedCustomTarget=>TextureIndex==-2;
}
public sealed record MapFinalOutputPassConstantSlice(int ParameterIndex,string Name,
    int Offset,int ByteSize,ReadOnlyMemory<byte> SourceDefaultBytes);
public sealed record MapFinalOutputPassReflectedField(string Name,int Offset,int ByteSize,
    ReadOnlyMemory<byte>? DeclaredDefaultBytes);
public sealed record MapFinalOutputPassConstantBuffer(uint StageCode,uint Register,int ByteSize,
    IReadOnlyList<MapFinalOutputPassConstantSlice> SourceParameterSlices,
    IReadOnlyList<string> RetainedFields,
    IReadOnlyList<MapFinalOutputPassReflectedField> ReflectedFields);
public sealed record MapFinalOutputPass(int Index,string Name,string FunctorName,
    MapFinalOutputProgramSource Program,IReadOnlyList<MapFinalOutputPassInput> Inputs,
    IReadOnlyList<MapFinalOutputPassOutput> Outputs,
    IReadOnlyList<MapFinalOutputPassConstantBuffer> ConstantBuffers,
    IReadOnlyList<int> ParameterIndices,ReadOnlyMemory<byte> RawFunctor,
    IReadOnlyList<uint> UnmappedOutputRegisters)
{
    public bool RequiresUnportedNativeExecutor=>FunctorName is
        "PostEffectFunctorRenderHexDOFForKids" or "PostEffectFunctorRenderStarForKids" or
        "PostEffectFunctorRenderLensFlare" or "PostEffectFunctorOnce" or "PostEffectFunctorSwitchTex";
}
public sealed record MapFinalOutputTextureAllocation(int TextureIndex,uint PixelFormatCode,
    int Width,int Height,byte Flags14,byte Flags15,bool InvertedFlag16,uint NativeTextureKind)
{
    /// <summary>CreatorC01FF0 forces ColorSpace0 for source TEMP/KEEP at
    /// C0203D. Its render texture descriptor has constant LevelNum1, independently
    /// of allocation+14's render-target selector and allocation+15's usage flag.</summary>
    public uint? ForcedColorSpace=>NativeTextureKind is 3 or 4?0u:null;
    public uint? ForcedLevelNum=>NativeTextureKind is 3 or 4?1u:null;
}
public sealed record MapFinalOutputProducerDraw<T>(MapFinalOutputPass Pass,
    IReadOnlyDictionary<uint,T> PixelInputs,IReadOnlyList<MapFinalOutputTextureAllocation> Allocations,
    IReadOnlyDictionary<int,T> AvailableTextures);
public sealed record MapFinalOutputVertexWrapperState(float PixelOffsetX,float PixelOffsetY,
    ReadOnlyMemory<byte> RetainedUvVectors);

/// <summary>Initial current parameter bytes of a newly loaded native SPK
/// instance, before Camera, functor or retained-state writers. This state does
/// not initialize constant buffers, viewport or wrapper UV storage.</summary>
public sealed class MapFinalOutputFreshParameterState
{
    public IReadOnlyDictionary<int,ReadOnlyMemory<byte>> Parameters { get; }
    public string SpkSha256=>MapFinalOutputResources.SpkSha256;
    public string SourceEvidence { get; }
    internal MapFinalOutputFreshParameterState(
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> parameters,string sourceEvidence)
    {
        Parameters=new System.Collections.ObjectModel.ReadOnlyDictionary<int,ReadOnlyMemory<byte>>(
            parameters.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray()));
        SourceEvidence=sourceEvidence;
    }
}
[Flags]
public enum MapFinalOutputNativeExecutors
{
    None=0,HexDof=1,Star=2,StockFlare=4,Once=8,SwitchHistory=16,
    FinalInputEffects=HexDof|Star|StockFlare
}

/// <summary>Ordered native producer dependencies. Execute never substitutes an
/// absent external texture, aliases Scene to a TEMP, or owns caller resources.
/// The callback executes the actual program with its prepared constants/quad and
/// returns precisely the declared outputs. Its allocation and lifetime stay owned
/// by the GPU runner. Native DOF texWeight preparation remains an explicit input.</summary>
public sealed class MapFinalOutputProducerPlan
{
    public MapFinalOutputPassGraph Graph { get; }
    public IReadOnlyList<MapFinalOutputPass> Steps { get; }
    public IReadOnlyList<int> ExternalTextureIndices { get; }
    public MapFinalOutputNativeExecutors NativeExecutors { get; }
    public IReadOnlyDictionary<int,IReadOnlyList<MapFinalOutputPassInput>> ExecutionInputs { get; }
    internal MapFinalOutputProducerPlan(MapFinalOutputPassGraph graph,IEnumerable<int> indices,
        MapFinalOutputNativeExecutors nativeExecutors=MapFinalOutputNativeExecutors.None)
    {
        Graph=graph;Steps=Array.AsReadOnly(indices.Select(i=>graph.Passes[i]).ToArray());NativeExecutors=nativeExecutors;
        ExecutionInputs=Steps.ToDictionary(p=>p.Index,p=>graph.NativeExecutionInputs(p.Index));
        var produced=new HashSet<int>();var external=new SortedSet<int>();
        foreach(var step in Steps)
        {
            if(step.UnmappedOutputRegisters.Count!=0)throw new InvalidDataException($"SPK pass {step.Index} requires its native custom functor to resolve output registers [{string.Join(',',step.UnmappedOutputRegisters)}].");
            var required=step.FunctorName switch
            {
                "PostEffectFunctorRenderHexDOFForKids"=>MapFinalOutputNativeExecutors.HexDof,
                "PostEffectFunctorRenderStarForKids"=>MapFinalOutputNativeExecutors.Star,
                "PostEffectFunctorRenderLensFlare"=>MapFinalOutputNativeExecutors.StockFlare,
                "PostEffectFunctorOnce"=>MapFinalOutputNativeExecutors.Once,
                "PostEffectFunctorSwitchTex"=>MapFinalOutputNativeExecutors.SwitchHistory,
                _=>MapFinalOutputNativeExecutors.None
            };
            if((nativeExecutors&required)!=required)throw new InvalidDataException($"SPK pass {step.Index} requires its original {required} executor; one generic quad cannot replace it.");
            foreach(var input in ExecutionInputs[step.Index])if(!produced.Contains(input.TextureIndex))external.Add(input.TextureIndex);
            foreach(var output in step.Outputs)
            {
                if(output.UsesCurrentTarget)throw new InvalidDataException("A TEMP producer plan cannot own the native current-target sentinel.");
                produced.Add(output.TextureIndex);
            }
        }
        ExternalTextureIndices=Array.AsReadOnly(external.ToArray());
    }
    public IReadOnlyDictionary<int,T> Execute<T>(int baseWidth,int baseHeight,
        IReadOnlyDictionary<int,T> actualExternalInputs,
        Func<MapFinalOutputProducerDraw<T>,IReadOnlyDictionary<int,T>> submit)
    {
        ArgumentNullException.ThrowIfNull(actualExternalInputs);ArgumentNullException.ThrowIfNull(submit);
        if(actualExternalInputs.Count!=ExternalTextureIndices.Count||ExternalTextureIndices.Any(i=>!actualExternalInputs.TryGetValue(i,out var value)||value is null))
            throw new InvalidDataException("SPK producer plan requires every exact external texture and no additional inferred inputs.");
        var textures=actualExternalInputs.ToDictionary(p=>p.Key,p=>p.Value);
        foreach(var step in Steps)
        {
            var inputs=ExecutionInputs[step.Index].ToDictionary(i=>i.TextureRegister,i=>textures.TryGetValue(i.TextureIndex,out var value)?value:
                throw new InvalidDataException("SPK producer dependency was not produced before its consumer."));
            var allocations=step.Outputs.Select(o=>Graph.ResolveAllocation(o.TextureIndex,baseWidth,baseHeight)).ToArray();
            var result=submit(new(step,inputs,Array.AsReadOnly(allocations),new System.Collections.ObjectModel.ReadOnlyDictionary<int,T>(textures.ToDictionary(p=>p.Key,p=>p.Value))))??throw new InvalidDataException("SPK producer returned no output registry.");
            var expected=step.Outputs.Select(o=>o.TextureIndex).ToArray();
            if(result.Count!=expected.Length||expected.Any(i=>!result.TryGetValue(i,out var value)||value is null))
                throw new InvalidDataException("SPK producer did not return precisely its original ordered MRT/TEMP outputs.");
            foreach(var output in result)textures[output.Key]=output.Value;
        }
        return textures;
    }
}

/// <summary>Hash-pinned SPK4100 technique0 reader and callable producer plan.
/// Native1139740 lays out its serialized tables;1143050 tests per-technique
/// membership masks;1139B20 resolves original names against the shader reflection.
/// A fresh load starts with the serialized parameter bytes. Later accepted
/// state remains explicit and must include every intervening native writer.</summary>
public sealed class MapFinalOutputPassGraph
{
    public LoadedMapFinalOutputResources Source { get; }
    public IReadOnlyList<MapFinalOutputSpkParameter> Parameters { get; }
    public IReadOnlyList<MapFinalOutputSpkTexture> Textures { get; }
    public IReadOnlyList<MapFinalOutputSpkSampler> Samplers { get; }
    public IReadOnlyList<MapFinalOutputSpkTechnique> Techniques { get; }
    public IReadOnlyList<MapFinalOutputPass> Passes { get; }
    private readonly byte[] _spk;
    private MapFinalOutputPassGraph(LoadedMapFinalOutputResources source,byte[] spk,
        MapFinalOutputSpkParameter[] parameters,MapFinalOutputSpkTexture[] textures,
        MapFinalOutputSpkSampler[] samplers,MapFinalOutputSpkTechnique[] techniques,
        MapFinalOutputPass[] passes)
    {
        Source=source;_spk=spk;Parameters=Array.AsReadOnly(parameters);Textures=Array.AsReadOnly(textures);
        Samplers=Array.AsReadOnly(samplers);Techniques=Array.AsReadOnly(techniques);Passes=Array.AsReadOnly(passes);
    }
    public static MapFinalOutputPassGraph Read(LoadedMapFinalOutputResources source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var bytes=source.PostEffectObject.Properties.Single(p=>p.NameHash==0x7AE38475).RawValue.ToArray();
        _=MapFinalOutputResources.ReadSpk(bytes);
        string Name(uint at)
        {
            if(at>=0x1770)throw new InvalidDataException("SPK name exceeds its original string table.");
            int begin=checked(100+(int)at),end=Array.IndexOf(bytes,(byte)0,begin,0x1770-(int)at);
            if(end<0)throw new InvalidDataException("SPK name is unterminated.");
            return Encoding.ASCII.GetString(bytes,begin,end-begin);
        }
        var parameters=Enumerable.Range(0,80).Select(i=>
        {
            int at=0x17D4+i*44,size=checked((int)U(bytes,at+36)),off=checked((int)U(bytes,at+40));
            if(size<0||off<0||(long)off+size>0x478)throw new InvalidDataException("SPK parameter default exceeds its original block.");
            uint semantic=U(bytes,at+4);
            return new MapFinalOutputSpkParameter(i,Name(U(bytes,at)),semantic==0?string.Empty:Name(semantic),U(bytes,at+8),U(bytes,at+20),size,bytes.AsMemory(0x2594+off,size).ToArray());
        }).ToArray();
        var textures=Enumerable.Range(0,32).Select(i=>
        {
            int at=0x2A0C+i*24,index=H(bytes,at+20);
            if(index>=32)throw new InvalidDataException("SPK texture allocation index exceeds its source table.");
            return new MapFinalOutputSpkTexture(i,Name(U(bytes,at)),Name(U(bytes,at+4)),U(bytes,at+16),index);
        }).ToArray();
        var samplers=Enumerable.Range(0,35).Select(i=>
        {
            int at=0x32C8+i*20,texture=checked((int)U(bytes,at));
            if(texture>=32)throw new InvalidDataException("SPK sampler refers to an absent source texture.");
            return new MapFinalOutputSpkSampler(i,Name(U(bytes,0x300C+i*20)),texture,U(bytes,at+4),U(bytes,at+8),U(bytes,at+12),U(bytes,at+16));
        }).ToArray();
        var techniques=Enumerable.Range(0,2).Select(i=>
        {
            int at=0x3584+i*12;return new MapFinalOutputSpkTechnique(i,Name(U(bytes,at)),H(bytes,at+8),H(bytes,at+10));
        }).ToArray();
        if(techniques[0]!=new MapFinalOutputSpkTechnique(0,"t0",0,29)||techniques[1]!=new MapFinalOutputSpkTechnique(1,"t1",29,29))
            throw new InvalidDataException("SPK techniques differ from the checked source ordering.");
        var passes=new MapFinalOutputPass[29];
        for(int ordinal=0;ordinal<29;ordinal++)
        {
            int at=0x43FC+ordinal*28,functor=H(bytes,at+22),fo=0x4A54+functor*48;
            if(functor>=58)throw new InvalidDataException("SPK pass has an invalid source functor index.");
            string functorName=Name(U(bytes,fo));int target=H(bytes,fo+20);
            var program=MapFinalOutputResources.SpkProgram(source,checked((int)U(bytes,at+12)),checked((int)U(bytes,at+16)));
            var rdef=DxbcResourceDefinitionReader.Read(program.PixelBytes.Span);
            var memberSamplers=samplers.Where(s=>Member(bytes,0x3F9C+s.Index*16,ordinal)).ToArray();
            var inputs=new List<MapFinalOutputPassInput>();
            foreach(var binding in rdef.Bindings.Where(b=>b.InputType==3))
            {
                var sampler=memberSamplers.SingleOrDefault(s=>s.Name==binding.Name.Text)??throw new InvalidDataException("Reflected SPK sampler has no exact native membership/name join.");
                var image=rdef.Bindings.SingleOrDefault(b=>b.InputType==2&&b.Name.Text==sampler.Name+"_Tex")??throw new InvalidDataException("SPK sampler image name has no unique original reflection join.");
                if(binding.BindCount!=1||image.BindCount!=1)throw new InvalidDataException("SPK source producer array binding is unsupported.");
                var texture=textures[sampler.TextureIndex];
                inputs.Add(new(0,image.BindPoint,binding.BindPoint,sampler.Index,texture.Index,texture.Name,texture.NativeKind));
            }
            if(inputs.Count!=memberSamplers.Length)throw new InvalidDataException("SPK native sampler membership differs from its pixel reflection.");
            var outputIds=new List<int>{target==0xFFFF?-1:target};
            if(functorName=="PostEffectFunctorMRTForKids")
            {
                int count=checked((int)F(bytes,fo+4));
                if(count<1||count>4)throw new InvalidDataException("SPK MRT functor output count is outside its proved native parameter record.");
                for(int i=1;i<count;i++)outputIds.Add(checked((int)F(bytes,fo+4+i*4)));
            }
            else if(functorName=="PostEffectFunctorRenderHexDOFForKids")
            {
                // E257D0 stores parameter0 in+B8;VT+70 E22DE0 resolves it.
                // E1E8BE selects that texture on the final internal iteration,
                // and E1EB9A..E1EBA2 submits primary/additional MRT count2.
                outputIds.Add(checked((int)F(bytes,fo+4)));
            }
            var signatures=DxbcSignatureReader.Read(program.PixelBytes.Span).Parts.Single(p=>p.Kind==DxbcSignatureKind.Output).Parameters;
            var targets=signatures.Where(p=>p.SemanticName.Text.Equals("SV_Target",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p.SemanticIndex).ToArray();
            if(targets.Length!=outputIds.Count||outputIds.Any(i=>i>=32))
            {
                string signatureText=string.Join(',',signatures.Select(s=>$"{s.SemanticName.Text}{s.SemanticIndex}:r{s.Register}:m{s.Mask}"));
                throw new InvalidDataException($"SPK pass {ordinal} ({Name(U(bytes,at))}), functor {functor} ({functorName}), target {target}, VS {U(bytes,at+12)}, PS {U(bytes,at+16)}: declared outputs [{string.Join(',',outputIds)}] disagree with exact original PS signatures [{signatureText}].");
            }
            var outputs=targets.Select((t,i)=>new MapFinalOutputPassOutput(t.SemanticIndex,i<outputIds.Count?outputIds[i]:-2,t.Mask)).ToArray();
            var unmapped=targets.Skip(outputIds.Count).Select(t=>t.SemanticIndex).ToArray();
            var memberParameters=parameters.Where(p=>Member(bytes,0x359C+p.Index*16,ordinal)).ToArray();
            var constants=new List<MapFinalOutputPassConstantBuffer>();
            foreach(var stage in new[]{(Stage:1u,Bytes:program.VertexBytes),(Stage:0u,Bytes:program.PixelBytes)})
            {
                var definition=DxbcResourceDefinitionReader.Read(stage.Bytes.Span);
                foreach(var buffer in definition.Buffers)
                {
                    var binding=definition.Bindings.Single(b=>b.InputType==0&&b.Name.Text==buffer.Name.Text);
                    var slices=new List<MapFinalOutputPassConstantSlice>();var retained=new List<string>();
                    var fields=new List<MapFinalOutputPassReflectedField>();
                    foreach(var variable in buffer.Variables)
                    {
                        ReadOnlyMemory<byte>? declared=variable.DefaultValueRange is { } range?range.Slice(definition.PayloadRangeInProgram.Slice(stage.Bytes.Span)).ToArray():null;
                        fields.Add(new(variable.Name.Text,variable.StartOffset,variable.ByteSize,declared));
                        var parameter=memberParameters.SingleOrDefault(p=>p.Name==variable.Name.Text);
                        if(parameter is null){retained.Add(variable.Name.Text);continue;}
                        if(parameter.ByteSize!=variable.ByteSize)throw new InvalidDataException("SPK parameter size differs from its named reflected destination.");
                        slices.Add(new(parameter.Index,parameter.Name,variable.StartOffset,variable.ByteSize,parameter.DefaultBytes.ToArray()));
                    }
                    constants.Add(new(stage.Stage,binding.BindPoint,buffer.ByteSize,slices.AsReadOnly(),retained.AsReadOnly(),fields.AsReadOnly()));
                }
            }
            passes[ordinal]=new(ordinal,Name(U(bytes,at)),functorName,program,inputs.AsReadOnly(),Array.AsReadOnly(outputs),constants.AsReadOnly(),Array.AsReadOnly(memberParameters.Select(p=>p.Index).ToArray()),bytes.AsMemory(fo,48).ToArray(),Array.AsReadOnly(unmapped));
        }
        return new(source,bytes,parameters,textures,samplers,techniques,passes);
    }
    public MapFinalOutputProducerPlan CreateDofAndRadialProducerPlan(bool runDistantDofPasses=false)
        =>new(this,runDistantDofPasses?new[]{0,1,2,25,26,27}:new[]{0,1,2,27});

    /// <summary>1139740 reads the metadata into Shader+20;1139690 makes
    /// Shader+C0 point to the serialized parameter block.113B4E1..113B4F4
    /// uploads record+28 relative to that pointer. This is current state only
    /// at fresh initialization, before any Camera/functor writes.</summary>
    public MapFinalOutputFreshParameterState CreateFreshSourceParameterState()
    {
        var initial=Enumerable.Range(0,80).ToDictionary(i=>i,i=>
        {
            int record=0x17D4+i*44,size=checked((int)U(_spk,record+36)),offset=checked((int)U(_spk,record+40));
            return (ReadOnlyMemory<byte>)_spk.AsMemory(0x2594+offset,size).ToArray();
        });
        return new(initial,Source.PostEffect.Source.ResourceUri+"#A1C83E34/fresh-SPK-current-parameters;"+
            "TC:1138EC0/1139240/1139740/1139690/1139410/113B4E1;"+
            "SC:113A0B0/113A430/113A930/113A880/113A600/113C6D1;"+
            "evidence=ce-map-final-output-source-20261002/camera-fields/spk-links/fresh-contract.json");
    }

    /// <summary>Every producer needed by the final image's effect inputs. This
    /// includes actual custom executors, separately from their declared SPK
    /// programs. The independent legacy luminance/history group4..9 is outside
    /// this dependency plan; node1 supplies the actual Exposure14 input.</summary>
    public MapFinalOutputProducerPlan CreateFinalInputProducerPlan(
        MapFinalOutputNativeExecutors nativeExecutors,bool runDistantDofPasses=false)
        =>new(this,Enumerable.Range(0,4).Concat(Enumerable.Range(10,15))
            .Concat(runDistantDofPasses?new[]{25,26,27}:new[]{27}),nativeExecutors);

    /// <summary>Native Flare18 overrides its empty PS18 placeholder with stock
    /// screen PS3 and samples the texture in its first functor float parameter.
    /// The separate list preserves the original declared reflection unchanged.</summary>
    public IReadOnlyList<MapFinalOutputPassInput> NativeExecutionInputs(int passIndex)
    {
        var pass=Passes[passIndex];
        if(pass.FunctorName!="PostEffectFunctorRenderLensFlare")return pass.Inputs;
        int index=checked((int)BinaryPrimitives.ReadSingleLittleEndian(pass.RawFunctor.Span.Slice(4,4)));
        if(index!=21||pass.Inputs.Count!=0)throw new InvalidDataException("Native Flare source texture and its declared zero-placeholder reflection disagree.");
        var texture=Textures[index];
        return Array.AsReadOnly(new[]{new MapFinalOutputPassInput(0,0,0,-1,index,texture.Name,texture.NativeKind)});
    }

    /// <summary>Native cache E1F3D0/E24640 selects Distant DOF only when the
    /// current float KIDS_DOF_TYPE is exactly2 and ordered. Source type0 skips
    /// passes25/26 despite its nonzero threshold/intensity declarations.</summary>
    public bool ShouldRunDistantDofPasses(IReadOnlyDictionary<int,ReadOnlyMemory<byte>> currentParameters)
    {
        ArgumentNullException.ThrowIfNull(currentParameters);
        if(Parameters[25].NativeType!=0x15||!currentParameters.TryGetValue(25,out var value)||value.Length!=4)
            throw new InvalidDataException("Native Distant gate requires the complete current float KIDS_DOF_TYPE parameter25.");
        return BinaryPrimitives.ReadSingleLittleEndian(value.Span)==2;
    }

    /// <summary>Copies the original SPK declarations for a caller that explicitly
    /// chooses source defaults. These values do not establish prepared camera
    /// state, dynamic functor outputs, or a previously accepted frame.</summary>
    public IReadOnlyDictionary<int,ReadOnlyMemory<byte>> CopyDeclaredDefaultParameters()
        =>Parameters.ToDictionary(p=>p.Index,p=>(ReadOnlyMemory<byte>)p.DefaultBytes.ToArray());

    public MapFinalOutputTextureAllocation ResolveAllocation(int textureIndex,int baseWidth,int baseHeight)
    {
        if(textureIndex<0||textureIndex>=Textures.Count||baseWidth<=0||baseHeight<=0)
            throw new InvalidDataException("SPK allocation needs an actual texture index and explicit positive native base extent.");
        int at=0x2D0C+Textures[textureIndex].AllocationIndex*24;
        return new(textureIndex,U(_spk,at),Dimension(baseWidth,U(_spk,at+4),F(_spk,at+12)),Dimension(baseHeight,U(_spk,at+8),F(_spk,at+16)),_spk[at+20],_spk[at+21],_spk[at+22]==0,Textures[textureIndex].NativeKind);
    }
    /// <summary>Original typeless resource and base SRV/RTV formats for the
    /// proved TEMP/KEEP creator. C01FF0 forces ColorSpace0; untouched property15
    /// defaults to OutputColorSpace2 in C06D40 and inherits0 atC06FAE.</summary>
    public RenderTextureFormats ResolveTempFormats(int textureIndex)
    {
        if(textureIndex<0||textureIndex>=Textures.Count||Textures[textureIndex].NativeKind is not(3 or 4))
            throw new InvalidDataException("Native TEMP format resolution requires an actual SPK TEMP/KEEP texture.");
        uint format=U(_spk,0x2D0C+Textures[textureIndex].AllocationIndex*24);
        return format switch
        {
            1=>new(90,87,87),2=>new(39,41,41),3=>new(9,10,10),
            _=>throw new InvalidDataException($"SPK TEMP pixel format {format} has no proved native view recipe in this producer implementation.")
        };
    }
    public IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> BuildConstants(int passIndex,
        IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> completeRetainedTemplates,
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> currentParameters)
    {
        ArgumentNullException.ThrowIfNull(completeRetainedTemplates);ArgumentNullException.ThrowIfNull(currentParameters);
        var pass=Passes[passIndex];var result=new Dictionary<(uint,uint),ReadOnlyMemory<byte>>();
        if(completeRetainedTemplates.Count!=pass.ConstantBuffers.Count)throw new InvalidDataException("SPK producer requires all exact retained native CB templates.");
        foreach(var buffer in pass.ConstantBuffers)
        {
            var key=(buffer.StageCode,buffer.Register);
            if(!completeRetainedTemplates.TryGetValue(key,out var original)||original.Length!=buffer.ByteSize)throw new InvalidDataException("SPK retained CB template has a missing or wrong-sized original binding.");
            byte[] bytes=original.ToArray();
            foreach(var slice in buffer.SourceParameterSlices)
            {
                if(!currentParameters.TryGetValue(slice.ParameterIndex,out var value)||value.Length!=slice.ByteSize)throw new InvalidDataException("SPK producer requires each current named parameter slice; no implicit source defaults are applied.");
                value.Span.CopyTo(bytes.AsSpan(slice.Offset,slice.ByteSize));
            }
            result.Add(key,bytes);
        }
        return result;
    }

    /// <summary>Ports the proved p0/p1/p2/p27 vertex uploads:1143C40's
    /// position/texture rectangle,113C5F0's rounded VIEWPORT rectangle, and
    /// E25DD0/113BD80's target-normalized TEX_OFFSET vectors. Bytes outside the
    /// original upload ranges retain the caller's complete source CB template.
    /// p27 has no TEX_OFFSET parameter and uploads its actual retained wrapper
    /// vectors. Both current VIEWPORT and wrapper state are explicit inputs.</summary>
    public ReadOnlyMemory<byte> BuildRequiredPassVertexConstants(int passIndex,
        int targetWidth,int targetHeight,ReadOnlyMemory<byte> completeRetainedTemplate,
        MapFinalOutputVertexWrapperState wrapper,
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> currentParameters)
    {
        ArgumentNullException.ThrowIfNull(wrapper);ArgumentNullException.ThrowIfNull(currentParameters);
        if(passIndex is not(0 or 1 or 2 or 27)||targetWidth<=0||targetHeight<=0||targetWidth>ushort.MaxValue||targetHeight>ushort.MaxValue)
            throw new InvalidDataException("Native vertex writer requires a proved producer pass and its actual positive target0 u16 resource extent.");
        var buffer=Passes[passIndex].ConstantBuffers.Single(b=>b.StageCode==1&&b.Register==0);
        if(completeRetainedTemplate.Length!=buffer.ByteSize)
            throw new InvalidDataException("Native vertex writer requires its complete retained CB0 template.");
        if(!currentParameters.TryGetValue(14,out var viewport)||viewport.Length!=16)
            throw new InvalidDataException("Native vertex writer requires current SPK VIEWPORT14; its zero declaration does not establish a full viewport.");
        int vectorCount=passIndex switch{1=>2,2=>8,27=>3,_=>0};
        if(wrapper.RetainedUvVectors.Length!=(passIndex==27?vectorCount*16:0))
            throw new InvalidDataException("Native sentinel UV writer requires exactly its three actual wrapper vectors; other proved passes use their current TEX_OFFSET parameter.");
        byte[] bytes=completeRetainedTemplate.ToArray();
        WriteFloat(bytes,0,Add(Divide(wrapper.PixelOffsetX,targetWidth),0));
        WriteFloat(bytes,4,Add(Divide(wrapper.PixelOffsetY,targetHeight),0));
        WriteFloat(bytes,8,0);WriteFloat(bytes,12,0);
        var horizontal=TextureBounds(targetWidth,BinaryPrimitives.ReadSingleLittleEndian(viewport.Span),BinaryPrimitives.ReadSingleLittleEndian(viewport.Span[8..]));
        var vertical=TextureBounds(targetHeight,BinaryPrimitives.ReadSingleLittleEndian(viewport.Span[4..]),BinaryPrimitives.ReadSingleLittleEndian(viewport.Span[12..]));
        int textureOffset=buffer.ByteSize==32?16:272;
        WriteFloat(bytes,textureOffset,Subtract(horizontal.End,horizontal.Begin));
        WriteFloat(bytes,textureOffset+4,Subtract(vertical.End,vertical.Begin));
        WriteFloat(bytes,textureOffset+8,horizontal.Begin);WriteFloat(bytes,textureOffset+12,vertical.Begin);
        if(passIndex==27)wrapper.RetainedUvVectors.Span.CopyTo(bytes.AsSpan(16,48));
        else if(vectorCount>0)
        {
            int parameterIndex=passIndex==1?4:8;
            if(!currentParameters.TryGetValue(parameterIndex,out var offsets)||offsets.Length!=vectorCount*16)
                throw new InvalidDataException("Native UV writer requires every current selected TEX_OFFSET float4.");
            float reciprocalWidth=Divide(1,targetWidth),reciprocalHeight=Divide(1,targetHeight);
            for(int i=0;i<vectorCount*4;i++)WriteFloat(bytes,16+i*4,
                Multiply(BinaryPrimitives.ReadSingleLittleEndian(offsets.Span.Slice(i*4,4)),(i&1)==0?reciprocalWidth:reciprocalHeight));
        }
        return bytes;
    }
    public IReadOnlyList<MapFinalOutputSamplerBinding> PrepareSamplers(int passIndex,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> actualProducerKeys)
    {
        ArgumentNullException.ThrowIfNull(actualProducerKeys);var pass=Passes[passIndex];var result=new List<MapFinalOutputSamplerBinding>();
        if(actualProducerKeys.Count!=pass.Inputs.Count)throw new InvalidDataException("SPK producer needs precisely every actual native sampler key.");
        foreach(var input in pass.Inputs)
        {
            if(!actualProducerKeys.TryGetValue(input.SamplerRegister,out var original)||original.Length!=8)throw new InvalidDataException("SPK producer sampler key is missing or incomplete.");
            byte[] key=original.ToArray();var sampler=Samplers[input.SamplerIndex];
            if(input.NativeKind!=5)
            {
                ushort prior=BinaryPrimitives.ReadUInt16LittleEndian(key);
                ushort address=(ushort)(((uint)prior&0xFFF0)|(sampler.AddressU&3)|((sampler.AddressV&3)<<2));
                ushort final=(ushort)(((uint)address&0x03FF)|(((sampler.MinFilter&3)|((sampler.MagFilter&3)<<2))<<10));
                BinaryPrimitives.WriteUInt16LittleEndian(key,final);
            }
            result.Add(new(input.SamplerRegister,input.SamplerIndex,(uint)input.TextureIndex,input.NativeKind,key,RenderSamplerSourceAdapters.DecodeKey(Source.StockScreen.Native,key)));
        }
        return result.AsReadOnly();
    }
    private static bool Member(byte[] bytes,int at,int pass)
        =>U(bytes,at)==0&&(U(bytes,at+4+(pass/32)*4)&(1u<<(pass&31)))!=0;
    private static int Dimension(int extent,uint additive,float scale)
    {
        if(!(scale>0))return additive!=0?unchecked((int)additive):extent;
        float value=Add(Multiply(extent,scale),additive);
        return float.IsNaN(value)||value>=2147483648f||value< -2147483648f?int.MinValue:(int)value;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Multiply(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Subtract(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
    private static (float Begin,float End) TextureBounds(int extent,float origin,float span)
    {
        int first=RoundedPixel(extent,origin),last=unchecked(first+RoundedPixel(extent,span));
        float normalizedBegin=Divide(first,extent),normalizedSpan=Subtract(Divide(last,extent),normalizedBegin);
        float ratio=Divide(span,normalizedSpan);
        return(Subtract(origin,Multiply(ratio,normalizedBegin)),Add(origin,Multiply(Subtract(1,normalizedBegin),ratio)));
    }
    private static int RoundedPixel(int extent,float value)
    {
        float rounded=MathF.Floor(Add(Multiply(extent,value),.5f));
        return float.IsNaN(rounded)||rounded>=2147483648f||rounded< -2147483648f?int.MinValue:(int)rounded;
    }
    private static void WriteFloat(byte[] bytes,int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at,4),value);
    private static uint U(byte[] bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at,4));
    private static int H(byte[] bytes,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at,2));
    private static float F(byte[] bytes,int at)=>BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at,4));
}

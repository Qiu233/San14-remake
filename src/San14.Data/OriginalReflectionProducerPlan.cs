using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalReflectionShaderStage(uint StageCode, int ProgramIndex,
    string ProgramSha256, DxbcResourceDefinitions Definitions, DxbcProgramSignatures Signatures);
public sealed record OriginalReflectionShaderPass(uint PassIndex, G2ShaderCombination Combination,
    G1ShaderResourceBindings Resources, IReadOnlyList<OriginalReflectionShaderStage> Stages);
public sealed record OriginalReflectionSourceInput(string Role, uint CpuObjectOffset,
    TaskGraphArgument Argument, OriginalRenderTextureDefinition Texture);
public sealed record OriginalReflectionRawTarget(uint Slot, uint? RawDescriptorIndex,
    long? ByteOffset, uint? DeclaredSlice, bool RequiresCurrentAttachment);

/// <summary>Exact node66 RefProbe source selection, including all original
/// compute/draw rows. Active execution is incomplete: intermediate production
/// and the three active raw target descriptors are not a proved RR0/1/2 map.
/// No current View/provider values or intermediate pixels are inferred.</summary>
public sealed class LoadedOriginalReflectionProducerPlan
{
    public LoadedRenderGraph Graph { get; }
    public CompiledGraphNode Node { get; }
    public LoadedObjects ShaderObjects { get; }
    public DokObject ShaderObject { get; }
    public DokProperty ShaderReference { get; }
    public LoadedShaderContainer Shader { get; }
    public OriginalRenderTextureDefinition Target { get; }
    public byte ViewId { get; }
    public uint LightGroupId { get; }
    public IReadOnlyList<OriginalReflectionSourceInput> Inputs { get; }
    public IReadOnlyList<OriginalReflectionShaderPass> Passes { get; }
    public OriginalReflectionShaderPass FinalPass => Passes[12];
    public uint FinalVertexCount => 3;
    public uint FinalColorWriteMask => 7;
    public bool CanExecute => false;
    public IReadOnlyList<string> MissingContracts { get; } = Array.AsReadOnly(new[] {
        "active-raw-target-indices-3-4-5-not-resolved-to-three-live-attachments",
        "FD77B0-FD7AE0-probe-list-compute-dispatch-and-ping-pong-intermediates-not-produced",
        "prepared-view-provider-constants-resource-packets-and-order-not-complete" });

    internal LoadedOriginalReflectionProducerPlan(LoadedRenderGraph graph, CompiledGraphNode node,
        LoadedObjects objects, DokObject shaderObject, DokProperty reference, LoadedShaderContainer shader,
        OriginalRenderTextureDefinition target, byte viewId, uint lightGroup,
        OriginalReflectionSourceInput[] inputs, OriginalReflectionShaderPass[] passes)
        => (Graph,Node,ShaderObjects,ShaderObject,ShaderReference,Shader,Target,ViewId,LightGroupId,Inputs,Passes) =
            (graph,node,objects,shaderObject,reference,shader,target,viewId,lightGroup,
                Array.AsReadOnly(inputs),Array.AsReadOnly(passes));

    public void RequireExecutable() => throw new NotSupportedException(
        "Original active ReflectionProbe cannot write yet: " + string.Join("; ", MissingContracts));
}

public static class OriginalReflectionProducerPlan
{
    public const uint FactoryClassId = 0x88416CDB;
    public const uint ShaderObjectId = 0xD1EB2B6A;
    public const uint ShaderObjectClassId = 0x0FB246DB;
    public const uint ShaderFileId = 0xD070067D;
    public const string ShaderSha256 = "8B73DA27C874AF0E4A2E77CCD89F2DFCC28719AE8E87708D740A518072B6E02C";
    public const string GraphSha256 = "B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";

    public static LoadedOriginalReflectionProducerPlan Read(LoadedRenderGraph graph,
        LoadedObjects shaderObjects, LoadedObjects textures, LoadedShaderContainer shader)
    {
        ArgumentNullException.ThrowIfNull(graph); ArgumentNullException.ThrowIfNull(shaderObjects);
        ArgumentNullException.ThrowIfNull(textures); ArgumentNullException.ThrowIfNull(shader);
        if (graph.Document.GraphKind != OriginalCompiledGraphKind.RenderGraph ||
            graph.Source.FileId != 0x024496EE || graph.Document.Nodes.Count != 211 ||
            graph.Source.DecodedSha256 != GraphSha256 || Hash(graph.Source.Bytes) != GraphSha256)
            throw new InvalidDataException("Reflection source requires the verified full original RenderGraph.");
        var node = graph.Document.Nodes[66];
        if (node.Index != 66 || node.Call.FactoryClassId != FactoryClassId || node.Call.Arguments.Count != 9)
            throw new InvalidDataException("Reflection node66 differs from the source factory contract.");
        TaskGraphArgument Arg(uint hash,uint descriptor,int bytes)
        {
            var fields=node.Call.Arguments.Where(a=>a.NameHash==hash && a.DescriptorWord==descriptor).ToArray();
            if(fields.Length!=1 || fields[0].RawValue.Length!=bytes)
                throw new InvalidDataException("Reflection requires one explicit typed field "+hash.ToString("X8"));
            return fields[0];
        }
        uint U(uint name)=>BinaryPrimitives.ReadUInt32LittleEndian(Arg(name,0x05000001,4).RawValue);
        byte view=Arg(0x9075EDA0,0x01000001,1).RawValue[0];
        if(U(0x72DB7EBD)!=0xE1B6A687 || view!=0 || U(0x59BD6FC4)!=8 ||
            U(0x1BA2A872)!=OriginalDeferredExtraLayerPlan.RrObjectId || U(0xBEB4831E)!=ShaderObjectId)
            throw new InvalidDataException("Reflection View/group/target/shader references differ from node66.");
        var objects=shaderObjects.Document.Objects.Where(o=>o.ObjectId==ShaderObjectId).ToArray();
        if(objects.Length!=1 || objects[0].Kind!=DokObjectKind.Inline || objects[0].ClassId!=ShaderObjectClassId)
            throw new InvalidDataException("Reflection requires its original inline RefProbe shader object.");
        var refs=objects[0].Properties.Where(p=>p.NameHash==0x193025DF).ToArray();
        if(refs.Length!=1 || refs[0].TypeCode!=5 || refs[0].ElementCount!=1 || refs[0].RawValue.Length!=4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(refs[0].RawValue)!=ShaderFileId ||
            shader.Source.FileId!=ShaderFileId || shader.Source.DecodedSha256!=ShaderSha256 ||
            Hash(shader.Source.Bytes)!=ShaderSha256)
            throw new InvalidDataException("Reflection G1S reference or complete RefProbe shader bytes differ.");
        var target=OriginalRenderTextureResources.Read(textures,U(0x1BA2A872));
        if(target.Kind!=OriginalRenderTextureKind.Texture2DArray || target.ArrayDepth!=3 ||
            target.LevelNum!=1 || target.NeedSliceTargets!=1 || target.NeedAllTargets!=0 ||
            OriginalRenderTextureFormatMath.Resolve(target).TargetViewFormat!=26)
            throw new InvalidDataException("Reflection source RR array/mip/format contract differs.");
        (string Role,uint Offset,uint Field,uint Id)[] sourceInputs = [
            ("Depth",0x20,0xAD514389,0xE13DDB4F), ("Normal",0x28,0x88A03085,0x7E94C315),
            ("Shadowmap",0x30,0x61A17857,0xEA36C27D), ("RS",0x38,0x454BF54B,0x521C7DED) ];
        var inputs=sourceInputs.Select(i=>
        {
            if(U(i.Field)!=i.Id)throw new InvalidDataException("Reflection texture reference differs: "+i.Role);
            return new OriginalReflectionSourceInput(i.Role,i.Offset,Arg(i.Field,0x05000001,4),
                OriginalRenderTextureResources.Read(textures,i.Id));
        }).ToArray();
        var tables=shader.ReadG1Bindings();
        if(tables.PassCount!=14 || !tables.RawDimensionCounts.SequenceEqual(new uint[]{1,0,0,0,0,0,1}) ||
            tables.Combinations.Rows.Count!=14 || tables.Variants.RecordWidth!=8)
            throw new InvalidDataException("RefProbe source pass/dimension table shape differs.");
        // Every effective source dimension has width1. Its sole valid index is
        // zero; this follows source bounds and does not infer committed state.
        uint[] indices=new uint[7];
        var passes=Enumerable.Range(0,14).Select(i=>
        {
            var row=tables.ResolveCombination(indices,(uint)i);
            var resources=shader.ReadG1ResourceBindings(row.Index);
            var stages=row.Stages.Where(s=>s.Entry is not null).Select(s=>new OriginalReflectionShaderStage(
                s.StageCode,s.Entry!.ProgramIndex,
                Hash(shader.Container.GetProgramBytes(shader.Source.Bytes,s.Entry.ProgramIndex)),
                shader.ReadResourceDefinitions(s.Entry.ProgramIndex),shader.ReadSignatures(s.Entry.ProgramIndex))).ToArray();
            return new OriginalReflectionShaderPass((uint)i,row,resources,Array.AsReadOnly(stages));
        }).ToArray();
        var final=passes[12];
        if(final.Stages.Count!=2 || final.Stages.Single(s=>s.StageCode==1).ProgramIndex!=1 ||
            final.Stages.Single(s=>s.StageCode==0).ProgramIndex!=6 ||
            final.Stages.Single(s=>s.StageCode==1).ProgramSha256!="D583026266536E5973DDFD5C75D7E7DB684B1784B414AB7C5BE03ED2E423757A" ||
            final.Stages.Single(s=>s.StageCode==0).ProgramSha256!="D8C6D927D130B57684B9620EFAE545EF0A238CA20DB53F818FF2DCC331656F84")
            throw new InvalidDataException("RefProbe final pass12 source programs differ.");
        var pixel=final.Stages.Single(s=>s.StageCode==0);
        if(pixel.Definitions.Buffers.Count!=0 || pixel.Definitions.Bindings.Count!=2 ||
            pixel.Definitions.Bindings.Any(b=>b.InputType!=2 || b.Dimension!=4 || b.BindCount!=1) ||
            !pixel.Definitions.Bindings.Select(b=>(b.Name.Text,b.BindPoint)).SequenceEqual(
                new[]{("sPreviousResult",8u),("sPreviousResult2",9u)}) ||
            !pixel.Signatures.Parts.Single(p=>p.Kind==DxbcSignatureKind.Output).Parameters
                .Select(p=>(p.SemanticName.Text,p.SemanticIndex,p.Register,p.Mask))
                .SequenceEqual(new[]{("SV_Target",0u,0u,(byte)7),("SV_Target",1u,1u,(byte)7),("SV_Target",2u,2u,(byte)7)}))
            throw new InvalidDataException("RefProbe final textures or three RGB outputs differ.");
        return new(graph,node,shaderObjects,objects[0],refs[0],shader,target,view,U(0x59BD6FC4),inputs,passes);
    }

    /// <summary>EC8664..EC86D9 uses multiplier3/4/5, not fallback1/2/3.
    /// Under the proved all-array mip prefix, raw3 is RR slice2 while raw4/5
    /// exceed the declared three layers. Keep that gap instead of wrapping it.
    /// Missing raw descriptors instead require three current attachment slots.</summary>
    public static IReadOnlyList<OriginalReflectionRawTarget> FinalRawTargets(ushort explicitRawMipWord,
        bool rawSliceDescriptorsPresent,uint explicitArrayDepth)
    {
        if(!rawSliceDescriptorsPresent)return Array.AsReadOnly(Enumerable.Range(0,3)
            .Select(i=>new OriginalReflectionRawTarget((uint)i,null,null,null,true)).ToArray());
        uint mips=((uint)explicitRawMipWord>>10)&31;
        if(mips==0 || explicitArrayDepth==0)throw new InvalidDataException("Active reflection raw descriptor source has zero mip/depth.");
        return Array.AsReadOnly(Enumerable.Range(0,3).Select(i=>
        {
            uint multiplier=(uint)i+3,index=multiplier*mips,slice=multiplier-1;
            return new OriginalReflectionRawTarget((uint)i,index,(long)index*0x268,
                slice<explicitArrayDepth ? slice : null,false);
        }).ToArray());
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
}

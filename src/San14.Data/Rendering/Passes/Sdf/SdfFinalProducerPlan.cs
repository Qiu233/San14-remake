using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum SdfFinalAction { Preserve,Draw,RequiresCompute,RequiresIntermediates }
/// <summary>Caller-proved selected operation1/graphics readiness. A completed
/// input claim describes supplied upstream images; it is not inferred from
/// shader existence, fallback once bits or an empty occluder list.</summary>
public sealed record SdfFinalSelection(bool Operation1Present,bool SelectedShaderReady,
    byte Interface79,bool IntermediatesCompleted);
public sealed record SdfFinalDecision(SdfFinalAction Action,string Reason);
public sealed record SdfGraphicsGateDecision(bool EntersActive,uint Threshold,byte? Interface79Write);
public sealed record SdfFinalProgramSource(LoadedDeferredExtraLayerPlan Extra,DokObject SdfObject,
    DokProperty OcclusionShaderReference,NamedModelShader Provider,LoadedShaderContainer Shader,
    IReadOnlyList<uint> ExplicitDimensions,uint ExplicitPass,G1ShaderResourceBindings Resources,
    DxbcResourceDefinitions PixelDefinitions,DxbcProgramSignatures PixelSignatures)
{
    public int CombinationIndex=>Resources.Combination.Index;
    public uint ColorWriteMask=>3;
    public bool WholeActiveProducerImplemented=>false;
}

/// <summary>Source node164 final graphics program with an explicit caller
/// resolved G1S row. Native operation1 is not equated with G1S pass1 or row4.
/// Earlier SDF dispatches/uploads and native committed pass selection remain
/// separate source contracts.</summary>
public static class SdfFinalProducerPlan
{
    public const uint SdfObjectId=0xA024EF37,SdfClassId=0x71B26C2F;
    public const uint ShaderObjectId=0x7A2527FC,ShaderClassId=0xE4CB21FE,ShaderFileId=0xFD20CB1A;
    public const string ShaderSha256="C2C4F7C1AD4E7914244ACE15F73CC0EF439011EB9D1C430B0B8069F8E5AC624C";
    public const string VertexSha256="C51D71A59DC89E4B23B68395180A41473B35914AD33BCE80C92AF636A379757D";
    public const string PixelSha256="0E4B8681AB55D0F03DA56FE4CD26326E24AA0B8960DF9EAFD2C73AB891DA28CE";

    public static SdfFinalProgramSource Read(LoadedDeferredExtraLayerPlan extra,
        LoadedObjects shaderObjects,LoadedShaderContainer shader,IReadOnlyList<uint> explicitSevenDimensions,uint explicitPass)
    {
        ArgumentNullException.ThrowIfNull(extra);ArgumentNullException.ThrowIfNull(shaderObjects);
        ArgumentNullException.ThrowIfNull(shader);ArgumentNullException.ThrowIfNull(explicitSevenDimensions);
        var n=extra.SdfNode;
        TaskGraphArgument Arg(uint hash,uint descriptor,int bytes)
        {
            var args=n.Call.Arguments.Where(a=>a.NameHash==hash && a.DescriptorWord==descriptor).ToArray();
            if(args.Length!=1 || args[0].RawValue.Length!=bytes)throw new InvalidDataException("Missing explicit SDF final graph field.");
            return args[0];
        }
        uint U(uint h)=>BinaryPrimitives.ReadUInt32LittleEndian(Arg(h,0x05000001,4).RawValue);
        byte B(uint h)=>Arg(h,0x01000001,1).RawValue[0];
        if(extra.Deferred.Graph.Source.DecodedSha256!=ReflectionProducerPlan.GraphSha256 || n.Index!=164 ||
            n.Call.FactoryClassId!=0xEBFE38E8 || U(0x3D905A18)!=SdfObjectId || U(0xFA5A06A8)!=3 ||
            U(0x43E79658)!=DeferredExtraLayerPlan.RsObjectId || B(0x69E3D5B7)!=1 || B(0xE98D44D5)!=2)
            throw new InvalidDataException("SDF final source requires exact node164 RS3/RG contract.");
        var objects=extra.Deferred.TextureObjects.Document.Objects.Where(o=>o.ObjectId==SdfObjectId).ToArray();
        if(objects.Length!=1 || objects[0].Kind!=DokObjectKind.Inline || objects[0].ClassId!=SdfClassId)
            throw new InvalidDataException("SDF final requires original ViewSDF source object A024EF37.");
        var fields=objects[0].Properties.Where(p=>p.NameHash==0x5C6659B5).ToArray();
        if(fields.Length!=1 || fields[0].TypeCode!=5 || fields[0].ElementCount!=1 || fields[0].RawValue.Length!=4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue)!=ShaderObjectId)
            throw new InvalidDataException("ViewSDF occlusion shader field differs from source CPU+20.");
        var provider=ModelShaderResources.ReadDeclaredShader(shaderObjects,ShaderObjectId);
        if(provider.Object.ClassId!=ShaderClassId || provider.DependencyObjectHashes.Count!=0 ||
            provider.ShaderFileHash!=ShaderFileId || shader.Source.FileId!=ShaderFileId ||
            shader.Source.DecodedSha256!=ShaderSha256 || Hash(shader.Source.Bytes)!=ShaderSha256)
            throw new InvalidDataException("SDFOCC source provider/file/complete bytes differ.");
        var tables=shader.ReadG1Bindings();
        var dimensions=explicitSevenDimensions.ToArray();
        var row=tables.ResolveCombination(dimensions,explicitPass);
        var active=row.Stages.Where(s=>s.Entry is not null).ToArray();
        if(active.Length!=2 || active.SingleOrDefault(s=>s.StageCode==1)?.Entry?.ProgramIndex!=0 ||
            active.SingleOrDefault(s=>s.StageCode==0)?.Entry?.ProgramIndex!=1 ||
            Hash(shader.Container.GetProgramBytes(shader.Source.Bytes,0))!=VertexSha256 ||
            Hash(shader.Container.GetProgramBytes(shader.Source.Bytes,1))!=PixelSha256)
            throw new InvalidDataException("Explicit SDF final row is not the proved VS0/PS1 graphics pair.");
        var rdef=shader.ReadResourceDefinitions(1);var signatures=shader.ReadSignatures(1);
        var cb=rdef.Buffers.Single();
        if(cb.Name.Text!="$Globals" || cb.ByteSize!=64 || cb.Variables.Count!=1 ||
            cb.Variables[0].Name.Text!="mV2W" || cb.Variables[0].StartOffset!=0 || cb.Variables[0].ByteSize!=64 ||
            rdef.Bindings.Count!=8 || !rdef.Bindings.Where(b=>b.InputType==2).Select(b=>(b.Name.Text,b.BindPoint,b.Dimension,b.ReturnType))
                .SequenceEqual(new[]{("sDepth",0u,4u,5u),("sGBuf0",1u,4u,5u),("sHalfGBuffer0",4u,4u,5u),
                    ("sHalfGBuffer1",5u,4u,5u),("sHalfOcclusion",6u,4u,5u)}) ||
            !rdef.Bindings.Where(b=>b.InputType==3).Select(b=>b.BindPoint).SequenceEqual(new uint[]{0,1}))
            throw new InvalidDataException("SDF final full64-byte matrix/texture/sampler declarations differ.");
        return new(extra,objects[0],fields[0],provider,shader,Array.AsReadOnly(dimensions),explicitPass,
            shader.ReadG1ResourceBindings(row.Index),rdef,signatures);
    }

    public static SdfFinalDecision Select(SdfFinalSelection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(!source.Operation1Present || !source.SelectedShaderReady)
            return new(SdfFinalAction.Preserve,"original-final-operation1-or-shader-unavailable");
        if(source.Interface79==0)return new(SdfFinalAction.RequiresCompute,"original-final-interface79-selects-compute");
        if(!source.IntermediatesCompleted)return new(SdfFinalAction.RequiresIntermediates,"original-final-upstream-intermediates-not-completed");
        return new(SdfFinalAction.Draw,"explicit-completed-intermediates-final-graphics-stage");
    }

    /// <summary>Source D6D02C publication after the caller has completed the
    /// original final method, including its source no-pixels completion paths.
    /// Do not publish it merely for a skipped standalone-stage API call.</summary>
    public static uint AfterCompletedFinalMethod(uint explicitCpuState40)=>explicitCpuState40&0xBFFFFFFF;

    /// <summary>EA8F63..EA8F7F: only a ready provider with unsigned View level
    /// at least the packed threshold calls D725A0 with1. Failed gates enter
    /// fallback and do not write0 to the retained interface79 byte.</summary>
    public static SdfGraphicsGateDecision GraphicsGate(bool explicitProviderD61120Ready,
        uint explicitView158C,uint explicitPackedProcess30)
    {
        uint threshold=(explicitPackedProcess30>>8)&3;
        bool active=explicitProviderD61120Ready && explicitView158C>=threshold;
        return new(active,threshold,active?(byte)1:null);
    }

    /// <summary>The original RDEF matrix is64B; SHEX declares and uses only
    /// its first three float4 rows. vkd3d exposes a48B minimum. Retain the full
    /// source64B upload while proving this exact prefix layout; do not weaken
    /// the generic binding reader or alter source RDEF/SPIR-V bytes.</summary>
    public static TranslatedShaderBindings ReadTranslatedPixelBindings(SdfFinalProgramSource source,ReadOnlySpan<byte> spirv)
    {
        ArgumentNullException.ThrowIfNull(source);
        var module=SpirvShaderInterfaceReader.Read(spirv);
        if(module.EntryPoints.Count!=1 || module.EntryPoints[0].ExecutionModel!=4 || module.Descriptors.Count!=8 ||
            module.EntryPoints[0].Name!="main" || !module.ExecutionModes.Any(m=>m.Mode==7))
            throw new InvalidDataException("SDF final translated fragment interface differs.");
        var rows=new List<TranslatedShaderBinding>();
        var matched=new HashSet<(uint,uint)>();
        foreach(var v in module.Descriptors)
        {
            uint type,register;string kind;
            if(v.Name=="cb0_0"){type=0;register=0;kind="UniformBuffer";}
            else if(v.Name is "s0" or "s1"){type=3;register=uint.Parse(v.Name[1..]);kind="Sampler";}
            else if(v.Name is "t0" or "t1" or "t4" or "t5" or "t6"){type=2;register=uint.Parse(v.Name[1..]);kind="Texture";}
            else throw new InvalidDataException("Unexpected SDF final descriptor register.");
            if(v.DescriptorSet!=0 || v.Binding is null || !matched.Add((type,register)))
                throw new InvalidDataException("SDF final descriptors must uniquely use fragment set0.");
            var b=source.PixelDefinitions.Bindings.Single(b=>b.InputType==type && b.BindPoint==register);
            DxbcRdefBuffer? cb=null;
            if(type==0)
            {
                cb=source.PixelDefinitions.Buffers.Single();var members=v.Type.Members;
                if(v.StorageClass!=2 || v.Type.Kind!="struct" || !v.Type.Block || v.RequiredBufferBytes!=48 ||
                    members is null || members.Count!=1 || members[0].Offset!=0 || members[0].Type is not
                    {Kind:"array",Count:3,ArrayStride:16,ElementType:{Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}}} || cb.ByteSize!=64)
                    throw new InvalidDataException("SDF final64-byte source CB differs from the proved48-byte read prefix.");
            }
            else if(type==3)
            {if(v.StorageClass!=0 || v.Type.Kind!="sampler")throw new InvalidDataException("SDF final sampler interface differs.");}
            else if(v.StorageClass!=0 || v.Type is not {Kind:"image",Dimension:1,Arrayed:false,Multisampled:false,SampledCode:1,ImageFormat:0,
                ComponentType:{Kind:"float",Bits:32}})throw new InvalidDataException("SDF final 2D float image interface differs.");
            rows.Add(new(v,b,cb,kind));
        }
        return new(module,source.PixelDefinitions,rows.AsReadOnly());
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
}

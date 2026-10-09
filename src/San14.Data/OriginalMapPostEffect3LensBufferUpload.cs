using San14.Data.Formats;

namespace San14.Data;

/// <summary>Immutable complete byte capacity produced by native Lens Init's
/// actual memset and bounded array copy. This is not an indirect draw packet.</summary>
public sealed class OriginalMapPostEffect3LensBufferUpload
{
    private readonly byte[] bytes;
    internal OriginalMapPostEffect3LensBufferUpload(OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3ParameterLensFlareInitialization init,OriginalMapPostEffect3BufferAllocation declaration,byte[] bytes)
    {SourceSha256=allocation.Source.Sha256;OwnerIdentity=allocation.ContextOwnerIdentity;FrameNumber=init.FrameNumber;
     Declaration=declaration;Locale=init.Locale;this.bytes=bytes.ToArray();SourceEvidence=init.SourceEvidence+"-map;-zero-capacity;-copy-source-arrays";}
    public OriginalMapPostEffect3BufferAllocation Declaration{get;}
    public string SourceSha256{get;}
    public string OwnerIdentity{get;}
    public long FrameNumber{get;}
    public string Locale{get;}
    public OriginalMapPostEffect3BufferWriter Writer=>OriginalMapPostEffect3BufferWriter.LensInitialization;
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
    public string SourceEvidence{get;}
}

public sealed record OriginalMapPostEffect3BufferBindingRequirement(int PassIndex,int SourceRowIndex,int ParameterIndex,
    uint StageCode,int RegisterIndex,OriginalMapPostEffect3BindingRole Role,
    OriginalMapPostEffect3Program Program,DxbcRdefBinding? ShaderDeclaration,
    OriginalMapPostEffect3BufferShape Shape)
{
    public bool ShaderDeclaresRegister=>ShaderDeclaration is not null;
    public bool RequiresCounterAdapter=>ShaderDeclaration?.InputType is 9 or 10 or 11;
    public bool RequiresIndirectArgumentAdapter=>(Shape.MiscFlags&0x10)!=0;
}

public static class OriginalMapPostEffect3LensBufferUploads
{
    public static IReadOnlyList<OriginalMapPostEffect3LensBufferUpload> Prepare(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3ParameterLensFlareInitialization init)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(init);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(init.SourceSha256!=allocation.Source.Sha256||init.OwnerIdentity!=allocation.ContextOwnerIdentity||init.FrameNumber<=0||
            init.BufferWrites.Count!=2)throw new InvalidDataException("Lens buffer initialization source/frame/owner differs.");
        byte[] source=OriginalMapPostEffect3LensPresetBuiltin0.CopyPrimitive();
        var result=new List<OriginalMapPostEffect3LensBufferUpload>();
        foreach(var row in new[]{(Name:"LensGroup",Offset:0x90,Count:4,Stride:16),(Name:"SimpleGhost",Offset:0xD0,Count:16,Stride:28)})
        {
            var declarations=allocation.BufferAllocations.Where(a=>a.Parameter.NameHash==OriginalMapPostEffect3ParameterState.PrimaryNameHash(row.Name)).ToArray();
            if(declarations.Length!=1)throw new InvalidDataException("Original Lens buffer declaration missing: "+row.Name);
            var declaration=declarations[0];var shape=OriginalMapPostEffect3AllocationBufferShapeReader.Read(declaration);
            if(declaration.Parameter.Ownership!=OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration||
                shape.Dimension!=OriginalMapPostEffect3BufferDimension.Structured||shape.StructureByteStride!=row.Stride||
                shape.ByteCount!=row.Count*row.Stride||!shape.CreatesShaderResourceView||shape.CreatesUnorderedAccessView)
                throw new InvalidDataException("Original Lens CPU-upload buffer shape differs.");
            var writes=init.BufferWrites.Where(w=>w.ParameterIndex==declaration.Parameter.Index).ToArray();
            byte[] expected=new byte[checked((int)shape.ByteCount)];source.AsSpan(row.Offset,row.Count*row.Stride).CopyTo(expected);
            if(writes.Length!=1||!writes[0].Bytes.Span.SequenceEqual(expected)||string.IsNullOrWhiteSpace(writes[0].SourceEvidence))
                throw new InvalidDataException("Complete Lens buffer bytes differ from the original builtin array writer.");
            result.Add(new(allocation,init,declaration,expected));
        }
        return result.AsReadOnly();
    }

    /// <summary>Ordered native buffer bindings, including actual setters whose
    /// selected stage has no descriptor. RDEF establishes role/type/register;
    /// the device must still validate the selected translated descriptor.</summary>
    public static IReadOnlyList<OriginalMapPostEffect3BufferBindingRequirement> ReadBindings(
        OriginalMapPostEffect3AllocationPlan allocation,int passIndex)
    {
        ArgumentNullException.ThrowIfNull(allocation);OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if((uint)passIndex>=allocation.Passes.Count)throw new ArgumentOutOfRangeException(nameof(passIndex));
        var links=allocation.Passes[passIndex];var result=new List<OriginalMapPostEffect3BufferBindingRequirement>();
        foreach(var row in links.ActiveBindings.Where(r=>r.Table==OriginalMapPostEffect3BindingTable.BufferParameters))
        foreach(var stage in links.Pass.ProgramReferences.Where(s=>s.StageIndex is not null&&
            (row.StageMask&StageBit(s.StageCode))!=0))
        {
            if(row.MipSelector!=-1||row.SliceSelector!=-1||row.Role is not (OriginalMapPostEffect3BindingRole.ShaderResourceRead or OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite))
                throw new NotSupportedException("Native buffer row needs an explicit view/role adapter.");
            var program=allocation.Source.GetProgram(stage.StageCode,stage.StageIndex!.Value);
            var rdef=DxbcResourceDefinitionReader.Read(program.Bytes.Span);
            bool Match(uint type)=>row.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead?type is 2 or 5 or 7:type is 4 or 6 or 8 or 9 or 10 or 11;
            var candidates=rdef.Bindings.Where(b=>Match(b.InputType)&&b.BindPoint==row.RegisterIndex).ToArray();
            if(candidates.Length>1||candidates.Any(b=>b.Dimension!=1||b.BindCount!=1))
                throw new InvalidDataException("Native buffer register has an ambiguous or non-buffer source declaration.");
            var shape=OriginalMapPostEffect3AllocationBufferShapeReader.Read(allocation.BufferAllocations.Single(a=>a.Parameter.Index==row.ParameterIndex));
            result.Add(new(passIndex,row.SourceRowIndex,row.ParameterIndex,stage.StageCode,row.RegisterIndex,row.Role,
                program,candidates.SingleOrDefault(),shape));
        }
        return result.AsReadOnly();
    }
    private static byte StageBit(uint stage)=>stage switch{5=>1,1=>2,0=>4,2=>8,3=>16,4=>32,_=>throw new NotSupportedException()};
}

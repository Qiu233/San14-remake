namespace San14.Data;

/// <summary>One owned native CB image. A write establishes only its explicit
/// bytes; allocation and unread padding establish no original values.</summary>
public sealed class OriginalMapPostEffect3ConstantBufferImage
{
    private readonly byte[] bytes;
    private readonly bool[] known;
    private readonly OriginalMapPostEffect3AllocationPlan allocation;
    public OriginalMapPostEffect3ConstantBufferImage(OriginalMapPostEffect3AllocationPlan allocation,
        int parameterIndex,long frameNumber)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        if(frameNumber<0||(uint)parameterIndex>=allocation.ConstantBufferParameters.Count)
            throw new ArgumentOutOfRangeException(nameof(parameterIndex));
        this.allocation=allocation;Declaration=allocation.ConstantBufferParameters[parameterIndex];
        FrameNumber=frameNumber;bytes=new byte[Declaration.ByteCount];known=new bool[bytes.Length];
    }
    public OriginalMapPostEffect3ConstantBufferParameter Declaration{get;}
    public long FrameNumber{get;}
    public string OwnerIdentity=>allocation.ContextOwnerIdentity;
    public string SourceSha256=>allocation.Source.Sha256;
    public int KnownByteCount=>known.Count(v=>v);

    /// <summary>The caller supplies an actual recovered writer and its exact
    /// current output. Native later writes replace earlier values in order.</summary>
    public void Write(int offset,ReadOnlySpan<byte> value,OriginalMapPostEffect3ConstantBufferWriter writer,string sourceEvidence)
    {
        if(string.IsNullOrWhiteSpace(sourceEvidence))
            throw new InvalidDataException("CB bytes require an explicit recovered source writer.");
        if(offset<0||value.Length==0||(long)offset+value.Length>bytes.Length)
            throw new InvalidDataException("CB writer exceeds its native allocation.");
        value.CopyTo(bytes.AsSpan(offset));Array.Fill(known,true,offset,value.Length);
    }

    /// <summary>Only selected-program active variables require known source
    /// bytes. Unobserved bytes are zero in this backend representation and
    /// remain marked unknown; they are never reported as native zero values.</summary>
    public OriginalMapPostEffect3ConstantBufferUpload PrepareForPass(
        OriginalMapPostEffect3AllocationPlan exactAllocation,int passIndex,long frameNumber)
    {
        if(!ReferenceEquals(allocation,exactAllocation)||frameNumber!=FrameNumber)
            throw new InvalidDataException("CB upload belongs to another allocation or frame.");
        var requirements=OriginalMapPostEffect3ParameterBindingRequirements.Read(allocation,passIndex)
            .Where(r=>r.ParameterIndex==Declaration.Index).ToArray();
        if(requirements.Length==0)
            throw new InvalidDataException("Selected pass does not bind this native CB.");
        foreach(var requirement in requirements)
            foreach(var variable in requirement.UsedVariables)
                for(int i=variable.Offset;i<variable.Offset+variable.ByteCount;i++)
                    if(!known[i])throw new NotSupportedException(
                        $"Pass {passIndex} stage {requirement.StageCode} CB {Declaration.Index} reads unknown {variable.Name} byte {i}.");
        return new(Declaration,FrameNumber,OwnerIdentity,SourceSha256,passIndex,bytes,known,requirements);
    }
}

/// <summary>Frozen backend bytes plus their native knowledge mask and exact
/// ordered selected-stage requirements. This proves no draw or GPU upload.</summary>
public sealed class OriginalMapPostEffect3ConstantBufferUpload
{
    private readonly byte[] bytes;
    private readonly bool[] known;
    internal OriginalMapPostEffect3ConstantBufferUpload(OriginalMapPostEffect3ConstantBufferParameter declaration,
        long frameNumber,string owner,string source,int passIndex,byte[] bytes,bool[] known,
        OriginalMapPostEffect3ParameterBindingRequirement[] requirements)
    {
        Declaration=declaration;FrameNumber=frameNumber;OwnerIdentity=owner;SourceSha256=source;
        PassIndex=passIndex;this.bytes=bytes.ToArray();this.known=known.ToArray();
        Requirements=Array.AsReadOnly(requirements);
    }
    public OriginalMapPostEffect3ConstantBufferParameter Declaration{get;}
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}
    public string SourceSha256{get;}
    public int PassIndex{get;}
    public ReadOnlyMemory<byte> BackendBytes=>bytes.ToArray();
    public ReadOnlyMemory<bool> KnownSourceBytes=>known.ToArray();
    public IReadOnlyList<OriginalMapPostEffect3ParameterBindingRequirement> Requirements{get;}
    public bool AllNativeBytesKnown=>known.All(v=>v);
}

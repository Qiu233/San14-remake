using System.Buffers.Binary;

namespace San14.Data;

public enum MapPostEffect3AllocationOwnership { ExternalParameterSlot, OwnedDeclaration }
public enum MapPostEffect3BindingRole { ShaderResourceRead, UnorderedAccessReadWrite, RenderTargetWrite, ConstantBufferRead }
public enum MapPostEffect3BindingTable { TextureParameters, BufferParameters, ConstantBufferParameters }

public sealed record MapPostEffect3TextureParameter(int Index,uint NameHash,short NativeKind,ushort NativeFlags,
    sbyte WidthMode,sbyte HeightMode,uint WidthWord,uint HeightWord,uint RawValue14,
    uint NativeFormat,sbyte RawSignedByte1C,uint RawValue20,ReadOnlyMemory<byte> RawBytes)
{
    public MapPostEffect3AllocationOwnership Ownership=>(NativeFlags&4)!=0?
        MapPostEffect3AllocationOwnership.OwnedDeclaration:MapPostEffect3AllocationOwnership.ExternalParameterSlot;
    public bool HasTwoRuntimeInstances=>(NativeFlags&0x40)!=0;
}
public sealed record MapPostEffect3BufferParameter(int Index,uint NameHash,uint NativeKind,ushort NativeFlags,
    byte WidthMode,byte HeightMode,uint WidthWord,uint HeightWord,uint NativeFormatOrStride,
    uint RawValue1C,uint RawValue20,ReadOnlyMemory<byte> RawBytes)
{
    public MapPostEffect3AllocationOwnership Ownership=>(NativeFlags&1)!=0?
        MapPostEffect3AllocationOwnership.OwnedDeclaration:MapPostEffect3AllocationOwnership.ExternalParameterSlot;
    public bool HasTwoRuntimeInstances=>(NativeFlags&8)!=0;
}
/// <summary>Exact fields consumed by 's allocator.v20/v28 request.
/// Unnamed native request fields remain raw; this does not guess texture kind,
/// array/mip semantics or native resource handles from matching shape.</summary>
public sealed record MapPostEffect3TextureAllocation(MapPostEffect3TextureParameter Parameter,
    uint Width,uint Height,ushort Width16,ushort Height16,uint NativeCreateSlot,int InstanceCount,
    bool CreateInThisInvocation);
public sealed record MapPostEffect3BufferAllocation(MapPostEffect3BufferParameter Parameter,
    uint ByteCount,uint NativeCreateSlot,int InstanceCount,bool CreateInThisInvocation);
public sealed record MapPostEffect3ConstantBufferParameter(int Index,uint NameHash,uint NativeFlags,
    uint ByteCount,ReadOnlyMemory<byte> RawBytes)
{
    public MapPostEffect3AllocationOwnership Ownership=>(NativeFlags&1)!=0?
        MapPostEffect3AllocationOwnership.OwnedDeclaration:MapPostEffect3AllocationOwnership.ExternalParameterSlot;
    public bool HasTwoRuntimeInstances=>(NativeFlags&2)!=0;
}
public sealed record MapPostEffect3ConstantBufferAllocation(MapPostEffect3ConstantBufferParameter Parameter,
    uint NativeCreateSlot,int InstanceCount,bool CreateInThisInvocation);
public sealed record MapPostEffect3Binding(int SourceRowIndex,MapPostEffect3BindingTable Table,
    int ParameterIndex,short NativeViewKind,short RegisterIndex,sbyte MipSelector,sbyte SliceSelector,
    byte StageMask,MapPostEffect3BindingRole Role,short RawFlags0,ReadOnlyMemory<byte> RawBytes);
public sealed record MapPostEffect3BindingSpan(int PassFieldOffset,int FirstRow,int RowCount,
    IReadOnlyList<MapPostEffect3Binding> Bindings);
public sealed record MapPostEffect3DispatchExtent(uint X,uint Y,uint Z);
public sealed record MapPostEffect3PassAllocationLinks(MapPostEffect3Pass Pass,
    IReadOnlyList<MapPostEffect3BindingSpan> Spans,MapPostEffect3DispatchExtent? DispatchExtent)
{
    // .. prefers CS when present, otherwise requires PS and
    // selects the remaining five native stage bits. It does not infer a VS.
    public byte NativeActiveStageMask=>Pass.ProgramReferences.Any(p=>p.StageCode==5&&p.StageIndex is not null)?(byte)1:
        Pass.ProgramReferences.Any(p=>p.StageCode==0&&p.StageIndex is not null)?(byte)62:(byte)0;
    public IEnumerable<MapPostEffect3Binding> ActiveBindings=>Spans.SelectMany(s=>s.Bindings)
        .Where(b=>(b.StageMask&NativeActiveStageMask)!=0);
    public IEnumerable<MapPostEffect3Binding> ResourceInputs=>Spans.SelectMany(s=>s.Bindings)
        .Where(b=>b.Role is MapPostEffect3BindingRole.ShaderResourceRead or MapPostEffect3BindingRole.UnorderedAccessReadWrite or MapPostEffect3BindingRole.ConstantBufferRead);
    public IEnumerable<MapPostEffect3Binding> ResourceOutputs=>Spans.SelectMany(s=>s.Bindings)
        .Where(b=>b.Role is MapPostEffect3BindingRole.RenderTargetWrite or MapPostEffect3BindingRole.UnorderedAccessReadWrite);
}

public sealed class MapPostEffect3AllocationPlan
{
    internal MapPostEffect3AllocationPlan(MapPostEffect3Container source,uint width,uint height,string owner,
        bool resizeOnly,IReadOnlyList<MapPostEffect3TextureParameter> textures,
        IReadOnlyList<MapPostEffect3BufferParameter> buffers,IReadOnlyList<MapPostEffect3ConstantBufferParameter> constantBuffers,
        IReadOnlyList<MapPostEffect3TextureAllocation> textureAllocations,
        IReadOnlyList<MapPostEffect3BufferAllocation> bufferAllocations,IReadOnlyList<MapPostEffect3ConstantBufferAllocation> constantBufferAllocations,
        IReadOnlyList<MapPostEffect3PassAllocationLinks> passes)
    {Source=source;ContextWidth=width;ContextHeight=height;ContextOwnerIdentity=owner;ResizeOnly=resizeOnly;
     TextureParameters=textures;BufferParameters=buffers;ConstantBufferParameters=constantBuffers;TextureAllocations=textureAllocations;
     BufferAllocations=bufferAllocations;ConstantBufferAllocations=constantBufferAllocations;Passes=passes;}
    public MapPostEffect3Container Source{get;}
    public uint ContextWidth{get;private set;}
    public uint ContextHeight{get;private set;}
    public string ContextOwnerIdentity{get;}
    public bool ResizeOnly{get;}
    public IReadOnlyList<MapPostEffect3TextureParameter> TextureParameters{get;}
    public IReadOnlyList<MapPostEffect3BufferParameter> BufferParameters{get;}
    public IReadOnlyList<MapPostEffect3ConstantBufferParameter> ConstantBufferParameters{get;}
    public IReadOnlyList<MapPostEffect3TextureAllocation> TextureAllocations{get;private set;}
    public IReadOnlyList<MapPostEffect3BufferAllocation> BufferAllocations{get;private set;}
    public IReadOnlyList<MapPostEffect3ConstantBufferAllocation> ConstantBufferAllocations{get;}
    public IReadOnlyList<MapPostEffect3PassAllocationLinks> Passes{get;private set;}
    /// <summary>Publish a completed native resize-only allocation into the
    /// same module owner. Declarations, selectors, fixed resources and CBs
    /// retain their identity; physical dimensions and dispatch extents change.</summary>
    public void InstallCompletedOwnedResize(MapPostEffect3AllocationPlan resize)
    {
        ArgumentNullException.ThrowIfNull(resize);
        if(ResizeOnly||!resize.ResizeOnly||!ReferenceEquals(Source,resize.Source)||
            ContextOwnerIdentity!=resize.ContextOwnerIdentity)
            throw new InvalidDataException("Resize must belong to the same actual loaded module and owner.");
        ContextWidth=resize.ContextWidth;ContextHeight=resize.ContextHeight;
        TextureAllocations=Array.AsReadOnly(resize.TextureAllocations.Select(a=>a with{CreateInThisInvocation=true}).ToArray());
        BufferAllocations=Array.AsReadOnly(resize.BufferAllocations.Select(a=>a with{CreateInThisInvocation=true}).ToArray());
        Passes=resize.Passes;
    }
    public bool IsCompleteForExecution=>false;
    public string SourceEvidence=>"TC;v48;source-counted-3SPK";
    public IReadOnlyList<MapPostEffect3PassAllocationLinks> ReadSourceFunctorOrder(int explicitFunctorIndex)
    {
        var rows=Source.ReadFunctorPasses(explicitFunctorIndex);
        return Array.AsReadOnly(rows.Select(p=>Passes[p.Index]).ToArray());
    }
}

/// <summary>Native declarations and consumer-proven pass links. Allocation
/// requests are owned-context inputs, not observations of original GPU state.
/// Does not choose conditional passes, evaluate Apply parameters or execute GPU.</summary>
public static class MapPostEffect3AllocationReader
{
    public static MapPostEffect3AllocationPlan Read(MapPostEffect3Container source,
        uint contextWidth,uint contextHeight,string contextOwnerIdentity,bool resizeOnly=false)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentException.ThrowIfNullOrWhiteSpace(contextOwnerIdentity);
        if(contextWidth==0||contextHeight==0||contextWidth>65535||contextHeight>65535)
        throw new NotSupportedException("3SPK owned context exceeds the supported positive native 16-bit extent.");
        var textureTable=source.Tables.Single(t=>t.ChunkKind==2);var bufferTable=source.Tables.Single(t=>t.ChunkKind==3);
        var constantBufferTable=source.Tables.Single(t=>t.ChunkKind==4);var bindingTable=source.Tables.Single(t=>t.ChunkKind==6);
        var textures=new List<MapPostEffect3TextureParameter>();var buffers=new List<MapPostEffect3BufferParameter>();
        var textureAllocations=new List<MapPostEffect3TextureAllocation>();var bufferAllocations=new List<MapPostEffect3BufferAllocation>();
        var constantBuffers=new List<MapPostEffect3ConstantBufferParameter>();
        var constantBufferAllocations=new List<MapPostEffect3ConstantBufferAllocation>();
        for(int i=0;i<textureTable.Rows.Count;i++)
        {
            var raw=textureTable.Rows[i];var b=raw.Span;
            var p=new MapPostEffect3TextureParameter(i,U(b,4),S(b,0x16),H(b,0xA),(sbyte)b[8],(sbyte)b[9],
                U(b,0xC),U(b,0x10),U(b,0x14),U(b,0x18),(sbyte)b[0x1C],U(b,0x20),raw);textures.Add(p);
            if(p.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration)continue;
            uint slot=p.NativeKind switch{0=>0x20u,1=>0x28u,_=>throw new NotSupportedException("3SPK texture allocator kind is unsupported.")};
            uint w=ResolveTextureDimension(p.WidthMode,p.WidthWord,contextWidth),h=ResolveTextureDimension(p.HeightMode,p.HeightWord,contextHeight);
            if(w==0||h==0||w>65535||h>65535)throw new NotSupportedException("3SPK computed texture extent exceeds positive native 16-bit storage.");
            textureAllocations.Add(new(p,w,h,(ushort)w,(ushort)h,slot,p.HasTwoRuntimeInstances?2:1,
                !resizeOnly||p.WidthMode!=0||p.HeightMode!=0));
        }
        for(int i=0;i<bufferTable.Rows.Count;i++)
        {
            var raw=bufferTable.Rows[i];var b=raw.Span;
            var p=new MapPostEffect3BufferParameter(i,U(b,4),U(b,8),H(b,0xE),b[0xC],b[0xD],U(b,0x10),U(b,0x14),
                U(b,0x18),U(b,0x1C),U(b,0x20),raw);buffers.Add(p);
            if(p.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration)continue;
            uint slot=p.NativeKind switch{0=>0x30u,1=>0x38u,2=>0x40u,_=>throw new NotSupportedException("3SPK buffer allocator kind is unsupported.")};
            uint n=ResolveBufferByteCount(p,contextWidth,contextHeight);
            if(n==0)throw new NotSupportedException("3SPK zero-byte owned buffer is outside the supported allocation range.");
            bufferAllocations.Add(new(p,n,slot,p.HasTwoRuntimeInstances?2:1,!resizeOnly||p.WidthMode!=0||p.HeightMode!=0));
        }
        for(int i=0;i<constantBufferTable.Rows.Count;i++)
        {
            var raw=constantBufferTable.Rows[i];var p=new MapPostEffect3ConstantBufferParameter(i,U(raw.Span,4),U(raw.Span,8),U(raw.Span,0xC),raw);
            constantBuffers.Add(p);
            if(p.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration)continue;
            if(p.ByteCount==0||p.ByteCount>int.MaxValue)throw new NotSupportedException("3SPK constant buffer byte count is outside supported owned storage.");
            // skips this entire allocation loop during resize-only.
            // allocator v48 is -> (CPU constant-buffer owner),
            // not v40's -> generic GPU buffer.
            constantBufferAllocations.Add(new(p,0x48,p.HasTwoRuntimeInstances?2:1,!resizeOnly));
        }
        var passes=new List<MapPostEffect3PassAllocationLinks>();
        foreach(var pass in source.Passes)
        {
            var spans=new List<MapPostEffect3BindingSpan>();
            for(int group=0;group<5;group++)
            {
                int field=0x18+group*4,first=S(pass.RawBytes.Span,field),count=S(pass.RawBytes.Span,field+2);
                if(first<0||count<0||(long)first+count>bindingTable.Rows.Count)throw Error("pass binding span exceeds its native counted table");
                if(group is 1 or 3&&count!=0)throw new NotSupportedException("3SPK populated binding spans +1C/+24 are not yet consumer-proven.");
                var bindings=new List<MapPostEffect3Binding>();
                for(int j=0;j<count;j++)
                {
                    var raw=bindingTable.Rows[first+j];var b=raw.Span;
                    int param=S(b,2);short kind=S(b,4),register=S(b,6);byte stages=b[9];
                    MapPostEffect3BindingTable table=group switch{0=>MapPostEffect3BindingTable.TextureParameters,
                        2=>MapPostEffect3BindingTable.BufferParameters,4=>MapPostEffect3BindingTable.ConstantBufferParameters,
                        _=>throw new NotSupportedException("3SPK binding consumer is unsupported.")};
                    int tableCount=group==0?textures.Count:group==2?buffers.Count:constantBuffers.Count;
                    if(param<0||param>=tableCount||register<0||(stages&~63)!=0)throw Error("binding parameter/register/stage exceeds its source table");
                    if(group==4&&kind!=2)throw new NotSupportedException("3SPK constant-buffer native binding kind is unsupported.");
                    MapPostEffect3BindingRole role=group==4?MapPostEffect3BindingRole.ConstantBufferRead:kind switch{
                        0=>MapPostEffect3BindingRole.UnorderedAccessReadWrite,
                        1=>MapPostEffect3BindingRole.ShaderResourceRead,
                        3 when group==0=>MapPostEffect3BindingRole.RenderTargetWrite,
                        _=>throw new NotSupportedException("3SPK binding view kind is outside the consumer-proven subset.")};
                    bindings.Add(new(first+j,table,param,kind,register,(sbyte)b[8],(sbyte)b[10],stages,role,S(b,0),raw));
                }
                spans.Add(new(field,first,count,bindings.AsReadOnly()));
            }
            MapPostEffect3DispatchExtent? dispatch=null;
            if(pass.ProgramReferences.Any(p=>p.StageCode==5&&p.StageIndex is not null))
                dispatch=ResolveDispatch(pass.RawBytes.Span,contextWidth,contextHeight);
            passes.Add(new(pass,spans.AsReadOnly(),dispatch));
        }
        return new(source,contextWidth,contextHeight,contextOwnerIdentity,resizeOnly,textures.AsReadOnly(),buffers.AsReadOnly(),constantBuffers.AsReadOnly(),
            textureAllocations.AsReadOnly(),bufferAllocations.AsReadOnly(),constantBufferAllocations.AsReadOnly(),passes.AsReadOnly());
    }

    public static uint ResolveTextureDimension(sbyte mode,uint operand,uint callerExtent)
    {
        if(mode==0)return operand;
        if(mode is not(1 or 2))throw new NotSupportedException("3SPK texture dimension mode is unsupported.");
        float value=BitConverter.UInt32BitsToSingle(operand);
        float extent=unchecked((int)callerExtent);float result=mode==1?extent*value:extent/value;
        // : CVTDQ2PS -> MULSS/DIVSS -> CVTPS2PD -> ceil -> CVTTSD2SI.
        return PositiveInt32(Math.Ceiling((double)result),"texture extent");
    }
    public static uint ResolveBufferByteCount(MapPostEffect3BufferParameter p,uint width,uint height)
    {
        ArgumentNullException.ThrowIfNull(p);
        if((p.NativeFlags&2)!=0)return p.WidthWord;
        uint factor=p.NativeKind switch{0=>p.NativeFormatOrStride switch{
            6=>8u,24=>4u,_=>throw new NotSupportedException("3SPK native typed-buffer stride is outside the two source-proven installed codes.")},
            1 or 2=>p.NativeFormatOrStride,_=>throw new NotSupportedException("3SPK buffer kind is unsupported.")};
        // Native arithmetic is uint32 IMUL; preserve wrap instead of widening.
        if(p.WidthMode==255)return unchecked(p.WidthWord*factor);
        uint w=ResolveBufferDimension(p.WidthMode,p.WidthWord,width),h=ResolveBufferDimension(p.HeightMode,p.HeightWord,height);
        return unchecked(w*h*factor);
    }
    private static uint ResolveBufferDimension(byte mode,uint operand,uint callerExtent)
    {
        if(mode==0)return operand;
        if(mode is not(1 or 2))throw new NotSupportedException("3SPK buffer dimension mode is unsupported.");
        float scalar=BitConverter.UInt32BitsToSingle(operand),extent=unchecked((int)callerExtent);
        return PositiveInt32(Math.Truncate(mode==1?extent*scalar:extent/scalar),"buffer extent");
    }
    private static MapPostEffect3DispatchExtent ResolveDispatch(ReadOnlySpan<byte> row,uint width,uint height)
    {
        uint x=U(row,0xC),y=U(row,0x10),z=U(row,0x14);byte mode=row[0x3A];
        if(mode==1)
        {
            if(x==0||y==0)throw Error("compute dispatch divisor is zero");
            x=width/x+(width%x!=0?1u:0u);y=height/y+(height%y!=0?1u:0u);
        }
        else if(mode!=0)throw new NotSupportedException("3SPK compute dispatch dimension mode is unsupported.");
        return new(x,y,z);
    }
    private static uint PositiveInt32(double value,string name)
    {if(!double.IsFinite(value)||value<0||value>int.MaxValue)throw new NotSupportedException($"3SPK {name} lies outside supported native signed conversion.");return (uint)value;}
    private static uint U(ReadOnlySpan<byte>b,int i)=>BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(i,4));
    private static ushort H(ReadOnlySpan<byte>b,int i)=>BinaryPrimitives.ReadUInt16LittleEndian(b.Slice(i,2));
    private static short S(ReadOnlySpan<byte>b,int i)=>BinaryPrimitives.ReadInt16LittleEndian(b.Slice(i,2));
    private static InvalidDataException Error(string message)=>new($"3SPK allocation/link: {message}.");
}

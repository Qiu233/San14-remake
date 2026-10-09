using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Native initializes three identity matrix histories and
/// byteD0=1. These are handler fields, not globally initialized scalar values.</summary>
public sealed record MapPostEffect3ParameterTemporalAAHistory
{
    private MapPostEffect3ParameterTemporalAAHistory(string source,string owner,long frame,
        Matrix4x4 current,Matrix4x4 previous,Matrix4x4 older,bool enabled)
    {SourceSha256=source;OwnerIdentity=owner;FrameNumber=frame;Current10=current;Previous50=previous;Older90=older;EnabledD0=enabled;}
    public string SourceSha256{get;}
    public string OwnerIdentity{get;}
    public long FrameNumber{get;}
    public Matrix4x4 Current10{get;}
    public Matrix4x4 Previous50{get;}
    public Matrix4x4 Older90{get;}
    public bool EnabledD0{get;}
    public static MapPostEffect3ParameterTemporalAAHistory ConstructFresh(
        MapPostEffect3Container source,string ownerIdentity)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentException.ThrowIfNullOrWhiteSpace(ownerIdentity);
        MapPostEffect3ParameterState.ValidateSource(source);
        return new(source.Sha256,ownerIdentity,0,Matrix4x4.Identity,Matrix4x4.Identity,Matrix4x4.Identity,true);
    }
    internal MapPostEffect3ParameterTemporalAAHistory Next(long frame,Matrix4x4 current,bool enabled)
        =>enabled?new(SourceSha256,OwnerIdentity,frame,current,Current10,Previous50,true):
            new(SourceSha256,OwnerIdentity,frame,Current10,Previous50,Older90,false);
}

/// <summary>Only the source CB update. Disabled1150603 takes the separate named
/// texture alias route and writes no CB/history matrices. Neither result proves
/// the complete handler AL result, resource copy, pass choice or GPU execution.</summary>
public sealed record MapPostEffect3ParameterTemporalAAUpdate(long FrameNumber,
    MapPostEffect3ParameterTemporalAAHistory NextHistory,
    MapPostEffect3ParameterConstantBuffer? ConstantBuffer)
{
    public bool RequiresDisabledTextureAlias=>ConstantBuffer is null;
}

public static class MapPostEffect3ParameterTemporalAA
{
    public static MapPostEffect3ParameterTemporalAAUpdate Prepare(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3ParameterTemporalAAHistory history,int actualDescriptorWidth40)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(history);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(state.SourceSha256!=allocation.Source.Sha256||history.SourceSha256!=state.SourceSha256||
           state.OwnerIdentity!=allocation.ContextOwnerIdentity||history.OwnerIdentity!=state.OwnerIdentity||
           state.FrameNumber<=history.FrameNumber||actualDescriptorWidth40<=0||
           actualDescriptorWidth40!=allocation.ContextWidth)
            throw new InvalidDataException("3SPK TemporalAA scalar/allocation/history/descriptor owners differ or are stale.");
        bool enabled=history.EnabledD0;
        if(state.FindScalarIndex("TemporalAAEnable") is not null)
        {
            var value=state.RequireValue("TemporalAAEnable");
            if(value.Length!=4)throw new InvalidDataException("3SPK TemporalAAEnable extent differs.");
            enabled=value.Span[0]!=0; // reads a byte, not signed-positive int.
        }
        if(!enabled)return new(state.FrameNumber,history.Next(state.FrameNumber,history.Current10,false),null);

        Matrix4x4 current=Matrix4x4.Identity;
        if(state.FindScalarIndex("ViewProj") is not null)
        {
            var declaration=state.Declarations[state.FindScalarIndex("ViewProj")!.Value];
            if(declaration.NativeValueType!=5||declaration.ElementCount!=1)
                throw new InvalidDataException("3SPK ViewProj native matrix declaration differs.");
            current=Matrix(state.RequireValue("ViewProj").Span);
        }
        var next=history.Next(state.FrameNumber,current,true);
        float velocity=NativeFloat.Multiply(actualDescriptorWidth40,OptionalFloat(state,"TemporalAAVelocityWeight",.5f));
        float basis=OptionalFloat(state,"TemporalAABaseWeight",BitConverter.UInt32BitsToSingle(0x3DCCCCCD));
        float threshold=OptionalFloat(state,"TemporalAADepthDiffThreshold",BitConverter.UInt32BitsToSingle(0x3D4CCCCD));
        float tonemap=OptionalInt(state,"TemporalAAUseTonemap",1)==1?1:0;
        float autoExposure=OptionalInt(state,"AutoExposureEnable",1)==1?1:0;
        float exposure=OptionalFloat(state,"ExposureValue",1);
        if(tonemap==0)autoExposure=0;
        // uses the source parameter lookup result, not texture readiness.
        bool hasVelocity=allocation.TextureParameters.Any(t=>t.NameHash==
            MapPostEffect3ParameterState.PrimaryNameHash("TAAInputVelocity"))||
            allocation.TextureParameters.Any(t=>t.NameHash==MapPostEffect3ParameterState.PrimaryNameHash("InputVelocity"));
        int? index=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.ConstantBuffer,"plTemporalAAConstants");
        if(index is not int i)throw new NotSupportedException("3SPK selected TemporalAA CB declaration is absent.");
        var cb=allocation.ConstantBufferParameters[i];
        if(cb.ByteCount!=160||cb.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("3SPK native TemporalAA CB capacity differs.");
        byte[] bytes=new byte[160];
        // delegates to the already recovered full SSE3F6EB0 inverse.
        WriteMatrix(bytes,0,NativeMatrixMath.Invert(current,out _));WriteMatrix(bytes,64,next.Previous50);
        Put(bytes,128,velocity);Put(bytes,132,basis);Put(bytes,136,threshold);Put(bytes,140,hasVelocity?1:0);
        Put(bytes,144,tonemap);Put(bytes,148,autoExposure);Put(bytes,152,exposure);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(156),0);
        return new(state.FrameNumber,next,new(state,cb,MapPostEffect3ConstantBufferWriter.TemporalAA,bytes));
    }

    private static float OptionalFloat(MapPostEffect3ParameterState state,string name,float absentFallback)
    {
        int? i=state.FindScalarIndex(name);if(i is null)return absentFallback;
        var d=state.Declarations[i.Value];
        if(d.NativeValueType!=1||d.ElementCount!=1)throw new InvalidDataException("3SPK TemporalAA float declaration differs: "+name);
        return BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(state.RequireValue(name).Span));
    }
    private static int OptionalInt(MapPostEffect3ParameterState state,string name,int absentFallback)
    {
        int? i=state.FindScalarIndex(name);if(i is null)return absentFallback;
        var d=state.Declarations[i.Value];
        if(d.NativeValueType!=6||d.ElementCount!=1)throw new InvalidDataException("3SPK TemporalAA int declaration differs: "+name);
        return BinaryPrimitives.ReadInt32LittleEndian(state.RequireValue(name).Span);
    }
    private static Matrix4x4 Matrix(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length!=64)throw new InvalidDataException("3SPK source matrix byte extent differs.");
        var v=new float[16];for(int i=0;i<16;i++)v[i]=BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(i*4)..]));
        return new(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7],v[8],v[9],v[10],v[11],v[12],v[13],v[14],v[15]);
    }
    private static void WriteMatrix(Span<byte> bytes,int offset,Matrix4x4 m)
    {
        float[] v=[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
        for(int i=0;i<v.Length;i++)Put(bytes,offset+i*4,v[i]);
    }
    private static void Put(Span<byte> bytes,int offset,float value)
        =>BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..],BitConverter.SingleToUInt32Bits(value));
}

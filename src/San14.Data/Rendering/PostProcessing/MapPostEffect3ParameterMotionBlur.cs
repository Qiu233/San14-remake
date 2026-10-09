using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary> initializes enabled10=1, tag14=0 and two identity matrices.
/// The recovered11513C0 initializer never writes tag14; frame progression must
/// not silently replace that native comparison field.</summary>
public sealed record MapPostEffect3ParameterMotionBlurHistory
{
    private MapPostEffect3ParameterMotionBlurHistory(string source,string owner,long frame,
        bool enabled,uint tag,Matrix4x4 previous,Matrix4x4 current)
    {SourceSha256=source;OwnerIdentity=owner;FrameNumber=frame;Enabled10=enabled;
     NativeComparisonTag14=tag;Previous20=previous;Current60=current;}
    public string SourceSha256{get;}
    public string OwnerIdentity{get;}
    public long FrameNumber{get;}
    public bool Enabled10{get;}
    public uint NativeComparisonTag14{get;}
    public Matrix4x4 Previous20{get;}
    public Matrix4x4 Current60{get;}
    public static MapPostEffect3ParameterMotionBlurHistory ConstructFresh(
        MapPostEffect3Container source,string ownerIdentity)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentException.ThrowIfNullOrWhiteSpace(ownerIdentity);
        MapPostEffect3ParameterState.ValidateSource(source);
        return new(source.Sha256,ownerIdentity,0,true,0,Matrix4x4.Identity,Matrix4x4.Identity);
    }
    internal MapPostEffect3ParameterMotionBlurHistory Next(long frame,bool enabled,
        Matrix4x4 previous,Matrix4x4 current)
        =>new(SourceSha256,OwnerIdentity,frame,enabled,NativeComparisonTag14,previous,current);
}

/// <summary>Current native context counter1BF0 and the actual lookup/readiness
/// of MBInputVelocity are caller-owned inputs; declaration presence alone does
/// not establish that its native wrapper is nonnull.</summary>
public sealed record MapPostEffect3ParameterMotionBlurInputs(uint ActualContextCounter1BF0,
    bool ActualMbInputVelocityWrapperPresent,string SourceEvidence);

/// <summary>Complete initializer CB writes and the shared1153F90 gate only.
/// Both enabled and disabled paths publish MBOutputColor by native texture
/// alias; this receipt retains that separate producer requirement. It proves
/// neither alias publication nor pass/resource/GPU completion.</summary>
public sealed record MapPostEffect3ParameterMotionBlurUpdate(long FrameNumber,
    MapPostEffect3ParameterMotionBlurHistory NextHistory,bool InitializationReturnedTrue,
    string OutputTextureAliasSourceName,IReadOnlyList<MapPostEffect3ParameterConstantBuffer> ConstantBuffers)
{
    public bool RequiresOutputTextureAliasProducer=>true;
    public string OutputTextureAliasTargetName=>"MBOutputColor";
    public bool BeforeHookAllowsPasses=>NextHistory.Enabled10;
    public bool RequiresDisabledTextureAlias=>!NextHistory.Enabled10;
}

public static class MapPostEffect3ParameterMotionBlur
{
    /// <summary>Actual factory3 initializer. Required scalar lookup
    /// failures return ALfalse before CB writes; present but unwritten values
    /// reject. ReverseZ is consumed by pointer presence, not value truthiness.</summary>
    public static MapPostEffect3ParameterMotionBlurUpdate Prepare(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3ParameterMotionBlurHistory history,
        MapPostEffect3ParameterMotionBlurInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(history);ArgumentNullException.ThrowIfNull(inputs);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputs.SourceEvidence);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(state.SourceSha256!=allocation.Source.Sha256||history.SourceSha256!=state.SourceSha256||
           state.OwnerIdentity!=allocation.ContextOwnerIdentity||history.OwnerIdentity!=state.OwnerIdentity||
           state.FrameNumber<=history.FrameNumber||allocation.ContextWidth==0||allocation.ContextHeight==0||
           allocation.ContextWidth>int.MaxValue||allocation.ContextHeight>int.MaxValue)
            throw new InvalidDataException("3SPK MotionBlur scalar/allocation/history owners differ or are stale.");
        bool enabled=true; //: missing enable declaration defaults to1.
        if(state.FindScalarIndex("MotionBlurEnable") is not null)
            enabled=ReadInt(state,"MotionBlurEnable")!=0; // Full dword compare1151420.
        string alias=enabled?"MBOutputColorInternal":"MBInputColor";
        MapPostEffect3ParameterMotionBlurUpdate NoWrite(bool initialized)
            =>new(state.FrameNumber,history.Next(state.FrameNumber,enabled,history.Previous20,history.Current60),
                initialized,alias,Array.Empty<MapPostEffect3ParameterConstantBuffer>());
        if(!enabled)return NoWrite(false);
        string[] required=["ShutterSpeed","ReverseZ","DeltaTime","ViewProj","dbStopMB"];
        if(required.Any(name=>state.FindScalarIndex(name) is null))return NoWrite(false);

        float shutter=ReadFloat(state,"ShutterSpeed"),delta=ReadFloat(state,"DeltaTime");
        int stop=ReadInt(state,"dbStopMB");
        var previous=history.Previous20;var current=history.Current60;
        if(stop==0)
        {
            if(history.NativeComparisonTag14!=inputs.ActualContextCounter1BF0)previous=current;
            current=ReadMatrix(state,"ViewProj");
        }
        var next=history.Next(state.FrameNumber,true,previous,current);
        var declarations=new[]{RequireDeclaration(state,allocation,"plMotionBlurConstants",3,160),
            RequireDeclaration(state,allocation,"plMotionBlurScale0",4,16),
            RequireDeclaration(state,allocation,"plMotionBlurScale1",5,16)};
        float width=(int)allocation.ContextWidth,height=(int)allocation.ContextHeight;
        byte[] constants=new byte[160];Put(constants,0,width);Put(constants,4,height);
        float diagonal=MathF.Sqrt(NativeFloat.Add(NativeFloat.Multiply(height,height),
            NativeFloat.Multiply(width,width)));
        Put(constants,8,NativeLogarithm.Divide(diagonal,height));
        Put(constants,12,NativeLogarithm.Divide(width,height));
        //..: lens-infoXYZ=0; W=1 if ReverseZ pointer exists.
        Put(constants,16,0);Put(constants,20,0);Put(constants,24,0);Put(constants,28,1);
        WriteMatrix(constants,32,NativeMatrixMath.Invert(current,out _));
        WriteMatrix(constants,96,previous);
        float scale=NativeLogarithm.Divide(NativeFloat.Multiply(shutter,
            BitConverter.UInt32BitsToSingle(0x3E19999A)),delta);
        float velocity=inputs.ActualMbInputVelocityWrapperPresent?1:0;
        byte[] scale0=new byte[16],scale1=new byte[16];
        Put(scale0,0,scale);Put(scale0,4,BitConverter.UInt32BitsToSingle(0x3DE38E39));
        Put(scale0,8,velocity);Put(scale0,12,0);
        Put(scale1,0,scale);Put(scale1,4,1);Put(scale1,8,velocity);Put(scale1,12,0);
        MapPostEffect3ParameterConstantBuffer[] buffers=[new(state,declarations[0],MapPostEffect3ConstantBufferWriter.MotionBlur,constants),
            new(state,declarations[1],MapPostEffect3ConstantBufferWriter.MotionBlur,scale0),new(state,declarations[2],MapPostEffect3ConstantBufferWriter.MotionBlur,scale1)];
        return new(state.FrameNumber,next,true,alias,Array.AsReadOnly(buffers));
    }

    private static MapPostEffect3ConstantBufferParameter RequireDeclaration(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        string name,int expectedIndex,uint bytes)
    {
        int index=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.ConstantBuffer,name)
            ??throw new InvalidDataException("3SPK actual MotionBlur CB declaration is absent: "+name);
        var cb=allocation.ConstantBufferParameters[index];
        if(index!=expectedIndex||cb.ByteCount!=bytes||cb.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("3SPK actual MotionBlur CB capacity/ownership differs: "+name);
        return cb;
    }
    private static ReadOnlyMemory<byte> RequireTyped(MapPostEffect3ParameterState state,string name,uint type)
    {
        int index=state.FindScalarIndex(name)??throw new InvalidDataException("3SPK MotionBlur scalar absent: "+name);
        var declaration=state.Declarations[index];
        if(declaration.NativeValueType!=type||declaration.ElementCount!=1)
            throw new InvalidDataException("3SPK MotionBlur scalar declaration differs: "+name);
        return state.RequireValue(name);
    }
    private static float ReadFloat(MapPostEffect3ParameterState state,string name)
        =>BinaryPrimitives.ReadSingleLittleEndian(RequireTyped(state,name,1).Span);
    private static int ReadInt(MapPostEffect3ParameterState state,string name)
        =>BinaryPrimitives.ReadInt32LittleEndian(RequireTyped(state,name,6).Span);
    private static Matrix4x4 ReadMatrix(MapPostEffect3ParameterState state,string name)
    {
        var bytes=RequireTyped(state,name,5).Span;
        float[] v=new float[16];for(int i=0;i<16;i++)v[i]=BinaryPrimitives.ReadSingleLittleEndian(bytes[(i*4)..]);
        return new(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7],v[8],v[9],v[10],v[11],v[12],v[13],v[14],v[15]);
    }
    private static void WriteMatrix(Span<byte> bytes,int at,Matrix4x4 m)
    {
        float[] v=[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
        for(int i=0;i<16;i++)Put(bytes,at+i*4,v[i]);
    }
    private static void Put(Span<byte> bytes,int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes[at..],value);
}

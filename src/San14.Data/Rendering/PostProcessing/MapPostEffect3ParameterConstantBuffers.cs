using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Complete owned bytes produced by a recovered controller writer.
/// No native GPU upload or original active frame is implied.</summary>
public sealed class MapPostEffect3ParameterConstantBuffer
{
    private readonly byte[] bytes;
    internal MapPostEffect3ParameterConstantBuffer(MapPostEffect3ParameterState state,
        MapPostEffect3ConstantBufferParameter declaration,MapPostEffect3ConstantBufferWriter writer,byte[] bytes,string locale="TC")
    {
        FrameNumber=state.FrameNumber;OwnerIdentity=state.OwnerIdentity;SourceSha256=state.SourceSha256;
        Declaration=declaration;Writer=writer;Locale=locale;this.bytes=bytes.ToArray();
    }
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}
    public string SourceSha256{get;}
    public MapPostEffect3ConstantBufferParameter Declaration{get;}
    public MapPostEffect3ConstantBufferWriter Writer{get;}
    public string Locale{get;}
    public ReadOnlyMemory<byte> Bytes=>bytes.ToArray();
}

public sealed record MapPostEffect3ParameterConstantBufferBinding(int SourceRowIndex,
    uint StageCode,int RegisterIndex,MapPostEffect3Program Program,
    MapPostEffect3ParameterConstantBuffer ConstantBuffer)
{
    /// <summary>Native setters may bind a valid CB at a register that this
    /// selected program never declares. Retain that ordered setter without
    /// manufacturing a shader descriptor or a shader read.</summary>
    public bool ShaderDeclaresRegister { get; init; } = true;
}

/// <summary>Explicit inputs to native: caller width/height/level,
/// RenderContext1C48 and controller1A8. This record supplies values; it does not
/// claim to recover their current native producers or choose a loop iteration.</summary>
public sealed record MapPostEffect3ParameterBloomLoopInputs(int SourceWidth,int SourceHeight,
    int Level,float CurrentLoopRate,uint Controller1A8Word);

/// <summary>Scalar-to-CB is controller code, not a generic scalar/type6 memcpy.
/// Only complete source-closed writers are exposed; remaining CBs are rejected
/// by PreparePassBindings until their own writers produce complete packets.</summary>
public static class MapPostEffect3ParameterConstantBuffers
{
    /// <summary>..: complete32B cbBloom2Loop per invocation.
    /// The separately supplied iteration/context fields have no guessed defaults.</summary>
    public static MapPostEffect3ParameterConstantBuffer PrepareBloom2Loop(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,
        MapPostEffect3ParameterBloomLoopInputs inputs,string locale="TC")
    {
        Validate(state,allocation);ArgumentNullException.ThrowIfNull(inputs);
        var declaration=RequireDeclaration(state,allocation,"cbBloom2Loop",32);
        if(inputs.Level<0||inputs.Level>30||inputs.SourceWidth<=0||inputs.SourceHeight<=0)
            throw new NotSupportedException("3SPK bloom loop signed-divisor/extent profile is unsupported.");
        int divisor=1<<inputs.Level,w=inputs.SourceWidth/divisor,h=inputs.SourceHeight/divisor;
        if(w==0||h==0)throw new NotSupportedException("3SPK bloom loop would produce zero-sized divided extent.");
        float viewportW=1,viewportH=1;
        if(state.FindScalarIndex("Viewport") is not null)
        {
            var d=state.Declarations[state.FindScalarIndex("Viewport")!.Value];
            if(d.NativeValueType!=4||d.ElementCount!=1)throw new InvalidDataException("3SPK Viewport type differs.");
            var viewport=state.RequireValue("Viewport").Span;
            viewportW=BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(viewport[8..]));
            viewportH=BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(viewport[12..]));
        }
        bool cropped=viewportW!=1||viewportH!=1;float edgeW=1,edgeH=1;
        if(cropped)
        {
            int Cvt(float v)=>!float.IsFinite(v)||v<int.MinValue||v>=2147483648f?int.MinValue:(int)v;
            edgeW=NativeLogarithm.Divide(NativeFloat.Subtract(
                Cvt(NativeFloat.Multiply(w,viewportW)),.5f),w);
            edgeH=NativeLogarithm.Divide(NativeFloat.Subtract(
                Cvt(NativeFloat.Multiply(h,viewportH)),.5f),h);
        }
        byte[] bytes=new byte[32];Put(bytes,0,NativeLogarithm.Divide(1,w));
        Put(bytes,4,NativeLogarithm.Divide(1,h));Put(bytes,8,inputs.CurrentLoopRate);Put(bytes,12,inputs.Level);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16),inputs.Controller1A8Word);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20),0);Put(bytes,24,edgeW);Put(bytes,28,edgeH);
        return new(state,declaration,MapPostEffect3ConstantBufferWriter.BloomLoop,bytes,locale);
    }

    /// <summary>..: actual cbBloom2Main map/write/unmap.
    /// Preserves SSE scalar operation order, including native negative sentinel
    /// exposure/anti-flicker values; no brightness adjustment is introduced.</summary>
    public static MapPostEffect3ParameterConstantBuffer PrepareBloom2Main(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,string locale="TC")
    {
        Validate(state,allocation);
        var declaration=RequireDeclaration(state,allocation,"cbBloom2Main",32);
        // gives version4 only if all optional native scalar lookups exist.
        // On the pinned complete 6CA payload they do. Never infer this from a UI setting.
        bool hasVersion4=state.FindScalarIndex("Viewport") is not null&&
            state.FindScalarIndex("Enable4KCBFix") is not null&&state.FindScalarIndex("EnableBloomColorFix") is not null;
        bool colorFix=hasVersion4&&I(state,"EnableBloomColorFix")>0;
        float threshold=F(state,"BloomThresholdCurveParams",0),kneeFraction=F(state,"BloomThresholdCurveParams",4);
        float knee=NativeFloat.Add(NativeFloat.Multiply(threshold,kneeFraction),
            BitConverter.UInt32BitsToSingle(0x3727C5AC));
        float inverseKnee=NativeLogarithm.Divide(0.25f,knee);
        bool automatic=I(state,"EnableAutoExposure")>0,antiFlicker=I(state,"UseBloomAntiFlicker")>0;
        byte[] bytes=new byte[32];
        Put(bytes,0,NativeFloat.Subtract(threshold,knee));
        Put(bytes,4,NativeFloat.Add(knee,knee));Put(bytes,8,inverseKnee);Put(bytes,12,threshold);
        Put(bytes,16,automatic?-1f:F(state,"UserExposureScale"));
        Put(bytes,20,antiFlicker?-.5f:0f);Put(bytes,24,antiFlicker?1f:-1f);Put(bytes,28,colorFix?1f:0f);
        return new(state,declaration,MapPostEffect3ConstantBufferWriter.BloomMain,bytes,locale);
    }

    /// <summary>/: retain original type6 row order and stage-bit
    /// order, with program/RDEF register and byte-size crosschecks. Only explicit
    /// counted type4 CBs are returned; implicit backend/global CBs remain separate.</summary>
    public static IReadOnlyList<MapPostEffect3ParameterConstantBufferBinding> PreparePassBindings(
        MapPostEffect3AllocationPlan allocation,int passIndex,long frameNumber,
        IReadOnlyList<MapPostEffect3ParameterConstantBuffer> completeBuffers)
    {
        ArgumentNullException.ThrowIfNull(allocation);ArgumentNullException.ThrowIfNull(completeBuffers);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(frameNumber<0||(uint)passIndex>=allocation.Passes.Count)throw new ArgumentOutOfRangeException(nameof(passIndex));
        var byIndex=new Dictionary<int,MapPostEffect3ParameterConstantBuffer>();
        foreach(var packet in completeBuffers)
        {
            ArgumentNullException.ThrowIfNull(packet);
            int index=packet.Declaration.Index;
            if(packet.FrameNumber!=frameNumber||packet.OwnerIdentity!=allocation.ContextOwnerIdentity||
               packet.SourceSha256!=allocation.Source.Sha256||
               (uint)index>=allocation.ConstantBufferParameters.Count||
               packet.Declaration!=allocation.ConstantBufferParameters[index]||
               packet.Bytes.Length!=packet.Declaration.ByteCount||!byIndex.TryAdd(index,packet))
                throw new InvalidDataException("3SPK complete CB packet has stale or unrelated owner/source/declaration.");
        }
        var pass=allocation.Passes[passIndex];var result=new List<MapPostEffect3ParameterConstantBufferBinding>();
        foreach(var binding in pass.ActiveBindings.Where(b=>b.Role==MapPostEffect3BindingRole.ConstantBufferRead))
        {
            // Native row lookup precedes active stage traversal, including a
            // row whose stage mask produces no selected shader binding.
            if(!byIndex.ContainsKey(binding.ParameterIndex))
                throw new NotSupportedException($"3SPK CB {binding.ParameterIndex} has no complete recovered controller writer for pass {passIndex}.");
        }
        foreach(var requirement in MapPostEffect3ParameterBindingRequirements.Read(allocation,passIndex))
        {
            var packet=byIndex[requirement.ParameterIndex];
            if(requirement.ShaderDeclaredByteCount is int size&&size>packet.Bytes.Length)
                throw new InvalidDataException("3SPK type4 CB size disagrees with the selected DXBC declaration.");
            // Native binds by counted CB index/register, not name.
            // Preserve source row duplicates and actual selected stage order.
            result.Add(new(requirement.SourceRowIndex,requirement.StageCode,requirement.RegisterIndex,
                requirement.Program,packet){ShaderDeclaresRegister=requirement.ShaderDeclaredByteCount is not null});
        }
        return result.AsReadOnly();
    }

    private static void Validate(MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        MapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(state.SourceSha256!=allocation.Source.Sha256||state.OwnerIdentity!=allocation.ContextOwnerIdentity)
            throw new InvalidDataException("3SPK scalar/CB allocation owners differ.");
    }
    private static MapPostEffect3ConstantBufferParameter RequireDeclaration(
        MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation,string name,int byteCount)
    {
        int i=state.FindResourceIndex(MapPostEffect3ParameterResourceTable.ConstantBuffer,name)
            ??throw new InvalidDataException("3SPK source CB is absent.");
        var declaration=allocation.ConstantBufferParameters[i];
        if(declaration.ByteCount!=byteCount||declaration.Ownership!=MapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("3SPK source controller CB declaration differs.");
        return declaration;
    }
    private static float F(MapPostEffect3ParameterState state,string name,int offset=0)
    {
        var d=state.Declarations[state.FindScalarIndex(name)??throw new InvalidDataException("3SPK scalar is absent.")];
        if(d.NativeValueType!=1||offset<0||(long)offset+4>d.ValueByteCount)
            throw new InvalidDataException("3SPK float scalar type/extent differs from source controller.");
        return BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(state.RequireValue(name).Span[offset..]));
    }
    private static int I(MapPostEffect3ParameterState state,string name)
    {
        var d=state.Declarations[state.FindScalarIndex(name)??throw new InvalidDataException("3SPK scalar is absent.")];
        if(d.NativeValueType!=6||d.ElementCount!=1)throw new InvalidDataException("3SPK signed scalar type differs from source controller.");
        return BinaryPrimitives.ReadInt32LittleEndian(state.RequireValue(name).Span);
    }
    private static void Put(Span<byte> bytes,int offset,float value)
        =>BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..],BitConverter.SingleToUInt32Bits(value));
}

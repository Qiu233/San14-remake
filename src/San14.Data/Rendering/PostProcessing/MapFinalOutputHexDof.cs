using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace San14.Data;

public enum HexDofTextureRole { SourceFront,SourceBack,ScratchFront,ScratchBack,FinalFront,FinalBack }
public sealed record HexDofTextureSelector(uint Register,HexDofTextureRole Role,int? SpkTextureIndex);
public sealed record HexDofScratchRecipe(int Width,int Height,uint NativePixelFormat,
    RenderTextureFormats Formats,int SourceTextureIndex);
public sealed record HexDofDrawRecipe(int OuterIteration,int InnerIteration,
    MapFinalOutputPass Pass,IReadOnlyList<HexDofTextureSelector> Inputs,
    IReadOnlyList<HexDofTextureSelector> Outputs,bool Clear,uint ClearPackedColor,
    bool EnableBlend,uint NativeBlendWord,float RenderWeight,
    IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> Constants)
{
    public uint NativeClearFlags=>Clear?0xFu:0u;
    public MapFinalOutputProgramSource Program=>Pass.Program;
    public int SourcePassIndex=>Pass.Index;
}

/// <summary>Native E1E680: three directions, two draws per direction, with the
/// original VS3/PS3, actual source/scratch swaps, and additive final MRT. Owns no
/// GPU resources. Current parameter bytes and complete retained CBs are explicit.
/// Source DOF0 still executes all six draws; PS3 writes zero on that branch.</summary>
public sealed class MapFinalOutputHexDof
{
    public IReadOnlyList<HexDofDrawRecipe> Draws { get; }
    public HexDofScratchRecipe Scratch { get; }
    public float DofType { get; }
    public float InclinationDegrees { get; }
    public float Scale { get; }
    public bool PixelSamplingInactive=>DofType!=1;
    private MapFinalOutputHexDof(HexDofDrawRecipe[] draws,
        HexDofScratchRecipe scratch,float dof,float inclination,float scale)
    {Draws=Array.AsReadOnly(draws);Scratch=scratch;DofType=dof;InclinationDegrees=inclination;Scale=scale;}

    public static MapFinalOutputHexDof Create(MapFinalOutputPassGraph graph,
        int baseWidth,int baseHeight,
        IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>> completeRetainedTemplates,
        IReadOnlyDictionary<int,ReadOnlyMemory<byte>> currentParameters,
        MapFinalOutputVertexWrapperState wrapper)
    {
        ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(completeRetainedTemplates);
        ArgumentNullException.ThrowIfNull(currentParameters);ArgumentNullException.ThrowIfNull(wrapper);
        var pass=graph.Passes[3];
        if(pass.Program.VertexProgramIndex!=3||pass.Program.PixelProgramIndex!=3||
            pass.FunctorName!="PostEffectFunctorRenderHexDOFForKids"||
            !pass.Inputs.Select(i=>i.TextureIndex).SequenceEqual(new[]{2,3})||
            !pass.Outputs.Select(o=>o.TextureIndex).SequenceEqual(new[]{6,7}))
            throw new InvalidDataException("Hex requires its exact source pass3 and ordered MRT/input identities.");
        var source=graph.ResolveAllocation(2,baseWidth,baseHeight);
        if(source.Width<=0||source.Height<=0||source.Width>ushort.MaxValue||source.Height>ushort.MaxValue)
            throw new InvalidDataException("Hex source extent must fit the native positive u16 texture view.");
        foreach(int index in new[]{3,6,7})
        {
            var other=graph.ResolveAllocation(index,baseWidth,baseHeight);
            if(other.Width!=source.Width||other.Height!=source.Height||other.PixelFormatCode!=source.PixelFormatCode)
                throw new InvalidDataException("Hex source/final MRT extents and native formats must match.");
        }
        if(wrapper.RetainedUvVectors.Length!=0)
            throw new InvalidDataException("Hex writes its four native UV vectors; caller wrapper UV storage must be empty.");
        float inclination=Parameter(currentParameters,22),scale=Parameter(currentParameters,23),dof=Parameter(currentParameters,25);
        if(!float.IsFinite(inclination)||!float.IsFinite(scale)||dof is not(0 or 1 or 2))
            throw new InvalidDataException("Hex requires finite current inclination/scale and the clamped native DOF type.");
        if(!currentParameters.TryGetValue(14,out var viewport)||viewport.Length!=16)
            throw new InvalidDataException("Hex requires current VIEWPORT14, not its zero source declaration.");
        var scratch=new HexDofScratchRecipe(source.Width,source.Height,source.PixelFormatCode,graph.ResolveTempFormats(2),2);
        var draws=new HexDofDrawRecipe[6];
        float inclinationRadians=Multiply(inclination,FromBits(0x3C8EFA35));
        for(int outer=0;outer<3;outer++)
        {
            float baseAngle=Add(Multiply(outer,FromBits(0x40060A92)),inclinationRadians);
            for(int inner=0;inner<2;inner++)
            {
                float weight=inner==0?1:FromBits(0x3EAAAAAB);
                var parameters=currentParameters.ToDictionary(p=>p.Key,p=>p.Value);
                byte[] weightBytes=new byte[4];Write(weightBytes,0,weight);parameters[24]=weightBytes;
                var constants=graph.BuildConstants(3,completeRetainedTemplates,parameters).ToDictionary(p=>p.Key,p=>p.Value);
                float angle=Add(Multiply(inner,FromBits(0x40060A92)),baseAngle);
                var direction=NativeSinCos(angle);
                constants[(1,0)]=BuildVertex(constants[(1,0)],wrapper,viewport,source.Width,source.Height,direction.Sin,direction.Cos,scale);
                var inputs=inner==0?new[]{Select(0,HexDofTextureRole.SourceFront,2),Select(1,HexDofTextureRole.SourceBack,3)}:
                    new[]{Select(0,HexDofTextureRole.ScratchFront,null),Select(1,HexDofTextureRole.ScratchBack,null)};
                var outputs=inner==0?new[]{Select(0,HexDofTextureRole.ScratchFront,null),Select(1,HexDofTextureRole.ScratchBack,null)}:
                    new[]{Select(0,HexDofTextureRole.FinalFront,6),Select(1,HexDofTextureRole.FinalBack,7)};
                draws[outer*2+inner]=new(outer,inner,pass,Array.AsReadOnly(inputs),Array.AsReadOnly(outputs),inner==0||outer==0,
                    0,true,0x00111100,weight,new ReadOnlyDictionary<(uint,uint),ReadOnlyMemory<byte>>(constants));
            }
        }
        return new(draws,scratch,dof,inclination,scale);
    }

    private static HexDofTextureSelector Select(uint register,HexDofTextureRole role,int? texture)=>new(register,role,texture);
    private static float Parameter(IReadOnlyDictionary<int,ReadOnlyMemory<byte>> parameters,int index)
        =>parameters.TryGetValue(index,out var bytes)&&bytes.Length==4?BinaryPrimitives.ReadSingleLittleEndian(bytes.Span):
            throw new InvalidDataException($"Hex current source parameter {index} is missing or incomplete.");
    private static ReadOnlyMemory<byte> BuildVertex(ReadOnlyMemory<byte> template,
        MapFinalOutputVertexWrapperState wrapper,ReadOnlyMemory<byte> viewport,int width,int height,float sin,float cos,float scale)
    {
        if(template.Length!=288)throw new InvalidDataException("Hex VS3 requires its complete 288-byte retained CB0.");
        byte[] bytes=template.ToArray();
        Write(bytes,0,Add(Divide(wrapper.PixelOffsetX,width),0));Write(bytes,4,Add(Divide(wrapper.PixelOffsetY,height),0));
        Write(bytes,8,0);Write(bytes,12,0);
        float dx=Divide(sin,width),dy=Divide(cos,height);
        for(int pair=0;pair<8;pair++)
        {
            Write(bytes,16+pair*8,Multiply(Multiply(dx,pair+.5f),scale));
            Write(bytes,20+pair*8,Multiply(Multiply(dy,pair+.5f),scale));
        }
        var x=TextureBounds(width,BinaryPrimitives.ReadSingleLittleEndian(viewport.Span),BinaryPrimitives.ReadSingleLittleEndian(viewport.Span[8..]));
        var y=TextureBounds(height,BinaryPrimitives.ReadSingleLittleEndian(viewport.Span[4..]),BinaryPrimitives.ReadSingleLittleEndian(viewport.Span[12..]));
        Write(bytes,272,Subtract(x.End,x.Begin));Write(bytes,276,Subtract(y.End,y.Begin));
        Write(bytes,280,x.Begin);Write(bytes,284,y.Begin);
        return bytes;
    }

    // Exact native14590 piecewise scalar approximation, including float32
    // range reduction and polynomial operation order. It is not platform sinf.
    public static (float Sin,float Cos) NativeSinCos(float angle)
    {
        if(!float.IsFinite(angle))throw new InvalidDataException("Native Hex direction needs a finite angle.");
        if(angle==0)return(0,1);
        bool sinPositive=angle>=0,cosPositive=true;float x=angle<0?-angle:angle;
        float twoPi=FromBits(0x40C90FDB),pi=FromBits(0x40490FDB),halfPi=FromBits(0x3FC90FDB);
        // The original repeated subtraction can stall for enormous float inputs.
        // Bound this helper to its finite source executor domain.
        if(x>twoPi*1024)throw new InvalidDataException("Hex inclination exceeds the finite native source direction domain.");
        while(x>=twoPi)x=Add(x,-twoPi);
        if(x>=pi){x=Add(x,-pi);sinPositive=!sinPositive;cosPositive=false;}
        if(x>=halfPi){x=Subtract(pi,x);cosPositive=!cosPositive;}
        if(x<FromBits(0x3F2B16EC))return(SinPolynomial(x,sinPositive),CosPolynomial(x,cosPositive));
        if(x>=FromBits(0x3F688790))
        {
            float complement=Subtract(halfPi,x);
            return(CosPolynomial(complement,sinPositive),SinPolynomial(complement,cosPositive));
        }
        return(CosPolynomial(Subtract(halfPi,x),sinPositive),CosPolynomial(x,cosPositive));
    }
    private static float SinPolynomial(float x,bool positive)
    {
        float square=Multiply(x,x),fourth=Multiply(square,square);
        float result=positive?Add(Multiply(Subtract(6,square),20),fourth):Subtract(Multiply(Subtract(square,6),20),fourth);
        return Multiply(Multiply(result,x),FromBits(0x3C088889));
    }
    private static float CosPolynomial(float x,bool positive)
    {
        float square=Multiply(x,x),fourth=Multiply(square,square),sum=Add(fourth,360);
        float result=positive?Subtract(Multiply(sum,Subtract(30,square)),10080):Add(Multiply(sum,Subtract(square,30)),10080);
        return Multiply(result,FromBits(0x3AB60B61));
    }
    private static (float Begin,float End) TextureBounds(int extent,float origin,float span)
    {
        int first=RoundedPixel(extent,origin),last=unchecked(first+RoundedPixel(extent,span));
        float begin=Divide(first,extent),normalizedSpan=Subtract(Divide(last,extent),begin),ratio=Divide(span,normalizedSpan);
        return(Subtract(origin,Multiply(ratio,begin)),Add(origin,Multiply(Subtract(1,begin),ratio)));
    }
    private static int RoundedPixel(int extent,float value)
    {
        float rounded=MathF.Floor(Add(Multiply(extent,value),.5f));
        return !float.IsFinite(rounded)||rounded>=2147483648f||rounded< -2147483648f?int.MinValue:(int)rounded;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Multiply(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Subtract(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
    private static float FromBits(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    private static void Write(byte[] bytes,int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at,4),value);
}

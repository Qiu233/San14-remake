using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

public sealed record Sky2MoonScreenPosition(Vector4 Position,bool InFront);
public sealed record Sky2MoonGeometryPacket(Matrix4x4 MoonFace,
    Sky2MoonScreenPosition Screen);

/// <summary>Original118E6B0 MoonFace and118E7B0 MoonScreen, from actual
/// normalized rotation-derived Moon direction and raw selected Moon position.
/// Texture presence never selects these writers. Absent Moon skips both.</summary>
public static class Sky2MoonGeometryPreparation
{
    public static Sky2MoonGeometryPacket? Prepare(bool moonSetterPresent,
        Vector3 preparedRotationDerivedMoonDirection,Vector4 rawMoonPosition,
        ReadOnlySpan<float> preparedCameraF0,float preparedCamera138,float preparedCamera13C,
        int targetWidth,int targetHeight,float moonSize1498)
    {
        if(!moonSetterPresent)return null;
        return new(PrepareFace(true,preparedRotationDerivedMoonDirection)!.Value,
            PrepareScreen(true,rawMoonPosition,preparedCameraF0,preparedCamera138,preparedCamera13C,
                targetWidth,targetHeight,moonSize1498)!);
    }

    public static Matrix4x4? PrepareFace(bool moonSetterPresent,
        Vector3 preparedRotationDerivedMoonDirection)
    {
        if(!moonSetterPresent)return null;
        var d=preparedRotationDerivedMoonDirection;Finite(d.X);Finite(d.Y);Finite(d.Z);
        // Do not choose an arbitrary axis at the actual zero-cross degeneracy.
        float ax=S(M(d.Z,-1),M(d.Y,0)),ay=S(M(d.X,0),M(d.Z,0)),az=S(M(d.Y,0),M(d.X,-1));
        float dot=A(S(M(d.X,0),M(d.Y,1)),M(d.Z,0));
        float lengthSquared=A(A(M(ax,ax),M(ay,ay)),M(az,az));
        if(!float.IsFinite(lengthSquared)||lengthSquared<0||!Sse.IsSupported)
            throw new InvalidDataException("MoonFace finite source domain requires native finite SQRTPS operands.");
        float length=Sse.Sqrt(Vector128.Create(lengthSquared)).GetElement(0);
        if(length!=0){ax=D(ax,length);ay=D(ay,length);az=D(az,length);}
        else ax=ay=az=0;
        float angle=Neg(FiniteInverseTrigonometry.Acos(dot));
        var (s,c)=SinCos3F7280(angle);float t=S(1,c);
        float dx=A(M(M(t,ax),ax),c),dy=A(M(M(t,ay),ay),c),dz=A(M(M(t,az),az),c);
        float yz=M(M(ay,t),az),zx=M(M(az,t),ax),xy=M(M(ax,t),ay);
        float sx=M(s,ax),sy=M(s,ay),sz=M(s,az);
        return new(dx,A(sz,xy),S(zx,sy),0,S(xy,sz),dy,A(sx,yz),0,
            A(sy,zx),S(yz,sx),dz,0,0,0,0,1);
    }

    /// <summary>Raw position is negated once, without Sun's prior XYZ world
    /// normalization. Original division happens before the front sentinel.</summary>
    public static Sky2MoonScreenPosition? PrepareScreen(bool moonSetterPresent,
        Vector4 rawMoonPosition,ReadOnlySpan<float> preparedCameraF0,float preparedCamera138,float preparedCamera13C,
        int targetWidth,int targetHeight,float moonSize1498)
    {
        if(!moonSetterPresent)return null;
        Finite(rawMoonPosition.X);Finite(rawMoonPosition.Y);Finite(rawMoonPosition.Z);Finite(rawMoonPosition.W);
        Finite(preparedCamera138);Finite(preparedCamera13C);Finite(moonSize1498);
        if(preparedCameraF0.Length!=16||targetWidth<=0||targetHeight<=0||preparedCamera138==0)
            throw new InvalidDataException("MoonScreen finite source domain requires preparedF0, positive dimensions and nonzero camera138.");
        foreach(float value in preparedCameraF0)Finite(value);
        var m=preparedCameraF0;
        float x=M(rawMoonPosition.X,-1),y=M(rawMoonPosition.Y,-1),z=M(rawMoonPosition.Z,-1),w=M(rawMoonPosition.W,-1);
        float clipX=A(A(A(M(y,m[4]),M(x,m[0])),M(z,m[8])),M(w,m[12]));
        float clipY=A(A(A(M(y,m[5]),M(x,m[1])),M(z,m[9])),M(w,m[13]));
        float clipW=A(A(A(M(y,m[7]),M(x,m[3])),M(z,m[11])),M(w,m[15]));
        float inverse=D(1,clipW),screenX=M(clipX,inverse),screenY=M(clipY,inverse);
        // Reject the known finite contract's exceptional division, after the
        // source operation. No guessed epsilon, cached screen or prior skip.
        if(!float.IsFinite(inverse)||!float.IsFinite(screenX)||!float.IsFinite(screenY))
            throw new InvalidDataException("MoonScreen exceptional projection needs its separate original IEEE cache scope.");
        bool front=clipW>0;if(!front)screenX=screenY=F(0xC47A0000);
        float angularHalf=M(M(moonSize1498,F(0x3C142917)),.5f);
        float pixelMinimum=D(2,(float)Math.Max(targetWidth,targetHeight));
        float cameraRatio=D(angularHalf,preparedCamera138);
        float maximum=pixelMinimum>cameraRatio?pixelMinimum:cameraRatio;
        var result=new Vector4(screenX,screenY,preparedCamera13C,A(maximum,maximum));
        Finite(result.X);Finite(result.Y);Finite(result.Z);Finite(result.W);
        return new(result,front);
    }

    private static (float Sin,float Cos) SinCos3F7280(float angle)
    {
        Finite(angle);
        if(angle < F(0xC0490FDB)||angle>F(0x40490FDB))
            throw new InvalidDataException("Moon native sine/cosine is scoped to negative finite acos angles.");
        float q=M(angle,F(0x3E22F983));q=angle>=0?A(q,.5f):S(q,.5f);
        int reductions=(int)q;float a=S(angle,M((float)reductions,F(0x40C90FDB))),sign=1;
        if(a>F(0x3FC90FDB)){a=S(F(0x40490FDB),a);sign=-1;}
        else if(a<F(0xBFC90FDB)){a=S(F(0xC0490FDB),a);sign=-1;}
        float z=M(a,a);
        float sp=S(F(0x3638B88E),M(z,F(0x32CD365B)));
        float cpTerm=M(z,F(0x348BDD11));
        sp=S(M(sp,z),F(0x39500BF1));sp=A(M(sp,z),F(0x3C088886));
        sp=S(M(sp,z),F(0x3E2AAAAB));sp=A(M(sp,z),1);float sine=M(sp,a);
        float cp=S(F(0x37CFB4C2),cpTerm);cp=S(M(cp,z),F(0x3AB609AA));cp=A(M(cp,z),F(0x3D2AAAA3));
        cp=S(M(cp,z),.5f);cp=A(M(cp,z),1);return(sine,M(cp,sign));
    }
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    private static float Neg(float value)=>F(BitConverter.SingleToUInt32Bits(value)^0x80000000);
    private static void Finite(float value){if(!float.IsFinite(value))throw new InvalidDataException("Moon geometry requires a finite actual source input.");}
    private static float A(float a,float b)=>NativeFloat.Add(a,b);
    private static float S(float a,float b)=>NativeFloat.Subtract(a,b);
    private static float M(float a,float b)=>NativeFloat.Multiply(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}

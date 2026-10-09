using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Explicit prepared Camera words consumed by original9F97E0.
/// Fallback20 is copied from C20.xyz; no semantic or normalization assumption
/// about that source vector is made here.</summary>
public sealed record DeferredRimCameraInputs(Vector3 Eye,Vector3 Target,Vector3 Fallback20,float Camera13C);
public sealed record DeferredRimPreparation(float Threshold,IReadOnlyList<Vector4> Primary,
    IReadOnlyList<Vector4> Secondary);

/// <summary>Original PB2IndexedRamp mode9's geometric preparation. ReferenceXZ
/// is explicitly selected by the original caller (auxiliary record or data118).
/// It must not be replaced with a fixed sun direction.</summary>
public static class DeferredRimPreparationMath
{
    /// <summary>Project the same prepared Camera C used by mode9, including
    /// its target/fallback vectors and the separate C13C scalar.</summary>
    public static DeferredRimCameraInputs ReadPreparedCamera(ReadOnlySpan<byte> preparedCameraPrefix)
    {
        if(preparedCameraPrefix.Length!=0x140)
            throw new InvalidDataException("Rim camera preparation requires the explicit prepared Camera prefix0x140.");
        var eye=ReadVector(preparedCameraPrefix,0);var target=ReadVector(preparedCameraPrefix,0x10);var fallback=ReadVector(preparedCameraPrefix,0x20);
        float scalar=BinaryPrimitives.ReadSingleLittleEndian(preparedCameraPrefix.Slice(0x13C,4));
        Finite(eye.X,eye.Y,eye.Z,target.X,target.Y,target.Z,fallback.X,fallback.Y,fallback.Z,scalar);
        return new(eye,target,fallback,scalar);
    }

    /// <summary>Original9F9870..9F98A3 chooses first auxiliary vector X/Z when
    /// one exists, otherwise reads the selected PB2 data118/data120. An active
    /// auxiliary branch does not read or require the fallback data prefix.</summary>
    public static Vector2 ReadSelectedReference(Vector4? explicitFirstAuxiliaryVector,ReadOnlySpan<byte> selectedPb2DataPrefix)
    {
        if(explicitFirstAuxiliaryVector is { } auxiliary)
        {
            Finite(auxiliary.X,auxiliary.Z);return new(auxiliary.X,auxiliary.Z);
        }
        if(selectedPb2DataPrefix.Length!=0x124)
            throw new InvalidDataException("Rim reference fallback requires the selected PB2 data prefix0x124.");
        float x=BinaryPrimitives.ReadSingleLittleEndian(selectedPb2DataPrefix.Slice(0x118,4));
        float z=BinaryPrimitives.ReadSingleLittleEndian(selectedPb2DataPrefix.Slice(0x120,4));
        Finite(x,z);return new(x,z);
    }

    /// <summary>Original null rim-data branch9F9B57: two complete row-pairs are
    /// installed and threshold is +0. This is a writer, not a retain branch.</summary>
    public static DeferredRimPreparation NoRimDataBranch() => new(0,
        Array.AsReadOnly(new[]{new Vector4(0,1,0,0),Vector4.Zero}),
        Array.AsReadOnly(new[]{new Vector4(0,1,0,0),Vector4.Zero}));

    public static DeferredRimPreparation Prepare(ReadOnlySpan<byte> selectedRimPrefix,
        DeferredRimCameraInputs camera,Vector2 explicitReferenceXZ)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if(selectedRimPrefix.Length!=0x34)
            throw new InvalidDataException("Rim preparation requires the selected explicit data prefix0x34.");
        var source=new float[13];
        for(int i=0;i<source.Length;i++)source[i]=BinaryPrimitives.ReadSingleLittleEndian(selectedRimPrefix.Slice(i*4,4));
        Finite(source);
        Finite(camera.Eye.X,camera.Eye.Y,camera.Eye.Z,camera.Target.X,camera.Target.Y,camera.Target.Z,
            camera.Fallback20.X,camera.Fallback20.Y,camera.Fallback20.Z,camera.Camera13C,explicitReferenceXZ.X,explicitReferenceXZ.Y);
        float x=Subtract(camera.Eye.X,camera.Target.X),y=Subtract(camera.Eye.Y,camera.Target.Y),z=Subtract(camera.Eye.Z,camera.Target.Z);
        float length=SquareRoot(Add(Add(Multiply(x,x),Multiply(y,y)),Multiply(z,z)));
        float inverse=Divide(1,length);
        x=Multiply(x,inverse);y=Multiply(y,inverse);z=Multiply(z,inverse);
        Vector3 horizontal;
        if(x==0 && z==0) horizontal=camera.Fallback20;
        else
        {
            float hInverse=Divide(1,SquareRoot(Add(Add(Multiply(x,x),0),Multiply(z,z))));
            horizontal=new(Multiply(hInverse,x),Multiply(hInverse,0),Multiply(hInverse,z));
        }
        Vector3 reference;
        if(explicitReferenceXZ.X==0 && explicitReferenceXZ.Y==0)
            reference=new(Multiply(camera.Fallback20.X,-1),Multiply(camera.Fallback20.Y,-1),Multiply(camera.Fallback20.Z,-1));
        else
        {
            float rInverse=Divide(1,SquareRoot(Add(Add(Multiply(explicitReferenceXZ.X,explicitReferenceXZ.X),0),
                Multiply(explicitReferenceXZ.Y,explicitReferenceXZ.Y))));
            reference=new(Multiply(explicitReferenceXZ.X,rInverse),Multiply(0,rInverse),Multiply(explicitReferenceXZ.Y,rInverse));
        }
        // Original scalar order is Y product, then X, then Z.
        float dot=Add(Add(Multiply(reference.Y,horizontal.Y),Multiply(reference.X,horizontal.X)),Multiply(reference.Z,horizontal.Z));
        float positive=dot>0 ? dot : 0f; // MAXSS(dot,+0), including equality.
        float positiveSquared=Multiply(positive,positive);
        float symmetric=Multiply(Add(dot,1),.5f);
        float intensity=Add(Multiply(Subtract(source[9],source[0]),positiveSquared),source[0]);
        float threshold=Add(Multiply(Subtract(source[10],source[1]),positiveSquared),source[1]);
        if(intensity<0) intensity=0; // COMISS below, preserving a source -0.
        if(threshold<0) threshold=0;
        float remaining=Multiply(Subtract(1,symmetric),source[8]);
        var primary0=new Vector4(intensity,remaining,source[3],source[12]);
        var primary1=new Vector4(source[5],source[6],source[7],source[11]);
        var secondary0=new Vector4(x,y,z,source[4]);
        var secondary1=new Vector4(1,1,camera.Camera13C,Multiply(symmetric,source[2]));
        Finite(primary0.X,primary0.Y,primary0.Z,primary0.W,primary1.X,primary1.Y,primary1.Z,primary1.W,
            secondary0.X,secondary0.Y,secondary0.Z,secondary0.W,secondary1.X,secondary1.Y,secondary1.Z,secondary1.W,threshold);
        return new(threshold,Array.AsReadOnly(new[]{primary0,primary1}),Array.AsReadOnly(new[]{secondary0,secondary1}));
    }

    public static DeferredRampInputs Apply(DeferredRampInputs previous,DeferredRimPreparation prepared)
    {
        ArgumentNullException.ThrowIfNull(previous);ArgumentNullException.ThrowIfNull(prepared);
        if(prepared.Primary is null||prepared.Primary.Count!=2||prepared.Secondary is null||prepared.Secondary.Count!=2)
            throw new InvalidDataException("Rim preparation requires two explicit rows per primary/secondary group.");
        return previous with{RimThreshold=prepared.Threshold,RimParameters=Array.AsReadOnly(prepared.Primary.ToArray()),
            SecondaryRimParameters=Array.AsReadOnly(prepared.Secondary.ToArray())};
    }
    private static Vector3 ReadVector(ReadOnlySpan<byte> source,int at)=>new(BinaryPrimitives.ReadSingleLittleEndian(source.Slice(at,4)),
        BinaryPrimitives.ReadSingleLittleEndian(source.Slice(at+4,4)),BinaryPrimitives.ReadSingleLittleEndian(source.Slice(at+8,4)));
    private static void Finite(params float[] values){if(values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Original rim preparation requires finite consumed source values and results.");}
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Multiply(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float a,float b)=>a/b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Subtract(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float SquareRoot(float a)=>MathF.Sqrt(a);
}

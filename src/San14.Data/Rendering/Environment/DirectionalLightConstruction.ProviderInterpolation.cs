using System.Buffers.Binary;
using System.Runtime.CompilerServices;
namespace San14.Data;

public static partial class DirectionalLightConstruction
{
    // Temporary root/root selection at weight 0. Field ranges are LightData,
    // not Actor-relative, and are derived from the typed fields of that path.
    internal static byte[] ProviderZeroPairOwned(byte[] source)
    {
        byte[] d=new byte[LightDataBytes];
        // CB6510's only nonzero LightData defaults. The interpolation writes
        // both words, but keeping their constructor values states the lifetime.
        Word(0x2A0,0x46FA0000);Word(0x2A4,0x46FA0000);
        foreach(var (start,end) in new (int,int)[]{
            (0x10,0x1C),(0x30,0x3C),(0x50,0x5C),(0x70,0x7C),
            (0x80,0x9C),(0xA0,0x120),
            (0x1B0,0x1EC),(0x1FC,0x488),(0x4B8,0xAD4),
            (0xAD8,0xB68),(0xB70,0xB94),(0xB98,0xCBC),
            (0xCC0,0xDC8),(0xDCC,0xDD4),(0xDD8,0xDE0),
            (0xDE4,0xE00),(0xE04,0xE2C),(0xE38,0xF1C),
            (0xF30,0xF3C),(0xF40,0xF4C),(0xF80,0xFD8)})
            for(int at=start;at<end;at+=4) {float x=Read(at);Put(at,PAdd(PMul(x,1),PMul(x,0)));}
        foreach(var (start,end) in new (int,int)[]{
            (0x488,0x4B8),(0xAD4,0xAD5),(0xB68,0xB6F),
            (0xB94,0xB95),(0xDC8,0xDC9),(0xDD4,0xDD5),
            (0xDE0,0xDE1),(0xE00,0xE01),(0x10B0,0x10B8)})
            Copy(start,end-start);
        foreach(int at in new[]{0x1C,0x3C,0x5C,0x7C,0x9C,0x1EC,0x1F0,0xCBC})Copy(at,4);
        Copy(0xF1C,16);Copy(0xFE0,0xB0);
        // temp/root both have no external actor +20 data receiver in this path.
        // param5=0 thus supplies a zero fog block, independent of source fog.
        // d[150..190] is already zero, including the integer word160.
        Word(0xFDC,BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(0xFDC,4))|0x80000000u);
        Put(0xFD0,PAdd(PMul(Read(0xFD8),1),PMul(Read(0xFD8),0)));
        // FD8 is not written by interpolation. Its constructor value is zero.
        if(BinaryPrimitives.ReadUInt64LittleEndian(source.AsSpan(0xF68,8))==0)
        {
            // CB6510 default0 retained in F3C/F4C..F64 as well as these handles.
            // A null first reference resets all three references, even if the
            // source's second or third reference contains another raw value.
        }
        else
        {
            Copy(0xF68,24);Copy(0xF3C,4);Copy(0xF58,16);
            for(int at=0xF4C;at<0xF58;at+=4){float x=Read(at);Put(at,PAdd(PMul(x,1),PMul(x,0)));}
        }
        foreach(int at in new[]{0xA0,0xB0,0xC0,0xD0,0xF0})OrdinaryQuaternion(at);
        NormalizedQuaternion(0xF80);NormalizedQuaternion(0xF90);
        return d;
        float Read(int at)=>BinaryPrimitives.ReadSingleLittleEndian(source.AsSpan(at,4));
        void Put(int at,float x)=>BinaryPrimitives.WriteSingleLittleEndian(d.AsSpan(at,4),x);
        void Word(int at,uint x)=>BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(at,4),x);
        void Copy(int at,int n)=>source.AsSpan(at,n).CopyTo(d.AsSpan(at,n));
        void OrdinaryQuaternion(int at)
        {
            float x=Read(at),y=Read(at+4),z=Read(at+8),w=Read(at+12);
            float dot=PAdd(PAdd(PAdd(PMul(y,y),PMul(x,x)),PMul(z,z)),PMul(w,w));
            float a=1,b=0;
            if(!(dot<=-.9f||.9f<=dot))
            {
                float angle=ProviderAcosPolynomial(dot);
                if(1.5707964f<=angle)angle=PSub(3.1415927f,angle);
                float angle2=PMul(angle,angle);
                float b2=PMul(PMul(b,b),angle2),a2=PMul(PMul(a,a),angle2);
                float inverse=PDiv(1,ProviderSinPolynomial(angle2));
                a=PMul(PMul(ProviderSinPolynomial(a2),a),inverse);
                b=PMul(PMul(ProviderSinPolynomial(b2),b),inverse);
            }
            if(0<=dot)
            {
                Put(at,PAdd(PMul(x,b),PMul(x,a)));
                Put(at+4,PAdd(PMul(b,y),PMul(a,y)));
                Put(at+8,PAdd(PMul(a,z),PMul(b,z)));
                Put(at+12,PAdd(PMul(a,w),PMul(b,w)));
            }
            else
            {
                Put(at,PSub(PMul(x,a),PMul(x,b)));
                Put(at+4,PSub(PMul(a,y),PMul(b,y)));
                Put(at+8,PSub(PMul(a,z),PMul(b,z)));
                Put(at+12,PSub(PMul(a,w),PMul(b,w)));
            }
        }
        void NormalizedQuaternion(int at)
        {
            float x=Read(at),y=Read(at+4),z=Read(at+8),w=Read(at+12);
            float dot=PAdd(PAdd(PAdd(PMul(x,x),PMul(y,y)),PMul(z,z)),PMul(w,w));
            if(dot<=-1f||.9f<=dot)
            {
                float a=1,b=0;
                float norm=(float)Math.Sqrt(PSub(1,PMul(PMul(PAdd(a,a),b),PSub(1,dot))));
                float inverse=PDiv(1,norm);a=PMul(a,inverse);b=PMul(inverse,b);
                Put(at,PAdd(PMul(x,b),PMul(x,a)));
                Put(at+4,PAdd(PMul(b,y),PMul(a,y)));
                Put(at+8,PAdd(PMul(a,z),PMul(b,z)));
                Put(at+12,PAdd(PMul(a,w),PMul(b,w)));
            }
            else
            {
                float angle=ProviderAcosPolynomial(dot);
                float inverse=PDiv(1,(float)Math.Sqrt(PAdd(PAdd(dot,dot),2)));
                float cx=PMul(PAdd(x,x),inverse),cy=PMul(PAdd(y,y),inverse);
                float cz=PMul(PAdd(z,z),inverse),cw=PMul(PAdd(w,w),inverse);
                float angle2=PMul(angle,angle);
                float denominator=PAdd(PMul(PMul(PSub(PMul(angle2,.25f),20),angle2),.25f),120);
                float k=PDiv(2,denominator);
                float b2=PMul(PMul(0,0),angle2),a2=PMul(PMul(.5f,.5f),angle2);
                float b=PMul(PMul(ProviderSinPolynomial(b2),0),k);
                float a=PMul(PMul(ProviderSinPolynomial(a2),.5f),k);
                Put(at,PAdd(PMul(x,a),PMul(b,cx)));
                Put(at+4,PAdd(PMul(b,cy),PMul(a,y)));
                Put(at+8,PAdd(PMul(a,z),PMul(b,cz)));
                Put(at+12,PAdd(PMul(a,w),PMul(b,cw)));
            }
        }
    }
    private static float ProviderAcosPolynomial(float dot)
    {
        float z=PMul(dot,dot);
        float a=PAdd(PMul(z,.041791946f),.04464286f);
        a=PAdd(PMul(a,z),.075f);a=PAdd(PMul(a,z),.16666667f);a=PAdd(PMul(a,z),1);
        return PSub(1.5707964f,PMul(a,dot));
    }
    private static float ProviderSinPolynomial(float z)=>PAdd(PMul(PSub(z,20),z),120);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float PMul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float PAdd(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float PSub(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float PDiv(float a,float b)=>a/b;
}

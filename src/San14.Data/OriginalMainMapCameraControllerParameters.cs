using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>File-backed startup parameter tables for the exact original controller.
/// Later native parameter overrides require their explicit current values.</summary>
public sealed class OriginalMainMapCameraControllerParameters
{
    private readonly int[] anchors;
    private readonly Vector4[] minimumFocus,maximumFocus;
    private readonly float[] minimumYaw,maximumYaw,minimumPitch,maximumPitch;
    private readonly double[] bandPitch;
    private OriginalMainMapCameraControllerParameters(string locale,string sha,int[] anchors,
        Vector4[] minimumFocus,Vector4[] maximumFocus,float[] minimumYaw,float[] maximumYaw,
        float[] minimumPitch,float[] maximumPitch,double[] bandPitch)
    {
        Locale=locale;ExecutableSha256=sha;this.anchors=anchors;this.minimumFocus=minimumFocus;
        this.maximumFocus=maximumFocus;this.minimumYaw=minimumYaw;this.maximumYaw=maximumYaw;
        this.minimumPitch=minimumPitch;this.maximumPitch=maximumPitch;this.bandPitch=bandPitch;
    }
    public string Locale {get;}
    public string ExecutableSha256 {get;}
    public float ZoomAnchor(int band) => anchors[CheckBand(band)];
    public double ZoomBandPitch(int band) => bandPitch[CheckBand(band)];
    public double MinimumDistance => ZoomAnchor(0);
    public double MaximumDistance => ZoomAnchor(6);
    public int SourceInertiaTimeoutMilliseconds {get;private init;}=250;

    public static OriginalMainMapCameraControllerParameters LoadSourceDefaults(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string sha=OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory,locale);
        byte[] exe=File.ReadAllBytes(Path.Combine(repository.SourceDirectory,$"SAN14CE_{locale}.exe"));
        if(Convert.ToHexString(SHA256.HashData(exe))!=sha)
            throw new InvalidDataException("Original controller executable changed while reading its parameter tables.");
        int delta=locale switch{"SC"=>0,"TC"=>0x1000,_=>throw new InvalidDataException("Original controller requires exact SC or TC.")};
        byte[] focus=ReadImage(exe,0x1A22000-delta,7*32),yaw=ReadImage(exe,0x1A22100-delta,7*8),
            pitch=ReadImage(exe,0x1A22138-delta,7*8);
        float F(byte[] raw,int off)=>BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(off,4));
        Vector4 V(int at)=>new(F(focus,at),F(focus,at+4),F(focus,at+8),F(focus,at+12));
        int zoomDelta=locale=="TC"?0x1030:0;
        int[] anchors=Enumerable.Range(0,7).Select(i=>BinaryPrimitives.ReadInt32LittleEndian(
            ReadImage(exe,0x1A4B950+i*12-zoomDelta,4))).ToArray();
        if(!anchors.SequenceEqual(new[]{5000,10000,17500,30000,55000,120000,200000}))
            throw new InvalidDataException("Original controller seven anchor table does not match its pinned source profile.");
        // C880 literal binary64 stores; each is the widened source binary32 angle.
        ulong[] words=[0x3FE0C15240000000,0x3FE6571860000000,0x3FE6571860000000,
            0x3FF0C15240000000,0x3FF921FB60000000,0x3FF921FB60000000,0x3FF921FB60000000];
        return new(locale,sha,anchors,Enumerable.Range(0,7).Select(i=>V(i*32)).ToArray(),
            Enumerable.Range(0,7).Select(i=>V(i*32+16)).ToArray(),
            Enumerable.Range(0,7).Select(i=>F(yaw,i*8)).ToArray(),Enumerable.Range(0,7).Select(i=>F(yaw,i*8+4)).ToArray(),
            Enumerable.Range(0,7).Select(i=>F(pitch,i*8)).ToArray(),Enumerable.Range(0,7).Select(i=>F(pitch,i*8+4)).ToArray(),
            words.Select(v=>BitConverter.UInt64BitsToDouble(v)).ToArray()) {
                SourceInertiaTimeoutMilliseconds=BinaryPrimitives.ReadInt32LittleEndian(ReadImage(exe,0x1A4A02C-zoomDelta,4))};
    }

    /// <summary>B370/BF30/BCF0 interpolate each binary32 table value, then widen
    /// angle bounds. The separate current maxY override is explicit.</summary>
    public OriginalMainMapCameraControllerBounds Bounds(OriginalMainMapCameraZoomSelection selection,
        bool currentMaximumYOverride)
    {
        int b=CheckBand(selection.Band1E0),n=Math.Min(b+1,6);
        float t=OriginalMainMapCameraController.DivideFloat(selection.Fraction1E4,10000f);
        float L(float a,float c)=>b==6?a:OriginalMainMapCameraController.InterpolateFloat(a,c,t);
        Vector4 a=minimumFocus[b],c=minimumFocus[n],x=maximumFocus[b],z=maximumFocus[n];
        if(currentMaximumYOverride){x.Y=4000;z.Y=4000;}
        return new(new(L(a.X,c.X),L(a.Y,c.Y),L(a.Z,c.Z),1),
            new(L(x.X,z.X),L(x.Y,z.Y),L(x.Z,z.Z),1),MinimumDistance,MaximumDistance,
            L(minimumYaw[b],minimumYaw[n]),L(maximumYaw[b],maximumYaw[n]),
            L(minimumPitch[b],minimumPitch[n]),L(maximumPitch[b],maximumPitch[n]));
    }
    private static int CheckBand(int b)=>b is>=0 and<=6?b:throw new InvalidDataException("Original controller zoom band must be 0 through 6.");
    private static byte[] ReadImage(byte[] exe,int rva,int count)
    {
        int pe=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(0x3C,4)),n=BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(pe+6,2));
        int table=pe+24+BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(pe+20,2));
        for(int i=0;i<n;i++)
        {
            int at=table+i*40,va=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(at+12,4)),
                size=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(at+16,4)),raw=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(at+20,4));
            if(rva>=va&&rva+count<=va+size)return exe.AsSpan(raw+rva-va,count).ToArray();
        }
        throw new InvalidDataException("Original controller parameter bytes are outside the pinned PE raw sections.");
    }
}

public readonly record struct OriginalMainMapCameraControllerBounds(Vector4 MinimumFocus70,
    Vector4 MaximumFocus80,double MinimumDistance98,double MaximumDistanceA0,double MinimumYawB0,
    double MaximumYawB8,double MinimumPitchC8,double MaximumPitchD0);
public readonly record struct OriginalMainMapCameraZoomSelection(int Band1E0,int Fraction1E4,float NativeReturnedIndex);
public readonly record struct OriginalMainMapCameraControllerRequest(int DurationMilliseconds,
    Vector4 Focus10,double Distance20,double Yaw28,double Pitch30,int Selector38);
public readonly record struct OriginalMainMapCameraZoomTarget(int Band,double Distance,double Pitch,int DurationMilliseconds);

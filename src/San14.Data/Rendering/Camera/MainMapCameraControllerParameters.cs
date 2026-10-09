using System.Numerics;

namespace San14.Data;

/// <summary>Remake camera presets. Later parameter overrides are explicit current inputs.</summary>
public sealed class MainMapCameraControllerParameters
{
    private readonly int[] anchors;
    private readonly Vector4[] minimumFocus,maximumFocus;
    private readonly float[] minimumYaw,maximumYaw,minimumPitch,maximumPitch;
    private readonly double[] bandPitch;
    private MainMapCameraControllerParameters(string locale,int[] anchors,
        Vector4[] minimumFocus,Vector4[] maximumFocus,float[] minimumYaw,float[] maximumYaw,
        float[] minimumPitch,float[] maximumPitch,double[] bandPitch)
    {
        Locale=locale;this.anchors=anchors;this.minimumFocus=minimumFocus;
        this.maximumFocus=maximumFocus;this.minimumYaw=minimumYaw;this.maximumYaw=maximumYaw;
        this.minimumPitch=minimumPitch;this.maximumPitch=maximumPitch;this.bandPitch=bandPitch;
    }
    public string Locale {get;}
    public string RuleVersion => MainMapCameraRules.Version;
    public float ZoomAnchor(int band) => anchors[CheckBand(band)];
    public double ZoomBandPitch(int band) => bandPitch[CheckBand(band)];
    public double MinimumDistance => ZoomAnchor(0);
    public double MaximumDistance => ZoomAnchor(6);
    public int SourceInertiaTimeoutMilliseconds {get;private init;}=250;

    public static MainMapCameraControllerParameters LoadSourceDefaults(GameResourceRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(locale);
    }

    public static MainMapCameraControllerParameters CreateDefaults(string locale)
    {
        static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
        float pi=F(0x40490FDB),halfPi=F(0x3FC90FDB),twoPi=F(0x40C90FDB);
        return new(MainMapCameraRules.Language(locale),[5000,10000,17500,30000,55000,120000,200000],
            [new(40000,0,14000,1),new(38000,0,13000,1),new(43000,0,17500,1),
             new(45000,0,24000,1),new(60000,0,30000,1),new(110000,0,74100,1),new(112400,0,115400,1)],
            [new(192571,0,193430,1),new(192571,0,193430,1),new(190000,0,190000,1),
             new(190000,0,190000,1),new(175000,0,173000,1),new(110000,0,130000,1),new(112400,0,115400,1)],
            [0,0,0,0,pi,pi,pi],[twoPi,twoPi,twoPi,twoPi,pi,pi,pi],
            [F(0x3E44980A),F(0x3EB2B8C3),F(0x3F32B8C3),F(0x3F860A92),halfPi,halfPi,halfPi],
            [halfPi,halfPi,halfPi,halfPi,halfPi,halfPi,halfPi],
            [F(0x3F060A92),F(0x3F32B8C3),F(0x3F32B8C3),F(0x3F860A92),halfPi,halfPi,halfPi]);
    }

    /// <summary>B370/BF30/BCF0 interpolate each binary32 table value, then widen
    /// angle bounds. The separate current maxY override is explicit.</summary>
    public MainMapCameraControllerBounds Bounds(MainMapCameraZoomSelection selection,
        bool currentMaximumYOverride)
    {
        int b=CheckBand(selection.Band1E0),n=Math.Min(b+1,6);
        float t=MapCameraController.DivideFloat(selection.Fraction1E4,10000f);
        float L(float a,float c)=>b==6?a:MapCameraController.InterpolateFloat(a,c,t);
        Vector4 a=minimumFocus[b],c=minimumFocus[n],x=maximumFocus[b],z=maximumFocus[n];
        if(currentMaximumYOverride){x.Y=4000;z.Y=4000;}
        return new(new(L(a.X,c.X),L(a.Y,c.Y),L(a.Z,c.Z),1),
            new(L(x.X,z.X),L(x.Y,z.Y),L(x.Z,z.Z),1),MinimumDistance,MaximumDistance,
            L(minimumYaw[b],minimumYaw[n]),L(maximumYaw[b],maximumYaw[n]),
            L(minimumPitch[b],minimumPitch[n]),L(maximumPitch[b],maximumPitch[n]));
    }
    private static int CheckBand(int b)=>b is>=0 and<=6?b:throw new InvalidDataException("Original controller zoom band must be 0 through 6.");
}

public readonly record struct MainMapCameraControllerBounds(Vector4 MinimumFocus70,
    Vector4 MaximumFocus80,double MinimumDistance98,double MaximumDistanceA0,double MinimumYawB0,
    double MaximumYawB8,double MinimumPitchC8,double MaximumPitchD0);
public readonly record struct MainMapCameraZoomSelection(int Band1E0,int Fraction1E4,float NativeReturnedIndex);
public readonly record struct MainMapCameraControllerRequest(int DurationMilliseconds,
    Vector4 Focus10,double Distance20,double Yaw28,double Pitch30,int Selector38);
public readonly record struct MainMapCameraZoomTarget(int Band,double Distance,double Pitch,int DurationMilliseconds);

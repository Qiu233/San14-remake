using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>One selected View Camera after its actual package/current writers.
/// Complete Sky2/controller retained state is preserved outside the exact
/// D7CD91/D7CE32..D7CEAD setter fields.</summary>
public sealed class SkySceneCurrentCameraParameters
{
    private readonly byte[] prefix;
    internal SkySceneCurrentCameraParameters(long frame,string owner,byte[] prefix,
        SkySceneControllerSourceProfile controller,SkySceneSky2Parameters parameters)
    {FrameNumber=frame;CameraOwnerProvenance=owner;this.prefix=prefix;Controller=controller;Parameters=parameters;}
    public long FrameNumber{get;}
    public string CameraOwnerProvenance{get;}
    public ReadOnlyMemory<byte> PreparedCameraPrefix=>prefix;
    public SkySceneControllerSourceProfile Controller{get;}
    public SkySceneSky2Parameters Parameters{get;}
}

/// <summary>Native selected View Camera→current Sky geometry/phase setters.
/// This does not select a seasonal child or infer a current Camera from a
/// declaration. Package application and Main17BD0 post-writers precede it.</summary>
public static class SkySceneCurrentCameraProducer
{
    public const string SelectedViewContract="TC-D7CD91-B992A0-D7CE32-D7CE6C-D7CE89-D7CE9D-D7CEAD";

    public static SkySceneCurrentCameraParameters PrepareSelectedView(long frameNumber,
        ReadOnlySpan<byte> completeCurrentPrefix,string currentCameraOwnerProvenance,
        SkySceneControllerSourceProfile retainedController,
        SkySceneSky2Parameters retainedSky2Parameters)
    {
        ArgumentNullException.ThrowIfNull(retainedController);ArgumentNullException.ThrowIfNull(retainedSky2Parameters);
        if(frameNumber<1||completeCurrentPrefix.Length!=PreparedCameraLighting.PrefixByteLength||
            string.IsNullOrWhiteSpace(currentCameraOwnerProvenance)||string.IsNullOrWhiteSpace(retainedController.SourceProvenance)||
            string.IsNullOrWhiteSpace(retainedSky2Parameters.SourceProvenance))
            throw new InvalidDataException("Current Sky setters require the same selected View frame and complete explicit Camera/Sky state.");
        // B992A0 copies the seven Camera words unchanged. D7 uses MAXSS with
        // zero for DateTime; D3's separate provider path does not share this gate.
        var sky=SkylightParameters.ReadSourceFields(completeCurrentPrefix.Slice(0x200,28));
        float degrees=BitConverter.UInt32BitsToSingle(0x3C8EFA35),scale=.01f;
        float longitude=M(sky.Longitude,degrees),latitude=M(sky.Latitude,degrees),north=M(sky.NorthDir,degrees);
        float altitude=M(sky.Altitude,1000f),moon=sky.MoonAge<0?0:sky.MoonAge>=1?1:sky.MoonAge;
        foreach(float value in new[]{longitude,latitude,north,altitude})
            if(!float.IsFinite(value))throw new InvalidDataException("Current Sky geometry setter overflowed its finite source domain.");
        string evidence=currentCameraOwnerProvenance+$";frame={frameNumber};{SelectedViewContract}";
        var controller=retainedController with{SourceProvenance=retainedController.SourceProvenance+";"+evidence,
            Scalar38=scale,MilkyStrength68=M(scale,BitConverter.UInt32BitsToSingle(0x3D8ACF4C)),
            MonthDay48=sky.MonthDay,DateTime4C=sky.DateTime>0?sky.DateTime:0,MoonAge70=moon,MoonObliquity74=0};
        var parameters=retainedSky2Parameters with{SourceProvenance=retainedSky2Parameters.SourceProvenance+";"+evidence,
            Angle1430=longitude,Angle1434=latitude,Angle143C=north,SkyYOffset1438=altitude,
            MilkyStrength1484=controller.MilkyStrength68};
        return new(frameNumber,currentCameraOwnerProvenance,completeCurrentPrefix.ToArray(),controller,parameters);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
}

using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed class OriginalMapSeasonMainCameraPhaseSource
{
    private OriginalMapSeasonMainCameraPhaseSource(string locale) => Locale=OriginalMainMapCameraRules.Language(locale);
    public string Locale {get;}
    public string RuleVersion => OriginalMainMapCameraRules.Version;
    public int StartPhaseNumerator => 7635;
    public int EndPhaseNumerator => 7650;
    public int Denominator => 10000;
    public string SourceProvenance => $"{RuleVersion};{Locale};zoom-phase={StartPhaseNumerator}/{EndPhaseNumerator}/{Denominator}";
    public static OriginalMapSeasonMainCameraPhaseSource CreateDefaults(string locale) => new(locale);
    public static OriginalMapSeasonMainCameraPhaseSource LoadSourceDefaults(OriginalGameRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(locale);
    }
}

public sealed record OriginalMapSeasonMainCameraPhaseResult(OriginalSkylightParameters Parameters,
    OriginalCameraLightingController LightingController,bool ZoomPhaseWritten,float EcB0Index,string SourceProvenance);

/// <summary>Exact Main17BD0 writes after external V468 package application and
/// before V418. Native ECb0 owner fields remain explicit; a controller's EAB0
/// result is used only when its actual optional-owner branch is selected.</summary>
public static class OriginalMapSeasonMainCameraPhase
{
    public static float FromOwnerZoomFields(int ownerBand230,int ownerFraction234)
    {
        if(ownerBand230 is <0 or >6||ownerFraction234 is <0 or >10000)
            throw new InvalidDataException("Native ECb0 primary owner requires its actual finite current230/234 fields.");
        return A(ownerBand230,D(ownerFraction234,10000f));
    }
    public static float FromOptionalController(OriginalMainMapCameraControllerParameters sourceParameters,
        float currentControllerScalar24,double currentDistance90)
    {
        ArgumentNullException.ThrowIfNull(sourceParameters);
        if(!float.IsFinite(currentControllerScalar24)||!double.IsFinite(currentDistance90))
            throw new InvalidDataException("Native ECb0 optional controller fields must be finite.");
        if(sourceParameters.ZoomAnchor(0)>=currentControllerScalar24)return 0;
        return OriginalMainMapCameraController.ClassifyZoom(sourceParameters,currentDistance90).NativeReturnedIndex;
    }
    public static OriginalMapSeasonMainCameraPhaseResult ApplyAfterPackage(
        OriginalSkylightParameters appliedPackageParameters,OriginalCameraLightingController appliedPackageController,
        int currentOwnerMode288,float actualEcB0Index,OriginalMapSeasonMainCameraPhaseSource source,
        string currentOwnerProvenance)
    {
        ArgumentNullException.ThrowIfNull(appliedPackageParameters);ArgumentNullException.ThrowIfNull(appliedPackageController);
        ArgumentNullException.ThrowIfNull(source);
        if(!float.IsFinite(actualEcB0Index)||string.IsNullOrWhiteSpace(currentOwnerProvenance))
            throw new InvalidDataException("Main phase post-writer requires actual current native ECb0 and owner provenance.");
        bool written=currentOwnerMode288>=3;
        var p=appliedPackageParameters;
        // CPU74/78 are Light/DarkAdaptedScale, not controller154/CC.
        // Their prepared writes are applied separately to Camera1C4/1C8.
        var controller=appliedPackageController;
        if(written)
        {
            float t=S(actualEcB0Index,5f),start=D(source.StartPhaseNumerator,source.Denominator),end=D(source.EndPhaseNumerator,source.Denominator);
            float phase=A(M(start,S(1f,t)),M(end,t));
            if(!float.IsFinite(phase))throw new InvalidDataException("Main phase current writer overflowed its finite source domain.");
            p=p with{DateTime=phase};
        }
        return new(p,controller,written,actualEcB0Index,source.SourceProvenance+";"+currentOwnerProvenance+
            $";V468-package-before-17E09;owner288={currentOwnerMode288};V418-after-post-writes");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}

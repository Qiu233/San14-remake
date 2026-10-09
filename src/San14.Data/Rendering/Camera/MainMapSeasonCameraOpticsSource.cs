using System.Buffers.Binary;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Loaded direct selected Camera optics for the original V468 optional
/// FOV -> V410 path. Source lookup is performed at load; ComputeCurrent uses
/// the retained descriptors and native math, before geometry/LOD publication.</summary>
public sealed class LoadedMainMapSeasonCameraOpticsSource
{
    private readonly float selectedFov, computedFov;
    private readonly bool lensPresetResolved, hdrLutResolved;

    private LoadedMainMapSeasonCameraOpticsSource(string locale,string evidence,
        uint camera,uint lens,uint hdr,uint extension,float distance,float focal,float aspect,
        float selectedFov,bool lensResolved,bool hdrResolved,float film,float exponent,
        MapPostEffect3LensPresetIntermediateMathSources math)
    {
        Locale=locale;SourceEvidence=evidence;SelectedCameraObjectId=camera;
        LensPresetObjectKey285=lens;HdrLutObjectKey932=hdr;SelectedExtensionFlag1078=extension;
        ObjectDistance1048=distance;InfiniteFocalLength104C=focal;Aspect12EC=aspect;
        this.selectedFov=selectedFov;lensPresetResolved=lensResolved;hdrLutResolved=hdrResolved;
        computedFov=hdrResolved||lensResolved?ComputeOpticalFov(distance,focal,aspect,film,exponent,math):selectedFov;
    }

    public string Locale{get;}
    public string SourceEvidence{get;}
    public uint SelectedCameraObjectId{get;}
    public uint LensPresetObjectKey285{get;}
    public uint HdrLutObjectKey932{get;}
    public uint SelectedExtensionFlag1078{get;}
    public float ObjectDistance1048{get;}
    public float InfiniteFocalLength104C{get;}
    public float Aspect12EC{get;}
    public bool HdrLutWrapperResolved=>hdrLutResolved;
    public bool LensPresetPayloadResolved=>lensPresetResolved;
    // Source Main constructor TC C3E1AF / SC C3F85F writes this independent
    // CPU word. It is not a DOK property or prepared Camera relative1324.
    public const uint FreshMainSource1324=1;
    public const uint OrdinaryMainV468CallerFlags=8;

    /// <summary>Create a successful owned source-resolution scope. Different
    /// season resources or a changed direct package require loading a new
    /// descriptor. No original process/GPU readiness is inferred.</summary>
    public static LoadedMainMapSeasonCameraOpticsSource Load(
        GameResourceRepository repository,string locale,MapSeasonCameraSelection selection)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(selection);
        selection.Validate(); // source boundary only, never ComputeCurrent.
        if(selection.SecondPackage is not null)
            throw new NotSupportedException("Main optics requires the actual direct selected Camera; blended geometry has a separate source producer.");
        var package=selection.FirstPackage;
        if(package.Camera.ClassId!=MapCameraConstructionSource.PerspectiveCameraClassId)
            throw new NotSupportedException("Main selected optics currently implements the original perspective Camera branch.");
        var camera=package.Camera;
        uint Word(int ordinal,uint hash,uint type)
        {
            var field=camera.Properties[ordinal];
            if(field.Index!=ordinal||field.NameHash!=hash||field.TypeCode!=type||
                field.ElementCount!=1||field.ElementSize!=4||field.RawValue.Length!=4)
                throw new InvalidDataException($"Selected Camera optics property{ordinal} differs from its original loader binding.");
            return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
        float Float(int ordinal,uint hash)=>BitConverter.UInt32BitsToSingle(Word(ordinal,hash,8));
        uint lens=Word(285,0xA4541BE1,5),hdr=Word(932,0x2572A4E9,5);
        uint extension=Word(315,0xC651B23A,4);
        float distance=Float(274,0xEE9D93F9),focal=Float(275,0x995A7B0E),
            aspect=Float(6,0xA70685F8),fov=Float(5,0xBA7543EA);
        bool hdrResolved=false,lensResolved=false;
        string evidence=selection.SourceEvidence+";owned-V468-direct-selected-optics;";
        if(hdr!=0)
        {
            // Native C57C30 stores the successful593 CPU wrapper; CB30B0
            // only checks this pointer. A GPU child upload is not its gate.
            var definitions=repository.LoadObjects("KIDSSystemResource",0xD2ECCCDF);
            var resolved=MapPostEffect3LutSource.Load(repository,definitions,"KIDSSystemResource",hdr);
            hdrResolved=true;evidence+=resolved.SourceEvidence+";source2EC8-wrapper-resolved;";
        }
        if(lens!=0)
        {
            if(lens!=0x3D60C3C7)
                throw new NotSupportedException("Selected Main optics needs its declared LensPreset payload producer.");
            var definitions=repository.LoadObjects("system",0xDAB638B2);
            var matches=definitions.Document.Objects.Where(o=>o.ObjectId==lens).ToArray();
            if(definitions.Source.DecodedSha256!=MapPostEffect3LensPresetSource.ResourceSha256||
                matches.Length!=1||matches[0].Kind!=DokObjectKind.Inline||
                matches[0].ClassId!=0x162FB139||matches[0].Properties.Count!=29)
                throw new InvalidDataException("Selected Main optics LensPreset source differs from its original class162FB139.");
            foreach(var(index,hash)in new[]{(27,0x29B2A52Eu),(28,0x081439EAu)})
            {
                var field=matches[0].Properties[index];
                if(field.Index!=index||field.NameHash!=hash||field.TypeCode!=4||field.RawValue.Length!=4||
                    BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue)!=0)
                    throw new NotSupportedException("Selected optics supports the actual original LensPreset builtin0/flags0 payload.");
            }
            lensResolved=true;evidence+=definitions.Source.ResourceUri+"#3D60C3C7;owned-builtin0-payload-resolved;";
        }
        byte[] builtin=MapPostEffect3LensPresetBuiltin0.CopyPrimitive();
        float film=BinaryPrimitives.ReadSingleLittleEndian(builtin.AsSpan(4,4)),
            power=BinaryPrimitives.ReadSingleLittleEndian(builtin.AsSpan(0x40,4));
        var math=MapPostEffect3LensPresetIntermediateMathSources.Load(repository,locale);
        return new(locale,evidence+math.SourceEvidence+
            ";TC-CB30B0/1148060/11480E0/CB37C0/C408D0;geometryFOV-before-capture;not-far-writer",
            camera.ObjectId,lens,hdr,extension,distance,focal,aspect,fov,lensResolved,hdrResolved,film,power,math);
    }

    /// <summary>Return the actual V410 sourceCPU12E8 store, or null when its
    /// gate suppresses the store. The caller applies App290 afterwards and
    /// rebuilds the same geometry/LOD/current Camera matrices. All four words
    /// are explicit current inputs; frame ordinal never selects a branch.</summary>
    public float? ComputeCurrent(uint mainSource1324,uint mainPreparedFlags1488,
        uint mainExtensionFlag1078,uint callerFlags)
    {
        if((mainSource1324&1)==0)return null; // Main.V4B0: optional scalar and lateV410 gate.
        uint extensionOverride=(callerFlags&4)!=0?mainExtensionFlag1078:0;
        if((extensionOverride&1)!=0)return null; // CB37AB: no V410.
        if((mainPreparedFlags1488&1)!=0)return null; // V410 orthographic keepsCPU12E8.
        if(!hdrLutResolved&&!lensPresetResolved)return selectedFov; // CB31D5.
        return computedFov;
    }

    private static float ComputeOpticalFov(float sourceDistance,float sourceFocal,float sourceAspect,
        float diagonalFilm,float exponent,MapPostEffect3LensPresetIntermediateMathSources math)
    {
        float distance=M(sourceDistance,math.DistanceUnitScale),
            focal=M(sourceFocal,math.DistanceUnitScale),
            magnification=D(distance,S(distance,focal));
        float power=SkyWeatherNativePower.PowSse(magnification,exponent,math.Power);
        float effectiveFocal=M(power,focal),twiceFocal=A(effectiveFocal,effectiveFocal);
        float diagonal=Sse.SqrtScalar(Vector128.CreateScalar(A(M(sourceAspect,sourceAspect),1))).ToScalar();
        float angle=D(D(diagonalFilm,diagonal),twiceFocal);
        return M(NativeSkyAtan.Atan1032034(angle,math.Atan),2);
    }

    private static float M(float x,float y)=>Sse.MultiplyScalar(Vector128.CreateScalar(x),Vector128.CreateScalar(y)).ToScalar();
    private static float A(float x,float y)=>Sse.AddScalar(Vector128.CreateScalar(x),Vector128.CreateScalar(y)).ToScalar();
    private static float S(float x,float y)=>Sse.SubtractScalar(Vector128.CreateScalar(x),Vector128.CreateScalar(y)).ToScalar();
    private static float D(float x,float y)=>Sse.DivideScalar(Vector128.CreateScalar(x),Vector128.CreateScalar(y)).ToScalar();
}

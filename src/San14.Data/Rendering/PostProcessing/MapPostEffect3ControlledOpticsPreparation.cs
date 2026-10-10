using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Owned CPU result of the conditional . The return AL1
/// is the complete literal writer, independent of named-setter results.
/// The actual ghost buffer allocation/upload remains its own device producer.</summary>
public sealed class MapPostEffect3ControlledOpticsPreparation
{
    private readonly byte[] allGhosts,selectedGhosts;
    private byte[] initialGhosts=[];
    private IReadOnlyList<MapPostEffect3ApplyValueWrite> initialWrites=Array.Empty<MapPostEffect3ApplyValueWrite>();
    private MapPostEffect3ParameterState? appliedState;
    private MapPostEffect3AllocationPlan? appliedAllocation;
    private bool initializationApplied;
    private bool currentApplied;
    private readonly HashSet<string> absentScalarNames=new(StringComparer.Ordinal);
    internal MapPostEffect3ControlledOpticsPreparation(long frame,string owner,
        bool dirty,IReadOnlyList<MapPostEffect3ApplyValueWrite> writes,byte[] all,byte[] selected,
        int ghostCount,float distance,float focal,float stop,string evidence,string locale)
    {FrameNumber=frame;OwnerIdentity=owner;ActualDirtyAfBeforePreparation=dirty;Writes=writes;allGhosts=all;selectedGhosts=selected;
        GhostSpriteCount=ghostCount;ObjectDistance60=distance;FocalLength64=focal;FStop68=stop;SourceEvidence=evidence;Locale=locale;}
    public long FrameNumber{get;}
    public string OwnerIdentity{get;}
    public bool ActualDirtyAfBeforePreparation{get;}
    public byte DirtyAfAfterPreparation=>0;
    public byte NativePreparationReturnAl=>1;
    public string Locale{get;}
    public IReadOnlyList<MapPostEffect3ApplyValueWrite> Writes{get;}
    public IReadOnlyList<MapPostEffect3ApplyValueWrite> InitializationWrites=>initialWrites;
    public IReadOnlyList<string> ExplicitlyAbsentScalarNames=>Array.AsReadOnly(absentScalarNames.Order().ToArray());
    public int GhostSpriteCount{get;}
    public int NativeGhostStride=>28;
    public int NativeGhostAllocationByteCount=>448;
    public ReadOnlyMemory<byte> AllGhostRecords440=>allGhosts.ToArray();
    public ReadOnlyMemory<byte> SelectedGhostRecords640=>selectedGhosts.ToArray();
    /// <summary> prepares and stages16 source rows before current
    /// Camera writes. A later count0 upload does not clear these device bytes.</summary>
    public ReadOnlyMemory<byte> InitialGhostRecords640=>initialGhosts.ToArray();
    public int ActualGhostUploadByteCount=>selectedGhosts.Length;
    public float ObjectDistance60{get;}
    public float FocalLength64{get;}
    public float FStop68{get;}
    public string SourceEvidence{get;}
    public bool IsCompleteCpuOpticsPreparation=>true;
    public bool ProvesGpuTextureOrBufferReady=>false;
    internal void AcceptFreshInitialization(MapPostEffect3ControlledOpticsPreparation initial,
        IReadOnlyList<MapPostEffect3ApplyValueWrite> resetWrites)
    {
        initialGhosts=initial.SelectedGhostRecords640.ToArray();
        initialWrites=Array.AsReadOnly(resetWrites.Concat(initial.Writes).Select(w=>w.CopyAcceptedSnapshot()).ToArray());
    }
    internal static MapPostEffect3ControlledOpticsPreparation RetainUnchanged(
        MapPostEffect3ControlledOpticsPreparation previous,MapPostEffect3ParameterState state,
        MapPostEffect3AllocationPlan allocation)
    {
        const string retainedMarker=";retained-controller-same-optics-setters/AF0";
        int previousMarker=previous.SourceEvidence.IndexOf(retainedMarker,StringComparison.Ordinal);
        string originalEvidence=previousMarker<0?previous.SourceEvidence:previous.SourceEvidence[..previousMarker];
        var current=new MapPostEffect3ControlledOpticsPreparation(state.FrameNumber,state.OwnerIdentity,false,
            Array.Empty<MapPostEffect3ApplyValueWrite>(),previous.allGhosts.ToArray(),previous.selectedGhosts.ToArray(),
            previous.GhostSpriteCount,previous.ObjectDistance60,previous.FocalLength64,previous.FStop68,
            originalEvidence+retainedMarker+";retained-optics-without-reinitialization;currentFrame="+state.FrameNumber,previous.Locale);
        current.initialGhosts=previous.initialGhosts.ToArray();
        current.RequireOwner(state,allocation);current.initializationApplied=true;current.currentApplied=true;
        current.appliedState=state;current.appliedAllocation=allocation;return current;
    }
    public void ApplyInitializationToCurrentState(MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation)
    {
        if(initializationApplied||appliedState is not null)throw new InvalidOperationException("Controlled initialization was already applied.");
        RequireOwner(state,allocation);ApplySourceNamedWrites(state,InitializationWrites,SourceEvidence+"ControlledOpticsPreparation");
        initializationApplied=true;appliedState=state;appliedAllocation=allocation;
    }
    public void ApplyToCurrentState(MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation)
    {
        if(!initializationApplied||currentApplied||!ReferenceEquals(state,appliedState)||!ReferenceEquals(allocation,appliedAllocation))
            throw new InvalidOperationException("Controlled current optics needs this actual fresh initialization first.");
        RequireOwner(state,allocation);ApplySourceNamedWrites(state,Writes,SourceEvidence);
        currentApplied=true;
    }
    public void ValidateCurrent(MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation)
    {
        if(!currentApplied||!ReferenceEquals(state,appliedState)||!ReferenceEquals(allocation,appliedAllocation))
            throw new InvalidDataException("Controlled preparation return is not from this applied scalar bank/allocation.");
        RequireOwner(state,allocation);
    }
    public void ValidateAppliedState(MapPostEffect3ParameterState state)
    {
        if(appliedAllocation is null)throw new InvalidDataException("Controlled preparation has no actual applied bank.");
        ValidateCurrent(state,appliedAllocation);
    }
    private void RequireOwner(MapPostEffect3ParameterState state,MapPostEffect3AllocationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        if(state.FrameNumber!=FrameNumber||state.OwnerIdentity!=OwnerIdentity||allocation.ContextOwnerIdentity!=OwnerIdentity||
            state.SourceSha256!=allocation.Source.Sha256)
            throw new InvalidDataException("Controlled preparation return and optics belong to another scalar owner/frame.");
        state.BindOwnedAllocation(allocation);
    }
    private void ApplySourceNamedWrites(MapPostEffect3ParameterState state,
        IReadOnlyList<MapPostEffect3ApplyValueWrite> writes,string evidence)
    {
        // Every complete native body calls and ignores a missing
        // primary named scalar. FlareScreenDhAspect is actually absent from
        // this pinned type5 bank; a missing slot must not become an alias.
        var accepted=new List<MapPostEffect3ApplyValueWrite>();
        foreach(var write in writes)
        {
            if(state.FindScalarIndex(write.Name) is not int index){absentScalarNames.Add(write.Name);continue;}
            if(state.Declarations[index].ValueByteCount!=write.Bytes.Length)
                throw new InvalidDataException("Controlled source named writer differs from its actual primary declaration size: "+write.Name);
            accepted.Add(write);
        }
        state.ApplyKnownCameraWrites(new(FrameNumber,accepted.AsReadOnly(),evidence));
    }
}

public static class MapPostEffect3ControlledOpticsSource
{
    public static LoadedMapPostEffect3ControlledOpticsSource Load(GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.ReadControlledOpticsSource(
            RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale),SkyWeatherNativePowerSource.Load(repository,locale),
            NativeSkyAtanSource.Load(repository.SourceDirectory,locale),NativeCameraTangentSource.Load(repository,locale),
            NativeLogTables.Load(repository.SourceDirectory,locale));
    }
}

/// <summary>Actual source fresh controller optics followed by the current
/// Camera setters. runs the constructor's once and clears AF;
/// current setters dirty it by native float comparisons. Only fields read by
/// these complete bodies are populated; unconsumed padding is never invented.</summary>
public sealed class LoadedMapPostEffect3ControlledOpticsSource
{
    private readonly SkyWeatherNativePowerSource power;
    private readonly LoadedNativeSkyAtanSource atan;
    private readonly LoadedNativeCameraTangentSource tangent;
    private readonly NativeLogTables log;
    private const float StarFactor=3.8e-7f,StarSize=1024,DistanceScale=.01f,FocalScale=.001f;
    internal LoadedMapPostEffect3ControlledOpticsSource(LoadedDeferredSamplerNativeSource native,
        SkyWeatherNativePowerSource power,LoadedNativeSkyAtanSource atan,
        LoadedNativeCameraTangentSource tangent,NativeLogTables log,string evidence)
    {
        Native=native;this.power=power;this.atan=atan;this.tangent=tangent;this.log=log;
        SourceEvidence=evidence;
    }
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}

    public MapPostEffect3ControlledOpticsPreparation PrepareFreshCurrentCamera(
        MapCameraConstructionPacket camera,MapPostEffect3ApplyDerivedScalarPlan dimensions,
        string actualControllerScalarOwner,NativeLogDispatch actualOwnedLogDispatch)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(dimensions);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualControllerScalarOwner);
        if(camera.ExecutableLocale!=Native.Locale||dimensions.ExecutableLocale!=Native.Locale||camera.FrameNumber!=dimensions.FrameNumber||
            dimensions.Dimensions.OwnerIdentity!=actualControllerScalarOwner||actualOwnedLogDispatch!=NativeLogDispatch.Sse)
            throw new NotSupportedException("Fresh controlled optics requires the same-locale source owner/frame and explicitly owned SSE log dispatch.");
        var raw=MapPostEffect3ApplyUncontrolledCameraInputs.ConstructFreshSource(camera,actualControllerScalarOwner+"/controlled-optics-fields");
        var tail=MapPostEffect3ApplyFreshCameraScalars.ConstructFreshSource(camera,actualControllerScalarOwner+"/controlled-optics-tail");
        float distance=M(raw.Float(0x12FC),DistanceScale);
        // performs Camera1300*10 before applies .001. Retain
        // both binary32 multiplications instead of combining their factors.
        float focal=M(M(raw.Float(0x1300),10),FocalScale),stop=raw.Float(0x1304);
        float limbStart=raw.Float(0x7A8);
        float limbEnd=A(limbStart,raw.Float(0x7AC));
        bool anamorphic=raw.Word(0x131C)!=0;
        bool dirty=distance!=1||focal!=B(0x3D4CCCCD)||stop!=B(0x40B33333)||anamorphic||
            dimensions.ApplyWrites.Any(w=>w.Name is "Enable4KResFix" or "EnableMultiResFix")||
            limbStart!=B(0x3F4CCCCD)||limbEnd!=B(0x3F8CCCCD);
        // The source fresh route's sets DC/AF, clears AF.
        // Native current Camera setters are the only subsequent dirty writers
        // consumed here. These finite source fields require no retained state.
        if(new[]{distance,focal,stop,limbStart,limbEnd}.Any(x=>!float.IsFinite(x))||distance<=0||focal<=0||stop<=0||distance<=focal)
            throw new NotSupportedException("Controlled optics left its actual finite positive object/focal/FStop source domain.");
        float initialWidth=dimensions.Dimensions.DeviceWidth,initialHeight=dimensions.Dimensions.DeviceHeight;
        float scaledWidth=M(B(0x40166666),initialWidth);
        float initialDiagonal=MathF.Sqrt(A(SkyWeatherNativePower.PowSse(scaledWidth,2,power),
            SkyWeatherNativePower.PowSse(initialHeight,2,power)));
        var initialDimensions=dimensions with{EffectiveWidth80=initialWidth,EffectiveHeight84=initialHeight,
            AspectRatioF4=D(dimensions.Dimensions.DeviceAspect94,B(0x40166666)),SensorHeightRatioF0=D(initialHeight,initialDiagonal),
            ApplyWrites=Array.Empty<MapPostEffect3ApplyValueWrite>()};
        var initial=Calculate(camera.FrameNumber,actualControllerScalarOwner,1,B(0x3D4CCCCD),B(0x40B33333),
            B(0x3F4CCCCD),B(0x3F8CCCCD),0,16,false,initialDimensions,actualOwnedLogDispatch,true);
        var current=Calculate(camera.FrameNumber,actualControllerScalarOwner,distance,focal,stop,limbStart,limbEnd,
            camera.Optics.Limb.Weight,unchecked((int)tail.Settings.LensEffects.GhostNum),anamorphic,
            dimensions,actualOwnedLogDispatch,dirty);
        current.AcceptFreshInitialization(initial,Array.AsReadOnly(new[]{
            new MapPostEffect3ApplyValueWrite("HaloAberrationWavesRateRB",MapPostEffect3WriteStage.OpticsHaloAberrationWavesRateRB,PackFloats(.98f,1.02f),SourceEvidence+";fresh-two-float-writer"),
            new MapPostEffect3ApplyValueWrite("AnamorphicCoatColor",MapPostEffect3WriteStage.OpticsAnamorphicCoatColor,PackFloats(.000075f,.000125f,.0005f),SourceEvidence+";fresh-three-float-writer")}));
        return current;
    }

    private MapPostEffect3ControlledOpticsPreparation Calculate(long frame,string owner,float distance,float focal,float stop,
        float limbStart,float limbEnd,float limbWeight,int requestedGhosts,bool anamorphic,
        MapPostEffect3ApplyDerivedScalarPlan dimensions,NativeLogDispatch dispatch,bool dirty)
    {
        float Log(float x)=>NativeLogarithm.Log(x,log,dispatch);
        float Pow(float x,float e)=>SkyWeatherNativePower.PowSse(x,e,power);
        float Atan(float x)=>NativeSkyAtan.Atan1032034(x,atan);
        float Tan(float x)=>NativeCameraTangent.Tangent1020A90(x,tangent,NativeCameraTangentDispatch.Sse);
        float const10=B(0x3D313BE2),const14=B(0x3CC49BA6),const18=B(0x3C9374BC),const1C=B(0x40266666),const20=B(0x3FB33333);
        float focusRatio=D(distance,S(distance,focal)),powerB4=Pow(focusRatio,-1),b8=M(powerB4,focal),b0=D(b8,focusRatio);
        float diagonalRatio=D(dimensions.Dimensions.Diagonal98,dimensions.Dimensions.DeviceHeight),deviceAspect=dimensions.Dimensions.DeviceAspect94;
        float d4=S(1,M(S(1,D(S(b0,const18),S(const1C,const18))),S(1,B(0x3E6B851F))));
        float angleInput=D(D(const10,A(b8,b8)),diagonalRatio);
        float c8=M(Atan(angleInput),2),cc=M(Atan(M(angleInput,deviceAspect)),2);
        float lensDistance=D(M(stop,distance),S(distance,b0));
        float d0=D(const20,lensDistance);
        float lensScale=M(M(S(MathF.Max(distance,b0),b0),lensDistance),const14);lensScale=A(lensScale,lensScale);
        float dofCoefficient=D(M(b0,b0),lensScale);
        if((dimensions.EffectiveWidth80!=dimensions.Dimensions.DeviceWidth)||(dimensions.EffectiveHeight84!=dimensions.Dimensions.DeviceHeight)||
            dimensions.ApplyWrites.Any(w=>w.Name=="Enable4KResFix"&&BinaryPrimitives.ReadInt32LittleEndian(w.Bytes.Span)!=0))
        {
            // Only controllerFC (4KResFix) enables this multiplication. FD's
            // separate multi-resolution fix does not change the coefficient.
            bool fc=dimensions.ApplyWrites.Any(w=>w.Name=="Enable4KResFix"&&BinaryPrimitives.ReadInt32LittleEndian(w.Bytes.Span)!=0);
            if(fc)dofCoefficient=M(dofCoefficient,D(1080,dimensions.EffectiveHeight84));
        }
        float log2=Log(2),c0=D(Log(const20),log2),c4=D(c0,D(Log(32),log2));
        float stopPoly=S(1,S(D(c0,D(Log(stop),log2)),c4));
        float starSample=D(M(M(stop,StarFactor),StarSize),const14);
        bool fcEnabled=dimensions.ApplyWrites.Any(w=>w.Name=="Enable4KResFix"&&BinaryPrimitives.ReadInt32LittleEndian(w.Bytes.Span)!=0);
        // 's five-element local lens array starts with an actual zero.
        float[] lens=new float[5];float previousDelta=0;
        for(int j=0;j<4;j++)
        {
            var response=LensResponses[j];
            float original=M(Log(M(focal,response.FocalScale)),response.LogScale),adjusted=original;
            if(j==0)adjusted=M(adjusted,powerB4);
            else if(j==1)adjusted=A(adjusted,previousDelta);
            lens[j+1]=adjusted;previousDelta=S(original,adjusted);
        }
        byte[] allGhosts=new byte[16*28];
        for(int j=0;j<16;j++)
        {
            var ghost=GhostDefinitions[j];
            float relative=M(S(B(0x3F4CCCCD),D(A(lens[ghost.LensIndex],ghost.LensOffset),b8)),ghost.ScreenPosition<0?-1:1);
            float width=M(relative,fcEnabled?1080:dimensions.EffectiveHeight84);
            float scale=M(MathF.Abs(D(1,width)),ghost.RadiusScale);
            float[] record=[A(ghost.ScreenPosition,relative),relative,M(ghost.Red,scale),
                M(ghost.Green,scale),M(ghost.Blue,scale),1,ghost.SpriteType];
            for(int lane=0;lane<7;lane++)BinaryPrimitives.WriteSingleLittleEndian(allGhosts.AsSpan(j*28+lane*4),record[lane]);
        }
        int count=requestedGhosts<4?0:Math.Min(requestedGhosts,16);
        byte[] selected=new byte[count*28];int[] order=[0,2,6,15,7,3,8,14,9,1,4,10,13,11,5,12];
        for(int i=0;i<count;i++)allGhosts.AsSpan(order[i]*28,28).CopyTo(selected.AsSpan(i*28));
        var writes=new List<MapPostEffect3ApplyValueWrite>();string evidence=SourceEvidence+
            $"frame={frame};owner={owner};fresh-AF0;current-setters-AF={(dirty?1:0)};-fixedAL1";
        void F(string name,MapPostEffect3WriteStage stage,params float[] values)
        {
            if(values.Any(v=>!float.IsFinite(v)))throw new NotSupportedException("Actual controller optics reaches a nonfinite named writer: "+name);
            byte[] bytes=new byte[values.Length*4];for(int i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*4),values[i]);
            writes.Add(new(name,stage,bytes,evidence));
        }
        void I(string name,MapPostEffect3WriteStage stage,int value)
        {byte[] bytes=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(bytes,value);writes.Add(new(name,stage,bytes,evidence));}
        // Current native branch executes only when AF is dirty. A fresh bank
        // also receives the unconditional initialization values. For
        // this source current seasonal route the Camera setters dirty AF;
        // callers outside it require their separate retained controller owner.
        if(!dirty)throw new NotSupportedException("Current Camera leaves AF clear: preserve the actual  initial optics receipt instead of inventing a later writer");
        F("StopPolyRate",MapPostEffect3WriteStage.OpticsStopPolyRate,stopPoly);
        F("FlareScreenDhAspect",MapPostEffect3WriteStage.OpticsFlareScreenDhAspect,anamorphic?dimensions.SensorHeightRatioF0:diagonalRatio);
        F("FlareScreenWhAspect",MapPostEffect3WriteStage.OpticsFlareScreenWhAspect,anamorphic?dimensions.AspectRatioF4:deviceAspect);
        I("EnableAnamorphic",MapPostEffect3WriteStage.OpticsEnableAnamorphic,anamorphic?1:-1);
        float anamorphicRate=anamorphic?A(M(S(D(1,B(0x40166666)),B(0x40166666)),stopPoly),B(0x40166666)):1;
        F("AnamorphicRateXY",MapPostEffect3WriteStage.OpticsAnamorphicRateXY,anamorphicRate,1);
        float screenAspect=anamorphic?D(1,dimensions.AspectRatioF4):deviceAspect;
        F("ScreenWhAspect",MapPostEffect3WriteStage.OpticsScreenWhAspect,screenAspect);
        I("GhostSpriteNum",MapPostEffect3WriteStage.OpticsGhostSpriteNum,count);
        const int halo=15*28;
        F("LensHaloInfo0",MapPostEffect3WriteStage.OpticsLensHaloInfo0,ReadF(allGhosts,halo+8),ReadF(allGhosts,halo+12),ReadF(allGhosts,halo+16),ReadF(allGhosts,halo+4));
        float tangentValue=M(Tan(M(M(d4,cc),.5f)),2),limbRatio=D(screenAspect,tangentValue),limb0=M(limbRatio,limbRatio);
        float stretch=M(A(D(S(b8,b0),b0),1),limbEnd),linear=M(M(S(stretch,limbStart),.5f),d0);
        F("LimbDarkenningInfo",MapPostEffect3WriteStage.OpticsLimbDarkenningInfo,limb0,M(S(stretch,linear),M(screenAspect,.5f)),A(M(M(screenAspect,.5f),linear),M(M(screenAspect,.5f),linear)));
        float limbFade=limbWeight>0?Pow(limbWeight,B(0x3EE8BA2E)):-1;
        F("LimbDarkenningLinFade",MapPostEffect3WriteStage.OpticsSetLimbDarkening,limbFade>0?limbFade:-1);
        F("StarBurstSampleScale",MapPostEffect3WriteStage.OpticsStarBurstSampleScale,starSample);
        F("FocasuedDistance",MapPostEffect3WriteStage.OpticsFocasuedDistance,distance);
        F("DofLensCoeff",MapPostEffect3WriteStage.OpticsDofLensCoeff,dofCoefficient);
        return new(frame,owner,dirty,writes.AsReadOnly(),allGhosts,selected,count,distance,focal,stop,evidence,Native.Locale);
    }

    private static float ReadF(byte[] bytes,int at)=>BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at));
    private static byte[] PackFloats(params float[] values)
    {
        byte[] bytes=new byte[values.Length*4];
        for(int i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*4),values[i]);
        return bytes;
    }
    private static float B(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
    private readonly record struct LensResponse(float LogScale,float FocalScale);
    private readonly record struct GhostDefinition(int LensIndex,float ScreenPosition,float LensOffset,
        float RadiusScale,float Red,float Green,float Blue,int SpriteType);
    // Logarithmic lens response; unused source-row padding has no runtime role.
    private static readonly LensResponse[] LensResponses=
    [
        new(0.0231749993f,60.8249016f),
        new(-0.0111959996f,2.78804994f),
        new(-0.00216799998f,1.66887999f),
        new(0.0236000009f,289.988007f),
    ];
    // Signed position, lens displacement, radius and RGB weight for each ghost.
    private static readonly GhostDefinition[] GhostDefinitions=
    [
        new(0,-1f,0.0156999994f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,1),
        new(0,0.75f,0.0147000002f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,0),
        new(1,-2.5f,0.0137f,0.0199999996f,0.150000006f,0.25f,1f,1),
        new(1,-0.300000012f,0.0118000004f,0.0199999996f,0.150000006f,0.25f,1f,1),
        new(1,0.25f,0.00899999961f,0.0199999996f,0.150000006f,0.25f,1f,0),
        new(1,-0.5f,0.00810000021f,0.0199999996f,0.150000006f,0.25f,1f,0),
        new(2,-1.79999995f,0.00730000017f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,1),
        new(2,1.62f,0.00680000009f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,0),
        new(2,-1.39999998f,0.00639999984f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,0),
        new(2,1.20000005f,0.00850000046f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,0),
        new(2,-1.25f,0.00810000021f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,0),
        new(2,1.29999995f,0.00779999979f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,0),
        new(2,-0.150000006f,0.00779999979f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,0),
        new(3,0.800000012f,0.00439999998f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,0),
        new(3,-1.64999998f,0.00639999984f,0.0199999996f,0.107643999f,0.358815014f,0.0538222007f,0),
        new(3,-2.45000005f,-0.0142000001f,0.0199999996f,0.150000006f,0.25f,1f,1),
    ];

}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3ControlledOpticsSource ReadControlledOpticsSource(
        LoadedDeferredSamplerNativeSource native,SkyWeatherNativePowerSource power,
        LoadedNativeSkyAtanSource atan,LoadedNativeCameraTangentSource tangent,NativeLogTables log)
    {
        if(power.Locale!=native.Locale||atan.Locale!=native.Locale||tangent.Language!=native.Locale||log.Locale!=native.Locale)
            throw new NotSupportedException("Controlled optics math and complete controller bodies must use the same original source generation.");
        return new(native,power,atan,tangent,log,RenderRuleIdentity.For("post-effect3-controlled-optics",native.Locale)+
            ";fresh-initialization/current-dirty-setters;typed-lens-and-ghost-rules;28B-packed-output;owned-SSE-math");
    }
}

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual current viewport depth and module gates from the same owned
/// renderer generation. A disabled projection module needs its retained cache;
/// a missing refraction texture needs its retained three source rows.</summary>
public sealed record MainMapCameraDeferredCurrentInputs(
    float ViewportMinimumDepth,float ViewportMaximumDepth,bool RefractionTexturePresent,
    bool ProjectionModuleA8Bit0,DeferredTranslucencyViewportInputs CurrentTranslucencyViewport,
    long FrameNumber,string CurrentOwnerProvenance,
    IReadOnlyList<Vector4>? RetainedRefractionWorldToTexture=null,
    DeferredProjectionInputs? RetainedProjectionWhenDisabled=null);

public sealed record MainMapCameraDeferredSourcePacket(
    DeferredRefractionInputs Refraction,DeferredProjectionInputs Projection,
    DeferredTranslucencyViewportInputs TranslucencyViewport,long FrameNumber,string SourceProvenance);

public sealed record MainMapCameraDeferredFreshInputs(
    float ViewportMinimumDepth,float ViewportMaximumDepth,bool RefractionTexturePresent,
    DeferredTranslucencyViewportInputs CurrentTranslucencyViewport,
    long FrameNumber,string CurrentOwnerProvenance,IReadOnlyList<Vector4>? RetainedRefractionWorldToTexture=null);

public sealed record MainMapCameraDeferredInitializedFreshInputs(
    float ViewportMinimumDepth,float ViewportMaximumDepth,bool RefractionTexturePresent,
    long FrameNumber,string CurrentOwnerProvenance,IReadOnlyList<Vector4>? RetainedRefractionWorldToTexture=null);

/// <summary>Source9B4FF0/9C0560 writers joined to the accepted Main Camera.
/// Prepared C is full K+10: Target C10=K20, Near/Far C130/134=K140/144,
/// reverse-depth C148.bit1=K158.bit1 and projection CB0=KC0. Viewport/module
/// caches remain explicit current owners; this factory has no startup defaults.</summary>
public static class MainMapCameraDeferredSource
{
    /// <summary>C13E58's successful868 header initializer, before any F103
    /// temporary override. This is separate from the postdraw restore path.</summary>
    public static DeferredTranslucencyViewportInputs CreateFreshThinSurfaceViewport()
        => new(new Vector4(0,0,1,1));
    private sealed class LoadedThinSurfaceFactory { }
    private static readonly ConditionalWeakTable<LoadedRdbAsset, LoadedThinSurfaceFactory> LoadedFactories = new();
    public static MainMapCameraDeferredSourcePacket PrepareCurrent(
        MainMapCameraFrame frame,MainMapCameraDeferredCurrentInputs current)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return PrepareCurrent(frame.Camera,current);
    }

    /// <summary>The accepted native camera packet may come directly from the
    /// owned scene session; no reconstruction of controller state is needed.</summary>
    public static MainMapCameraDeferredSourcePacket PrepareCurrent(
        MapCameraConstructionPacket camera,MainMapCameraDeferredCurrentInputs current)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(current.CurrentTranslucencyViewport);
        ArgumentException.ThrowIfNullOrWhiteSpace(current.CurrentOwnerProvenance);
        if(current.FrameNumber!=camera.FrameNumber)
            throw new InvalidDataException("Deferred camera module values belong to a different Main frame.");
        var prepared=camera.SceneInputs;
        var aim=camera.Geometry.Target;
        var target=new Vector3(aim.X,aim.Y,aim.Z);
        var refraction=DeferredRefractionMath.Prepare(prepared,target,camera.Lens.Near,camera.Lens.Far,
            camera.Lens.ReverseDepth,current.ViewportMinimumDepth,current.ViewportMaximumDepth,
            current.RefractionTexturePresent);
        if(refraction.WorldToTexture is null)
        {
            if(current.RetainedRefractionWorldToTexture is not {Count:3} retained)
                throw new InvalidDataException("Absent refraction texture retains its unknown three-row cache.");
            refraction=refraction with {WorldToTexture=Array.AsReadOnly(retained.ToArray())};
        }
        var projection=DeferredRefractionMath.PrepareProjectionToView(camera.SourceProjection,
            current.ProjectionModuleA8Bit0)??current.RetainedProjectionWhenDisabled??
            throw new InvalidDataException("Disabled projection module retains its unknown projection cache.");
        return new(refraction,projection,current.CurrentTranslucencyViewport,current.FrameNumber,
            camera.FrameProvenance+";"+current.CurrentOwnerProvenance+";prepared-camera=fullK+10;"+
            "refraction=9B4FF0;projection=9C0560;translucency=current-named-value");
    }

    /// <summary>Exact fresh ThinSurfaceBSDF source factory: the five named
    /// parameters select A8=5; the registered Refraction record selects the
    /// final9C0440 result2. The source9C0530 aliases its
    /// projection cache and9C0560 writes inverse(CB0). Later edited modules
    /// use PrepareCurrent with their actual gate/retained cache.</summary>
    public static MainMapCameraDeferredSourcePacket PrepareFreshSelectedCurrent(
        DeferredF103SelectedSource selected,MainMapCameraDeferredFreshInputs current)
    {
        ArgumentNullException.ThrowIfNull(selected);ArgumentNullException.ThrowIfNull(current);

        var shader=selected.SourcePlan.Shader;
        _=LoadedFactories.GetValue(shader.Source, source=>
        {
            if(source.ResourceUri!="san14://rdb/system/9D8E811F"||source.OverrideSource is not null)
                throw new InvalidDataException("Fresh projection factory requires the exact source9D8E811F shader.");
            var thin=G1ShaderConfigurationReader.Read(source.Bytes,shader.Container).Records.Single(r=>r.Name=="ThinSurfaceBSDF");
            (uint Hash,uint Value)[] expected=[(0x42B7E6DD,0),(0xFEB72A6D,0),(0x2BC1B848,0),(0xB4762740,0),(0x93A3D611,1)];
            if(thin.SourceOffset!=0xC780||thin.Parameters.Count!=7||expected.Any(p=>thin.Find(p.Hash)?.RawValue!=p.Value))
                throw new InvalidDataException("Fresh ThinSurfaceBSDF parameters differ from the source A8=5 factory.");
            return new();
        });
        return PrepareCurrent(selected.Camera,new(current.ViewportMinimumDepth,current.ViewportMaximumDepth,
            current.RefractionTexturePresent,true,current.CurrentTranslucencyViewport,current.FrameNumber,
            current.CurrentOwnerProvenance+";source9D8E811F/ThinSurfaceBSDF@C780/factory97E1C0-9C0060-9BFEB0/A8=5",
            current.RetainedRefractionWorldToTexture));
    }

    /// <summary>Successful actual8687085E header initialization calls1593F0,
    /// thenC13E58 writes mapped vTsbViewPort=(0,0,1,1). This scope also requires
    /// the fresh Main/View constructor's internal K1548/K154C scales, both1,
    /// with no subsequent edit. F103 therefore skips its temporary override.
    /// Later edited View or shader values use PrepareFreshSelectedCurrent.
    /// Native viewport depth and actual refraction binding stay explicit.</summary>
    public static MainMapCameraDeferredSourcePacket PrepareAfterSuccessfulFreshMainViewInitialization(
        DeferredF103SelectedSource selected,MainMapCameraDeferredInitializedFreshInputs current)
    {
        ArgumentNullException.ThrowIfNull(selected);ArgumentNullException.ThrowIfNull(current);
        var reference=selected.SourcePlan.ShaderReference;
        if(selected.Camera.Source.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId||
            selected.SourcePlan.ViewId!=0||reference.ShaderObject.ClassId!=RenderGraphShaderReferences.ShaderObjectClassId||
            reference.ShaderObject.ObjectId!=0xC96F6A61||reference.ShaderFileHash!=0x9D8E811F||
            reference.G1SReference.NameHash!=RenderGraphShaderReferences.G1SFileResourceHashProperty||
            reference.G1SReference.TypeCode!=5||reference.G1SReference.ElementCount!=1||reference.G1SReference.RawValue.Length!=4||
            BinaryPrimitives.ReadUInt32LittleEndian(reference.G1SReference.RawValue)!=0x9D8E811F||selected.Current.NativeColorMode!=0x60)
            throw new InvalidDataException("Successful fresh Main View initialization requires actual38D/View0/Class868 source owners.");
        return PrepareFreshSelectedCurrent(selected,new(current.ViewportMinimumDepth,current.ViewportMaximumDepth,
            current.RefractionTexturePresent,CreateFreshThinSurfaceViewport(),current.FrameNumber,
            current.CurrentOwnerProvenance+";successful-source868-header/1593F0-C13E58;fresh-Main-K1548/K154C=1;no-later-View-edit",
            current.RetainedRefractionWorldToTexture));
    }
}

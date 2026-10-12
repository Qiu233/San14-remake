using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;
namespace San14.Data;

/// <summary>Actual current1193A90 color162/PS47 and finite same-frame Main
/// weather input. Animation is a pure once-per-frame1191B40 candidate until
/// the complete Host output commits. The explicit owned clock is not Clock254.</summary>
public sealed class SkyAppliedSourceCloudOwner
{
    private readonly GameResourceRepository repository;
    private readonly SkyAppliedSourceCloudSelectionOwner selectionOwner;
    private readonly SkyCloudSourceClipPlane clip;
    private readonly string native,locale;
    private SkyCloudAnimationState acceptedAnimation=SkyCloudAnimationState.Fresh;
    private SkyAppliedSourceCloudFrame? candidate;
    private long acceptedFrame;
    private MapOwnedGraphicsMaskPrefix? currentClipScope;
    public void BindCurrentClipScope(MapOwnedGraphicsMaskPrefix scope)
    { ArgumentNullException.ThrowIfNull(scope);scope.RequireCanonicalSkyDeclaredHookScope();currentClipScope=scope; }
    private SkyAppliedSourceCloudOwner(GameResourceRepository repo,string locale,string owner,
        SkyAppliedSourceCloudSelectionOwner selection,SkyCloudSourceClipPlane clip,string native)
    {repository=repo;this.locale=locale;OwnerIdentity=owner;selectionOwner=selection;this.clip=clip;this.native=native;}
    public string OwnerIdentity {get;}
    /// <summary>The privately created special-cache owner, borrowed by the
    /// actual complete final wrapper. It cannot be replaced by a caller.</summary>
    public SkyCloudSourceClipPlane ClipSource => clip;
    public SkyCloudSourceClipPlanePacket RequireCurrentClipPacket()
    {
        var frame=candidate??throw new InvalidDataException("Cloud has no current clip candidate.");
        frame.ValidateCandidate(frame.SourceFrame,frame.AcceptedPresent);
        if(frame.Clip.IsConstructorPlan)throw new InvalidDataException("Cloud constructor plan cannot identify an actual current clip submission.");
        return frame.Clip;
    }
    public static SkyAppliedSourceCloudOwner CreateFresh(GameResourceRepository repository,string locale,string ownedCloudIdentity,
        SkyCloudProgramResources programs)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(ownedCloudIdentity);
        return new(repository,locale,ownedCloudIdentity,SkyAppliedSourceCloudSelectionOwner.CreateFresh(repository,locale,programs),
            SkyCloudSourceClipPlane.CreateFresh(repository,locale),RenderSamplerSourceAdapters.ReadAppliedCloudCurrentWriterNativeRecipe(repository.SourceDirectory,locale));
    }
    public SkyAppliedSourceCloudFrame PrepareBootstrap(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance)
        => Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,true);
    public SkyAppliedSourceCloudFrame PrepareCurrent(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance)
    {
        return Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,false);
    }
    /// <summary>A separate actual mode6 selected-source entry. The old
    /// mode2 zero-Moon/Fog0 gate remains on the original entry points.</summary>
    public SkyAppliedSourceCloudFrame PrepareDirectionalBootstrap(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance,
        SkyDirectionalSelectedLightSource directional)
        =>Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,true,directional);
    public SkyAppliedSourceCloudFrame PrepareDirectionalCurrent(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance,
        SkyDirectionalSelectedLightSource directional)
        =>Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,false,directional);
    private SkyAppliedSourceCloudFrame Prepare(SkySceneSourceFramePacket sourceFrame,
        MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance,bool constructorPlan,
        SkyDirectionalSelectedLightSource? directional=null)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame);ArgumentNullException.ThrowIfNull(present);ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(selectedView);ArgumentException.ThrowIfNullOrWhiteSpace(ownedClockProvenance);
        selectedView.RequireSelectedView(present.FrameNumber,sourceFrame.Binding);
        var lighting=stages.AcceptedLighting;var selected=present.AcceptedSeasonCamera.Inputs.Source.Selection;
        stages.ValidateCurrent(lighting,selected,present.CurrentSkyCamera);
        if(!ReferenceEquals(stages.AcceptedPresent,present)||sourceFrame.FrameNumber!=present.FrameNumber||
            sourceFrame.Binding!=lighting.Binding||present.FrameNumber<=acceptedFrame||
            present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale!=locale||
            !float.IsFinite(explicitOwnedClockDelta)||explicitOwnedClockDelta<0)
            throw new InvalidDataException("Cloud current writer requires the exact accepted frame, View and finite explicit owned clock.");
        var actualSource=present.AcceptedSeasonCamera.Inputs.Source;
        var textures=actualSource.CloudTextures;
        var selection=selectionOwner.PrepareCurrent(present,stages,textures);
        var bindings=SkyAppliedCloudTextureSet.Import(textures);
        SkyAppliedCloudTextureBindings.ValidateProgram(selection,bindings);
        var clipPacket=constructorPlan?clip.PrepareConstructorPlan(sourceFrame,present,stages,selectedView):
            clip.PrepareCurrent(currentClipScope??throw new InvalidDataException("Cloud current clip requires a typed owned frontend prefix."),sourceFrame,present,stages,selectedView);
        var weather=present.CloudWeather;
        var source=sourceFrame.SourceFrame;
        if(source.SixCameras.Count!=6||source.Controller is null||!source.Lights.SunPresent||!source.Lights.MoonPresent)
            throw new NotSupportedException("Cloud actual branch requires six source cameras and the complete current controller/light path.");
        if(directional is null&&(source.Lights.SourceZeroMoon is null||source.Atmosphere.Fog.SourceFogMode!=0||
            source.Atmosphere.Fog.RendererFogMode!=4||source.Atmosphere.Fog.Profile!=1||source.Atmosphere.Fog.Value!=Vector4.Zero))
            throw new NotSupportedException("Cloud actual branch requires six source cameras, complete current controller/Sun/zero-Moon and source Fog0.");
        if(directional is not null)
        {
            directional.ValidateCurrent();directional.ValidatePreparedLights(source.Lights);
            if(!ReferenceEquals(directional.AcceptedLighting,lighting))
                throw new InvalidDataException("Directional Cloud must use its actual same selected group80 lighting owner.");
            if(source.SourceSelectedAtmosphere is { } selectedAtmosphere)
            {
                selectedAtmosphere.ValidateFrame(sourceFrame,directional);selectedAtmosphere.ValidateStage(stages);
                selectedAtmosphere.ValidateAtmosphere(source.Atmosphere);
                (directional.CurrentColorSetterReceipt??throw new InvalidDataException("Current Directional Cloud lacks its actual D3 global color setter receipt."))
                    .ValidateFrame(sourceFrame,directional);
            }
            else
            {
            if(source.Atmosphere.Fog.SourceMode4Proof is null)
                throw new InvalidDataException("Directional Cloud requires its current Sky mode4 Fog/Scat owner before the renderer0/profile1 stage.");
            }
        }
        var sourceCamera=selected.FirstPackage.Camera;
        var geometryField=sourceCamera.Properties.Single(p=>p.Index==614);
        if(geometryField.NameHash!=0x59633E4E||geometryField.TypeCode!=8||geometryField.ElementCount!=1||geometryField.ElementSize!=4||geometryField.RawValue.Length!=4)
            throw new InvalidDataException("Cloud controller40 source614 geometry field changed.");
        float geometry40=selectedView.SelectedViewIndex==0?BinaryPrimitives.ReadSingleLittleEndian(geometryField.RawValue):-1;
        if(!float.IsFinite(geometry40)||geometry40==0)throw new InvalidDataException("Cloud controller current geometry40 is outside the finite source branch.");
        var lightBytes=lighting.LocalLightData.Span;
        Vector3 rawSun=new(BinaryPrimitives.ReadSingleLittleEndian(lightBytes.Slice(0xFA0,4)),BinaryPrimitives.ReadSingleLittleEndian(lightBytes.Slice(0xFA4,4)),BinaryPrimitives.ReadSingleLittleEndian(lightBytes.Slice(0xFA8,4)));
        //1195B14/44..65 reads controller50's attached Sun record+10 RGB;
        //1195BB5/BE3..BEF does the same for controller58's Moon. The current
        //D3 frame shares these exact global records after its color setters.
        if(directional?.CurrentColorSetterReceipt is { } colorReceipt)
        {colorReceipt.ValidateFrame(sourceFrame,directional);rawSun=colorReceipt.SunRgb;}
        if(directional is null&&source.Lights.RawMoonRgb!=Vector3.Zero)throw new NotSupportedException("Cloud current Moon branch requires its actual nonnull zero-RGB source setter.");
        var material=present.CloudMaterial;
        var animation=acceptedAnimation.Advance(material,explicitOwnedClockDelta);
        uint maximum=weather.Settings.MinimumRaySamples;
        if(maximum is <1 or >4096)throw new NotSupportedException("Current source ray samples exceed the finite GPU work contract.");
        var volume=material.PrepareVolume(animation);
        float planetRadius=actualSource.RequireRenderingAtmosphere().PlanetRadius;
        var packets=new SkyCloudConstantPacket[6];
        for(int face=0;face<6;face++)packets[face]=Packet(selection,source,face,material,volume,planetRadius,maximum,clipPacket.Value,rawSun,geometry40);
        return candidate=new(this,sourceFrame,present,stages,selectedView,selection,bindings,clipPacket,packets,material,animation,volume,maximum,
            native+";"+weather.SourceProvenance+";"+clipPacket.Provenance+";"+ownedClockProvenance+
            ";same-frame1193A90-full-named-VS160/PS512;once1191B40-pure-phase-candidate;owner="+OwnerIdentity);
    }
    internal void RequireCandidate(SkyAppliedSourceCloudFrame frame)
    {if(!ReferenceEquals(candidate,frame))throw new InvalidDataException("Cloud candidate is stale, superseded or already committed.");}
    public void CommitCompleteFrame(SkyAppliedSourceCloudFrame frame)
    {RequireCandidate(frame);if(frame.Clip.IsConstructorPlan)throw new InvalidDataException("Cloud constructor plan cannot acknowledge an actual completed Host frame.");frame.ValidateCandidate(frame.SourceFrame,frame.AcceptedPresent);clip.RequireCompletedPacket(frame.Clip);acceptedAnimation=frame.Animation;acceptedFrame=frame.FrameNumber;candidate=null;}

    private static SkyCloudConstantPacket Packet(SkyAppliedSourceCloudSelection s,SkySceneSourceFrame frame,
        int face,SkyCloudMaterial material,SkyCloudVolumeMaterial volume,float planetRadius,uint maximum,Vector4 clip,Vector3 rawSun,float geometry40)
    {
        var c=frame.SixCameras[face];var l=frame.Lights;var controller=frame.Controller!;
        if(c.NativeClipProfile!=2||(c.Flags148&~2u)!=0)throw new NotSupportedException("Cloud current source writer closes ordinary Perspective clip2 only.");
        float far=(c.Flags148&2)!=0?0:1,near=(c.Flags148&2)!=0?1:0;
        Matrix4x4 p2w=NativeMatrixMath.Invert(c.MatrixF0,out _);
        Matrix4x4 directionView=DirectionView(face),projection=Projection((c.Flags148&2)!=0);
        Matrix4x4 rays=NativeMatrixMath.Invert(TerrainFrustumMath.Multiply(directionView,projection),out _);
        float[] vectors=new float[16];
        for(int vertex=0;vertex<4;vertex++)
        {
            float x=(vertex>>1)*2-1,y=1-(vertex&1)*2;
            Vector4 a=Transform(x,y,far,rays),b=Transform(x,y,near,rays);float ai=D(1,a.W),bi=D(1,b.W);
            //1193FCF/1193FDD/1193FEB: normalized near minus far.
            vectors[vertex*4]=S(M(b.X,bi),M(a.X,ai));vectors[vertex*4+1]=S(M(b.Y,bi),M(a.Y,ai));vectors[vertex*4+2]=S(M(b.Z,bi),M(a.Z,ai));vectors[vertex*4+3]=S(M(b.W,bi),M(a.W,ai));
        }
        Dictionary<string,float[]> vertexValues=new(){["ksgClipPlane"]=Words(clip),["g_mtP2W"]=Words(p2w),["g_vCameraDirVectors"]=vectors,["g_vProjFarNear"]=[far,near]};
        Dictionary<string,float[]> pixelValues=new(){["litDir"]=Words(l.PackageLightPosition),["vEye"]=Words(c.Position),["fog"]=Words(Vector4.Zero),["Scat"]=frame.Atmosphere.Scattering.SelectMany(Words).ToArray()};
        void Set(string name,params float[] words){pixelValues.Add(name,words);}
        var common=volume.PreparePixel(c.Position,c.Scalar138,planetRadius);
        Set("g_vBaseTexOffset",common.BaseTextureOffset.X,common.BaseTextureOffset.Y,common.BaseTextureOffset.Z);
        Set("g_vBaseTexScale",common.BaseTextureScale.X,common.BaseTextureScale.Y,common.BaseTextureScale.Z);
        Set("g_vDetailTexOffset",common.DetailTextureOffset.X,common.DetailTextureOffset.Y,common.DetailTextureOffset.Z);
        Set("g_vDetailTexScale",common.DetailTextureScale.X,common.DetailTextureScale.Y,common.DetailTextureScale.Z);
        Set("g_vDistortTexOffset",common.DistortTextureOffset.X,common.DistortTextureOffset.Y);
        Set("g_vDistortTexScale",common.DistortTextureScale.X,common.DistortTextureScale.Y);
        Set("g_vCirrusTexOffset",common.CirrusTextureOffset.X,common.CirrusTextureOffset.Y);
        Set("g_vCirrusTexScale",common.CirrusTextureScale.X,common.CirrusTextureScale.Y);
        Set("g_vCirrusDistortTexOffset",common.CirrusDistortTextureOffset.X,common.CirrusDistortTextureOffset.Y);
        Set("g_vCirrusDistortTexScale",common.CirrusDistortTextureScale.X,common.CirrusDistortTextureScale.Y);
        Set("g_vRaySampleNum",Words(common.RaySampleCount));
        Set("g_vEarthRadius",Words(common.EarthRadius));
        // Actual1742230 descriptor23/24/25 -> cache C50/CD0/D50.
        // Their names are not the arithmetic preparation order.
        Set("g_vCloudParams1",Words(common.CloudParameters1));
        Set("g_vCloudParams2",Words(material.SampleStepParameters));
        Set("g_vCloudParams0",Words(common.CloudParameters0));
        Set("g_vDistortScale",Words(common.DistortionScale));
        Set("g_vCirrusParams",Words(common.CirrusParameters));
        Set("g_vCirrusDistortScale",common.CirrusDistortionScale.X,common.CirrusDistortionScale.Y);Set("g_vErodeScale",common.ErosionScale.X,common.ErosionScale.Y);
        Set("g_vHgParams0",Words(material.HgParameters0));
        Set("g_vHgParams1",Words(material.HgParameters1));
        float sun=controller.SkyInfoZW7C78.X,moon=controller.SkyInfoZW7C78.Y;
        if(sun>0&&M(l.RawSunPosition.Y,-1)>F(0xBE19999A))
        {
            float intensity=A(A(M(l.RawMoonRgb.Y,F(0x3EAAAAAB)),M(l.RawMoonRgb.X,F(0x3EAAAAAB))),M(l.RawMoonRgb.Z,F(0x3EAAAAAB)));
            if(D(F(0x3B000000),geometry40)>intensity)moon=0;
        }
        Set("g_vSky2SunRGB",M(rawSun.X,sun),M(rawSun.Y,sun),M(rawSun.Z,sun),sun);
        Set("g_vSky2SunDir",M(l.RawSunPosition.X,-1),M(l.RawSunPosition.Y,-1),M(l.RawSunPosition.Z,-1),SkyCloudMaterial.SunHorizonOffset);
        Set("g_vSky2MoonRGB",M(l.RawMoonRgb.X,moon),M(l.RawMoonRgb.Y,moon),M(l.RawMoonRgb.Z,moon),moon);
        Set("g_vSky2MoonDir",M(l.RawMoonPosition.X,-1),M(l.RawMoonPosition.Y,-1),M(l.RawMoonPosition.Z,-1),0);
        Set("g_vSky2RayleighExtinction",0,0,0,A(SkyCloudMaterial.AtmosphereHeight,planetRadius));Set("g_vSky2MieExtinction",0,0,0,0);
        Set("g_vStableDim",Words(material.StableDimensions));Set("g_vClampValue",0,0,0,controller.Scalar1F0);
        var vertexDefinitions=s.ProgramSelection.VertexDefinitions;
        var pixelDefinitions=s.ProgramSelection.PixelDefinitions;
        byte[] vertexBytes=Pack(vertexDefinitions,160,vertexValues),pixel=Pack(pixelDefinitions,s.PixelConstantByteLength,pixelValues);
        return SkyCloudConstants.ReadColor(vertexDefinitions,pixelDefinitions,vertexBytes,pixel,maximum);
    }
    private static byte[] Pack(DxbcResourceDefinitions definitions,int length,Dictionary<string,float[]> fields)
    {
        var buffer=definitions.Buffers.Single();if(buffer.ByteSize!=length||fields.Count!=buffer.Variables.Count)throw new InvalidDataException("Cloud named source inventory differs from actual shader.");
        byte[] b=new byte[length];foreach(var variable in buffer.Variables)
        {
            if(!fields.TryGetValue(variable.Name.Text,out var values)||values.Length*4!=variable.ByteSize)throw new InvalidDataException("Cloud current named writer missing "+variable.Name.Text);
            for(int i=0;i<values.Length;i++){if(!float.IsFinite(values[i]))throw new InvalidDataException("Cloud current named result is nonfinite: "+variable.Name.Text);BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(checked((int)variable.StartOffset+i*4),4),values[i]);}
        }
        return b;
    }
    private static Matrix4x4 DirectionView(int face)
    {
        var axis=SkySceneSourceDefaults.CubeFaces[face];Vector3 z=new(S(0,axis.Direction.X),S(0,axis.Direction.Y),S(0,axis.Direction.Z));
        Vector3 x=new(S(M(z.Z,axis.Up.Y),M(z.Y,axis.Up.Z)),S(M(z.X,axis.Up.Z),M(z.Z,axis.Up.X)),S(M(z.Y,axis.Up.X),M(z.X,axis.Up.Y)));
        Vector3 y=new(S(M(x.Z,z.Y),M(x.Y,z.Z)),S(M(x.X,z.Z),M(x.Z,z.X)),S(M(x.Y,z.X),M(x.X,z.Y)));
        float Dot(Vector3 a)=>A(A(M(0f,a.X),M(0f,a.Y)),M(0f,a.Z));
        return new(x.X,y.X,z.X,0,x.Y,y.Y,z.Y,0,x.Z,y.Z,z.Z,0,Dot(x),Dot(y),Dot(z),1);
    }
    private static Matrix4x4 Projection(bool reverse)
    {float z=D(reverse?1:100,reverse?99:-99);return new(-1,0,0,0,0,1,0,0,0,0,z,-1,0,0,M(z,reverse?100:1),0);}
    private static Vector4 Transform(float x,float y,float z,Matrix4x4 m)=>new(A(A(A(M(x,m.M11),M(y,m.M21)),M(z,m.M31)),m.M41),A(A(A(M(x,m.M12),M(y,m.M22)),M(z,m.M32)),m.M42),A(A(A(M(x,m.M13),M(y,m.M23)),M(z,m.M33)),m.M43),A(A(A(M(x,m.M14),M(y,m.M24)),M(z,m.M34)),m.M44));
    private static float[] Words(Vector4 v)=>[v.X,v.Y,v.Z,v.W];
    private static float[] Words(Matrix4x4 v)=>[v.M11,v.M12,v.M13,v.M14,v.M21,v.M22,v.M23,v.M24,v.M31,v.M32,v.M33,v.M34,v.M41,v.M42,v.M43,v.M44];
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;

}
public sealed class SkyAppliedSourceCloudFrame
{
    internal SkyAppliedSourceCloudOwner SourceCreatedInputOwner => owner;
    private readonly SkyAppliedSourceCloudOwner owner;private readonly SkyCloudConstantPacket[] packets;
    internal SkyAppliedSourceCloudFrame(SkyAppliedSourceCloudOwner owner,SkySceneSourceFramePacket source,MapSourceCreatedSeasonLightingFrame present,SkyAppliedSourceStageFrame stages,MainSceneNode51Execution view,SkyAppliedSourceCloudSelection selection,SkyAppliedCloudTextureSet bindings,SkyCloudSourceClipPlanePacket clip,SkyCloudConstantPacket[] packets,SkyCloudMaterial material,SkyCloudAnimationState animation,SkyCloudVolumeMaterial volume,uint maximumRaySamples,string provenance)
    {this.owner=owner;SourceFrame=source;AcceptedPresent=present;Stages=stages;SelectedView=view;Selection=selection;Bindings=bindings;Clip=clip;this.packets=packets;Material=material;Animation=animation;Volume=volume;MaximumRaySamples=maximumRaySamples;SourceProvenance=provenance;}
    public SkySceneSourceFramePacket SourceFrame {get;}
    public MapSourceCreatedSeasonLightingFrame AcceptedPresent {get;}
    public SkyAppliedSourceStageFrame Stages {get;}
    public MainSceneNode51Execution SelectedView {get;}
    public SkyAppliedSourceCloudSelection Selection {get;}
    public SkyAppliedCloudTextureSet Bindings {get;}
    public SkyCloudSourceClipPlanePacket Clip{get;}
    public SkyCloudMaterial Material {get;}
    public SkyCloudAnimationState Animation {get;}
    public SkyCloudVolumeMaterial Volume {get;}
    public uint MaximumRaySamples {get;}
    /// <summary>Borrow the current volume without advancing its clock or decoding a color buffer.
    /// Pattern uses its own camera and the actual atmosphere CPU's planet radius.</summary>
    public SkyCloudPatternMaterial PreparePatternMaterial(SkyAtmoTableCpuOwner atmosphere,
        SkyPatternPreparedCameras cameras,Vector4 actualClipPlane)
    {
        ArgumentNullException.ThrowIfNull(atmosphere);
        ValidateCandidate(SourceFrame,AcceptedPresent);
        atmosphere.RequireCurrent();
        if(!atmosphere.Source.Tables.SameResources(AcceptedPresent.AcceptedSeasonCamera.Inputs.Source.Transmittance.RequireTables()))
            throw new InvalidDataException("Pattern atmosphere belongs to another selected resource generation.");
        return new(cameras.PrepareVertexMaterial(actualClipPlane),
            Volume.PreparePixel(cameras.Temporary.Eye,cameras.Temporary.Lens.VerticalFovRadians,atmosphere.PlanetRadius58));
    }
    public long FrameNumber=>AcceptedPresent.FrameNumber;
    public string SourceProvenance {get;}
    public IReadOnlyList<SkyCloudConstantPacket> SixPackets=>Array.AsReadOnly(packets.Select(p=>p with{VertexBytes=p.VertexBytes.ToArray(),PixelBytes=p.PixelBytes.ToArray()}).ToArray());
    public void ValidateCandidate(SkySceneSourceFramePacket source,MapSourceCreatedSeasonLightingFrame present)
    {if(!ReferenceEquals(SourceFrame,source)||!ReferenceEquals(AcceptedPresent,present))throw new InvalidDataException("Cloud candidate belongs to another exact accepted frame.");SourceFrame.SourceFrame.SourceSelectedAtmosphere?.ValidateFrame(SourceFrame);SourceFrame.SourceFrame.SourceSelectedAtmosphere?.ValidateStage(Stages);Selection.ValidateCurrent(present,Stages);Clip.ValidateCurrent(source,present);owner.RequireCandidate(this);}
}




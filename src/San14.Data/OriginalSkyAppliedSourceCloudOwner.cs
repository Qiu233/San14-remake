using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using San14.Data.Formats;
namespace San14.Data;

/// <summary>Actual current1193A90 color162/PS47 and finite same-frame Main
/// weather input. Animation is a pure once-per-frame1191B40 candidate until
/// the complete Host output commits. The explicit owned clock is not Clock254.</summary>
public sealed class OriginalSkyAppliedSourceCloudOwner
{
    private readonly OriginalGameRepository repository;
    private readonly OriginalSkyAppliedSourceCloudSelectionOwner selectionOwner;
    private readonly OriginalSkyCloudSourceClipPlane clip;
    private readonly string native,locale;
    private readonly Dictionary<int,float> acceptedPhase;
    private OriginalSkyAppliedSourceCloudFrame? candidate;
    private long acceptedFrame;
    private OriginalMapOwnedGraphicsMaskPrefix? currentClipScope;
    private OriginalMapSeasonCameraPackage? loadedTexturePackage;
    private OriginalSkyAppliedSeasonCloudTextureSources? loadedTextures;
    public void BindCurrentClipScope(OriginalMapOwnedGraphicsMaskPrefix scope)
    { ArgumentNullException.ThrowIfNull(scope);scope.RequireCanonicalSkyDeclaredHookScope();currentClipScope=scope; }
    private OriginalSkyAppliedSourceCloudOwner(OriginalGameRepository repo,string locale,string owner,
        OriginalSkyAppliedSourceCloudSelectionOwner selection,OriginalSkyCloudSourceClipPlane clip,string native)
    {repository=repo;this.locale=locale;OwnerIdentity=owner;selectionOwner=selection;this.clip=clip;this.native=native;
        acceptedPhase=PhaseOffsets.ToDictionary(x=>x,x=>0f);}
    public string OwnerIdentity {get;}
    /// <summary>The privately created special-cache owner, borrowed by the
    /// actual complete final wrapper. It cannot be replaced by a caller.</summary>
    public OriginalSkyCloudSourceClipPlane ClipSource => clip;
    public OriginalSkyCloudSourceClipPlanePacket RequireCurrentClipPacket()
    {
        var frame=candidate??throw new InvalidDataException("Cloud has no current clip candidate.");
        frame.ValidateCandidate(frame.SourceFrame,frame.AcceptedPresent);
        if(frame.Clip.IsConstructorPlan)throw new InvalidDataException("Cloud constructor plan cannot identify an actual current clip submission.");
        return frame.Clip;
    }
    public static OriginalSkyAppliedSourceCloudOwner CreateFresh(OriginalGameRepository repository,string locale,string ownedCloudIdentity)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentException.ThrowIfNullOrWhiteSpace(ownedCloudIdentity);
        return new(repository,locale,ownedCloudIdentity,OriginalSkyAppliedSourceCloudSelectionOwner.CreateFresh(repository,locale),
            OriginalSkyCloudSourceClipPlane.CreateFresh(repository,locale),OriginalDeferredSamplerSources.ReadAppliedCloudCurrentWriterNativeRecipe(repository.SourceDirectory,locale));
    }
    public OriginalSkyAppliedSourceCloudFrame PrepareBootstrap(OriginalSkySceneSourceFramePacket sourceFrame,
        OriginalMapSourceCreatedSeasonLightingFrame present,OriginalSkyAppliedSourceStageFrame stages,
        OriginalMainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance)
        => Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,true);
    public OriginalSkyAppliedSourceCloudFrame PrepareCurrent(OriginalSkySceneSourceFramePacket sourceFrame,
        OriginalMapSourceCreatedSeasonLightingFrame present,OriginalSkyAppliedSourceStageFrame stages,
        OriginalMainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance)
    {
        return Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,false);
    }
    /// <summary>A separate actual mode6 selected-source entry. The old
    /// mode2 zero-Moon/Fog0 gate remains on the original entry points.</summary>
    public OriginalSkyAppliedSourceCloudFrame PrepareDirectionalBootstrap(OriginalSkySceneSourceFramePacket sourceFrame,
        OriginalMapSourceCreatedSeasonLightingFrame present,OriginalSkyAppliedSourceStageFrame stages,
        OriginalMainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance,
        OriginalSkyDirectionalSelectedLightSource directional)
        =>Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,true,directional);
    public OriginalSkyAppliedSourceCloudFrame PrepareDirectionalCurrent(OriginalSkySceneSourceFramePacket sourceFrame,
        OriginalMapSourceCreatedSeasonLightingFrame present,OriginalSkyAppliedSourceStageFrame stages,
        OriginalMainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance,
        OriginalSkyDirectionalSelectedLightSource directional)
        =>Prepare(sourceFrame,present,stages,selectedView,explicitOwnedClockDelta,ownedClockProvenance,false,directional);
    private OriginalSkyAppliedSourceCloudFrame Prepare(OriginalSkySceneSourceFramePacket sourceFrame,
        OriginalMapSourceCreatedSeasonLightingFrame present,OriginalSkyAppliedSourceStageFrame stages,
        OriginalMainSceneNode51Execution selectedView,float explicitOwnedClockDelta,string ownedClockProvenance,bool constructorPlan,
        OriginalSkyDirectionalSelectedLightSource? directional=null)
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
        if(!ReferenceEquals(loadedTexturePackage,selected.FirstPackage))
        {
            var loaded=OriginalSkyAppliedSeasonCloudTextures.Load(repository,actualSource);
            loadedTexturePackage=selected.FirstPackage;loadedTextures=loaded;
        }
        var textures=loadedTextures!.ForCurrent(actualSource);
        var selection=selectionOwner.PrepareCurrent(present,stages,textures);
        var bindings=OriginalSkyAppliedCloudTextureBindings.PrepareCurrent(selection,textures);
        var clipPacket=constructorPlan?clip.PrepareConstructorPlan(sourceFrame,present,stages,selectedView):
            clip.PrepareCurrent(currentClipScope??throw new InvalidDataException("Cloud current clip requires a typed owned frontend prefix."),sourceFrame,present,stages,selectedView);
        var weather=OriginalSkyAppliedSourceCloudWeatherFields.PrepareCurrent(present);
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
            source.Atmosphere.Fog.SourceMode4Proof?.Validate(source.Atmosphere.Fog);
            var expected=directional.PrepareAtmosphere(OriginalSkyDirectionalAtmosphereNativeSource.Load(repository,locale,OriginalNativeLogDispatch.Sse));
            if(source.Atmosphere.Fog.SourceMode4Proof is null||
                !source.Atmosphere.Fog.Bytes.AsSpan().SequenceEqual(expected.Fog.Bytes)||
                !source.Atmosphere.Scattering.SequenceEqual(expected.Scattering))
                throw new InvalidDataException("Directional Cloud requires actual current Sky mode4 Fog/Scat before its source renderer0/profile1 retains Scat and writes zeroFog.");
            }
        }
        var feature=selection.Shader.ReadG1Bindings().Features[0].Feature;
        if(feature.Words[6]!=0||feature.Words[7]!=1)throw new InvalidDataException("Cloud source row0 no longer retains Sky2 Scat under renderer0/profile1.");
        var sourceCamera=OriginalSkySeasonPackageTextures.Read(selected.FirstPackage).Package.Camera;
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
        var input=Input(weather,present);var phase=new Dictionary<int,float>(acceptedPhase);
        foreach(var row in AnimatedAxes)
        {
            float current=S(phase[row.Phase],M(explicitOwnedClockDelta,input[row.Wind]));
            phase[row.Phase]=NativeFiniteRemainder(current,input[row.Size]);
        }
        for(int lane=0;lane<3;lane++)phase[0x280+lane*4]=NativeFiniteRemainder(A(phase[0x280+lane*4],M(explicitOwnedClockDelta,input[0x110+lane*4])),input[0xF0+lane*4]);
        foreach(var p in phase)input[p.Key]=p.Value;
        uint maximum=weather.ReadBits(0x63C);
        if(maximum is <1 or >4096)throw new NotSupportedException("Current source ray samples exceed the finite GPU work contract.");
        var packets=new OriginalSkyCloudConstantPacket[6];
        for(int face=0;face<6;face++)packets[face]=Packet(selection,source,face,input,maximum,clipPacket.Value,rawSun,geometry40);
        return candidate=new(this,sourceFrame,present,stages,selectedView,selection,bindings,weather,clipPacket,packets,phase,
            native+";"+weather.SourceProvenance+";"+clipPacket.Provenance+";"+ownedClockProvenance+
            ";same-frame1193A90-full-named-VS160/PS512;once1191B40-pure-phase-candidate;owner="+OwnerIdentity);
    }
    internal void RequireCandidate(OriginalSkyAppliedSourceCloudFrame frame)
    {if(!ReferenceEquals(candidate,frame))throw new InvalidDataException("Cloud candidate is stale, superseded or already committed.");}
    public void CommitCompleteFrame(OriginalSkyAppliedSourceCloudFrame frame)
    {RequireCandidate(frame);if(frame.Clip.IsConstructorPlan)throw new InvalidDataException("Cloud constructor plan cannot acknowledge an actual completed Host frame.");frame.ValidateCandidate(frame.SourceFrame,frame.AcceptedPresent);clip.RequireCompletedPacket(frame.Clip);foreach(var p in frame.CandidatePhase)acceptedPhase[p.Key]=p.Value;acceptedFrame=frame.FrameNumber;candidate=null;}

    private static Dictionary<int,float> Input(OriginalSkyAppliedSourceCloudWeatherFields w,OriginalMapSourceCreatedSeasonLightingFrame present)
    {
        var v=CtorInputs.ToDictionary(p=>p.Offset,p=>F(p.Bits));
        float C(int at)=>w.ReadFloat(at);
        void Copy(int target,int camera,int n=1){for(int lane=0;lane<n;lane++)v[target+lane*4]=C(camera+lane*4);}
        Copy(0x9C,0x624);Copy(0xB4,0x628);v[0x90]=w.ReadBits(0x638);v[0x94]=w.ReadBits(0x63C);
        Copy(0x98,0x640);Copy(0xA0,0x648);Copy(0xA4,0x64C);Copy(0xAC,0x658);Copy(0xB8,0x67C);
        Copy(0x260,0x664);Copy(0x264,0x668);v[0x268]=w.ReadBits(0x66C);Copy(0x26C,0x670);
        v[0xD8]=BinaryPrimitives.ReadSingleLittleEndian(present.PreparedCameraPrefix.Span.Slice(0x22C,4));
        if(BitConverter.SingleToUInt32Bits(v[0xD8])!=BitConverter.SingleToUInt32Bits(present.AcceptedWeather.SourceWeatherSaturation22C))
            throw new InvalidDataException("Cloud saturation inputD8 differs from this accepted Main22C weather writer.");
        v[0xC8]=ClampSigned(C(0x680));v[0xCC]=ClampSigned(C(0x684));Copy(0xD0,0x688);Copy(0xD4,0x68C);
        Copy(0xF0,0x690,3);for(int lane=0;lane<3;lane++)v[0xF0+lane*4]=Size(v[0xF0+lane*4]);
        Copy(0x100,0x69C,3);Copy(0x110,0x6A8,3);
        Copy(0xC0,0x6B4);Copy(0xC4,0x71C);Copy(0x130,0x6B8,3);for(int lane=0;lane<3;lane++)v[0x130+lane*4]=Size(v[0x130+lane*4]);
        Copy(0x140,0x6E4,3);Copy(0x150,0x6F0,3);v[0x168]=Size(C(0x720));v[0x16C]=Size(C(0x724));
        Copy(0x170,0x728,2);Copy(0x178,0x730,2);Copy(0x180,0x738,3);
        // Current D7D4C2 exact Cirrus wrapper is nonnull in the loaded owner.
        Copy(0x218,0x744,4);v[0x198]=Size(C(0x764));v[0x19C]=Size(C(0x768));
        Copy(0x1A8,0x76C,2);v[0x1B8]=Size(C(0x774));v[0x1BC]=Size(C(0x778));Copy(0x1C8,0x77C,4);
        v[0x228]=present.AcceptedSeasonCamera.Inputs.Source.Transmittance.Tables.PlanetRadius;
        //11919E0 follows the actual2A0 input constructor and explicitly sets60.
        v[0x60]=32;v[0x64]=128;v[0x68]=32;v[0x6C]=8;
        foreach(float f in v.Values)if(!float.IsFinite(f))throw new InvalidDataException("Cloud finite source input overflowed.");
        return v;
    }
    private static OriginalSkyCloudConstantPacket Packet(OriginalSkyAppliedSourceCloudSelection s,OriginalSkySceneSourceFrame frame,
        int face,Dictionary<int,float> input,uint maximum,Vector4 clip,Vector3 rawSun,float geometry40)
    {
        float R(int at)=>input.TryGetValue(at,out float v)?v:throw new InvalidDataException($"Cloud source input{at:X} has no writer.");
        var c=frame.SixCameras[face];var l=frame.Lights;var controller=frame.Controller!;
        if(c.NativeClipProfile!=2||(c.Flags148&~2u)!=0)throw new NotSupportedException("Cloud current source writer closes ordinary Perspective clip2 only.");
        float far=(c.Flags148&2)!=0?0:1,near=(c.Flags148&2)!=0?1:0;
        Matrix4x4 p2w=OriginalNativeMatrixMath.Inverse3F6EB0(c.MatrixF0,out _);
        Matrix4x4 directionView=DirectionView(face),projection=Projection((c.Flags148&2)!=0);
        Matrix4x4 rays=OriginalNativeMatrixMath.Inverse3F6EB0(TerrainFrustumMath.Multiply(directionView,projection),out _);
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
        for(int group=0;group<5;group++)
        {
            string name=new[]{"Base","Detail","Distort","Cirrus","CirrusDistort"}[group];
            int n=group<2?3:2;int phase=new[]{0xE0,0x120,0x160,0x190,0x1B0}[group],offset=new[]{0x100,0x140,0x170,0x1A0,0x1C0}[group],size=new[]{0xF0,0x130,0x168,0x198,0x1B8}[group];
            float[] scales=new float[n],offsets=new float[n];
            for(int lane=0;lane<n;lane++){scales[lane]=D(1,R(size+lane*4));offsets[lane]=M(A(R(phase+lane*4),R(offset+lane*4)),scales[lane]);}
            Set("g_v"+name+"TexOffset",offsets);Set("g_v"+name+"TexScale",scales);
        }
        Set("g_vRaySampleNum",S(R(0x90),R(0x94)),R(0x94),0,c.Scalar138);
        float bottom=A(R(0x9C),R(0x228));Set("g_vEarthRadius",R(0x228),bottom,A(bottom,R(0xA0)),A(R(0x218),R(0x228)));
        // Actual1742230 descriptor23/24/25 -> cache C50/CD0/D50.
        // Their names are not the arithmetic preparation order.
        Set("g_vCloudParams1",S(1,R(0x98)),R(0xAC),R(0xB4),D(R(0xA4),R(0xA8)));
        Set("g_vCloudParams2",D(R(0xB8),4),M(R(0xB8),2.5f),D(1,R(0xA8)),R(0xB0));
        Set("g_vCloudParams0",R(0x9C),R(0xA0),D(1,R(0xA0)),R(0x22C));
        Set("g_vDistortScale",M(D(1,R(0x130)),R(0x180)),M(D(1,R(0x134)),R(0x184)),M(D(1,R(0x138)),R(0x188)),R(0xD8));
        Set("g_vCirrusParams",R(0x218),S(1,R(0x21C)),R(0x220),R(0x224));
        Set("g_vCirrusDistortScale",M(R(0x1D0),D(1,R(0x198))),M(R(0x1D4),D(1,R(0x19C))));Set("g_vErodeScale",R(0xC0),R(0xC4));
        float h0=M(R(0xC8),R(0xC8)),h1=M(R(0xCC),R(0xCC));
        Set("g_vHgParams0",M(D(S(1,h0),F(0x41490FDB)),R(0xD4)),M(R(0xC8),-2),A(h0,1),R(0xD0));
        Set("g_vHgParams1",M(D(S(1,h1),F(0x41490FDB)),R(0xD4)),M(R(0xCC),-2),A(h1,1),0);
        float sun=controller.SkyInfoZW7C78.X,moon=controller.SkyInfoZW7C78.Y;
        if(sun>0&&M(l.RawSunPosition.Y,-1)>F(0xBE19999A))
        {
            float intensity=A(A(M(l.RawMoonRgb.Y,F(0x3EAAAAAB)),M(l.RawMoonRgb.X,F(0x3EAAAAAB))),M(l.RawMoonRgb.Z,F(0x3EAAAAAB)));
            if(D(F(0x3B000000),geometry40)>intensity)moon=0;
        }
        Set("g_vSky2SunRGB",M(rawSun.X,sun),M(rawSun.Y,sun),M(rawSun.Z,sun),sun);
        Set("g_vSky2SunDir",M(l.RawSunPosition.X,-1),M(l.RawSunPosition.Y,-1),M(l.RawSunPosition.Z,-1),R(0x230));
        Set("g_vSky2MoonRGB",M(l.RawMoonRgb.X,moon),M(l.RawMoonRgb.Y,moon),M(l.RawMoonRgb.Z,moon),moon);
        Set("g_vSky2MoonDir",M(l.RawMoonPosition.X,-1),M(l.RawMoonPosition.Y,-1),M(l.RawMoonPosition.Z,-1),0);
        Set("g_vSky2RayleighExtinction",0,0,0,A(R(0x22C),R(0x228)));Set("g_vSky2MieExtinction",0,0,0,0);
        Set("g_vStableDim",R(0x60),R(0x64),R(0x68),R(0x6C));Set("g_vClampValue",0,0,0,controller.Scalar1F0);
        var vertexDefinitions=s.Shader.ReadResourceDefinitions(s.ColorVertexProgram);
        var pixelDefinitions=s.Shader.ReadResourceDefinitions(s.ColorPixelProgram);
        byte[] vertexBytes=Pack(vertexDefinitions,160,vertexValues),pixel=Pack(pixelDefinitions,s.PixelConstantByteLength,pixelValues);
        return OriginalSkyCloudConstants.ReadColor(vertexDefinitions,pixelDefinitions,vertexBytes,pixel,maximum);
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
        var axis=OriginalSkySceneSourceDefaults.CubeFaces[face];Vector3 z=new(S(0,axis.Direction.X),S(0,axis.Direction.Y),S(0,axis.Direction.Z));
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
    private static float Size(float v)=>v>=float.MaxValue?float.MaxValue:v>F(0x3A9C4000)?v:F(0x3A9C4000);
    private static float ClampSigned(float v)=>v>=F(0x3F7FFF38)?F(0x3F7FFF38):v>F(0xBF7FFF38)?v:F(0xBF7FFF38);
    private static float NativeFiniteRemainder(float value,float period)
    {
        if(!float.IsFinite(value)||!float.IsFinite(period)||period<=0)throw new InvalidDataException("Native finite Cloud phase remainder requires finite positive period.");
        double x=Math.Abs((double)value),y=period;if(x==y)return F(BitConverter.SingleToUInt32Bits(value)&0x80000000u);if(x<y)return value;
        int a=(int)((BitConverter.DoubleToUInt64Bits(x)>>52)&2047),b=(int)((BitConverter.DoubleToUInt64Bits(y)>>52)&2047),blocks=a>b?(a-b)/24:0;
        double scaled=DM(y,BitConverter.UInt64BitsToDouble((ulong)(blocks*24+1023)<<52));
        for(int n=0;n<blocks;n++){x=DS(x,DM((int)DD(x,scaled),scaled));scaled=DM(scaled,BitConverter.UInt64BitsToDouble(0x3E70000000000000));}
        x=DS(x,DM((int)DD(x,scaled),scaled));if(value<0)x=-x;return(float)x;
    }
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DM(double a,double b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DS(double a,double b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static double DD(double a,double b)=>a/b;
    private static readonly int[] PhaseOffsets=[0xE0,0xE4,0xE8,0x120,0x124,0x128,0x160,0x164,0x190,0x194,0x1B0,0x1B4,0x1D8,0x1DC,0x1F8,0x1FC,0x280,0x284,0x288];
    private static readonly (int Phase,int Wind,int Size)[] AnimatedAxes=[(0xE0,0x110,0xF0),(0xE4,0x114,0xF4),(0xE8,0x118,0xF8),(0x120,0x150,0x130),(0x124,0x154,0x134),(0x128,0x158,0x138),(0x160,0x178,0x168),(0x164,0x17C,0x16C),(0x190,0x1A8,0x198),(0x194,0x1AC,0x19C),(0x1B0,0x1C8,0x1B8),(0x1B4,0x1CC,0x1BC),(0x1D8,0x1F0,0x1E0),(0x1DC,0x1F4,0x1E4),(0x1F8,0x210,0x200),(0x1FC,0x214,0x204)];
    private static readonly (int Offset,uint Bits)[] CtorInputs=[(0xA8,0x447A0000u),(0xB0,0x3F800000u),(0x198,0x4AE4E1C0u),(0x19C,0x4AE4E1C0u),(0x1A0,0u),(0x1A4,0u),(0x1A8,0u),(0x1AC,0u),(0x1B8,0x48F42400u),(0x1BC,0x48F42400u),(0x1C0,0u),(0x1C4,0u),(0x1C8,0u),(0x1CC,0u),(0x1D0,0x47927C00u),(0x1D4,0x47927C00u),(0x1E0,0x49F42400u),(0x1E4,0x49F42400u),(0x1E8,0u),(0x1EC,0u),(0x1F0,0u),(0x1F4,0u),(0x200,0x49F42400u),(0x204,0x49F42400u),(0x208,0u),(0x20C,0u),(0x210,0u),(0x214,0u),(0x218,0x49742400u),(0x21C,0x3F000000u),(0x220,0x3F000000u),(0x224,0u),(0x22C,0x4AF42400u),(0x230,0xBD75C28Fu)];
}
public sealed class OriginalSkyAppliedSourceCloudFrame
{
    internal OriginalSkyAppliedSourceCloudOwner SourceCreatedInputOwner => owner;
    private readonly OriginalSkyAppliedSourceCloudOwner owner;private readonly OriginalSkyCloudConstantPacket[] packets;private readonly Dictionary<int,float> phase;
    internal OriginalSkyAppliedSourceCloudFrame(OriginalSkyAppliedSourceCloudOwner owner,OriginalSkySceneSourceFramePacket source,OriginalMapSourceCreatedSeasonLightingFrame present,OriginalSkyAppliedSourceStageFrame stages,OriginalMainSceneNode51Execution view,OriginalSkyAppliedSourceCloudSelection selection,IReadOnlyList<OriginalSkyAppliedCloudTextureBinding> bindings,OriginalSkyAppliedSourceCloudWeatherFields weather,OriginalSkyCloudSourceClipPlanePacket clip,OriginalSkyCloudConstantPacket[] packets,Dictionary<int,float> phase,string provenance)
    {this.owner=owner;SourceFrame=source;AcceptedPresent=present;Stages=stages;SelectedView=view;Selection=selection;Bindings=bindings;Weather=weather;Clip=clip;this.packets=packets;this.phase=phase;SourceProvenance=provenance;}
    public OriginalSkySceneSourceFramePacket SourceFrame {get;}
    public OriginalMapSourceCreatedSeasonLightingFrame AcceptedPresent {get;}
    public OriginalSkyAppliedSourceStageFrame Stages {get;}
    public OriginalMainSceneNode51Execution SelectedView {get;}
    public OriginalSkyAppliedSourceCloudSelection Selection {get;}
    public IReadOnlyList<OriginalSkyAppliedCloudTextureBinding> Bindings {get;}
    public OriginalSkyAppliedSourceCloudWeatherFields Weather {get;}
    public OriginalSkyCloudSourceClipPlanePacket Clip{get;}
    public long FrameNumber=>AcceptedPresent.FrameNumber;
    public string SourceProvenance {get;}
    internal IReadOnlyDictionary<int,float> CandidatePhase=>phase;
    public IReadOnlyList<OriginalSkyCloudConstantPacket> SixPackets=>Array.AsReadOnly(packets.Select(p=>p with{VertexBytes=p.VertexBytes.ToArray(),PixelBytes=p.PixelBytes.ToArray()}).ToArray());
    public void ValidateCandidate(OriginalSkySceneSourceFramePacket source,OriginalMapSourceCreatedSeasonLightingFrame present)
    {if(!ReferenceEquals(SourceFrame,source)||!ReferenceEquals(AcceptedPresent,present))throw new InvalidDataException("Cloud candidate belongs to another exact accepted frame.");SourceFrame.SourceFrame.SourceSelectedAtmosphere?.ValidateFrame(SourceFrame);SourceFrame.SourceFrame.SourceSelectedAtmosphere?.ValidateStage(Stages);Selection.ValidateCurrent(present,Stages);Weather.ValidateCurrent(present);Clip.ValidateCurrent(source,present);owner.RequireCandidate(this);}
}




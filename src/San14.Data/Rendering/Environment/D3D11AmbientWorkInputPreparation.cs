using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record D3D11AmbientWorkConstants(
    IReadOnlyList<SkyScenePreparedCamera> Cameras,
    IReadOnlyList<Sky2DrawInputs> SkyFaces,D3D11MainSkyInputPreparation SkyMedia,
    IReadOnlyList<SkyCloudConstantPacket> Clouds,
    IReadOnlyList<D3D11CloudAnimationAxis> CloudAxes,
    LoadedSkyAmbientResources Ambient,SkyAmbientFilterSelection Filter,bool SkyEnabled,
    SkyCloudColorProgramSelection CloudProgram,
    SkyTextureSource? EarthTexture,SkyAppliedCloudTextureSet CloudTextures)
{
    public CubeRenderTextureDefinition Target => Ambient.SpecularTargets[0];
}

public sealed record D3D11CloudAnimationAxis(int SourcePhase,int PixelOffset,
    float Wind,float Period,float Position,float Scale);

/// <summary>Fresh WORK constants from the actual current Main and its selected
/// default-caller light. Animation/history remain owned by the native session.
/// No numeric cache is read by this preparation.</summary>
public static class D3D11AmbientWorkInputPreparation
{
    public static D3D11AmbientWorkConstants Prepare(GameResourceRepository repository,
        MapSourceCreatedSeasonLightingFrame present,
        DirectionalLightProviderTemporary defaultCaller,SkyCloudProgramResources cloudPrograms,
        SkyMainProgramResources? skyPrograms = null)
    {
        string locale=present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale;
        var hdr=repository.LoadShaderContainer("system",0x9D8E811F);
        var binding=new MapLightingBinding("ambient-work/view","ambient-work/main","ambient-work/light80",
            hdr.Source.ResourceUri+"@"+hdr.Source.DecodedSha256,0,4,MapLightingBasePath.UnmodifiedOrdinary);
        var inputs=new MapSourceCreatedLightingFrameInputs(present.FrameNumber,binding,present.Parameters,
            present.PriorPreparedCameraPrefix,defaultCaller.Root.ContextFlags3E4,
            present.FrameDelta,MapLightingRuntimeTable.Present,
            MapLightingCameraScope.LightingPrefix,0,true,new(),PresentSeasonCamera:present);
        var lighting=MapSourceCreatedLighting.PrepareFreshDefaultCaller(defaultCaller,inputs);
        var selected=SkySceneSelectedViewLightSource.Capture(lighting.LocalLightData.Span,
            new(present.FrameNumber,binding.ViewIdentity,"ambient-work/selected80",binding.LightOwnerIdentity));
        var directional=SkyDirectionalSelectedLightSource.ReadCurrent(selected,lighting);
        var stages=SkyAppliedSourceStageOwner.CreateFresh(repository,locale,"ambient-work/work",binding.CameraOwnerIdentity,
            present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.Source)
            .PrepareCurrent(present,lighting);
        var ambient=SkyAmbientResources.Load(repository,repository.LoadObjects("system",0xDAB638B2),
            0x910603AC,repository.LoadObjects("system",0x718CCB8B),repository.LoadObjects("system",0x93FB3280));
        skyPrograms ??= SkyMainProgramResources.Load(repository,NamedViewRendererResources.Load(repository));
        var skyShader=skyPrograms.Shader;
        // This preparation owns its binding state while producing initial numeric inputs.
        using var bindings = new Sky2TextureBindings();
        bindings.ApplyPackage(SkySeasonPackageTextures.Read(
            present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage),
            reference => SkySeasonPackageTextures.LoadSampling(repository, reference));
        var textureBindings = bindings.Capture();
        directional.PrepareLights(skyShader, textureBindings);
        var frame=directional.PrepareSourceSelectedSeasonFrame(ambient.SpecularTargets[0],
            SkyDirectionalAtmosphereNativeSource.Load(repository,locale,NativeLogDispatch.Sse),2,skyShader,stages,textureBindings);
        var selections=directional.CaptureDesiredSelection(skyShader,frame);
        var desired=selections.GetSelection(false).Combination;
        var atmosphereProgram=selections.GetSelection(true).Combination.Stages.Single(stage=>stage.StageCode==0).Entry!.ProgramIndex;
        var prepared=SkySceneFramePreparation.PrepareFreshDirectionalFullyWritten(frame.SourceFrame,directional,
            skyShader.ReadResourceDefinitions(atmosphereProgram),
            skyShader.ReadResourceDefinitions(desired.Stages.Single(stage=>stage.StageCode==0).Entry!.ProgramIndex));
        var skyProgram=skyPrograms.ImportSelection(new(desired.Index,Program(desired,1),Program(desired,0)));
        var general=MainSceneNode51Source.Load(repository,locale);
        var view=general.PrepareCurrent(new(present.FrameNumber,"ambient-work/node51","ambient-work/view-collection",
            general.ViewCollectionSourceNameHash,true,true,null,0,binding,true,"owned-first-Main-selected-view0"));
        var clouds=SkyAppliedSourceCloudOwner.CreateFresh(repository,locale,"ambient-work/clouds",cloudPrograms)
            .PrepareDirectionalBootstrap(frame,present,stages,view,0,"fresh-owned-Cloud-phase0;native-shares-node3-phase",directional);
        var cloudPackets=clouds.SixPackets.ToArray();
        for(int face=0;face<6;face++)
        {
            if(face is 0 or 1 or 4 or 5)
            {
                var plane=SkyWorkCloudClipPreparation.Prepare(present.PreparedCameraPrefix.Span,
                    frame.SourceFrame.SixCameras[face],face,present.AcceptedSeasonCamera.Inputs.Source.Transmittance.Tables.PlanetRadius);
                Vector(cloudPackets[face].VertexBytes,0,plane);
            }
        }
        var skyMedia=D3D11MainSkyInputPreparation.Load(skyProgram,clouds.Bindings,textureBindings);
        var filter=SkyAmbientCurrentFilterSelection.CaptureCurrent(ambient.Shader,present).Selection;
        return new(frame.SourceFrame.SixCameras,prepared.DrawPackets,skyMedia,cloudPackets,
            CloudAxes(clouds.Weather,clouds.Selection.ProgramSelection.PixelDefinitions),
            ambient,filter,stages.Sky2Enabled,clouds.Selection.ProgramSelection,
            textureBindings.GetTexture(SkySamplingRole.Earth)?.Texture,clouds.Bindings);
    }

    public static void WriteNativeInputs(GameResourceRepository repository,
        D3D11AmbientWorkConstants constants,string inputRoot,string locale,
        D3D11AmbientRenderResources? preparedResources = null)
    {
        string ambient=Path.Combine(inputRoot,"ambient-input"),cloud=Path.Combine(inputRoot,"ambient-cloud-continuous-input"),
            current=Path.Combine(inputRoot,"current-ambient-frame-input"),
            initial=Path.Combine(current,"spring");
        foreach(string root in new[]{ambient,cloud,initial})Directory.CreateDirectory(root);
        void Write(string root,string name,byte[] data)=>File.WriteAllBytes(Path.Combine(root,name),data);
        (preparedResources ?? D3D11AmbientRenderResources.Load(repository,constants,locale)).WriteNativeInputs(inputRoot);
        Write(ambient,"quad-position-xy.bin",Floats([-1,1,-1,-1,1,1,1,-1]));
        for(int face=0;face<6;face++)
        {
            var c=constants.Clouds[face];
            Write(ambient,$"clouds-face{face}-vs-b0.bin",c.VertexBytes);Write(ambient,$"clouds-face{face}-ps-b0.bin",c.PixelBytes);
            Write(cloud,$"base-face{face}-vs-b0.bin",c.VertexBytes);Write(cloud,$"base-face{face}-ps-b0.bin",c.PixelBytes);
            Write(initial,$"cloud-face{face}-vs.bin",c.VertexBytes);Write(initial,$"cloud-face{face}-ps.bin",c.PixelBytes);
        }
        WriteStarCameras(Path.Combine(ambient,"star-cameras.bin"),constants.Cameras);
        File.WriteAllLines(cloud+"/animation.txt",constants.CloudAxes.Select(a=>FormattableString.Invariant(
            $"{a.SourcePhase} {a.PixelOffset} {a.Wind:R} {a.Period:R} {a.Position:R} {a.Scale:R}")));
        File.WriteAllLines(ambient+"/work-face-depth.txt",constants.Cameras.Select((c,face)=>FormattableString.Invariant(
            $"{face} {c.Flags148} {((c.Flags148&2)!=0?0:1)}")));
    }

    private static void WriteStarCameras(string path,IReadOnlyList<SkyScenePreparedCamera> cameras)
    {
        if(cameras.Count!=6)throw new InvalidDataException("WORK Star preparation requires six actual cameras.");
        using var writer=new BinaryWriter(File.Create(path));
        writer.Write(0x31435357u); // WSC1: WORK Star cameras, independent of material storage.
        foreach(var camera in cameras)
        {
            writer.Write(camera.Position.Y);writer.Write(camera.Scalar138);
            var m=camera.MatrixF0;
            foreach(float value in new[]{m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
                m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44})writer.Write(value);
        }
    }

    private static IReadOnlyList<D3D11CloudAnimationAxis> CloudAxes(SkyAppliedSourceCloudWeatherFields weather,DxbcResourceDefinitions definitions)
    {
        var rows=new List<D3D11CloudAnimationAxis>();var buffer=definitions.Buffers.Single();
        foreach(var group in new[]{("Base",0xE0,0x6A8,0x690,0x69C,3),("Detail",0x120,0x6F0,0x6B8,0x6E4,3),
            ("Distort",0x160,0x730,0x720,0x728,2),("Cirrus",0x190,0x76C,0x764,-1,2),("CirrusDistort",0x1B0,0x77C,0x774,-1,2)})
        {
            int offset=buffer.Variables.Single(v=>v.Name.Text=="g_v"+group.Item1+"TexOffset").StartOffset;
            for(int lane=0;lane<group.Item6;lane++)
            {
                float period=weather.ReadFloat(group.Item4+lane*4);period=period>=float.MaxValue?float.MaxValue:period>.0011920929f?period:.0011920929f;
                rows.Add(new(group.Item2+lane*4,offset+lane*4,weather.ReadFloat(group.Item3+lane*4),period,
                    group.Item5<0?0:weather.ReadFloat(group.Item5+lane*4),1/period));
            }
        }
        return rows;
    }
    private static int Program(G2ShaderCombination combination,int stage)=>combination.Stages.Single(s=>s.StageCode==stage&&s.Entry is not null).Entry!.ProgramIndex;
    private static byte[] Floats(float[] values){byte[] bytes=new byte[values.Length*4];for(int i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*4),values[i]);return bytes;}
    private static void Vector(byte[] bytes,int offset,Vector4 v)=>Floats([v.X,v.Y,v.Z,v.W]).CopyTo(bytes,offset);
}

public static partial class MapSourceCreatedLighting
{
    /// <summary>Borrow the registered root's actual force=-1/pair0/weight0
    /// provider temporary and publish this request's camera writes.</summary>
    public static MapSourceCreatedLightingPacket PrepareFreshDefaultCaller(
        DirectionalLightProviderTemporary temporary,MapSourceCreatedLightingFrameInputs input)
    {
        var present=input.PresentSeasonCamera!;var camera=present.PreparedCameraPrefix.ToArray();
        var local=temporary.ApplyCurrentCamera(camera);
        var scene=input.AdditionalSceneInputs with{
            Lighting=DeferredSceneConstants.ReadPreparedBaseLighting(local.Bytes.AsSpan(0,0x1B0),4),
            Camera=PreparedCameraSceneSource.ReadCompletePrefix(camera),
            Ambient=DeferredSceneConstants.ReadAmbientModifier(local.Bytes.AsSpan(0x1B0,0x178),0),
            Fog=DeferredSceneConstants.ReadSharedFog(local.Bytes.AsSpan(0,0x1B0),true)};
        return new(temporary.Root,input,camera,local,scene);
    }
}

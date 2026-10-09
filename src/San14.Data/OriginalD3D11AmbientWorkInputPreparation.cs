using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalD3D11AmbientWorkConstants(
    IReadOnlyList<OriginalSkyScenePreparedCamera> Cameras,
    IReadOnlyList<byte[]> SkyVertex,IReadOnlyList<byte[]> SkyPixel,
    IReadOnlyList<OriginalSkyCloudConstantPacket> Clouds,
    IReadOnlyList<OriginalSkyStarConstantPacket> Stars,float StarClockScale,
    IReadOnlyList<OriginalD3D11AmbientCloudAxis> CloudAxes,
    OriginalCubeRenderTextureDefinition Target,OriginalSkyAmbientFilterSelection Filter,uint StarPointCount,bool SkyEnabled,
    uint SelectedCameraObjectId);

public sealed record OriginalD3D11AmbientCloudAxis(int SourcePhase,int PixelOffset,
    float Wind,float Period,float Position,float Scale);

/// <summary>Fresh WORK constants from the actual current Main and its selected
/// default-caller light. Animation/history remain owned by the native session.
/// No numeric cache is read by this preparation.</summary>
public static class OriginalD3D11AmbientWorkInputPreparation
{
    public static OriginalD3D11AmbientWorkConstants Prepare(OriginalGameRepository repository,
        OriginalMapSourceCreatedSeasonLightingFrame present,
        OriginalDirectionalLightProviderTemporary defaultCaller)
    {
        string locale=present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale;
        var hdr=repository.LoadShaderContainer("system",0x9D8E811F);
        var binding=new OriginalMapLightingBinding("ambient-work/view","ambient-work/main","ambient-work/light80",
            hdr.Source.ResourceUri+"@"+hdr.Source.DecodedSha256,0,4,OriginalMapLightingBasePath.UnmodifiedOrdinary);
        var inputs=new OriginalMapSourceCreatedLightingFrameInputs(present.FrameNumber,binding,present.Parameters,
            present.PriorPreparedCameraPrefix,defaultCaller.Root.ContextFlags3E4,
            present.FrameDelta,OriginalMapLightingRuntimeTable.Present,
            OriginalMapLightingCameraScope.LightingPrefix,0,true,new(),PresentSeasonCamera:present);
        var lighting=OriginalMapSourceCreatedLighting.PrepareFreshDefaultCaller(defaultCaller,inputs);
        var selected=OriginalSkySceneSelectedViewLightSource.Capture(lighting.LocalLightData.Span,
            new(present.FrameNumber,binding.ViewIdentity,"ambient-work/selected80",binding.LightOwnerIdentity));
        var directional=OriginalSkyDirectionalSelectedLightSource.ReadCurrent(selected,lighting);
        var stages=OriginalSkyAppliedSourceStageOwner.CreateFresh(repository,locale,"ambient-work/work",binding.CameraOwnerIdentity)
            .PrepareCurrent(present,lighting);
        var ambient=OriginalSkyAmbientResources.Load(repository,repository.LoadObjects("system",0xDAB638B2),
            0x910603AC,repository.LoadObjects("system",0x718CCB8B),repository.LoadObjects("system",0x93FB3280));
        var skyShader=repository.LoadShaderContainer("system",0x340AC335);
        directional.PrepareLights(skyShader,OriginalSky2SourceNull2D.FromAppliedSeasonOnFreshShader(
            present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage,OriginalSkySamplingRole.Moon,
            OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader));
        var frame=directional.PrepareSourceSelectedSeasonFrame(ambient.SpecularTargets[0],
            OriginalSkyDirectionalAtmosphereNativeSource.Load(repository,locale,OriginalNativeLogDispatch.Sse),2,skyShader,stages);
        var prepared=OriginalSkySceneFramePreparation.PrepareFreshDirectionalFullyWritten(frame.SourceFrame,directional);
        var desired=directional.CaptureDesiredSelection(skyShader,frame).GetSelection(false).Combination;
        var vertexDefinitions=skyShader.ReadResourceDefinitions(Program(desired,1));
        var pixelDefinitions=skyShader.ReadResourceDefinitions(Program(desired,0));
        var general=OriginalMainSceneNode51Source.Load(repository,locale);
        var view=general.PrepareCurrent(new(present.FrameNumber,"ambient-work/node51","ambient-work/view-collection",
            general.ViewCollectionSourceNameHash,true,true,null,0,binding,true,"owned-first-Main-selected-view0"));
        var starOwner=OriginalSkyAppliedSourceStarOwner.CreateFresh(repository,locale,"ambient-work/stars");
        var stars=starOwner.PrepareCurrent(frame,present,stages,view,0,"fresh-owned-Star-phase0;native-shares-node3-delta");
        var clouds=OriginalSkyAppliedSourceCloudOwner.CreateFresh(repository,locale,"ambient-work/clouds")
            .PrepareDirectionalBootstrap(frame,present,stages,view,0,"fresh-owned-Cloud-phase0;native-shares-node3-phase",directional);
        var cloudPackets=clouds.SixPackets.ToArray();
        var trig=OriginalMapPostEffect3PresentLightMathSource.Load(repository,locale);
        for(int face=0;face<6;face++)
        {
            if(face is 0 or 1 or 4 or 5)
            {
                var plane=OriginalSkyWorkCloudClipPreparation.Prepare(present.PreparedCameraPrefix.Span,
                    frame.SourceFrame.SixCameras[face],face,present.AcceptedSeasonCamera.Inputs.Source.Transmittance.Tables.PlanetRadius,trig);
                Vector(cloudPackets[face].VertexBytes,0,plane);
            }
        }
        byte[][] skyVertex=prepared.DrawPackets.Select(draw=>OriginalSky2Constants.Vertex(vertexDefinitions,
            new byte[vertexDefinitions.Buffers.Single().ByteSize],draw.Vertex).Bytes).ToArray();
        byte[][] skyPixel=prepared.DrawPackets.Select(draw=>OriginalSky2Constants.Pixel(pixelDefinitions,
            new byte[pixelDefinitions.Buffers.Single().ByteSize],draw.Pixel).Bytes).ToArray();
        var package=present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage.Camera;
        float starScale=BinaryPrimitives.ReadSingleLittleEndian(package.Properties[621].RawValue);
        var filter=OriginalSkyAmbientCurrentFilterSelection.CaptureCurrent(ambient.Shader,present).Selection;
        return new(frame.SourceFrame.SixCameras,skyVertex,skyPixel,cloudPackets,stars.SixPackets,starScale,
            CloudAxes(clouds.Weather,clouds.Selection.Shader.ReadResourceDefinitions(clouds.Selection.ColorPixelProgram)),
            ambient.SpecularTargets[0],filter,checked((uint)starOwner.Geometry.PointCount),stages.Sky2Enabled,package.ObjectId);
    }

    public static void WriteNativeInputs(OriginalGameRepository repository,
        OriginalD3D11AmbientWorkConstants constants,string inputRoot,string locale)
    {
        string ambient=Path.Combine(inputRoot,"ambient-input"),cloud=Path.Combine(inputRoot,"ambient-cloud-continuous-input"),
            stars=Path.Combine(inputRoot,"ambient-stars-continuous-input"),current=Path.Combine(inputRoot,"current-ambient-frame-input"),
            initial=Path.Combine(current,"spring");
        foreach(string root in new[]{ambient,cloud,stars,initial})Directory.CreateDirectory(root);
        void Write(string root,string name,byte[] data)=>File.WriteAllBytes(Path.Combine(root,name),data);
        var formats=OriginalRenderTextureFormatMath.Resolve(constants.Target.PixelFormat,constants.Target.ColorSpace,constants.Target.OutputColorSpace);
        File.WriteAllText(Path.Combine(ambient,"ambient-native.txt"),FormattableString.Invariant(
            $"{constants.Target.Width} {constants.Target.LevelNum} {formats.TargetViewFormat} {constants.Filter.SampleCount} 0 {constants.StarPointCount} {(constants.SkyEnabled?1:0)}\n"));
        var sampler=OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale);
        byte[] samplerKey=OriginalDeferredSamplerSources.AmbientSpecularKey(OriginalDeferredSamplerSources.CubeTextureKey(sampler,constants.Target.LevelNum));
        Write(ambient,"cube-sampler-key.bin",samplerKey);Write(ambient,"cube-sampler-desc.bin",OriginalDeferredSamplerSources.DecodeKey(sampler,samplerKey).EncodeD3D11Descriptor());
        Write(ambient,"quad-position-xy.bin",Floats([-1,1,-1,-1,1,1,1,-1]));
        for(int face=0;face<6;face++)
        {
            var c=constants.Clouds[face];var s=constants.Stars[face];
            Write(ambient,$"sky-current-face{face}-vs-b0.bin",constants.SkyVertex[face]);
            Write(ambient,$"sky-current-face{face}-ps-b0.bin",constants.SkyPixel[face]);
            Write(ambient,$"clouds-face{face}-vs-b0.bin",c.VertexBytes);Write(ambient,$"clouds-face{face}-ps-b0.bin",c.PixelBytes);
            Write(ambient,$"stars-face{face}-vs-b0.bin",s.VertexBytes);Write(ambient,$"stars-face{face}-gs-b0.bin",s.GeometryBytes);
            Write(ambient,$"stars-face{face}-ps-b0.bin",s.PixelBytes);
            Write(ambient,$"blend-face{face}-vs-b0.bin",OriginalSkyCubeBlendFaceMath.EncodeNativeFaceMatrix((uint)face));
            Write(cloud,$"base-face{face}-vs-b0.bin",c.VertexBytes);Write(cloud,$"base-face{face}-ps-b0.bin",c.PixelBytes);
            Write(stars,$"stars-face{face}-vs-b0.bin",s.VertexBytes);Write(stars,$"stars-face{face}-gs-b0.bin",s.GeometryBytes);
            Write(stars,$"stars-face{face}-ps-b0.bin",s.PixelBytes);
            Write(initial,$"sky-face{face}-vs.bin",constants.SkyVertex[face]);Write(initial,$"sky-face{face}-ps.bin",constants.SkyPixel[face]);
            Write(initial,$"cloud-face{face}-vs.bin",c.VertexBytes);Write(initial,$"cloud-face{face}-ps.bin",c.PixelBytes);
            Write(initial,$"star-face{face}-vs.bin",s.VertexBytes);Write(initial,$"star-face{face}-gs.bin",s.GeometryBytes);Write(initial,$"star-face{face}-ps.bin",s.PixelBytes);
        }
        Write(ambient,"blend-t0-ps-b0.bin",new byte[16]);
        Write(stars,"star-clock-parameters.bin",Floats([constants.StarClockScale,..constants.Cameras.Select(c=>c.Position.Y),0]));
        File.WriteAllLines(cloud+"/animation.txt",constants.CloudAxes.Select(a=>FormattableString.Invariant(
            $"{a.SourcePhase} {a.PixelOffset} {a.Wind:R} {a.Period:R} {a.Position:R} {a.Scale:R}")));
        File.WriteAllLines(ambient+"/work-face-depth.txt",constants.Cameras.Select((c,face)=>FormattableString.Invariant(
            $"{face} {c.Flags148} {((c.Flags148&2)!=0?0:1)}")));
        WriteClockScales(repository,current,constants.SelectedCameraObjectId);
        var samples=new Dictionary<uint,OriginalSkyFilterGeneratedSamples>();
        for(uint mip=1;mip<constants.Target.LevelNum;mip++)
        {
            var sample=OriginalSkyFilterSampleMath.GenerateMip(constants.Filter.SampleCount,checked((ushort)constants.Target.Width),
                constants.Target.LevelNum,mip,OriginalNativeLogTables.Load(repository.SourceDirectory,locale),OriginalNativeLogDispatch.AvxFma,constants.Filter.SampleCount);
            samples.Add(mip,sample);Write(ambient,$"filter-mip{mip}-t17-half4.bin",sample.Packet.DirectionBytes.ToArray());
            Write(ambient,$"filter-mip{mip}-t18-float.bin",sample.Packet.WeightBytes.ToArray());
        }
        foreach(var step in OriginalSkySceneSchedule.BasePreparation(constants.Target.LevelNum))
        {
            float width=step.Operation==OriginalSkySceneCopyOperation.WorkMip?
                OriginalCubeRenderTextureResources.ResolveSurface(constants.Target,step.DestinationSurfaceId).Extent:constants.Target.Width;
            Write(ambient,$"copy-surface{step.DestinationSurfaceId}-b0.bin",Floats([width,0,0,step.Face]));
        }
        foreach(var step in OriginalSkySceneSchedule.Filter(constants.Target.LevelNum,6,uint.MaxValue))
            Write(ambient,$"filter-surface{step.DestinationSurfaceId}-b0.bin",Floats([constants.Target.Width,samples[step.Mip].Packet.WeightSum,0,step.Face]));
    }

    private static void WriteClockScales(OriginalGameRepository repository,string directory,uint selectedCameraObjectId)
    {
        using var writer=new BinaryWriter(File.Create(Path.Combine(directory,"star-clock-scales.bin")));writer.Write(4u);
        foreach(int month in new[]{2,6,9,12})
        {
            var action=OriginalMapSeasonResourceConsumer.LoadActionSource(repository,month);
            var database=OriginalMapSeasonResourceConsumer.LoadActionDatabase(repository,action);
            var camera=OriginalMapSeasonResourceConsumer.ReadCameraPackage(database,selectedCameraObjectId).Camera;
            writer.Write((uint)action.Selection.SeasonIndex);writer.Write(BinaryPrimitives.ReadSingleLittleEndian(camera.Properties[621].RawValue));
        }
    }

    private static IReadOnlyList<OriginalD3D11AmbientCloudAxis> CloudAxes(OriginalSkyAppliedSourceCloudWeatherFields weather,DxbcResourceDefinitions definitions)
    {
        var rows=new List<OriginalD3D11AmbientCloudAxis>();var buffer=definitions.Buffers.Single();
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

public static partial class OriginalMapSourceCreatedLighting
{
    /// <summary>Borrow the registered root's actual force=-1/pair0/weight0
    /// provider temporary and publish this request's camera writes.</summary>
    public static OriginalMapSourceCreatedLightingPacket PrepareFreshDefaultCaller(
        OriginalDirectionalLightProviderTemporary temporary,OriginalMapSourceCreatedLightingFrameInputs input)
    {
        var present=input.PresentSeasonCamera!;var camera=present.PreparedCameraPrefix.ToArray();
        var local=temporary.ApplyCurrentCamera(camera);
        var scene=input.AdditionalSceneInputs with{
            Lighting=OriginalDeferredSceneConstants.ReadPreparedBaseLighting(local.Bytes.AsSpan(0,0x1B0),4),
            Camera=OriginalPreparedCameraSceneSource.ReadCompletePrefix(camera),
            Ambient=OriginalDeferredSceneConstants.ReadAmbientModifier(local.Bytes.AsSpan(0x1B0,0x178),0),
            Fog=OriginalDeferredSceneConstants.ReadSharedFog(local.Bytes.AsSpan(0,0x1B0),true)};
        return new(temporary.Root,input,camera,local,scene);
    }
}

using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record D3D11AmbientWorkConstants(
    IReadOnlyList<SkyScenePreparedCamera> Cameras,
    IReadOnlyList<Sky2DrawInputs> SkyFaces,D3D11MainSkyInputPreparation SkyMedia,
    IReadOnlyList<SkyCloudConstantPacket> Clouds,
    IReadOnlyList<D3D11CloudAnimationAxis> CloudAxes,
    CubeRenderTextureDefinition Target, SkyCloudColorProgramSelection CloudProgram);

/// <summary>Fresh WORK constants from the actual current Main and its selected
/// typed light and atmosphere. Animation/history remain owned by the native session.
/// No numeric cache is read by this preparation.</summary>
public static class D3D11AmbientWorkInputPreparation
{
    public static D3D11AmbientWorkConstants Prepare(GameResourceRepository repository,
        MapSourceCreatedSeasonLightingFrame present,
        CurrentDirectionalLightState light, SkyScenePreparedController controller,
        Vector3 sunRgb, Vector3 moonRgb, float controllerGeometry40, SkyCloudProgramResources cloudPrograms,
        LoadedSkyAmbientResources ambient,
        SkyMainProgramResources? skyPrograms = null, Sky2TextureBindingSnapshot? sampling = null)
    {
        var inputs = present.AcceptedSeasonCamera.Inputs;
        var source = inputs.Source;
        var selection = source.Selection;
        // The Camera writer supplies Sun/Moon, the first directional light and
        // Fog before its optional third-color continuation. WORK reads only
        // those fields, so either current third-color branch has the same ABI.
        if (!light.ContextApplied || !light.Mode6Applied || light.Exposure.Type != 6)
            throw new InvalidDataException("WORK requires the current mode-six Camera directional light.");
        if (selection.SecondPackage is not null || inputs.MainFrame.Camera.Source.Declaration.SkyDomeSceneKey != 0 ||
            selection.FirstPackage.SkyDomeSceneKey != 0)
            throw new NotSupportedException("WORK requires the direct selected package and an implemented null scene branch.");
        var target = ambient.SpecularTargets[0];
        var camera = present.CurrentSkyCamera;
        if (camera.Parameters.NativeTargetFormat != target.PixelFormat)
            throw new InvalidDataException("WORK target format differs from the current Sky parameters.");
        skyPrograms ??= SkyMainProgramResources.Load(repository,NamedViewRendererResources.Load(repository));
        var skyShader=skyPrograms.Shader;
        // A retained WORK owner supplies its own sampling generation. Standalone
        // initial preparation keeps the original fresh-binding behavior.
        using var bindings = sampling is null ? new Sky2TextureBindings() : null;
        if (bindings is not null)
            bindings.ApplyPackage(SkySeasonPackageTextures.Read(
                present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage),
                reference => SkySeasonPackageTextures.LoadSampling(repository, reference));
        var textureBindings = sampling ?? bindings!.Capture();
        textureBindings.RequireCurrent();
        if (textureBindings.Presence.Moon || textureBindings.Presence.Milky)
            throw new NotSupportedException("Fresh WORK requires the source-null Moon and Milky texture family.");
        byte[] request = SkyDirectionalRendererFeatureWriter.WriteRequest(light.Fog.Mode);
        var selector = Sky2Selection.Read(skyShader);
        var atmosphereSelection = selector.SelectDesired(request, true, false, light.Exposure.Type, light.Exposure.Flags, true);
        var desired = selector.SelectDesired(request, true, false, light.Exposure.Type, light.Exposure.Flags, false).Combination;
        var atmosphereDefinitions = skyShader.ReadResourceDefinitions(Program(atmosphereSelection.Combination, 0));
        var contributionDefinitions = skyShader.ReadResourceDefinitions(Program(desired, 0));
        var baselineMoon = new Vector3(light.MoonBaseline.X, light.MoonBaseline.Y, light.MoonBaseline.Z);
        Sky2SourceZeroMoon? zeroMoon = null;
        if (BitConverter.SingleToUInt32Bits(baselineMoon.X) == 0 &&
            BitConverter.SingleToUInt32Bits(baselineMoon.Y) == 0 && BitConverter.SingleToUInt32Bits(baselineMoon.Z) == 0)
        {
            zeroMoon = Sky2SourceZeroMoon.Create(textureBindings.RequireNull(SkySamplingRole.Moon), baselineMoon);
            if (moonRgb != Vector3.Zero)
                throw new InvalidDataException("Zero Moon baseline cannot cover a nonzero current color.");
            // The zero setter retains its baseline positive zeros after checking the current color.
            moonRgb = baselineMoon;
        }
        var moonCache = zeroMoon is null ? Sky2CameraLightPreparation.PrepareRgbCache(moonRgb) :
            Sky2CameraLightPreparation.PrepareSourceZeroMoonCache(moonRgb, zeroMoon);
        var lights = new SkySceneLights("current-WORK-directional-light", true,
            controller.SunPosition ?? throw new InvalidDataException("WORK has no current Sun position."),
            Sky2CameraLightPreparation.PrepareRgbCache(sunRgb), true,
            controller.MoonPosition ?? throw new InvalidDataException("WORK has no current Moon position."),
            moonRgb, moonCache.Maximum, light.Lighting.Directional[0].Direction)
            { SourceZeroMoon = zeroMoon, SourceMoonRgbCache1470 = zeroMoon is null ? null : moonCache };
        var packageFog = SkyDirectionalRendererFeatureWriter.PreparePackageFog(light.Fog,
            inputs.MainFrame.Camera.Source.Declaration.FogLightingBlend);
        var feature = atmosphereSelection.RendererFeature.Feature.Words;
        var color = light.Lighting.Directional[0].Color;
        Vector3 directionalRgb = new(color.X, color.Y, color.Z);
        SkyFogPacket fog;
        IReadOnlyList<Vector4> scattering;
        if (feature[6] == 7 && feature[7] == 2)
        {
            fog = SkyFogMath.GenerateWeather(packageFog.Mode, packageFog.Values, 2, true);
            scattering = fog.RequiresMode7NeutralScattering ? fog.ScatteringOverride!.Values :
                SkyScatteringMath.GenerateWeather(packageFog.Values, directionalRgb).Values;
        }
        else if (feature[6] == 4 && feature[7] == 1)
        {
            // The mode-four writer uses float-max for nonincreasing intervals.
            fog = SkyDirectionalMode4FogReceipt.Generate(light.Fog.Mode, light.Fog.Values.Start, light.Fog.Values.End, float.MaxValue);
            scattering = SkyScatteringMode4.Generate(light.Fog.Values,
                new(directionalRgb, 1, Vector4.Zero, null), source.AtmosphereScattering).Values;
        }
        else throw new NotSupportedException("WORK requires an implemented renderer fog mode and profile.");
        var cameras = SkySceneSourceDefaults.PrepareCubeCameras(present.PreparedCameraPrefix.Span,
            camera.CameraOwnerProvenance, 2, target.Width, target.Width);
        var frame = new SkySceneSourceFrame(cameras,
            camera.Parameters with { MilkyStrength1484 = controller.MilkyStrength1484 }, controller.Controller,
            lights, textureBindings.Presence, new("current-WORK-atmosphere", fog, scattering));
        var prepared = SkySceneFramePreparation.PrepareFreshFullyWritten(frame, atmosphereDefinitions, contributionDefinitions);
        var skyProgram=skyPrograms.ImportSelection(new(desired.Index,Program(desired,1),Program(desired,0)));
        var weather = present.CloudWeather;
        if (weather.Settings.RayExponent != 4 || weather.Settings.MinimumRaySamples is < 1 or > 4096 ||
            !float.IsFinite(controllerGeometry40) || controllerGeometry40 == 0)
            throw new NotSupportedException("WORK Cloud requires ray exponent four, supported sample count and finite nonzero geometry scale.");
        var cloudProgram = cloudPrograms.SelectColor(weather.State.LowCostLevel);
        source.CloudTextures.RequireSelectedPackage(selection.FirstPackage);
        var cloudBindings = SkyAppliedCloudTextureSet.Import(source.CloudTextures);
        SkyAppliedCloudTextureBindings.ValidateProgram(cloudProgram);
        var material = present.CloudMaterial;
        var volume = material.PrepareVolume(SkyCloudAnimationState.Fresh.Advance(material, 0));
        float planetRadius = source.RequireRenderingAtmosphere().PlanetRadius;
        var cloudPackets = new SkyCloudConstantPacket[6];
        for(int face=0;face<6;face++)
        {
            var plane = face is 0 or 1 or 4 or 5 ? SkyWorkCloudClipPreparation.Prepare(
                present.PreparedCameraPrefix.Span, cameras[face], face, planetRadius) : Vector4.Zero;
            cloudPackets[face] = SkyCloudConstants.PrepareCurrentFace(cloudProgram, frame, face, material, volume,
                planetRadius, weather.Settings.MinimumRaySamples, plane, sunRgb, controllerGeometry40);
        }
        var skyMedia=D3D11MainSkyInputPreparation.Load(skyProgram,cloudBindings,textureBindings);
        return new(cameras,prepared.DrawPackets,skyMedia,cloudPackets,
            SkyCloudAnimationEncoding.CreateAxes(material,cloudProgram.PixelDefinitions),target,cloudProgram);
    }

    public static void WriteNativeInputs(D3D11AmbientWorkConstants constants,string inputRoot)
    {
        string ambient=Path.Combine(inputRoot,"ambient-input"),cloud=Path.Combine(inputRoot,"ambient-cloud-continuous-input"),
            current=Path.Combine(inputRoot,"current-ambient-frame-input"),
            initial=Path.Combine(current,"spring");
        foreach(string root in new[]{ambient,cloud,initial})Directory.CreateDirectory(root);
        void Write(string root,string name,byte[] data)=>File.WriteAllBytes(Path.Combine(root,name),data);
        constants.SkyMedia.WriteNativeInputs(Path.Combine(ambient,"sky"));
        for(int face=0;face<constants.SkyFaces.Count;face++)
        {
            byte[] material=new byte[SkyMainMaterialEncoding.ByteLength];
            SkyMainMaterialEncoding.Write(constants.SkyFaces[face],material);
            Write(Path.Combine(ambient,"sky"),$"face{face}.bin",material);
        }
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

    private static int Program(G2ShaderCombination combination,int stage)=>combination.Stages.Single(s=>s.StageCode==stage&&s.Entry is not null).Entry!.ProgramIndex;
    private static byte[] Floats(float[] values){byte[] bytes=new byte[values.Length*4];for(int i=0;i<values.Length;i++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*4),values[i]);return bytes;}
}

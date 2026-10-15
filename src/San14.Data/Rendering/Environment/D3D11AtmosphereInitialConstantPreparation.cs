using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record D3D11AtmosphereInitialOptions(int Width,int Height,int NativeTargetFormat,
    Vector4 CloudClipPlane,float CloudElapsedSeconds,float CommonClock);

public static class D3D11AtmosphereInitialCallerRules
{
    // D38940 ->118D940 sets this MAIN clamp every call. WORK cube construction
    // has a separate zero clamp; it must not borrow this caller's values.
    public static Vector4 MainSkyClamp => new(10000,10000,10000,0);
    // Only fresh frontend mask0 / special cache clearing belongs to this rule.
    public static Vector4 FreshMainCloudClip => Vector4.Zero;
}

public sealed record D3D11AtmosphereInitialLight(uint GroupMask,CurrentDirectionalLightState State);

public sealed record D3D11AtmosphereInitialInputs(MapSourceCreatedSeasonLightingFrame Present,
    IReadOnlyDictionary<int,uint> CurrentMainWords,IReadOnlyList<D3D11AtmosphereInitialLight> Lights,
    SkyScenePreparedController Controller,Sky2PreparedLight Sun,Sky2PreparedLight Moon,
    Vector3 SunRgb,Vector3 MoonRgb,float ControllerGeometry40,D3D11AtmosphereInitialOptions Options,
    HeightFogSettings? HeightFog = null,uint? ActiveFogGroups = null);

public sealed record D3D11AtmosphereProgramSelection(int Combination,int VertexProgram,int PixelProgram);

public sealed record D3D11AtmosphereInitialFogConstants(string Name,
    HeightFogProgram? Program,byte[] VertexBytes,byte[] PixelBytes,
    IReadOnlyList<(int Offset,float Velocity)> Phases,uint Mask)
{
    public D3D11AtmosphereProgramSelection? Programs => Program?.Programs;
}

public sealed record D3D11MainSkyConstants(SkyMainProgramSelection Program, Sky2DrawInputs Material);

public sealed record AtmosphereInitialConstants(byte[] CameraProfile,
    D3D11MainSkyConstants? Sky, D3D11MainCloudConstants Cloud, HeightFogInitialFrame HeightFog)
{
    public IReadOnlyList<D3D11AtmosphereInitialFogConstants> Fog => HeightFog.Passes;
}

public sealed record D3D11MainCloudColorConstants(byte[] Vertex, byte[] Pixel);

public sealed record D3D11MainCloudConstants(SkyCloudMainProgramSelection Programs,D3D11MainCloudColorConstants? Color,
    byte[] HistoryVertex,byte[] HistoryPixel,byte[] CompositeVertex,byte[] CompositePixel,
    IReadOnlyList<D3D11CloudAnimationAxis> Axes,byte[] CurrentW2P,byte[] PreviousW2P,float Cloudy);

/// <summary>Fresh complete named Sky/MainCloud/SSFog constants from the actual
/// current Main and season. It reads resource declarations and owned scalar
/// rules; retained history and subsequent clock updates remain native owners.</summary>
public static partial class D3D11AtmosphereInitialConstantPreparation
{
    private sealed record CurrentAtmosphereContext(byte[] Main, CurrentDirectionalLightState Light,
        SkySceneSharedAtmosphere Atmosphere,
        Vector4 Eye, Vector4 Target, Vector4 Up, CameraLens Lens,
        MapCameraConstructionMatrices Matrices);

    private sealed record CurrentCameraContext(byte[] Main, Vector4 Eye, Vector4 Target, Vector4 Up,
        CameraLens Lens, MapCameraConstructionMatrices Matrices);

    private static CurrentCameraContext PrepareCameraContext(D3D11AtmosphereInitialInputs input)
    {
        var options = input.Options;
        if (options.Width <= 0 || options.Height <= 0 || !float.IsFinite(options.CloudElapsedSeconds) ||
            options.CloudElapsedSeconds < 0 || !float.IsFinite(options.CommonClock))
            throw new InvalidDataException("Atmosphere initialization needs actual finite clocks and target dimensions.");
        if (input.Present.AcceptedSeasonCamera.Inputs.Source.Selection.SecondPackage is not null)
            throw new NotSupportedException("Atmosphere initialization needs the actual paired-package numeric producer.");
        byte[] main = input.Present.PreparedCameraPrefix.ToArray();
        Vector4 eye = V(main, 0x10), target = V(main, 0x20), up = V(main, 0x30);
        eye.Y = A(M(F(main, 0x210), 100000), eye.Y);
        var lens = CameraLens.DecodePrefix(main.AsSpan(0, 0x160));
        var matrices = MapCameraConstructionMath.CompleteAB6C60(eye, target, up, lens, 2, NativeCameraTangentDispatch.Sse);
        return new(main, eye, target, up, lens, matrices);
    }

    private static CurrentAtmosphereContext PrepareContext(D3D11AtmosphereInitialInputs input,
        G1ShaderBindingTables tables, CurrentCameraContext camera)
    {
        var present = input.Present;
        byte[] main = camera.Main;
        var selected = input.Lights.First(light => (light.GroupMask & 0x80) != 0).State;
        byte[] request = SkyDirectionalRendererFeatureWriter.WriteRequest(selected.Fog.Mode);
        int featureIndex = Feature(tables, request);
        var feature = tables.Features[featureIndex].Feature.Words;
        var packageFog = SkyDirectionalRendererFeatureWriter.PreparePackageFog(selected.Fog, F(main, 0x1EC));
        var color = selected.Lighting.Directional[0].Color;
        Vector3 directionalRgb = new(color.X, color.Y, color.Z);
        SkyFogPacket fog;
        IReadOnlyList<Vector4> scattering;
        if(feature[6]==7)
        {
            fog=SkyFogMath.GenerateWeather(packageFog.Mode,packageFog.Values,checked((int)feature[7]),true);
            scattering=fog.RequiresMode7NeutralScattering ? fog.ScatteringOverride!.Values :
                SkyScatteringMath.GenerateWeather(packageFog.Values,directionalRgb).Values;
        }
        else if(feature[6] is 0 or 4&&feature[7]==1&&packageFog.Mode==0)
        {
            // Cloud's feature0 and Sky's feature4 use different program-bank
            // codes. The same inactive package still prepares its inherited
            // mode4 scattering; Cloud's no-fog program does not consume it.
            fog=new(4,1,true,0,Vector4.Zero,new byte[16],false,null);
            scattering=SkyScatteringMode4.Generate(packageFog.Values,new(directionalRgb,1,Vector4.Zero,null),
                present.AcceptedSeasonCamera.Inputs.Source.AtmosphereScattering).Values;
        }
        else throw new NotSupportedException("Initial atmosphere requires its actual selected Fog/scattering producer.");
        var atmosphere = new SkySceneSharedAtmosphere("owned-current-Main/selected-group80", fog, scattering);
        return new(main, selected, atmosphere, camera.Eye, camera.Target,
            camera.Up, camera.Lens, camera.Matrices);
    }

    /// <summary>Prepares Main Cloud independently of Sky geometry and height-fog
    /// program support. The combined preparation uses the same context and writer.</summary>
    public static D3D11MainCloudConstants PrepareCloudConstants(D3D11AtmosphereInitialInputs input,
        D3D11CloudProgramMedia cloudPrograms, D3D11CloudRenderResources cloudResources)
    {
        var camera = PrepareCameraContext(input);
        var context = input.Present.AcceptedSeasonCamera.Inputs.Source.RenderingAtmosphere is null
            ? null : PrepareContext(input, cloudPrograms.Resources.Bindings, camera);
        return PrepareCloud(cloudPrograms.Main, cloudResources, input, camera.Eye, camera.Target, camera.Up,
            camera.Lens, camera.Matrices.WorldToProjection100, context?.Atmosphere);
    }

    public static RenderTextureDefinition LoadFreshMainTarget(GameResourceRepository repository,
        MapSourceCreatedSeasonLightingFrame present)
    {
        var graph=repository.LoadRenderGraph("system",0x024496EE);
        var textures=repository.LoadObjects("system",0x93FB3280);
        var common=CommonRenderResources.ImportSettings(repository.LoadObjects("KIDSSystemResource",0x6D011726));
        uint Name(uint hash)=>BinaryPrimitives.ReadUInt32LittleEndian(graph.Document.Nodes[1].Call.Arguments.Single(argument=>argument.NameHash==hash).RawValue);
        int selector=BinaryPrimitives.ReadInt32LittleEndian(present.PreparedCameraPrefix.Span[0x1CC..]);
        uint? terminal=MapSceneProxySourcePolicy.ReadSourceCreatedFreshTerminal(selector,common.Flags8,common.Flags0C,
            Name(0x514B2AC3),Name(0x0EBBA230),Name(0xD776D144));
        return terminal is uint name?RenderTextureResources.Read(textures,name):
            RenderGraphTargetResources.Read(graph,73,textures).ColorTargets.Single().Texture;
    }

    public static AtmosphereInitialConstants Prepare(GameResourceRepository repository,
        D3D11AtmosphereInitialInputs input,D3D11MainCloudConstants cloud,
        SkyMainProgramResources skyResources, Sky2TextureBindingSnapshot sampling,
        HeightFogProgramResources? fogPrograms = null)
    {
        var present=input.Present;var options=input.Options;
        sampling.RequireCurrent();
        var baseCamera = PrepareCameraContext(input);
        var parameters=present.CurrentSkyCamera.Parameters with{NativeTargetFormat=options.NativeTargetFormat,ClampDelta1610=D3D11AtmosphereInitialCallerRules.MainSkyClamp};
        var transform=Sky2GeometryPreparation.PrepareSkyTransform(parameters.Angle1430,parameters.Angle1434,parameters.Angle143C);
        var fogPackets=PrepareFog(repository,input,baseCamera.Main,baseCamera.Lens,fogPrograms ?? HeightFogProgramResources.Load(repository));
        var profile=Profile(input,baseCamera.Main,transform,baseCamera.Lens);
        if (present.AcceptedSeasonCamera.Inputs.Source.RenderingAtmosphere is null)
            return new(profile,null,cloud,fogPackets);
        var skyShader=skyResources.Shader;
        var skyTables=skyShader.ReadG1Bindings();
        var context = PrepareContext(input, skyTables, baseCamera);
        byte[] main = context.Main;
        var selected = context.Light;
        var presence = sampling.Presence;
        var skyProgram = SelectSkyProgram(present, selected, skyResources, presence);
        var atmosphere = context.Atmosphere;
        var fog = atmosphere.Fog;
        var scattering = atmosphere.Scattering;
        Vector4 eye = context.Eye;
        var lens = context.Lens;
        var matrices = context.Matrices;
        var ctl=input.Controller.Controller;
        var camera=Sky2CameraLightPreparation.PrepareCamera(matrices.WorldToView80,matrices.ProjectionC0,
            eye,transform,lens.Flags,2,ctl.Matrix1B0);
        var sunScreen=Sky2GeometryPreparation.PrepareSunScreenPosition(true,input.Sun.PreparedNegatedSunPosition!.Value,
            Words(matrices.WorldToProjection100),lens.VerticalFovRadians,lens.Aspect,options.Width,options.Height,parameters.SunSize1490)!;
        var moonGeometry=Sky2MoonGeometryPreparation.Prepare(true,
            new(input.Moon.Direction.X,input.Moon.Direction.Y,input.Moon.Direction.Z),input.Controller.MoonPosition!.Value,
            Words(matrices.WorldToProjection100),lens.VerticalFovRadians,lens.Aspect,options.Width,options.Height,parameters.MoonSize1498)!;
        var vertexInputs = new Sky2VertexInputs(fog.Value, camera.CameraToLocal, camera.ProjectionFarZ, ctl.Matrix1B0);
        var pixelInputs = new Sky2PixelInputs(fog.Value, Array.AsReadOnly(scattering.ToArray()),
            Sky2ParameterPreparation.PrepareViewPosition(eye,parameters.AltitudeInfo,parameters.SkyYOffset1438,transform),
            Sky2ParameterPreparation.PrepareSkyInfo0(lens.VerticalFovRadians,options.Height,
                true,parameters.SunSize1490,true,parameters.MoonSize1498,ctl.SkyInfoZW7C78),
            Sky2ParameterPreparation.PrepareSkyInfo1(presence.Earth, presence.Moon, presence.Milky, parameters.MilkyScale14A0,
                parameters.MilkyStrength1484,selected.Lighting.Directional[0].Direction,input.Controller.MoonPosition.Value),
            input.Sun.Rgb,input.Sun.Direction,input.Moon.Rgb,input.Moon.Direction,
            sunScreen.Position,parameters.AltitudeInfo,parameters.StableDimensions,
            Sky2ParameterPreparation.PrepareClampValue(options.NativeTargetFormat,parameters.ClampDelta1610,ctl.Scalar1F0),
            moonGeometry.MoonFace,moonGeometry.Screen.Position);
        var skyMaterial = new Sky2DrawInputs(vertexInputs, pixelInputs);
        return new(profile,new(skyProgram,skyMaterial),cloud,fogPackets);
    }

    /// <summary>Select the normal Main's two nonnull prepared light inputs.
    /// Texture presence comes from the installed logical binding, not package keys.</summary>
    public static SkyMainProgramSelection SelectSkyProgram(MapSourceCreatedSeasonLightingFrame present,
        ReadOnlySpan<byte> selectedLight, SkyMainProgramResources resources, SkySceneTexturePresence textures)
        => SelectSkyProgram(present, SkyDirectionalRendererFeatureWriter.WriteRequest(selectedLight),
            selectedLight[0x10B0], selectedLight[0xFDC], resources, textures);

    public static SkyMainProgramSelection SelectSkyProgram(MapSourceCreatedSeasonLightingFrame present,
        CurrentDirectionalLightState selectedLight, SkyMainProgramResources resources, SkySceneTexturePresence textures)
        => SelectSkyProgram(present, SkyDirectionalRendererFeatureWriter.WriteRequest(selectedLight.Fog.Mode),
            selectedLight.Exposure.Type, selectedLight.Exposure.Flags, resources, textures);

    private static SkyMainProgramSelection SelectSkyProgram(MapSourceCreatedSeasonLightingFrame present,
        byte[] request, byte lightType, byte lightFlags, SkyMainProgramResources resources, SkySceneTexturePresence textures)
    {
        var tables = resources.Shader.ReadG1Bindings();
        int feature = Feature(tables, request);
        byte[] variant = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(variant, 1); // Moon light is present; Sun light is nonnull.
        BinaryPrimitives.WriteUInt32LittleEndian(variant.AsSpan(8),
            lightType == 4 || (lightFlags & 2) != 0 ? 1u : 0u);
        var binding = tables.Variants.Match(variant)
            ?? throw new NotSupportedException("Current Main Sky light variant has no selected resource combination.");
        uint pass = F(present.PreparedCameraPrefix.Span, 0x1D0) == 0 || textures.Earth ? 0u : 1u;
        var combination = tables.ResolveCombination([(uint)feature, 0, 0, 0, 0, 0, (uint)binding.Index], pass);
        return resources.ImportSelection(Programs(combination));
    }

    public static void WriteNativeInputs(AtmosphereInitialConstants constants,
        string atmosphereDirectory,string mainSkyDirectory,string mainCloudDirectory,string fogDirectory)
    {
        void Write(string root,string name,byte[] bytes){string path=Path.Combine(root,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,bytes);}
        Write(atmosphereDirectory,"profile.bin",constants.CameraProfile);
        if (constants.Sky is { } sky)
            SkyMainMaterialEncoding.WriteNativeInputs(mainSkyDirectory, sky.Material);
        foreach(string root in new[]{Path.Combine(atmosphereDirectory,"cloud"),mainCloudDirectory})
        {
            if (constants.Cloud.Color is { } color)
            {
                Write(root,"pass1-vs-b0.bin",color.Vertex);Write(root,"pass1-ps-b0.bin",color.Pixel);
            }
            Write(root,"pass2-vs-b0.bin",constants.Cloud.HistoryVertex);Write(root,"pass2-ps-b0.bin",constants.Cloud.HistoryPixel);
            Write(root,"pass4-vs-b0.bin",constants.Cloud.CompositeVertex);
            if(constants.Cloud.CompositePixel.Length!=0)Write(root,"pass4-ps-b0.bin",constants.Cloud.CompositePixel);
            else File.Delete(Path.Combine(root,"pass4-ps-b0.bin"));
        }
        string continuous=Path.Combine(Path.GetDirectoryName(mainCloudDirectory)!,"main-cloud-continuous-input");
        Write(continuous,"current-w2p.bin",constants.Cloud.CurrentW2P);
        Write(continuous,"previous-w2p.bin",constants.Cloud.PreviousW2P);
        Write(continuous,"cloudy.bin",BitConverter.GetBytes(constants.Cloud.Cloudy));
        File.WriteAllLines(Path.Combine(continuous,"animation.txt"),constants.Cloud.Axes.Select(axis=>FormattableString.Invariant(
            $"{axis.SourcePhase} {axis.PixelOffset} {axis.Wind:R} {axis.Period:R} {axis.Position:R} {axis.Scale:R}")));
        File.WriteAllLines(Path.Combine(continuous,"pending-vector.txt"),constants.Cloud.Axes.Take(3).Select(axis=>FormattableString.Invariant($"{axis.Wind:R} {axis.Period:R}")));
        constants.HeightFog.WriteNativeInputs(fogDirectory);
    }

    private static byte[] Profile(D3D11AtmosphereInitialInputs input,byte[] main,float[] transform,CameraLens lens)
    {
        using var output=new MemoryStream();using(var writer=new BinaryWriter(output,System.Text.Encoding.ASCII,true))
        {
            writer.Write(main);foreach(float value in transform)writer.Write(value);
            foreach(float value in Words(input.Controller.Controller.Matrix1B0))writer.Write(value);
            foreach(float value in Words(input.Sun.PreparedNegatedSunPosition!.Value))writer.Write(value);
            foreach(float value in Words(input.Controller.MoonPosition!.Value))writer.Write(value);
            var p=input.Present.CurrentSkyCamera.Parameters;foreach(float value in Words(p.AltitudeInfo))writer.Write(value);
            foreach(float value in new[]{p.SkyYOffset1438,p.SunSize1490,p.MoonSize1498,input.Controller.Controller.SkyInfoZW7C78.X,
                input.Controller.Controller.SkyInfoZW7C78.Y,NativeCameraTangent.Tangent1020A90(M(lens.VerticalFovRadians,.5f),NativeCameraTangentDispatch.Sse)})writer.Write(value);
            writer.Write(input.Options.Width);writer.Write(input.Options.Height);
        }
        return output.ToArray();
    }

    private sealed class Fields:Dictionary<string,uint[]>
    {
        internal void Set(string name,params float[] values)=>Add(name,values.Select(BitConverter.SingleToUInt32Bits).ToArray());
        internal void SetInt(string name,uint value)=>Add(name,[value]);
    }
    private static byte[] Pack(DxbcResourceDefinitions definitions,int byteLength,Fields fields)
    {
        var buffer=definitions.Buffers.Single();
        if(buffer.ByteSize!=byteLength||buffer.Variables.Count!=fields.Count)
            throw new NotSupportedException("Atmosphere named fields do not cover this loaded shader layout.");
        // The new buffer has defined zero padding. Every named semantic field
        // must have its producer; no absent shader value is filled with zero.
        byte[] bytes=new byte[byteLength];
        foreach(var variable in buffer.Variables)
        {
            if(!fields.TryGetValue(variable.Name.Text,out uint[]? values))
                throw new NotSupportedException("Atmosphere named writer is missing: "+variable.Name.Text);
            var type=definitions.Types.Single(value=>value.Offset==variable.TypeOffset);
            int elementWords=type.Rows*type.Columns,elements=Math.Max(1,(int)type.Elements);
            if(type.Members.Count!=0||values.Length!=elementWords*elements)
                throw new NotSupportedException("Atmosphere field shape is not supplied: "+variable.Name.Text);
            int stride=type.Elements==0?elementWords*4:(elementWords*4+15)&~15;
            for(int element=0;element<elements;element++)for(int word=0;word<elementWords;word++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(variable.StartOffset+element*stride+word*4),values[element*elementWords+word]);
        }
        return bytes;
    }
    private static int Feature(G1ShaderBindingTables tables,byte[] request)
    {
        var match=G1ShaderFeatureMatcher.Select(G1ShaderFeatureRowReader.ReadRow(request).Words.ToArray(),tables.Features.Select(row=>row.Feature).ToArray());
        return match.RowIndex>=0?match.RowIndex:throw new NotSupportedException("Current atmosphere renderer feature has no source combination.");
    }
    private static D3D11AtmosphereProgramSelection Programs(G2ShaderCombination combination)=>new(combination.Index,
        combination.Stages.Single(stage=>stage.StageCode==1).Entry!.ProgramIndex,
        combination.Stages.Single(stage=>stage.StageCode==0).Entry!.ProgramIndex);
    private static float F(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]);
    private static Vector4 V(ReadOnlySpan<byte> bytes,int at)=>new(F(bytes,at),F(bytes,at+4),F(bytes,at+8),F(bytes,at+12));
    private static float[] Words(Vector4 value)=>[value.X,value.Y,value.Z,value.W];
    private static float[] Words(Matrix4x4 v)=>[v.M11,v.M12,v.M13,v.M14,v.M21,v.M22,v.M23,v.M24,v.M31,v.M32,v.M33,v.M34,v.M41,v.M42,v.M43,v.M44];
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}

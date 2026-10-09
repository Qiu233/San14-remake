using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalD3D11AtmosphereInitialOptions(int Width,int Height,int NativeTargetFormat,
    Vector4 CloudClipPlane,float CloudElapsedSeconds,float CommonClock);

public static class OriginalD3D11AtmosphereInitialCallerRules
{
    // D38940 ->118D940 sets this MAIN clamp every call. WORK cube construction
    // has a separate zero clamp; it must not borrow this caller's values.
    public static Vector4 MainSkyClamp => new(10000,10000,10000,0);
    // Only fresh frontend mask0 / special cache clearing belongs to this rule.
    public static Vector4 FreshMainCloudClip => Vector4.Zero;
}

public sealed record OriginalD3D11AtmosphereInitialLight(uint GroupMask,ReadOnlyMemory<byte> CompleteData);

public sealed record OriginalD3D11AtmosphereInitialInputs(OriginalMapSourceCreatedSeasonLightingFrame Present,
    IReadOnlyDictionary<int,uint> CurrentMainWords,IReadOnlyList<OriginalD3D11AtmosphereInitialLight> Lights,
    OriginalSkyScenePreparedController Controller,OriginalSky2PreparedLight Sun,OriginalSky2PreparedLight Moon,
    Vector3 SunRgb,Vector3 MoonRgb,float ControllerGeometry40,OriginalD3D11AtmosphereInitialOptions Options);

public sealed record OriginalD3D11AtmosphereProgramSelection(int Combination,int VertexProgram,int PixelProgram);

public sealed record OriginalD3D11AtmosphereInitialFogConstants(string Name,
    OriginalD3D11AtmosphereProgramSelection Programs,byte[] VertexBytes,byte[] PixelBytes,
    IReadOnlyList<(int Offset,float Velocity)> Phases);

public sealed record OriginalD3D11AtmosphereInitialConstants(byte[] CameraProfile,
    OriginalD3D11AtmosphereProgramSelection SkyPrograms,byte[] SkyVertexBytes,byte[] SkyPixelBytes,
    OriginalD3D11AtmosphereProgramSelection CloudPrograms,byte[] CloudVertexBytes,byte[] CloudPixelBytes,
    byte[] CloudHistoryVertexBytes,byte[] CloudHistoryPixelBytes,
    IReadOnlyList<OriginalD3D11AtmosphereInitialFogConstants> Fog);

/// <summary>Fresh complete named Sky/MainCloud/SSFog constants from the actual
/// current Main and season. It reads resource declarations and owned scalar
/// rules; retained history and subsequent clock updates remain native owners.</summary>
public static partial class OriginalD3D11AtmosphereInitialConstantPreparation
{
    public static OriginalRenderTextureDefinition LoadFreshMainTarget(OriginalGameRepository repository,
        OriginalMapSourceCreatedSeasonLightingFrame present)
    {
        var graph=repository.LoadRenderGraph("system",0x024496EE);
        var textures=repository.LoadObjects("system",0x93FB3280);
        var common=OriginalMapFreshCommonResources.ReadSourceTemplate(repository.LoadObjects("KIDSSystemResource",0x6D011726));
        uint Name(uint hash)=>BinaryPrimitives.ReadUInt32LittleEndian(graph.Document.Nodes[1].Call.Arguments.Single(argument=>argument.NameHash==hash).RawValue);
        int selector=BinaryPrimitives.ReadInt32LittleEndian(present.PreparedCameraPrefix.Span[0x1CC..]);
        uint? terminal=OriginalMapSceneProxySourcePolicy.ReadSourceCreatedFreshTerminal(selector,common.Flags8,common.Flags0C,
            Name(0x514B2AC3),Name(0x0EBBA230),Name(0xD776D144));
        return terminal is uint name?OriginalRenderTextureResources.Read(textures,name):
            OriginalRenderGraphTargetResources.Read(graph,73,textures).ColorTargets.Single().Texture;
    }

    public static OriginalD3D11AtmosphereInitialConstants Prepare(OriginalGameRepository repository,
        OriginalD3D11AtmosphereInitialInputs input)
    {
        var present=input.Present;var options=input.Options;
        if(options.Width<=0||options.Height<=0||!float.IsFinite(options.CloudElapsedSeconds)||options.CloudElapsedSeconds<0||
            !float.IsFinite(options.CommonClock))throw new InvalidDataException("Atmosphere initialization needs actual finite clocks and target dimensions.");
        var package=present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
        if(present.AcceptedSeasonCamera.Inputs.Source.Selection.SecondPackage is not null)
            throw new NotSupportedException("Atmosphere initialization needs the actual paired-package numeric producer.");
        byte[] main=present.PreparedCameraPrefix.ToArray();
        var selected=input.Lights.First(light=>(light.GroupMask&0x80)!=0).CompleteData;
        var skyShader=repository.LoadShaderContainer("system",0x340AC335);
        var skyTables=skyShader.ReadG1Bindings();
        byte[] request=OriginalSkyDirectionalRendererFeatureWriter.WriteRequest(selected.Span);
        int skyFeature=Feature(skyTables,request);
        bool earth=TextureReference(package.Camera,0xB0EE87B8);
        bool moonTexture=TextureReference(package.Camera,0xF83B0CC1);
        bool milky=TextureReference(package.Camera,0x66BCE906);
        if(moonTexture||milky)throw new NotSupportedException("Fresh Main Sky needs its actual Moon/Milky descriptor installation.");
        byte[] skyVariant=new byte[12];BinaryPrimitives.WriteUInt32LittleEndian(skyVariant,1);
        BinaryPrimitives.WriteUInt32LittleEndian(skyVariant.AsSpan(8),selected.Span[0x10B0]==4||(selected.Span[0xFDC]&2)!=0?1u:0u);
        var skyBinding=skyTables.Variants.Match(skyVariant)??throw new InvalidDataException("Main Sky current binding variant is absent.");
        uint skyPass=F(main,0x1D0)==0||earth?0u:1u;
        var skyCombination=skyTables.ResolveCombination([(uint)skyFeature,0,0,0,0,0,(uint)skyBinding.Index],skyPass);
        var skyPrograms=Programs(skyCombination);
        var feature=skyTables.Features[skyFeature].Feature.Words;
        if(feature[6]!=7)throw new NotSupportedException("Initial Main Sky requires its selected non-mode7 Fog/Scat producer.");
        byte[] fog96=OriginalSkyDirectionalRendererFeatureWriter.CopyPackageFog96(selected.Span.Slice(0x150,96),F(main,0x1EC));
        var fog=OriginalSkyFogMath.Generate(7,checked((int)feature[7]),true,fog96);
        var scattering=fog.RequiresMode7NeutralScattering?fog.ScatteringOverride!.Values:
            OriginalSkyScatteringMath.Generate(7,fog96,Rgb(selected.Span,0x10)).Values;
        var atmosphere=new OriginalSkySceneSharedAtmosphere("owned-current-Main/selected-group80",fog,scattering);
        var parameters=present.CurrentSkyCamera.Parameters with{NativeTargetFormat=options.NativeTargetFormat,ClampDelta1610=OriginalD3D11AtmosphereInitialCallerRules.MainSkyClamp};
        var transform=OriginalSky2GeometryPreparation.PrepareSkyTransform(parameters.Angle1430,parameters.Angle1434,parameters.Angle143C);
        Vector4 eye=V(main,0x10),target=V(main,0x20),up=V(main,0x30);
        eye.Y=A(M(F(main,0x210),100000),eye.Y);
        var lens=OriginalCameraLens.DecodePrefix(main.AsSpan(0,0x160));
        var matrices=OriginalMapCameraConstructionMath.CompleteAB6C60(eye,target,up,lens,2,OriginalNativeCameraTangentDispatch.Sse);
        var ctl=input.Controller.Controller;
        var camera=OriginalSky2CameraLightPreparation.PrepareCamera(matrices.WorldToView80,matrices.ProjectionC0,
            eye,transform,lens.Flags,2,ctl.Matrix1B0);
        var sunScreen=OriginalSky2GeometryPreparation.PrepareSunScreenPosition(true,input.Sun.PreparedNegatedSunPosition!.Value,
            Words(matrices.WorldToProjection100),lens.VerticalFovRadians,lens.Aspect,options.Width,options.Height,parameters.SunSize1490)!;
        var moonGeometry=OriginalSky2MoonGeometryPreparation.Prepare(true,
            new(input.Moon.Direction.X,input.Moon.Direction.Y,input.Moon.Direction.Z),input.Controller.MoonPosition!.Value,
            Words(matrices.WorldToProjection100),lens.VerticalFovRadians,lens.Aspect,options.Width,options.Height,parameters.MoonSize1498,
            OriginalSky2MoonGeometryNativeSource.Load(repository,present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale))!;
        var skyVertex=new Fields();
        skyVertex.Set("g_mC2L",Words(camera.CameraToLocal));skyVertex.Set("g_fProjFarZ",camera.ProjectionFarZ);skyVertex.Set("g_mL2G",Words(ctl.Matrix1B0));
        var skyPixel=new Fields();
        skyPixel.Set("fog",Words(fog.Value));skyPixel.Set("Scat",scattering.SelectMany(Words).ToArray());
        skyPixel.Set("g_vViewPos",Words(OriginalSky2ParameterPreparation.PrepareViewPosition(eye,parameters.AltitudeInfo,parameters.SkyYOffset1438,transform)));
        skyPixel.Set("g_vSkyInfo0",Words(OriginalSky2ParameterPreparation.PrepareSkyInfo0(lens.VerticalFovRadians,options.Height,
            true,parameters.SunSize1490,true,parameters.MoonSize1498,ctl.SkyInfoZW7C78)));
        skyPixel.Set("g_vSkyInfo1",Words(OriginalSky2ParameterPreparation.PrepareSkyInfo1(earth,false,false,parameters.MilkyScale14A0,
            parameters.MilkyStrength1484,V(selected.Span,0),input.Controller.MoonPosition.Value)));
        skyPixel.Set("g_vSunRGB",Words(input.Sun.Rgb));skyPixel.Set("g_vSunDir",Words(input.Sun.Direction));
        skyPixel.Set("g_vMoonRGB",Words(input.Moon.Rgb));skyPixel.Set("g_vMoonDir",Words(input.Moon.Direction));
        skyPixel.Set("g_mMoonFace",Words(moonGeometry.MoonFace));skyPixel.Set("g_vSunScreenPos",Words(sunScreen.Position));
        skyPixel.Set("g_vMoonScreenPos",Words(moonGeometry.Screen.Position));skyPixel.Set("g_vAltitudeInfo",Words(parameters.AltitudeInfo));
        skyPixel.Set("g_vStableDim",Words(parameters.StableDimensions));
        skyPixel.Set("g_vClampValue",Words(OriginalSky2ParameterPreparation.PrepareClampValue(options.NativeTargetFormat,parameters.ClampDelta1610,ctl.Scalar1F0)));
        var cloud=PrepareCloud(repository,input,eye,target,up,lens,matrices.WorldToProjection100,atmosphere,request,earth);
        var fogPackets=PrepareFog(repository,input,main,lens);
        return new(Profile(input,main,transform,lens),skyPrograms,
            Pack(skyShader.ReadResourceDefinitions(skyPrograms.VertexProgram),144,skyVertex),
            Pack(skyShader.ReadResourceDefinitions(skyPrograms.PixelProgram),336,skyPixel),cloud.Programs,cloud.Vertex,cloud.Pixel,
            cloud.HistoryVertex,cloud.HistoryPixel,fogPackets);
    }

    public static void WriteNativeInputs(OriginalD3D11AtmosphereInitialConstants constants,
        string atmosphereDirectory,string mainSkyDirectory,string mainCloudDirectory,string fogDirectory)
    {
        void Write(string root,string name,byte[] bytes){string path=Path.Combine(root,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,bytes);}
        Write(atmosphereDirectory,"profile.bin",constants.CameraProfile);
        foreach(string root in new[]{atmosphereDirectory,mainSkyDirectory})
        {Write(root,"main-sky-vs-b0.bin",constants.SkyVertexBytes);Write(root,"main-sky-ps-b0.bin",constants.SkyPixelBytes);}
        foreach(string root in new[]{Path.Combine(atmosphereDirectory,"cloud"),mainCloudDirectory})
        {
            Write(root,"pass1-vs-b0.bin",constants.CloudVertexBytes);Write(root,"pass1-ps-b0.bin",constants.CloudPixelBytes);
            Write(root,"pass2-vs-b0.bin",constants.CloudHistoryVertexBytes);Write(root,"pass2-ps-b0.bin",constants.CloudHistoryPixelBytes);
            Write(root,"pass4-vs-b0.bin",constants.CloudHistoryVertexBytes);
        }
        foreach(var fog in constants.Fog)
        {
            Write(atmosphereDirectory,"fog/"+fog.Name+"-vs-b0.bin",fog.VertexBytes);
            Write(atmosphereDirectory,"fog/"+fog.Name+"-ps-b0.bin",fog.PixelBytes);
            Write(fogDirectory,fog.Name+"/vs-b0.bin",fog.VertexBytes);Write(fogDirectory,fog.Name+"/ps-b0.bin",fog.PixelBytes);
            File.WriteAllLines(Path.Combine(fogDirectory,fog.Name,"phase-velocities.txt"),fog.Phases.Select(row=>
                row.Offset.ToString(CultureInfo.InvariantCulture)+" "+row.Velocity.ToString("R",CultureInfo.InvariantCulture)));
        }
    }

    private static byte[] Profile(OriginalD3D11AtmosphereInitialInputs input,byte[] main,float[] transform,OriginalCameraLens lens)
    {
        using var output=new MemoryStream();using(var writer=new BinaryWriter(output,System.Text.Encoding.ASCII,true))
        {
            writer.Write(main);foreach(float value in transform)writer.Write(value);
            foreach(float value in Words(input.Controller.Controller.Matrix1B0))writer.Write(value);
            foreach(float value in Words(input.Sun.PreparedNegatedSunPosition!.Value))writer.Write(value);
            foreach(float value in Words(input.Controller.MoonPosition!.Value))writer.Write(value);
            var p=input.Present.CurrentSkyCamera.Parameters;foreach(float value in Words(p.AltitudeInfo))writer.Write(value);
            foreach(float value in new[]{p.SkyYOffset1438,p.SunSize1490,p.MoonSize1498,input.Controller.Controller.SkyInfoZW7C78.X,
                input.Controller.Controller.SkyInfoZW7C78.Y,OriginalNativeCameraTangent.Tangent1020A90(M(lens.VerticalFovRadians,.5f),OriginalNativeCameraTangentDispatch.Sse)})writer.Write(value);
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
    private static OriginalD3D11AtmosphereProgramSelection Programs(G2ShaderCombination combination)=>new(combination.Index,
        combination.Stages.Single(stage=>stage.StageCode==1).Entry!.ProgramIndex,
        combination.Stages.Single(stage=>stage.StageCode==0).Entry!.ProgramIndex);
    private static bool TextureReference(DokObject camera,uint nameHash)
    {
        var reference=camera.Properties.Single(property=>property.NameHash==nameHash);
        if(reference.TypeCode!=5||reference.ElementCount!=1||reference.ElementSize!=4)
            throw new NotSupportedException("Atmosphere texture reference requires a scalar resource key.");
        return BinaryPrimitives.ReadUInt32LittleEndian(reference.RawValue)!=0;
    }
    private static float F(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]);
    private static Vector4 V(ReadOnlySpan<byte> bytes,int at)=>new(F(bytes,at),F(bytes,at+4),F(bytes,at+8),F(bytes,at+12));
    private static Vector3 Rgb(ReadOnlySpan<byte> bytes,int at)=>new(F(bytes,at),F(bytes,at+4),F(bytes,at+8));
    private static float[] Words(Vector4 value)=>[value.X,value.Y,value.Z,value.W];
    private static float[] Words(Matrix4x4 v)=>[v.M11,v.M12,v.M13,v.M14,v.M21,v.M22,v.M23,v.M24,v.M31,v.M32,v.M33,v.M34,v.M41,v.M42,v.M43,v.M44];
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}

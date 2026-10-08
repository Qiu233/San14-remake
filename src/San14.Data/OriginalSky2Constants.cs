using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalSky2VertexInputs(Vector4 Fog, Matrix4x4 CameraToLocal,
    float ProjectionFarZ, Matrix4x4 LocalToGlobal)
{
    public OriginalSkyDirectionalMode4FogReceipt? SourceMode4Fog { get; init; }
}

public sealed record OriginalSky2PixelInputs(Vector4 Fog, IReadOnlyList<Vector4> Scattering,
    Vector4 ViewPosition, Vector4 SkyInfo0, Vector4 SkyInfo1, Vector4 SunRgb, Vector4 SunDirection,
    Vector4 MoonRgb, Vector4 MoonDirection, Vector4 SunScreenPosition, Vector4 AltitudeInfo,
    Vector4 StableDimensions, Vector4 ClampValue, Matrix4x4? MoonFace = null,
    Vector4? MoonScreenPosition = null)
{
    public OriginalSky2SourceZeroMoon? SourceZeroMoon { get; init; }
    public OriginalSkyDirectionalMode4FogReceipt? SourceMode4Fog { get; init; }
}

public sealed record OriginalSky2ConstantWrite(string Name, int Offset, int ElementCount, int Stride, int ElementBytes);
public sealed record OriginalSky2ConstantPacket(byte[] Bytes, IReadOnlyList<OriginalSky2ConstantWrite> Writes);
public sealed record OriginalSky2DrawInputs(OriginalSky2VertexInputs Vertex, OriginalSky2PixelInputs Pixel);

/// <summary>Packs explicit named inputs against the verified original RDEF.
/// A complete caller-supplied template retains padding; this does not recover native globals.</summary>
public static class OriginalSky2Constants
{
    private sealed record Field(string Name, int Offset, ushort Class, ushort Rows, ushort Columns,
        ushort Elements, float[] Words);

    /// <summary>Apply prepared camera/light outputs while retaining native
    /// cache values for absent lights, absent controller and skipped Sun token.
    /// Texture presence, SkyInfo and InFront/variant selection remain explicit.</summary>
    public static OriginalSky2DrawInputs ApplyPreparedCameraAndLights(OriginalSky2DrawInputs previous,
        OriginalSky2CameraPacket camera,OriginalSky2PreparedLight? sun,OriginalSky2PreparedLight? moon,
        OriginalSky2SunScreenPosition? sunScreen)
        =>ApplyPreparedCameraAndLights(previous,camera,sun,moon,sunScreen,null);

    /// <summary>The actual nonnull Moon continuation also writes Face and
    /// Screen. A skipped Moon setter retains both previous caches, independently
    /// of texture presence and the separate Sun projection.</summary>
    public static OriginalSky2DrawInputs ApplyPreparedCameraAndLights(OriginalSky2DrawInputs previous,
        OriginalSky2CameraPacket camera,OriginalSky2PreparedLight? sun,OriginalSky2PreparedLight? moon,
        OriginalSky2SunScreenPosition? sunScreen,OriginalSky2MoonGeometryPacket? moonGeometry)
    {
        ArgumentNullException.ThrowIfNull(previous);ArgumentNullException.ThrowIfNull(camera);
        if(moon is null&&moonGeometry is not null)
            throw new InvalidDataException("Absent Moon setter cannot acquire current MoonFace/Screen writes.");
        return new(previous.Vertex with{CameraToLocal=camera.CameraToLocal,ProjectionFarZ=camera.ProjectionFarZ,
            LocalToGlobal=camera.LocalToGlobalOverride??previous.Vertex.LocalToGlobal},
            previous.Pixel with{SunRgb=sun?.Rgb??previous.Pixel.SunRgb,SunDirection=sun?.Direction??previous.Pixel.SunDirection,
                MoonRgb=moon?.Rgb??previous.Pixel.MoonRgb,MoonDirection=moon?.Direction??previous.Pixel.MoonDirection,
                SunScreenPosition=sunScreen?.Position??previous.Pixel.SunScreenPosition,
                MoonFace=moonGeometry?.MoonFace??previous.Pixel.MoonFace,
                MoonScreenPosition=moonGeometry?.Screen.Position??previous.Pixel.MoonScreenPosition});
    }

    /// <summary>Apply final shared fog to both stages, honoring the explicit
    /// mode7 neutral Scat override and the source mode4/profile1 inactive
    /// zeroFog branch. Otherwise retain the caller's existing Scat;
    /// this does not infer dirty state or unconditionally rerun the writer.</summary>
    public static OriginalSky2DrawInputs ApplySharedFog(OriginalSkyFogPacket fog,
        OriginalSky2VertexInputs vertex, OriginalSky2PixelInputs pixel)
    {
        ArgumentNullException.ThrowIfNull(fog);ArgumentNullException.ThrowIfNull(vertex);ArgumentNullException.ThrowIfNull(pixel);
        bool sourceMode4=fog.RendererFogMode==4&&fog.Profile==1&&fog.SourceFogMode==0&&fog.FogEnabled&&
            !fog.RequiresMode7NeutralScattering&&fog.ScatteringOverride is null&&fog.Value==Vector4.Zero&&
            fog.Bytes.Length==16&&fog.Bytes.All(b=>b==0);
        if(fog.SourceMode4Proof is { } sourceMode4Proof)
        { sourceMode4Proof.Validate(fog); sourceMode4=true; }
        if((fog.RendererFogMode!=7&&!sourceMode4) || fog.RequiresMode7NeutralScattering!=(fog.ScatteringOverride is not null))
            throw new InvalidDataException("Unsupported or inconsistent original shared fog packet.");
        var scattering=fog.ScatteringOverride;
        if(scattering is not null && (scattering.RendererFogMode!=7 || scattering.Values.Count!=4 || scattering.Bytes.Length!=64))
            throw new InvalidDataException("Original neutral scattering override has an unsupported layout.");
        return new(vertex with{Fog=fog.Value,SourceMode4Fog=fog.SourceMode4Proof},
            pixel with{Fog=fog.Value,Scattering=scattering?.Values??pixel.Scattering,SourceMode4Fog=fog.SourceMode4Proof});
    }

    public static OriginalSky2ConstantPacket Vertex(DxbcResourceDefinitions definitions,
        ReadOnlySpan<byte> explicitTemplate, OriginalSky2VertexInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(definitions);
        input.SourceMode4Fog?.ValidateDefinitions(definitions);input.SourceMode4Fog?.ValidateValue(input.Fog);
        if(definitions.Buffers.Count==1 && definitions.Buffers[0].ByteSize==144)
            return Pack(definitions,explicitTemplate,144,
            [new("g_mC2L",0,2,4,4,0,Words(input.CameraToLocal)),
             new("g_fProjFarZ",64,0,1,1,0,[input.ProjectionFarZ]),
             new("g_mL2G",80,2,4,4,0,Words(input.LocalToGlobal))]);
        return Pack(definitions, explicitTemplate, 160,
        [new("fog",0,1,1,4,0,Words(input.Fog)),
         new("g_mC2L",16,2,4,4,0,Words(input.CameraToLocal)),
         new("g_fProjFarZ",80,0,1,1,0,[input.ProjectionFarZ]),
         new("g_mL2G",96,2,4,4,0,Words(input.LocalToGlobal))],sourceMode4Fog:input.SourceMode4Fog);
    }

    public static OriginalSky2ConstantPacket Pixel(DxbcResourceDefinitions definitions,
        ReadOnlySpan<byte> explicitTemplate, OriginalSky2PixelInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(definitions);
        input.SourceMode4Fog?.ValidateDefinitions(definitions);input.SourceMode4Fog?.ValidateValue(input.Fog);
        if (input.SourceZeroMoon is { } proof)
        {
            proof.ValidateDefinitions(definitions);
            proof.ValidatePreparedRgb(input.MoonRgb);
        }
        Field Vector(string name,int offset,Vector4 value)=>new(name,offset,1,1,4,0,Words(value));
        if(definitions.Buffers.Count==1 && definitions.Buffers[0].ByteSize==64)
            return Pack(definitions,explicitTemplate,64,
            [Vector("g_vViewPos",0,input.ViewPosition),Vector("g_vSkyInfo1",16,input.SkyInfo1),
             Vector("g_vAltitudeInfo",32,input.AltitudeInfo),Vector("g_vClampValue",48,input.ClampValue)]);
        if(definitions.Buffers.Count==1 && definitions.Buffers[0].ByteSize==128)
            return Pack(definitions,explicitTemplate,128,
            [Vector("g_vViewPos",0,input.ViewPosition),Vector("g_vSkyInfo0",16,input.SkyInfo0),
             Vector("g_vSkyInfo1",32,input.SkyInfo1),Vector("g_vMoonRGB",48,input.MoonRgb),
             Vector("g_vMoonDir",64,input.MoonDirection),Vector("g_vAltitudeInfo",80,input.AltitudeInfo),
             Vector("g_vStableDim",96,input.StableDimensions),Vector("g_vClampValue",112,input.ClampValue)]);
        if(input.Scattering is null || input.Scattering.Count != 4)
            throw new InvalidDataException("Original Sky2 Scat requires four explicit float4 values.");
        if(definitions.Buffers.Count==1 && definitions.Buffers[0].ByteSize==336)
        {
            if(input.MoonFace is not Matrix4x4 moonFace || input.MoonScreenPosition is not Vector4 moonScreen)
                throw new InvalidDataException("Original Moon Sky2 requires an explicit MoonFace matrix and MoonScreenPosition.");
            return Pack(definitions,explicitTemplate,336,
            [Vector("fog",0,input.Fog),new("Scat",16,1,1,4,4,input.Scattering.SelectMany(Words).ToArray()),
             Vector("g_vViewPos",80,input.ViewPosition),Vector("g_vSkyInfo0",96,input.SkyInfo0),
             Vector("g_vSkyInfo1",112,input.SkyInfo1),Vector("g_vSunRGB",128,input.SunRgb),
             Vector("g_vSunDir",144,input.SunDirection),Vector("g_vMoonRGB",160,input.MoonRgb),
             Vector("g_vMoonDir",176,input.MoonDirection),new("g_mMoonFace",192,2,4,4,0,Words(moonFace)),
             Vector("g_vSunScreenPos",256,input.SunScreenPosition),Vector("g_vMoonScreenPos",272,moonScreen),
             Vector("g_vAltitudeInfo",288,input.AltitudeInfo),Vector("g_vStableDim",304,input.StableDimensions),
             Vector("g_vClampValue",320,input.ClampValue)],sourceMode4Fog:input.SourceMode4Fog);
        }
        return Pack(definitions,explicitTemplate,256,
        [Vector("fog",0,input.Fog),new("Scat",16,1,1,4,4,input.Scattering.SelectMany(Words).ToArray()),
         Vector("g_vViewPos",80,input.ViewPosition),Vector("g_vSkyInfo0",96,input.SkyInfo0),
         Vector("g_vSkyInfo1",112,input.SkyInfo1),Vector("g_vSunRGB",128,input.SunRgb),
         Vector("g_vSunDir",144,input.SunDirection),Vector("g_vMoonRGB",160,input.MoonRgb),
         Vector("g_vMoonDir",176,input.MoonDirection),Vector("g_vSunScreenPos",192,input.SunScreenPosition),
         Vector("g_vAltitudeInfo",208,input.AltitudeInfo),Vector("g_vStableDim",224,input.StableDimensions),
         Vector("g_vClampValue",240,input.ClampValue)], input.SourceZeroMoon,input.SourceMode4Fog);
    }

    private static OriginalSky2ConstantPacket Pack(DxbcResourceDefinitions definitions,
        ReadOnlySpan<byte> template,int expectedBytes,Field[] fields, OriginalSky2SourceZeroMoon? sourceZeroMoon = null,
        OriginalSkyDirectionalMode4FogReceipt? sourceMode4Fog = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if(template.Length != expectedBytes || definitions.Buffers.Count != 1)
            throw new InvalidDataException("Original Sky2 requires one source buffer and a complete explicit template.");
        var buffer=definitions.Buffers[0];
        if(buffer.Name.Text != "$Globals" || buffer.ByteSize != expectedBytes || buffer.Variables.Count != fields.Length)
            throw new InvalidDataException("Original Sky2 constant inventory differs from the verified source.");
        byte[] bytes=template.ToArray();var writes=new List<OriginalSky2ConstantWrite>();
        foreach(var field in fields)
        {
            var matches=buffer.Variables.Where(v=>v.Name.Text==field.Name).ToArray();
            if(matches.Length != 1)throw new InvalidDataException($"Missing or duplicate Sky2 constant {field.Name}.");
            var variable=matches[0];var type=definitions.Types.Single(t=>t.Offset==variable.TypeOffset);
            if(type.Class!=field.Class || type.BaseType!=3 || type.Rows!=field.Rows || type.Columns!=field.Columns ||
                type.Elements!=field.Elements || type.Members.Count!=0 || variable.StartOffset!=field.Offset ||
                variable.ByteSize!=field.Words.Length*4 ||
                field.Words.Where((value, index) => !(sourceMode4Fog is not null && field.Name == "fog") &&
                    !(sourceZeroMoon is not null && expectedBytes == 256 && field.Name == "g_vMoonRGB" && index == 3))
                    .Any(value=>!float.IsFinite(value)))
                throw new InvalidDataException($"Unsupported original Sky2 constant layout or value: {field.Name}.");
            for(int word=0;word<field.Words.Length;word++)
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(field.Offset+word*4,4),field.Words[word]);
            int elements=Math.Max(1,(int)field.Elements),stride=field.Elements==0?field.Words.Length*4:16;
            writes.Add(new(field.Name,field.Offset,elements,stride,stride));
        }
        return new(bytes,writes.AsReadOnly());
    }

    private static float[] Words(Vector4 v)=>[v.X,v.Y,v.Z,v.W];
    private static float[] Words(Matrix4x4 m)=>[m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
        m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
}

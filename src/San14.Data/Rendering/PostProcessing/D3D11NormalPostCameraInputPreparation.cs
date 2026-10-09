using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Encodes the normal post-camera projection rules and this frame's
/// selected light for the native consumer. Other post templates and controller
/// state remain with their own producers.</summary>
public static class D3D11NormalPostCameraInputPreparation
{
    public const string NativeDirectory="normal-post-camera-input";
    public const string NativeFilename="projection-profile.bin";
    public const int NativeByteLength=52;

    public static LoadedMapPostEffect3ControlledCameraSource LoadRules(
        GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.ReadControlledCameraSource(
            RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
    }

    public static byte[] Encode(LoadedMapPostEffect3ControlledCameraSource rules,
        MapCameraConstructionPacket actualCamera,MapSourceCreatedLightingPacket? actualSelectedLight,
        int actualProjectionConvention)
    {
        ArgumentNullException.ThrowIfNull(rules);ArgumentNullException.ThrowIfNull(actualCamera);
        if(rules.Native.Locale!=actualCamera.ExecutableLocale)
            throw new InvalidDataException("Normal post projection rules and Camera use different locales.");
        return Encode(rules,actualCamera.FrameNumber,actualSelectedLight?.LocalLightData,
            actualSelectedLight?.FrameNumber,actualProjectionConvention);
    }

    /// <summary>The selected LightData is the actual complete current packet,
    /// not a seed or a projection of Camera F0. Null records an absent selection.
    /// Its frame number is supplied by the current selection's owner.</summary>
    public static byte[] Encode(LoadedMapPostEffect3ControlledCameraSource rules,
        long actualCameraFrameNumber,ReadOnlyMemory<byte>? actualSelectedLightData,
        long? actualSelectedLightFrameNumber,int actualProjectionConvention)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Vector3 selectedSun=Vector3.Zero;
        if(actualSelectedLightData is{} light)
        {
            if(actualSelectedLightFrameNumber!=actualCameraFrameNumber)
                throw new InvalidDataException("Normal post projection needs the selected light from the same Camera frame.");
            if(light.Length!=0x10C0)
                throw new InvalidDataException("Normal post projection requires the complete selected LightData.");
            selectedSun=new(BinaryPrimitives.ReadSingleLittleEndian(light.Span[0xF80..]),
                BinaryPrimitives.ReadSingleLittleEndian(light.Span[0xF84..]),
                BinaryPrimitives.ReadSingleLittleEndian(light.Span[0xF88..]));
        }
        byte[] bytes=new byte[NativeByteLength];
        void F(int offset,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset),value);
        F(0,rules.ProjectionThreshold);F(4,rules.ProjectionLimit);
        F(8,rules.ProjectionEpsilon);F(12,rules.ProjectionScale);
        Vector4 absent=rules.AbsentLightProjection;
        F(16,absent.X);F(20,absent.Y);F(24,absent.Z);F(28,absent.W);
        F(32,selectedSun.X);F(36,selectedSun.Y);F(40,selectedSun.Z);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(44),actualProjectionConvention);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(48),actualSelectedLightData is null?0u:1u);
        return bytes;
    }

    public static void WriteNativeInputs(string directory,LoadedMapPostEffect3ControlledCameraSource rules,
        MapCameraConstructionPacket actualCamera,MapSourceCreatedLightingPacket? actualSelectedLight,
        int actualProjectionConvention)
        =>Write(directory,Encode(rules,actualCamera,actualSelectedLight,actualProjectionConvention));

    public static void WriteNativeInputs(string directory,LoadedMapPostEffect3ControlledCameraSource rules,
        long actualCameraFrameNumber,ReadOnlyMemory<byte>? actualSelectedLightData,
        long? actualSelectedLightFrameNumber,int actualProjectionConvention)
        =>Write(directory,Encode(rules,actualCameraFrameNumber,actualSelectedLightData,
            actualSelectedLightFrameNumber,actualProjectionConvention));

    private static void Write(string directory,byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory,NativeFilename),bytes);
    }
}

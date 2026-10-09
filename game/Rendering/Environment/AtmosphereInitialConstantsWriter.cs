using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>Adapts the actual first current season frame to complete fresh
/// atmosphere constants. It advances neither World nor renderer history.</summary>
public static class AtmosphereInitialConstantsWriter
{
    public static San14.Data.AtmosphereInitialConstants WriteFreshNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,int width,int height,float cloudElapsedSeconds,float commonClock,
        string atmosphereDirectory,string mainSkyDirectory,string mainCloudDirectory,string fogDirectory)
    {
        var target=D3D11AtmosphereInitialConstantPreparation.LoadFreshMainTarget(repository,current.Present);
        var options=new D3D11AtmosphereInitialOptions(width,height,target.PixelFormat,
            D3D11AtmosphereInitialCallerRules.FreshMainCloudClip,cloudElapsedSeconds,commonClock);
        return WriteNativeInputs(repository,current,options,atmosphereDirectory,mainSkyDirectory,mainCloudDirectory,fogDirectory);
    }

    public static San14.Data.AtmosphereInitialConstants Prepare(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11AtmosphereInitialOptions options)
    {
        // These three values are the actual D3 setter output already carried
        // by this frame's typed atmosphere transport, before Sky2 cache math.
        var rgb=current.Atmosphere.NumericBytes.Span.Slice(200,12);
        Vector3 sunRgb=new(BinaryPrimitives.ReadSingleLittleEndian(rgb),
            BinaryPrimitives.ReadSingleLittleEndian(rgb[4..]),BinaryPrimitives.ReadSingleLittleEndian(rgb[8..]));
        var inputs=new D3D11AtmosphereInitialInputs(current.Present,
            current.PreparedWords.ToDictionary(word=>word.Offset,word=>word.Bits),
            current.Lights.Select(light=>new D3D11AtmosphereInitialLight(light.GroupMask,light.CompleteData)).ToArray(),
            current.Atmosphere.Controller,current.Atmosphere.Sun,current.Atmosphere.Moon,sunRgb,current.Atmosphere.MoonRgb,
            current.Atmosphere.ControllerGeometry40,options);
        return D3D11AtmosphereInitialConstantPreparation.Prepare(repository,inputs);
    }

    public static San14.Data.AtmosphereInitialConstants WriteNativeInputs(GameResourceRepository repository,
        D3D11CurrentSeasonFrame current,D3D11AtmosphereInitialOptions options,
        string atmosphereDirectory,string mainSkyDirectory,string mainCloudDirectory,string fogDirectory)
    {
        var constants=Prepare(repository,current,options);
        D3D11AtmosphereInitialConstantPreparation.WriteNativeInputs(constants,
            atmosphereDirectory,mainSkyDirectory,mainCloudDirectory,fogDirectory);
        return constants;
    }
}

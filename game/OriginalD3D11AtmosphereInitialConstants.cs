using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>Adapts the actual first current season frame to complete fresh
/// atmosphere constants. It advances neither World nor renderer history.</summary>
public static class OriginalD3D11AtmosphereInitialConstants
{
    public static San14.Data.OriginalD3D11AtmosphereInitialConstants WriteFreshNativeInputs(OriginalGameRepository repository,
        OriginalD3D11CurrentSeasonFrame current,int width,int height,float cloudElapsedSeconds,float commonClock,
        string atmosphereDirectory,string mainSkyDirectory,string mainCloudDirectory,string fogDirectory)
    {
        var target=OriginalD3D11AtmosphereInitialConstantPreparation.LoadFreshMainTarget(repository,current.Present);
        var options=new OriginalD3D11AtmosphereInitialOptions(width,height,target.PixelFormat,
            OriginalD3D11AtmosphereInitialCallerRules.FreshMainCloudClip,cloudElapsedSeconds,commonClock);
        return WriteNativeInputs(repository,current,options,atmosphereDirectory,mainSkyDirectory,mainCloudDirectory,fogDirectory);
    }

    public static San14.Data.OriginalD3D11AtmosphereInitialConstants Prepare(OriginalGameRepository repository,
        OriginalD3D11CurrentSeasonFrame current,OriginalD3D11AtmosphereInitialOptions options)
    {
        // These three values are the actual D3 setter output already carried
        // by this frame's typed atmosphere transport, before Sky2 cache math.
        var rgb=current.Atmosphere.NumericBytes.Span.Slice(200,12);
        Vector3 sunRgb=new(BinaryPrimitives.ReadSingleLittleEndian(rgb),
            BinaryPrimitives.ReadSingleLittleEndian(rgb[4..]),BinaryPrimitives.ReadSingleLittleEndian(rgb[8..]));
        var inputs=new OriginalD3D11AtmosphereInitialInputs(current.Present,
            current.PreparedWords.ToDictionary(word=>word.Offset,word=>word.Bits),
            current.Lights.Select(light=>new OriginalD3D11AtmosphereInitialLight(light.GroupMask,light.CompleteData)).ToArray(),
            current.Atmosphere.Controller,current.Atmosphere.Sun,current.Atmosphere.Moon,sunRgb,current.Atmosphere.MoonRgb,
            current.Atmosphere.ControllerGeometry40,options);
        return OriginalD3D11AtmosphereInitialConstantPreparation.Prepare(repository,inputs);
    }

    public static San14.Data.OriginalD3D11AtmosphereInitialConstants WriteNativeInputs(OriginalGameRepository repository,
        OriginalD3D11CurrentSeasonFrame current,OriginalD3D11AtmosphereInitialOptions options,
        string atmosphereDirectory,string mainSkyDirectory,string mainCloudDirectory,string fogDirectory)
    {
        var constants=Prepare(repository,current,options);
        OriginalD3D11AtmosphereInitialConstantPreparation.WriteNativeInputs(constants,
            atmosphereDirectory,mainSkyDirectory,mainCloudDirectory,fogDirectory);
        return constants;
    }
}

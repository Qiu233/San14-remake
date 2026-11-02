using San14.Data;

/// <summary>Camera declarations imported at load, rather than when a zoom
/// mode first reaches them. Live frames borrow only their selected settings.</summary>
internal sealed class D3D11CurrentSeasonCameraSettings
{
    internal D3D11CurrentSeasonCameraSettings(MapSeasonCameraPackage package)
    {
        Cloud=SkyCloudShaderSettings.Import(package);Pattern=PatternCameraSettingsImport.Load(package);
        Rain=RainDropCameraSettings.Import(package);View=CameraViewSettingsImport.Load(package);
        Waves=OceanWaveSettingsImport.Load(package);Surface=OceanSurfaceSettingsImport.Load(package);
        Optics=OceanOpticsSettingsImport.Load(package);Texture=OceanTextureMaterialSettingsImport.Load(package);
        Bubble=OceanBubbleMaterialSettingsImport.Load(package);Stars=SkyStarSettings.Import(package);
        Fog=HeightFogResourceImport.Load(package);
    }
    internal SkyCloudShaderSettings Cloud {get;}
    internal PatternCameraSettings Pattern {get;}
    internal RainDropCameraSettings Rain {get;}
    internal CameraViewSettings View {get;}
    internal OceanWaveSettings Waves {get;}
    internal OceanSurfaceSettings Surface {get;}
    internal OceanOpticsSettings Optics {get;}
    internal OceanTextureMaterialSettings Texture {get;}
    internal OceanBubbleMaterialSettings Bubble {get;}
    internal SkyStarSettings Stars {get;}
    internal HeightFogSettings Fog {get;}
}

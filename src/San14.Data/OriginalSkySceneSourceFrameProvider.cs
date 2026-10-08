namespace San14.Data;

public sealed record OriginalSkySceneSourceFramePacket(long FrameNumber,
    OriginalMapLightingBinding Binding, string SourceProvenance,
    OriginalSkySceneSourceFrame SourceFrame, bool UsesSourceConstructorDefaults);

/// <summary>Source-created Camera/HDRI lighting and original cube-camera
/// construction feed one coherent sky frame. Ordinary row ownership, prepared
/// Sky2 controller, atmosphere and applied textures remain explicit. Unknown
/// current edits never silently select constructor parameter values.</summary>
public static class OriginalSkySceneSourceFrameProvider
{
    /// <summary>An actually selected seasonal package and selected View Camera
    /// have their own current owner, separate from the HDRI setter Camera3/4.
    /// Current Sun/Moon inputs must come from their explicitly supplied producer;
    /// no HDRI ordinary-row or declared-table absence is inferred here.</summary>
    public static OriginalSkySceneSourceFramePacket PrepareAppliedSeason(
        OriginalMapSeasonCameraSelection appliedSelection,OriginalSkySceneCurrentCameraParameters currentCamera,
        OriginalMapSourceCreatedLightingPacket acceptedLighting,OriginalCubeRenderTextureDefinition target,
        OriginalSkySceneLights currentCameraLights,OriginalSkySceneSharedAtmosphere atmosphere,
        OriginalSkyScenePreparedController preparedController,int nativeClipProfile)
    {
        ArgumentNullException.ThrowIfNull(appliedSelection);ArgumentNullException.ThrowIfNull(currentCamera);
        ArgumentNullException.ThrowIfNull(acceptedLighting);ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(currentCameraLights);ArgumentNullException.ThrowIfNull(atmosphere);
        ArgumentNullException.ThrowIfNull(preparedController);appliedSelection.Validate();
        if(currentCamera.FrameNumber!=acceptedLighting.FrameNumber||currentCamera.Parameters.NativeTargetFormat!=target.PixelFormat)
            throw new InvalidDataException("Applied season Sky and accepted HDRI lighting require one explicit frame and target format.");
        foreach(var package in new[]{appliedSelection.FirstPackage,appliedSelection.SecondPackage}.Where(p=>p is not null))
            _=OriginalSkySeasonPackageTextures.Read(package!);
        var cameras=OriginalSkySceneSourceDefaults.PrepareCubeCameras(currentCamera.PreparedCameraPrefix.Span,
            currentCamera.CameraOwnerProvenance,nativeClipProfile,target.Width,target.Width);
        var parameters=currentCamera.Parameters with{MilkyStrength1484=preparedController.MilkyStrength1484};
        // D7CF68 attaches the same copied Sun/Moon global records before
        // 11775C0 updates their positions. RGB caches retain the earlier setters.
        var lights=currentCameraLights with{
            RawSunPosition=preparedController.SunPosition??currentCameraLights.RawSunPosition,
            RawMoonPosition=preparedController.MoonPosition??currentCameraLights.RawMoonPosition,
            SourceProvenance=currentCameraLights.SourceProvenance+";D7CF68-attached-global-records;11775C0-position-update-after-RGB-setters"};
        var copiedAtmosphere=atmosphere with{Scattering=Array.AsReadOnly(atmosphere.Scattering.ToArray())};
        string evidence=acceptedLighting.SourceProvenance+";separate-selected-View-Camera="+currentCamera.CameraOwnerProvenance+
            ";applied-source-season="+appliedSelection.SourceEvidence+";actual-Earth622DC967;fresh-shader-retained-null-Moon-Milky";
        return new(acceptedLighting.FrameNumber,acceptedLighting.Binding,evidence,
            new(cameras,parameters,preparedController.Controller,lights,new(true,false,false),copiedAtmosphere),false);
    }

    public static OriginalSkySceneSourceFramePacket PrepareFresh(
        OriginalSkySceneCameraSourceProfile sourceCamera,
        OriginalMapSourceCreatedLightingPacket lighting,
        OriginalCubeRenderTextureDefinition sourceTarget,
        OriginalSkySceneLights retainedLightCaches, OriginalSkySceneLightRows selectedLightRows,
        OriginalSkySceneSharedAtmosphere atmosphere, OriginalSkySceneTexturePresence appliedTextures,
        OriginalSkySceneSky2Controller? preparedSky2Controller, int nativeClipProfile)
    {
        ArgumentNullException.ThrowIfNull(sourceTarget);
        return Prepare(sourceCamera,lighting,sourceTarget,retainedLightCaches,selectedLightRows,atmosphere,
            appliedTextures,preparedSky2Controller,nativeClipProfile,
            OriginalSkySceneSourceDefaults.CreateSky2Parameters(sourceTarget.PixelFormat),true);
    }

    /// <summary>The original controller's prepared Milky strength and matrix
    /// packet are consumed together. Sun/Moon CPU position writes still require
    /// their explicit source light owner equality before ordinary row selection.</summary>
    public static OriginalSkySceneSourceFramePacket PrepareFreshWithController(
        OriginalSkySceneCameraSourceProfile sourceCamera,
        OriginalMapSourceCreatedLightingPacket lighting,
        OriginalCubeRenderTextureDefinition sourceTarget,
        OriginalSkySceneLights retainedLightCaches, OriginalSkySceneLightRows selectedLightRows,
        OriginalSkySceneSharedAtmosphere atmosphere, OriginalSkySceneTexturePresence appliedTextures,
        OriginalSkyScenePreparedController preparedController, int nativeClipProfile)
    {
        ArgumentNullException.ThrowIfNull(sourceTarget);ArgumentNullException.ThrowIfNull(preparedController);
        var parameters=OriginalSkySceneSourceDefaults.CreateSky2Parameters(sourceTarget.PixelFormat)
            with{MilkyStrength1484=preparedController.MilkyStrength1484};
        return Prepare(sourceCamera,lighting,sourceTarget,retainedLightCaches,selectedLightRows,atmosphere,
            appliedTextures,preparedController.Controller,nativeClipProfile,parameters,true);
    }

    /// <summary>Current or edited instances require their complete resolved
    /// Sky2 parameter state; absence is not filled from source constructors.</summary>
    public static OriginalSkySceneSourceFramePacket PrepareResolved(
        OriginalSkySceneCameraSourceProfile sourceCamera,
        OriginalMapSourceCreatedLightingPacket lighting,
        OriginalCubeRenderTextureDefinition sourceTarget,
        OriginalSkySceneLights retainedLightCaches, OriginalSkySceneLightRows selectedLightRows,
        OriginalSkySceneSharedAtmosphere atmosphere, OriginalSkySceneTexturePresence appliedTextures,
        OriginalSkySceneSky2Controller? preparedSky2Controller, int nativeClipProfile,
        OriginalSkySceneSky2Parameters completeResolvedParameters)
        => Prepare(sourceCamera,lighting,sourceTarget,retainedLightCaches,selectedLightRows,atmosphere,
            appliedTextures,preparedSky2Controller,nativeClipProfile,completeResolvedParameters,false);

    private static OriginalSkySceneSourceFramePacket Prepare(
        OriginalSkySceneCameraSourceProfile camera, OriginalMapSourceCreatedLightingPacket lighting,
        OriginalCubeRenderTextureDefinition target, OriginalSkySceneLights retained,
        OriginalSkySceneLightRows rows, OriginalSkySceneSharedAtmosphere atmosphere,
        OriginalSkySceneTexturePresence textures, OriginalSkySceneSky2Controller? controller,
        int nativeClipProfile, OriginalSkySceneSky2Parameters parameters, bool fresh)
    {
        ArgumentNullException.ThrowIfNull(camera); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(retained);
        ArgumentNullException.ThrowIfNull(rows); ArgumentNullException.ThrowIfNull(atmosphere);
        ArgumentNullException.ThrowIfNull(textures); ArgumentNullException.ThrowIfNull(parameters);
        var identity=lighting.SourceIdentity;
        if(camera.ResourceUri!=identity.CameraResourceUri || camera.SourceSha256!=identity.CameraSha256 ||
            camera.ObjectId!=identity.CameraObjectId || string.IsNullOrWhiteSpace(parameters.SourceProvenance) ||
            parameters.NativeTargetFormat!=target.PixelFormat)
            throw new InvalidDataException("Sky source frame requires the same source-created Camera and exact native target format.");
        if(fresh && camera.References.Sky2Controller!=0 && controller is null)
            throw new InvalidDataException("The fresh source declares a Sky2 controller; prepare its original frame fields before sky input preparation.");
        var cameras=OriginalSkySceneSourceDefaults.PrepareCubeCameras(lighting.PreparedCameraPrefix.Span,
            lighting.SourceProvenance,nativeClipProfile,target.Width,target.Width);
        var lights=OriginalSkySceneLightData.ApplyOrdinaryRows(retained,lighting.LocalLightData.Span,rows);
        var copiedAtmosphere=atmosphere with{Scattering=Array.AsReadOnly(atmosphere.Scattering.ToArray())};
        string provenance=lighting.SourceProvenance+$";SkyTarget={target.Source.Source.ResourceUri}@{target.Source.Source.DecodedSha256}#{target.Object.ObjectId:X8};"+
            $"SkyParameters={parameters.SourceProvenance};SkyOrdinaryRows={rows.SourceProvenance}";
        return new(lighting.FrameNumber,lighting.Binding,provenance,
            new(cameras,parameters,controller,lights,textures,copiedAtmosphere),fresh);
    }
}

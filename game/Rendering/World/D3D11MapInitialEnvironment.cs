using San14.Data;

/// <summary>CPU-only first-frame banks. The caller owns current/camera snapshots
/// and exclusively lends the unpublished CPU resource package for this work.</summary>
internal sealed record D3D11MapInitialEnvironment(
    SkyAtmosphereResourceOwner AtmosphereOwner, SkyMainResourceOwner SkyResources,
    D3D11SkyAtmosphereResources AtmosphereResources, SkyStarResources? StarResources,
    D3D11AmbientRenderResources AmbientResources, D3D11CloudProgramMedia CloudMedia,
    D3D11CloudRenderResources CloudResources, ShadowReceiverPatternResources ReceiverPatterns,
    D3D11NormalPostInitialConstants Post)
{
    internal static D3D11MapInitialEnvironment Prepare(D3D11MapCpuResources cpu,
        D3D11CurrentSeasonFrame current, MainMapCameraFrame currentMain,
        SceneViewSourceCameraInputs cameraInputs, MapStaticModelGroundHeightSource currentField,
        CancellationToken token)
    {
        var repository=cpu.Repository;var inputs=cpu.Inputs;var source=cpu.Source;
        var scene=cpu.Scene;var sceneMedia=cpu.SceneMedia;var groundDetails=cpu.GroundDetails;
        var fogAcceleration=cpu.FogAcceleration;var cloudPrograms=cpu.CloudPrograms;
        var skyPrograms=cpu.SkyPrograms;var fogPrograms=cpu.FogPrograms;
        var initialContext=cpu.InitialContext;var hdr=cpu.Hdr;
        SkyMainResourceOwner? skyResources=null;SkyAtmosphereResourceOwner? atmosphereOwner=null;
        D3D11SkyAtmosphereResources? atmosphereResources=null;SkyStarResources? starResources=null;
        D3D11AmbientRenderResources? ambientResources=null;D3D11CloudProgramMedia? cloudMedia=null;
        D3D11CloudRenderResources? cloudResources=null;ShadowReceiverPatternResources? receiverPatterns=null;
        token.ThrowIfCancellationRequested();
        try
        {
            // Fixed media was published before Title admission. Only this
            // opening's current resources remain in the opening directory.
            cpu.EnvironmentStatic?.WriteOpeningReference(inputs);
            groundDetails.WriteNativeInputs(inputs, current.Rgb.Inputs.MainPostSelection!.AppFlags);
            token.ThrowIfCancellationRequested();
            var globalFog=current.Lights.First(light=>(light.GroupMask&0x80)!=0).State;
            var fogRgb=globalFog.Lighting.Directional[0].Color;
            var initialFog=fogAcceleration.PrepareInitialFrame(globalFog.Fog,
                new System.Numerics.Vector3(fogRgb.X,fogRgb.Y,fogRgb.Z),current.Present.CapturePreparedCamera());
            fogAcceleration.WriteNativeInputs(initialFog,Path.Combine(inputs,"fog-accel-input"));
            token.ThrowIfCancellationRequested();
            cloudMedia=D3D11CloudProgramMedia.Prepare(cloudPrograms,current.Present,
                current.Lights.First(light=>(light.GroupMask&0x80)!=0).State);
            cloudMedia.WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            if (cpu.EnvironmentStatic is null)
                D3D11CloudPatternProgramMedia.Prepare(cloudMedia.Resources).WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            ambientResources = D3D11AmbientRenderResources.Load(repository,current.Present,cpu.SeasonResources.Source.Locale);
            ambientResources.WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            var images = SkyAppliedCloudTextureSet.Import(current.Present.AcceptedSeasonCamera.Inputs.Source.CloudTextures);
            atmosphereResources = D3D11SkyAtmosphereResources.Load(
                current.Present.AcceptedSeasonCamera.Inputs.Source, images);
            atmosphereResources.WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            if (cpu.EnvironmentStatic is null)
                D3D11PatternRenderResources.Load(repository,ambientResources.Declaration,cpu.SeasonResources.Source.Locale)
                    .WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            skyResources = SkyMainResourceOwner.CreateFresh(repository, current.Present, skyPrograms, images);
            atmosphereOwner = new(atmosphereResources, images, skyResources, null);
            cloudResources = D3D11CloudRenderResources.Prepare(repository, current.Present,
                cpu.Width, cpu.Height, images,
                skyResources.Sampling, cloudMedia);
            cloudResources.WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            var starPackage = current.Present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
            starResources = SkyStarResources.Load(repository, sceneMedia.Renderer, starPackage);
            string starDirectory = Path.Combine(inputs, "main-sky-input", "stars");
            Directory.CreateDirectory(starDirectory);
            File.WriteAllBytes(Path.Combine(starDirectory, "program-presence.bin"),
                BitConverter.GetBytes(starResources is not null ? 1u : 0u));
            if (starResources is not null)
                D3D11MainStarInputPreparation.Load(starResources).WriteNativeInputs(starDirectory);
            token.ThrowIfCancellationRequested();
            SkyStarCurrentEncoding.WriteNativeInputs(starDirectory, current.Stars,
                current.Atmosphere.Controller.MatrixF0, current.Atmosphere.Controller.Controller.Scalar38);
            var post = current.Rgb.Inputs.MainPostSelection!;
            var fieldModels = sceneMedia.FieldModels;
            D3D11OwnCameraInputPreparation.WriteFreshNativeInputs(repository, scene,
                new(currentMain, cameraInputs.CurrentMaximumYOverride,
                    cameraInputs.NativeClipProfile, cameraInputs.TangentDispatch),
                new(post.QualityOption18, post.MainMode288, post.AppFlags,
                    post.PriorCloudy430Bits, post.SelectedCloudy430Bits),
                currentField, inputs, fieldModels, sceneMedia.Terrain, preparedPostSelection:post);
            var initialMainNumeric = D3D11MainCameraInitialNumericInputs.Create(repository, currentMain);
            initialMainNumeric.WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            if (cpu.EnvironmentStatic is null)
                ShadowReceiverResources.Load(repository).WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            if (cpu.EnvironmentStatic is null)
                ShadowReceiverSamplerEncoding.WriteNativeInputs(repository, cpu.Locale, inputs);
            token.ThrowIfCancellationRequested();
            receiverPatterns = ShadowReceiverPatternResources.Load(repository, starPackage, cpu.Locale,
                cpu.SeasonResources.PatternTextures);
            receiverPatterns.WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            D3D11ShadowInputs.WriteNativeInputs(repository, fieldModels, source,
                cpu.PrimaryTemplates, cpu.SeasonResources, current);
            D3D11OwnedNumericInputPreparation.WriteFreshNativeInputs(repository, scene,
                currentMain, current, cpu.SeasonResources, cpu.SeasonLibrary,
                fieldModels,initialContext.ContextFlags3E4,inputs,
                cpu.Width, cpu.Height);
            var sceneColor=SceneColorTargetResources.PrepareFresh(repository,
                current.Present.CapturePreparedCamera(),cpu.SeasonResources.InitialCommonSettings,
                cpu.Width,cpu.Height);
            ScreenMapResources.PrepareNormal(repository,
                current.Present.CapturePreparedCamera(),cpu.Locale).WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            if (cpu.EnvironmentStatic is null)
                ContactOcclusionResources.Load(repository,cpu.Locale).WriteNativeInputs(inputs);
            token.ThrowIfCancellationRequested();
            D3D11HdrResourceBindings.WriteProducerTargets(repository,
                Path.Combine(inputs, "hdr-bank"),
                cpu.Width, cpu.Height);
            D3D11RefractionCopyInputPreparation.WriteNativeInputs(repository, current,sceneColor,
                Path.Combine(inputs, "refraction-source-input"),
                cpu.Width, cpu.Height);
            D3D11RefractionFilterInputPreparation.WriteNativeInputs(repository, current,sceneColor,
                Path.Combine(inputs, "refraction-source-input", "filter137"),
                cpu.Width, cpu.Height);
            D3D11HdrProgramMediaPreparation.WriteNativeInputs(repository,
                current, cpu.SeasonResources, hdr, sceneColor, inputs,
                cpu.Width, cpu.Height);
            var atmosphere = AtmosphereInitialConstantsWriter.WriteFreshNativeInputs(repository, current,
                sceneColor,
                cloudElapsedSeconds: 0, commonClock: 0,
                Path.Combine(inputs, "atmosphere-camera-continuous-input", "initial"),
                Path.Combine(inputs, "main-sky-input"), Path.Combine(inputs, "main-cloud-input"),
                Path.Combine(inputs, "ssfog-population-input"),cloudMedia,cloudResources,
                skyPrograms,skyResources.Sampling,fogPrograms);
            if (atmosphere.Sky is { } sky)
                skyResources.Prepare(sky.Program).WriteNativeInputs(Path.Combine(inputs, "main-sky-input"));
            token.ThrowIfCancellationRequested();
            if (current.Present.AcceptedSeasonCamera.Inputs.Source.RenderingAtmosphere is not null)
            {
                var workSky = atmosphereOwner.PrepareWorkSky(repository, current.Present, skyPrograms);
                var work = D3D11AmbientWorkInitialConstants.Prepare(repository,
                    current, cloudMedia, ambientResources.Declaration,
                    skyPrograms, workSky.Sampling);
                workSky.Prepare(work.SkyMedia.Program);
                D3D11AmbientWorkInputPreparation.WriteNativeInputs(work, inputs);
            token.ThrowIfCancellationRequested();
            }
            var normalPost=D3D11NormalPostInitialConstantPreparation.WriteNativeInputs(repository,
                current, inputs, cpu.Width, cpu.Height,sceneColor);
            D3D11PrimaryForwardInputs.WriteFreshNativeInputs(repository,
                cpu.PrimaryTemplates, cpu.SeasonResources, current, inputs,
                cpu.Width, cpu.Height,staticCpu:cpu.PrimaryGpuMedia?.RequireForward(cpu.Locale));
            token.ThrowIfCancellationRequested();
            return new(atmosphereOwner!,skyResources!,atmosphereResources!,starResources,
                ambientResources!,cloudMedia!,cloudResources!,receiverPatterns!,normalPost);
        }
        catch { if(atmosphereOwner is not null)atmosphereOwner.Dispose();else skyResources?.Dispose();throw; }
    }
}

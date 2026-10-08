using Godot;
using San14.Data;

/// <summary>Compiled SceneView source assembly for the bounded TC zero-Army
/// opening. Main activation requires a complete accepted Host output.
/// No caller can supply a success boolean,
/// empty GPU callback, fabricated atmosphere or guessed terrain constants.</summary>
public static class OriginalSceneViewNativeMapSourceFactory
{
    public const string RetainedEvidenceSha256 =
        "78B7367EF40035B4E9DAA510E41F7C47779721D7D58824B92E353A1F4F56E875";

    /// <summary>The evidence must be an explicitly supplied saved complete V3
    /// report. It is copied into a NEW owned allocation; native process/Main
    /// identity, startup defaults and cross-restart retention are not claimed.
    /// The factory does not resolve any path embedded in that report.</summary>
    public static OriginalSceneViewNativeMapDependencyFactory CreateFromSavedRetainedObservation(
        ReadOnlyMemory<byte> completeSavedV3Report, string evidenceUri)
    {
        var observation = OriginalNamedHdriRetainedObservation.ReadSavedV3(
            completeSavedV3Report.Span, evidenceUri, RetainedEvidenceSha256);
        return (loaded, capture) => BuildAsync(loaded, capture, observation);
    }

    private static async Task<OriginalSceneViewNativeMapDependencies> BuildAsync(
        OriginalSceneViewNativeMapLoadInputs loaded,
        Func<OriginalNativeMapFrameContext, OriginalSceneViewNativeMapFrameCapture> capture,
        OriginalNamedHdriRetainedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(loaded); ArgumentNullException.ThrowIfNull(capture);
        var repo = loaded.Repository;
        string locale = loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        if (locale != observation.Locale || locale != "TC" || loaded.InitialFrame.World.Units.Count != 0)
            throw new NotSupportedException("This first factory requires its actual TC opening evidence and actual zero-Army world; other populations/locales need their source owners.");
        string generation = "owned-SceneView-native:" + loaded.InitialFrame.SceneGeneration + ":" + Guid.NewGuid().ToString("N");
        var graph = repo.LoadRenderGraph("system", 0x024496EE);
        var shaders = repo.LoadObjects("system", 0x718CCB8B);
        var textures = repo.LoadObjects("system", 0x93FB3280);
        var plan = OriginalDeferredTexturePlan.Read(graph, 166, shaders, textures,
            repo.LoadShaderContainer("system", 0x9D8E811F), 0);
        var hostSource = OriginalMainMapContextOwnerSource.LoadFreshHostDefaults(repo, locale);
        var retained = observation.CaptureOwnedTemplate(OriginalMainLightBindingSource.LoadGameDefault(repo),
            generation + "/new-owned-retained-L", 1, hostSource.ContextFlags3E4);
        var binding = OriginalMapSourceCreatedNativeSession.CreateExpectedDefault1022Binding(plan,
            generation + "/View", generation + "/Main", generation + "/class66-L");
        var cpu = OriginalSceneViewNativeMapCpuSourceOwner.CreateFresh(loaded, binding.ViewIdentity,
            generation + "/static-source-generation");
        var model = new OriginalSceneViewNativeMapModelSourceOwner(repo, graph, shaders, hostSource);
        var mode = OriginalMainMapCameraModeOwner.CreateFresh(repo, locale, binding.CameraOwnerIdentity);

        // Source cliff, separate NearFar, ordered targets, native resize and
        // D/S-only clear are validated before GPU state. Raster request is a
        // bootstrap CPU hint; actual same-frame receipts replace it later.
        var terrain = OriginalSceneViewNativeMapTerrainSourceOwner.ReadSource(loaded, graph, textures,
            hostSource, generation + "/Host");
        var bootCapture = loaded.InitialFrame with { MainCamera = loaded.InitialFrame.MainCamera with {
            Camera = loaded.InitialFrame.MainCamera.Camera.CopyAcceptedSnapshotForOwnedFrame(1, generation + "/bootstrap-camera") } };
        var bootPopulation = cpu.PrepareCurrent(bootCapture);
        var bootModels = new OriginalMapStaticModelAssembler(cpu.SourceRevision, model.LoadDraw)
            .Prepare(bootPopulation.Visibility, model.PrepareRuntime);
        if (!bootModels.Complete) throw new InvalidDataException("Complete actual bootstrap static source draws are unavailable.");
        terrain.InitializeUnconsumedRequestHint(bootModels);
        var sceneSource = terrain.CreateSceneSource(bootCapture);
        var materials = OriginalMapMaterialCacheProvider.PrepareSourceNativeMap(repo, plan,
            generation + "/source-material-cache", bootModels.Packets, terrain.PrepareMaterialConsumers(bootCapture));

        Vkd3dShaderCompiler? compiler = null;
        OriginalMapSourceCreatedLightingDeviceOwner? lighting = null;
        OriginalNativeMapModelPopulationRenderer? population = null;
        OriginalNativeMapArmyPopulationRenderer? army = null;
        OriginalSceneViewNativeMapSkySourceOwner? sky = null;
        OriginalSceneViewNativeMapStockBackendSourceOwner? stockBackend = null;
        try
        {
            compiler = OriginalShaderCompilerRuntime.Open();
            lighting = await OriginalMapSourceCreatedLightingDeviceOwner.CreateFreshHostGameDefaultAsync(
                repo, 3, retained, locale, allowReadback: false);
            if (lighting.RetainedNormalization is not null)
                throw new InvalidDataException("Copied complete retained owner must not be replaced by a normalized backend seed.");
            var current = new OriginalSceneViewNativeMapF103SourceOwner(loaded, cpu, lighting,
                plan, binding, generation, mode);
            var staticStencil = OriginalNativeStaticStencilCurrentOwner.CreateFreshSourceClass18NoExternalStencilAssignments();
            var terrainStencil = OriginalNativeTerrainStencilCurrentOwner.CreateFreshFieldSceneNoExternalAssignments(loaded.TerrainMaterial);
            population = OriginalNativeMapModelPopulationRenderer.CreateForCurrentOwnedSource(compiler,
                cpu.SourceRevision, materials.Cache, model.LoadDraw, current.RequirePopulation,
                model.PrepareRuntime, new(512L << 20, 256L << 20, 512, 4096), model.CommitAccepted);
            population.NativeStencilPreparation = (ctx, prepared) =>
                staticStencil.PrepareCurrent(ctx, current.RequireAdmission(ctx), prepared);
            var metadata = repo.EffectiveScenarios.Single(s =>
                GameResourceId.File(s.SourcePath).ToString() == loaded.InitialFrame.World.ScenarioKey && s.Locale == locale);
            army = OriginalNativeMapArmyPopulationRenderer.CreateForPlayableOwnedSource(compiler, repo,
                generation + "/Army", repo.LoadScenario(metadata), repo.LoadCeMap(), loaded.Projection,
                loaded.Terrain.HeightField,
                ctx => new(capture(ctx).World, new(0, OriginalArmyAnimationClockPolicy.RepeatDeclaredDuration,
                    generation + "/explicit-initial-zero-Army-no-animation-step")));

            // D3A850 selected full L+150 -> package+40; exact source row0;
            // actual97C1F0 and pending121 mode4/7 dispatch; then real Sky WORK.
            // This owner borrows registration and uses the exact current final
            // Present-season prefix. It cannot accept Main72 as Fog96.
            sky = await OriginalSceneViewNativeMapSkySourceOwner.BuildAsync(loaded, compiler,
                lighting, current.Bootstrap, current.RequireBootstrapPresentSeason(), current.RequireBootstrapSeasonRgb(),
                current.RequireBootstrapNode51Execution());
            var family = OriginalDeferredSourceViewFamilyOwner.CreateFresh(
                OriginalDeferredSamplerSources.Load(repo.SourceDirectory, locale),
                loaded.InitialFrame.MainCamera.Camera.Source, binding, generation,
                OriginalDeferredSourceViewFamilyScope.FreshSourceMainNoExternalFamilyAssignments);
            var filters = OriginalMapCommonViewFilterSource.LoadGameDefault(repo);
            var prefixSource = OriginalDeferredAuxiliaryPrefixSource.Read(graph,
                repo.LoadObjects("system", 0x908AB6E2), generation);
            var prefixOwner = prefixSource.CreateFreshQueryOwner(current.Bootstrap.Camera,
                current.Bootstrap.ViewFields, generation, generation + "/source-RLR-construction");
            var actualArmy = army;
            var actualSky = sky;
            var actualPopulation = population;
            var sources = OriginalMapSourceCreatedPlayableCurrentSources.FromSourceNullGeometryPixelFactory(
                (ctx, preparedArmy, actualBinding) =>
                {
                    var currentCapture = capture(ctx);
                    return current.PrepareCurrent(ctx, currentCapture, preparedArmy,
                        actualBinding, currentCapture.PresentationDeltaSeconds);
                },
                current.PrepareEarly, army, family,
                (frame, acceptedStatic, completedEarly) =>
                {
                    var q = current.PrepareAuxiliaryContext(frame);
                    var filter = filters.PrepareAppliedCurrent(frame.Context.FrameNumber,
                        generation + "/source-Common-settings", binding.ViewIdentity);
                    var aux = OriginalMapSourceCreatedAuxiliaryCurrentFactory.Prepare(frame, filter, q,
                        filter.CommonSettingsOwnerIdentity);
                    var prefix = prefixSource.Prepare(frame.PreparedSourceFrame!, q, prefixOwner);
                    var backend = (stockBackend ?? throw new InvalidDataException("Actual current stock source owner is unavailable."))
                        .PrepareCurrent(frame, actualSky.RequireCompletedSkyGraphicsPrefix(frame.Context), acceptedStatic,
                            actualArmy.LastAcceptedFrame ?? throw new InvalidDataException("Actual current zero-Army submissions are unavailable."),
                            terrain.RequirePreparedColorMask(frame.Context), completedEarly);
                    var pixels = new OriginalDeferredAuxiliarySourcePixelInputs(aux, prefix,
                        backend.Color, backend.Depth,
                        OriginalNativeCameraTangentDispatch.Sse);
                    return Task.FromResult(new OriginalMapSourceCreatedPlayableAuxiliarySourceFrameInputs(
                        pixels, terrain.PrepareMaterialConsumers(capture(frame.Context)), terrain.PrepareDeferredRaster(frame)));
                },
                frame => OriginalMapFinalOutputProductionFrameInputs.FromSourceSession(frame, frame.Context,
                    OriginalMapSeasonRuntime.ResolveSourceFrame(repo, capture(frame.Context).World.NativeMapSeason!,
                        frame.Context.FrameNumber).Calendar, OriginalNativeCameraTangentDispatch.Sse,
                    current.RequireSeasonRgb(frame.Context))) with
            {
                CreateGraphicsColorMaskOwner = actualFinal =>
                {
                    var history = OriginalMapSourceCreatedGraphicsColorMaskOwner.ReadFresh(repo, locale,
                        generation + "/actual-source-frontend", binding.ViewIdentity, actualFinal);
                    stockBackend = OriginalSceneViewNativeMapStockBackendSourceOwner.ReadFresh(history.OwnedSource);
                    history.BindCurrentStockHistory(stockBackend.OwnedSource, stockBackend.RequirePreparedPacket);
                    actualSky.BindActualGraphicsColorMaskOwner(history);
                    return history;
                },
                AfterCurrentPrepared = async frame =>
                {
                    var armyFrame = await actualArmy.CaptureBeforeFrameAsync(frame.Context);
                    var armyPublication = await actualArmy.PrepareCurrentPrimitivePublicationAsync(armyFrame,
                        frame.Lighting, frame.PreparedSourceFrame!.Common);
                    await actualArmy.BindCurrentPrimitiveAdmissionAsync(armyFrame, armyPublication,
                        current.RequireAdmission(frame.Context));
                    await actualSky.SubmitCurrentAsync(frame, current.RequirePresentSeason(frame.Context),
                        current.RequireSeasonRgb(frame.Context), current.RequireNode51Execution(frame.Context),
                        capture(frame.Context).PresentationDeltaSeconds);
                },
                PrepareTerrainNativeStencil = (frame, unconsumedRasterHint) =>
                {
                    var submittedModels = actualPopulation.LastAcceptedFrame ??
                        throw new InvalidDataException("Terrain inheritance requires this attempt's actual model submission.");
                    var submittedArmy = actualArmy.LastAcceptedFrame ??
                        throw new InvalidDataException("Terrain inheritance requires this attempt's actual zero-Army submissions.");
                    var actualInheritedRaster = terrain.PrepareSubmittedTerrainRaster(frame, submittedModels, submittedArmy);
                    return Task.FromResult(current.PrepareTerrainStencil(frame, terrainStencil, actualInheritedRaster));
                }
            };
            return new(compiler, lighting, population, army, sceneSource, materials,
                current.Bootstrap, current.BootstrapEarly, sources, terrain.PrepareFrame, terrain.PrepareResize)
                { OtherOwnedStages = [sky], CommitAcceptedFrame = acceptedContext =>
                    {
                        // Validate both candidates before committing either.
                        // Substage GPU success is not a source-time commit.
                        _ = current.RequireSeasonRgb(acceptedContext);
                        actualSky.ValidateCompleteFrameCandidate(acceptedContext);
                        current.CommitCompleteFrame(acceptedContext);
                        actualSky.CommitCompleteFrame(acceptedContext);
                    } };
        }
        catch (Exception buildFailure)
        {
            IAsyncDisposable? failedSky = buildFailure is OriginalNativeMapBorrowerConstructionException nested
                ? nested.RetainedPackage : sky;
            // The failed package owns all references, including compiler.
            // Stop at the first failed borrower; do not release its dependencies.
            var partial = new OriginalSceneViewNativeMapPartialRetirementPackage(
                generation, compiler, army, population, failedSky, lighting);
            try { await partial.RetireAsync(); }
            catch (Exception retirementFailure)
            {
                OriginalSceneViewNativeMapPartialRetirementPackage.RetainFailure(partial);
                throw new OriginalSceneViewNativeMapPartialConstructionException(
                    buildFailure, retirementFailure, partial);
            }
            throw;
        }
    }

}

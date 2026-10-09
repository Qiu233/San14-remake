using Godot;
using San14.Data;

/// <summary>Actual five-root Directional World assembly with the current
/// source-owned static and Army inventories. Main activation requires a complete accepted Host output.
/// No caller can supply a success boolean,
/// empty GPU callback, fabricated atmosphere or guessed terrain constants.</summary>
public static class OriginalSceneViewNativeMapWorldSourceFactory
{
    public static OriginalSceneViewNativeMapWorldDependencyFactory Create()
        => (loaded, capture) => BuildAsync(loaded, capture);

    public static async Task<OriginalSceneViewNativeMapWorldDependencies> BuildAsync(
        OriginalSceneViewNativeMapLoadInputs loaded,
        Func<OriginalNativeMapFrameContext, OriginalSceneViewNativeMapFrameCapture> capture,
        bool allowSkyReadback = false,
        Func<OriginalSkyDirectionalAmbientDeviceOwner, Task>? configureSkyBeforeStartup = null)
    {
        if (configureSkyBeforeStartup is not null && !allowSkyReadback)
            throw new InvalidOperationException("Sky numeric observation requires explicit readback construction.");
        ArgumentNullException.ThrowIfNull(loaded); ArgumentNullException.ThrowIfNull(capture);
        var repo = loaded.Repository;
        string locale = loaded.InitialFrame.MainCamera.Camera.ExecutableLocale;
        if (locale is not ("TC" or "SC"))
            throw new NotSupportedException("This World factory requires an actual TC/SC source installation.");
        string generation = "owned-SceneView-native:" + loaded.InitialFrame.SceneGeneration + ":" + Guid.NewGuid().ToString("N");
        var graph = repo.LoadRenderGraph("system", 0x024496EE);
        var shaders = repo.LoadObjects("system", 0x718CCB8B);
        var textures = repo.LoadObjects("system", 0x93FB3280);
        var plan = OriginalDeferredTexturePlan.Read(graph, 166, shaders, textures,
            repo.LoadShaderContainer("system", 0x9D8E811F), 0);
        var hostSource = OriginalMainMapContextOwnerSource.LoadFreshHostDefaults(locale);
        var binding = OriginalMapSourceCreatedWorldNativeSession.CreateExpectedBinding(plan,
            generation + "/View", generation + "/Main", generation + "/Directional80-L");
        var cpu = OriginalSceneViewNativeMapCpuSourceOwner.CreateFresh(loaded, binding.ViewIdentity,
            generation + "/static-source-generation");
        var shaderTables = OriginalNativeWorldShaderTableRegistry.CreateLoaded(shaders);
        var model = new OriginalSceneViewNativeMapModelSourceOwner(repo, graph, shaders, hostSource, shaderTables);
        var primitiveOrderSource = OriginalWorldPrimitiveOrderSource.LoadDefaultView0(repo);
        var staticOrder = new OriginalNativeMapStaticWorldOrderOwner(model,
            primitiveOrderSource, model.ReadFinalOwnedTable);
        var staticColorSource = OriginalModelFrontendColorSource.Load(repo, locale);
        var staticQueue54 = OriginalModelRendererStateSourceReader.ReadQueue(graph, 54);
        var staticQueue55 = OriginalModelRendererStateSourceReader.ReadQueue(graph, 55);
        var mode = OriginalMainMapCameraModeOwner.CreateFresh(locale, binding.CameraOwnerIdentity);

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
        OriginalMapSourceCreatedDirectionalLightingDeviceOwner? lighting = null;
        OriginalNativeMapModelPopulationRenderer? population = null;
        OriginalNativeMapArmyPopulationRenderer? army = null;
        OriginalSceneViewNativeMapWorldSkySourceOwner? sky = null;
        OriginalSceneViewNativeMapStockBackendSourceOwner? stockBackend = null;
        OriginalSceneViewNativeMapWorldGeometrySourceOwner? geometry = null;
        try
        {
            compiler = OriginalShaderCompilerRuntime.Open();
            var season = OriginalMapSeasonDirectionalLightSources.Load(repo,
                (loaded.InitialFrame.World.NativeMapSeason ?? throw new InvalidDataException("World source season is absent.")).ThirdDate.Month);
            lighting = await OriginalMapSourceCreatedDirectionalLightingDeviceOwner.CreateSourceReadyNormalizedAsync(
                repo, season, loaded.InitialFrame.MainCamera.Camera.Source, hostSource.ContextFlags3E4,
                generation + "/Directional-backend-named", allowReadback: false);
            var actualLighting = lighting;
            var metadata = repo.EffectiveScenarios.Single(s =>
                GameResourceId.File(s.SourcePath).ToString() == loaded.InitialFrame.World.ScenarioKey && s.Locale == locale);
            army = OriginalNativeMapArmyPopulationRenderer.CreateForPlayableOwnedSource(compiler, repo,
                generation + "/Army", repo.LoadScenario(metadata), repo.LoadCeMap(), loaded.Projection,
                loaded.Terrain.HeightField, ctx =>
                {
                    var captured = capture(ctx);
                    float elapsed = (float)captured.PresentationElapsedSeconds;
                    if (!double.IsFinite(captured.PresentationElapsedSeconds) || !float.IsFinite(elapsed) || elapsed < 0)
                        throw new InvalidDataException("Army animation requires the actual finite presentation elapsed time.");
                    return new(captured.World, new(elapsed, OriginalArmyAnimationClockPolicy.RepeatDeclaredDuration,
                        generation + "/owned-SceneView-presentation-elapsed/action0-repeat"));
                });
            var bootstrapArmy = await army.CaptureActualLogicalInventoryAsync(loaded.InitialFrame.World);
            var current = new OriginalSceneViewNativeMapWorldF103SourceOwner(loaded, cpu,
                actualLighting.RequireCurrentSeasonSources,plan,binding,generation,mode,
                actualLighting.RequireCurrentSeasonProviders,bootstrapArmy);
            var sceneProxy = OriginalMapSceneProxySourcePolicy.LoadFreshWorld(repo,
                current.Bootstrap.First,generation+"/node1-Scene-proxy-allocation");
            var staticStencil = OriginalNativeStaticStencilCurrentOwner.CreateFreshSourceClass18NoExternalStencilAssignments();
            var terrainStencil = OriginalNativeTerrainStencilCurrentOwner.CreateFreshFieldSceneNoExternalAssignments(loaded.TerrainMaterial);
            population = OriginalNativeMapModelPopulationRenderer.CreateForCurrentOwnedSource(compiler,
                cpu.SourceRevision, materials.Cache, model.LoadDraw, current.RequirePopulation,
                model.PrepareRuntime, new(512L << 20, 256L << 20, 512, 4096), model.CommitAccepted);
            population.NativeStencilPreparation = (ctx, prepared) =>
                staticStencil.PrepareCurrent(ctx, current.RequireAdmission(ctx), prepared);
            sky = await OriginalSceneViewNativeMapWorldSkySourceOwner.BuildAsync(loaded, compiler,
                lighting, current.Bootstrap, current.BootstrapNode51, allowSkyReadback, configureSkyBeforeStartup);
            var family = OriginalDeferredSourceViewFamilyOwner.CreateFresh(
                OriginalDeferredSamplerSources.Load(repo.SourceDirectory, locale),
                loaded.InitialFrame.MainCamera.Camera.Source, binding, generation,
                OriginalDeferredSourceViewFamilyScope.FreshSourceMainNoExternalFamilyAssignments);
            var filters = OriginalMapCommonViewFilterSource.LoadGameDefault(repo);
            var prefixSource = OriginalDeferredAuxiliaryPrefixSource.Read(graph,
                repo.LoadObjects("system", 0x908AB6E2), generation);
            var prefixOwner = prefixSource.CreateFreshQueryOwner(current.Bootstrap.First.Camera,
                current.Bootstrap.First.ViewFields, generation, generation + "/source-RLR-construction");
            var actualArmy = army;
            var actualSky = sky;
            var actualPopulation = population;
            var sources = OriginalMapSourceCreatedWorldPlayableCurrentSources.FromSourceNullGeometryPixelFactory(
                (ctx, preparedArmy) =>
                {
                    var currentCapture = capture(ctx);
                    return current.PrepareCurrent(ctx, currentCapture, preparedArmy,
                        currentCapture.PresentationDeltaSeconds);
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
                frame => OriginalMapFinalOutputProductionFrameInputs.FromWorldSourceSession(frame, frame.Context,
                    current.RequireCurrent(frame.Context),
                    OriginalMapSeasonRuntime.PrepareFrame(capture(frame.Context).World.NativeMapSeason!,
                        frame.Context.FrameNumber), OriginalNativeCameraTangentDispatch.Sse),
                frame => actualSky.SelectCurrentAmbient(frame,current.RequireCurrent(frame.Context))) with
            {
                SubmitStaticGeometry = async (frame, renderer) =>
                {
                    await using var queues = await renderer.PrepareQueuesAsync(frame.Context,
                        current.RequireAdmission(frame.Context), staticStencil);
                    var order = staticOrder.PrepareCurrentOrder(queues.Plan);
                    void RequireCurrent() => _ = current.RequireCurrent(frame.Context);
                    var color54 = OriginalNativeMapStaticActiveColorSourceOwner.CaptureActualEntry(staticColorSource,
                        queues.Plan, staticQueue54, queues.Plan.Admission.Frame.Common, RequireCurrent);
                    await queues.DrawQueueAsync(54, order, color54);
                    var color55 = OriginalNativeMapStaticActiveColorSourceOwner.CaptureActualEntry(staticColorSource,
                        queues.Plan, staticQueue55, queues.Plan.Admission.Frame.Common, RequireCurrent);
                    await queues.DrawQueueAsync(55, order, color55);
                    _ = queues.CompleteAccepted();
                },
                CreateGraphicsColorMaskOwner = actualFinal =>
                {
                    var history = OriginalMapSourceCreatedGraphicsColorMaskOwner.ReadFresh(repo, locale,
                        generation + "/actual-source-frontend", binding.ViewIdentity, actualFinal);
                    stockBackend = OriginalSceneViewNativeMapStockBackendSourceOwner.ReadFresh(history.OwnedSource);
                    history.BindCurrentStockHistory(stockBackend.OwnedSource, stockBackend.RequirePreparedPacket);
                    actualSky.BindActualGraphicsColorMaskOwner(history);
                    return history;
                },
                AfterCurrentPreparedWithLighting = async (frame, consumerReceipt) =>
                {
                    var armyFrame = await actualArmy.CaptureBeforeFrameAsync(frame.Context);
                    var world = current.RequireCurrent(frame.Context);
                    var armyPublication = await actualArmy.PrepareCurrentEmptyWorldPrimitivePublicationAsync(armyFrame,
                        frame.Lighting, frame.PreparedSourceFrame!.Common, world.CountedQueue);
                    await actualArmy.BindCurrentPrimitiveAdmissionAsync(armyFrame, armyPublication,
                        current.RequireAdmission(frame.Context));
                    await actualSky.SubmitCurrentAsync(frame, world,
                        world.Node51 ?? throw new InvalidDataException("Actual World node51 execution is unavailable."),
                        capture(frame.Context).PresentationDeltaSeconds, consumerReceipt);
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
            geometry = new OriginalSceneViewNativeMapWorldGeometrySourceOwner(repo, locale, current, actualArmy,
                actualSky, staticOrder, shaderTables, primitiveOrderSource, staticColorSource,staticStencil);
            return new(compiler, lighting, sky, population, army, sceneSource, materials,
                current, sources, terrain.PrepareFrame, terrain.PrepareResize, acceptedContext =>
                {
                    _ = current.RequireCurrent(acceptedContext);
                    actualSky.ValidateCompleteFrameCandidate(acceptedContext);
                    current.CommitCompleteFrame(acceptedContext);
                    actualSky.CommitCompleteFrame(acceptedContext);
                }) { Repository = repo, SourceSceneProxyPolicy = sceneProxy, CaptureCurrentFrame = capture,
                    OrdinaryCurrent = geometry.CreateCurrentSources(sources), GeometryLifetime = geometry.Targets, GeometrySource = geometry,
                    Preceding = new OriginalNativeMapSceneStages(BeforeTargetRetirement: geometry.RetireTargetGenerationAsync) };
        }
        catch (Exception buildFailure)
        {
            IAsyncDisposable? failedSky = buildFailure is OriginalNativeMapBorrowerConstructionException nested
                ? nested.RetainedPackage : sky;
            // The failed package owns all references, including compiler.
            // Stop at the first failed borrower; do not release its dependencies.
            var partial = new OriginalSceneViewNativeMapWorldPartialRetirementPackage(
                generation, compiler, geometry, army, population, failedSky, lighting);
            try { await partial.DisposeAsync(); }
            catch (Exception retirementFailure)
            {
                throw new OriginalSceneViewNativeMapWorldConstructionException(
                    buildFailure, retirementFailure, partial);
            }
            throw;
        }
    }

}

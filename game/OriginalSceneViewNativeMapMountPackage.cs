/// <summary>One SceneView lifecycle for the explicit legacy HDRI route and
/// the independent World route. Each concrete package owns its source commit
/// and ordered retirement; SceneView owns capture, input and presentation.</summary>
internal abstract class OriginalSceneViewNativeMapMountPackage : IAsyncDisposable
{
    public abstract OriginalNativeMapSurface Surface { get; }
    public bool HasAcceptedOutput => Surface.HasAcceptedOutput;
    // The key belongs to the package's actual constructed source, not the
    // latest campaign date. Legacy HDRI has no seasonal World generation.
    public virtual San14.Data.OriginalMapSeasonSelection? SeasonSelection => null;
    public abstract OriginalMapSourceCreatedPlayableTerrainFrame PrepareTerrainFrame(
        OriginalSceneViewNativeMapFrameCapture capture);
    public abstract Task BuildHostAsync(OriginalSceneViewNativeMapFrameCapture capture,
        OriginalMapSourceCreatedPlayableTerrainFrame first);
    public abstract Task RenderFrameAsync(OriginalSceneViewNativeMapFrameCapture capture,
        OriginalMapSourceCreatedPlayableTerrainFrame frame);
    public abstract Task ResizeTargetsAsync(OriginalSceneViewNativeMapFrameCapture capture);
    public virtual Task InstallSeasonResourcesAsync(OriginalSceneViewNativeMapFrameCapture capture)
        =>throw new NotSupportedException("This map package has no seasonal World resource owner.");
    public abstract ValueTask DisposeAsync();

    public static OriginalSceneViewNativeMapMountPackage FromLegacy(
        San14.Data.OriginalGameRepository repository,
        OriginalSceneViewNativeMapDependencies dependencies) => new Legacy(repository, dependencies);
    public static OriginalSceneViewNativeMapMountPackage FromWorld(
        OriginalSceneViewNativeMapWorldDependencies dependencies) => new World(dependencies);

    private sealed class Legacy(San14.Data.OriginalGameRepository repository,
        OriginalSceneViewNativeMapDependencies dependencies)
        : OriginalSceneViewNativeMapMountPackage
    {
        private OriginalMapSourceCreatedPlayableHost? host;
        private bool hostRetired;
        public override OriginalNativeMapSurface Surface =>
            (host ?? throw new InvalidOperationException("Legacy map Host is not built.")).Surface;
        public override OriginalMapSourceCreatedPlayableTerrainFrame PrepareTerrainFrame(
            OriginalSceneViewNativeMapFrameCapture capture) => dependencies.PrepareTerrainFrame(capture);
        public override async Task BuildHostAsync(OriginalSceneViewNativeMapFrameCapture capture,
            OriginalMapSourceCreatedPlayableTerrainFrame first)
        {
            if (host is not null || hostRetired)
                throw new InvalidOperationException("Legacy mount package can build one Host.");
            host = await OriginalMapSourceCreatedPlayableHost.BuildAsync(repository,
                dependencies.Compiler, dependencies.SceneSource, dependencies.Lighting,
                dependencies.Population, dependencies.Army, dependencies.Materials,
                dependencies.Bootstrap, dependencies.BootstrapEarly, dependencies.Current,
                first, dependencies.Preceding, dependencies.Exposure);
            CommitAcceptedOutput();
        }
        private void CommitAcceptedOutput()
        {
            var context = host?.Scene.LastCompletedContext
                ?? throw new InvalidDataException("Complete legacy map has no accepted context.");
            dependencies.CommitAcceptedFrame?.Invoke(context);
        }
        public override async Task RenderFrameAsync(OriginalSceneViewNativeMapFrameCapture capture,
            OriginalMapSourceCreatedPlayableTerrainFrame frame)
        {
            await (host ?? throw new InvalidOperationException("Legacy map Host is not built.")).RenderFrameAsync(frame);
            CommitAcceptedOutput();
        }
        public override async Task ResizeTargetsAsync(OriginalSceneViewNativeMapFrameCapture capture)
        {
            var targets = dependencies.PrepareResize(capture)
                ?? throw new InvalidDataException("Production resize writer returned no coherent target extents.");
            await (host ?? throw new InvalidOperationException("Legacy map Host is not built.")).ResizeTargetsAsync(
                targets.TerrainExtents, targets.OutputExtents);
        }
        public override async ValueTask DisposeAsync()
        {
            if (!hostRetired)
            {
                if (host is not null) await host.DisposeAsync();
                hostRetired = true;
            }
            await dependencies.RetireBorrowedOwnersAsync();
        }
    }

    private sealed class World(OriginalSceneViewNativeMapWorldDependencies dependencies)
        : OriginalSceneViewNativeMapMountPackage
    {
        private OriginalSceneViewNativeMapWorldPackage? package;
        public override San14.Data.OriginalMapSeasonSelection SeasonSelection =>
            dependencies.WorldSource.SeasonResources.Selection;
        public override OriginalNativeMapSurface Surface =>
            (package ?? throw new InvalidOperationException("World map Host is not built.")).Surface;
        public override OriginalMapSourceCreatedPlayableTerrainFrame PrepareTerrainFrame(
            OriginalSceneViewNativeMapFrameCapture capture) => dependencies.PrepareTerrainFrame(capture);
        public override async Task BuildHostAsync(OriginalSceneViewNativeMapFrameCapture capture,
            OriginalMapSourceCreatedPlayableTerrainFrame first)
        {
            if (package is not null) throw new InvalidOperationException("World mount package can build one Host.");
            // BuildHostAsync renders and commits the first accepted output.
            // SceneView must not repeat that source-time commit.
            package = await dependencies.BuildHostAsync(capture);
        }
        public override Task RenderFrameAsync(OriginalSceneViewNativeMapFrameCapture capture,
            OriginalMapSourceCreatedPlayableTerrainFrame frame) =>
            (package ?? throw new InvalidOperationException("World map Host is not built.")).RenderFrameAsync(capture);
        public override Task ResizeTargetsAsync(OriginalSceneViewNativeMapFrameCapture capture) =>
            (package ?? throw new InvalidOperationException("World map Host is not built.")).ResizeTargetsAsync(capture);
        public override async Task InstallSeasonResourcesAsync(OriginalSceneViewNativeMapFrameCapture capture)
        {
            _=await (package??throw new InvalidOperationException("World map Host is not built.")).InstallSeasonResourcesAsync(capture);
        }
        public override async ValueTask DisposeAsync()
        {
            if (package is not null) await package.DisposeAsync();
            else
            {
                // BuildHostAsync may have accepted its first frame but failed
                // during source commit and retained the newly built package.
                // Its Host must retire before this dependency generation.
                foreach (var failed in OriginalSceneViewNativeMapWorldPackage.FailedRetirements
                    .Where(failed => ReferenceEquals(failed.Dependencies, dependencies)))
                    await failed.DisposeAsync();
                await dependencies.DisposeAsync();
            }
        }
    }
}

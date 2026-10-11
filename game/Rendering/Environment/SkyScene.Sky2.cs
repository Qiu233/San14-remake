using San14.Data;

public sealed partial class SkyScene
{
    /// <summary>Only the Sky2 program and its six draws are seasonal. They
    /// borrow the existing WORK target and the texture owner held by the caller.</summary>
    internal sealed class Sky2Resources : IAsyncDisposable
    {
        public Sky2Program Program { get; }
        public IReadOnlyList<Sky2Draw> Faces { get; }
        private readonly NativeMapBorrowerRetirementPackage retirement;

        public Sky2Resources(Sky2Program program, IReadOnlyList<Sky2Draw> faces)
        {
            Program = program; Faces = faces;
            retirement = new("Sky2-branch/" + Guid.NewGuid().ToString("N"),
                faces.Reverse().Cast<IAsyncDisposable>().Append(program).ToArray());
        }
        public ValueTask DisposeAsync() => retirement.DisposeAsync();
    }

    public sealed class PreparedSky2Replacement : IAsyncDisposable
    {
        internal readonly SkyScene scene;
        internal readonly Sky2Program previous;
        internal Sky2Resources? resources;
        internal readonly SkySceneProgramSelection selection;
        internal bool installed;

        internal PreparedSky2Replacement(SkyScene scene, Sky2Program previous,
            Sky2Resources resources, SkySceneProgramSelection selection)
        { this.scene = scene; this.previous = previous; this.resources = resources; this.selection = selection; }

        public void RequireInstallable() => scene.RequireSky2Replacement(this);
        public async ValueTask DisposeAsync()
        {
            if (!installed && resources is { } pending)
            { await pending.DisposeAsync(); resources = null; }
        }
    }

    /// <summary>Allocates the replacement without changing the installed
    /// binding generation, WORK targets, filter programs or generation progress.
    /// Buffers are allocation storage, not a published source frame.</summary>
    public async Task<PreparedSky2Replacement> PrepareSky2ReplacementAsync(
        LoadedShaderContainer source, Vkd3dShaderCompiler compiler,
        Sky2TextureBindingSnapshot snapshot, IReadOnlyList<Sky2DeviceInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            if (_phase is not (SkyScenePhase.Idle or SkyScenePhase.Faulted))
                throw new InvalidOperationException("Finish the current Sky generation before replacing its resources.");
            Sky2Program.RequireCurrent(); snapshot.RequireCurrent();
            var selection = Selection with { TextureBindings = snapshot };
            Sky2Program? program = null;
            var faces = new List<Sky2Draw>();
            try
            {
                program = await Sky2Program.BuildAsync(source, selection.Sky2Combination,
                    compiler, selection.Sky2Operation, snapshot);
                for (uint face = 0; face < 6; face++)
                    faces.Add(await Sky2Draw.BuildAsync(program, PrimaryWork,
                        SkySceneSchedule.SurfaceId(PrimaryWork.Source.LevelNum, face, 0),
                        new byte[program.VertexConstantByteLength], new byte[program.PixelConstantByteLength],
                        inputs, new Sky2NativeComposition(selection.Sky2ColorWriteMask!.Value)));
                return new(this, Sky2Program, new(program, faces.AsReadOnly()), selection);
            }
            catch (Exception failure)
            {
                var partial = new NativeMapBorrowerRetirementPackage("Sky2-replacement-partial/" + Guid.NewGuid().ToString("N"),
                    faces.AsEnumerable().Reverse().Cast<IAsyncDisposable?>().Append(program).ToArray());
                try { await partial.DisposeAsync(); }
                catch (Exception cleanup)
                { partial.RetainFailure(); throw new NativeMapBorrowerConstructionException(failure, cleanup, partial); }
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    private void RequireSky2Replacement(PreparedSky2Replacement replacement)
    {
        if (!_gate.Wait(0)) throw new InvalidOperationException("Sky resources are currently borrowed.");
        try { RequireSky2ReplacementCore(replacement); }
        finally { _gate.Release(); }
    }

    private void RequireSky2ReplacementCore(PreparedSky2Replacement replacement)
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        if (!ReferenceEquals(replacement.scene, this) || replacement.installed || replacement.resources is null ||
            !ReferenceEquals(replacement.previous, Sky2Program) || _phase is not (SkyScenePhase.Idle or SkyScenePhase.Faulted))
            throw new InvalidOperationException("Sky2 replacement belongs to another or changed scene.");
        Sky2Program.RequireCurrent(); replacement.selection.TextureBindings.RequireCurrent();
    }

    /// <summary>Commit only the prepared branch. Its first BeginGeneration
    /// must write all six actual current packets before any Sky2 draw.</summary>
    public IAsyncDisposable InstallSky2Replacement(PreparedSky2Replacement replacement)
    {
        if (!_gate.Wait(0)) throw new InvalidOperationException("Sky resources are currently borrowed.");
        try
        {
            RequireSky2ReplacementCore(replacement);
            var previous = sky2Resources;
            sky2Resources = replacement.resources!;
            Selection = replacement.selection;
            sky2NeedsCurrentFrame = true;
            replacement.installed = true;
            replacement.resources = null;
            return previous;
        }
        finally { _gate.Release(); }
    }
}

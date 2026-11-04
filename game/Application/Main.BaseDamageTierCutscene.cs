using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class BaseTierCutsceneWorkspace(GameplaySession session, GameState world,
        MapSceneView map, CampaignPeriodPlayback? playback, long batch, int index, BaseDamageTierCutsceneRequest request)
    {
        internal readonly GameplaySession Session = session;
        internal readonly GameState World = world;
        internal readonly MapSceneView Map = map;
        internal readonly CampaignPeriodPlayback? Playback = playback;
        internal readonly long Batch = batch;
        internal readonly int Index = index;
        internal readonly BaseDamageTierCutsceneRequest Request = request;
        internal readonly CancellationTokenSource Lifetime = new();
        internal Control? Shield;
        internal CollectionMoviePlaybackView? Movie;
        internal Task? Retirement;
        internal bool CameraEntered, AudioEntered;
    }
    private BaseTierCutsceneWorkspace? baseTierCutscene;
    // Only abandoned BaseTier movie children enter this finite drain. Their
    // decoder/audio retirement must finish while the parent tree still lives.
    private readonly List<Task> baseTierMovieRetirements = [];
    private GameState? baseTierFailedWorld;
    private bool HasBaseTierCutscene => baseTierCutscene is not null;
    private bool IsBaseTierCutsceneCurrent => baseTierCutscene is { } owner && IsBaseTierCurrent(owner);
    private bool IsBaseTierCurrent(BaseTierCutsceneWorkspace owner) => ReferenceEquals(baseTierCutscene, owner) &&
        !owner.Lifetime.IsCancellationRequested && ReferenceEquals(_session, owner.Session) &&
        ReferenceEquals(_residentMap, owner.Map) && !_failed && !_shuttingDown &&
        (owner.Playback is { } playback ? ReferenceEquals(_periodPlayback, playback) && ReferenceEquals(playback.Candidate, owner.World) :
            ReferenceEquals(PersonnelPresentationOwner(owner.Session), owner.World));

    private bool TryBeginBaseTierCutscene(GameState world, MapSceneView map, CampaignPeriodPlayback? playback = null)
    {
        if (baseTierCutscene is not null) return true;
        if (ReferenceEquals(baseTierFailedWorld, world)) return false;
        foreach (var batch in world.CampaignEffectRequests?.PendingBatches ?? [])
        {
            if (batch.Source != CampaignEffectSource.NormalCombatApplication) continue;
            for (int index = 0; index < batch.Requests.Length; index++)
            {
                if (batch.Requests[index].BaseDamageTierCutscene is not { } request) continue;
                var owner = new BaseTierCutsceneWorkspace(_session ?? throw new InvalidOperationException("base-tier.session"),
                    world, map, playback, batch.Sequence, index, request);
                baseTierCutscene = owner;
                if (playback is not null) playback.BaseTierPresentationOpen = true;
                _ = PresentBaseTierCutscene(owner);
                return true;
            }
        }
        return false;
    }

    private async Task PresentBaseTierCutscene(BaseTierCutsceneWorkspace owner)
    {
        try
        {
            var enabled = BaseDamageTierCutsceneSource.IsPlaybackEnabled(owner.Request, CurrentMoviePlaybackEnabled());
            if (enabled is null) throw new InvalidDataException("base-tier.movie.application_gate_unknown");
            // 84F6A0's actual disabled branch never enters the cutscene state.
            if (enabled == false) { CompleteBaseTierCutscene(owner); return; }
            if (_repository is null) throw new InvalidOperationException("base-tier.movie.repository");
            var publication = BaseDamageTierCutsceneSource.ReadPlayback(_repository, owner.Request, _ui.SourceLocale);
            owner.Shield = new Control { Name = "BaseTierCutsceneInput", ZIndex = 90, MouseFilter = Control.MouseFilterEnum.Stop };
            AddChild(owner.Shield); owner.Shield.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            owner.Map.BeginBaseTierCamera(owner); owner.CameraEntered = true;
            (_sceneAudioBinding ?? throw new InvalidOperationException("base-tier.audio.owner")).BeginBaseTierCutsceneAudio(owner);
            owner.AudioEntered = true;
            await WaitBaseTierCamera(owner);
            RequireBaseTierCurrent(owner);
            owner.Map.FocusBaseTierCamera(owner, owner.Request);
            await WaitBaseTierCamera(owner);
            RequireBaseTierCurrent(owner);
            _sceneAudioBinding!.PrepareBaseTierCutsceneMovie(owner);
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var movie = new CollectionMoviePlaybackView(); owner.Movie = movie; AddChild(movie);
            movie.Configure(_repository, _ui, publication, ResolveMovieNativeLibrary(), () => IsBaseTierCurrent(owner) && movie.PlaybackActive,
                () => _preferences.Audio.Movie / 100f, () => finished.TrySetResult(), error => finished.TrySetException(error));
            movie.ZIndex = 100;
            // Source phase3 queues return immediately after creating its child.
            // Map's shared camera continues ticking while this child owns input;
            // this parent does not advance until the child's real retirement.
            owner.Map.ReturnBaseTierCamera(owner);
            await finished.Task.WaitAsync(owner.Lifetime.Token);
            await RetireBaseTierMovie(owner);
            RequireBaseTierCurrent(owner);
            await WaitBaseTierCamera(owner);
            RequireBaseTierCurrent(owner);
            EndBaseTierParent(owner, interrupted: false);
            CompleteBaseTierCutscene(owner);
        }
        catch (OperationCanceledException) when (owner.Lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!ReferenceEquals(baseTierCutscene, owner)) return;
            // Failures retire actual media/camera/audio and preserve the intent.
            Exception failure = error;
            try { await RetireBaseTierMovie(owner); }
            catch (Exception retirementError) { failure = new AggregateException(failure, retirementError); }
            try { EndBaseTierParent(owner, interrupted: true); }
            catch (Exception parentError) { failure = new AggregateException(failure, parentError); }
            baseTierCutscene = null;
            owner.Lifetime.Dispose();
            if (owner.Playback is { } playback) playback.FailPresentation(failure);
            else { baseTierFailedWorld = owner.World; ReportPlayFailure(failure); RefreshPlayControls(); }
        }
    }

    private void RequireBaseTierCurrent(BaseTierCutsceneWorkspace owner)
    {
        owner.Lifetime.Token.ThrowIfCancellationRequested();
        if (!IsBaseTierCurrent(owner)) throw new InvalidOperationException("base-tier.source_owner_changed");
        if (owner.CameraEntered && !owner.Map.IsBaseTierCameraCurrent(owner))
            throw new InvalidOperationException("base-tier.camera.owner_retired");
    }

    private async Task WaitBaseTierCamera(BaseTierCutsceneWorkspace owner)
    {
        while (!owner.Map.IsStoryCameraIdle(owner))
        {
            RequireBaseTierCurrent(owner);
            // An actual application frame, never a guessed movie/camera timer.
            var tree = GetTree(); var token = owner.Lifetime.Token;
            var frame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Reached() { tree.ProcessFrame -= Reached; frame.TrySetResult(); }
            tree.ProcessFrame += Reached;
            using var registration = token.Register(() => { tree.ProcessFrame -= Reached; frame.TrySetCanceled(token); });
            await frame.Task;
        }
    }

    private Task RetireBaseTierMovie(BaseTierCutsceneWorkspace owner)
    {
        if (owner.Retirement is not null) return owner.Retirement;
        if (owner.Movie is not { } movie) return Task.CompletedTask;
        owner.Movie = null;
        movie.Abandon();
        var retirement = movie.Retirement;
        movie.QueueFree();
        return owner.Retirement = retirement;
    }

    private void EndBaseTierParent(BaseTierCutsceneWorkspace owner, bool interrupted)
    {
        try
        {
            if (owner.CameraEntered && GodotObject.IsInstanceValid(owner.Map))
            { owner.CameraEntered = false; owner.Map.EndBaseTierCamera(owner, interrupted); }
        }
        finally
        {
            try
            {
                if (owner.AudioEntered)
                { owner.AudioEntered = false; _sceneAudioBinding?.EndBaseTierCutsceneAudio(owner); }
            }
            finally
            {
                if (owner.Shield is { } shield)
                { owner.Shield = null; shield.GetParent()?.RemoveChild(shield); shield.QueueFree(); }
            }
        }
    }

    private void CompleteBaseTierCutscene(BaseTierCutsceneWorkspace owner)
    {
        RequireBaseTierCurrent(owner);
        bool completed = owner.Playback is { } playback ?
            playback.TryCompleteBaseTier(owner.World, owner.Batch, owner.Index, owner.Request) :
            owner.Session.TryCompleteBaseDamageTierCutscene(owner.World, owner.Batch, owner.Index, owner.Request);
        if (!completed) throw new InvalidOperationException("base-tier.source_head_receipt");
        baseTierCutscene = null;
        owner.Lifetime.Dispose();
        if (owner.Playback is { } period)
        {
            period.BaseTierPresentationOpen = false;
            PublishPeriodPlayback(period, owner.Map, period.Candidate!);
        }
        else
        {
            SynchronizePersonnelPresentationOwner(owner.Session);
            UpdateResidentMap();
            RefreshPlayablePeriodReport(); RefreshPlayControls();
            Callable.From(() => { TryOpenPersonnelPresentation(); }).CallDeferred();
        }
    }

    private bool HandleBaseTierCutsceneInput(InputEvent input)
    {
        if (baseTierCutscene is not { } owner || !IsBaseTierCurrent(owner)) return false;
        if (input is not (InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false } or
            InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })) return false;
        owner.Movie?.RequestBack(); return true;
    }

    private void RetireBaseTierCutscene(CampaignPeriodPlayback? playback = null)
    {
        if (baseTierCutscene is not { } owner || playback is not null && !ReferenceEquals(owner.Playback, playback)) return;
        baseTierCutscene = null;
        owner.Lifetime.Cancel();
        var retirement = RetireBaseTierMovie(owner);
        // Release the source/camera borrow synchronously; actual decoder/audio
        // retirement remains joined before the operation can leave its parent.
        try { EndBaseTierParent(owner, interrupted: true); }
        catch (Exception error) { GD.PushWarning(error.ToString()); }
        if (owner.Playback is not null) owner.Playback.BaseTierPresentationOpen = false;
        Task completion = Drain();
        baseTierMovieRetirements.RemoveAll(task => task.IsCompletedSuccessfully);
        baseTierMovieRetirements.Add(completion);
        _ = ObserveBaseTierMovieRetirement(completion);
        async Task Drain()
        {
            try { await retirement; }
            finally { owner.Lifetime.Dispose(); }
        }
    }

    private async Task DrainBaseTierMovieRetirementsAsync()
    {
        // Shutdown, return-to-title and failed-interface teardown have stopped
        // new scene admission before taking this actual retirement snapshot.
        await Task.WhenAll(baseTierMovieRetirements.ToArray());
        baseTierMovieRetirements.RemoveAll(task => task.IsCompletedSuccessfully);
    }

    private static async Task ObserveBaseTierMovieRetirement(Task retirement)
    {
        try { await retirement; }
        catch (Exception error) { GD.PushWarning($"Base damage-tier movie retirement failed: {error}"); }
    }
}

using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class UniqueDirectionWorkspace(GameplaySession session, MapSceneView map,
        TacticPresentationRequest request, UniqueTacticLayoutPublication layout)
    {
        internal readonly GameplaySession Session = session;
        internal readonly MapSceneView Map = map;
        internal readonly TacticPresentationRequest Request = request;
        internal readonly UniqueTacticLayoutPublication Layout = layout;
        internal readonly List<Task> Requests = [];
        internal readonly List<object> Beams = [];
        internal UniqueTacticSceneView? View;
        internal UniqueTacticEffectHost? Effects;
        internal Task? EffectFrame, Retirement;
        internal double Elapsed;
        internal bool OwnsCamera;
        internal bool MouseHeld;
        internal int MouseHoldUpdates;
        internal Vector2 MousePress;
    }
    private UniqueDirectionWorkspace? uniqueDirection;
    private readonly HashSet<long> uniqueDirectionFailures = [];

    private void AdvanceUniqueDirection(double seconds, int framesPerSecond)
    {
        if (_session is not { } session || ordinaryTacticSession != session || _residentMap is not { } map ||
            ordinaryTacticMap != map || _campaignComposition is not { } composition ||
            (_advancingPeriod && _periodPlayback?.Candidate is null) || _failed || _shuttingDown || IsReturningToTitle) return;
        long? attemptingSequence = null;
        try
        {
            var owner = OrdinaryTacticOwner(session);
            var request = owner.ManualTacticIntervention?.Directions.FirstOrDefault();
            attemptingSequence = request?.Sequence;
            if (uniqueDirection is { } previous && (previous.Session != session || previous.Map != map ||
                !ReferenceEquals(previous.Request, request))) CloseUniqueDirection();
            if (request is null) return;
            if (uniqueDirectionFailures.Contains(request.Sequence)) return;
            if (uniqueDirection is null)
            {
                if (HasAdministrationPage || HasMilitaryOrderPage || HasEnvironmentSettings || HasCampaignSavePage ||
                    HasMapSystemMenu || HasCommandWorkspace || HasPersonnelPresentation || HasStoryPresentation || _detailsView is not null) return;
                var query = composition.PrepareArmyQueries(owner).QueryUniqueTacticLayout(owner, request);
                var layout = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
                var workspace = new UniqueDirectionWorkspace(session, map, request, layout);
                uniqueDirection = workspace;
                var view = new UniqueTacticSceneView { ZIndex = 110 }; workspace.View = view;
                AddChild(view);
                view.Configure(_repository!, map, layout,
                    (kind, layer) => workspace.Effects!.TryCreate(kind, layer, view.Cutin.EffectOwner, ordinaryTacticLifetime.Token),
                    () => workspace.Effects!.StopLinked(view.Cutin.EffectOwner),
                    () => workspace.Effects?.StopOwned(view.Cutin.EffectOwner),
                    id => workspace.Requests.Add(_sceneAudioBinding!.PlayNormalSoundAsync(id, ordinaryTacticLifetime.Token)),
                    () => PrepareUniqueDirectionVoice(workspace),
                    () => workspace.Requests.Add(_sceneAudioBinding!.PlayNormalSoundAsync(layout.InitialSoundId, ordinaryTacticLifetime.Token)),
                    () => PublishUniqueDirectionResults(workspace), () => PublishUniqueDirectionBeams(workspace));
                map.BeginUniqueDirection(view, request); workspace.OwnsCamera = true;
                workspace.Effects = map.CreateUniqueTacticEffectHost(view.Cutin.BackEffects, view.Cutin.FrontEffects,
                    view.Cutin.ReadEffectAlpha);
            }
            var active = uniqueDirection!;
            active.Elapsed += seconds;
            if (active.MouseHeld) active.MouseHoldUpdates++;
            if (active.EffectFrame is { IsCompleted: false }) return;
            active.EffectFrame?.GetAwaiter().GetResult();
            foreach (var pending in active.Requests.Where(task => task.IsCompleted)) pending.GetAwaiter().GetResult();
            active.Requests.RemoveAll(task => task.IsCompleted);
            if (!active.View!.Finished)
            {
                active.View.UpdateScene(active.Elapsed, framesPerSecond);
                active.EffectFrame = map.AdvanceUniqueTacticEffects(active.Effects!, active.Elapsed, framesPerSecond);
                active.Elapsed = 0;
                // The final update can submit real effects. Consume its
                // receipt on the next update before retiring or acknowledging.
                return;
            }
            if (active.Requests.Count != 0) return;
            active.Retirement ??= Task.WhenAll(active.Beams.Select(map.RetireTacticBeam)
                .Append(map.RetireUniqueTacticEffectHostAsync(active.Effects!)));
            if (!active.Retirement.IsCompleted) return;
            active.Retirement.GetAwaiter().GetResult();
            owner = OrdinaryTacticOwner(session);
            var manager = owner.ManualTacticIntervention!;
            if (session.PendingManualTacticCandidate is not null && !_advancingPeriod)
                throw new InvalidDataException("unique.legacy_manual_progress_host");
            bool completed = _advancingPeriod
                ? _periodPlayback!.TryCompleteUniqueDirection(owner, manager, request.Sequence)
                : session.TryCompleteUniqueDirectionPresentation(owner, manager, request.Sequence);
            if (!completed) throw new InvalidOperationException("unique.scene.receipt_owner");
            CloseUniqueDirection();
            if (!_advancingPeriod) RefreshCurrentPlayWorld();
        }
        catch (Exception error)
        {
            if (attemptingSequence is long sequence) uniqueDirectionFailures.Add(sequence);
            FailUniqueDirection(error);
        }
    }

    private void FailUniqueDirection(Exception error)
    {
        if (uniqueDirection is { } active) uniqueDirectionFailures.Add(active.Request.Sequence);
        _periodPlayback?.FailPresentation(error);
        CloseUniqueDirection(); GD.PushWarning(error.Message);
    }

    private void PrepareUniqueDirectionVoice(UniqueDirectionWorkspace workspace)
    {
        var owner = OrdinaryTacticOwner(workspace.Session);
        var person = owner.Officers.GetValueOrDefault(workspace.Layout.SpeakerId)
            ?? throw new InvalidDataException("unique.voice.captured_person");
        var speaker = UniqueTacticVoiceSpeaker.FromOfficer(person);
        var settings = _preferences.Voice.Capture();
        int voiceEvent = workspace.Layout.OrdinaryVoiceEvent;
        if (workspace.Layout.EligibleForSpecialVoice32)
        {
            var special = UniqueTacticVoicePublication.QueryHasVoice32(speaker, settings.Language);
            if (!special.Available) throw new InvalidDataException(special.UnresolvedInput);
            if (special.Value) voiceEvent = 32;
        }
        var prepared = UniqueTacticVoicePublication.Prepare(owner.CampaignAppRandom32, speaker, voiceEvent, settings,
            _sceneAudioBinding!.EventPlaybackAudioSuppressed);
        if (!prepared.Available) throw new InvalidDataException(prepared.UnresolvedOwner);
        var candidate = owner with { CampaignAppRandom32 = prepared.RandomState };
        var manager = owner.ManualTacticIntervention!;
        bool adopted = _advancingPeriod
            ? _periodPlayback!.TryAdoptUniqueDirectionAudio(owner, manager, workspace.Request.Sequence, candidate)
            : workspace.Session.TryAdoptUniqueDirectionPreparedAudio(owner, manager, workspace.Request.Sequence, candidate);
        if (!adopted) throw new InvalidOperationException("unique.voice.owner");
        if (!_advancingPeriod && workspace.Session.PendingNormalPeriod is null && prepared.RandomState is { } random)
            _presentationRandom.InstallCommittedState(random.State);
        if (prepared.SelectedVoice is { } selected)
            workspace.Requests.Add(_sceneAudioBinding.PlayUniqueTacticVoiceAsync(selected));
    }

    private void PublishUniqueDirectionResults(UniqueDirectionWorkspace workspace)
    {
        var owner = OrdinaryTacticOwner(workspace.Session);
        var query = _campaignComposition!.PrepareArmyQueries(owner).QueryUniqueTacticResults(owner,
            workspace.Request, workspace.Map.SourceMainCameraTerrain);
        var result = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
        workspace.Requests.Add(RequireUniqueTacticAdmission(workspace.Map.DispatchTacticResultEventsAsync(result, _sceneAudioBinding!, ordinaryTacticLifetime.Token)));
        workspace.Map.PublishOrdinaryTacticNumbers(result);
    }

    private void PublishUniqueDirectionBeams(UniqueDirectionWorkspace workspace)
    {
        var owner = OrdinaryTacticOwner(workspace.Session);
        var query = _campaignComposition!.PrepareArmyQueries(owner).QueryUniqueTacticBeams(owner, workspace.Request);
        var source = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
        var placements = source.CreatePlacements(workspace.Map.QueryTacticViewPose);
        foreach (var placement in placements.Value ?? throw new InvalidDataException(placements.UnresolvedInput))
        {
            object handle = new(); workspace.Beams.Add(handle);
            workspace.Requests.Add(RequireUniqueTacticAdmission(workspace.Map.EnqueueTacticBeam(handle, source.LinkedActors[placement.SlotIndex].Actor,
                placement.Transform, ordinaryTacticLifetime.Token)));
        }
    }

    private static async Task RequireUniqueTacticAdmission(Task<CaptureEffectAdmission> pending)
    {
        var admission = await pending;
        if (!admission.Completed) throw new InvalidDataException(admission.UnresolvedInput);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    private bool HandleUniqueDirectionInput(InputEvent input)
    {
        if (uniqueDirection is not { View: { } view } active || _failed || _shuttingDown) return false;
        bool requestSkip = false;
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            int usage = KeyboardPhysicalInput.Hid(key.PhysicalKeycode);
            int encoded = usage | (key.ShiftPressed ? KeyboardBindingCatalog.ShiftModifier : 0) |
                (key.CtrlPressed ? KeyboardBindingCatalog.ControlModifier : 0);
            var bindings = _preferences.KeyboardBindings;
            requestSkip = usage != 0 && (encoded == bindings[23 * 2] || encoded == bindings[23 * 2 + 1]);
        }
        else if (input is InputEventJoypadButton { ButtonIndex: JoyButton.B, Pressed: false }) requestSkip = true;
        else if (input is InputEventMouseButton button)
        {
            var primary = OperatingSystem.IsWindows() && GetSystemMetrics(23) != 0 ? MouseButton.Right : MouseButton.Left;
            if (button.ButtonIndex == primary)
            {
                var point = view.Cutin.GetGlobalTransformWithCanvas().AffineInverse() * button.Position;
                if (button.Pressed)
                { active.MouseHeld = true; active.MouseHoldUpdates = 0; active.MousePress = point; }
                else
                {
                    requestSkip = active.MouseHeld && active.MouseHoldUpdates <= 15 && active.MousePress.DistanceSquaredTo(point) <= 100;
                    active.MouseHeld = false;
                }
            }
        }
        if (requestSkip) view.RequestSkip();
        //The Direction controller consumes input before normal map/period
        //commands, so the release cannot also open a menu or select an Army.
        return true;
    }

    private void CloseUniqueDirection()
    {
        var active = uniqueDirection; uniqueDirection = null;
        if (active is null) return;
        if (active.OwnsCamera && active.View is { } view && GodotObject.IsInstanceValid(active.Map))
        { view.RestoreAndClose(); active.Map.EndUniqueDirection(view); }
        foreach (var handle in active.Beams) _ = active.Map.RetireTacticBeam(handle);
        if (active.View is { } child)
        {
            child.Hide();
            _ = RetireViewAsync();
            async Task RetireViewAsync()
            {
                try { if (active.Effects is { } effects) await active.Map.RetireUniqueTacticEffectHostAsync(effects); }
                catch (Exception error) { GD.PushWarning(error.Message); }
                finally
                {
                    if (GodotObject.IsInstanceValid(child)) { child.GetParent()?.RemoveChild(child); child.QueueFree(); }
                }
            }
        }
    }
}

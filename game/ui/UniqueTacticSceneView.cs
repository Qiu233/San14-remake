using Godot;
using San14.Data;

/// <summary>A Direction owns five command groups. Commands in one group run
/// concurrently; only the whole group's completion permits the next group.</summary>
internal partial class UniqueTacticSceneView : Control
{
    private sealed class CameraCommand(MainMapCameraControllerRequest request)
    {
        private int stage;
        internal bool Update(MapSceneView map, bool skip)
        {
            if (skip) { map.StopUniqueDirectionCamera(); stage = 2; }
            switch (stage)
            {
                case 0: map.SubmitUniqueDirectionCamera(request); stage = 1; return true;
                case 1: if (!map.UniqueDirectionCameraMoving) stage = 2; return true;
                default: return false;
            }
        }
    }

    private sealed class LambdaCommand(Action invoke, double delayMilliseconds = 0)
    {
        private bool completed;
        private double delay = delayMilliseconds;
        internal bool Update(double milliseconds, bool skip)
        {
            if (skip) { completed = true; return false; }
            if (completed) return false;
            delay = Math.Max(0, delay - milliseconds);
            if (delay > 0) return true;
            invoke(); completed = true; return true;
        }
    }

    private MapSceneView map = null!;
    private UniqueTacticCameraPlan camera = null!;
    private CameraCommand[] cameraCommands = [];
    private LambdaCommand voice = null!, sound = null!, linkedSound = null!, results = null!, beams = null!;
    private UniqueTacticCutinView cutin = null!;
    private bool skip, cancelled, waitComplete;
    private double remainingWait = 500;
    internal int Group { get; private set; }
    internal bool Finished => Group == 5;
    internal bool WasSkipped => skip;
    internal UniqueTacticCutinView Cutin => cutin;

    internal void Configure(GameResourceRepository repository, MapSceneView owner,
        UniqueTacticLayoutPublication publication, Action<int, int> createEffect,
        Action stopLinkedEffect, Action stopEffects, Action<int> playSound,
        Action playVoice, Action initialSound, Action publishResults, Action publishBeams)
    {
        map = owner; camera = UniqueTacticCameraPlan.Create(owner.SourceMainCameraFrame ??
            throw new InvalidDataException("unique.camera.actual_frame"), owner.QueryTacticViewPose(publication.CameraFocusView).Resolve());
        cameraCommands = [new(camera.Approach), new(camera.Orbit), new(camera.Restore)];
        voice = new(playVoice, 330); sound = new(initialSound);
        linkedSound = new(() => { if (publication.LinkedOfficerCount > 0) playSound(47); });
        results = new(publishResults); beams = new(publishBeams);
        Name = "UniqueTacticDirection"; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        cutin = new(); AddChild(cutin);
        cutin.Configure(repository, publication, createEffect, stopLinkedEffect, stopEffects, playSound);
        Resized += Fit; Fit();
    }

    private void Fit()
    {
        float scale = Math.Min(Size.X / 1920f, Size.Y / 1080f);
        cutin.Scale = new(scale, scale); cutin.Position = (Size - new Vector2(1920, 1080) * scale) * .5f;
    }

    internal void RequestSkip() => skip = true;

    internal void UpdateScene(double seconds, int logicalFramesPerSecond)
    {
        if (cancelled || Finished) return;
        double milliseconds = seconds * 1000;
        bool active;
        switch (Group)
        {
            case 0: active = cameraCommands[0].Update(map, skip); break;
            case 1: active = cameraCommands[1].Update(map, skip); break;
            case 2:
                active = cutin.UpdateCommand(seconds, logicalFramesPerSecond, skip);
                active |= voice.Update(milliseconds, skip);
                active |= sound.Update(milliseconds, skip);
                active |= linkedSound.Update(milliseconds, skip); break;
            case 3: active = cameraCommands[2].Update(map, skip); break;
            default:
                //Skip never reaches these two callbacks. They publish the
                //already calculated packet, and never reapply gameplay.
                active = results.Update(milliseconds, false);
                active |= beams.Update(milliseconds, false);
                if (!waitComplete)
                {
                    remainingWait = Math.Max(0, remainingWait - milliseconds);
                    if (remainingWait == 0) waitComplete = true;
                    active = true;
                }
                break;
        }
        if (!active) Group++;
        SetMeta("direction_group", Group);
    }

    internal void RestoreAndClose()
    {
        if (cancelled) return;
        cancelled = true; cutin.Cancel();
        if (skip || !Finished)
        {
            map.StopUniqueDirectionCamera();
            map.SubmitUniqueDirectionCamera(camera.Restore with { DurationMilliseconds = 0 });
        }
    }
}

using Godot;
using San14.Data;
using NVector4 = System.Numerics.Vector4;

public partial class MapSceneView
{
    private readonly MapCameraNavigation sourceNavigation = new();
    private MapCameraNavigationOptions sourceNavigationOptions = new();
    private uint sourceCameraHeldCommands;
    private bool sourceCameraControlHeld, sourceCameraShiftHeld, sourceCameraPointerOverSurface;
    private Window? sourceCameraInputWindow;

    internal MapCameraNavigationOptions SourceNavigationOptions => sourceNavigationOptions;

    internal void SetSourceNavigationOptions(MapCameraNavigationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        sourceNavigationOptions = options;
    }

    private void AttachSourceNavigationInput()
    {
        sourceCameraInputWindow = GetWindow();
        sourceCameraInputWindow.FocusExited += ResetSourceCameraInput;
        _surface.MouseEntered += () => sourceCameraPointerOverSurface = true;
        _surface.MouseExited += () => sourceCameraPointerOverSurface = false;
    }

    private void ReleaseSourceNavigationInput()
    {
        if (sourceCameraInputWindow is { } window && GodotObject.IsInstanceValid(window))
            window.FocusExited -= ResetSourceCameraInput;
        sourceCameraInputWindow = null;
    }

    private bool HandleSourceNavigationKey(InputEvent input)
    {
        if (!_embedded || _detailsOpen || !IsVisibleInTree() || _sourceMainCamera is null ||
            input is not InputEventKey key) return false;
        sourceCameraControlHeld = key.CtrlPressed;
        sourceCameraShiftHeld = key.ShiftPressed;
        // Fresh 31x2 source bindings use these physical key positions for the
        // eight held camera commands. Logical letters must not replace them on
        // a different keyboard layout. Other commands/binding edits are separate.
        int command = key.PhysicalKeycode switch
        { Key.W => 3, Key.S => 4, Key.A => 5, Key.D => 6, Key.C => 7, Key.V => 8, Key.Q => 9, Key.E => 10, _ => -1 };
        if (command < 0) return false;
        uint bit = 1u << command;
        if (key.Pressed) sourceCameraHeldCommands |= bit;
        else sourceCameraHeldCommands &= ~bit;
        return !sourceCameraControlHeld && !sourceCameraShiftHeld;
    }

    private void ApplySourceNavigationKeyboard(SceneViewSourceCameraInputs input)
    {
        if (_sourceMainCamera is not { } camera || sourceCameraControlHeld || sourceCameraShiftHeld ||
            sourceCameraHeldCommands == 0) return;
        float fps = (float)Engine.GetFramesPerSecond();
        if (!(fps > 0) || !float.IsFinite(fps)) return;
        float scale = 30f / fps;
        int horizontal = Held(5) - Held(6), vertical = Held(4) - Held(3);
        if (horizontal != 0 || vertical != 0)
        {
            int speed = sourceNavigation.Speed(camera.CurrentZoom.Band1E0, sourceNavigationOptions, keyboard: true);
            if (speed > 0)
                camera.ApplyImmediateRequest(MapCameraNavigation.Planar(camera.CurrentState,
                    (int)(speed * scale), horizontal, vertical), input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
        }
        int pitch = Held(7) - Held(8), yaw = Held(10) - Held(9);
        if (pitch != 0)
            camera.ApplyImmediateRequest(new(0, NVector4.Zero, -1, 62.83185308,
                pitch * (sourceNavigation.AngleHundredths / 100.0) * (double)scale + camera.CurrentState.PitchC0, 0),
                input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
        if (yaw != 0)
            camera.ApplyImmediateRequest(new(0, NVector4.Zero, -1,
                yaw * (sourceNavigation.AngleHundredths / 100.0) * (double)scale + camera.CurrentState.YawA8, -1, 0),
                input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);

        int Held(int command) => (int)(sourceCameraHeldCommands >> command & 1);
    }

    private void ApplySourceNavigationEdge(SceneViewSourceCameraInputs input)
    {
        if (_sourceMainCamera is not { } camera || !sourceCameraPointerOverSurface || !_sourceCameraCurrentCursorKnown ||
            sourceNavigationOptions.Edge == MapEdgeScrollSpeed.Disabled) return;
        var cursor = _sourceCameraCurrentCursor;
        var direction = sourceNavigation.EdgeDirection(cursor.X34, cursor.Y38, (int)_surface.Size.X, (int)_surface.Size.Y);
        if (direction == (0, 0)) return;
        int speed = sourceNavigation.Speed(camera.CurrentZoom.Band1E0, sourceNavigationOptions, keyboard: false);
        camera.ApplyImmediateRequest(MapCameraNavigation.Planar(camera.CurrentState, speed, direction.Horizontal, direction.Vertical),
            input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
    }
}

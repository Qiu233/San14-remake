using Godot;
using San14.Data;
using NVector4 = System.Numerics.Vector4;

public partial class MapSceneView
{
    private readonly MapCameraNavigation sourceNavigation = new();
    private MapCameraNavigationOptions sourceNavigationOptions = new();
    private uint sourceCameraHeldCommands;
    private bool sourceCameraControlHeld, sourceCameraShiftHeld;
    private Window? sourceCameraInputWindow;
    private IReadOnlyList<int> sourceKeyboardBindings = new int[KeyboardBindingCatalog.BindingCount];
    private readonly HashSet<int> sourceCameraPressedUsages = [];

    internal MapCameraNavigationOptions SourceNavigationOptions => sourceNavigationOptions;

    internal void SetKeyboardBindings(IReadOnlyList<int> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Count != KeyboardBindingCatalog.BindingCount)
            throw new ArgumentException("Map keyboard input requires the current 31 binding pairs.", nameof(bindings));
        sourceKeyboardBindings = bindings;
        ResetSourceNavigationKeyboard();
    }

    private void ResetSourceNavigationKeyboard()
    {
        sourceCameraPressedUsages.Clear();
        sourceCameraHeldCommands = 0;
        sourceCameraControlHeld = false; sourceCameraShiftHeld = false;
    }

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
    }

    private void ReleaseSourceNavigationInput()
    {
        if (sourceCameraInputWindow is { } window && GodotObject.IsInstanceValid(window))
            window.FocusExited -= ResetSourceCameraInput;
        sourceCameraInputWindow = null;
    }

    private bool HandleSourceNavigationKey(InputEvent input)
    {
        if (!_embedded || _detailsOpen || sourceCameraInputSuspended || !IsVisibleInTree() || _sourceMainCamera is null ||
            input is not InputEventKey key) return false;
        if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
        { ResetSourceNavigationKeyboard(); return false; }
        uint previous = sourceCameraHeldCommands;
        sourceCameraControlHeld = key.CtrlPressed;
        sourceCameraShiftHeld = key.ShiftPressed;
        int usage = KeyboardPhysicalInput.Hid(key.PhysicalKeycode);
        if (usage != 0)
        {
            if (key.Pressed) sourceCameraPressedUsages.Add(usage);
            else sourceCameraPressedUsages.Remove(usage);
        }
        int modifiers = (key.ShiftPressed ? KeyboardBindingCatalog.ShiftModifier : 0) |
            (key.CtrlPressed ? KeyboardBindingCatalog.ControlModifier : 0);
        sourceCameraHeldCommands = 0;
        bool eventMatchesHeldBinding = false;
        // Only the fixed eight camera actions are revisited after an actual
        // keyboard event. The frame consumes the resulting mask directly.
        for (int command = 3; command <= 10; command++)
            for (int slot = 0; slot < 2; slot++)
            {
                int binding = sourceKeyboardBindings[command * 2 + slot];
                if (binding != 0 && (binding & 0x3000) == modifiers)
                {
                    if ((binding & 0xFFF) == usage) eventMatchesHeldBinding = true;
                    if (sourceCameraPressedUsages.Contains(binding & 0xFFF))
                        sourceCameraHeldCommands |= 1u << command;
                }
            }
        bool consumed = previous != sourceCameraHeldCommands || eventMatchesHeldBinding;
        int encoded = usage | modifiers;
        if (key.Pressed && !key.Echo && usage != 0)
        {
            if (Matches(1, encoded)) { Run(() => ZoomSourceCameraByKeyboard(-1)); return true; }
            if (Matches(2, encoded)) { Run(() => ZoomSourceCameraByKeyboard(1)); return true; }
        }
        return consumed || (usage != 0 && (Matches(1, encoded) || Matches(2, encoded)));

        bool Matches(int command, int encoded) =>
            sourceKeyboardBindings[command * 2] == encoded || sourceKeyboardBindings[command * 2 + 1] == encoded;
    }

    private void ApplySourceNavigationKeyboard(SceneViewSourceCameraInputs input)
    {
        if (_sourceMainCamera is not { } camera || sourceCameraHeldCommands == 0) return;
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

    // 171C0 -> A8A0 -> 13A00: a key press selects the adjacent rounded
    // zoom band, with current distance limits and the saved band pitch. It
    // does not use the wheel's cursor compensation or manufacture a mouse event.
    private void ZoomSourceCameraByKeyboard(int direction)
    {
        if (_sourceMainCamera is not { } camera || _sourceMainCameraInputs is not { } input) return;
        var runtime = camera.CurrentRuntime;
        int rounded = (int)MathF.Round(runtime.Selection.Band1E0 + runtime.Selection.Fraction1E4 / 10000f,
            MidpointRounding.AwayFromZero);
        int minimum = MapCameraController.ClassifyZoom(camera.Parameters, runtime.Bounds.MinimumDistance98).Band1E0;
        int maximum = MapCameraController.ClassifyZoom(camera.Parameters, runtime.Bounds.MaximumDistanceA0).Band1E0;
        int target = Math.Clamp(rounded + direction, minimum, maximum);
        if (target == rounded) return;
        if (target < 2 && _sourceMainCameraTerrain is null) { SetSourceCameraStatus("pending_terrain"); return; }
        double pitch = runtime.BandPitch.TryGetValue(target, out var retained) ? retained : camera.Parameters.ZoomBandPitch(target);
        var request = new MainMapCameraControllerRequest(500, NVector4.Zero,
            camera.Parameters.ZoomAnchor(target), 62.83185308, pitch, 1);
        var prepared = MapCameraController.MaterializeRequest(runtime, camera.Parameters, request,
            input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
        camera.QueueTimedRequest(prepared, unchecked((int)Time.GetTicksMsec()));
    }

    internal bool ToggleKeyboardMapOverlay(int command)
    {
        if (command != 18 || !UseD3D11Map || _detailsOpen) return false;
        SetTerritoryDisplay(!_d3d11TerritoryDisplay);
        return true;
    }

    private void ApplySourceNavigationEdge(SceneViewSourceCameraInputs input)
    {
        if (_sourceMainCamera is not { } camera || !MapPointerOverSurface() || !_sourceCameraCurrentCursorKnown ||
            sourceNavigationOptions.Edge == MapEdgeScrollSpeed.Disabled) return;
        var cursor = _sourceCameraCurrentCursor;
        var direction = sourceNavigation.EdgeDirection(cursor.X34, cursor.Y38, (int)_surface.Size.X, (int)_surface.Size.Y);
        if (direction == (0, 0)) return;
        int speed = sourceNavigation.Speed(camera.CurrentZoom.Band1E0, sourceNavigationOptions, keyboard: false);
        camera.ApplyImmediateRequest(MapCameraNavigation.Planar(camera.CurrentState, speed, direction.Horizontal, direction.Vertical),
            input.CurrentMaximumYOverride, SourceCameraTerrainQualifier);
    }
}

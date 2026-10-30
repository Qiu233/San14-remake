using System.Numerics;

namespace San14.Data;

/// <summary>The title-owned CStartupCameraCtrl installed at MapMain+280.
/// 17BD0 sends its shared15B50/15A80 V20 to the existing Main camera; source
/// geometry, optics and renderer frame types remain the ordinary owners.</summary>
public sealed class StartupMapCameraAdapter
{
    private StartupMapCameraState current;
    private readonly MainMapCameraFocus focus;
    private float retainedFov12E8;
    private float retainedFar14;
    private bool titleFarApplied,forceSelectionEntered;

    private StartupMapCameraAdapter(LoadedMapCameraConstructionSource source,
        MainMapCameraControllerParameters parameters, string provenance)
    {
        Source = source; Parameters = parameters;
        current = StartupMapCameraController.ConstructTitleMap();
        focus = new(null, null, null, null, new(32400, 0, 144400, 1), provenance)
            { Origin = MainMapCameraFocusOrigin.ControllerDefault };
        retainedFov12E8 = source.Declaration.Perspective.Lens.VerticalFovRadians;
        retainedFar14 = source.Declaration.Perspective.Lens.Far;
    }

    public LoadedMapCameraConstructionSource Source { get; }
    public MainMapCameraControllerParameters Parameters { get; }
    public StartupMapCameraState CurrentRuntime => current;
    public MainMapCameraControllerState CurrentState => current.Pose;
    public float CurrentFov12E8 => retainedFov12E8;
    public float CurrentFar14 => retainedFar14;
    /// <summary>4E8850 writes28C=4. 17BD0 copies this override to mode288;
    /// it is neither the camera distance nor the label zoom band.</summary>
    public int AppModeOverride28C => 4;

    public static StartupMapCameraAdapter FromTitleMap(GameResourceRepository repository,
        string locale, string ownedWorldProvenance, MainMapCameraEntryInputs entryInputs)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedWorldProvenance);
        var declarations = DefaultView0Source.LoadDeclarationDefaults(repository);
        var source = MapCameraConstructionSource.ReadPerspective(declarations.CameraDefinitions,
            declarations.MainCameraObjectId);
        var result = new StartupMapCameraAdapter(source,
            MainMapCameraControllerParameters.LoadSourceDefaults(repository, locale), ownedWorldProvenance);
        result.ApplyTitleEntryFar(entryInputs);
        return result;
    }

    /// <summary>4F9910/156900 writes CPU14=300000 only after its actual
    /// MainData, Host210 and MainCPU gates. This does not assert GPU readiness.</summary>
    public bool ApplyTitleEntryFar(MainMapCameraEntryInputs inputs)
    {
        if (!inputs.MainDataPresent || !inputs.HostGate210Present || !inputs.MainCpuPresent) return false;
        retainedFar14 = 300000; titleFarApplied = true; return true;
    }

    public void EnterForceSelection(int sourceClock)
    {
        current = StartupMapCameraController.EnterForceSelection(current, sourceClock);
        forceSelectionEntered=true;
    }

    public void Advance(int sourceClock) => current = StartupMapCameraController.Tick(current, sourceClock);

    public MainMapCameraFrame AdvanceAndPrepareCurrent(MainMapCameraFrameInputs input, int sourceClock)
    { Advance(sourceClock); return PrepareCurrent(input); }

    public MainMapCameraFrame PrepareCurrent(MainMapCameraFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.FrameNumber < 1 || string.IsNullOrWhiteSpace(input.FrameProvenance))
            throw new InvalidDataException("Startup camera publication requires an explicit frame and provenance.");
        var pose = MapCameraController.Publish(current.Pose, input.CurrentScale90);
        string provenance = $"ce-startup-CB60;{(forceSelectionEntered?"force-entry-11A70":"title-constructor-11F40")};active-MapMain280;{Source.ConstructorOrigin};" +
            $"owned-world={focus.OwnedWorldProvenance};far-writer={(titleFarApplied ? "4F9910/156900" : "retained-source")};" +
            $"frame={input.FrameProvenance}";
        var camera = MapCameraConstruction.PrepareFreshPerspective(Source, pose,
            input.ContextOwner3E4Bit29, input.NativeClipProfile, input.TangentDispatch,
            Parameters.Locale, input.FrameNumber, provenance);
        if (input.SourceSelectedFovV410 is float selected) retainedFov12E8 = selected;
        bool overrideApplied = input.OwnerFovOverride290 > 0;
        float fov = MapCameraController.ApplyPositiveFovOverride(retainedFov12E8, input.OwnerFovOverride290);
        float far = pose.Scale90.Z * retainedFar14;
        if (BitConverter.SingleToUInt32Bits(camera.Lens.VerticalFovRadians) != BitConverter.SingleToUInt32Bits(fov) ||
            BitConverter.SingleToUInt32Bits(camera.Lens.Far) != BitConverter.SingleToUInt32Bits(far))
        {
            var lens = camera.Lens with { VerticalFovRadians = fov, Far = far };
            var geometry = camera.Geometry.WithLens(lens, input.NativeClipProfile, input.TangentDispatch);
            camera = new(camera.Prefix.ToArray(), Source, geometry, Parameters.Locale,
                input.FrameNumber, provenance + ";same-source-V418-retained-lens", optics: camera.Optics);
        }
        retainedFov12E8 = fov;
        // ECB0 uses active controller Eye+24 for an override owner. Source
        // EAB0's seven numeric anchors are shared; no Main runtime clamp runs.
        float eyeY = MapCameraController.DerivePose(current.Pose).Eye10.Y;
        var zoom = MapCameraController.ClassifyZoom(Parameters, eyeY);
        return new(camera, current.Pose, zoom, focus, overrideApplied);
    }
}

using System.Buffers.Binary;
using System.Numerics;
using San14.Core;

namespace San14.Data;

/// <summary>Owned source inputs consumed at the Main controller's publication
/// boundary. Scale, renderer flags, clip profile and the optional FOV override
/// are retained current values, separate from wheel or drag state.</summary>
public sealed record OriginalMainMapCameraFrameInputs(Vector3 CurrentScale90,
    bool ContextOwner3E4Bit29, uint NativeClipProfile,
    OriginalNativeCameraTangentDispatch TangentDispatch, float OwnerFovOverride290,
    ulong ImageBase, long FrameNumber, string FrameProvenance)
{
    /// <summary>This update's source V468/V410 store, before the later App290
    /// override. Null retains CPU12E8 without inventing a seasonal write.</summary>
    public float? SourceSelectedFovV410 { get; init; }
}

public sealed record OriginalMainMapCameraFrame(OriginalMapCameraConstructionPacket Camera,
    OriginalMainMapCameraControllerState ControllerState,
    OriginalMainMapCameraZoomSelection ZoomSelection, OriginalMainMapCameraFocus InitialFocus,
    bool PositiveFovOverrideApplied)
{
    /// <summary>Capture territory inputs from this same immutable frame's
    /// primary Main state, retaining all current map display controls.</summary>
    public OriginalTerritoryStyleSelection CaptureTerritorySelection(OriginalTerritoryStyleSelection retainedMapControls) =>
        SelectTerritory(ControllerState, ZoomSelection, retainedMapControls);

    public OriginalMainMapCameraCursorRay CursorRay(int canonicalPixelX, int canonicalPixelY) =>
        OriginalMainMapCameraController.CursorRay(Camera.Prefix.Span, canonicalPixelX, canonicalPixelY);
    public OriginalMainMapCameraEyeForward EyeForward =>
        OriginalMainMapCameraController.EyeForward(Camera.CurrentPose);

    internal static OriginalTerritoryStyleSelection SelectTerritory(OriginalMainMapCameraControllerState state,
        OriginalMainMapCameraZoomSelection selection, OriginalTerritoryStyleSelection retainedMapControls)
    {
        ArgumentNullException.ThrowIfNull(retainedMapControls);
        var selected = retainedMapControls with { ZoomBand = selection.Band1E0,
            InterpolationPercent = selection.Fraction1E4, ZoomValue = state.Distance90 };
        _ = OriginalTerritoryStyleSources.CacheZoomValue(selected);
        return selected;
    }
}

/// <summary>A concrete source-created Main38D camera owner. CTitleState's
/// immediate ruler affiliation focus initializes its primary controller; the
/// verified V20 publisher supplies Camera.V70/V68 and V418 preparation.
/// The HDRI setter cameras3/4 have distinct owners.</summary>
public sealed class OriginalMainMapCameraAdapter
{
    public const uint MainCameraObjectId = 0x38D04576;
    public const string ContractId = "ce-20261002-main-map-camera-owned-adapter";
    private OriginalMainMapCameraControllerRuntimeState runtime;
    private float retainedFov12E8;
    private float retainedFar14;
    private string? farWriter;

    private OriginalMainMapCameraAdapter(LoadedOriginalMapCameraConstructionSource source,
        OriginalMainMapCameraControllerParameters parameters, OriginalMainMapCameraFocus focus,
        bool currentMaximumYOverride)
    {
        Source = source;
        Parameters = parameters;
        InitialFocus = focus;
        retainedFov12E8 = BitConverter.UInt32BitsToSingle(source.CpuWord(0x12E8));
        retainedFar14 = BitConverter.UInt32BitsToSingle(source.CpuWord(0x14));
        var state = OriginalMainMapCameraController.InitialFocus(parameters,
            new(focus.RequestedWorldFocus.X, focus.RequestedWorldFocus.Y, focus.RequestedWorldFocus.Z),
            currentMaximumYOverride);
        runtime = OriginalMainMapCameraController.CreateRuntime(parameters, state, currentMaximumYOverride);
    }

    public LoadedOriginalMapCameraConstructionSource Source { get; }
    public OriginalMainMapCameraControllerParameters Parameters { get; }
    public OriginalMainMapCameraFocus InitialFocus { get; }
    public OriginalMainMapCameraControllerState CurrentState => runtime.Pose;
    public OriginalMainMapCameraControllerRuntimeState CurrentRuntime => runtime;
    public float CurrentFov12E8 => retainedFov12E8;
    public float CurrentFar14 => retainedFar14;
    public OriginalMainMapCameraZoomSelection CurrentZoom => runtime.Selection;

    public static OriginalMainMapCameraAdapter FromOwnedCampaign(OriginalGameRepository repository,
        string locale, GameState currentOwnedWorld, int currentNativePlayerForceSlot,
        string ownedWorldProvenance, bool currentMaximumYOverride)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return FromSources(OriginalDefaultView0Source.LoadDeclarationDefaults(repository).MainCamera,
            OriginalMainMapCameraControllerParameters.LoadSourceDefaults(repository, locale),
            currentOwnedWorld, currentNativePlayerForceSlot, ownedWorldProvenance, currentMaximumYOverride);
    }

    public static OriginalMainMapCameraAdapter FromSources(LoadedOriginalMapCameraConstructionSource source,
        OriginalMainMapCameraControllerParameters parameters, GameState currentOwnedWorld,
        int currentNativePlayerForceSlot, string ownedWorldProvenance, bool currentMaximumYOverride)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parameters);
        if (source.Camera.ObjectId != MainCameraObjectId)
            throw new InvalidDataException("The Main map camera owner requires exact source38D04576.");
        OriginalMainMapCameraFocus focus = OriginalMainMapCameraFocusSource.ResolveCurrentRuler(
            currentOwnedWorld, currentNativePlayerForceSlot, ownedWorldProvenance);
        return new(source, parameters, focus, currentMaximumYOverride);
    }

    /// <summary>Apply the source Title exit setter once at the owned map entry
    /// boundary. Missing native-role inputs retain the previous source far.
    /// Later publication uses this field in the same geometry/depth matrices.</summary>
    public OriginalMainMapCameraEntryFarWrite ApplyTitleExitFar(
        LoadedOriginalMainMapCameraEntrySource source,OriginalMainMapCameraEntryInputs current)
    {
        ArgumentNullException.ThrowIfNull(source);
        var write=source.ApplyTitleExitFar(current,retainedFar14);
        if(write.Applied){retainedFar14=write.SourceFar14;farWriter=source.SourceEvidence;}
        return write;
    }

    /// <summary>175E0 orbit writer followed by the actual immediate request
    /// consumer. A rejected pose retains the earlier native metadata writes.</summary>
    public bool ApplyOrbitDrag(int signedDeltaX44, int signedDeltaY48, bool currentMaximumYOverride,
        Func<OriginalMainMapCameraTerrainPitchInput, OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier = null)
    {
        var request = OriginalMainMapCameraController.OrbitRequest(runtime.Pose, signedDeltaX44, signedDeltaY48);
        var result = OriginalMainMapCameraController.ApplyImmediate(runtime, Parameters, request,
            currentMaximumYOverride, lowBandTerrainQualifier);
        runtime = result.State;
        return result.PoseCommitted;
    }

    /// <summary>Apply an owned source immediate request through142A0/AB70.
    /// Retain the entire returned runtime, including stores before rejection.</summary>
    public bool ApplyImmediateRequest(OriginalMainMapCameraControllerRequest request,
        bool currentMaximumYOverride,
        Func<OriginalMainMapCameraTerrainPitchInput,OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier=null)
    {
        var result=OriginalMainMapCameraController.ApplyImmediate(runtime,Parameters,request,
            currentMaximumYOverride,lowBandTerrainQualifier);
        runtime=result.State;
        return result.PoseCommitted;
    }

    /// <summary>175E0 pan writer consumes its two current source Camera rays,
    /// applies the native request, then retains the source input velocity sample.</summary>
    public bool ApplyPanDrag(Vector4 sharedSecondRayOrigin, Vector4 displacedRayDirection,
        Vector4 centerRayDirection, bool sourceSampleOnePredicate, int sourceClock,
        bool currentMaximumYOverride,
        Func<OriginalMainMapCameraTerrainPitchInput, OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier = null)
    {
        var pan = OriginalMainMapCameraController.PanRequest(runtime.Pose, sharedSecondRayOrigin,
            displacedRayDirection, centerRayDirection);
        var result = OriginalMainMapCameraController.ApplyImmediate(runtime, Parameters, pan.Request,
            currentMaximumYOverride, lowBandTerrainQualifier);
        runtime = OriginalMainMapCameraController.SavePanVelocity(result.State, pan, sourceSampleOnePredicate, sourceClock);
        return result.PoseCommitted;
    }

    /// <summary>The source wheel writer includes cursor compensation and A960
    /// target materialization before queuing its 500 ms request.</summary>
    public bool QueueWheelZoom(int signedWheelDelta, OriginalMainMapCameraCursorRay currentCursorRay,
        OriginalMainMapCameraEyeForward currentSourceCamera, int sourceClock, bool currentMaximumYOverride,
        Func<OriginalMainMapCameraTerrainPitchInput, OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier = null)
    {
        if (runtime.RequestMode188 != 0) return false;
        var request = OriginalMainMapCameraController.BuildWheelRequest(runtime, Parameters,
            signedWheelDelta, currentCursorRay, currentSourceCamera, currentMaximumYOverride, lowBandTerrainQualifier);
        if (request is null) return false;
        runtime = OriginalMainMapCameraController.QueuePreparedTimedRequest(runtime, request.Value, sourceClock);
        return true;
    }

    /// <summary>142A0's positive-duration route to13E00. Raw source commands
    /// such as north retain their unchanged-target sentinels; wheel callers
    /// separately materialize their target through A960 before this route.</summary>
    public bool QueueTimedRequest(OriginalMainMapCameraControllerRequest request, int sourceClock)
    {
        if (runtime.RequestMode188 != 0) return false;
        runtime = OriginalMainMapCameraController.QueuePreparedTimedRequest(runtime, request, sourceClock);
        return true;
    }

    /// <summary>Consume EFB0/E790 results from the same owned Main camera
    /// frame. Cursor coordinates retain the source's canonical1920×1080 domain.</summary>
    public bool QueueWheelZoomFromFrame(OriginalMainMapCameraFrame currentFrame, int signedWheelDelta,
        int canonicalPixelX, int canonicalPixelY, int sourceClock, bool currentMaximumYOverride,
        Func<OriginalMainMapCameraTerrainPitchInput, OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier = null)
    {
        ValidateCurrentFrame(currentFrame);
        var ray = currentFrame.CursorRay(canonicalPixelX, canonicalPixelY);
        if (!ray.SourceReady) return false;
        return QueueWheelZoom(signedWheelDelta, ray, currentFrame.EyeForward, sourceClock,
            currentMaximumYOverride, lowBandTerrainQualifier);
    }

    /// <summary>Use the proved client scaling and letterbox writer before
    /// consuming the same current source Camera frame.</summary>
    public bool QueueWheelZoomFromClientFrame(OriginalMainMapCameraFrame currentFrame, int signedWheelDelta,
        OriginalMainMapCameraClientPoint clientPoint, int effectiveClientWidth, int effectiveClientHeight,
        int currentTargetWidth, int currentTargetHeight, int sourceClock, bool currentMaximumYOverride,
        Func<OriginalMainMapCameraTerrainPitchInput, OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier = null)
    {
        var canonical = OriginalMainMapCameraController.CanonicalCursor(clientPoint,
            effectiveClientWidth, effectiveClientHeight, currentTargetWidth, currentTargetHeight);
        return QueueWheelZoomFromFrame(currentFrame, signedWheelDelta, canonical.X34, canonical.Y38,
            sourceClock, currentMaximumYOverride, lowBandTerrainQualifier);
    }

    /// <summary>175E0 uses rays at(960-dx,540-dy) and(960,540), sharing
    /// the second origin. Signed deltas retain native32-bit subtraction.</summary>
    public bool ApplyPanDragFromFrame(OriginalMainMapCameraFrame currentFrame, int signedDeltaX44,
        int signedDeltaY48, bool sourceSampleOnePredicate, int sourceClock, bool currentMaximumYOverride,
        Func<OriginalMainMapCameraTerrainPitchInput, OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier = null)
    {
        ValidateCurrentFrame(currentFrame);
        var first = currentFrame.CursorRay(unchecked(960 - signedDeltaX44), unchecked(540 - signedDeltaY48));
        var second = currentFrame.CursorRay(960, 540);
        if (!first.SourceReady || !second.SourceReady) return false;
        return ApplyPanDrag(second.Origin, first.Direction, second.Direction, sourceSampleOnePredicate,
            sourceClock, currentMaximumYOverride, lowBandTerrainQualifier);
    }

    /// <summary>Native input samples the difference of two canonical points
    /// after projection. Disabled input or zero motion submits no pan request.</summary>
    public bool ApplyPanDragFromCanonicalPoints(OriginalMainMapCameraFrame currentFrame,
        OriginalMainMapCameraCanonicalPoint currentPoint, OriginalMainMapCameraCanonicalPoint previousPoint,
        bool inputEnabled64, bool sourceSampleOnePredicate, int sourceClock, bool currentMaximumYOverride,
        Func<OriginalMainMapCameraTerrainPitchInput, OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier = null)
    {
        var delta = OriginalMainMapCameraController.CanonicalDelta(currentPoint, previousPoint, inputEnabled64);
        if (delta.X44 == 0 && delta.Y48 == 0) return false;
        return ApplyPanDragFromFrame(currentFrame, delta.X44, delta.Y48, sourceSampleOnePredicate,
            sourceClock, currentMaximumYOverride, lowBandTerrainQualifier);
    }

    private void ValidateCurrentFrame(OriginalMainMapCameraFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!ReferenceEquals(frame.Camera.Source, Source) || frame.Camera.ExecutableLocale != Parameters.Locale)
            throw new InvalidDataException("Camera input rays require the same owned Main38D source and executable profile.");
    }

    /// <summary>Run selected state V18 before V20 publication. Clock samples,
    /// inertia timeout and current source Y override remain explicit.</summary>
    public void Tick(int inertiaClock, int animationClock, int currentInertiaTimeoutMilliseconds,
        bool currentMaximumYOverride,
        Func<OriginalMainMapCameraTerrainPitchInput, OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier = null)
    {
        runtime = OriginalMainMapCameraController.Tick(runtime, Parameters, inertiaClock, animationClock,
            currentInertiaTimeoutMilliseconds, currentMaximumYOverride, lowBandTerrainQualifier);
    }

    /// <summary>18C506/TC18C526 passes the primary singleton state+50 to the
    /// territory writer. Its raw1E4 fraction is copied without percent rounding.</summary>
    public OriginalTerritoryStyleSelection CaptureTerritorySelection(OriginalTerritoryStyleSelection retainedMapControls)
        => OriginalMainMapCameraFrame.SelectTerritory(runtime.Pose, runtime.Selection, retainedMapControls);

    /// <summary>Run the source V18 tick before V20 and publish one coherent
    /// current camera/controller/zoom frame for rendering and territory input.</summary>
    public OriginalMainMapCameraFrame AdvanceAndPrepareCurrent(OriginalMainMapCameraFrameInputs input,
        int inertiaClock, int animationClock, int currentInertiaTimeoutMilliseconds, bool currentMaximumYOverride,
        Func<OriginalMainMapCameraTerrainPitchInput, OriginalMainMapCameraTerrainPitchResult>? lowBandTerrainQualifier = null)
    {
        Tick(inertiaClock, animationClock, currentInertiaTimeoutMilliseconds,
            currentMaximumYOverride, lowBandTerrainQualifier);
        return PrepareCurrent(input);
    }

    public OriginalMainMapCameraFrame PrepareCurrent(OriginalMainMapCameraFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.FrameNumber < 0 || string.IsNullOrWhiteSpace(input.FrameProvenance))
            throw new InvalidDataException("Main camera publication requires an explicit frame number and provenance.");
        OriginalMapCameraCurrentPose pose = OriginalMainMapCameraController.Publish(runtime.Pose, input.CurrentScale90);
        string provenance = $"{ContractId};{Source.ConstructorOrigin};" +
            $"controller={Parameters.Locale}@{Parameters.RuleVersion};" +
            $"ruler-focus={InitialFocus.PlayerForcePhysicalSlot}/{InitialFocus.RulerPersonSourceId}/" +
            $"{InitialFocus.AffiliationLocationId}/{InitialFocus.PhysicalHexId};" +
            $"owned-world={InitialFocus.OwnedWorldProvenance};frame={input.FrameProvenance}"+
            (farWriter is null?"":";map-entry-far="+farWriter);
        OriginalMapCameraConstructionPacket camera = OriginalMapCameraConstruction.PrepareFreshPerspective(
            Source, pose, input.ContextOwner3E4Bit29, input.NativeClipProfile, input.TangentDispatch,
            input.ImageBase, Parameters.Locale, input.FrameNumber, provenance);

        // V468 may call V410 and overwrite sourceCPU12E8. The current source
        // producer supplies that conditional store before geometry is captured.
        if(input.SourceSelectedFovV410 is float selectedFov)retainedFov12E8=selectedFov;
        // 17BD0 applies owner+290 after the first state V20/V418 publication,
        // writes sourceCPU12E8, then invokes V418 again in the same update.
        bool applied = input.OwnerFovOverride290 > 0f;
        float fov = OriginalMainMapCameraController.ApplyPositiveFovOverride(
            retainedFov12E8, input.OwnerFovOverride290);
        // The source CPU14 is retained in source units; C40A80 publishes
        // Scale90.Z * CPU14 to the prepared lens144, as for the near plane.
        float preparedFar=pose.Scale90.Z*retainedFar14;
        if (applied || BitConverter.SingleToUInt32Bits(fov) !=
            BitConverter.SingleToUInt32Bits(camera.Lens.VerticalFovRadians) ||
            BitConverter.SingleToUInt32Bits(preparedFar)!=BitConverter.SingleToUInt32Bits(camera.Lens.Far))
        {
            byte[] prefix = camera.Prefix.ToArray();
            BinaryPrimitives.WriteSingleLittleEndian(prefix.AsSpan(0x148, 4), fov);
            BinaryPrimitives.WriteSingleLittleEndian(prefix.AsSpan(0x144, 4),preparedFar);
            var lens = camera.Lens with { VerticalFovRadians = fov,Far=preparedFar };
            var matrices = OriginalMapCameraConstructionMath.CompleteAB6C60(
                ReadVector(prefix, 0x10), ReadVector(prefix, 0x20), ReadVector(prefix, 0x30),
                lens, input.NativeClipProfile, input.TangentDispatch);
            matrices.WriteEarlyMatrices(prefix);
            prefix[0x160] = prefix[0x161] = 0;
            camera = new(prefix, Source, pose, lens, matrices, input.ImageBase, Parameters.Locale,
                input.FrameNumber, provenance + (applied
                    ? ";same-update-positive-owner290-fov" : input.SourceSelectedFovV410 is not null
                        ? ";same-update-source-V468-V410-fov" : ";retained-sourceCPU12E8"));
        }
        retainedFov12E8 = fov;
        return new(camera, runtime.Pose, CurrentZoom, InitialFocus, applied);
    }

    private static Vector4 ReadVector(ReadOnlySpan<byte> prefix, int offset) => new(
        BinaryPrimitives.ReadSingleLittleEndian(prefix.Slice(offset, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(prefix.Slice(offset + 4, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(prefix.Slice(offset + 8, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(prefix.Slice(offset + 12, 4)));
}

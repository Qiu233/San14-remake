using System.Runtime.InteropServices;
using Godot;
using San14.Data;
using San14.Core;

internal enum D3D11ArmyPublicationRole { Campaign = 0, StartupAbsent = 1 }

/// <summary>One owned camera publication and the elapsed time supplied by its caller.</summary>
public sealed class D3D11MapInput
{
    private readonly byte[] _camera;
    private readonly double[] _controller;
    public ReadOnlyMemory<byte> Camera => _camera;
    public ReadOnlyMemory<double> Controller => _controller;
    public int Band { get; }
    public int Fraction { get; }
    public double ElapsedSeconds { get; }
    public bool Paused { get; }
    public bool EligibleAnimationUpdate { get; }
    public bool TerritoryDisplayEnabled { get; init; } = true;
    // Captured App override; negative selects this capture's automatic zoom mode.
    internal int MainModeOverride { get; init; } = -1;
    // Source 186340: ownership0/frontline1/neither2. Null keeps old callers.
    internal int? InformationTerritoryStyle { get; init; }
    internal MapInformationLayerSnapshot? InformationLayers { get; init; }
    public MainMapCameraFrame? SourceFrame { get; private init; }
    public TerrainPicker? TerrainPicker { get; init; }
    public D3D11TerritoryPublication? TerritoryOwnership { get; init; }
    public D3D11CommandMapPublication? CommandMap { get; init; }
    // Independent first-focus scalar: null retains, the native invalid slot clears.
    internal uint? HexFocus { get; init; }
    public D3D11RoutePublication? Routes { get; init; }
    public G1EffectGpuPublication? Effects { get; init; }
    public StoryEffectCommonCameraFrame? StoryEffectCamera { get; init; }
    public uint CommandMapFpsMode { get; init; }
    //Absence belongs to the real Startup role, not an invented empty World.
    internal D3D11ArmyPublicationRole ArmyPublicationRole { get; init; } = D3D11ArmyPublicationRole.Campaign;
    public D3D11ArmyPublication? ArmyWorld { get; init; }
    public ArmyMapGroupingFrame? ArmyGroupingWorld { get; init; }
    public D3D11CityGatePublication? CityGateWorld { get; init; }
    public D3D11AreaCapitalPublication? AreaCapitalWorld { get; init; }
    public GameState? WorldState { get; init; }
    internal NativeMapSeasonState? PeriodSeason { get; init; }
    internal D3D11ArmyLabelPublication? PeriodArmyLabels { get; init; }
    internal FacilityMapInfoPublication? FacilityInfo { get; init; }
    internal HexFirePresentationPublication? HexFire { get; init; }
    internal SceneryMapPresentation? SceneryPresentation {get;init;}
    internal SceneryMapCapture? SceneryWorld {get;init;}
    internal bool? SceneryCameraStable {get;init;}
    public D3D11CurrentSeasonFrame? CurrentSeason { get; private init; }
    internal byte[]? CurrentSeasonBytes { get; private init; }
    internal string? AtmosphereResourceDirectory { get; private init; }
    internal string? PatternResourceDirectory { get; private init; }

    public D3D11MapInput(ReadOnlySpan<byte> camera, ReadOnlySpan<double> controller,
        int band, int fraction, double elapsedSeconds, bool paused = false,
        bool eligibleAnimationUpdate = false, MainMapCameraFrame? sourceFrame = null)
    {
        if (camera.Length != 960 || controller.Length != 7)
            throw new ArgumentException("Native Main requires a 960-byte camera and seven controller doubles.");
        ValidateElapsed(elapsedSeconds);
        _camera = camera.ToArray(); _controller = controller.ToArray();
        Band = band; Fraction = fraction; ElapsedSeconds = elapsedSeconds;
        Paused = paused; EligibleAnimationUpdate = eligibleAnimationUpdate; SourceFrame = sourceFrame;
    }

    private D3D11MapInput(D3D11MapInput input, double elapsedSeconds)
    {
        _camera = input._camera; _controller = input._controller;
        Band = input.Band; Fraction = input.Fraction; ElapsedSeconds = elapsedSeconds;
        Paused = input.Paused; EligibleAnimationUpdate = input.EligibleAnimationUpdate;
        SourceFrame = input.SourceFrame;
        MainModeOverride = input.MainModeOverride;
        TerrainPicker = input.TerrainPicker;
        TerritoryOwnership = input.TerritoryOwnership;
        CommandMap = input.CommandMap; CommandMapFpsMode = input.CommandMapFpsMode;
        HexFocus = input.HexFocus;
        Routes = input.Routes;
        Effects = input.Effects;
        StoryEffectCamera = input.StoryEffectCamera;
        ArmyPublicationRole = input.ArmyPublicationRole;
        ArmyWorld = input.ArmyWorld;
        ArmyGroupingWorld = input.ArmyGroupingWorld;
        CityGateWorld = input.CityGateWorld;
        AreaCapitalWorld = input.AreaCapitalWorld;
        WorldState = input.WorldState;
        PeriodSeason = input.PeriodSeason; PeriodArmyLabels = input.PeriodArmyLabels;
        FacilityInfo = input.FacilityInfo;
        HexFire = input.HexFire;
        SceneryPresentation=input.SceneryPresentation;SceneryWorld=input.SceneryWorld;
        SceneryCameraStable=input.SceneryCameraStable;
        CurrentSeason = input.CurrentSeason; CurrentSeasonBytes = input.CurrentSeasonBytes;
        AtmosphereResourceDirectory = input.AtmosphereResourceDirectory;
        PatternResourceDirectory = input.PatternResourceDirectory;
        TerritoryDisplayEnabled = input.TerritoryDisplayEnabled;
        InformationTerritoryStyle = input.InformationTerritoryStyle;
        InformationLayers = input.InformationLayers;
    }

    public static D3D11MapInput FromCamera(MainMapCameraFrame frame,
        double elapsedSeconds, bool paused = false, bool eligibleAnimationUpdate = false)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var state = frame.ControllerState;
        return new(frame.Camera.Prefix.Span,
            [state.FocusX, state.FocusY, state.FocusZ, state.FocusW, state.Distance90, state.YawA8, state.PitchC0],
            frame.ZoomSelection.Band1E0, frame.ZoomSelection.Fraction1E4, elapsedSeconds,
            paused, eligibleAnimationUpdate, frame);
    }

    internal D3D11MapInput WithElapsed(double elapsedSeconds) => new(this, elapsedSeconds);
    internal D3D11MapInput WithRoutes(D3D11RoutePublication? routes) => new(this, ElapsedSeconds) { Routes = routes };
    public D3D11MapInput WithEffects(G1EffectGpuPublication? effects) => new(this, ElapsedSeconds) { Effects = effects };
    internal D3D11MapInput WithSceneryWorld(SceneryMapCapture? frame)=>new(this,ElapsedSeconds){SceneryWorld=frame};
    internal D3D11MapInput WithResourceContextGeneration(ulong generation) => new(this, ElapsedSeconds)
        { ArmyWorld = this.ArmyWorld?.WithResourceContextGeneration(generation) };
    internal D3D11MapInput WithCurrentSeason(D3D11CurrentSeasonFrame frame,
        D3D11CurrentHdrCatalog hdr, string? atmosphereResourceDirectory = null,
        ulong? resourceContextGeneration = null, string? patternResourceDirectory = null) => new(this, ElapsedSeconds)
        { SourceFrame = frame.Mode.AcceptedMainFrame, CurrentSeason = frame, MainModeOverride = frame.Mode.Override28C,
            CurrentSeasonBytes = D3D11CurrentSeasonEncoding.WriteNative(frame, hdr),
            AtmosphereResourceDirectory = atmosphereResourceDirectory,
            PatternResourceDirectory = patternResourceDirectory,
            ArmyWorld = resourceContextGeneration is { } generation
                ? this.ArmyWorld?.WithResourceContextGeneration(generation) : this.ArmyWorld };
    internal D3D11RenderSession.NativeInput ToNative()
    {
        // Native flags20..22:0 keeps automatic classification;1..5 carry
        // this immutable capture's actual Main override0..4, including events
        // that precede a draw's independent full CurrentSeason preparation.
        uint mainModeFlags = MainModeOverride < 0 ? 0u : MainModeOverride <= 4
            ? ((uint)MainModeOverride + 1u) << 20
            : throw new InvalidDataException("Native Main override exceeds its prepared0..4 profile.");
        uint armyRoleFlags = ArmyPublicationRole switch
        {
            D3D11ArmyPublicationRole.Campaign => 0u,
            D3D11ArmyPublicationRole.StartupAbsent when ArmyWorld is null => 0x80000u,
            D3D11ArmyPublicationRole.StartupAbsent => throw new InvalidDataException("Startup cannot publish a live Army World."),
            _ => throw new InvalidDataException("Native Army publication role is unsupported.")
        };
        var sun = CurrentSeason?.Atmosphere.Controller.SunPosition ?? default;
        return new()
        {
            Camera = _camera, Controller = _controller, Band = Band, Fraction = Fraction,
            DeltaSeconds = (float)ElapsedSeconds,
            Flags = 1u | (Paused ? 2u : 0u) | (EligibleAnimationUpdate ? 4u : 0u) |
                (TerritoryDisplayEnabled ? 0u : 8u) | armyRoleFlags | mainModeFlags |
                (InformationTerritoryStyle is int style ? 0x40000u | (uint)style << 16 : 0u),
            CurrentRawSunPosition = [sun.X, sun.Y, sun.Z, sun.W],
            CurrentCloudGeometry40 = CurrentSeason?.Atmosphere.ControllerGeometry40 ?? 0,
            AtmosphereResourceDirectory = AtmosphereResourceDirectory is { } atmosphere
                ? D3D11RenderSession.NativeFileSystemPath(atmosphere) : null,
            PatternResourceDirectory = PatternResourceDirectory is { } pattern
                ? D3D11RenderSession.NativeFileSystemPath(pattern) : null
        };
    }
    private static void ValidateElapsed(double value)
    {
        if (value < 0 || !double.IsFinite(value) || !float.IsFinite((float)value))
            throw new ArgumentOutOfRangeException(nameof(value), "Renderer elapsed time must be finite and nonnegative.");
    }
}

public readonly record struct D3D11MapReceipt(ulong Request, ReadOnlyMemory<byte> Camera)
{
    public D3D11AnimationState Animation {get;init;}
    public IReadOnlyList<D3D11ArmyTargetReactionReceipt> TargetReactions {get;init;} = [];
    public IReadOnlyList<D3D11ArmyViewRetirementReceipt> ViewRetirements {get;init;} = [];
    public IReadOnlyList<D3D11ArmyLabelUnitPosition> ArmyUnitPositions {get;init;} = [];
}
public enum D3D11ArmyViewRetirementStatus:uint { None=0,Accepted=1,MissingOwner=2,KnownNoView=3 }
[StructLayout(LayoutKind.Sequential)]
public struct D3D11ArmyViewRetirementReceipt
{
    public ulong RequestId;
    public int ArmyId;
    public D3D11ArmyViewRetirementStatus Status;
}
public enum D3D11ArmyTargetReactionStatus:uint { None=0,Accepted=1,MissingOwner=2,KnownNoView=3 }
[StructLayout(LayoutKind.Sequential)]
public struct D3D11ArmyTargetReactionReceipt
{
    public ulong RequestId;
    public int ArmyId,Variant;
    public D3D11ArmyTargetReactionStatus Status;
}
[StructLayout(LayoutKind.Sequential)]
public struct D3D11AnimationState
{
    public ulong Updates;
    public uint ArmyPlayers,CityGroups,AreaGroups,FpsMode;
    public float FirstBodyPhase,FirstFlagPhase;
}

/// <summary>Owns one NativeApi.h session and DLL reference. The default consumer
/// uses Godot's render thread. A surface can explicitly own serialized native
/// jobs; direct-device operations retain their existing gate. Godot RID work
/// remains with the caller. The explicit source directory is an
/// imported source resources. Actual territory/Army publications
/// are copied synchronously and retained independently of camera requests.</summary>
public sealed partial class D3D11RenderSession : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeInput
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 960)] public byte[] Camera;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)] public double[] Controller;
        public int Band, Fraction;
        public float DeltaSeconds;
        public uint Flags;
        public IntPtr OwnershipColors;
        public ulong OwnershipRevision;
        public uint OwnershipCount;
        public IntPtr OwnershipMapColors;
        public uint OwnershipMapColorCount;
        public IntPtr ArmyWorld;
        public uint ArmyWorldBytes;
        public IntPtr CurrentSeason;
        public uint CurrentSeasonBytes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public float[] CurrentRawSunPosition;
        public float CurrentCloudGeometry40;
        public IntPtr CityGateWorld;
        public uint CityGateWorldBytes;
        public IntPtr AreaCapitalWorld;
        public uint AreaCapitalWorldBytes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? AtmosphereResourceDirectory;
        [MarshalAs(UnmanagedType.LPWStr)] public string? PatternResourceDirectory;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeCommandMapInputV1
    {
        public uint Size, Version;
        public ulong Revision, DataRevision, ProviderGeneration;
        public uint Present, HoverHex, FpsMode, Count;
        public IntPtr Membership, RawAreas;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeCommandMapInputV2
    {
        public uint Size, Version;
        public ulong Revision, DataRevision, ProviderGeneration;
        public D3D11CommandMapDisplayKind DisplayKind;
        public uint HoverHex, FpsMode, Count;
        public IntPtr Membership, RawAreas;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NativeRouteInputV1
    {
        public uint Size, Version;
        public ulong Revision, ResourceGeneration;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ResourceDirectory;
        public IntPtr Vertices, Spans, Endpoints;
        public uint VertexCount, SpanCount, EndpointCount, UpdateKind;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeReceipt
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 960)] public byte[] Camera;
        public ulong Request;
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate IntPtr CreateCall(ulong device, ulong queue, ulong output,
        [MarshalAs(UnmanagedType.LPWStr)] string source, int width, int height, uint populationMask,
        in NativeInput input, out NativeReceipt receipt);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NextCall(IntPtr owner, ulong output, in NativeInput input,
        uint outputUse, out NativeReceipt receipt);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CommandMapNextCall(IntPtr owner, ulong output, in NativeInput input,
        in NativeCommandMapInputV1 command, uint outputUse, out NativeReceipt receipt);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CandidateMapNextCall(IntPtr owner, ulong output, in NativeInput input,
        in NativeCommandMapInputV2 command, uint outputUse, out NativeReceipt receipt);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate int MapOverlaysNextCall(IntPtr owner, ulong output, in NativeInput input,
        IntPtr command, in NativeRouteInputV1 routes, uint outputUse, out NativeReceipt receipt);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int AnimationCall(IntPtr owner,in NativeInput input,uint fpsMode);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AnimationStateCall(IntPtr owner,out D3D11AnimationState state);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TargetReactionReceiptCall(IntPtr owner,
        [Out, MarshalAs(UnmanagedType.LPArray,SizeParamIndex=2)] D3D11ArmyTargetReactionReceipt[] receipts,uint capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ViewRetirementReceiptCall(IntPtr owner,
        [Out, MarshalAs(UnmanagedType.LPArray,SizeParamIndex=2)] D3D11ArmyViewRetirementReceipt[] receipts,uint capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ArmyUnitPositionsCall(IntPtr owner,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=2)] D3D11ArmyLabelUnitPosition[] positions,
        uint capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DestroyCall(IntPtr owner);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ErrorCall();
    private IntPtr _module, _owner;
    private readonly CreateCall _create;
    private readonly NextCall _next;
    private CommandMapNextCall? _commandMapNext;
    private CandidateMapNextCall? _candidateMapNext;
    private MapOverlaysNextCall? _mapOverlaysNext;
    private readonly AnimationCall _animation;
    private readonly AnimationStateCall _animationState;
    private readonly TargetReactionReceiptCall _targetReactionReceipt;
    private readonly ViewRetirementReceiptCall _viewRetirementReceipt;
    private readonly ArmyUnitPositionsCall _armyUnitPositions;
    private readonly DestroyCall _destroy;
    private readonly ErrorCall _error;
    private readonly string _sourceDirectory;
    private readonly uint _populationMask;
    private readonly D3D11TerrainPreparedPrograms? _terrainPrograms;
    private D3D11TerritoryPublication? _sentTerritory;
    private float[]? _sentMapColors;
    private D3D11ArmyPublication? _sentArmy;
    private D3D11CityGatePublication? _sentCityGate;
    private D3D11AreaCapitalPublication? _sentAreaCapital;
    private D3D11CommandMapData? _sentCommandMapData;
    private D3D11CommandMapPublication? _sentCommandMap;
    private uint _sentCommandMapFpsMode;
    private D3D11RoutePublication? _sentRoutes;
    private ulong _routeRequestRevision;
    private ulong _sentRouteResourceGeneration;
    private string? _sentRouteResourceDirectory;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OutputProtocolCall(IntPtr owner);
    private OutputProtocolCall? _drainSubmittedOutput;
    private readonly bool _nativeWorker;
    private int _nativeJobThreadId;

    public D3D11RenderSession(string sourceDirectory, string nativeDLLPath, uint populationMask)
        : this(sourceDirectory,nativeDLLPath,populationMask,nativeWorker:false) { }

    internal D3D11RenderSession(string sourceDirectory,string nativeDLLPath,uint populationMask,bool nativeWorker,
        OutputQueueBridge? outputQueueBridge=null,D3D11TerrainPreparedPrograms? terrainPrograms=null)
    {
        if(terrainPrograms is not null&&outputQueueBridge is null)
            throw new ArgumentException("Resident Landscape programs require their actual application output bridge.",nameof(terrainPrograms));
        _terrainPrograms=terrainPrograms;
        if(outputQueueBridge is not null&&!nativeWorker)
            throw new ArgumentException("An independent output queue belongs to the native worker.",nameof(outputQueueBridge));
        _outputQueueBridge=outputQueueBridge;
        _nativeWorker=nativeWorker;
        if(!nativeWorker)RequireRenderThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeDLLPath);
        _sourceDirectory = NativeFileSystemPath(sourceDirectory); _populationMask = populationMask;
        _module = NativeLibrary.Load(NativeFileSystemPath(nativeDLLPath));
        try
        {
            _create = Export<CreateCall>("san14_render_create");
            _next = Export<NextCall>("san14_render_next");
            _animation=Export<AnimationCall>("san14_render_animation_event");
            _animationState=Export<AnimationStateCall>("san14_render_animation_state");
            _targetReactionReceipt=Export<TargetReactionReceiptCall>("san14_render_target_reaction_receipt_v1");
            _viewRetirementReceipt=Export<ViewRetirementReceiptCall>("san14_render_army_view_retirement_receipt_v1");
            _armyUnitPositions=Export<ArmyUnitPositionsCall>("san14_render_army_unit_positions");
            _destroy = Export<DestroyCall>("san14_render_destroy");
            _error = Export<ErrorCall>("san14_render_error");
        }
        catch { NativeLibrary.Free(_module); _module = IntPtr.Zero; throw; }
    }

    public D3D11MapReceipt Create(ulong device, ulong queue, ulong output,
        D3D11MapInput input, int width = 1920, int height = 1080)
    {
        RequireSubmissionThread(); ObjectDisposedException.ThrowIf(_module == IntPtr.Zero, this);
        if (_owner != IntPtr.Zero) throw new InvalidOperationException("Native Main is already open.");
        // The command page is admitted only after the real map is ready.
        // Keep the established initial-create ABI and its first publication.
        if (input.CommandMap is not null) throw new InvalidOperationException("Command map display requires an initialized map.");
        if (input.Routes is not null) throw new InvalidOperationException("Route display requires an initialized map.");
        if (input.HexFocus is not null) throw new InvalidOperationException("Hex focus requires an initialized map.");
        if (input.Effects is not null) throw new InvalidOperationException("Effect display requires an initialized map.");
        var native = input.ToNative();
        var pin = PinWorld(input, ref native);
        NativeReceipt receipt;
        try
        {
            if(_outputQueueBridge is { } bridge)
            {
                if(_terrainPrograms is { } programs)
                {
                    _createResidentMapWorker??=Export<CreateResidentMapWorkerCall>("san14_render_create_map_worker_v2");
                    _owner=_createResidentMapWorker(bridge.Handle,output,_sourceDirectory,width,height,
                        _populationMask,programs.LogicalKey,programs.Identity,in native,out receipt);
                }
                else
                {
                    _createMapWorker??=Export<CreateMapWorkerCall>("san14_render_create_map_worker_v1");
                    _owner=_createMapWorker(bridge.Handle,output,_sourceDirectory,width,height,
                        _populationMask,in native,out receipt);
                }
            }
            else _owner = _create(device, queue, output, _sourceDirectory, width, height,
                _populationMask, in native, out receipt);
        }
        finally { pin.Dispose(); }
        if (_owner == IntPtr.Zero) throw NativeFailure();
        // The bridge factory selects its independent queue before creating
        // On12 and enables the two-output protocol before its first copy.
        // Existing non-bridge workers retain their established opt-in.
        if (_nativeWorker && _outputQueueBridge is null &&
            Export<OutputProtocolCall>("san14_render_enable_double_buffered_output_v1")(_owner) != 0)
            throw NativeFailure();
        _sentTerritory = input.TerritoryOwnership ?? _sentTerritory;
        _sentMapColors = input.TerritoryOwnership?.SourceMapColors ?? _sentMapColors;
        _sentArmy = input.ArmyWorld ?? _sentArmy;
        _sentCityGate = input.CityGateWorld ?? _sentCityGate;
        _sentAreaCapital = input.AreaCapitalWorld ?? _sentAreaCapital;
        _animationState(_owner,out var animation);
        return new(receipt.Request, receipt.Camera){Animation=animation,ArmyUnitPositions=ReadArmyUnitPositions(),TargetReactions=ReadTargetReactionReceipts(),ViewRetirements=ReadViewRetirementReceipts()};
    }

    /// <summary>The output's last submitted Godot use must be Canvas sampling.
    /// Native restores SRV after its GPU copy; no clear or readback is performed.</summary>
    public D3D11MapReceipt Next(ulong output, D3D11MapInput input)
        => Next(output, input, outputHasCanvasSampling: true);

    // A new bounded output still has the actual RD clear/render-target use.
    // After its first real Canvas submission, later requests restore SRV.
    public D3D11MapReceipt Next(ulong output, D3D11MapInput input, bool outputHasCanvasSampling)
        => RenderRequest(output,input,outputHasCanvasSampling,prepareSceneOnly:false);

    internal D3D11MapReceipt PrepareScene(D3D11MapInput input)
        => RenderRequest(0,input,false,prepareSceneOnly:true);

    private D3D11MapReceipt RenderRequest(ulong output,D3D11MapInput input,
        bool outputHasCanvasSampling,bool prepareSceneOnly)
    {
        uint outputUse = outputHasCanvasSampling ? 1u : 0u;
        RequireSubmissionThread(); ObjectDisposedException.ThrowIf(_module == IntPtr.Zero, this);
        if (_owner == IntPtr.Zero) throw new InvalidOperationException("Native Main has not been created.");
        var native = input.ToNative();
        var pin = PinWorld(input, ref native);
        NativeReceipt receipt; int result;
        try
        {
            if (prepareSceneOnly)
                result = PrepareSceneNative(input,in native,ref pin,out receipt);
            else if (input.HexFocus is not null)
                result = RenderSceneExtensions(input, in native, ref pin, false, output, outputUse, out receipt);
            else if (input.Effects is { } effects)
                result = NextWithEffects(output, input, in native, effects, ref pin, outputUse, out receipt);
            else if (input.Routes is { } routes)
            {
                _mapOverlaysNext ??= Export<MapOverlaysNextCall>("san14_render_next_map_overlays_v1");
                var routeInput = PinRoutes(routes, ref pin);
                IntPtr commandPointer = IntPtr.Zero;
                if (input.CommandMap is { } routeCommand &&
                    (!ReferenceEquals(routeCommand, _sentCommandMap) || input.CommandMapFpsMode != _sentCommandMapFpsMode))
                {
                    var commandInput = PinCommandMap(routeCommand, input.CommandMapFpsMode, ref pin);
                    pin.RouteCommand = GCHandle.Alloc(new[] { commandInput }, GCHandleType.Pinned);
                    commandPointer = pin.RouteCommand.AddrOfPinnedObject();
                }
                result = _mapOverlaysNext(_owner, output, in native, commandPointer, in routeInput, outputUse, out receipt);
            }
            else if (input.CommandMap is { } command)
            {
                uint count = 0;
                IntPtr membership = IntPtr.Zero, areas = IntPtr.Zero;
                if (command.Data is { } data && NeedsCommandMapData(command))
                {
                    count = D3D11CommandMapData.CellCount;
                    if(data.HasMembership)
                    {
                        pin.CommandMembership = GCHandle.Alloc(data.Membership, GCHandleType.Pinned);
                        membership = pin.CommandMembership.AddrOfPinnedObject();
                    }
                    if (data.HasFocusGroups)
                    {
                        pin.CommandAreas = GCHandle.Alloc(data.RawAreas, GCHandleType.Pinned);
                        areas = pin.CommandAreas.AddrOfPinnedObject();
                    }
                }
                // Both extensions remain 64 bytes. Original administration
                // uses V1 unchanged; candidate entry AND close use explicit V2.
                if (command.UsesCandidateProtocol)
                {
                    _candidateMapNext ??= Export<CandidateMapNextCall>("san14_render_next_command_map_v2");
                    var extension = new NativeCommandMapInputV2 {
                        Size = (uint)Marshal.SizeOf<NativeCommandMapInputV2>(), Version = 2,
                        Revision = command.Revision, DataRevision = command.DataRevision,
                        ProviderGeneration = command.ProviderGeneration, DisplayKind = command.DisplayKind,
                        HoverHex = command.HoverHex, FpsMode = input.CommandMapFpsMode,
                        Count = count, Membership = membership, RawAreas = areas
                    };
                    result = _candidateMapNext(_owner, output, in native, in extension, outputUse, out receipt);
                }
                else
                {
                    _commandMapNext ??= Export<CommandMapNextCall>("san14_render_next_command_map_v1");
                    var extension = new NativeCommandMapInputV1 {
                        Size = (uint)Marshal.SizeOf<NativeCommandMapInputV1>(), Version = 1,
                        Revision = command.Revision, DataRevision = command.DataRevision,
                        ProviderGeneration = command.ProviderGeneration, Present = command.Present ? 1u : 0u,
                        HoverHex = command.HoverHex, FpsMode = input.CommandMapFpsMode,
                        Count = count, Membership = membership, RawAreas = areas
                    };
                    result = _commandMapNext(_owner, output, in native, in extension, outputUse, out receipt);
                }
            }
            else result = _next(_owner, output, in native, outputUse, out receipt);
        }
        finally { pin.Dispose(); }
        if (result != 0) throw NativeFailure();
        if (input.Effects is { } acceptedEffects) _sentEffectResources = acceptedEffects.Resources;
        if (input.CommandMap is { } acceptedCommand)
        {
            _sentCommandMapData = acceptedCommand.Data;
            _sentCommandMap = acceptedCommand;
            _sentCommandMapFpsMode = input.CommandMapFpsMode;
        }
        _sentRoutes = input.Routes ?? _sentRoutes;
        if (input.Routes is { IsEmpty: false } acceptedRoutes)
        {
            _sentRouteResourceGeneration = acceptedRoutes.ResourceGeneration;
            _sentRouteResourceDirectory = acceptedRoutes.ResourceDirectory;
        }
        _sentTerritory = input.TerritoryOwnership ?? _sentTerritory;
        _sentMapColors = input.TerritoryOwnership?.SourceMapColors ?? _sentMapColors;
        _sentArmy = input.ArmyWorld ?? _sentArmy;
        _sentCityGate = input.CityGateWorld ?? _sentCityGate;
        _sentAreaCapital = input.AreaCapitalWorld ?? _sentAreaCapital;
        _animationState(_owner,out var animation);
        return new(receipt.Request, receipt.Camera){Animation=animation,ArmyUnitPositions=ReadArmyUnitPositions(),TargetReactions=ReadTargetReactionReceipts(),ViewRetirements=ReadViewRetirementReceipts()};
    }

    public void UpdateAnimations(D3D11MapInput input,uint fpsMode)
    {
        RequireSubmissionThread();ObjectDisposedException.ThrowIf(_module==IntPtr.Zero,this);
        var native=input.ToNative();var pin=PinWorld(input,ref native);int result;
        try{result=_animation(_owner,in native,fpsMode);}finally{pin.Dispose();}
        if(result!=0)throw NativeFailure();
        _sentTerritory=input.TerritoryOwnership??_sentTerritory;
        _sentMapColors=input.TerritoryOwnership?.SourceMapColors??_sentMapColors;
        _sentArmy=input.ArmyWorld??_sentArmy;
        _sentCityGate=input.CityGateWorld??_sentCityGate;
        _sentAreaCapital=input.AreaCapitalWorld??_sentAreaCapital;
    }

    internal void DrainSubmittedOutputWork()
    {
        RequireSubmissionThread();ObjectDisposedException.ThrowIf(_module==IntPtr.Zero,this);
        _drainSubmittedOutput ??= Export<OutputProtocolCall>("san14_render_drain_submitted_output_v1");
        if (_drainSubmittedOutput(_owner)!=0) throw NativeFailure();
    }

    public void Dispose()
    {
        lock (_directGate)
        {
        if (_module == IntPtr.Zero) return;
        RequireSubmissionThread();
        // Native Owner drains queued work before retiring its coroutine and On12 owners.
        if (_owner != IntPtr.Zero) { _destroy(_owner); _owner = IntPtr.Zero; }
        NativeLibrary.Free(_module); _module = IntPtr.Zero;
        }
    }
    private D3D11ArmyViewRetirementReceipt[] ReadViewRetirementReceipts()
    {
        var receipts=new D3D11ArmyViewRetirementReceipt[_sentArmy?.ViewRetirements.Count??0];
        int count=_viewRetirementReceipt(_owner,receipts,(uint)receipts.Length);
        if(count<0)throw NativeFailure();
        if(count!=receipts.Length)Array.Resize(ref receipts,count);
        return receipts;
    }
    private D3D11ArmyTargetReactionReceipt[] ReadTargetReactionReceipts()
    {
        var receipts=new D3D11ArmyTargetReactionReceipt[_sentArmy?.TargetReactions.Count??0];
        int count=_targetReactionReceipt(_owner,receipts,(uint)receipts.Length);
        if(count<0)throw NativeFailure();
        if(count!=receipts.Length)Array.Resize(ref receipts,count);
        return receipts;
    }
    private D3D11ArmyLabelUnitPosition[] ReadArmyUnitPositions()
    {
        var positions=new D3D11ArmyLabelUnitPosition[_sentArmy?.UnitCenters.Count??0];
        int count=_armyUnitPositions(_owner,positions,(uint)positions.Length);
        if(count<0)throw NativeFailure();
        if(count!=positions.Length)Array.Resize(ref positions,count);
        return positions;
    }
    private T Export<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_module, name));
    private struct WorldPins : IDisposable
    {
        internal GCHandle Colors, Palette, Army, Season, CityGate, AreaCapital, CommandMembership, CommandAreas;
        internal GCHandle RouteVertices, RouteSpans, RouteEndpoints, RouteCommand;
        public void Dispose()
        {
            if (Colors.IsAllocated) Colors.Free();
            if (Palette.IsAllocated) Palette.Free();
            if (Army.IsAllocated) Army.Free();
            if (Season.IsAllocated) Season.Free();
            if (CityGate.IsAllocated) CityGate.Free();
            if (AreaCapital.IsAllocated) AreaCapital.Free();
            if (CommandMembership.IsAllocated) CommandMembership.Free();
            if (CommandAreas.IsAllocated) CommandAreas.Free();
            if (RouteVertices.IsAllocated) RouteVertices.Free();
            if (RouteSpans.IsAllocated) RouteSpans.Free();
            if (RouteEndpoints.IsAllocated) RouteEndpoints.Free();
            if (RouteCommand.IsAllocated) RouteCommand.Free();
        }
    }
    // Pending camera requests may merge a close/reopen before submission.
    // The same retained arrays then have a new binding revision, which the
    // native receiver must receive before it can accept that publication.
    private bool NeedsCommandMapData(D3D11CommandMapPublication command)
        => !ReferenceEquals(command.Data, _sentCommandMapData) || _sentCommandMap is null ||
            command.DataRevision != _sentCommandMap.DataRevision || command.DisplayKind != _sentCommandMap.DisplayKind;

    private NativeCommandMapInputV2 PinCommandMap(D3D11CommandMapPublication command, uint fpsMode, ref WorldPins pin)
    {
        var extension = new NativeCommandMapInputV2 {
            Size = (uint)Marshal.SizeOf<NativeCommandMapInputV2>(), Version = 2,
            Revision = command.Revision, DataRevision = command.DataRevision,
            ProviderGeneration = command.ProviderGeneration, DisplayKind = command.DisplayKind,
            HoverHex = command.HoverHex, FpsMode = fpsMode
        };
        if (command.Data is { } data && NeedsCommandMapData(command))
        {
            extension.Count = D3D11CommandMapData.CellCount;
            if(data.HasMembership)
            {
                pin.CommandMembership = GCHandle.Alloc(data.Membership, GCHandleType.Pinned);
                extension.Membership = pin.CommandMembership.AddrOfPinnedObject();
            }
            if (data.HasFocusGroups)
            {
                pin.CommandAreas = GCHandle.Alloc(data.RawAreas, GCHandleType.Pinned);
                extension.RawAreas = pin.CommandAreas.AddrOfPinnedObject();
            }
        }
        return extension;
    }
    private NativeRouteInputV1 PinRoutes(D3D11RoutePublication routes, ref WorldPins pin)
    {
        var extension = new NativeRouteInputV1 {
            Size = (uint)Marshal.SizeOf<NativeRouteInputV1>(), Version = 1,
            ResourceGeneration = routes.ResourceGeneration
        };
        if (ReferenceEquals(routes, _sentRoutes)) return extension;
        // Geometry captures can be merged or prepared after a later page
        // close. Number actual replacements on this serialized render owner.
        extension.Revision = ++_routeRequestRevision;
        extension.UpdateKind = routes.IsEmpty ? 2u : 1u;
        if (routes.IsEmpty) return extension;
        if (routes.ResourceGeneration != _sentRouteResourceGeneration ||
            routes.ResourceDirectory != _sentRouteResourceDirectory)
            extension.ResourceDirectory = NativeFileSystemPath(routes.ResourceDirectory);
        extension.VertexCount = checked((uint)routes.Vertices.Length);
        extension.SpanCount = checked((uint)routes.Spans.Length);
        extension.EndpointCount = checked((uint)routes.Endpoints.Length);
        if (routes.Vertices.Length != 0)
        {
            pin.RouteVertices = GCHandle.Alloc(routes.Vertices, GCHandleType.Pinned);
            extension.Vertices = pin.RouteVertices.AddrOfPinnedObject();
        }
        if (routes.Spans.Length != 0)
        {
            pin.RouteSpans = GCHandle.Alloc(routes.Spans, GCHandleType.Pinned);
            extension.Spans = pin.RouteSpans.AddrOfPinnedObject();
        }
        if (routes.Endpoints.Length != 0)
        {
            pin.RouteEndpoints = GCHandle.Alloc(routes.Endpoints, GCHandleType.Pinned);
            extension.Endpoints = pin.RouteEndpoints.AddrOfPinnedObject();
        }
        return extension;
    }
    private WorldPins PinWorld(D3D11MapInput input, ref NativeInput native)
    {
        var publication = input.TerritoryOwnership;
        WorldPins pin = default;
        try
        {
            if (publication is not null && !ReferenceEquals(publication, _sentTerritory))
            {
                pin.Colors = GCHandle.Alloc(publication.NativeColorIndices, GCHandleType.Pinned);
                native.OwnershipColors = pin.Colors.AddrOfPinnedObject(); native.OwnershipCount = D3D11TerritoryPublisher.CellCount;
                native.OwnershipRevision = publication.Revision;
                if (!ReferenceEquals(publication.SourceMapColors, _sentMapColors))
                {
                    pin.Palette = GCHandle.Alloc(publication.SourceMapColors, GCHandleType.Pinned);
                    native.OwnershipMapColors = pin.Palette.AddrOfPinnedObject(); native.OwnershipMapColorCount = 60;
                }
            }
            if (input.ArmyWorld is { } army && !ReferenceEquals(army, _sentArmy))
            {
                pin.Army = GCHandle.Alloc(army.NativeBytes, GCHandleType.Pinned);
                native.ArmyWorld = pin.Army.AddrOfPinnedObject(); native.ArmyWorldBytes = (uint)army.NativeBytes.Length;
            }
            if (input.CurrentSeasonBytes is { } season)
            {
                pin.Season = GCHandle.Alloc(season, GCHandleType.Pinned);
                native.CurrentSeason = pin.Season.AddrOfPinnedObject(); native.CurrentSeasonBytes = (uint)season.Length;
            }
            if (input.CityGateWorld is { } cityGate && !ReferenceEquals(cityGate.NativeBytes, _sentCityGate?.NativeBytes))
            {
                pin.CityGate = GCHandle.Alloc(cityGate.NativeBytes, GCHandleType.Pinned);
                native.CityGateWorld = pin.CityGate.AddrOfPinnedObject(); native.CityGateWorldBytes = (uint)cityGate.NativeBytes.Length;
            }
            if (input.AreaCapitalWorld is { } capital && !ReferenceEquals(capital, _sentAreaCapital))
            {
                pin.AreaCapital = GCHandle.Alloc(capital.NativeBytes, GCHandleType.Pinned);
                native.AreaCapitalWorld = pin.AreaCapital.AddrOfPinnedObject(); native.AreaCapitalWorldBytes = (uint)capital.NativeBytes.Length;
            }
            return pin;
        }
        catch { pin.Dispose(); throw; }
    }
    private Exception NativeFailure() => new InvalidOperationException(
        Marshal.PtrToStringUTF8(_error()) ?? "Native Main failed.");
    // Native CRT readers do not inherit .NET's automatic long-path handling.
    // Normalize once at each actual directory/library handoff; native child
    // paths retain the same extended root for this session's whole lifetime.
    internal static string NativeFileSystemPath(string path)
    {
        string absolute = Path.GetFullPath(path);
        if (!OperatingSystem.IsWindows() || absolute.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            absolute.StartsWith(@"\\.\", StringComparison.Ordinal)) return absolute;
        return absolute.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + absolute[2..] : @"\\?\" + absolute;
    }

    // Opt-in only for the surface's serialized native-only job. Existing render
    // thread consumers and StoryEffect retirement keep their original guard.
    internal T RunNativeJob<T>(Func<T> action)
    {
        if(!_nativeWorker)throw new InvalidOperationException("This session belongs to Godot's render thread.");
        int thread=System.Environment.CurrentManagedThreadId;
        if(Interlocked.CompareExchange(ref _nativeJobThreadId,thread,0)!=0)
            throw new InvalidOperationException("A native surface job is already active.");
        try { return action(); }
        finally { Volatile.Write(ref _nativeJobThreadId,0); }
    }
    private void RequireSubmissionThread()
    {
        if(!_nativeWorker){RequireRenderThread();return;}
        if(Volatile.Read(ref _nativeJobThreadId)!=System.Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("Native Main submission requires its active surface job.");
    }
    private static void RequireRenderThread()
    {
        if (!RenderingServer.IsOnRenderThread())
            throw new InvalidOperationException("Native Main ownership belongs to Godot's render thread.");
    }
}

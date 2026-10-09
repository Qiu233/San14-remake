using System.Runtime.InteropServices;
using Godot;
using San14.Data;
using San14.Core;

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
    public MainMapCameraFrame? SourceFrame { get; private init; }
    public TerrainPicker? TerrainPicker { get; init; }
    public D3D11TerritoryPublication? TerritoryOwnership { get; init; }
    public D3D11ArmyPublication? ArmyWorld { get; init; }
    public ArmyMapGroupingFrame? ArmyGroupingWorld { get; init; }
    public D3D11CityGatePublication? CityGateWorld { get; init; }
    public D3D11AreaCapitalPublication? AreaCapitalWorld { get; init; }
    public GameState? WorldState { get; init; }
    public D3D11CurrentSeasonFrame? CurrentSeason { get; private init; }
    internal byte[]? CurrentSeasonBytes { get; private init; }

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
        TerrainPicker = input.TerrainPicker;
        TerritoryOwnership = input.TerritoryOwnership;
        ArmyWorld = input.ArmyWorld;
        ArmyGroupingWorld = input.ArmyGroupingWorld;
        CityGateWorld = input.CityGateWorld;
        AreaCapitalWorld = input.AreaCapitalWorld;
        WorldState = input.WorldState;
        CurrentSeason = input.CurrentSeason; CurrentSeasonBytes = input.CurrentSeasonBytes;
        TerritoryDisplayEnabled = input.TerritoryDisplayEnabled;
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
    internal D3D11MapInput WithCurrentSeason(D3D11CurrentSeasonFrame frame,
        D3D11CurrentHdrCatalog hdr) => new(this, ElapsedSeconds)
        { SourceFrame = frame.Mode.AcceptedMainFrame, CurrentSeason = frame, CurrentSeasonBytes = frame.ToNativeBytes(hdr) };
    internal D3D11RenderSession.NativeInput ToNative()
    {
        var sun = CurrentSeason?.Atmosphere.Controller.SunPosition ?? default;
        return new()
        {
            Camera = _camera, Controller = _controller, Band = Band, Fraction = Fraction,
            DeltaSeconds = (float)ElapsedSeconds,
            Flags = 1u | (Paused ? 2u : 0u) | (EligibleAnimationUpdate ? 4u : 0u) |
                (TerritoryDisplayEnabled ? 0u : 8u),
            CurrentRawSunPosition = [sun.X, sun.Y, sun.Z, sun.W],
            CurrentCloudGeometry40 = CurrentSeason?.Atmosphere.ControllerGeometry40 ?? 0
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
    public IReadOnlyList<D3D11ArmyLabelUnitPosition> ArmyUnitPositions {get;init;} = [];
}
[StructLayout(LayoutKind.Sequential)]
public struct D3D11AnimationState
{
    public ulong Updates;
    public uint ArmyPlayers,CityGroups,AreaGroups,FpsMode;
    public float FirstBodyPhase,FirstFlagPhase;
}

/// <summary>Owns one NativeApi.h session and DLL reference. All methods, including
/// Dispose, run on Godot's render thread. The explicit source directory is an
/// imported source resources. Actual territory/Army publications
/// are copied synchronously and retained independently of camera requests.</summary>
public sealed class D3D11RenderSession : IDisposable
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
    private delegate int AnimationCall(IntPtr owner,in NativeInput input,uint fpsMode);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AnimationStateCall(IntPtr owner,out D3D11AnimationState state);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ArmyUnitPositionsCall(IntPtr owner,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=2)] D3D11ArmyLabelUnitPosition[] positions,
        uint capacity);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DestroyCall(IntPtr owner);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ErrorCall();
    private IntPtr _module, _owner;
    private readonly CreateCall _create;
    private readonly NextCall _next;
    private readonly AnimationCall _animation;
    private readonly AnimationStateCall _animationState;
    private readonly ArmyUnitPositionsCall _armyUnitPositions;
    private readonly DestroyCall _destroy;
    private readonly ErrorCall _error;
    private readonly string _sourceDirectory;
    private readonly uint _populationMask;
    private D3D11TerritoryPublication? _sentTerritory;
    private float[]? _sentMapColors;
    private D3D11ArmyPublication? _sentArmy;
    private D3D11CityGatePublication? _sentCityGate;
    private D3D11AreaCapitalPublication? _sentAreaCapital;

    public D3D11RenderSession(string sourceDirectory, string nativeDLLPath, uint populationMask)
    {
        RequireRenderThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeDLLPath);
        _sourceDirectory = Path.GetFullPath(sourceDirectory); _populationMask = populationMask;
        _module = NativeLibrary.Load(Path.GetFullPath(nativeDLLPath));
        try
        {
            _create = Export<CreateCall>("san14_render_create");
            _next = Export<NextCall>("san14_render_next");
            _animation=Export<AnimationCall>("san14_render_animation_event");
            _animationState=Export<AnimationStateCall>("san14_render_animation_state");
            _armyUnitPositions=Export<ArmyUnitPositionsCall>("san14_render_army_unit_positions");
            _destroy = Export<DestroyCall>("san14_render_destroy");
            _error = Export<ErrorCall>("san14_render_error");
        }
        catch { NativeLibrary.Free(_module); _module = IntPtr.Zero; throw; }
    }

    public D3D11MapReceipt Create(ulong device, ulong queue, ulong output,
        D3D11MapInput input, int width = 1920, int height = 1080)
    {
        RequireRenderThread(); ObjectDisposedException.ThrowIf(_module == IntPtr.Zero, this);
        if (_owner != IntPtr.Zero) throw new InvalidOperationException("Native Main is already open.");
        var native = input.ToNative();
        var pin = PinWorld(input, ref native);
        NativeReceipt receipt;
        try
        {
            _owner = _create(device, queue, output, _sourceDirectory, width, height,
            _populationMask, in native, out receipt);
        }
        finally { pin.Dispose(); }
        if (_owner == IntPtr.Zero) throw NativeFailure();
        _sentTerritory = input.TerritoryOwnership ?? _sentTerritory;
        _sentMapColors = input.TerritoryOwnership?.SourceMapColors ?? _sentMapColors;
        _sentArmy = input.ArmyWorld ?? _sentArmy;
        _sentCityGate = input.CityGateWorld ?? _sentCityGate;
        _sentAreaCapital = input.AreaCapitalWorld ?? _sentAreaCapital;
        _animationState(_owner,out var animation);
        return new(receipt.Request, receipt.Camera){Animation=animation,ArmyUnitPositions=ReadArmyUnitPositions()};
    }

    /// <summary>The output's last submitted Godot use must be Canvas sampling.
    /// Native restores SRV after its GPU copy; no clear or readback is performed.</summary>
    public D3D11MapReceipt Next(ulong output, D3D11MapInput input)
    {
        RequireRenderThread(); ObjectDisposedException.ThrowIf(_module == IntPtr.Zero, this);
        if (_owner == IntPtr.Zero) throw new InvalidOperationException("Native Main has not been created.");
        var native = input.ToNative();
        var pin = PinWorld(input, ref native);
        NativeReceipt receipt; int result;
        try { result = _next(_owner, output, in native, 1u, out receipt); }
        finally { pin.Dispose(); }
        if (result != 0) throw NativeFailure();
        _sentTerritory = input.TerritoryOwnership ?? _sentTerritory;
        _sentMapColors = input.TerritoryOwnership?.SourceMapColors ?? _sentMapColors;
        _sentArmy = input.ArmyWorld ?? _sentArmy;
        _sentCityGate = input.CityGateWorld ?? _sentCityGate;
        _sentAreaCapital = input.AreaCapitalWorld ?? _sentAreaCapital;
        _animationState(_owner,out var animation);
        return new(receipt.Request, receipt.Camera){Animation=animation,ArmyUnitPositions=ReadArmyUnitPositions()};
    }

    public void UpdateAnimations(D3D11MapInput input,uint fpsMode)
    {
        RequireRenderThread();ObjectDisposedException.ThrowIf(_module==IntPtr.Zero,this);
        var native=input.ToNative();var pin=PinWorld(input,ref native);int result;
        try{result=_animation(_owner,in native,fpsMode);}finally{pin.Dispose();}
        if(result!=0)throw NativeFailure();
        _sentTerritory=input.TerritoryOwnership??_sentTerritory;
        _sentMapColors=input.TerritoryOwnership?.SourceMapColors??_sentMapColors;
        _sentArmy=input.ArmyWorld??_sentArmy;
        _sentCityGate=input.CityGateWorld??_sentCityGate;
        _sentAreaCapital=input.AreaCapitalWorld??_sentAreaCapital;
    }

    public void Dispose()
    {
        if (_module == IntPtr.Zero) return;
        RequireRenderThread();
        // Native Owner drains queued work before retiring its coroutine and On12 owners.
        if (_owner != IntPtr.Zero) { _destroy(_owner); _owner = IntPtr.Zero; }
        NativeLibrary.Free(_module); _module = IntPtr.Zero;
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
        internal GCHandle Colors, Palette, Army, Season, CityGate, AreaCapital;
        public void Dispose()
        {
            if (Colors.IsAllocated) Colors.Free();
            if (Palette.IsAllocated) Palette.Free();
            if (Army.IsAllocated) Army.Free();
            if (Season.IsAllocated) Season.Free();
            if (CityGate.IsAllocated) CityGate.Free();
            if (AreaCapital.IsAllocated) AreaCapital.Free();
        }
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
    private static void RequireRenderThread()
    {
        if (!RenderingServer.IsOnRenderThread())
            throw new InvalidOperationException("Native Main ownership belongs to Godot's render thread.");
    }
}

using System.Buffers.Binary;
using San14.Core;
using San14.Data;

public readonly record struct OriginalD3D11PreparedMainWord(int Offset, uint Bits);
public sealed class OriginalD3D11CurrentSeasonLight
{
    internal OriginalD3D11CurrentSeasonLight(uint objectId, uint groupMask, byte[] data)
    { ObjectId = objectId; GroupMask = groupMask; CompleteData = data; }
    public uint ObjectId { get; }
    public uint GroupMask { get; }
    internal ReadOnlyMemory<byte> CompleteData { get; }
    public ReadOnlyMemory<byte> ShaderData328 => CompleteData[..0x328];
}

/// <summary>Same-request normal Main season/RGB/weather/local-light writes.
/// Geometry retains its preceding camera; post/Sky/lighting consume this
/// prepared prefix. This packet neither creates GPU owners nor advances World.</summary>
public sealed class OriginalD3D11CurrentSeasonFrame
{
    internal OriginalD3D11CurrentSeasonFrame(OriginalMainMapCameraModePacket mode,
        NativeMapSeasonState worldSeason, OriginalSkySeasonMainCameraRgbFrame rgb,
        OriginalMapSourceCreatedSeasonLightingFrame present,
        OriginalD3D11PreparedMainWord[] words, OriginalD3D11CurrentSeasonLight[] lights,
        OriginalD3D11CurrentAtmosphereFrame atmosphere)
    {
        Mode = mode; WorldSeason = worldSeason; Rgb = rgb; Present = present;
        PreparedMain = present.PreparedCameraPrefix; PreparedWords = Array.AsReadOnly(words);
        Lights = Array.AsReadOnly(lights); Atmosphere = atmosphere;
    }
    public OriginalMainMapCameraModePacket Mode { get; }
    public NativeMapSeasonState WorldSeason { get; }
    public OriginalSkySeasonMainCameraRgbFrame Rgb { get; }
    public OriginalMapSourceCreatedSeasonLightingFrame Present { get; }
    public ReadOnlyMemory<byte> PreparedMain { get; }
    public IReadOnlyList<OriginalD3D11PreparedMainWord> PreparedWords { get; }
    public IReadOnlyList<OriginalD3D11CurrentSeasonLight> Lights { get; }
    public OriginalD3D11CurrentAtmosphereFrame Atmosphere { get; }
    public int MainMode => Mode.Mode288;
    public int SeasonIndex => Rgb.Inputs.Source.Season.Resources.Selection.SeasonIndex;
    public ReadOnlyMemory<byte> MainFog72 => PreparedMain.Slice(0x340, 72);
    public uint HdrLutKey => PreparedWords.Single(word => word.Offset == 0xEB8).Bits;
    public uint LdrLutKey => PreparedWords.Single(word => word.Offset == 0xEBC).Bits;

    /// <summary>Minimal numeric payload for native upload. No pointers or
    /// resource hashes. Full LightData handles are deliberately not serialized.
    /// Resource keys and textures use the separately installed loaded season.</summary>
    public byte[] ToNativeBytes(OriginalD3D11CurrentHdrCatalog? hdr = null)
    {
        // Header32, Main3C0, sorted (int32 offset,uint32 bits) writes,
        // then five (uint32 object,mask,reserved,reserved)+numeric LightData328.
        byte[] hdrBytes = hdr?.PrepareCurrentBytes(this) ?? [];
        byte[] result = new byte[32 + 960 + PreparedWords.Count * 8 + Lights.Count * (16 + 0x328) + OriginalD3D11CurrentAtmosphereFrame.ByteLength + hdrBytes.Length];
        var bytes = result.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)MainMode);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], (uint)SeasonIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..], (uint)PreparedWords.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[12..], (uint)Lights.Count);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[16..], Mode.AcceptedMainFrame.Camera.FrameNumber);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[24..], WorldSeason.ProcessedNativeOrdinal);
        PreparedMain.Span.CopyTo(bytes[32..]);
        int at = 32 + 960;
        foreach (var word in PreparedWords)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes[at..], word.Offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[(at + 4)..], word.Bits); at += 8;
        }
        foreach (var light in Lights)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], light.ObjectId);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes[(at + 4)..], light.GroupMask);
            light.ShaderData328.Span.CopyTo(bytes[(at + 16)..]); at += 16 + 0x328;
        }
        Atmosphere.NumericBytes.Span.CopyTo(bytes[at..]);
        hdrBytes.CopyTo(bytes[(at + OriginalD3D11CurrentAtmosphereFrame.ByteLength)..]);
        return result;
    }

    /// <summary>Same actual World group query and modifier for each HDR pass.
    /// Caller provides that pass's masks/arg18, current ambient rate and Fog
    /// owner. The result's names are bound using that shader's loaded RDEF.</summary>
    public IReadOnlyDictionary<string, byte[]> DeferredNamedFields(uint outerMask,
        uint innerMask, bool arg18, float ambientMapsRate, bool fogOwnerEnabled)
        => OriginalD3D11CurrentLightingFields.Create(this, outerMask, innerMask,
            arg18, ambientMapsRate, fogOwnerEnabled);
}

/// <summary>Retained CPU current producer for the native D3D11 session. Load
/// kernels once; consume the immutable World and camera after coalescing delta.
/// Accept only after native success so skipped pending cameras do not advance
/// the previous RGB transition. A failed native session is closed by its owner.</summary>
public sealed class OriginalD3D11CurrentSeasonPublisher
{
    private OriginalD3D11CurrentSeasonResources _resources;
    private readonly OriginalMainMapCameraModeOwner _mode;
    private readonly OriginalMapMainCameraPostSelectionOwner _post;
    private readonly OriginalMapGameLightQualityOwner _quality;
    private readonly OriginalMapSeasonMainCameraPhaseSource _phase;
    private readonly OriginalSkySceneControllerSourceProfile _skyController;
    private readonly OriginalSkySceneSky2Parameters _sky;
    private readonly LoadedOriginalSkyDirectionalSetterColorNativeSource _color;
    private OriginalSkySeasonMainCameraRgbFrame? _previousRgb;
    private readonly OriginalD3D11CurrentSeasonLibrary? _library;

    public OriginalD3D11CurrentSeasonPublisher(OriginalGameRepository repository,
        OriginalD3D11CurrentSeasonResources resources,
        OriginalMainMapCameraModeOwner? actualMainMode = null,
        OriginalMapGameLightQualityOwner? actualQuality = null,
        OriginalD3D11CurrentSeasonLibrary? resourceLibrary = null)
    {
        _resources = resources;
        _library = resourceLibrary;
        string locale = resources.Source.Locale;
        _mode = actualMainMode ?? OriginalMainMapCameraModeOwner.CreateFresh(repository, locale, "native-current-season/App");
        _quality = actualQuality ?? OriginalMapGameLightQualityOwner.CreateFreshPrimary(repository, locale, "native-current-season/Configs");
        _post = OriginalMapMainCameraPostSelectionOwner.CreateFresh(repository, _mode);
        _phase = OriginalMapSeasonMainCameraPhaseSource.LoadSourceDefaults(repository, locale);
        _skyController = OriginalSkySceneSourceDefaults.CreateControllerConstructor();
        _sky = OriginalSkySceneSourceDefaults.CreateSky2Parameters(107);
        _color = OriginalSkyDirectionalSetterColorNativeSource.Load(repository, locale);
    }

    public OriginalMapSeasonSelection LoadedSelection => _resources.Selection;
    public OriginalD3D11CurrentSeasonResources LoadedResources => _resources;

    /// <summary>Caller installs the already loaded generation while the render
    /// pump is drained. Main/App, previous RGB and native history stay alive.</summary>
    public void Install(OriginalD3D11CurrentSeasonResources resources)
    {
        if (resources.Source.Locale != _resources.Source.Locale)
            throw new InvalidDataException("A live Main season cannot switch executable locale.");
        _resources = resources;
    }

    public OriginalD3D11CurrentSeasonFrame PrepareCurrent(GameState world,
        OriginalMainMapCameraFrame currentMain, float elapsedSeconds)
    {
        var season = world.NativeMapSeason ?? throw new NotSupportedException(
            "Current World has no owned third-date season; Core.Date cannot supply it.");
        var calendar = OriginalMapSeasonRuntime.PrepareFrame(season, currentMain.Camera.FrameNumber);
        if (calendar.Selection != _resources.Selection && _library is not null)
            Install(_library.Resolve(season, currentMain).Resources);
        var selected = _resources.Source.CameraSource.WithCalendar(calendar);
        var mode = _mode.PrepareCurrent(currentMain);
        var post = _post.PrepareCurrent(selected, mode, _quality);
        var before = OriginalSkySeasonMainCameraProducer.PrepareBeforeFinalPreparation(
            selected, currentMain, mode.Mode288, _phase,
            previousAcceptedRgbFrame: _previousRgb, mainPostSelection: post);
        var rgb = OriginalSkySeasonMainCameraProducer.PrepareSunMoonAndSky(before,
            elapsedSeconds, OriginalNativeCameraTangentDispatch.Sse, _skyController, _sky);
        var weather = OriginalSkySeasonMainWeatherFog.PrepareSourceDatabaseResetFrame(rgb,
            selected.Transmittance.Power, OriginalNativeCameraTangentDispatch.Sse);
        var present = OriginalMapSourceCreatedSeasonLightingFrame.CapturePreparedSeason(rgb, weather);
        var actual = selected.PostEffectScalarSource.CaptureActualMainPreparation(before, currentMain.Camera);
        var words = actual.Scalars.PreparedCameraOffsets.ToDictionary(at => at, at =>
            actual.Scalars.TryReadWord(at, out uint bits)
                ? bits : throw new InvalidDataException("Current Main scalar writer is missing its word."));
        // C358A0 selected copies precede the actual V418 writers. They supply
        // only missing words; dynamic post/quality stores retain precedence.
        foreach (var word in _resources.SelectedShadowWords) words.TryAdd(word.Key, word.Value);
        if (actual.PreparedSpecialRectangleFlag1510 is { } flag)
            words[0x1510] = flag;
        var prefix = present.PreparedCameraPrefix;
        var lights = _resources.Lights.Select(light => new OriginalD3D11CurrentSeasonLight(
            light.ObjectId, light.GroupMask,
            light.DefaultCaller.ApplyCurrentCamera(prefix.Span).Bytes)).ToArray();
        var atmosphere = OriginalD3D11CurrentAtmosphereFrame.Prepare(present,
            selected.Transmittance.Tangent, _color,
            lights.First(light => (light.GroupMask & 0x80) != 0).CompleteData.Span,
            _resources.ControllerGeometry40);
        return new(mode, season, rgb, present,
            words.OrderBy(word => word.Key).Select(word => new OriginalD3D11PreparedMainWord(word.Key, word.Value)).ToArray(), lights, atmosphere);
    }

    public void AcceptCurrent(OriginalD3D11CurrentSeasonFrame frame) => _previousRgb = frame.Rgb;
}

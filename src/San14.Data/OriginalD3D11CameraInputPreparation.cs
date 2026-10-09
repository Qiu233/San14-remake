using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Current owner values which are not immutable source declarations.
/// The accepted Main frame retains its actual lens, prefix and controller state.</summary>
public sealed record OriginalD3D11CameraContinuousInputs(OriginalMainMapCameraFrame InitialMain,
    bool MaximumYOverride, uint NativeClipProfile, OriginalNativeCameraTangentDispatch TangentDispatch);

/// <summary>The initial direct-store state for the supported Main mode0/1 profile.
/// Selected cloudy and prior cloudy are original float bits, not resource keys.</summary>
public sealed record OriginalD3D11CameraDirectProfileInputs(int Quality, int MainMode288,
    uint AppFlags, uint PriorCloudy430Bits, uint SelectedCloudy430Bits);

/// <summary>Installed-source encoding of the existing CCP1/RMP1/CMP1 camera
/// profiles. These are first-load local caches, not a camera selection policy.
/// The caller supplies its accepted Main and actual owner/season values. This
/// source owner does not create the complete prepared Main18C0 numeric tail.</summary>
public sealed class OriginalD3D11CameraInputPreparation
{
    private readonly OriginalD3D11TerrainInputPreparation terrain;
    private OriginalD3D11CameraInputPreparation(string locale, OriginalD3D11TerrainInputPreparation terrain,
        LoadedOriginalMainMapCameraStartupSource startup, OriginalMainMapCameraControllerParameters parameters,
        LoadedOriginalMapMainCameraPostSelectionSource directSource)
    {
        Locale = locale; this.terrain = terrain; Startup = startup;
        Parameters = parameters; DirectSource = directSource;
        var property = terrain.Parameters.TerrainRoot.Properties.Single(p => p.NameHash == 0x0575B6F4);
        if (property.TypeCode != 8 || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException("Camera terrain LOD scale lacks its scalar source declaration.");
        LodScale = BinaryPrimitives.ReadSingleLittleEndian(property.RawValue);
        if (!float.IsFinite(LodScale) || LodScale <= 0 || terrain.Terrain.SpatialTree.LevelCount != 6 ||
            terrain.Terrain.SpatialTree.Nodes.Count != 1365 || terrain.Terrain.SpatialTree.RawHeader.Length != 20)
            throw new NotSupportedException("Camera CCP1 requires the supported original terrain tree/profile shape.");
    }

    public string Locale { get; }
    public LoadedOriginalMainMapCameraStartupSource Startup { get; }
    public OriginalMainMapCameraControllerParameters Parameters { get; }
    public LoadedOriginalMapMainCameraPostSelectionSource DirectSource { get; }
    public float LodScale { get; }

    public static OriginalD3D11CameraInputPreparation Load(OriginalGameRepository repository, string locale,
        OriginalD3D11TerrainInputPreparation terrain)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(terrain);
        if (terrain.Scalar.Locale != locale)
            throw new InvalidDataException("Camera and terrain preparation require the same installed source locale.");
        return new(locale, terrain, OriginalMainMapCameraStartupSource.Load(repository, locale),
            OriginalMainMapCameraControllerParameters.LoadSourceDefaults(repository, locale),
            OriginalMapMainCameraPostSelectionSource.Load(repository, locale));
    }

    public void WriteContinuousInputs(string directory, OriginalD3D11CameraContinuousInputs current)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory); ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(current.InitialMain);
        var main = current.InitialMain;
        var camera = main.Camera;
        if (camera.ExecutableLocale != Locale || camera.Source.Camera.ObjectId != OriginalMainMapCameraAdapter.MainCameraObjectId ||
            camera.Source.Resource.Source.ResourceUri != OriginalMapCameraConstructionSource.SourceResourceUri ||
            camera.CurrentPose.Scale90 != Vector3.One || camera.Prefix.Length != OriginalMapCameraConstruction.PrefixByteLength)
            throw new NotSupportedException("Camera CCP1 needs the accepted installed Main38D and supported unit-scale source frame.");
        byte[] prefix = camera.Prefix.ToArray();
        prefix.AsSpan(0x10, 0x130).Clear(); // Eye/target/up/matrices are computed by the retained native owner.
        float tangent = OriginalNativeCameraTangent.Tangent1020A90(camera.Lens.VerticalFovRadians * .5f, current.TangentDispatch);
        if (!float.IsFinite(tangent)) throw new InvalidDataException("Camera half-FOV tangent is nonfinite.");
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "profile.bin")));
        writer.Write(0x31504343u); writer.Write(prefix);
        writer.Write(tangent); writer.Write(BitConverter.UInt32BitsToSingle(camera.Source.CpuWord(0x0C)));
        writer.Write(current.NativeClipProfile); writer.Write(LodScale);
        writer.Write(terrain.Height.Height.Width); writer.Write(terrain.Material.InitialControl.SourceMap.Width);
        for (int band = 0; band < 7; band++) writer.Write(Parameters.ZoomAnchor(band));
        for (int band = 0; band < 7; band++)
        {
            var b = Parameters.Bounds(new(band, 0, 0), current.MaximumYOverride);
            foreach (float value in new[] { b.MinimumFocus70.X, b.MinimumFocus70.Y, b.MinimumFocus70.Z,
                b.MaximumFocus80.X, b.MaximumFocus80.Y, b.MaximumFocus80.Z, (float)b.MinimumYawB0,
                (float)b.MaximumYawB8, (float)b.MinimumPitchC8, (float)b.MaximumPitchD0 }) writer.Write(value);
        }
        var state = main.ControllerState;
        foreach (double value in new[] { state.FocusX, state.FocusY, state.FocusZ, state.FocusW,
            state.Distance90, state.YawA8, state.PitchC0 }) writer.Write(value);
        writer.Write(terrain.Terrain.SpatialTree.RawHeader);
        foreach (var node in terrain.Terrain.SpatialTree.Nodes) writer.Write(node.RawBytes);
    }

    public void WriteRenderModePrefixInputs(string directory, OriginalD3D11CameraDirectProfileInputs current)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory); ArgumentNullException.ThrowIfNull(current);
        if (current.MainMode288 is < 0 or > 1 ||
            !float.IsFinite(BitConverter.UInt32BitsToSingle(current.PriorCloudy430Bits)) ||
            !float.IsFinite(BitConverter.UInt32BitsToSingle(current.SelectedCloudy430Bits)))
            throw new NotSupportedException("RMP1 needs the supported mode0/1 owner and finite cloudy values.");
        var words = DirectSource.ProjectDirectWrites(current.Quality, current.MainMode288, current.AppFlags,
            current.PriorCloudy430Bits, current.SelectedCloudy430Bits);
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "render-mode-prefix-profile.bin")));
        writer.Write(0x31504D52u);
        writer.Write(words[0x1C4]); writer.Write(words[0x180]); writer.Write(words[0x188]);
        writer.Write(words[0x1D0]); writer.Write(words[0x210]); writer.Write(current.PriorCloudy430Bits);
    }

    public void WriteMotionInputs(string directory) => WriteMotionInputs(directory, Parameters);

    public static void WriteMotionInputs(string directory, OriginalMainMapCameraControllerParameters parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory); ArgumentNullException.ThrowIfNull(parameters);
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "motion-profile.bin")));
        writer.Write(0x31504D43u);
        for (int band = 0; band < 7; band++) writer.Write(parameters.ZoomBandPitch(band));
        writer.Write(parameters.SourceInertiaTimeoutMilliseconds);
    }

    /// <summary>Encode the explicitly selected current Field and preparation
    /// branch. A present MHK does not select successful manager creation, and
    /// unknown Field edits must not be replaced with an empty sequence.</summary>
    public static void WriteGroundHeightInputs(string directory, OriginalMapStaticModelGroundHeightSource field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory); ArgumentNullException.ThrowIfNull(field);
        var grid = field.Grid;
        if (!Enum.IsDefined(field.PreparationBranch) || field.CurrentEdits is null ||
            grid.HeightScale14 is not { } scale || grid.OriginX20 is not { } originX ||
            grid.OriginZ24 is not { } originZ || grid.SampleSpacing4C is not { } spacing ||
            grid.MinimumHeight50 is not { } minimum || grid.MaximumHeight54 is not { } maximum ||
            grid.CellWidth48 is not { } width || grid.CellHeight4A is not { } height || grid.PackedSamples58 is not { } samples)
            throw new InvalidDataException("GroundHeight requires the explicit prepared current Field grid.");
        if (width is < 1 or > 65535 || height is < 1 or > 65535 ||
            samples.LongLength != ((long)width + 1) * ((long)height + 1) ||
            !float.IsFinite(scale) || !float.IsFinite(originX) || !float.IsFinite(originZ) ||
            !float.IsFinite(spacing) || spacing <= 0 || !float.IsFinite(minimum) || !float.IsFinite(maximum))
            throw new InvalidDataException("GroundHeight grid differs from the supported native packet.");
        Directory.CreateDirectory(directory);
        using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "profile.bin"))))
        {
            writer.Write((uint)field.PreparationBranch); writer.Write(1u);
            writer.Write(scale); writer.Write(originX); writer.Write(originZ); writer.Write(spacing);
            writer.Write(minimum); writer.Write(maximum); writer.Write(width); writer.Write(height);
        }
        using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "packed24.bin"))))
            foreach (uint word in samples) writer.Write(word);
    }
}

using System.Buffers.Binary;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public enum OriginalMapSeasonCameraSelectionPolicy
{
    ExplicitRoot,
    DeclaredChild,
    ExplicitPair,
    SourceEmptyAxesFallback
}

public sealed record OriginalMapSeasonCameraControlDeclarations(uint SkylightStaObjectId,
    bool AutoClock, float TimeSpan, float TransitionSpan, float SelectorScalar1A0,
    float SelectorScalar1A4, string SourceEvidence);

public sealed record OriginalMapSeasonSceneClockSeedField(string Name, int SourcePropertyOrdinal,
    uint NameHash, int SourceCpuOffset, int GlobalOffset, uint Bits, float Value);

/// <summary>Source-created values of the eight global words written by native
/// pending database state4 / 3FFA10. Loader readiness and later current writers
/// remain explicit responsibilities of the runtime owner.</summary>
public sealed class OriginalMapSeasonSceneClockSeed
{
    internal OriginalMapSeasonSceneClockSeed(OriginalMapSeasonCameraProvider provider,
        OriginalMapSeasonSceneClockSeedField[] fields, OriginalSkylightParameters root,
        OriginalMapSeasonCameraControlDeclarations controls)
    {
        Provider = provider; GlobalFields = Array.AsReadOnly(fields);
        RootSkylightParameters = root; Controls = controls;
        Origin = "source-created pending SceneDatabase state4 root E17F5C21 reset; " +
            "TC3FDB3B readiness gate ->3FDBAE/3FFA10; no later runtime writer applied";
        SourceEvidence = provider.SourceEvidence + "; " + Origin;
    }
    public OriginalMapSeasonCameraProvider Provider { get; }
    public IReadOnlyList<OriginalMapSeasonSceneClockSeedField> GlobalFields { get; }
    public OriginalSkylightParameters RootSkylightParameters { get; }
    public OriginalMapSeasonCameraControlDeclarations Controls { get; }
    public float MonthDay => GlobalFields[0].Value;
    public float DateTime => GlobalFields[1].Value;
    public float NorthDir => GlobalFields[2].Value;
    public float Cloudy => GlobalFields[3].Value;
    public float Temperature => GlobalFields[4].Value;
    public float Humidity => GlobalFields[5].Value;
    public float Precipitation => GlobalFields[6].Value;
    public float Foggy => GlobalFields[7].Value;
    // These source declarations are retained for callers; 3FFA10 does not copy them.
    public float Longitude => RootSkylightParameters.Longitude;
    public float Latitude => RootSkylightParameters.Latitude;
    public float Altitude => RootSkylightParameters.Altitude;
    public float TimeSpan => Controls.TimeSpan;
    public bool AutoClock => Controls.AutoClock;
    public string Origin { get; }
    public string SourceEvidence { get; }
    public bool NativeDatabaseReadinessObserved => false;
    public bool IsCurrentRuntimeState => false;
    public void Validate()
    {
        Provider.Validate();
        var canonical = OriginalMapSeasonCameraApplication.ReadSourceSceneClockSeed(Provider);
        if (!GlobalFields.SequenceEqual(canonical.GlobalFields) ||
            !OriginalMapSeasonCameraApplication.SameBits(RootSkylightParameters, canonical.RootSkylightParameters) ||
            Controls != canonical.Controls)
            throw new InvalidDataException("Scene clock seed differs from its canonical root source declarations.");
    }
}

/// <summary>A complete source Camera graph. Source declarations are retained separately
/// from the explicit native selection arguments supplied by a current producer.</summary>
public sealed class OriginalMapSeasonCameraProvider
{
    private readonly OriginalSkylightParameters[] skylightParameters;
    internal OriginalMapSeasonCameraProvider(OriginalMapSeasonActionDatabase database,
        OriginalMapSeasonCameraPackage root, DokObject childArray, DokObject selector,
        uint[] childKeys, OriginalMapSeasonCameraPackage[] children, uint externalSelectorObjectId)
    {
        Database = database; Root = root; ChildArray = childArray; Selector = selector;
        ChildKeys = Array.AsReadOnly(childKeys); CameraChildren = Array.AsReadOnly(children);
        ExternalSelectorObjectId = externalSelectorObjectId;
        skylightParameters = children.Prepend(root)
            .Select(OriginalMapSeasonCameraApplication.ReadSkylightParameters).ToArray();
        SourceEvidence = root.SourceEvidence + $"; ChildCamera[{childArray.ObjectId:X8}]" +
            $"/FF26EE41 -> selector {selector.ObjectId:X8}/class13696D79 and four Camera children; " +
            OriginalMapSeasonCameraApplication.NativeEvidence;
    }

    public OriginalMapSeasonActionDatabase Database { get; }
    public OriginalMapSeasonCameraPackage Root { get; }
    public DokObject ChildArray { get; }
    public DokObject Selector { get; }
    public IReadOnlyList<uint> ChildKeys { get; }
    public IReadOnlyList<OriginalMapSeasonCameraPackage> CameraChildren { get; }
    public uint ExternalSelectorObjectId { get; }
    public string SourceEvidence { get; }

    public void Validate() => OriginalMapSeasonCameraApplication.ValidateProvider(this);

    internal OriginalMapSeasonCameraPackage Package(int nativeIndex) => nativeIndex <= 0
        ? Root : nativeIndex < ChildKeys.Count ? CameraChildren[nativeIndex - 1]
        : throw new InvalidDataException("Native Camera child index exceeds the complete source graph.");
    internal OriginalSkylightParameters Parameters(int nativeIndex)
        => nativeIndex <= 0 ? skylightParameters[0] : nativeIndex < skylightParameters.Length
            ? skylightParameters[nativeIndex]
            : throw new InvalidDataException("Native Camera child index exceeds the imported parameter directory.");
}

/// <summary>Seven native float32 fields selected from an explicit source graph.
/// This token represents the bounded C3FEA0/C9B5C0 selection and C358A0 copy,
/// and does not claim that an original process or the full native callback ran.</summary>
public sealed class OriginalMapSeasonCameraSelection
{
    internal OriginalMapSeasonCameraSelection(OriginalMapSeasonCameraProvider provider,
        int firstIndex, int? secondIndex, float weight, OriginalMapSeasonCameraSelectionPolicy policy,
        float? scalar1A0 = null, float? scalar1A4 = null)
    {
        Provider = provider; FirstIndex = firstIndex; SecondIndex = secondIndex; Weight = weight;
        SelectionPolicy = policy; Scalar1A0 = scalar1A0; Scalar1A4 = scalar1A4;
        FirstPackage = provider.Package(firstIndex);
        SecondPackage = secondIndex is int second ? provider.Package(second) : null;
        var first = provider.Parameters(firstIndex);
        SkylightParameters = SecondPackage is null ? first : OriginalMapSeasonCameraApplication.Interpolate(
            first, provider.Parameters(secondIndex!.Value), weight);
        SourceEvidence = provider.SourceEvidence + $"; policy={policy}; first={firstIndex}/" +
            $"{FirstPackage.Camera.ObjectId:X8}; second={secondIndex?.ToString() ?? "none"}; " +
            $"weightBits={BitConverter.SingleToUInt32Bits(weight):X8}; " +
            "TC CB35C0->C3FEA0, C9B915..C9B9F6, CB364E/CB3693->C359E5..C35A3A";
    }

    public OriginalMapSeasonCameraProvider Provider { get; }
    public OriginalMapSeasonCameraPackage FirstPackage { get; }
    public OriginalMapSeasonCameraPackage? SecondPackage { get; }
    public int FirstIndex { get; }
    public int? SecondIndex { get; }
    public float Weight { get; }
    public OriginalMapSeasonCameraSelectionPolicy SelectionPolicy { get; }
    public float? Scalar1A0 { get; }
    public float? Scalar1A4 { get; }
    public OriginalSkylightParameters SkylightParameters { get; }
    public string SourceEvidence { get; }
    public bool OriginalNativeCodeExecuted => false;

    public void Validate()
    {
        Provider.Validate();
        var first = Provider.Parameters(FirstIndex);
        var expected = SecondIndex is int second ? OriginalMapSeasonCameraApplication.Interpolate(first,
            Provider.Parameters(second), Weight) : first;
        if (!OriginalMapSeasonCameraApplication.SameBits(expected, SkylightParameters))
            throw new InvalidDataException("Selected Camera skylight fields no longer match their canonical source graph.");
        if (SelectionPolicy == OriginalMapSeasonCameraSelectionPolicy.SourceEmptyAxesFallback)
            OriginalMapSeasonCameraApplication.ValidateEmptyAxesScope(Provider, Scalar1A0!.Value, Scalar1A4!.Value);
    }
}

public static class OriginalMapSeasonCameraApplication
{
    // Package import is a source load boundary. Current selection arguments,
    // interpolation, clocks and Main writers are not stored in this directory.
    private sealed record ImportedPackage(OriginalMapSeasonCameraPackage Canonical,
        OriginalSkylightParameters Parameters);
    private static readonly ConditionalWeakTable<OriginalMapSeasonCameraPackage, ImportedPackage> ImportedPackages = new();
    public const uint RootCameraObjectId = 0xE17F5C21;
    public const uint CameraChildrenClassId = 0x2EB79141;
    public const uint SelectorClassId = 0x13696D79;
    public const string NativeEvidence = "docs/.local/ce-season-camera-package-20261002/contract.json";

    public static OriginalMapSeasonCameraProvider ReadProvider(OriginalMapSeasonActionDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        database = OriginalMapSeasonResourceConsumer.ReadActionDatabase(database.Action, database.Objects);
        var root = OriginalMapSeasonResourceConsumer.ReadCameraPackage(database, RootCameraObjectId);
        uint childrenKey = Word(root.Camera, 27, 0xA7DFA945, 5);
        DokObject childArray = Object(database, childrenKey, CameraChildrenClassId);
        uint[] keys = Words(Field(childArray, 2, 0xFF26EE41, 5, 5));
        if (keys.Any(key => key == 0) || keys.Distinct().Count() != keys.Length)
            throw new InvalidDataException("Original Camera children require one selector and four distinct packages.");
        DokObject selector = Object(database, keys[0], SelectorClassId);
        ValidateSelector(selector);
        var children = keys.Skip(1).Select(key => OriginalMapSeasonResourceConsumer.ReadCameraPackage(database, key)).ToArray();
        foreach (var camera in children.Prepend(root))
        {
            if (camera.Camera.Properties.Count != 1119)
                throw new InvalidDataException("Original external Camera requires all 1119 declared properties.");
            _ = ReadSkylightParameters(camera);
            if (camera.Camera.ObjectId != RootCameraObjectId && Word(camera.Camera, 27, 0xA7DFA945, 5) != 0)
                throw new InvalidDataException("Nested Camera children fall outside the bounded source graph.");
        }
        uint externalSelector = Word(root.Camera, 1050, 0x2D4CC9AF, 5);
        return new(database, root, childArray, selector, keys, children, externalSelector);
    }

    /// <summary>C3FF3D direct child selection; index zero is the explicit root branch.</summary>
    public static OriginalMapSeasonCameraSelection SelectDeclaredPackage(
        OriginalMapSeasonCameraProvider provider, int nativePositiveChildIndex)
    {
        ArgumentNullException.ThrowIfNull(provider); provider.Validate();
        if (nativePositiveChildIndex < 0 || nativePositiveChildIndex >= provider.ChildKeys.Count)
            throw new ArgumentOutOfRangeException(nameof(nativePositiveChildIndex));
        return new(provider, nativePositiveChildIndex, null, 0,
            nativePositiveChildIndex == 0 ? OriginalMapSeasonCameraSelectionPolicy.ExplicitRoot
            : OriginalMapSeasonCameraSelectionPolicy.DeclaredChild);
    }

    /// <summary>C9B4E0 uses the root for a nonpositive pair index. C9B5C0 performs
    /// plain float32 interpolation without clamping or calendar wrapping.</summary>
    public static OriginalMapSeasonCameraSelection SelectExplicitPair(
        OriginalMapSeasonCameraProvider provider, int firstIndex, int secondIndex, float nativeWeight)
    {
        ArgumentNullException.ThrowIfNull(provider); provider.Validate();
        if (!float.IsFinite(nativeWeight)) throw new ArgumentOutOfRangeException(nameof(nativeWeight));
        if (firstIndex >= provider.ChildKeys.Count || secondIndex >= provider.ChildKeys.Count)
            throw new ArgumentOutOfRangeException(nameof(firstIndex));
        return new(provider, Math.Max(firstIndex, 0), Math.Max(secondIndex, 0), nativeWeight,
            OriginalMapSeasonCameraSelectionPolicy.ExplicitPair);
    }

    /// <summary>The source selector has empty interpolation axes. With native index
    /// arguments -1/-1/-1, a nonnull temporary Camera, no later override writer,
    /// scalar1A0 in [0,24], and scalar1A4 in [0,1], C40199 returns the root.
    /// Callers must provide the actual scalar inputs and retain this scope.</summary>
    public static OriginalMapSeasonCameraSelection SelectSourceEmptyAxes(
        OriginalMapSeasonCameraProvider provider, float scalar1A0, float scalar1A4)
    {
        ArgumentNullException.ThrowIfNull(provider); provider.Validate();
        ValidateEmptyAxesScope(provider, scalar1A0, scalar1A4);
        return new(provider, 0, null, 0, OriginalMapSeasonCameraSelectionPolicy.SourceEmptyAxesFallback,
            scalar1A0, scalar1A4);
    }

    public static OriginalSkylightParameters ReadSkylightParameters(OriginalMapSeasonCameraPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return Import(package).Parameters;
    }
    private static ImportedPackage Import(OriginalMapSeasonCameraPackage package)
        => ImportedPackages.GetValue(package, actual =>
        {
            var canonical = CanonicalPackage(actual);
            return new(canonical, ReadImportedSkylightParameters(canonical));
        });
    private static OriginalSkylightParameters ReadImportedSkylightParameters(OriginalMapSeasonCameraPackage package)
    {
        Span<byte> fields = stackalloc byte[28];
        for (int index = 0; index < OriginalSkylightParameters.Properties.Count; index++)
            Field(package.Camera, 826 + index, OriginalSkylightParameters.Properties[index].NameHash, 8, 1)
                .RawValue.CopyTo(fields.Slice(index * 4, 4));
        return OriginalSkylightParameters.ReadSourceFields(fields);
    }

    public static OriginalMapSeasonCameraControlDeclarations ReadControlDeclarations(
        OriginalMapSeasonCameraPackage package)
    {
        ArgumentNullException.ThrowIfNull(package); package = Import(package).Canonical;
        uint sta = Word(package.Camera, 825, 0x063D4BE7, 5);
        uint autoClock = Word(package.Camera, 905, 0xBE47DAC5, 4);
        if (autoClock > 1) throw new InvalidDataException("Original source AutoClock is not a boolean declaration.");
        float timeSpan = Float(package.Camera, 904, 0xC579866B);
        float transitionSpan = Float(package.Camera, 902, 0x7C80D02B);
        float scalar1A0 = Float(package.Camera, 211, 0xDCC14DE8);
        float scalar1A4 = Float(package.Camera, 212, 0xD923A0AC);
        return new(sta, autoClock != 0, timeSpan, transitionSpan, scalar1A0, scalar1A4,
            package.SourceEvidence + "; Camera declaration fields825/902/904/905; selector scalar fields211/212; " + NativeEvidence);
    }

    /// <summary>Read the exact nonnull-provider branch of 3FFA10 as a source
    /// seed. This does not establish database/GPU readiness or current weather,
    /// and never maps the simulation/World month numerically onto MonthDay.</summary>
    public static OriginalMapSeasonSceneClockSeed ReadSourceSceneClockSeed(
        OriginalMapSeasonCameraProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider); provider.Validate();
        var camera = provider.Root.Camera;
        var fields = new (string Name, int Ordinal, uint Hash, int Cpu, int Global)[]
        {
            ("SkylightMonthDay",826,0x085B6991,0xAC,0x18),
            ("SkylightDateTime",827,0x1EB4D0F0,0xB0,0x1C),
            ("SkylightNorthDir",831,0xF3554D1D,0xC0,0x20),
            ("SkylightCloudy",850,0x2F6A6AF9,0x7C,0x24),
            ("SkylightTemperature",851,0xDF989EE1,0x80,0x28),
            ("SkylightHumidity",852,0xAC082408,0x84,0x2C),
            ("SkylightPrecipitation",853,0x4669C572,0x88,0x30),
            ("SkylightFoggy",891,0x90B9A1E5,0x8C,0x34)
        };
        return new(provider, fields.Select(item => new OriginalMapSeasonSceneClockSeedField(item.Name,
            item.Ordinal, item.Hash, item.Cpu, item.Global, Word(camera,item.Ordinal,item.Hash,8),
            Float(camera,item.Ordinal,item.Hash))).ToArray(),
            ReadSkylightParameters(provider.Root), ReadControlDeclarations(provider.Root));
    }

    internal static void ValidateProvider(OriginalMapSeasonCameraProvider provider)
    {
        // ReadProvider imported and checked every source object/field. Runtime
        // selection needs its counted index directory; it does not reload the
        // complete CommonSetting and season archives to detect internal edits.
        if (provider.ChildKeys.Count != 5 || provider.CameraChildren.Count != 4 ||
            provider.Root.Camera.ObjectId != RootCameraObjectId ||
            provider.ChildKeys[0] != provider.Selector.ObjectId)
            throw new InvalidDataException("Camera provider has no complete imported child directory.");
        for (int index = 0; index < provider.CameraChildren.Count; index++)
            if (provider.CameraChildren[index].Camera.ObjectId != provider.ChildKeys[index + 1])
                throw new InvalidDataException("Camera child index differs from its imported source directory.");
    }

    internal static void ValidateEmptyAxesScope(OriginalMapSeasonCameraProvider provider, float a, float b)
    {
        if (!float.IsFinite(a) || !float.IsFinite(b) || a < 0 || a > 24 || b < 0 || b > 1 ||
            provider.ExternalSelectorObjectId != 0 || provider.Selector.Properties[4].ElementCount != 0 ||
            provider.Selector.Properties[5].ElementCount != 0)
            throw new InvalidDataException("Camera source does not satisfy the native empty-axis root selection scope.");
    }

    internal static OriginalSkylightParameters Interpolate(OriginalSkylightParameters first,
        OriginalSkylightParameters second, float weight)
    {
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original Camera interpolation requires SSE float32 operations.");
        float complement = Sse.SubtractScalar(Vector128.CreateScalar(1f), Vector128.CreateScalar(weight)).ToScalar();
        float Blend(float left, float right)
        {
            var rightProduct = Sse.MultiplyScalar(Vector128.CreateScalar(right), Vector128.CreateScalar(weight));
            var leftProduct = Sse.MultiplyScalar(Vector128.CreateScalar(left), Vector128.CreateScalar(complement));
            return Sse.AddScalar(leftProduct, rightProduct).ToScalar();
        }
        var result = new OriginalSkylightParameters(Blend(first.MonthDay, second.MonthDay),
            Blend(first.DateTime, second.DateTime), Blend(first.Longitude, second.Longitude),
            Blend(first.Latitude, second.Latitude), Blend(first.Altitude, second.Altitude),
            Blend(first.NorthDir, second.NorthDir), Blend(first.MoonAge, second.MoonAge));
        if (Values(result).Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("Native Camera interpolation produced a nonfinite field.");
        return result;
    }

    internal static bool SameBits(OriginalSkylightParameters a, OriginalSkylightParameters b) =>
        Values(a).Select(BitConverter.SingleToUInt32Bits).SequenceEqual(Values(b).Select(BitConverter.SingleToUInt32Bits));
    private static float[] Values(OriginalSkylightParameters p) =>
        [p.MonthDay, p.DateTime, p.Longitude, p.Latitude, p.Altitude, p.NorthDir, p.MoonAge];

    private static void ValidateSelector(DokObject selector)
    {
        if (selector.Properties.Count != 7) throw new InvalidDataException("Original source selector requires seven fields.");
        _ = Field(selector, 0, 0xF357B5DC, 5, 2); _ = Field(selector, 1, 0x2672FAC7, 5, 2);
        _ = Field(selector, 2, 0xF840EF92, 8, 2); _ = Field(selector, 3, 0xB0B5B508, 5, 1);
        _ = Field(selector, 4, 0x642A6605, 8, selector.Properties[4].ElementCount);
        _ = Field(selector, 5, 0x642A6643, 8, selector.Properties[5].ElementCount);
        _ = Field(selector, 6, 0x15247A48, 4, 3);
    }

    private static DokObject Object(OriginalMapSeasonActionDatabase database, uint key, uint type)
    {
        DokObject[] matches = database.Objects.Document.Objects.Where(item => item.ObjectId == key).ToArray();
        if (key == 0 || matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].ClassId != type)
            throw new InvalidDataException($"Original Camera graph requires one local object {key:X8}/class{type:X8}.");
        return matches[0];
    }

    private static DokProperty Field(DokObject owner, int ordinal, uint key, uint type, int count)
    {
        if (ordinal < 0 || ordinal >= owner.Properties.Count) throw new InvalidDataException("Missing Camera source property.");
        DokProperty field = owner.Properties[ordinal];
        if (field.Index != ordinal || field.NameHash != key || field.TypeCode != type ||
            field.ElementCount != count || field.ElementSize != 4 || field.RawValue.Length != count * 4 ||
            owner.Properties.Count(item => item.NameHash == key) != 1)
            throw new InvalidDataException($"Original Camera property {ordinal}/{key:X8} has an invalid typed declaration.");
        return field;
    }
    private static uint Word(DokObject owner, int ordinal, uint key, uint type) =>
        BinaryPrimitives.ReadUInt32LittleEndian(Field(owner, ordinal, key, type, 1).RawValue);
    private static float Float(DokObject owner, int ordinal, uint key)
    {
        float value = BinaryPrimitives.ReadSingleLittleEndian(Field(owner, ordinal, key, 8, 1).RawValue);
        if (!float.IsFinite(value)) throw new InvalidDataException("Original Camera declaration is not finite.");
        return value;
    }
    private static OriginalMapSeasonCameraPackage CanonicalPackage(OriginalMapSeasonCameraPackage package)
    {
        var database = OriginalMapSeasonResourceConsumer.ReadActionDatabase(package.Action, package.Objects);
        return OriginalMapSeasonResourceConsumer.ReadCameraPackage(database, package.Camera.ObjectId);
    }
    private static uint[] Words(DokProperty field) => Enumerable.Range(0, field.ElementCount)
        .Select(index => BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue.AsSpan(index * 4, 4))).ToArray();
}

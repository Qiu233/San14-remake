using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current Main fields consumed by the Forward cluster and volume writers.
/// The endpoint overrides are current Main tail fields, not cached constructor bytes.</summary>
public sealed record D3D11PrimaryForwardMainFields(float Near, float Far, bool ReverseDepth,
    float ClusterStartOverride, float ClusterEndOverride, int ViewportWidth, int ViewportHeight,
    float ClusterScalingDivisor, float VolumePlugin28, float VolumePlugin2C);

/// <summary>The current normal receiver and Pattern owner values, including retained inactive rows.</summary>
public sealed record D3D11PrimaryForwardShadowFields(Matrix4x4 WorldToPattern,
    IReadOnlyList<Matrix4x4> WorldToShadow, IReadOnlyList<Vector4> ShadowOffsets,
    Vector4 ViewZScale, Vector4 FadeDistances, Vector4 ShadowScale, int ShadowLoopCount,
    IReadOnlyList<Vector4> LightPlanes, Vector4 SidePlane, float Pattern,
    IReadOnlyList<Vector4> PatternPlaneSub, Vector4 SubShadowScale);

public sealed record D3D11PrimaryForwardCurrentFields(DeferredSceneInputs Lighting,
    D3D11PrimaryForwardMainFields Main, D3D11PrimaryForwardShadowFields Shadow,
    IReadOnlyList<Vector4> AmbientTransformRows, uint CurrentSubShadowCount);

/// <summary>Only values retained across native draws. The native ordinary,
/// HDR and normal receiver writers supply all remaining lanes before upload.</summary>
public sealed record D3D11PrimaryForwardRetainedFields(DeferredSceneInputs Lighting,
    D3D11PrimaryForwardMainFields Main, IReadOnlyList<Vector4> PatternPlaneSub,
    Vector4 SubShadowScale, IReadOnlyList<Vector4> AmbientTransformRows, uint CurrentSubShadowCount);

public sealed record D3D11PrimaryForwardAllocationFields(uint MaxPointLights, uint MaxSpotLights,
    uint IndicesCapacity, uint ClusterSizeX, uint ClusterSizeY, uint ClusterZCount, uint ClusterMode,
    uint SubShadowWidth, uint SubShadowHeight, uint SubShadowLayers, RenderTextureFormats SubShadowFormats);

public readonly record struct D3D11PrimaryForwardWrite(int Offset, int ByteCount);
public sealed record D3D11PrimaryForwardNumericPacket(byte[] Vertex, byte[] Pixel,
    IReadOnlyList<D3D11PrimaryForwardWrite> VertexWrites,
    IReadOnlyList<D3D11PrimaryForwardWrite> PixelWrites);

/// <summary>Builds Forward VS0/PS11 named numeric lanes from current typed owners.
/// Model geometry, source shader programs, selection and layout remain resource inputs.
/// Unknown optional Sub rows and array padding are not uploaded.</summary>
public sealed class D3D11PrimaryForwardNumericInputPreparation
{
    private readonly DxbcResourceDefinitions vertexDefinitions, pixelDefinitions;
    private readonly int clusterSize, clusterZCount;
    private readonly bool clusterScalingEnabled, clusterFadeEnabled;
    private readonly uint pointSoftShadowType, spotSoftShadowType;
    private D3D11PrimaryForwardNumericInputPreparation(LoadedShaderContainer shader)
    {
        vertexDefinitions = shader.ReadResourceDefinitions(0);
        pixelDefinitions = shader.ReadResourceDefinitions(11);
        var modules = shader.ReadConfigurations().Records;
        var cluster = modules.Single(record => record.Name == "ClusterLightList");
        clusterSize = checked((int)Parameter(cluster, "ClusterSize"));
        clusterZCount = checked((int)Parameter(cluster, "ClusterZNum"));
        clusterScalingEnabled = Parameter(cluster, "Scaling") != 0;
        clusterFadeEnabled = Parameter(cluster, "FadeOut") != 0;
        pointSoftShadowType = Parameter(modules.Single(record => record.Name == "SubPointLightShadows"), "SoftShadowType");
        spotSoftShadowType = Parameter(modules.Single(record => record.Name == "SubSpotLightShadows"), "SoftShadowType");
    }

    public static D3D11PrimaryForwardNumericInputPreparation Load(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(repository.LoadShaderContainer("system", 0x8F6C9FB9));
    }

    /// <summary>The source class18 constructor's affine identity. A later
    /// ambient transform setter must provide its actual three rows instead.</summary>
    public static IReadOnlyList<Vector4> FreshAmbientTransformRows() =>
        [new(1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, 1, 0)];

    public DeferredClusterInputs PrepareCurrentCluster(D3D11PrimaryForwardMainFields main,
        DeferredClusterFloatLogPath logarithmPath)
    {
        float start = Add(main.Near, MathF.Min(100, Divide(Subtract(main.Far, main.Near), 10)));
        if (start < main.ClusterStartOverride) start = main.ClusterStartOverride;
        float end = main.Far;
        if (main.ClusterEndOverride < end) end = MathF.Max(start, main.ClusterEndOverride);
        return DeferredClusterMath.Prepare(main.ViewportWidth, main.ViewportHeight,
            clusterSize, clusterZCount,
            main.ClusterScalingDivisor, DeferredClusterLogMath.Prepare(start, end, logarithmPath),
            main.Near, main.Far, main.ReverseDepth, clusterScalingEnabled, clusterFadeEnabled, end, 2000);
    }

    public D3D11PrimaryForwardNumericPacket Prepare(D3D11PrimaryForwardCurrentFields current,
        ModelGbufferVertexInputs primitiveVertex, Vector4 primitiveSpecular, float primitiveAlphaTest,
        DeferredClusterFloatLogPath logarithmPath)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(primitiveVertex);
        if (current.CurrentSubShadowCount != 0)
            throw new NotSupportedException("Forward optional Sub rows require their actual current producer.");
        var lighting = current.Lighting.Lighting ?? throw new InvalidDataException("Current Forward base lights are absent.");
        var camera = current.Lighting.Camera ?? throw new InvalidDataException("Current Forward camera is absent.");
        var fog = current.Lighting.Fog ?? throw new InvalidDataException("Current Forward fog is absent.");
        var ambient = current.Lighting.Ambient ?? throw new InvalidDataException("Current Forward ambient modifier is absent.");
        var occurrence = current.Lighting.Occlusion ?? throw new InvalidDataException("Current Forward occlusion is absent.");
        if (lighting.Lights.Count is < 1 or > 4 || fog.Scattering.Count != 4 ||
            current.Shadow.WorldToShadow.Count != 4 || current.Shadow.ShadowOffsets.Count != 4 ||
            current.Shadow.LightPlanes.Count != 12 || current.Shadow.PatternPlaneSub.Count != 2 ||
            current.AmbientTransformRows.Count != 3 || primitiveVertex.WorldToProjectionRows.Length != 16 ||
            primitiveVertex.LocalToWorldNormalRows.Length != 12 || primitiveVertex.LocalToWorldPositionRows.Length != 12)
            throw new InvalidDataException("Forward requires complete current matrix and vector groups.");
        var clst = PrepareCurrentCluster(current.Main, logarithmPath);
        if (clst.Scaling is not float scaling || clst.FadeDistance is not Vector2 fade)
            throw new NotSupportedException("Forward retained scaling/fade values need their actual owner when these module writes are disabled.");
        var volume = DeferredSceneConstants.PrepareVolumeFog(current.Main.Near, current.Main.Far,
            current.Main.ReverseDepth, current.Main.VolumePlugin28, current.Main.VolumePlugin2C);
        var values = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        void Floats(string name, params float[] words) => values.Add(name, words.Select(BitConverter.SingleToUInt32Bits).ToArray());
        void Vectors(string name, IEnumerable<Vector4> vectors) => Floats(name, vectors.SelectMany(Components).ToArray());
        void Vector(string name, Vector4 vector) => Floats(name, Components(vector));
        void Int(string name, int word) => values.Add(name, [unchecked((uint)word)]);
        Floats("mW2P", primitiveVertex.WorldToProjectionRows);
        Floats("mL2WT", primitiveVertex.LocalToWorldNormalRows);
        Floats("mL2W", primitiveVertex.LocalToWorldPositionRows);
        Floats("mW2Pt", Components(current.Shadow.WorldToPattern));
        Floats("mW2S", current.Shadow.WorldToShadow.SelectMany(Components).ToArray());
        Vector4[] directions = new Vector4[4], colors = new Vector4[4]; float[] rates = new float[4];
        for (int i = 0; i < 4; i++)
        {
            var light = i < lighting.Lights.Count ? lighting.Lights[i] : null;
            directions[i] = light?.Direction ?? new(0, 1, 0, 0);
            colors[i] = light?.Color ?? Vector4.Zero; rates[i] = light?.Occurrence ?? 1;
        }
        Vectors("litDir", directions); Vectors("litCol", colors); Floats("litOcc", rates);
        Vector("ambLit", lighting.Ambient); Int("nDirLit", lighting.Lights.Count - 1);
        Vector("vEye", camera.SourceEye); Vector("cSpec", primitiveSpecular);
        Floats("vOccRate", occurrence.Rates.X, occurrence.Rates.Y);
        Vector("vClstZPrm", clst.DepthParameters);
        values.Add("nClstPtch", [unchecked((uint)clst.HorizontalPatchCount), unchecked((uint)clst.PatchCount)]);
        Floats("fClstScl", scaling); Floats("vFadeDist", fade.X, fade.Y); Vector("gVfogInfo", volume.Info);
        Floats("fPtn", current.Shadow.Pattern); Vectors("fPtnPlSub", current.Shadow.PatternPlaneSub);
        Floats("vATest", primitiveAlphaTest); Vectors("vSMOf", current.Shadow.ShadowOffsets);
        Vector("vS2VZ", current.Shadow.ViewZScale); Vector("vSMDF", current.Shadow.FadeDistances);
        Vector("scSM", current.Shadow.ShadowScale); Int("nSSLoop", current.Shadow.ShadowLoopCount);
        Vectors("vLPlane", current.Shadow.LightPlanes); Vector("sdplane", current.Shadow.SidePlane);
        Int("nSubSdw", 0); Vector("scSubSM", current.Shadow.SubShadowScale);
        Vector("fog", fog.Coefficients); Vector("cFog", fog.Color); Vectors("Scat", fog.Scattering);
        Floats("fAmbMapsDORate", ambient.MapsDirectionalOcclusionRate);
        Vectors("mW2E", current.AmbientTransformRows);
        Vector("vAmbDifScl", ambient.DiffuseScale); Vector("vAmbSpcScl", ambient.SpecularScale);
        var vertex = Project(vertexDefinitions, values, false);
        var pixel = Project(pixelDefinitions, values, true);
        return new(vertex.Bytes, pixel.Bytes, vertex.Writes, pixel.Writes);
    }

    /// <summary>Startup storage for prepareNativePrimaryForwardCurrent. No
    /// fabricated primitive/shadow matrix is supplied: those fields are left
    /// as storage until the existing live writer replaces them before Map.
    /// Upload ranges still include them; optional Sub arrays are excluded.</summary>
    public D3D11PrimaryForwardNumericPacket PrepareFreshRetained(
        D3D11PrimaryForwardRetainedFields current, DeferredClusterFloatLogPath logarithmPath)
    {
        if (current.CurrentSubShadowCount != 0)
            throw new NotSupportedException("Forward optional Sub rows require their actual current producer.");
        var lighting = current.Lighting.Lighting ?? throw new InvalidDataException("Current Forward base lights are absent.");
        var camera = current.Lighting.Camera ?? throw new InvalidDataException("Current Forward camera is absent.");
        var fog = current.Lighting.Fog ?? throw new InvalidDataException("Current Forward fog is absent.");
        var ambient = current.Lighting.Ambient ?? throw new InvalidDataException("Current Forward ambient modifier is absent.");
        var occurrence = current.Lighting.Occlusion ?? throw new InvalidDataException("Current Forward occlusion is absent.");
        if (lighting.Lights.Count is < 1 or > 4 || fog.Scattering.Count != 4 ||
            current.PatternPlaneSub.Count != 2 || current.AmbientTransformRows.Count != 3)
            throw new InvalidDataException("Forward retained vectors require their complete typed groups.");
        var clusterValues = PrepareCurrentCluster(current.Main, logarithmPath);
        if (clusterValues.Scaling is not float scaling || clusterValues.FadeDistance is not Vector2 fade)
            throw new NotSupportedException("Forward retained scaling/fade needs its owner when the module disables these writes.");
        var volume = DeferredSceneConstants.PrepareVolumeFog(current.Main.Near, current.Main.Far,
            current.Main.ReverseDepth, current.Main.VolumePlugin28, current.Main.VolumePlugin2C);
        var values = new Dictionary<string, uint[]>(StringComparer.Ordinal);
        void Floats(string name, params float[] words) => values.Add(name, words.Select(BitConverter.SingleToUInt32Bits).ToArray());
        void Vectors(string name, IEnumerable<Vector4> vectors) => Floats(name, vectors.SelectMany(Components).ToArray());
        void Vector(string name, Vector4 vector) => Floats(name, Components(vector));
        Vector4[] directions = new Vector4[4], colors = new Vector4[4]; float[] rates = new float[4];
        for (int i = 0; i < 4; i++)
        {
            var light = i < lighting.Lights.Count ? lighting.Lights[i] : null;
            directions[i] = light?.Direction ?? new(0, 1, 0, 0);
            colors[i] = light?.Color ?? Vector4.Zero; rates[i] = light?.Occurrence ?? 1;
        }
        Vectors("litDir", directions); Vectors("litCol", colors); Floats("litOcc", rates);
        Vector("ambLit", lighting.Ambient); values.Add("nDirLit", [unchecked((uint)(lighting.Lights.Count - 1))]);
        Vector("vEye", camera.SourceEye); Floats("vOccRate", occurrence.Rates.X, occurrence.Rates.Y);
        Vector("vClstZPrm", clusterValues.DepthParameters);
        values.Add("nClstPtch", [unchecked((uint)clusterValues.HorizontalPatchCount), unchecked((uint)clusterValues.PatchCount)]);
        Floats("fClstScl", scaling); Floats("vFadeDist", fade.X, fade.Y); Vector("gVfogInfo", volume.Info);
        Vectors("fPtnPlSub", current.PatternPlaneSub); values.Add("nSubSdw", [current.CurrentSubShadowCount]);
        Vector("scSubSM", current.SubShadowScale); Vector("fog", fog.Coefficients); Vector("cFog", fog.Color);
        Vectors("Scat", fog.Scattering); Floats("fAmbMapsDORate", ambient.MapsDirectionalOcclusionRate);
        Vectors("mW2E", current.AmbientTransformRows);
        Vector("vAmbDifScl", ambient.DiffuseScale); Vector("vAmbSpcScl", ambient.SpecularScale);
        var vertex = Project(vertexDefinitions, values, false,
            new HashSet<string>(["mW2P", "mL2WT", "mL2W", "mW2Pt", "mW2S"], StringComparer.Ordinal));
        var pixel = Project(pixelDefinitions, values, true,
            new HashSet<string>(["cSpec", "vATest", "fPtn", "vSMOf", "vS2VZ", "vSMDF", "scSM", "nSSLoop", "vLPlane", "sdplane"], StringComparer.Ordinal));
        return new(vertex.Bytes, pixel.Bytes, vertex.Writes, pixel.Writes);
    }

    public static void WriteNativeTemplate(string directory, D3D11PrimaryForwardNumericPacket packet)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "vs-b0.bin"), packet.Vertex);
        File.WriteAllBytes(Path.Combine(directory, "ps-b0.bin"), packet.Pixel);
    }

    /// <summary>Numeric upload lanes, shared by every template using these selected stages.</summary>
    public static void WriteNativeRanges(string baseDirectory, D3D11PrimaryForwardNumericPacket packet)
    {
        Directory.CreateDirectory(baseDirectory);
        File.WriteAllLines(Path.Combine(baseDirectory, "vs-defined-ranges.txt"), packet.VertexWrites.Select(w => $"{w.Offset} {w.ByteCount}"));
        File.WriteAllLines(Path.Combine(baseDirectory, "ps-defined-ranges.txt"), packet.PixelWrites.Select(w => $"{w.Offset} {w.ByteCount}"));
    }

    /// <summary>Initial retained node71/Sub allocations. Cluster lists remain
    /// undefined GPU storage; this writes descriptors, never initialized list contents.</summary>
    public static void WriteNativeAllocation(string directory, D3D11PrimaryForwardAllocationFields fields,
        int viewportWidth, int viewportHeight)
    {
        if (fields.ClusterMode is not (1 or 2) || fields.MaxPointLights == 0 && fields.MaxSpotLights == 0 ||
            fields.IndicesCapacity == 0 || fields.ClusterSizeX == 0 || fields.ClusterSizeY == 0 || fields.ClusterZCount == 0)
            throw new NotSupportedException("Forward cluster allocation gate is closed for the selected View.");
        uint elementBytes = fields.MaxPointLights > 65535 || fields.MaxSpotLights > 65535 ? 4u : 2u;
        uint x = checked((uint)MathF.Ceiling((float)viewportWidth / fields.ClusterSizeX));
        uint y = checked((uint)MathF.Ceiling((float)viewportHeight / fields.ClusterSizeY));
        uint z = fields.ClusterMode == 1 ? fields.ClusterZCount : 1;
        uint bytes = unchecked(((8 / elementBytes) * x * y * z + fields.IndicesCapacity) * elementBytes);
        uint indexFormat = elementBytes == 4 ? 42u : 57u;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "allocation.txt"), FormattableString.Invariant(
            $"{bytes} {indexFormat} {bytes / elementBytes} 4 42 1 {fields.SubShadowWidth} {fields.SubShadowHeight} {fields.SubShadowLayers} {fields.SubShadowFormats.ResourceFormat} {fields.SubShadowFormats.ShaderViewFormat} {fields.SubShadowFormats.TargetViewFormat}\n"));
    }

    /// <summary>Successful fresh point/spot count-zero owners after borrowing
    /// their actual shadow metadata. The unused matrices/tails remain unwritten.</summary>
    public void WriteNativeFreshCountZeroSparseInputs(string directory,
        ushort actualPointMetadataWidth, ushort actualSpotMetadataWidth)
    {
        Directory.CreateDirectory(directory);
        var rows = new List<string>();
        Write(1, "SplsCB", pointSoftShadowType, actualPointMetadataWidth);
        Write(2, "SslsCB", spotSoftShadowType, actualSpotMetadataWidth);
        File.WriteAllLines(Path.Combine(directory, "sparse-cbuffers.txt"), rows);
        void Write(int slot, string name, uint declaredSoft, ushort width)
        {
            var buffer = pixelDefinitions.Buffers.Single(b => b.Name.Text == name);
            uint soft = declaredSoft == 0x7FFFFFFF ? 1 : declaredSoft;
            byte[] header = new byte[12]; BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), soft);
            float inverse = Divide(1, width);
            float[] values = [inverse, width, Multiply(inverse, -1.5f), Add(inverse, inverse)];
            byte[] size = new byte[16];
            for (int i = 0; i < 4; i++) BinaryPrimitives.WriteSingleLittleEndian(size.AsSpan(i * 4, 4), values[i]);
            File.WriteAllBytes(Path.Combine(directory, $"ps-b{slot}-header12.bin"), header);
            File.WriteAllBytes(Path.Combine(directory, $"ps-b{slot}-size16.bin"), size);
            rows.Add($"{slot} {buffer.ByteSize} 0 12 ps-b{slot}-header12.bin");
            rows.Add($"{slot} {buffer.ByteSize} 16 16 ps-b{slot}-size16.bin");
        }
    }

    private static uint Parameter(G1ShaderConfigurationRecord module, string name)
    {
        // G1S uses ResourceNameHash (base27), not the distinct 3SPK
        // primary parameter hash (weighted base31).
        uint hash = 0;
        foreach (char value in name) hash = unchecked(hash * 27 + (uint)(sbyte)value);
        return module.Find(hash)?.RawValue
            ?? throw new InvalidDataException("Forward module property is absent: " + module.Name + "." + name);
    }

    private static (byte[] Bytes, IReadOnlyList<D3D11PrimaryForwardWrite> Writes) Project(
        DxbcResourceDefinitions definitions, IReadOnlyDictionary<string, uint[]> values, bool optionalSubAbsent,
        IReadOnlySet<string>? liveFields = null)
    {
        var buffer = definitions.Buffers.Single(b => b.Name.Text == "$Globals");
        byte[] bytes = new byte[buffer.ByteSize]; var writes = new List<D3D11PrimaryForwardWrite>();
        foreach (var variable in buffer.Variables)
        {
            if (optionalSubAbsent && variable.Name.Text is ("mSubW2S" or "vSubLPlane")) continue;
            bool live = liveFields?.Contains(variable.Name.Text) == true;
            if (!values.TryGetValue(variable.Name.Text, out var words) && !live)
                throw new NotSupportedException("Current Forward numeric producer is absent: " + variable.Name.Text);
            var type = definitions.Types.Single(t => t.Offset == variable.TypeOffset);
            int count = Math.Max(1, (int)type.Elements), width = type.Rows * type.Columns;
            int stride = type.Elements == 0 ? width * 4 : Math.Max(16, width * 4);
            if ((!live && words!.Length != count * width) || variable.ByteSize != (count - 1) * stride + width * 4)
                throw new InvalidDataException("Forward numeric field has an unsupported resource shape: " + variable.Name.Text);
            for (int i = 0; i < count; i++)
            {
                int at = variable.StartOffset + i * stride;
                if (!live) for (int j = 0; j < width; j++)
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + j * 4, 4), words![i * width + j]);
                writes.Add(new(at, width * 4));
            }
        }
        return (bytes, writes.AsReadOnly());
    }

    private static float[] Components(Vector4 v) => [v.X, v.Y, v.Z, v.W];
    private static float[] Components(Matrix4x4 m) =>
        [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
         m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float a, float b) => a * b;
}

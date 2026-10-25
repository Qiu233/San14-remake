using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current node71 inputs after its Camera and viewport preparation.
/// DepthBoundaries contains the seventeen actual 9A9D30 results, including
/// Camera near at zero and the current global endpoints at one and sixteen.</summary>
public sealed record G1EffectDirectLightingClusterInputs(Matrix4x4 WorldToView,
    Matrix4x4 Projection, bool Orthographic, uint ScreenWidth, uint ScreenHeight,
    uint PointCount, uint SpotCount, uint IndexDxgiFormat,
    ReadOnlyMemory<float> DepthBoundaries, DeferredClusterInputs? ConsumerInputs = null);

public readonly record struct G1EffectDirectLightingClusterGroups(uint X, uint Y, uint Z);

/// <summary>Normal CLC programs and current uploads. This packet does not claim
/// that the producer buffers exist or that either dispatch has executed.</summary>
public sealed record G1EffectDirectLightingClusterPrepared(LoadedShaderContainer Source,
    int VariantRow, ReadOnlyMemory<byte> Variant44, G1EffectGpuStage Reset,
    G1EffectGpuStage Cull, ReadOnlyMemory<byte> ResetConstants,
    ReadOnlyMemory<byte> CullConstants, G1EffectDirectLightingClusterGroups ResetGroups,
    G1EffectDirectLightingClusterGroups CullGroups, uint HeaderScalarCount,
    IReadOnlyList<G1EffectGpuMissing> Missing)
{
    public bool IsReady => Missing.Count == 0;
    public DeferredClusterInputs? ConsumerInputs { get; init; }
}

/// <summary>The ordinary node71 CLC owner. Its initial condition is copied
/// from the source's first variant exactly as 981F00 initializes the decorator;
/// current Camera, index format and depth writers then select the actual row.</summary>
public sealed class G1EffectDirectLightingCluster
{
    private readonly G1ShaderBindingTables bindings;
    private readonly Dictionary<int, G1EffectGpuStage> programs = [];
    public G1EffectDirectLightingCluster(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        Source = repository.LoadShaderContainer("system", 0x5C3D4FCE);
        bindings = Source.ReadG1Bindings();
        if (bindings.PassCount != 2 || bindings.Variants.RecordWidth != 44 ||
            bindings.Variants.Records.Count == 0)
            throw new InvalidDataException("Normal CLC requires its two passes and 44-byte source condition.");
    }
    public LoadedShaderContainer Source { get; }

    /// <summary>Current Main global endpoints used by both node71's depth
    /// boundaries and the selected ClusterLightList consumer.</summary>
    public static (float Start, float End) PrepareEndpoints(D3D11PrimaryForwardMainFields main)
    {
        ArgumentNullException.ThrowIfNull(main);
        float interval=(main.Far-main.Near)/10;
        float start=main.Near+MathF.Min(100,interval);
        if(start<main.ClusterStartOverride)start=main.ClusterStartOverride;
        float end=main.Far;
        if(main.ClusterEndOverride<end)end=MathF.Max(start,main.ClusterEndOverride);
        return (start,end);
    }

    /// <summary>Uses this consumer's normal plugin properties, rather than
    /// borrowing a forward shader's configuration for a different HDR bank.</summary>
    public static DeferredClusterInputs PrepareConsumerInputs(LoadedShaderContainer consumer,
        D3D11PrimaryForwardMainFields main, DeferredClusterFloatLogPath logarithmPath)
    {
        ArgumentNullException.ThrowIfNull(consumer);ArgumentNullException.ThrowIfNull(main);
        var module=consumer.ReadConfigurations().Records.Single(record=>record.Name=="ClusterLightList");
        uint Property(string name)=>module.Find(name)?.RawValue ??
            throw new InvalidDataException("Current ClusterLightList property is absent: "+name);
        var endpoints=PrepareEndpoints(main);
        return DeferredClusterMath.Prepare(main.ViewportWidth,main.ViewportHeight,
            checked((int)Property("ClusterSize")),checked((int)Property("ClusterZNum")),
            main.ClusterScalingDivisor,DeferredClusterLogMath.Prepare(endpoints.Start,endpoints.End,logarithmPath),
            main.Near,main.Far,main.ReverseDepth,Property("Scaling")!=0,Property("FadeOut")!=0,endpoints.End,2000);
    }

    /// <summary>9A9D30's seventeen mathematical depth boundaries. Existing
    /// project-owned logarithm/power implementations supply the elementary
    /// functions; original SIMD versus scalar library rounding is not asserted.</summary>
    public static float[] PrepareDepthBoundaries(float cameraNear,float start,float end,
        DeferredClusterFloatLogPath logarithmPath,SkyWeatherNativePowerSource powerSource)
    {
        ArgumentNullException.ThrowIfNull(powerSource);
        if(!float.IsFinite(cameraNear)||cameraNear<=0||start<=0||end<=start)
            throw new InvalidDataException("CLC depth generation requires a positive Camera near and ordered current endpoints.");
        var logs=DeferredClusterLogMath.Prepare(start,end,logarithmPath);
        float logStart=logs.LogStart/logs.LogTwo,logEnd=logs.LogEnd/logs.LogTwo;
        float step=-(logStart-logEnd)/15;
        float origin=logStart-step;
        float[] values=new float[17];values[0]=cameraNear;values[1]=start;values[16]=end;
        for(int i=2;i<16;i++)
        {
            float product=i*step;
            float exponent=product+origin;
            values[i]=SkyWeatherNativePower.PowSse(2,exponent,powerSource);
        }
        return values;
    }

    /// <summary>Normal current Main preparation. The selected consumer shader
    /// owns its actual ClusterLightList properties; input viewport dimensions
    /// have already passed through node71's target/Camera scaling.</summary>
    public G1EffectDirectLightingClusterPrepared PrepareCurrent(G1EffectDirectLightingClusterInputs input,
        LoadedShaderContainer consumer,D3D11PrimaryForwardMainFields main,
        DeferredClusterFloatLogPath logarithmPath,SkyWeatherNativePowerSource powerSource)
    {
        var endpoints=PrepareEndpoints(main);
        return Prepare(input with {
            DepthBoundaries=PrepareDepthBoundaries(main.Near,endpoints.Start,endpoints.End,logarithmPath,powerSource),
            ConsumerInputs=PrepareConsumerInputs(consumer,main,logarithmPath)
        });
    }

    public G1EffectDirectLightingClusterPrepared Prepare(G1EffectDirectLightingClusterInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ScreenWidth == 0 || input.ScreenHeight == 0)
            throw new InvalidDataException("CLC dispatch requires the current positive prepared viewport.");
        if (input.IndexDxgiFormat is not (42 or 57))
            throw new InvalidDataException("CLC source indices require R32_UINT or R16_UINT.");
        byte[] condition = bindings.Variants.Records[0].RawBytes.ToArray();
        U(condition, 8, 16); // 994100, seventeen current boundaries.
        U(condition, 0x10, input.IndexDxgiFormat == 42 ? 1u : 0u); // D249C0.
        U(condition, 0x14, 0); // D249C0 ordinary UAV path.
        U(condition, 0x28, input.Orthographic ? 1u : 0u); // 993D50, current Camera148 bit0.
        var row = bindings.Variants.Match(condition) ??
            throw new NotSupportedException("Current CLC condition has no original shader variant.");
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(condition.AsSpan(4));
        uint depth = BinaryPrimitives.ReadUInt32LittleEndian(condition.AsSpan(8));
        if (size == 0 || depth == 0) throw new InvalidDataException("CLC source grid contains a zero dimension.");
        uint x = checked((input.ScreenWidth - 1 + size) / size);
        uint y = checked((input.ScreenHeight - 1 + size) / size);
        uint headers = checked(x * y * depth * (input.IndexDxgiFormat == 42 ? 2u : 4u));
        var reset = Stage(row.Index, 0); var cull = Stage(row.Index, 1);
        var missing = new List<G1EffectGpuMissing>();
        if(input.ConsumerInputs is null)
            missing.Add(new("CLC ClusterLightList", "Current consumer depth, patch, scaling and fade values are absent."));
        byte[] resetBytes = new byte[16], cullBytes = new byte[400];
        U(resetBytes, 0, headers); // 9931E0 nClrBuf, in typed scalar elements, not bytes.
        if (input.DepthBoundaries.Length != 17)
            missing.Add(new("CLC fClstZ", "Current 9A9D30 seventeen-boundary producer is absent."));
        else
        {
            var values = input.DepthBoundaries.Span;
            for (int i = 0; i < values.Length; i++)
            {
                if (!float.IsFinite(values[i])) throw new InvalidDataException("CLC depth boundary is nonfinite.");
                F(cullBytes, 64 + i * 16, values[i]); // Actual scalar-array CB stride.
            }
        }
        Matrix4x4 worldView = Matrix4x4.Transpose(input.WorldToView);
        WriteMatrix(cullBytes.AsSpan(0, 48), worldView, 12);
        U(cullBytes, 48, input.PointCount); U(cullBytes, 52, input.SpotCount);
        U(cullBytes, 56, input.ScreenWidth); U(cullBytes, 60, input.ScreenHeight);
        Matrix4x4 inverseProjection = NativeMatrixMath.Invert(input.Projection, out _);
        WriteMatrix(cullBytes.AsSpan(336, 64), inverseProjection, 16);
        RequireLayout(reset, 16, [("nClrBuf", 0, 4)]);
        RequireLayout(cull, 400, [("mWV", 0, 48), ("nPntLightNum", 48, 4),
            ("nSptLightNum", 52, 4), ("vScrnSize", 56, 8), ("fClstZ", 64, 260), ("mP2V", 336, 64)]);
        return new(Source, row.Index, condition, reset, cull,
            missing.Count == 0 ? resetBytes : ReadOnlyMemory<byte>.Empty,
            missing.Count == 0 ? cullBytes : ReadOnlyMemory<byte>.Empty,
            new(1, 1, 1), new(x, y, depth), headers, missing.AsReadOnly()) { ConsumerInputs=input.ConsumerInputs };
    }

    private G1EffectGpuStage Stage(int variant, uint pass)
    {
        var combination = bindings.ResolveCombination([0, 0, 0, 0, 0, 0, (uint)variant], pass);
        var active = combination.Stages.Where(stage => stage.Entry is not null).ToArray();
        if (active.Length != 1 || active[0].StageCode != 5)
            throw new NotSupportedException("Normal CLC pass requires its sole original compute stage.");
        int index = active[0].Entry!.ProgramIndex;
        if (!programs.TryGetValue(index, out var program))
        {
            program = new(5, index, Source.Container.GetProgramBytes(Source.Source.Bytes, index).ToArray(),
                Source.ReadSignatures(index), Source.ReadResourceDefinitions(index));
            programs.Add(index, program);
        }
        return program;
    }
    private static void RequireLayout(G1EffectGpuStage stage, int bytes,
        (string Name, int Offset, int Bytes)[] expected)
    {
        var buffers = stage.ResourceDefinitions.Buffers;
        if (buffers.Count != 1 || buffers[0].ByteSize != bytes || buffers[0].Variables.Count != expected.Length ||
            expected.Any(field => !buffers[0].Variables.Any(variable => variable.Name.Text == field.Name &&
                variable.StartOffset == field.Offset && variable.ByteSize == field.Bytes)))
            throw new NotSupportedException("Normal CLC current constant layout differs from the closed source writer.");
    }
    private static void U(Span<byte> bytes, int offset, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], value);
    private static void F(Span<byte> bytes, int offset, float value)
        => BinaryPrimitives.WriteSingleLittleEndian(bytes[offset..], value);
    private static void WriteMatrix(Span<byte> bytes, Matrix4x4 matrix, int count)
    {
        float[] values = [matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24, matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44];
        for (int i = 0; i < count; i++) F(bytes, i * 4, values[i]);
    }
}

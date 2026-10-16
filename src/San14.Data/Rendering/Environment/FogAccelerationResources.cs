using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The first accepted camera and light values for the two fog reduction draws.</summary>
public sealed class FogAccelerationInitialFrame
{
    internal FogAccelerationInitialFrame(byte[] firstPixel, byte[] vertex, byte[] pixel)
        => (FirstPixel, Vertex, Pixel) = (firstPixel, vertex, pixel);
    public ReadOnlyMemory<byte> FirstPixel { get; }
    public ReadOnlyMemory<byte> Vertex { get; }
    public ReadOnlyMemory<byte> Pixel { get; }
}

/// <summary>Imported node-one fog reduction programs and their named constant layouts.
/// Current camera and light preparation does not reload this resource owner.</summary>
public sealed partial class FogAccelerationResources
{
    private sealed record ProgramPair(int Vertex, int Pixel);
    private readonly ProgramPair[] programs;
    public LoadedShaderContainer Shader { get; }
    public RenderTextureDefinition Target { get; }
    public RenderTextureFormats Formats { get; }
    public int RendererFogMode { get; }
    public int Profile { get; }
    public bool FogEnabled { get; }

    private FogAccelerationResources(GameResourceRepository repository)
    {
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var node = graph.Document.Nodes.Single(value => value.Index == 1);
        uint Reference(uint hash)
        {
            var field = node.Call.Arguments.Single(value => value.NameHash == hash);
            if (field.DescriptorWord != 0x05000001 || field.RawValue.Length != 4)
                throw new InvalidDataException("Fog acceleration requires scalar resource references.");
            return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
        var declaration = ModelShaderResources.ReadDeclaredShader(
            repository.LoadObjects("system", 0x718CCB8B), Reference(0x3B0FDD5F));
        Shader = ModelShaderResources.LoadDeclaredG1S(repository, declaration);
        Target = RenderTextureResources.Read(repository.LoadObjects("system", 0x93FB3280), Reference(0xC8803F5B));
        if (Target.Kind != RenderTextureKind.Texture2D || Target.Width != 1 || Target.Height != 1 ||
            Target.LevelNum != 1 || Target.MinMipLevel != 0 || Target.MultiSample != 0)
            throw new NotSupportedException("Fog acceleration requires its single-mip one-by-one target.");
        Formats = RenderTextureFormatMath.Resolve(Target);
        // This fixed shader has no binding-variant records and declares zero
        // variant width. Import its feature and actual combinations independently
        // of the general variant selector, as for the fixed Ocean program pair.
        var directory = G1ShaderFeatureRowReader.ReadFixedPasses(Shader.Source.Bytes,
            Shader.Container, Shader.ReadCombinations());
        if (directory.Passes.Count != 2)
            throw new NotSupportedException("Fog acceleration requires two declared reduction passes.");
        var feature = directory.Feature.Words;
        RendererFogMode = checked((int)feature[6]); Profile = checked((int)feature[7]); FogEnabled = feature[5] != 0;
        if (RendererFogMode != 7 || Profile != 2)
            throw new NotSupportedException("Fog acceleration supports the declared mode-seven profile-two reduction.");
        programs = Enumerable.Range(0, 2).Select(pass =>
        {
            var combination = directory.Passes[pass];
            var stages = combination.Stages.Where(stage => stage.Entry is not null).ToArray();
            if (stages.Length != 2 || stages.Count(stage => stage.StageCode == 1) != 1 ||
                stages.Count(stage => stage.StageCode == 0) != 1)
                throw new NotSupportedException("Fog acceleration requires one declared vertex and pixel stage per pass.");
            return new ProgramPair(stages.Single(stage => stage.StageCode == 1).Entry!.ProgramIndex,
                stages.Single(stage => stage.StageCode == 0).Entry!.ProgramIndex);
        }).ToArray();
        foreach (var program in programs)
        {
            var inputs = Shader.ReadSignatures(program.Vertex).Parts.Where(part => part.Kind == DxbcSignatureKind.Input)
                .SelectMany(part => part.Parameters);
            if (inputs.Any(input => input.RawSystemValue != 6))
                throw new NotSupportedException("Fog acceleration vertex input requires a geometry producer.");
        }
        var firstVertex = Shader.ReadResourceDefinitions(programs[0].Vertex);
        if (firstVertex.Bindings.Count != 0 || firstVertex.Buffers.Count != 0)
            throw new NotSupportedException("Fog acceleration initialization vertex stage has unsupported resources.");
        RequireLayout(programs[0].Pixel, 16, ("fMaxDistance", 0, 1, 1, 0), ("fExtinctThreshold", 4, 1, 1, 0));
        RequireLayout(programs[1].Vertex, 144, ("mW2P", 0, 4, 4, 0), ("vViewParam", 64, 1, 2, 0),
            ("fInterval", 72, 1, 1, 0), ("mV2W", 80, 4, 4, 0));
        RequireLayout(programs[1].Pixel, 112, ("vEye", 0, 1, 4, 0), ("fog", 16, 1, 4, 0),
            ("Scat", 32, 1, 4, 4), ("fExtinctThreshold", 96, 1, 1, 0));
    }

    public static FogAccelerationResources Load(GameResourceRepository repository) => new(repository);

    private void RequireLayout(int program, int length,
        params (string Name, int Offset, int Rows, int Columns, int Elements)[] fields)
    {
        var definitions = Shader.ReadResourceDefinitions(program);
        var buffer = definitions.Buffers.Single();
        var binding = definitions.Bindings.Single();
        if (buffer.ByteSize != length || buffer.Variables.Count != fields.Length || binding.InputType != 0 ||
            binding.BindPoint != 0 || binding.BindCount != 1 || binding.Name.Text != buffer.Name.Text)
            throw new NotSupportedException("Fog acceleration constant interface differs from its native consumer.");
        foreach (var field in fields)
        {
            var variable = buffer.Variables.Single(value => value.Name.Text == field.Name);
            var type = definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            int count = field.Rows * field.Columns * Math.Max(1, field.Elements);
            bool expectedClass = field.Rows > 1 ? type.Class == 2 : type.Class == (field.Columns > 1 ? 1 : 0);
            if (variable.StartOffset != field.Offset || variable.ByteSize != count * 4 || type.BaseType != 3 ||
                type.Rows != field.Rows || type.Columns != field.Columns || type.Elements != field.Elements ||
                type.Members.Count != 0 || !expectedClass)
                throw new NotSupportedException("Fog acceleration named constant layout differs: " + field.Name);
        }
    }

    public FogAccelerationInitialFrame PrepareInitialFrame(DirectionalLightFogState fog,
        Vector3 directionalRgb, MapCameraConstructionPacket camera)
    {
        var projection = SkyFogMath.GenerateWeather(fog.Mode, fog.Values, Profile, FogEnabled);
        var scattering = projection.ScatteringOverride ?? SkyScatteringMath.GenerateWeather(fog.Values, directionalRgb);
        const float interval = 100f;
        float threshold = NativeFloat.Subtract(1f, BitConverter.UInt32BitsToSingle(0x3B808081));
        float tangent = NativeCameraTangent.Tangent1020A90(
            NativeFloat.Multiply(camera.Lens.VerticalFovRadians, .5f), NativeCameraTangentDispatch.Sse);
        byte[] first = new byte[16], vertex = new byte[144], pixel = new byte[112];
        Write(first, 0, NativeFloat.Multiply(100f, interval), threshold);
        Write(vertex, 0, Words(camera.SourceWorldToProjection));
        Write(vertex, 64, NativeFloat.Multiply(tangent, camera.Lens.Aspect), tangent, interval);
        Write(vertex, 80, Words(NativeMatrixMath.Invert(camera.SourceWorldToView, out _)));
        Write(pixel, 0, Words(camera.Geometry.Eye)); Write(pixel, 16, Words(projection.Value));
        Write(pixel, 32, scattering.Values.SelectMany(Words).ToArray()); Write(pixel, 96, threshold);
        return new(first, vertex, pixel);
    }

    private static float[] Words(Vector4 value) => [value.X, value.Y, value.Z, value.W];
    private static float[] Words(Matrix4x4 value) => [value.M11, value.M12, value.M13, value.M14,
        value.M21, value.M22, value.M23, value.M24, value.M31, value.M32, value.M33, value.M34,
        value.M41, value.M42, value.M43, value.M44];
    private static void Write(byte[] destination, int offset, params float[] values)
    {
        foreach (float value in values)
        {
            if (!float.IsFinite(value)) throw new InvalidDataException("Nonfinite fog acceleration constant.");
            BinaryPrimitives.WriteSingleLittleEndian(destination.AsSpan(offset, 4), value); offset += 4;
        }
    }
}

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum SkyAmbientOperation : uint { Copy, Filter, Blend }

/// <summary>The actual SkyAmbient shader's requested sample variants and
/// imported copy, filter and blend programs. Each interface is read once.</summary>
public sealed class SkyAmbientProgramResources
{
    private static readonly ConditionalWeakTable<LoadedShaderContainer, SkyAmbientProgramResources> imported = new();
    private readonly G1ShaderBindingTables tables;
    private readonly ConcurrentDictionary<uint, SkyAmbientFilterSelection> selections = new();
    private readonly ConcurrentDictionary<(SkyAmbientOperation, int), SkyAmbientProgramSelection> programs = new();
    private readonly ConcurrentDictionary<int, DxbcResourceDefinitions> definitions = new();
    private SkyAmbientProgramResources(LoadedShaderContainer shader)
    {
        Shader = shader;
        tables = shader.ReadG1Bindings();
        if (tables.Variants.RecordWidth != 4)
            throw new NotSupportedException("SkyAmbient sample variants require a DWORD request.");
    }
    public LoadedShaderContainer Shader { get; }
    public static SkyAmbientProgramResources Load(LoadedSkyAmbientResources ambient) => Import(ambient.Shader);
    public static SkyAmbientProgramResources Import(LoadedShaderContainer shader)
        => imported.GetValue(shader, static source => new(source));

    public SkyAmbientFilterSelection Select(uint requestedSampleCount)
        => selections.GetOrAdd(requestedSampleCount, count =>
        {
            Span<byte> requested = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(requested, count);
            var variant = tables.Variants.Match(requested) ??
                throw new NotSupportedException("Requested SkyAmbient sample count has no resource variant; retained-variant fallback is not supplied.");
            uint[] indices = [0, 0, 0, 0, 0, 0, (uint)variant.Index];
            return new(this, count, variant.Index,
                Program(SkyAmbientOperation.Copy, tables.ResolveCombination(indices, 9)),
                Program(SkyAmbientOperation.Filter, tables.ResolveCombination(indices, 10)),
                Program(SkyAmbientOperation.Blend, tables.ResolveCombination(indices, 7)));
        });

    internal SkyAmbientFilterSelection FromCombination(int combinationIndex, SkyAmbientOperation operation)
    {
        foreach (var variant in tables.Variants.Records)
        {
            uint[] indices = [0, 0, 0, 0, 0, 0, (uint)variant.Index];
            uint pass = operation switch { SkyAmbientOperation.Copy => 9u, SkyAmbientOperation.Filter => 10u,
                SkyAmbientOperation.Blend => 7u, _ => throw new ArgumentOutOfRangeException(nameof(operation)) };
            if (tables.ResolveCombination(indices, pass).Index == combinationIndex)
                return Select(BinaryPrimitives.ReadUInt32LittleEndian(variant.RawBytes));
        }
        throw new NotSupportedException("Explicit SkyAmbient combination does not belong to the requested operation.");
    }

    internal DxbcResourceDefinitions Definitions(int index)
        => definitions.GetOrAdd(index, Shader.ReadResourceDefinitions);
    private SkyAmbientProgramSelection Program(SkyAmbientOperation operation, G2ShaderCombination combination)
        => programs.GetOrAdd((operation, combination.Index), _ => new(this, operation, combination));
}

/// <summary>An actual selected pair and its named material and sampled roles.</summary>
public sealed class SkyAmbientProgramSelection
{
    private readonly byte[] vertex, pixel;
    internal SkyAmbientProgramSelection(SkyAmbientProgramResources resources,
        SkyAmbientOperation operation, G2ShaderCombination combination)
    {
        Resources = resources; Operation = operation; Combination = combination;
        if (combination.Stages.Count(value => value.Entry is not null) != 2 ||
            combination.Stages.Any(value => value.Entry is not null && value.StageCode is not (0 or 1)))
            throw new NotSupportedException("SkyAmbient operations require a vertex and pixel pair.");
        VertexProgram = combination.Stages.Single(value => value.StageCode == 1).Entry!.ProgramIndex;
        PixelProgram = combination.Stages.Single(value => value.StageCode == 0).Entry!.ProgramIndex;
        var shader = resources.Shader;
        vertex = shader.Container.GetProgramBytes(shader.Source.Bytes, VertexProgram).ToArray();
        pixel = shader.Container.GetProgramBytes(shader.Source.Bytes, PixelProgram).ToArray();
        VertexDefinitions = resources.Definitions(VertexProgram);
        PixelDefinitions = resources.Definitions(PixelProgram);
        MaterialLayout = SkyAmbientMaterialLayout.Import(operation, VertexDefinitions, PixelDefinitions);
        PixelSha256 = Convert.ToHexString(SHA256.HashData(pixel));
        RequireSignatures();
    }
    public SkyAmbientProgramResources Resources { get; }
    public SkyAmbientOperation Operation { get; }
    public G2ShaderCombination Combination { get; }
    public int VertexProgram { get; }
    public int PixelProgram { get; }
    public DxbcResourceDefinitions VertexDefinitions { get; }
    public DxbcResourceDefinitions PixelDefinitions { get; }
    public SkyAmbientMaterialLayout MaterialLayout { get; }
    public string PixelSha256 { get; }
    public byte[] CopyVertexBytes() => vertex.ToArray();
    public byte[] CopyPixelBytes() => pixel.ToArray();

    private void RequireSignatures()
    {
        var vs = DxbcSignatureReader.Read(vertex);
        var ps = DxbcSignatureReader.Read(pixel);
        var inputs = vs.Parts.Single(part => part.Kind == DxbcSignatureKind.Input).Parameters;
        if (inputs.Count != 1 || inputs[0].RawSystemValue != 6 || inputs[0].SemanticIndex != 0 ||
            !inputs[0].SemanticName.Text.Equals("SV_VertexID", StringComparison.OrdinalIgnoreCase) ||
            inputs[0].ComponentType != 1 || inputs[0].Mask != 1)
            throw new NotSupportedException("SkyAmbient geometry requires its vertex-ID draw input.");
        var produced = vs.Parts.Single(part => part.Kind == DxbcSignatureKind.Output).Parameters;
        var consumed = ps.Parts.Single(part => part.Kind == DxbcSignatureKind.Input).Parameters;
        if (consumed.Any(input => !produced.Any(output =>
            output.SemanticName.Text.Equals(input.SemanticName.Text, StringComparison.OrdinalIgnoreCase) &&
            output.SemanticIndex == input.SemanticIndex && output.ComponentType == input.ComponentType &&
            (output.Mask & input.Mask) == input.Mask)))
            throw new NotSupportedException("SkyAmbient selected vertex and pixel interfaces do not link.");
        var targets = ps.Parts.Single(part => part.Kind == DxbcSignatureKind.Output).Parameters;
        if (targets.Count != 1 || targets[0].SemanticIndex != 0 || targets[0].Register != 0 ||
            !targets[0].SemanticName.Text.Equals("SV_Target", StringComparison.OrdinalIgnoreCase) ||
            targets[0].ComponentType != 3 || targets[0].Mask != 15)
            throw new NotSupportedException("SkyAmbient operations require a single float4 target output.");
    }
}

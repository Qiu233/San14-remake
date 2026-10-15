using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The selected generator supported by the CPU zero-brush adapter.
/// This adapter implements one kernel; later editing operations require their
/// own producer. Container names, reflection metadata and program order do not
/// define the kernel's capability.</summary>
public sealed class TerrainInitialHeightGenerator
{
    private const string IdentityKernelCodeSha256 = "D3F9071D3E511AD76E87B1AF2194B208AF92486EA8C87A8ECCD1D107DF51AC5F";
    private TerrainInitialHeightGenerator(LoadedShaderContainer source, G2ShaderCombination combination, int programIndex)
        => (Source, Combination, ProgramIndex) = (source, combination, programIndex);
    public LoadedShaderContainer Source { get; }
    public G2ShaderCombination Combination { get; }
    public int ProgramIndex { get; }

    public static TerrainInitialHeightGenerator Import(LoadedShaderContainer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var bindings = source.ReadG1Bindings();
        if (bindings.PassCount != 1 || bindings.RawDimensionCounts.Take(6).Any(count => count > 1))
            throw new NotSupportedException("Initial height requires a single pass without additional selection inputs.");
        Span<byte> condition = stackalloc byte[8];
        condition.Clear();
        var variant = bindings.Variants.Match(condition)
            ?? throw new InvalidDataException("Height generator has no initial zero-brush variant.");
        uint[] indices = new uint[7];
        indices[6] = (uint)variant.Index;
        var combination = bindings.ResolveCombination(indices, 0);
        var stages = combination.Stages.Where(stage => stage.Entry is not null).ToArray();
        if (stages.Length != 1 || stages[0].StageCode != 5)
            throw new NotSupportedException("Initial height requires one compute stage.");
        int programIndex = stages[0].Entry!.ProgramIndex;
        var program = source.Container.Programs[programIndex];
        var code = program.Container.Parts.Single(part => part.TokenHeader is not null);
        var bytes = source.Container.GetProgramBytes(source.Source.Bytes, programIndex);
        if (Convert.ToHexString(SHA256.HashData(code.PayloadRange.Slice(bytes))) != IdentityKernelCodeSha256)
            throw new NotSupportedException("Selected height kernel requires an initial producer beyond the CPU zero-brush adapter.");
        return new(source, combination, programIndex);
    }
}

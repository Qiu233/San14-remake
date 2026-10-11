using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The current requested sample count selects actual copy, filter and
/// blend combinations and supplies the separate CPU sample producer.</summary>
public sealed class SkyAmbientFilterSelection
{
    internal SkyAmbientFilterSelection(SkyAmbientProgramResources resources, uint count, int variant,
        SkyAmbientProgramSelection copy, SkyAmbientProgramSelection filter, SkyAmbientProgramSelection blend)
        => (Resources, SampleCount, BindingVariantIndex, CopyProgram, FilterProgram, BlendProgram)
            = (resources, count, variant, copy, filter, blend);
    public SkyAmbientProgramResources Resources { get; }
    public LoadedShaderContainer Source => Resources.Shader;
    public uint SampleCount { get; }
    public int BindingVariantIndex { get; }
    public SkyAmbientProgramSelection CopyProgram { get; }
    public SkyAmbientProgramSelection FilterProgram { get; }
    public SkyAmbientProgramSelection BlendProgram { get; }
    public G2ShaderCombination CopyCombination => CopyProgram.Combination;
    public G2ShaderCombination FilterCombination => FilterProgram.Combination;
    public G2ShaderCombination BlendCombination => BlendProgram.Combination;
    public string FilterPixelSha256 => FilterProgram.PixelSha256;
    public string SourceProvenance => Source.Source.ResourceUri + "@" + Source.Source.DecodedSha256 +
        $";requested-count={SampleCount};binding-variant={BindingVariantIndex}";

    public static SkyAmbientFilterSelection SelectExplicitSampleCount(LoadedShaderContainer source, uint sampleCount)
        => SkyAmbientProgramResources.Import(source).Select(sampleCount);

    public static SkyAmbientFilterSelection FromExplicitCombination(LoadedShaderContainer source, int combinationIndex, bool filter)
        => SkyAmbientProgramResources.Import(source).FromCombination(combinationIndex,
            filter ? SkyAmbientOperation.Filter : SkyAmbientOperation.Copy);

    public static SkyAmbientFilterSelection FromExplicitBlendCombination(LoadedShaderContainer source, int combinationIndex)
        => SkyAmbientProgramResources.Import(source).FromCombination(combinationIndex, SkyAmbientOperation.Blend);

    public void WriteNativeInputs(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var program in new[] { CopyProgram, FilterProgram, BlendProgram })
        {
            string name = program.Operation.ToString().ToLowerInvariant();
            File.WriteAllBytes(Path.Combine(directory, name + "-vs.dxbc"), program.CopyVertexBytes());
            File.WriteAllBytes(Path.Combine(directory, name + "-ps.dxbc"), program.CopyPixelBytes());
        }
        SkyAmbientMaterialLayout.WriteNativeInputs(directory, [CopyProgram, FilterProgram, BlendProgram]);
    }
}

/// <summary>A selection borrowed from the actual accepted Main Camera frame.
/// Typed declaration import owns author validation; current state owns the count.</summary>
public sealed class SkyAmbientCurrentFilterSelection
{
    private readonly MapSourceCreatedSeasonLightingFrame present;
    private SkyAmbientCurrentFilterSelection(MapSourceCreatedSeasonLightingFrame present, SkyAmbientFilterSelection selection)
        => (this.present, Selection) = (present, selection);
    public SkyAmbientFilterSelection Selection { get; }
    public long FrameNumber => present.FrameNumber;
    public string SourceProvenance => present.SourceProvenance + ";" + Selection.SourceProvenance;

    public static SkyAmbientCurrentFilterSelection CaptureCurrent(LoadedShaderContainer shader,
        MapSourceCreatedSeasonLightingFrame present)
    {
        ArgumentNullException.ThrowIfNull(present);
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(present.PreparedCameraPrefix.Span.Slice(0x3A4, 4));
        return new(present, SkyAmbientFilterSelection.SelectExplicitSampleCount(shader, count));
    }

    public void ValidateCurrent(MapSourceCreatedSeasonLightingFrame actualPresent)
    {
        if (!ReferenceEquals(present, actualPresent))
            throw new InvalidDataException("SkyAmbient selection belongs to another accepted Camera frame.");
    }

    public void RequireSameProgramSampleCount(SkyAmbientFilterSelection allocatedSelection)
    {
        ArgumentNullException.ThrowIfNull(allocatedSelection);
        var left = Selection.FilterProgram;
        var right = allocatedSelection.FilterProgram;
        if (Selection.SampleCount != allocatedSelection.SampleCount ||
            left.Resources.Shader.Source.ResourceUri != right.Resources.Shader.Source.ResourceUri ||
            left.Resources.Shader.Source.DecodedSha256 != right.Resources.Shader.Source.DecodedSha256 ||
            left.VertexProgram != right.VertexProgram || left.PixelProgram != right.PixelProgram)
            throw new NotSupportedException("Changed SkyAmbient sample buffers or filter programs require scene resource replacement.");
    }
}

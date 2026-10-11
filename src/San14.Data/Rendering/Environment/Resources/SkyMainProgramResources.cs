using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The Main Sky shader declared by the actual named renderer.
/// Numerical preparation selects its combination; this owner imports that
/// selection without substituting a stock file or a previous program index.</summary>
public sealed class SkyMainProgramResources
{
    private static readonly ConditionalWeakTable<LoadedShaderContainer, SkyMainProgramResources> imported = new();
    private readonly ConcurrentDictionary<int, SkyMainProgramSelection> selections = new();
    private SkyMainProgramResources(LoadedShaderContainer shader) => Shader = shader;
    public LoadedShaderContainer Shader { get; }

    public static SkyMainProgramResources Load(GameResourceRepository repository, NamedViewRendererResources renderer)
    {
        var declaration = renderer.Definitions.Document.Objects.Single(value => value.ObjectId == renderer.RendererObjectId);
        var field = declaration.Properties.Single(value => value.NameHash == 0xF5743E16);
        if (field.TypeCode != 5 || field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
            throw new InvalidDataException("The selected Renderer requires a scalar Sky shader reference.");
        uint key = BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        if (key == 0) throw new NotSupportedException("The selected Renderer declares no Main Sky shader.");
        return Import(repository.LoadShaderContainer("system", key));
    }

    public static SkyMainProgramResources Import(LoadedShaderContainer shader)
        => imported.GetValue(shader, static value => new(value));

    public SkyMainProgramSelection ImportSelection(D3D11AtmosphereProgramSelection selection)
    {
        var combinations = Shader.ReadG1Bindings().Combinations.Rows;
        if ((uint)selection.Combination >= combinations.Count)
            throw new InvalidDataException("Main Sky selection lies outside its actual program combinations.");
        var combination = combinations[selection.Combination];
        if (combination.Stages.Count(stage => stage.Entry is not null) != 2 ||
            combination.Stages.Any(stage => stage.Entry is not null && stage.StageCode is not (0 or 1)) ||
            combination.Stages.Single(stage => stage.StageCode == 1).Entry?.ProgramIndex != selection.VertexProgram ||
            combination.Stages.Single(stage => stage.StageCode == 0).Entry?.ProgramIndex != selection.PixelProgram)
            throw new InvalidDataException("Main Sky programs do not belong to the prepared combination.");
        return selections.GetOrAdd(selection.Combination, _ => new(this, selection));
    }
}

/// <summary>One imported Main Sky combination and its actual shader interfaces.
/// The source owner is shared with the numerical preparer and static media writer.</summary>
public sealed class SkyMainProgramSelection
{
    private readonly byte[] vertex, pixel;
    internal SkyMainProgramSelection(SkyMainProgramResources resources, D3D11AtmosphereProgramSelection programs)
    {
        Resources = resources; Programs = programs;
        var shader = resources.Shader;
        vertex = shader.Container.GetProgramBytes(shader.Source.Bytes, programs.VertexProgram).ToArray();
        pixel = shader.Container.GetProgramBytes(shader.Source.Bytes, programs.PixelProgram).ToArray();
        VertexDefinitions = shader.ReadResourceDefinitions(programs.VertexProgram);
        PixelDefinitions = shader.ReadResourceDefinitions(programs.PixelProgram);
        MaterialLayout = SkyMainMaterialLayout.Import(VertexDefinitions, PixelDefinitions);
        RequireSignatures(vertex, pixel);
    }
    public SkyMainProgramResources Resources { get; }
    public D3D11AtmosphereProgramSelection Programs { get; }
    public DxbcResourceDefinitions VertexDefinitions { get; }
    public DxbcResourceDefinitions PixelDefinitions { get; }
    public SkyMainMaterialLayout MaterialLayout { get; }
    public byte[] CopyVertexBytes() => vertex.ToArray();
    public byte[] CopyPixelBytes() => pixel.ToArray();

    private static void RequireSignatures(byte[] vertex, byte[] pixel)
    {
        var vs = DxbcSignatureReader.Read(vertex);
        var ps = DxbcSignatureReader.Read(pixel);
        var inputs = vs.Parts.Single(part => part.Kind == DxbcSignatureKind.Input).Parameters
            .Where(parameter => parameter.RawSystemValue == 0).ToArray();
        if (inputs.Length != 1 || !inputs[0].SemanticName.Text.Equals("POSITION", StringComparison.OrdinalIgnoreCase) ||
            inputs[0].SemanticIndex != 0 || inputs[0].ComponentType != 3 || inputs[0].Mask != 3)
            throw new NotSupportedException("Main Sky requires its float2 POSITION quad input.");
        var produced = vs.Parts.Single(part => part.Kind == DxbcSignatureKind.Output).Parameters;
        var consumed = ps.Parts.Single(part => part.Kind == DxbcSignatureKind.Input).Parameters;
        if (consumed.Any(input => !produced.Any(output =>
            output.SemanticName.Text.Equals(input.SemanticName.Text, StringComparison.OrdinalIgnoreCase) &&
            output.SemanticIndex == input.SemanticIndex && output.ComponentType == input.ComponentType &&
            (output.Mask & input.Mask) == input.Mask)))
            throw new NotSupportedException("Main Sky selected vertex and pixel linkage is unsupported.");
        var targets = ps.Parts.Single(part => part.Kind == DxbcSignatureKind.Output).Parameters;
        if (targets.Count != 1 || !targets[0].SemanticName.Text.Equals("SV_Target", StringComparison.OrdinalIgnoreCase) ||
            targets[0].SemanticIndex != 0 || targets[0].Register != 0 || targets[0].ComponentType != 3 || targets[0].Mask != 15)
            throw new NotSupportedException("Main Sky requires its single float4 target output.");
    }
}

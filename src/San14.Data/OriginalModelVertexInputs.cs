using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalModelVertexSemantic(string Name, uint Index);
public sealed record OriginalModelResolvedVertexInput(int LayoutIndex, int SourceAttributeIndex,
    ushort SourceSemanticCode, string SemanticName, uint SemanticIndex, uint SourceRegister,
    uint Location, uint SourceComponentType);

public sealed class OriginalModelVertexInputResolution
{
    public IReadOnlyList<OriginalModelResolvedVertexInput> Inputs { get; }
    public IReadOnlyList<OriginalModelVertexSemanticBinding> Bindings { get; }
    internal OriginalModelVertexInputResolution(IEnumerable<OriginalModelResolvedVertexInput> inputs)
    {
        OriginalModelResolvedVertexInput[] snapshot = inputs.ToArray();
        Inputs = Array.AsReadOnly(snapshot);
        Bindings = Array.AsReadOnly(snapshot.Select(x =>
            new OriginalModelVertexSemanticBinding(x.LayoutIndex, x.SourceSemanticCode, x.Location)).ToArray());
    }
}

/// <summary>Original G1M section5 rows are copied to the native layout CPU+44;
/// its D3D descriptor builder reads semantic byte6/index byte7 through the
/// original SC/TC table. This resolves those names against the selected VS's
/// ISGN and its actual translated input locations. It does not select a model
/// shader or substitute a first-attribute fallback for missing source channels.</summary>
public static class OriginalModelVertexInputs
{
    // TC14BF700 / SC14C05E0, sixteen bytes per source usage value. The
    // exact table and raw G1M-row creator chain are locked in the source audit.
    private static readonly string[] Semantics =
    [
        "POSITION", "BLENDWEIGHT", "BLENDINDICES", "NORMAL", "PSIZE", "TEXCOORD", "TANGENT",
        "BINORMAL", "TESSFACTOR", "POSITIONT", "COLOR", "FOG", "DEPTH", "SAMPLE"
    ];

    public static OriginalModelVertexSemantic ReadSemantic(ushort sourceCode)
    {
        int usage = sourceCode & 255, index = sourceCode >> 8;
        if (usage >= Semantics.Length || index >= 128)
            throw new InvalidDataException($"Unsupported original model semantic code {sourceCode:X4}; native table/signed-index profile is not proven for this value.");
        return new(Semantics[usage], (uint)index);
    }

    public static OriginalModelVertexInputResolution Resolve(G1ModelGeometry geometry,
        DxbcProgramSignatures originalVertexSignature, SpirvShaderInterface translatedVertex,
        IReadOnlyCollection<int>? explicitLayoutIndices = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(originalVertexSignature);
        ArgumentNullException.ThrowIfNull(translatedVertex);
        if (translatedVertex.EntryPoints.Count != 1 || translatedVertex.EntryPoints[0].ExecutionModel != 0 ||
            translatedVertex.EntryPoints[0].Name != "main")
            throw new InvalidDataException("Model vertex semantic resolution requires a single translated Vertex/main entry.");
        DxbcSignaturePart[] inputParts = originalVertexSignature.Parts.Where(x => x.Kind == DxbcSignatureKind.Input).ToArray();
        if (inputParts.Length != 1)
            throw new InvalidDataException("Model vertex semantic resolution requires one original ISGN part.");
        SpirvInterfaceVariable[] deviceInputs = translatedVertex.StageVariables
            .Where(x => x.StorageClass == 1 && x.Location is not null).ToArray();
        if (deviceInputs.Length == 0 || deviceInputs.Any(x => x.BuiltIn is not null) ||
            deviceInputs.Select(x => x.Location).Distinct().Count() != deviceInputs.Length)
            throw new InvalidDataException("Model translated vertex inputs must have unique explicit locations and no conflicting builtin decoration.");
        int[] selected = explicitLayoutIndices?.ToArray() ?? geometry.Layouts.Select(x => x.Index).ToArray();
        if (selected.Length == 0 || selected.Distinct().Count() != selected.Length ||
            selected.Any(x => x < 0 || x >= geometry.Layouts.Count || geometry.Layouts[x].Index != x))
            throw new InvalidDataException("Model vertex semantic resolution references missing or repeated layouts.");
        var rows = new List<OriginalModelResolvedVertexInput>();
        var usedRegisters = new HashSet<uint>();
        foreach (SpirvInterfaceVariable device in deviceInputs)
        {
            if (device.Name is not { Length: > 1 } name || name[0] != 'v' ||
                !uint.TryParse(name.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out uint register) ||
                name != "v" + register.ToString(CultureInfo.InvariantCulture) || !usedRegisters.Add(register))
                throw new InvalidDataException("Model translated input lacks a unique compiler vN register name.");
            DxbcSignatureParameter[] parameters = inputParts[0].Parameters.Where(x => x.Register == register).ToArray();
            if (parameters.Length != 1 || parameters[0].RawSystemValue != 0 ||
                parameters[0].Mask is 0 or > 15 || parameters[0].ComponentType is < 1 or > 3)
                throw new InvalidDataException("Model input register does not uniquely identify a supported original non-system ISGN semantic; packed-register inputs are unsupported.");
            DxbcSignatureParameter parameter = parameters[0];
            SpirvInterfaceType scalar = device.Type.Kind == "vector" ? device.Type.ComponentType! : device.Type;
            bool validType = parameter.ComponentType switch
            {
                1 => scalar is { Kind: "int", Bits: 32, Signed: false },
                2 => scalar is { Kind: "int", Bits: 32, Signed: true },
                3 => scalar is { Kind: "float", Bits: 32 },
                _ => false
            };
            if (!validType || (device.Type.Kind == "vector" && device.Type.ComponentCount is not (>= 1 and <= 4)))
                throw new InvalidDataException("Model translated vertex input numeric type differs from its original ISGN.");
            foreach (int layoutIndex in selected)
            {
                G1VertexLayout layout = geometry.Layouts[layoutIndex];
                var candidates = layout.Attributes.Select((attribute, index) =>
                    (Attribute: attribute, Index: index, Semantic: ReadSemantic(attribute.SemanticCode)))
                    .Where(x => x.Semantic.Name.Equals(parameter.SemanticName.Text, StringComparison.Ordinal) &&
                        x.Semantic.Index == parameter.SemanticIndex).ToArray();
                if (candidates.Length != 1)
                    throw new InvalidDataException($"Model layout {layoutIndex} has no unique source {parameter.SemanticName.Text}{parameter.SemanticIndex} for vertex register {register}; original fallback is not implemented.");
                var matched = candidates[0];
                var numeric = OriginalModelDeviceGeometry.Format(matched.Attribute.TypeCode);
                if (numeric.ShaderType != parameter.ComponentType)
                    throw new InvalidDataException($"Model layout {layoutIndex} numeric format for {matched.Semantic.Name}{matched.Semantic.Index} does not match its original shader input type.");
                rows.Add(new(layoutIndex, matched.Index, matched.Attribute.SemanticCode, matched.Semantic.Name,
                    matched.Semantic.Index, register, device.Location!.Value, parameter.ComponentType));
            }
        }
        return new(rows);
    }
}

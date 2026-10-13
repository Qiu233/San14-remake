using San14.Data.Formats;

namespace San14.Data;

/// <summary>One imported shader resource. Program descriptions are parsed on first
/// use and retained for this owner, together with selection and resource binding
/// tables. Reload resources through a new owner.</summary>
public sealed class LoadedShaderContainer
{
    private readonly object programMetadataGate = new();
    private readonly Dictionary<int, DxbcProgramSignatures> signatures = new();
    private readonly Dictionary<int, DxbcResourceDefinitions> definitions = new();
    private G2ShaderCombinations? combinations;
    private G1ShaderBindingTables? bindingTables;
    private readonly Dictionary<int, G1ShaderResourceBindings> resourceBindings = new();
    private readonly Dictionary<uint, G1ShaderResourceBindings> resourceTables = new();
    private G1ShaderMatrixBindings? matrixBindings;
    private G1ShaderConfigurations? configurations;
    private G1ShaderSelectionDimensions? selectionDimensions;
    public LoadedShaderContainer(G2ShaderContainer container, LoadedRdbAsset source)
        => (Container, Source) = (container, source);
    public G2ShaderContainer Container { get; }
    public LoadedRdbAsset Source { get; }
    public DxbcProgramSignatures ReadSignatures(int programIndex)
    {
        if ((uint)programIndex >= Container.Programs.Count)
            throw new ArgumentOutOfRangeException(nameof(programIndex), $"Program index is outside {Source.ResourceUri}.");
        try
        {
            lock (programMetadataGate)
            {
                if (signatures.TryGetValue(programIndex, out var cached)) return cached;
                var parsed = DxbcSignatureReader.Read(Container.GetProgramBytes(Source.Bytes, programIndex));
                signatures.Add(programIndex, parsed);
                return parsed;
            }
        }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Shader signatures failed: {Source.ResourceUri}, program {programIndex}. {error.Message}", error); }
    }

    public G2ShaderMetadata ReadMetadata()
    {
        try { return G2ShaderMetadataReader.Read(Source.Bytes); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Shader metadata failed: {Source.ResourceUri}. {error.Message}", error); }
    }

    public G1ShaderBindingTables ReadG1Bindings()
    {
        try
        {
            lock (programMetadataGate)
                return bindingTables ??= G1ShaderBindingReader.Read(Source.Bytes, ReadCombinations());
        }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"G1S shader binding tables failed: {Source.ResourceUri}. {error.Message}", error); }
    }

    public G1ShaderResourceBindings ReadG1ResourceBindings(int explicitCombinationIndex)
    {
        try
        {
            lock (programMetadataGate)
            {
                if (resourceBindings.TryGetValue(explicitCombinationIndex, out var cached)) return cached;
                var combination = ReadCombination(explicitCombinationIndex);
                G1ShaderResourceBindings parsed;
                if (resourceTables.TryGetValue(combination.RawValue4, out var shared))
                    parsed = shared with { Combination = combination };
                else
                {
                    parsed = G1ShaderResourceBindingReader.Read(Source.Bytes, Container, combination);
                    resourceTables.Add(combination.RawValue4, parsed);
                }
                resourceBindings.Add(explicitCombinationIndex, parsed);
                return parsed;
            }
        }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"G1S resource binding table failed: {Source.ResourceUri}, combination {explicitCombinationIndex}. {error.Message}", error); }
    }

    public G2ShaderCombinations ReadCombinations()
    {
        lock (programMetadataGate)
            return combinations ??= G2ShaderCombinationReader.Read(Source.Bytes, Container);
    }

    public G1ShaderMatrixBindings ReadMatrixBindings()
    {
        try
        {
            lock (programMetadataGate)
                return matrixBindings ??= G1ShaderMatrixBindingReader.Read(Source.Bytes, Container);
        }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Shader matrix bindings failed: {Source.ResourceUri}. {error.Message}", error); }
    }

    public G1ShaderConfigurations ReadConfigurations()
    {
        try
        {
            lock (programMetadataGate)
                return configurations ??= G1ShaderConfigurationReader.Read(Source.Bytes, Container);
        }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Shader configurations failed: {Source.ResourceUri}. {error.Message}", error); }
    }

    public G2ShaderCombination ReadCombination(int explicitCombinationIndex)
    {
        var rows = ReadCombinations().Rows;
        if ((uint)explicitCombinationIndex >= rows.Count)
            throw new InvalidDataException($"Shader combination is outside {Source.ResourceUri}: {explicitCombinationIndex}.");
        return rows[explicitCombinationIndex];
    }

    public G1ShaderSelectionDimensions ReadSelectionDimensions()
    {
        try
        {
            lock (programMetadataGate)
                return selectionDimensions ??= G1ShaderDimensionReader.Read(Source.Bytes, ReadG1Bindings(), ReadMatrixBindings());
        }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"Shader selection dimensions failed: {Source.ResourceUri}. {error.Message}", error); }
    }

    public DxbcResourceDefinitions ReadResourceDefinitions(int programIndex)
    {
        if ((uint)programIndex >= Container.Programs.Count)
            throw new ArgumentOutOfRangeException(nameof(programIndex), $"Program index is outside {Source.ResourceUri}.");
        try
        {
            lock (programMetadataGate)
            {
                if (definitions.TryGetValue(programIndex, out var cached)) return cached;
                var parsed = DxbcResourceDefinitionReader.Read(Container.GetProgramBytes(Source.Bytes, programIndex));
                definitions.Add(programIndex, parsed);
                return parsed;
            }
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Shader resource definitions failed: {Source.ResourceUri}, program {programIndex}. {error.Message}", error);
        }
    }
}

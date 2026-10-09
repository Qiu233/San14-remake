using System.Security.Cryptography;
using San14.Data;

public sealed partial class DeferredPixelProgram
{
    public DeferredF103SelectedSource? SourceSelection { get; private set; }
    private readonly object selectedBindingsImport = new();
    private TranslatedShaderBindings? loadedSelectedDeviceBindings;

    /// <summary>Compile exactly the original program selected by the complete
    /// current F10348/96-byte source key. No PS1 specialization is carried into
    /// another original program. The caller owns this resulting shader lifetime.</summary>
    public static async Task<DeferredPixelProgram> BuildAsync(
        DeferredF103SelectedSource selected, Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(selected); selected.Validate();
        var program = await BuildCoreAsync(selected.SourcePlan.Shader, selected.Combination.Index, compiler, [], selected: selected);
        try
        {
            _ = program.ReadValidatedSelectedSourceDeviceBindings();
            return program;
        }
        catch { await program.DisposeAsync(); throw; }
    }

    /// <summary>Only the audited selected-program adapter can remove named
    /// roles. Proofs retain their original owner generation; old PS1 adapters
    /// are never run against selected bytecode.</summary>
    public static async Task<DeferredPixelProgram> BuildAsync(
        DeferredF103SelectedSource selected, Vkd3dShaderCompiler compiler,
        IReadOnlyList<DeferredSourceNull2D> explicitFreshPairs,
        DeferredSourceNullIes? explicitFreshIes = null,
        DeferredSourceLocalAmbient? explicitFreshLocalAmbient = null)
    {
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(explicitFreshPairs);
        selected.Validate();
        var program = await BuildCoreAsync(selected.SourcePlan.Shader, selected.Combination.Index, compiler,
            explicitFreshPairs.ToArray(), explicitFreshLocalAmbient, explicitFreshIes, selected);
        try { _ = program.ReadValidatedSelectedSourceDeviceBindings(); return program; }
        catch { await program.DisposeAsync(); throw; }
    }

    public TranslatedShaderBindings ReadValidatedSelectedSourceDeviceBindings()
    {
        // The compiled owner retains immutable source/translation declarations.
        // Build imports and verifies them once. New program owners after a
        // source reload import independently; current selection and seasonal
        // proof consumers still validate their actual current source below.
        lock(selectedBindingsImport)
            return loadedSelectedDeviceBindings??=ImportSelectedSourceDeviceBindings();
    }

    private TranslatedShaderBindings ImportSelectedSourceDeviceBindings()
    {
        var selected = SourceSelection ?? throw new InvalidDataException("Original selected Deferred requires its complete source selection packet.");
        selected.Validate();
        if (Source.Source.DecodedSha256 != MapSourceCreatedShaderSelection.ShaderSha256 ||
            Combination.Index != selected.Combination.Index ||
            UnspecializedTranslation.SourceProgramSha256 != selected.PixelProgramSha256 ||
            Translation.SourceProgramSha256 != selected.PixelProgramSha256 ||
            Convert.ToHexString(SHA256.HashData(UnspecializedTranslation.Bytes)) != UnspecializedTranslation.SpirvSha256 ||
            NullImageSpecialization is not null || LocalAmbientNullSpecialization is not null ||
            IesSourceNull is not null || IesNullSpecialization is not null || OptionalLocalAmbientNullSpecialization is not null)
            throw new InvalidDataException("Selected original Deferred program differs from its source bytecode or contains an unrelated PS1 adapter.");
        var definitions = Source.ReadResourceDefinitions(selected.PixelProgramIndex);
        var verified = TranslatedShaderBindingReader.Read(definitions, UnspecializedTranslation.Bytes);
        if (verified.Bindings.Count != definitions.Bindings.Count ||
            !verified.Bindings.Select(SourceDeclarationIdentity).SequenceEqual(SourceDescriptorBindings.Bindings.Select(SourceDeclarationIdentity)))
            throw new InvalidDataException("Selected Deferred original RDEF and complete translated device declarations differ.");
        var effective = verified;
        if (SelectedSourceNullSpecialization is { } nulls)
        {
            if (!ReferenceEquals(nulls.SelectedSource, selected) ||
                nulls.UnspecializedTranslationSha256 != UnspecializedTranslation.SpirvSha256)
                throw new InvalidDataException("Selected null specialization belongs to another original selection or translation generation.");
            var canonical = DeferredSelectedSourceNullAdapter.Adapt(UnspecializedTranslation, verified, selected,
                nulls.ProofPairs, nulls.SourceNullIes, nulls.SourceLocalAmbient);
            if (canonical.Sha256 != nulls.Sha256 || !canonical.Bytes.AsSpan().SequenceEqual(Translation.Bytes))
                throw new InvalidDataException("Selected specialization differs from its exact original named reads and typed source proofs.");
            effective = DeferredSelectedSourceNullAdapter.ReadDeviceBindings(verified, nulls);
        }
        else if (Translation.SpirvSha256 != UnspecializedTranslation.SpirvSha256 ||
            !Translation.Bytes.AsSpan().SequenceEqual(UnspecializedTranslation.Bytes))
            throw new InvalidDataException("Selected original bytecode changed without its bounded typed adapter.");
        if (effective.Interface.Sha256 != Translation.SpirvSha256 ||
            Convert.ToHexString(SHA256.HashData(Translation.Bytes)) != Translation.SpirvSha256 ||
            !effective.Bindings.Select(SourceDeclarationIdentity).SequenceEqual(DescriptorBindings.Bindings.Select(SourceDeclarationIdentity)))
            throw new InvalidDataException("Selected effective declarations differ from their exact verified bytecode.");
        return effective;
    }

    /// <summary>A GPU program can be borrowed by another owned frame only after
    /// that frame's complete current native selection resolves to the same
    /// source shader key and Camera source generation. Current producer values
    /// are recomputed by the caller; the first frame is never reused as evidence.</summary>
    public void ValidateCompatibleCurrentSelection(DeferredF103SelectedSource current)
    {
        ArgumentNullException.ThrowIfNull(current); current.Validate();
        var initial = SourceSelection ?? throw new InvalidDataException("Compatible selected frames require the compiled source selection generation.");
        _ = ReadValidatedSelectedSourceDeviceBindings();
        if (current.Combination.Index != initial.Combination.Index || current.PixelProgramIndex != initial.PixelProgramIndex ||
            current.PixelProgramSha256 != initial.PixelProgramSha256 ||
            !current.RequestedVariant96.Span.SequenceEqual(initial.RequestedVariant96.Span) ||
            current.FeatureMatch.RowIndex != initial.FeatureMatch.RowIndex ||
            current.Camera.Source.Camera.ObjectId != initial.Camera.Source.Camera.ObjectId ||
            current.Camera.ExecutableLocale != initial.Camera.ExecutableLocale ||
            current.Camera.Source.Resource.Source.ResourceUri != initial.Camera.Source.Resource.Source.ResourceUri ||
            current.Camera.Source.Resource.Source.DecodedSha256 != initial.Camera.Source.Resource.Source.DecodedSha256 ||
            current.SourcePlan.Graph.Source.ResourceUri != initial.SourcePlan.Graph.Source.ResourceUri ||
            current.SourcePlan.Graph.Source.DecodedSha256 != initial.SourcePlan.Graph.Source.DecodedSha256)
            throw new InvalidDataException("Current accepted F103 selection requires another compiled program, Camera source generation or resource layout.");
    }
}

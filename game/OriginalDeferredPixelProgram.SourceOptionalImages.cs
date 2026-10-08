using System.Security.Cryptography;
using San14.Data;

public sealed partial class OriginalDeferredPixelProgram
{
    public static Task<OriginalDeferredPixelProgram> BuildAsync(LoadedShaderContainer source,
        int explicitCombinationIndex, Vkd3dShaderCompiler compiler,
        OriginalDeferredSourceNullIes explicitFreshIes,
        IReadOnlyList<OriginalDeferredSourceNull2D>? explicitFresh2DNulls = null,
        OriginalDeferredSourceLocalAmbient? explicitFreshLocalAmbient = null)
    {
        ArgumentNullException.ThrowIfNull(explicitFreshIes); explicitFreshIes.Validate();
        if (explicitFreshLocalAmbient is not null)
        {
            explicitFreshLocalAmbient.Validate();
            if (explicitFreshLocalAmbient.Shader.Source.ResourceUri != source.Source.ResourceUri ||
                explicitFreshLocalAmbient.Shader.Source.DecodedSha256 != source.Source.DecodedSha256 ||
                explicitFreshLocalAmbient.CombinationIndex != explicitCombinationIndex)
                throw new InvalidDataException("Source LAS proof belongs to another original shader or combination.");
        }
        return BuildCoreAsync(source, explicitCombinationIndex, compiler,
            (explicitFresh2DNulls ?? []).ToArray(), explicitFreshLocalAmbient, explicitFreshIes);
    }

    /// <summary>Only typed source proofs select these exact texture/sampler
    /// pairs. LAS removes three SRVs separately and keeps shared ambient s7.</summary>
    public IReadOnlyList<uint> SourceNullTextureSamplerRegisters => Array.AsReadOnly(
        (NullImageSpecialization?.NullPixelRegisters ?? []).Concat(IesSourceNull is null ? [] : new uint[] { 0 }).Order().ToArray());

    /// <summary>Verify the complete source translation, every typed proof stage
    /// and the final effective interface. No general declaration deletion is
    /// accepted. Returned original RDEF still contains all42 declarations.</summary>
    public OriginalTranslatedShaderBindings ReadValidatedSourceDeviceBindings()
    {
        if (SourceSelection is not null) return ReadValidatedSelectedSourceDeviceBindings();
        if (SourceDescriptorBindings.Bindings.Count != 42 ||
            OriginalTranslation.SourceProgramSha256 != OriginalDeferredBufferSources.PixelSourceSha256 ||
            OriginalTranslation.SpirvSha256 != OriginalDeferredNullImageAdapter.PixelTranslationSha256 ||
            Convert.ToHexString(SHA256.HashData(OriginalTranslation.Bytes)) != OriginalTranslation.SpirvSha256)
            throw new InvalidDataException("Source Deferred program requires its complete pinned original42 translation.");
        var verified = SourceDescriptorBindings;
        if (NullImageSpecialization is { } pairs)
            verified = OriginalDeferredNullImageAdapter.ReadDeviceBindings(SourceDescriptorBindings, pairs);
        if (LocalAmbientNullSpecialization is { } las)
        {
            if (las.Preceding2D != NullImageSpecialization || las.Source.CombinationIndex != Combination.Index ||
                las.Source.Shader.Source.ResourceUri != Source.Source.ResourceUri ||
                las.Source.Shader.Source.DecodedSha256 != Source.Source.DecodedSha256)
                throw new InvalidDataException("Source LAS proof differs from the original shader or preceding2D proof.");
            verified = OriginalDeferredSourceLocalAmbientNullAdapter.ReadDeviceBindings(SourceDescriptorBindings, las);
        }
        if (IesSourceNull is { } ies)
        {
            ies.Validate();
            if (OptionalLocalAmbientNullSpecialization is { } composite)
            {
                if (composite.Ies != ies || composite.LocalAmbient != LocalAmbientNullSpecialization || IesNullSpecialization is not null)
                    throw new InvalidDataException("Source IES/LAS stages differ from their exact typed proof generation.");
                verified = OriginalDeferredOptionalNullImageAdapter.ReadDeviceBindings(SourceDescriptorBindings, composite);
            }
            else if (IesNullSpecialization is { } optional && LocalAmbientNullSpecialization is null)
            {
                if (!optional.SourceEvidence.Contains(ies.SourceEvidence) ||
                    !optional.NullPixelRegisters.SequenceEqual(SourceNullTextureSamplerRegisters) ||
                    optional.ReplacedSamples != 2 + (NullImageSpecialization?.ReplacedSamples ?? 0))
                    throw new InvalidDataException("Source IES samples differ from the typed fresh Camera or preceding2D proof.");
                verified = OriginalDeferredOptionalNullImageAdapter.ReadDeviceBindings(SourceDescriptorBindings, optional);
            }
            else throw new InvalidDataException("Source IES proof lacks its exact final specialization.");
        }
        else if (IesNullSpecialization is not null || OptionalLocalAmbientNullSpecialization is not null)
            throw new InvalidDataException("Optional specialization lacks a typed source IES proof.");
        var removedPairs = SourceNullTextureSamplerRegisters.ToHashSet();
        var expected = SourceDescriptorBindings.Bindings.Where(d =>
            !(d.DescriptorKind is "Texture" or "Sampler" && removedPairs.Contains(d.Source.BindPoint)) &&
            !(LocalAmbientNullSpecialization is not null && d.Source.InputType == 2 && d.Source.BindPoint is 22 or 23 or 24));
        if (!expected.Select(SourceDeclarationIdentity).SequenceEqual(DescriptorBindings.Bindings.Select(SourceDeclarationIdentity)) ||
            !verified.Bindings.Select(SourceDeclarationIdentity).SequenceEqual(DescriptorBindings.Bindings.Select(SourceDeclarationIdentity)) ||
            verified.Interface.Sha256 != Translation.SpirvSha256 ||
            Convert.ToHexString(SHA256.HashData(Translation.Bytes)) != Translation.SpirvSha256)
            throw new InvalidDataException("Effective source Deferred interface differs from its exact typed null proofs.");
        return verified;
    }
    private static (string, uint, uint, uint?, uint?) SourceDeclarationIdentity(OriginalTranslatedShaderBinding d) =>
        (d.DescriptorKind, d.Source.BindPoint, d.Device.VariableId, d.Device.DescriptorSet, d.Device.Binding);
}

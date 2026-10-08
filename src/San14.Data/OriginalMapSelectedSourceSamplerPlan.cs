using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Complete key from one explicitly selected current producer. The
/// encoded role is semantic; a PS1 register is deliberately not accepted.</summary>
public sealed record OriginalMapSelectedSamplerSourceInput(short EncodedParameter, string TextureName,
    string SourceResourceUri, string SourceSha256, byte[] OriginalKey,
    long FrameNumber, string CurrentProducerProvenance);

public sealed class OriginalMapSelectedSourceSamplerPlan
{
    public OriginalMapSelectedSourceImagePlan ImagePlan { get; }
    public OriginalDeferredF103SelectedSource Selection => ImagePlan.Selection;
    public LoadedOriginalDeferredSamplerNativeSource Native { get; }
    public OriginalShaderSpirv OriginalPixel { get; }
    /// <summary>Compiled import provenance. Current seasonal authority is
    /// supplied separately by CurrentNullProofPairs and the actual Selection.</summary>
    public OriginalDeferredSelectedSourceNullSpirv? SourceNullSpecialization { get; }
    public OriginalTranslatedShaderBindings OriginalBindings { get; }
    public OriginalTranslatedShaderBindings EffectiveBindings { get; }
    public IReadOnlyList<OriginalDeferredSamplerSourceBinding> Bindings { get; }
    public IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> OriginalDescriptors { get; }
    public IReadOnlyList<OriginalDeferredSourceNull2D> CurrentNullProofPairs { get; }

    private sealed record ImportedProgram(OriginalTranslatedShaderBindings Original, OriginalTranslatedShaderBindings Effective);
    private sealed class ProgramImports
    {
        internal ImportedProgram? Plain;
        internal readonly ConditionalWeakTable<OriginalDeferredSelectedSourceNullSpirv, ImportedProgram> Specialized = new();
    }
    private sealed class ImportedAsset(LoadedRdbAsset source)
    {
        internal readonly Lazy<LoadedObjects> Objects = new(() => new(DokReader.Read(source.Bytes), source));
    }
    private static readonly ConditionalWeakTable<OriginalShaderSpirv, ConditionalWeakTable<LoadedShaderContainer, ProgramImports>> Programs = new();
    private static readonly ConditionalWeakTable<LoadedRdbAsset, ImportedAsset> Assets = new();
    private static readonly ConditionalWeakTable<OriginalRenderTextureDefinition, ImportedAsset> RenderTextures = new();

    private OriginalMapSelectedSourceSamplerPlan(OriginalMapSelectedSourceImagePlan imagePlan,
        LoadedOriginalDeferredSamplerNativeSource native, OriginalShaderSpirv originalPixel,
        OriginalDeferredSelectedSourceNullSpirv? specialization, OriginalTranslatedShaderBindings original,
        OriginalTranslatedShaderBindings effective, List<OriginalDeferredSamplerSourceBinding> bindings,
        Dictionary<uint, ReadOnlyMemory<byte>> descriptors,
        IReadOnlyList<OriginalDeferredSourceNull2D> currentNullPairs)
        => (ImagePlan, Native, OriginalPixel, SourceNullSpecialization, OriginalBindings, EffectiveBindings, Bindings, OriginalDescriptors, CurrentNullProofPairs) =
            (imagePlan, native, originalPixel, specialization, original, effective, bindings.AsReadOnly(),
             new ReadOnlyDictionary<uint, ReadOnlyMemory<byte>>(descriptors), Array.AsReadOnly(currentNullPairs.ToArray()));

    public static OriginalMapSelectedSamplerSourceInput FromProducerKey(OriginalMapSelectedSourceImagePlan plan,
        LoadedOriginalDeferredSamplerNativeSource native, short encodedRole, LoadedRdbAsset source,
        string actualProducerFragment, ReadOnlySpan<byte> finalKey)
    {
        ArgumentNullException.ThrowIfNull(plan); plan.Selection.Validate(); _ = ReadImportedAsset(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualProducerFragment);
        var consumer = plan.Images.SingleOrDefault(i => i.EncodedParameter == encodedRole)
            ?? throw Error("producer role is absent from this selected program");
        if (!plan.Samplers.Any(s => s.KeySource.Role == consumer.Role))
            throw Error("image has no declared sampler in this selected program");
        _ = OriginalDeferredSamplerSources.DecodeKey(native, finalKey);
        return new(encodedRole, consumer.Declaration.Name.Text, source.ResourceUri + "#" + actualProducerFragment,
            source.DecodedSha256, finalKey.ToArray(), plan.FrameNumber, plan.CurrentProducerProvenance);
    }

    public static OriginalMapSelectedSamplerSourceInput FromRenderTexture(OriginalMapSelectedSourceImagePlan plan,
        LoadedOriginalDeferredSamplerNativeSource native, short encodedRole, OriginalRenderTextureDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _ = RenderTextures.GetValue(source, static texture =>
        {
            var imported = ReadImportedAsset(texture.Source.Source);
            var canonical = OriginalRenderTextureResources.Read(imported.Objects.Value, texture.Object.ObjectId);
            if ((canonical with {Source=texture.Source,Object=texture.Object}) != texture)
                throw Error("producer render texture differs from its original typed declaration");
            return imported;
        });
        var consumer = plan.Images.SingleOrDefault(i => i.EncodedParameter == encodedRole)
            ?? throw Error("render texture role is absent from the selected program");
        uint dimension = source.Kind == OriginalRenderTextureKind.Texture2D ? 4u : 5u;
        if (consumer.Declaration.Dimension != dimension) throw Error("render texture dimension differs from selected consumer");
        return FromProducerKey(plan, native, encodedRole, source.Source.Source,
            "object/" + source.Object.ObjectId.ToString("X8") + "/actual-render-target-postloader",
            OriginalDeferredSamplerSources.FinalRenderTextureKey(native, source));
    }

    public static OriginalMapSelectedSourceSamplerPlan Resolve(OriginalMapSelectedSourceImagePlan imagePlan,
        LoadedOriginalDeferredSamplerNativeSource native, OriginalShaderSpirv originalPixel,
        IReadOnlyList<OriginalMapSelectedSamplerSourceInput> explicitCurrentProducers,
        OriginalDeferredSelectedSourceNullSpirv? sourceNullSpecialization = null)
    {
        ArgumentNullException.ThrowIfNull(imagePlan);
        if (sourceNullSpecialization is not null && !ReferenceEquals(sourceNullSpecialization.SelectedSource, imagePlan.Selection))
            throw Error("null specialization belongs to a different accepted selection generation");
        return ResolveCore(imagePlan, native, originalPixel, explicitCurrentProducers, sourceNullSpecialization,
            sourceNullSpecialization?.ProofPairs ?? []);
    }

    /// <summary>Reuse the compiled typed null-role interface while consuming
    /// this frame's actual Directional construction and effective null1008.
    /// The compiled import proof is not used as a current seasonal proof.</summary>
    public static OriginalMapSelectedSourceSamplerPlan ResolveWithCurrentDirectionalProofs(
        OriginalMapSelectedSourceImagePlan imagePlan, LoadedOriginalDeferredSamplerNativeSource native,
        OriginalShaderSpirv originalPixel, IReadOnlyList<OriginalMapSelectedSamplerSourceInput> explicitCurrentProducers,
        OriginalDeferredSelectedSourceNullSpirv compiledSpecialization,
        IReadOnlyList<OriginalDeferredSourceNull2D> actualCurrentPairs)
    {
        ArgumentNullException.ThrowIfNull(compiledSpecialization); ArgumentNullException.ThrowIfNull(actualCurrentPairs);
        if (compiledSpecialization.SourceNullIes is not null || compiledSpecialization.SourceLocalAmbient is not null ||
            actualCurrentPairs.Any(pair => pair.DirectionalConstruction is null) ||
            !compiledSpecialization.ProofPairs.Select(pair => pair.PixelRegister).Order()
                .SequenceEqual(actualCurrentPairs.Select(pair => pair.PixelRegister).Order()))
            throw Error("current Directional proof requires the compiled exact null-role interface");
        return ResolveCore(imagePlan, native, originalPixel, explicitCurrentProducers, compiledSpecialization, actualCurrentPairs);
    }

    private static OriginalMapSelectedSourceSamplerPlan ResolveCore(OriginalMapSelectedSourceImagePlan imagePlan,
        LoadedOriginalDeferredSamplerNativeSource native, OriginalShaderSpirv originalPixel,
        IReadOnlyList<OriginalMapSelectedSamplerSourceInput> explicitCurrentProducers,
        OriginalDeferredSelectedSourceNullSpirv? sourceNullSpecialization,
        IReadOnlyList<OriginalDeferredSourceNull2D> actualCurrentPairs)
    {
        ArgumentNullException.ThrowIfNull(imagePlan); imagePlan.Selection.Validate();
        ArgumentNullException.ThrowIfNull(native); ArgumentNullException.ThrowIfNull(originalPixel);
        ArgumentNullException.ThrowIfNull(explicitCurrentProducers);
        var selection = imagePlan.Selection;
        if (native.Locale != selection.Camera.ExecutableLocale || originalPixel.SourceProgramSha256 != selection.PixelProgramSha256)
            throw Error("native constructor or original translation differs from this source selection");
        foreach (var pair in actualCurrentPairs) pair.ValidateForSelection(selection);
        var imported = ReadImportedProgram(selection, originalPixel, sourceNullSpecialization);
        var original = imported.Original; var effective = imported.Effective;
        var active = effective.Bindings.Where(b => b.DescriptorKind == "Sampler").ToArray();
        var inputs = explicitCurrentProducers.Select(p => p is null ? throw Error("null producer key") :
            p with {OriginalKey=p.OriginalKey.ToArray()}).ToArray();
        if (inputs.Select(i => i.EncodedParameter).Distinct().Count() != inputs.Length || inputs.Any(i =>
            i.FrameNumber != imagePlan.FrameNumber || i.CurrentProducerProvenance != imagePlan.CurrentProducerProvenance ||
            string.IsNullOrWhiteSpace(i.SourceResourceUri) || i.SourceSha256 is not {Length:64} hash || !hash.All(Uri.IsHexDigit)))
            throw Error("selected sampler producers are duplicated, incomplete or from an older Camera/season frame");
        var selected = inputs.ToDictionary(i => i.EncodedParameter);
        var used = new HashSet<short>();
        var bindings = new List<OriginalDeferredSamplerSourceBinding>();
        var descriptors = new Dictionary<uint, ReadOnlyMemory<byte>>();
        foreach (var consumer in imagePlan.Samplers)
        {
            uint register = consumer.Declaration.BindPoint;
            if (!active.Any(b => b.Source == consumer.Declaration))
            {
                if (sourceNullSpecialization is null || !sourceNullSpecialization.RemovedSamplerRegisters.Contains(register))
                    throw Error("original sampler disappeared without its typed source proof");
                // This is metadata for the proved native null descriptor path.
                // No key, white texture or GPU sampler is allocated for it.
                descriptors.Add(register, OriginalDeferredSamplerSources.BackendDefaultDescriptor(native));
                continue;
            }
            var keySource = consumer.KeySource;
            byte[] key; string uri, hash;
            if (keySource.Role == "GGX")
            {
                key = OriginalDeferredSamplerSources.RenderTextureKey(native,1);
                uri = native.ResourceUri + "/generated-ggx-wrapper/mip1"; hash = native.ExecutableSha256;
            }
            else if (keySource.Role is "GBufferDepth" or "GBufferRR")
            {
                var target = imagePlan.TexturePlan.Inputs.Single(i => i.EncodedParameter == keySource.EncodedParameter);
                var sourceInput = FromRenderTexture(imagePlan, native, keySource.EncodedParameter!.Value, target.Texture);
                key = sourceInput.OriginalKey; uri = sourceInput.SourceResourceUri; hash = sourceInput.SourceSha256;
            }
            else
            {
                if (keySource.EncodedParameter is not short encoded || !selected.TryGetValue(encoded, out var sourceInput) ||
                    sourceInput.TextureName != keySource.Declaration.Name.Text)
                    throw Error("active declared sampler requires its exact current source producer key");
                used.Add(encoded); key = sourceInput.OriginalKey; uri = sourceInput.SourceResourceUri; hash = sourceInput.SourceSha256;
            }
            var recipe = OriginalDeferredSamplerSources.DecodeKey(native,key);
            if (((recipe.Filter & 0x80) != 0) != (keySource.Role is "PointShadow" or "SpotShadow"))
                throw Error("comparison mode differs from the selected native shadow role");
            descriptors.Add(register, recipe.EncodeD3D11Descriptor());
            bindings.Add(new(register,consumer.Declaration.Name.Text,keySource.Declaration.BindPoint,
                keySource.Declaration.Name.Text,uri,hash,recipe));
        }
        if (used.Count != inputs.Length || active.Length != bindings.Count)
            throw Error("producer keys override an absent, removed or independently owned sampler");
        return new(imagePlan,native,originalPixel,sourceNullSpecialization,original,effective,bindings,descriptors,actualCurrentPairs);
    }

    public void Validate()
    {
        Selection.Validate();
        foreach (var pair in CurrentNullProofPairs) pair.ValidateForSelection(Selection);
    }

    private static ImportedProgram ReadImportedProgram(OriginalDeferredF103SelectedSource selection,
        OriginalShaderSpirv originalPixel, OriginalDeferredSelectedSourceNullSpirv? specialized)
    {
        if (specialized is not null && (specialized.SelectedSource.Combination.Index != selection.Combination.Index ||
            specialized.SelectedSource.PixelProgramSha256 != selection.PixelProgramSha256))
            throw Error("compiled null-role interface belongs to another selected original program");
        var shaderImports = Programs.GetValue(originalPixel, static _ => new());
        var imports = shaderImports.GetValue(selection.SourcePlan.Shader, static _ => new());
        lock (imports)
        {
            ImportedProgram Import(OriginalDeferredSelectedSourceNullSpirv? specialization)
            {
                if (Convert.ToHexString(SHA256.HashData(originalPixel.Bytes)) != originalPixel.SpirvSha256)
                    throw Error("original translation differs from its loaded declared identity");
                var canonical = selection.SourcePlan.Shader.ReadResourceDefinitions(selection.PixelProgramIndex);
                var original = OriginalTranslatedShaderBindingReader.Read(canonical, originalPixel.Bytes);
                return new(original, specialization is null ? original :
                    OriginalDeferredSelectedSourceNullAdapter.ReadDeviceBindings(original, specialization));
            }
            return specialized is null ? imports.Plain ??= Import(null) :
                imports.Specialized.GetValue(specialized, specialization => Import(specialization));
        }
    }

    private static ImportedAsset ReadImportedAsset(LoadedRdbAsset source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Assets.GetValue(source, static asset =>
        {
            if (asset.OverrideSource is not null || string.IsNullOrWhiteSpace(asset.ResourceUri) ||
                Convert.ToHexString(SHA256.HashData(asset.Bytes)) != asset.DecodedSha256)
                throw Error("sampler producer source bytes differ from their original identity");
            return new(asset);
        });
    }
    private static InvalidDataException Error(string reason) => new("Selected F103 sampler plan: " + reason + ".");
}

using System.Buffers.Binary;
using San14.Data;

public sealed partial class OriginalNativeDeferredResources
{
    /// <summary>Use the exact selected original declarations. The five graph
    /// targets and complete source material rows retain their existing owners;
    /// the GGX texture and sampler move to their selected named registers.</summary>
    public static async Task<OriginalNativeDeferredResources> BuildSelectedAsync(
        OriginalDeferredPixelProgram program, LoadedOriginalDeferredMaterialTable explicitTable,
        OriginalGgxAmbientLut borrowedNativeGgx)
    {
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(explicitTable);
        ArgumentNullException.ThrowIfNull(borrowedNativeGgx);
        var selected = program.SourceSelection ?? throw new InvalidDataException("Selected resources require the complete current F103 selection.");
        selected.Validate();
        var effective = program.ReadValidatedSelectedSourceDeviceBindings();
        var plan = selected.ReadSelectedTexturePlan();
        if (!SameShader(program.Source, plan.Shader) || !SameShader(program.Source, explicitTable.Shader) ||
            plan.ResourceBindings.Combination.Index != program.Combination.Index || plan.Inputs.Count != 5)
            throw new InvalidDataException("Selected Deferred owners differ from their original shader and graph targets.");
        var declarations = effective.Bindings.Where(d => d.DescriptorKind != "UniformBuffer").ToArray();
        if (declarations.Select(d => (d.DescriptorKind, d.Source.BindPoint)).Distinct().Count() != declarations.Length)
            throw new InvalidDataException("Selected original resources contain duplicate declarations.");
        OriginalTranslatedShaderBinding Named(string kind, string name, uint dimension, uint? returnType = null)
        {
            var rows = declarations.Where(d => d.DescriptorKind == kind && d.Source.Name.Text == name).ToArray();
            if (rows.Length != 1 || rows[0].Source.Dimension != dimension || rows[0].Source.BindCount != 1 ||
                returnType.HasValue && rows[0].Source.ReturnType != returnType.Value)
                throw new InvalidDataException("Selected original known resource differs from its named source consumer: " + name);
            return rows[0];
        }
        var ggxTexture = Named("Texture", "sGxAmbLut", 4, 5);
        var ggxSampler = Named("Sampler", "__smpsGxAmbLut", 0);
        var ggxMap = selected.ResourceBindings.Entries.Where(r => r.PrimaryEncoded == 54).ToArray();
        if (ggxMap.Length != 1 || ggxMap[0].Name != "sGxAmbLut" || ggxMap[0].SlotIndex != ggxTexture.Source.BindPoint)
            throw new InvalidDataException("Selected GGX encoded source role differs from its RDEF texture register.");
        var material = Named("TextureBuffer", "tMaterialTable", 1, 4);
        var canonicalTable = OriginalDeferredMaterialTable.Create(program.Source, program.Combination.Index, []);
        if (material.Source != canonicalTable.PixelBinding || explicitTable.SlotParameter.RawValue != canonicalTable.SlotParameter.RawValue ||
            explicitTable.Layout.Name != canonicalTable.Layout.Name || explicitTable.Layout.NameHash != canonicalTable.Layout.NameHash ||
            explicitTable.EntryCount < 1 || explicitTable.Bytes.Length != checked(explicitTable.EntryCount * 4) ||
            BinaryPrimitives.ReadUInt32LittleEndian(explicitTable.Bytes.AsSpan(0, 4)) != 0)
            throw new InvalidDataException("Selected material table must retain the complete original uint rows and reserved zero entry.");
        foreach (var input in plan.Inputs)
        {
            var actual = Named("Texture", input.PixelBinding.Name.Text, input.PixelBinding.Dimension, 5);
            if (actual.Source != input.PixelBinding || input.ResourceMapping.PrimaryEncoded != input.EncodedParameter ||
                input.ResourceMapping.SlotIndex != actual.Source.BindPoint || input.ResourceMapping.Name != actual.Source.Name.Text ||
                input.SourceArgument.DescriptorWord != 0x05000001 || input.SourceArgument.RawValue.Length != 4 ||
                BinaryPrimitives.ReadUInt32LittleEndian(input.SourceArgument.RawValue) != input.Texture.Object.ObjectId)
                throw new InvalidDataException("Selected G-buffer declaration differs from its original graph target identity.");
        }
        var native = borrowedNativeGgx.NativeSource;
        if (native is null ||
            !borrowedNativeGgx.OriginalSamplerKey.AsSpan().SequenceEqual(Convert.FromHexString("1514300000000000")))
            throw new InvalidDataException("Selected GGX requires the actual original generator and sampler recipe.");
        uint[] expectedSampler = [0x14, 3, 3, 3, 0, 1, 4, 0, 0, 0, 0, 0, 0];
        byte[] sampler = borrowedNativeGgx.OriginalSamplerDescriptor;
        if (sampler.Length != 52 || Enumerable.Range(0, 13).Any(i =>
            BinaryPrimitives.ReadUInt32LittleEndian(sampler.AsSpan(i * 4, 4)) != expectedSampler[i]))
            throw new InvalidDataException("Selected GGX consumer sampler differs from its original native descriptor.");
        await ValidateGgxAsync(borrowedNativeGgx);
        var snapshot = canonicalTable with { Bytes = explicitTable.Bytes.ToArray(), EntryCount = explicitTable.EntryCount,
            ActiveOriginalBindingObserved = explicitTable.ActiveOriginalBindingObserved };
        var table = await OriginalDeferredMaterialTableDevice.BuildAsync(snapshot);
        try
        {
            OriginalDeferredDeviceBinding[] known = [
                new("Texture", ggxTexture.Source.BindPoint, borrowedNativeGgx.TextureRid, borrowedNativeGgx.TextureRid,
                    native.ResourceUri + "#generated-ambient-lut"),
                new("Sampler", ggxSampler.Source.BindPoint, borrowedNativeGgx.SamplerRid, null,
                    native.ResourceUri + "#sampler/1514300000000000"),
                new("TextureBuffer", material.Source.BindPoint, table.BufferRid, null,
                    table.Source.Shader.Source.ResourceUri + "#complete-source-material-table")
            ];
            return new(program, plan, table, borrowedNativeGgx, declarations, known);
        }
        catch { await table.DisposeAsync(); throw; }
    }
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The original selected stock Camera's fresh IES owner and fresh
/// ClusterLightList plugin. This token covers no later provider assignments.
/// It carries no RID or substitute image.</summary>
public sealed class OriginalDeferredSourceNullIes
{
    private OriginalDeferredSourceNullIes(LoadedOriginalMapCameraConstructionSource camera)
    {
        CameraSource = camera;
        SourceEvidence = camera.ConstructorOrigin +
            ";IESLightProfilesMap:ordinal301/hash9148D7AD/source-key-zero;" +
            "TC:C57D5F-C57DA9:CPU2EF0-zero;TC:CAB2B6-CAB2C4:CPU2B90=CPU2EF0;" +
            "embedded-Camera=CPU1330/native1860;TC:B9D136:prepared-Camera1860-copy;" +
            "TC:EAD3D9:null-owner-skips-encoded32-setter;" +
            "TC:9A9C53:fresh-ClusterLightList58-zero;TC:9AA6C0:copy-to-t0;" +
            "fresh-shader-interface;no-provider-assignments";
    }

    public uint PixelRegister => 0;
    public LoadedOriginalMapCameraConstructionSource CameraSource { get; }
    public string SourceEvidence { get; }

    public static OriginalDeferredSourceNullIes FromFreshConstruction(
        LoadedOriginalMapCameraConstructionSource camera, OriginalDeferredImageStateScope explicitScope)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (explicitScope != OriginalDeferredImageStateScope.FreshSourceConstructionNoAssignments)
            throw new InvalidDataException("Current IES bindings require their actual selected Camera owner and applied plugin state.");
        var owned = OriginalMapCameraConstructionSource.ReadPerspective(camera.Resource, camera.Camera.ObjectId);
        VerifyIesProperty(owned);
        return new(owned);
    }

    public void Validate() => VerifyIesProperty(OriginalMapCameraConstructionSource.ReadPerspective(
        CameraSource.Resource, CameraSource.Camera.ObjectId));

    private static void VerifyIesProperty(LoadedOriginalMapCameraConstructionSource camera)
    {
        DokProperty property = camera.Camera.Properties[301];
        if (property.Index != 301 || property.NameHash != 0x9148D7AD || property.TypeCode != 5 ||
            property.ElementCount != 1 || property.ElementSize != 4 || property.RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue) != 0)
            throw new InvalidDataException("Selected source Camera differs from its audited fresh null IES resource property.");
    }
}

/// <summary>Exact original PS1 specialization of the two null t0 float4 samples.
/// Optionally composes the separately proved fresh t6/t10 specialization.
/// t1 requires its actual producer or a separate closed activity proof.</summary>
public sealed record OriginalDeferredOptionalLocalAmbientNullSpirv(
    OriginalDeferredNullImageSpirv OptionalImages,
    OriginalDeferredSourceLocalAmbientNullSpirv LocalAmbient,
    OriginalDeferredSourceNullIes Ies)
{
    public byte[] Bytes => OptionalImages.Bytes;
    public string Sha256 => OptionalImages.Sha256;
}

public static class OriginalDeferredOptionalNullImageAdapter
{
    public static OriginalDeferredNullImageSpirv Adapt(OriginalShaderSpirv pixel,
        OriginalTranslatedShaderBindings sourceBindings, OriginalDeferredSourceNullIes ies,
        IReadOnlyList<OriginalDeferredSourceNull2D>? otherNulls = null)
    {
        ArgumentNullException.ThrowIfNull(pixel); ArgumentNullException.ThrowIfNull(sourceBindings);
        ArgumentNullException.ThrowIfNull(ies); ies.Validate();
        if (pixel.SourceProgramSha256 != OriginalDeferredImageSources.PixelSourceSha256 ||
            pixel.SpirvSha256 != OriginalDeferredNullImageAdapter.PixelTranslationSha256 ||
            Convert.ToHexString(SHA256.HashData(pixel.Bytes)) != pixel.SpirvSha256 ||
            sourceBindings.Interface.Sha256 != pixel.SpirvSha256 || sourceBindings.Bindings.Count != 42 ||
            sourceBindings.Interface.EntryPoints.Single().ExecutionModel != 4)
            throw new InvalidDataException("Optional null images require the complete pinned original Deferred PS1 translation.");

        byte[] input = pixel.Bytes;
        OriginalTranslatedShaderBindings deviceBindings = sourceBindings;
        OriginalDeferredNullImageSpirv? prior = null;
        if (otherNulls is { Count: > 0 })
        {
            prior = OriginalDeferredNullImageAdapter.Adapt(pixel, sourceBindings, otherNulls);
            input = prior.Bytes;
            deviceBindings = OriginalDeferredNullImageAdapter.ReadDeviceBindings(sourceBindings, prior);
        }
        return SpecializeIes(pixel, sourceBindings, ies, input, deviceBindings, prior);
    }

    public static OriginalDeferredOptionalLocalAmbientNullSpirv Adapt(OriginalShaderSpirv pixel,
        OriginalTranslatedShaderBindings sourceBindings, OriginalDeferredSourceNullIes ies,
        OriginalDeferredSourceLocalAmbient localAmbient,
        IReadOnlyList<OriginalDeferredSourceNull2D>? otherNulls = null)
    {
        ArgumentNullException.ThrowIfNull(ies); ies.Validate();
        var las = OriginalDeferredSourceLocalAmbientNullAdapter.Adapt(pixel, sourceBindings, localAmbient, otherNulls);
        var bindings = OriginalDeferredSourceLocalAmbientNullAdapter.ReadDeviceBindings(sourceBindings, las);
        return new(SpecializeIes(pixel, sourceBindings, ies, las.Bytes, bindings, las.Preceding2D), las, ies);
    }

    private static OriginalDeferredNullImageSpirv SpecializeIes(OriginalShaderSpirv pixel,
        OriginalTranslatedShaderBindings sourceBindings, OriginalDeferredSourceNullIes ies,
        byte[] input, OriginalTranslatedShaderBindings deviceBindings, OriginalDeferredNullImageSpirv? prior)
    {
        var image = deviceBindings.Bindings.Single(b => b.DescriptorKind == "Texture" && b.Source.BindPoint == 0);
        var sampler = deviceBindings.Bindings.Single(b => b.DescriptorKind == "Sampler" && b.Source.BindPoint == 0);
        if (image.Source.Name.Text != "sIES" || sampler.Source.Name.Text != "__smpsIES" ||
            image.Source.Dimension != 5 || image.Source.ReturnType != 5 || image.Source.BindCount != 1 ||
            image.Device.DescriptorSet != 0 || image.Device.Binding != 16 ||
            sampler.Device.DescriptorSet != 0 || sampler.Device.Binding != 3)
            throw new InvalidDataException("Null IES differs from its original float2DArray t0/s0 declaration.");

        var instructions = Parse(input);
        var resources = new HashSet<uint> { image.Device.VariableId, sampler.Device.VariableId };
        var removed = new HashSet<uint>(resources);
        var images = new HashSet<uint>(); var sampled = new HashSet<uint>();
        var zeros = new Dictionary<uint, uint>(); var body = new List<uint[]>();
        uint next = deviceBindings.Interface.IdBound; int replaced = 0, retained = 0;
        foreach (uint[] w in instructions)
        {
            ushort op = (ushort)w[0];
            if (op == 61 && resources.Contains(w[3]))
            { removed.Add(w[2]); if (w[3] == image.Device.VariableId) images.Add(w[2]); continue; }
            if (op == 86 && images.Contains(w[3]))
            {
                if (!removed.Contains(w[4])) throw new InvalidDataException("Null IES uses a different original sampler.");
                sampled.Add(w[2]); removed.Add(w[2]); continue;
            }
            if (op is 87 or 88 && sampled.Contains(w[3]))
            {
                uint[] vector = instructions.Single(t => (ushort)t[0] == 23 && t[1] == w[1]);
                if (vector[3] != 4 || !instructions.Any(t => (ushort)t[0] == 22 && t[1] == vector[2] && t[2] == 32))
                    throw new InvalidDataException("Null IES sample differs from original float4 output.");
                if (!zeros.TryGetValue(w[1], out uint zero)) zeros.Add(w[1], zero = next++);
                body.Add(Op(83, w[1], w[2], zero)); replaced++; continue;
            }
            body.Add(w); retained++;
        }
        if (replaced != 2) throw new InvalidDataException("Null IES sample schedule differs from the two pinned original PS1 samples.");
        var final = new List<uint>(); uint[] header = Words(input).Take(5).ToArray(); header[3] = next; final.AddRange(header);
        bool inserted = false;
        foreach (uint[] w in body)
        {
            ushort op = (ushort)w[0];
            if (op is 5 or 71 && removed.Contains(w[1])) continue;
            if (op == 59 && resources.Contains(w[2])) continue;
            if (op == 54 && !inserted)
            { foreach (var zero in zeros) final.AddRange(Op(46, zero.Key, zero.Value)); inserted = true; }
            final.AddRange(w);
        }
        byte[] bytes = new byte[final.Count * 4];
        for (int i = 0; i < final.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), final[i]);
        SpirvShaderInterface result = SpirvShaderInterfaceReader.Read(bytes);
        if (result.Descriptors.Count != deviceBindings.Interface.Descriptors.Count - 2 ||
            result.StageVariables.Count != sourceBindings.Interface.StageVariables.Count ||
            result.Descriptors.Any(d => resources.Contains(d.VariableId)))
            throw new InvalidDataException("Optional null specialization changed the remaining source interface.");
        uint[] slots = new[] { 0u }.Concat(prior?.NullPixelRegisters ?? []).Order().ToArray();
        string[] evidence = new[] { ies.SourceEvidence }.Concat(prior?.SourceEvidence ?? []).ToArray();
        return new(bytes, result.Sha256, pixel.SpirvSha256, Array.AsReadOnly(slots), Array.AsReadOnly(evidence),
            replaced + (prior?.ReplacedSamples ?? 0), retained);
    }

    public static OriginalTranslatedShaderBindings ReadDeviceBindings(OriginalTranslatedShaderBindings original,
        OriginalDeferredNullImageSpirv specialized)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(specialized);
        if (original.Interface.Sha256 != OriginalDeferredNullImageAdapter.PixelTranslationSha256 ||
            specialized.OriginalTranslationSha256 != original.Interface.Sha256 ||
            Convert.ToHexString(SHA256.HashData(specialized.Bytes)) != specialized.Sha256 ||
            !specialized.NullPixelRegisters.Contains(0u) || specialized.NullPixelRegisters.Any(s => s is not (0 or 6 or 10)) ||
            specialized.NullPixelRegisters.Distinct().Count() != specialized.NullPixelRegisters.Count)
            throw new InvalidDataException("Optional Deferred device bindings require the exact validated specialization identity.");
        var slots = specialized.NullPixelRegisters.ToHashSet();
        var subset = original.OriginalDefinitions with { Bindings = Array.AsReadOnly(original.OriginalDefinitions.Bindings
            .Where(b => b.InputType is not (2 or 3) || !slots.Contains(b.BindPoint)).ToArray()) };
        var device = OriginalTranslatedShaderBindingReader.Read(subset, specialized.Bytes);
        return device with { OriginalDefinitions = original.OriginalDefinitions };
    }

    public static OriginalTranslatedShaderBindings ReadDeviceBindings(OriginalTranslatedShaderBindings original,
        OriginalDeferredOptionalLocalAmbientNullSpirv specialized)
    {
        ArgumentNullException.ThrowIfNull(specialized); specialized.Ies.Validate();
        _ = OriginalDeferredSourceLocalAmbientNullAdapter.ReadDeviceBindings(original, specialized.LocalAmbient);
        var optional = specialized.OptionalImages;
        uint[] expected = new[] { 0u }.Concat(specialized.LocalAmbient.Preceding2D?.NullPixelRegisters ?? []).Order().ToArray();
        if (optional.OriginalTranslationSha256 != original.Interface.Sha256 ||
            Convert.ToHexString(SHA256.HashData(optional.Bytes)) != optional.Sha256 ||
            !optional.NullPixelRegisters.SequenceEqual(expected) ||
            optional.ReplacedSamples != 2 + (specialized.LocalAmbient.Preceding2D?.ReplacedSamples ?? 0))
            throw new InvalidDataException("Composite IES/LAS device declarations differ from their validated source proof.");
        var slots = expected.ToHashSet();
        var subset = original.OriginalDefinitions with { Bindings = Array.AsReadOnly(original.OriginalDefinitions.Bindings
            .Where(b => !(b.InputType == 2 && b.BindPoint is 22 or 23 or 24) &&
                !(b.InputType is 2 or 3 && slots.Contains(b.BindPoint))).ToArray()) };
        var device = OriginalTranslatedShaderBindingReader.Read(subset, optional.Bytes);
        return device with { OriginalDefinitions = original.OriginalDefinitions };
    }

    private static uint[] Op(ushort code, params uint[] words)
        => new[] { (uint)((words.Length + 1) << 16) | code }.Concat(words).ToArray();
    private static uint[] Words(ReadOnlySpan<byte> bytes)
    { var words = new uint[bytes.Length / 4]; for (int i = 0; i < words.Length; i++) words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(4 * i)..]); return words; }
    private static List<uint[]> Parse(ReadOnlySpan<byte> bytes)
    {
        uint[] words = Words(bytes); var list = new List<uint[]>();
        for (int at = 5; at < words.Length;)
        { int count = (int)(words[at] >> 16); if (count == 0 || count > words.Length - at) throw new InvalidDataException("Truncated optional Deferred SPIR-V."); list.Add(words.AsSpan(at, count).ToArray()); at += count; }
        return list;
    }
}

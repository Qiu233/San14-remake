using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Receiver sampler inputs from the actual depth atlas declaration
/// and the shadow manager's one-mip point wrapper construction.</summary>
public static class ShadowReceiverSamplerEncoding
{
    public static void WriteNativeInputs(GameResourceRepository repository, string locale, string inputRoot)
    {
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var textures = repository.LoadObjects("system", 0x93FB3280);
        var atlas = RenderGraphTargetResources.Read(graph, 17, textures).DepthTarget
            ?? throw new InvalidDataException("The shadow atlas operation requires its declared depth target.");
        if (atlas.PixelFormat != 0x4E)
            throw new NotSupportedException("The receiver atlas sampler requires its supported depth class.");
        var rules = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, locale);
        byte[] shadow = RenderSamplerSourceAdapters.FinalRenderTextureKey(rules, atlas);
        // The depth-class setter replaces address/filter bits, preserving the
        // declaration's mip limits and bias. The subsequent shadow queue enables comparison.
        ushort low = BinaryPrimitives.ReadUInt16LittleEndian(shadow);
        ushort high = BinaryPrimitives.ReadUInt16LittleEndian(shadow.AsSpan(2));
        BinaryPrimitives.WriteUInt16LittleEndian(shadow, (ushort)((low & 0x03F0) | 0x1405));
        BinaryPrimitives.WriteUInt16LittleEndian(shadow.AsSpan(2), (ushort)(high | 8));
        byte[] point = RenderSamplerSourceAdapters.RenderTextureKey(rules, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(point,
            (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(point) & 0x03FF));
        string directory = Path.Combine(inputRoot, "normal-pattern-receiver-input");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "shadow-atlas-object.txt"),
            atlas.Object.ObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");
        File.WriteAllBytes(Path.Combine(directory, "shadow-sampler-desc.bin"),
            RenderSamplerSourceAdapters.DecodeKey(rules, shadow).EncodeD3D11Descriptor());
        File.WriteAllBytes(Path.Combine(directory, "point-sampler-desc.bin"),
            RenderSamplerSourceAdapters.DecodeKey(rules, point).EncodeD3D11Descriptor());
        File.WriteAllBytes(Path.Combine(directory, "default-sampler-desc.bin"),
            RenderSamplerSourceAdapters.BackendDefaultDescriptor(rules));
    }
}

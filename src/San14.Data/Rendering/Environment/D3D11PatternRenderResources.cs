using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The selected Sky's four ordered Pattern target declarations and
/// output samplers. Allocation and generated contents belong to its native Sky.</summary>
public sealed class D3D11PatternRenderResources
{
    public const string NativeDirectory = "pattern-input";
    private readonly byte[] targets;
    private readonly byte[][] samplers;

    private D3D11PatternRenderResources(GameResourceRepository repository,
        LoadedSkyAmbientResources ambient, string locale)
    {
        if (ambient.PatternTargets.Count != 4)
            throw new InvalidDataException("Sky Pattern resources require four ordered target declarations.");
        targets = new byte[136];
        int cursor = 0;
        void Word(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(targets.AsSpan(cursor, 4), value);
            cursor += 4;
        }
        Word(0x31505450); // PTP1: four Pattern target descriptions.
        Word(4);
        var nativeSamplers = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, locale);
        samplers = new byte[4][];
        for (int index = 0; index < 4; index++)
        {
            var target = ambient.PatternTargets[index];
            var formats = RenderTextureFormatMath.Resolve(target);
            if (target.Kind != RenderTextureKind.Texture2D || target.Width <= 0 || target.Height <= 0 ||
                target.LevelNum != 1 || target.MinMipLevel != 0 || target.MultiSample != 0 ||
                formats != new RenderTextureFormats(61, 61, 61))
                throw new NotSupportedException("Pattern requires single-level, single-sample R8 target resources.");
            Word(target.Object.ObjectId);
            Word(checked((uint)target.Width));
            Word(checked((uint)target.Height));
            Word(target.LevelNum);
            Word(formats.ResourceFormat);
            Word(formats.ShaderViewFormat);
            Word(formats.TargetViewFormat);
            Word(1); // The declaration's zero multisample code resolves to one physical sample.
            var key = RenderSamplerSourceAdapters.FinalRenderTextureKey(nativeSamplers, target);
            samplers[index] = RenderSamplerSourceAdapters.DecodeKey(nativeSamplers, key).EncodeD3D11Descriptor();
        }
    }

    public static D3D11PatternRenderResources Load(GameResourceRepository repository,
        LoadedSkyAmbientResources ambient, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(ambient);
        return new(repository, ambient, locale);
    }

    public void WriteNativeInputs(string inputRoot)
    {
        string directory = Path.Combine(inputRoot, NativeDirectory);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "targets.bin"), targets);
        for (int index = 0; index < samplers.Length; index++)
            File.WriteAllBytes(Path.Combine(directory, $"target{index}-sampler-desc.bin"), samplers[index]);
    }
}

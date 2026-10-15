using San14.Data;
using System.Security.Cryptography;

public sealed partial class DeferredPixelProgram
{
    public DeferredEarlyCausticsNullSpirv? EarlyCausticsNullSpecialization { get; private init; }

    public static async Task<DeferredPixelProgram> BuildAsync(
        DeferredEarlyF103SelectedSource selected, Vkd3dShaderCompiler compiler,
        DeferredEarlyCausticsSourceNull sourceNull)
    {
        ArgumentNullException.ThrowIfNull(selected); ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(sourceNull);  sourceNull.Validate(selected);
        var source = selected.SourcePlan.Shader;
        byte[] vertex = source.Container.GetProgramBytes(source.Source.Bytes, selected.VertexProgramIndex).ToArray();
        if (Convert.ToHexString(SHA256.HashData(vertex)) != "C51D71A59DC89E4B23B68395180A41473B35914AD33BCE80C92AF636A379757D")
            throw new InvalidDataException("Early Caustics program requires the original audited fullscreen vertex program.");
        var original = compiler.Compile(source.Container.GetProgramBytes(source.Source.Bytes, selected.PixelProgramIndex));
        var sourceBindings = TranslatedShaderBindingReader.Read(selected.PixelDefinitions, original.Bytes);
        var adapted = DeferredEarlyCausticsNullAdapter.Adapt(original, sourceBindings, selected, sourceNull);
        var translated = original with { Bytes = adapted.Bytes, SpirvSha256 = adapted.Sha256 };
        var bindings = DeferredEarlyCausticsNullAdapter.ReadDeviceBindings(sourceBindings, adapted);
        return await InstallAsync(translated.Bytes,"OriginalEarly95SourceCausticsNull",
            "Early fullscreen adapter compilation failed: ",
            "Device rejected the source early Caustics NULL program.",
            (shader,resources)=>new DeferredPixelProgram(shader,resources,source,selected.Combination,translated,bindings,original,sourceBindings,null)
                { EarlyCausticsNullSpecialization = adapted });
    }
}

using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The project's implementations of the map's stock screen stages.</summary>
internal static class ProjectStockShaders
{
    internal sealed record Program(byte[] Dxbc, string ResourceUri, string Sha256,
        DxbcContainer Container, byte[] ResourceDefinitionBytes, DxbcResourceDefinitions Definitions,
        DxbcProgramSignatures Signatures);

    private static readonly Lazy<Program> VertexProgram = new(() => Compile("Vertex", "main", "vs_5_0"));
    private static readonly Lazy<Program> Default1Program = new(() => Compile("Copy", "Default1", "ps_5_0"));
    private static readonly Lazy<Program> Default3Program = new(() => Compile("Copy", "Default3", "ps_5_0"));
    private static readonly Lazy<Program> DepthProgram = new(() => Compile("Depth", "main", "ps_5_0"));
    private static readonly Lazy<Program> ToneProgram = new(() => Compile("Tone", "main", "ps_5_0"));
    private static readonly Lazy<Program> Default5Program = new(() => Compile("CopyMrt", "main", "ps_5_0"));
    private static readonly Lazy<Program> Common140Program = new(() => Compile("ShadowCompose", "Common140", "ps_5_0"));
    private static readonly Lazy<Program> Common138Program = new(() => Compile("ShadowCompose", "Common138", "ps_5_0"));
    internal static Program Vertex => VertexProgram.Value;
    internal static Program Default1 => Default1Program.Value;
    internal static Program Default3 => Default3Program.Value;
    internal static Program Depth => DepthProgram.Value;
    internal static Program Tone => ToneProgram.Value;
    internal static Program Default5 => Default5Program.Value;
    internal static Program Common140 => Common140Program.Value;
    internal static Program Common138 => Common138Program.Value;

    internal static void RequireLocale(string locale)
    {
        if (locale is not ("SC" or "TC"))
            throw new ArgumentOutOfRangeException(nameof(locale), locale, "Map source locale must be SC or TC.");
    }

    private static Program Compile(string kind, string entryPoint, string target)
    {
        byte[] bytes = ProjectHlslCompiler.CompileEmbedded($"ProjectStockShader.{kind}.hlsl", entryPoint, target);
        var container = DxbcContainerReader.Read(bytes);
        byte[] rdef = container.Parts.Single(p => p.FourCc == "RDEF").PayloadRange.Slice(bytes).ToArray();
        return new(bytes, $"san14://project/shaders/stock-{kind.ToLowerInvariant()}/{entryPoint}",
            Convert.ToHexString(SHA256.HashData(bytes)), container, rdef,
            DxbcResourceDefinitionReader.Read(bytes), DxbcSignatureReader.Read(bytes));
    }
}

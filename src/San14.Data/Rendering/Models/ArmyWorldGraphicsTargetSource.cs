using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Accepted original node48 attachment declaration. Acceptance is a
/// source load boundary; normal submissions inspect the actual bound views.</summary>
public sealed class ArmyWorldGraphicsTargetSource
{
    private ArmyWorldGraphicsTargetSource(LoadedRenderGraphTargets source) => Source = source;
    public LoadedRenderGraphTargets Source { get; }
    public const string GraphSha256 = "B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";
    public static ArmyWorldGraphicsTargetSource Read(LoadedRenderGraphTargets actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (actual.Graph.Source.OverrideSource is not null || actual.TextureObjects.Source.OverrideSource is not null ||
            actual.Graph.Source.ResourceUri != GameResourceId.Rdb("system", 0x024496EE).ToString() ||
            actual.Graph.Source.DecodedSha256 != GraphSha256 ||
            Convert.ToHexString(SHA256.HashData(actual.Graph.Source.Bytes)) != GraphSha256 ||
            Convert.ToHexString(SHA256.HashData(actual.TextureObjects.Source.Bytes)) != actual.TextureObjects.Source.DecodedSha256)
            throw new InvalidDataException("Army node48 requires its original accepted graph and attachment declarations.");
        var graph = actual.Graph with { Document = RenderGraphReader.Read(actual.Graph.Source.Bytes) };
        var objects = actual.TextureObjects with { Document = DokReader.Read(actual.TextureObjects.Source.Bytes) };
        var source = RenderGraphTargetResources.Read(graph, 48, objects);
        if (actual.Node.Index != 48 || actual.Flag != source.Flag || actual.ColorRenderTargetNum != 6 ||
            !actual.ColorTargets.Select(t => (t.Slot, t.ObjectNameHash, t.SurfaceId))
                .SequenceEqual(source.ColorTargets.Select(t => (t.Slot, t.ObjectNameHash, t.SurfaceId))) ||
            actual.DepthObjectNameHash != source.DepthObjectNameHash || actual.DepthSurfaceId != source.DepthSurfaceId ||
            source.Flag != 6 || source.DepthObjectNameHash != 0xE13DDB4F || source.DepthSurfaceId != 0 ||
            !source.ColorTargets.Select(t => (t.ObjectNameHash, t.SurfaceId)).SequenceEqual(new (uint,uint)[]
            { (0x8B18CE44,0), (0x7E94C315,0), (0x521C7DED,0), (0x85C125A6,0), (0x521C7DED,2), (0x85C125A6,2) }))
            throw new InvalidDataException("Army graphics requires the exact node48 six color surfaces and E13 depth/stencil.");
        return new(source);
    }
}

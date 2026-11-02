using System.Text;

namespace San14.Data;

/// <summary>The actual immutable Landscape program selection and its prepared
/// source locator. This is CPU source publication, never GPU readiness.</summary>
public sealed record D3D11TerrainPreparedPrograms(string LogicalKey, string Identity, string Directory)
{
    public const string FormatSignature = "SAN14_TERRAIN_PROGRAMS 1";

    /// <summary>An opening publishes only which already loaded program bank it
    /// borrows. The device owner must admit the actual matching GPU bank.</summary>
    public void WriteReference(string terrainDirectory, LoadedTerrainGbiMapResources material)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(terrainDirectory);
        var source = D3D11TerrainProgramSources.From(material);
        if (source.LogicalKey != LogicalKey || source.Identity != Identity)
            throw new InvalidDataException("Opening terrain differs from its startup program source.");
        File.WriteAllText(Path.Combine(terrainDirectory, "terrain-programs.ref"),
            FormatSignature + "\n" + LogicalKey + "\n" + Identity + "\n", Encoding.ASCII);
    }
}

/// <summary>Camera-free immutable source for the ordinary near/coarse shaders.
/// Decoded identity comes from the resident resource loader; no cache hit reads
/// or hashes files. Mutable instances, constants and textures are not included.</summary>
public sealed class D3D11TerrainProgramSources
{
    private readonly LoadedShaderContainer shader;
    private readonly GameResourceId resource;
    private readonly int nearVertex, nearPixel, coarseVertex, coarsePixel;
    private readonly object preparationGate = new();
    private D3D11TerrainPreparedPrograms? prepared;

    private D3D11TerrainProgramSources(LoadedTerrainGbiMapResources material)
    {
        shader = material.Shader.Shader;
        resource = GameResourceId.Parse(shader.Source.ResourceUri);
        if (resource.Kind != "rdb" || shader.Source.DecodedSha256.Length != 64 ||
            !shader.Source.DecodedSha256.All(Uri.IsHexDigit) ||
            !ReferenceEquals(material.DetailProgram.Source, shader) ||
            !ReferenceEquals(material.CoarseProgram.Source, shader))
            throw new InvalidDataException("Terrain programs require one actual decoded shader source.");
        nearVertex = material.DetailProgram.VertexProgramIndex;
        nearPixel = material.DetailProgram.PixelProgramIndex;
        coarseVertex = material.CoarseProgram.VertexProgramIndex;
        coarsePixel = material.CoarseProgram.PixelProgramIndex;
        if (nearVertex < 0 || nearPixel < 0 || coarseVertex < 0 || coarsePixel < 0)
            throw new InvalidDataException("Terrain program selection contains an invalid source index.");
        string selection = FormattableString.Invariant($"{nearVertex}:{nearPixel}:{coarseVertex}:{coarsePixel}:layout-v1");
        LogicalKey = resource.ToString().ToLowerInvariant() + ":" + selection;
        Identity = shader.Source.DecodedSha256.ToUpperInvariant() + ":" + selection;
    }

    public string LogicalKey { get; }
    public string Identity { get; }

    public static D3D11TerrainProgramSources From(LoadedTerrainGbiMapResources material)
    {
        ArgumentNullException.ThrowIfNull(material);
        return new(material);
    }

    /// <summary>Publishes four real DXBC inputs before the application admits
    /// Title. A caller may reuse its preceding immutable publication only when
    /// this category's actual source identity and cache namespace still match.</summary>
    public D3D11TerrainPreparedPrograms PrepareNativeInputs(string cacheRoot, CancellationToken token,
        D3D11TerrainPreparedPrograms? previous = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);
        string root = Path.GetFullPath(cacheRoot);
        string selection = FormattableString.Invariant($"{nearVertex}-{nearPixel}-{coarseVertex}-{coarsePixel}-layout-v1");
        string directory = Path.GetFullPath(Path.Combine(root, "rdb",
            resource.Key.Replace('/', Path.DirectorySeparatorChar), selection, shader.Source.DecodedSha256));
        lock (preparationGate)
        {
            token.ThrowIfCancellationRequested();
            if (prepared is not null)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(prepared.Directory, directory))
                    throw new InvalidOperationException("A terrain program source cannot change its prepared cache namespace.");
                return prepared;
            }
            if (previous is not null && previous.LogicalKey == LogicalKey && previous.Identity == Identity &&
                StringComparer.OrdinalIgnoreCase.Equals(previous.Directory, directory))
                return prepared = previous;

            Directory.CreateDirectory(directory);
            WriteProgram("near-vs.dxbc", nearVertex);
            WriteProgram("near-ps.dxbc", nearPixel);
            WriteProgram("coarse-vs.dxbc", coarseVertex);
            WriteProgram("coarse-ps.dxbc", coarsePixel);
            // The manifest is the publication boundary and is installed only
            // after every required code file has been atomically replaced.
            WriteAtomic(Path.Combine(directory, "terrain-programs.txt"),
                Encoding.ASCII.GetBytes(D3D11TerrainPreparedPrograms.FormatSignature + "\n" +
                    LogicalKey + "\n" + Identity + "\n"), token);
            token.ThrowIfCancellationRequested();
            return prepared = new(LogicalKey, Identity, directory);

            void WriteProgram(string filename, int programIndex)
            {
                token.ThrowIfCancellationRequested();
                WriteAtomic(Path.Combine(directory, filename),
                    shader.Container.GetProgramBytes(shader.Source.Bytes, programIndex), token);
            }
        }
    }

    private static void WriteAtomic(string path, ReadOnlySpan<byte> bytes, CancellationToken token)
    {
        // A temporary name is only an unpublished writer artifact. Neither it
        // nor the prepared path participates in the resource's logical identity.
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            token.ThrowIfCancellationRequested();
            File.WriteAllBytes(temporary, bytes);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

using System.Numerics;
using System.Text;

namespace San14.Data;

/// <summary>The selected atmosphere's complete CPU tables and ordered Plane.
/// Existing Sky and Cloud images are shared by identity; only unsampled Plane
/// entries need additional device media.</summary>
public sealed class D3D11SkyAtmosphereResources
{
    public const string NativeDirectory = "atmosphere-input";
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    private LoadedSkyAtmoTableSources acceptedTables;

    private D3D11SkyAtmosphereResources(LoadedSkyAtmoTableSources tables,
        SkyAppliedSeasonCloudTextureSources sources, D3D11AmbientWorkConstants work)
    {
        if (tables.PlaneStaObjectId != sources.RequireAtmospherePlane().ObjectId ||
            !work.CloudTextures.SameSta(SkyAppliedCloudTextureSet.Import(sources)))
            throw new InvalidDataException("Atmosphere tables and device images must belong to the same selected Plane.");
        acceptedTables = tables;

        using var settings = new MemoryStream();
        using (var writer = new BinaryWriter(settings, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x31544153u); // SAT1: selected atmosphere settings and raw table identities.
            writer.Write(tables.ObjectId);
            writer.Write(tables.PlaneStaObjectId);
            writer.Write(checked((uint)tables.Width));
            writer.Write(checked((uint)tables.Height));
            writer.Write(tables.Flags);
            writer.Write(tables.Transmittance.FileId);
            writer.Write(tables.Ambient2D.FileId);
            void Vector(Vector4 value)
            {
                writer.Write(value.X); writer.Write(value.Y);
                writer.Write(value.Z); writer.Write(value.W);
            }
            Vector(tables.MieExtinction);
            Vector(tables.RayleighExtinction);
            writer.Write(tables.MieHeight);
            writer.Write(tables.RayleighHeight);
            writer.Write(tables.PlanetRadius);
            writer.Write(tables.AtmoTopHeight);
        }
        files.Add("settings.bin", settings.ToArray());
        files.Add("transmittance-half4.bin", tables.Transmittance.CopyBytes());
        files.Add("ambient-half4.bin", tables.Ambient2D.CopyBytes());

        var existing = work.CloudTextures.CloudInputs.Select(input => input.Texture)
            .Concat(work.SkyMedia.Textures.Select(input => input.Texture)).ToArray();
        var planeRows = new List<string>();
        var additionalRows = new List<string>();
        for (int index = 0; index < work.CloudTextures.StaInputs.Count; index++)
        {
            var texture = work.CloudTextures.StaInputs[index];
            uint objectId = sources.RequireAtmospherePlane().Entries[index].Definition.ObjectId;
            planeRows.Add(FormattableString.Invariant($"{index} {objectId} {texture.ResourceUri} {texture.ImageIndex} {texture.NativeViewFormat} {texture.SourceSha256} {texture.SamplerDescriptorSha256}"));
            if (existing.Any(candidate => candidate.Identity == texture.Identity &&
                candidate.SamplerDescriptorSha256 == texture.SamplerDescriptorSha256)) continue;
            additionalRows.Add(SkyTextureInputEncoding.Add(files, "", "atmosphere-plane",
                checked((uint)index), $"plane{index}", texture));
        }
        files.Add("plane.tsv", Encoding.UTF8.GetBytes(string.Join("\n", planeRows) + "\n"));
        files.Add("textures.tsv", Encoding.UTF8.GetBytes(string.Join("\n", additionalRows) +
            (additionalRows.Count == 0 ? "" : "\n")));
    }

    public static D3D11SkyAtmosphereResources Load(LoadedSkyAtmoTableSources tables,
        SkyAppliedSeasonCloudTextureSources sources, D3D11AmbientWorkConstants work)
        => new(tables, sources, work);

    /// <summary>Numeric frame updates may borrow this resource generation.
    /// Replacing its tables or constructor settings requires a new Native owner.</summary>
    public void RequireCurrent(LoadedSkyAtmoTableSources tables)
    {
        if (ReferenceEquals(tables, acceptedTables)) return;
        if (!acceptedTables.SameResources(tables))
            throw new NotSupportedException("Changed atmosphere tables or settings require scene resource replacement.");
        acceptedTables = tables;
    }

    public void WriteNativeInputs(string inputRoot)
    {
        string directory = Path.Combine(inputRoot, NativeDirectory);
        Directory.CreateDirectory(directory);
        foreach (var file in files) File.WriteAllBytes(Path.Combine(directory, file.Key), file.Value);
    }
}

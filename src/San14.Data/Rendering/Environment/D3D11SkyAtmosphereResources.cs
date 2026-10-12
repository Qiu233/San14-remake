using System.Numerics;
using System.Text;

namespace San14.Data;

/// <summary>The selected Camera's atmosphere declaration and imported resources.
/// The primary provider takes precedence over the independently declared Plane
/// fallback. Each selected provider retains only its declared raw tables and
/// shares the actual prepared Sky and Cloud images.</summary>
public sealed class D3D11SkyAtmosphereResources
{
    public const string NativeDirectory = "atmosphere-input";
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    private LoadedSkyAtmoTableSources? acceptedTables;
    private readonly SkyAppliedCloudTextureSet? images;
    private readonly uint declaredAtmosphere, declaredFallbackPlane;

    private D3D11SkyAtmosphereResources(SkySeasonMainCameraSource source,
        SkyAppliedCloudTextureSet? images)
    {
        ArgumentNullException.ThrowIfNull(source);
        this.images = images;
        var tables = source.RenderingAtmosphere;
        var sources = source.CloudTextures;
        declaredAtmosphere = source.Controller.DeclaredTransmittanceTable;
        declaredFallbackPlane = sources.DeclaredFallbackPlane;
        if ((declaredAtmosphere != 0 ? declaredAtmosphere : declaredFallbackPlane) != (tables?.ObjectId ?? 0))
            throw new InvalidDataException("Atmosphere selection differs from its imported resource owner.");
        acceptedTables = tables;
        using (var selection = new MemoryStream())
        {
            using (var writer = new BinaryWriter(selection, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(0x31534153u); // SAS1: actual Static atmosphere and alternate Plane declarations.
                writer.Write(declaredAtmosphere);
                writer.Write(declaredFallbackPlane);
            }
            files.Add("selection.bin", selection.ToArray());
        }
        if (tables is null) return;
        if (images is null)
            throw new InvalidOperationException("A present atmosphere requires its actual prepared Plane images.");
        if (tables.PlaneStaObjectId != sources.RequireAtmospherePlane().ObjectId ||
            !images.SameSta(SkyAppliedCloudTextureSet.Import(sources)))
            throw new InvalidDataException("Atmosphere tables and device images must belong to the same selected Plane.");

        using var settings = new MemoryStream();
        using (var writer = new BinaryWriter(settings, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x31544153u); // SAT1: selected atmosphere settings and raw table identities.
            writer.Write(tables.ObjectId);
            writer.Write(tables.PlaneStaObjectId);
            writer.Write(checked((uint)tables.Width));
            writer.Write(checked((uint)tables.Height));
            writer.Write(tables.Flags);
            writer.Write(tables.TransmittanceTable?.FileId ?? 0);
            writer.Write(tables.AmbientTable?.FileId ?? 0);
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
        if(tables.TransmittanceTable is { } transmittance)
            files.Add("transmittance-half4.bin", transmittance.CopyBytes());
        if(tables.AmbientTable is { } ambient)
            files.Add("ambient-half4.bin", ambient.CopyBytes());

        var existing = images.CloudInputs.Select(input => input.Texture).ToArray();
        var planeRows = new List<string>();
        var additionalRows = new List<string>();
        for (int index = 0; index < images.StaInputs.Count; index++)
        {
            var texture = images.StaInputs[index];
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

    public static D3D11SkyAtmosphereResources Load(SkySeasonMainCameraSource source,
        SkyAppliedCloudTextureSet? images = null) => new(source, images);

    public bool HasRenderingAtmosphere => acceptedTables is not null;

    /// <summary>Prepare new immutable media when the selected tables, settings
    /// or Plane images change. Device consumers install this candidate together.</summary>
    public D3D11SkyAtmosphereResources? PrepareReplacement(SkySeasonMainCameraSource source,
        SkyAppliedCloudTextureSet candidateImages)
    {
        var tables = source.RenderingAtmosphere;
        // Activating or retiring optional draw media needs its own program and
        // device lifecycle. A retained resource replacement cannot supply it.
        if ((tables is null) != (acceptedTables is null) ||
            (source.Controller.DeclaredTransmittanceTable == 0) != (declaredAtmosphere == 0) ||
            (source.CloudTextures.DeclaredFallbackPlane == 0) != (declaredFallbackPlane == 0))
            throw new NotSupportedException("Changed atmosphere provider presence requires optional media preparation.");
        bool sameTables = ReferenceEquals(tables, acceptedTables) ||
            tables is not null && acceptedTables!.SameResources(tables);
        bool sameImages = ReferenceEquals(images, candidateImages) ||
            (images is null ? candidateImages.StaInputs.Count == 0 : images.SameSta(candidateImages));
        if (source.Controller.DeclaredTransmittanceTable == declaredAtmosphere &&
            source.CloudTextures.DeclaredFallbackPlane == declaredFallbackPlane && sameTables && sameImages)
        {
            acceptedTables = tables;
            return null;
        }
        return Load(source, candidateImages);
    }

    public void WriteNativeInputs(string inputRoot)
    {
        string directory = Path.Combine(inputRoot, NativeDirectory);
        Directory.CreateDirectory(directory);
        foreach (var file in files) File.WriteAllBytes(Path.Combine(directory, file.Key), file.Value);
    }
}

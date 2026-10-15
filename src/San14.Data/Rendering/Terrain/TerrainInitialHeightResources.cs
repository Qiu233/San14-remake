using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Explicit initial zero-brush height adapter. Later height edits are not represented.</summary>
public sealed class LoadedTerrainInitialHeightResources
{
    internal LoadedTerrainInitialHeightResources(LoadedTerrainResources terrain, LoadedTerrainTextureGroup field,
        TerrainTextureBinding heightBinding, TerrainTextureBinding controlBinding, FloatTexture height,
        Vector2 heightNormalInfo, DokProperty generatorReference, TerrainInitialHeightGenerator generatorProgram)
        => (Terrain, Field, HeightBinding, ControlBinding, Height, HeightNormalInfo, GeneratorReference, GeneratorProgram) =
            (terrain, field, heightBinding, controlBinding, height, heightNormalInfo, generatorReference, generatorProgram);
    public LoadedTerrainResources Terrain { get; }
    public LoadedTerrainTextureGroup Field { get; }
    public TerrainTextureBinding HeightBinding { get; }
    public TerrainTextureBinding ControlBinding { get; }
    public FloatTexture Height { get; }
    public Vector2 HeightNormalInfo { get; }
    public DokProperty GeneratorReference { get; }
    public TerrainInitialHeightGenerator GeneratorProgram { get; }
    public LoadedShaderContainer Generator => GeneratorProgram.Source;
}

public static class TerrainInitialHeightResources
{
    private const uint GeneratorProperty = 0xA8C0C79F;

    public static LoadedTerrainInitialHeightResources Load(GameResourceRepository repository,
        LoadedTerrainResources terrain, LoadedTerrainParameterResources parameters)
        => Load(repository, terrain, parameters, null);

    internal static LoadedTerrainInitialHeightResources Load(GameResourceRepository repository,
        LoadedTerrainResources terrain, LoadedTerrainParameterResources parameters, LoadedTerrainTextureGroup? field)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(parameters);
        if (terrain.Objects.Source.ResourceUri != parameters.Scene.TerrainBundle.Source.ResourceUri ||
            terrain.Objects.Source.DecodedSha256 != parameters.Scene.TerrainBundle.Source.DecodedSha256)
            throw new InvalidDataException("Initial height and Landscape parameter sources differ.");
        // Original DOK stores this bitfield as type5, despite its numeric flag meaning.
        DokProperty flags = Scalar(parameters.TerrainRoot, 0x05D92ACC, 5);
        if ((BinaryPrimitives.ReadUInt32LittleEndian(flags.RawValue) & 2) != 0)
            throw new InvalidDataException("Initial height adapter requires the original ordinary height mode.");
        DokProperty reference = Scalar(parameters.WrapperRoot, GeneratorProperty, 5);
        LoadedShaderContainer generator = repository.LoadShaderContainer("system", BinaryPrimitives.ReadUInt32LittleEndian(reference.RawValue));
        var generatorProgram = TerrainInitialHeightGenerator.Import(generator);
        field ??= TerrainTextureResources.LoadGroup(repository, terrain, TerrainTextureGroupKind.Field);
        TerrainTextureBinding height = Slot(field, 0), control = Slot(field, 1);
        G1TextureSamplerRecipe sampler = G1TextureSamplerReader.ReadRetainedSource(height.Image);
        if (height.Image.PixelFormat != 2 || height.Image.LayerCount != 1 || height.Image.MipCount != 1 ||
            sampler.Filter != 0 || sampler.AddressU != 3 || sampler.AddressV != 3 ||
            sampler.MinimumLodBits != 0 || sampler.MaximumLodBits != 0 || sampler.MipLodBiasBits != 0)
            throw new InvalidDataException("Initial height identity requires the original scalar point/clamp source with one mip and no LOD bias.");
        FloatTexture values = G1FloatTextureReader.Read(height.Image, height.Resource.Source.Bytes);
        // The CS uses (width>>5)+1 dispatch groups and denominator (groups*32-32).
        // Identity only holds for this square 32k+1 extent; do not silently rescale overrides.
        if (values.Width != values.Height || values.Width < 33 || (values.Width - 1) % 32 != 0 ||
            values.Width != terrain.HeightField.HeightField.SampleWidth || values.Height != terrain.HeightField.HeightField.SampleHeight)
            throw new InvalidDataException("Initial height source does not satisfy the verified square 32k+1 dispatch/identity dimensions.");
        var info = new Vector2(values.Width - 1, terrain.SpatialTree.RootSize / (values.Width - 1));
        return new(terrain, field, height, control, values, info, reference, generatorProgram);
    }

    public static float SamplePointClamp(FloatTexture height, Vector2 uv)
    {
        if (!float.IsFinite(uv.X) || !float.IsFinite(uv.Y)) throw new InvalidDataException("Height UV is not finite.");
        int x = Math.Clamp((int)MathF.Floor(Math.Clamp(uv.X, 0, 1) * height.Width), 0, height.Width - 1);
        int y = Math.Clamp((int)MathF.Floor(Math.Clamp(uv.Y, 0, 1) * height.Height), 0, height.Height - 1);
        return height.GetSample(x, y);
    }

    /// <summary>Original VS1 normal kernel uses texture resolution, independent of preview LOD step.</summary>
    public static Vector3 Normal(FloatTexture height, Vector2 uv, Vector2 info)
    {
        if (!float.IsFinite(info.X) || !float.IsFinite(info.Y) || info.X <= 0 || info.Y <= 0)
            throw new InvalidDataException("Invalid original height normal inputs.");
        float delta = (1f / info.X) * BitConverter.Int32BitsToSingle(0x3F8CCCCD);
        float xm = SamplePointClamp(height, new(uv.X - delta, uv.Y));
        float xp = SamplePointClamp(height, new(uv.X + delta, uv.Y));
        float zm = SamplePointClamp(height, new(uv.X, uv.Y - delta));
        float zp = SamplePointClamp(height, new(uv.X, uv.Y + delta));
        return Vector3.Normalize(new(xm - xp, 2 * info.Y, zm - zp));
    }

    private static TerrainTextureBinding Slot(LoadedTerrainTextureGroup field, uint slot) =>
        field.Bindings.SingleOrDefault(value => value.Slot == slot) ?? throw new InvalidDataException($"Missing Field source slot {slot}.");
    private static DokProperty Scalar(DokObject obj, uint hash, uint type)
    {
        DokProperty[] values = obj.Properties.Where(value => value.NameHash == hash).ToArray();
        if (values.Length != 1 || values[0].TypeCode != type || values[0].ElementCount != 1 || values[0].RawValue.Length != 4)
            throw new InvalidDataException($"Terrain object {obj.ObjectId:X8} requires scalar {hash:X8} type{type}.");
        return values[0];
    }
}

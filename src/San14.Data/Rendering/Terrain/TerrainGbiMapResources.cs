using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed class LoadedTerrainGbiMapResources
{
    internal LoadedTerrainGbiMapResources(LoadedTerrainParameterResources parameters,
        LoadedTerrainShaderResources shader, LoadedTerrainTextureGroup field, LoadedTerrainTextureGroup root,
        LoadedTerrainTextureGroup? auxiliary, LoadedTerrainInitialControlResources control,
        TerrainRendererPaletteProjection palette, G2ShaderFeatureRecord feature, G2ShaderFeatureRecord coarseFeature,
        TerrainShaderStages detail, TerrainShaderStages coarse, Vector2 nearFar, bool extraAlbedo)
        => (Parameters, Shader, Field, Root, Auxiliary, InitialControl, Palette, Feature, CoarseFeature,
            DetailProgram, CoarseProgram, NearFar, ExtraAlbedoEnabled) =
            (parameters, shader, field, root, auxiliary, control, palette, feature, coarseFeature,
                detail, coarse, nearFar, extraAlbedo);
    public LoadedTerrainParameterResources Parameters { get; }
    public LoadedTerrainShaderResources Shader { get; }
    public LoadedTerrainTextureGroup Field { get; }
    public LoadedTerrainTextureGroup Root { get; }
    public LoadedTerrainTextureGroup? Auxiliary { get; }
    public LoadedTerrainInitialControlResources InitialControl { get; }
    public TerrainRendererPaletteProjection Palette { get; }
    public G2ShaderFeatureRecord Feature { get; }
    public G2ShaderFeatureRecord CoarseFeature { get; }
    public TerrainShaderStages DetailProgram { get; }
    public TerrainShaderStages CoarseProgram { get; }
    public G2ShaderCombination Combination => DetailProgram.Combination;
    public G2ShaderCombination CoarseCombination => CoarseProgram.Combination;
    public float CliffThreshold => BitConverter.UInt32BitsToSingle(TerrainGbufferSourceParameters.CliffXBits);
    public Vector2 NearFar { get; }
    public bool ExtraAlbedoEnabled { get; }
}

/// <summary>Ordinary detail and coarse sources, stock initial state, with the replica's additional render-context texture gate disabled.</summary>
public static class TerrainGbiMapResources
{
    public static LoadedTerrainGbiMapResources LoadStockDefault(GameResourceRepository repository,
        LoadedTerrainResources terrain, LoadedTerrainParameterResources parameters)
    {
        if (terrain.Objects.Source.ResourceUri != parameters.Scene.TerrainBundle.Source.ResourceUri ||
            terrain.Objects.Source.DecodedSha256 != parameters.Scene.TerrainBundle.Source.DecodedSha256)
            throw new InvalidDataException("GBI material and terrain sources differ.");
        LoadedTerrainShaderResources shader = TerrainShaderResources.Load(repository, parameters);
        if (shader.Shader.Container.MaterialModeCount != 1)
            throw new NotSupportedException("Ordinary terrain requires the single-material parameter writer.");
        uint flags = U32(Scalar(parameters.TerrainRoot, 0x05D92ACC, 5).RawValue);
        if ((flags & 3) != 0) throw new InvalidDataException("GBI map requires ordinary mode with tessellation disabled.");
        uint extraAlbedo = U32(Scalar(terrain.FieldObject, 0x848CF8FC, 5).RawValue);
        uint extraNormal = U32(Scalar(terrain.FieldObject, 0x52CBB8AA, 5).RawValue);
        if (extraNormal != 0) throw new InvalidDataException("Additional normal texture path is not yet implemented.");
        byte[] raw = CreateGroup0Condition(flags, extraAlbedo != 0, false, false);
        var features = shader.Shader.ReadG2Features();
        G2ShaderFeatureRecord feature = features.Match(raw)
            ?? throw new InvalidDataException("Original shader feature matcher did not match the replica's explicit ordinary draw conditions.");
        G2ShaderCombination combination = shader.Shader.ReadCombination(feature.Index);
        G2ShaderFeatureRecord coarseFeature = features
            .Match(CreateGroup2Condition(extraAlbedo != 0, false, false))
            ?? throw new InvalidDataException("Original shader feature matcher did not match explicit coarse draw conditions.");
        G2ShaderCombination coarseCombination = shader.Shader.ReadCombination(coarseFeature.Index);
        var detailProgram = TerrainShaderStages.Import(shader.Shader, combination, false, extraAlbedo != 0);
        var coarseProgram = TerrainShaderStages.Import(shader.Shader, coarseCombination, true, extraAlbedo != 0);
        LoadedTerrainTextureGroup field = TerrainTextureResources.LoadGroup(repository, terrain, TerrainTextureGroupKind.Field);
        LoadedTerrainTextureGroup root = TerrainTextureResources.LoadGroup(repository, terrain, TerrainTextureGroupKind.Root);
        LoadedTerrainTextureGroup? auxiliary = extraAlbedo == 0 ? null :
            TerrainTextureResources.LoadGroup(repository, terrain, TerrainTextureGroupKind.Auxiliary);
        if (auxiliary is not null && (auxiliary.Bindings.Count != 1 || auxiliary.Bindings[0].Slot != 0))
            throw new InvalidDataException("Original ExAlbedo binding requires exactly one slot0 descriptor.");
        LoadedTerrainInitialControlResources control = TerrainInitialControlResources.LoadStockDefault(field);
        TerrainRendererPaletteProjection palette = TerrainRendererPalette.Project(parameters.DetailPaletteEntries);
        if (!palette.Resolved || palette.Entries.Count == 0)
            throw new InvalidDataException("GBI renderer palette is unresolved: " + string.Join(" ", palette.UnresolvedInputs));
        int layers = root.Bindings.Single(binding => binding.Slot == 0).Image.LayerCount;
        if (root.Bindings.Where(binding => binding.Slot < 3).Any(binding => binding.Image.LayerCount != layers))
            throw new InvalidDataException("GBI detail arrays have different layer counts.");
        foreach (TerrainRendererPaletteEntry entry in palette.Entries)
            if (entry.DetailTextureNameIndex >= layers)
                throw new InvalidDataException($"GBI palette {entry.ParameterIndex} references an unresolved detail array layer.");
        // Compact client arrays contain only proven source rows. Reject source
        // references to the untouched native tail rather than inventing its data.
        for (int at = 0; at < control.RgbaPixels.Length; at += 4)
            if (control.RgbaPixels[at] >= palette.Entries.Count ||
                control.RgbaPixels[at + 1] != 255 && control.RgbaPixels[at + 1] >= palette.Entries.Count)
                throw new InvalidDataException($"Initial control texel {at / 4} references an unwritten renderer palette row.");
        float near = F32(Scalar(parameters.TerrainRoot, 0x70CB1A2D, 8).RawValue);
        float range = F32(Scalar(parameters.TerrainRoot, 0x2913BB1D, 8).RawValue);
        if (!float.IsFinite(near) || !float.IsFinite(range) || range <= 0 || !float.IsFinite(near + range))
            throw new InvalidDataException("Invalid original NearFarBlendingOffset/Range.");
        return new(parameters, shader, field, root, auxiliary, control, palette, feature, coarseFeature,
            detailProgram, coarseProgram, new(near, near + range), extraAlbedo != 0);
    }
    public static byte[] CreateGroup0Condition(uint compositionFlags, bool extraAlbedo, bool extraNormal,
        bool renderContextExtraTexture)
    {
        uint[] words = [0, compositionFlags & 1, (compositionFlags >> 2) & 1, 0,
            extraAlbedo ? 1u : 0u, extraNormal ? 1u : 0u, renderContextExtraTexture ? 1u : 0u,
            (compositionFlags >> 3) & 1, (compositionFlags >> 4) & 1];
        byte[] raw = new byte[36];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(i * 4, 4), words[i]);
        return raw;
    }
    public static byte[] CreateGroup2Condition(bool extraAlbedo, bool extraNormal, bool renderContextExtraTexture,
        bool group2FeatureEnabled = true)
    {
        uint[] words = [group2FeatureEnabled ? 1u : 0u, 0, 0, 0, extraAlbedo ? 1u : 0u,
            extraNormal ? 1u : 0u, renderContextExtraTexture ? 1u : 0u, 0, 0];
        byte[] raw = new byte[36];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(i * 4, 4), words[i]);
        return raw;
    }
    private static DokProperty Scalar(DokObject obj, uint name, uint type)
    {
        DokProperty[] values = obj.Properties.Where(value => value.NameHash == name).ToArray();
        if (values.Length != 1 || values[0].TypeCode != type || values[0].ElementCount != 1 || values[0].RawValue.Length != 4)
            throw new InvalidDataException($"Terrain object {obj.ObjectId:X8} requires scalar {name:X8} type{type}.");
        return values[0];
    }
    private static uint U32(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    private static float F32(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadSingleLittleEndian(bytes);
}

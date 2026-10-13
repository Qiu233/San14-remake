using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record LoadedTerrainGbiMapResources(LoadedTerrainParameterResources Parameters,
    LoadedTerrainShaderResources Shader, LoadedTerrainTextureGroup Field, LoadedTerrainTextureGroup Root,
    LoadedTerrainTextureGroup? Auxiliary, LoadedTerrainInitialControlResources InitialControl,
    TerrainRendererPaletteProjection Palette, G2ShaderFeatureRecord Feature, G2ShaderCombination Combination,
    float CliffThreshold, Vector2 NearFar, bool ExtraAlbedoEnabled,
    G2ShaderFeatureRecord CoarseFeature, G2ShaderCombination CoarseCombination);

/// <summary>Ordinary detail and coarse sources, stock initial state, with the replica's additional render-context texture gate disabled.</summary>
public static class TerrainGbiMapResources
{
    private const string ShaderSha = "837B0FB842CEE4F358F34D36BB676C94F252AED67E2822A6228D39B06C93EF33";
    private const string Pixel52Sha = "08F898D88D527A70B8A4F0A8BF283E5C4ABFFCF7CC53D7992400005878A7D381";
    private const string Pixel54Sha = "7A99093D74CEA74D44FCCDE0D69C8A9D7AF4B1300A3A7F24B7195C784D9D93F9";

    public static LoadedTerrainGbiMapResources LoadStockDefault(GameResourceRepository repository,
        LoadedTerrainResources terrain, LoadedTerrainParameterResources parameters, string locale)
    {
        if (terrain.Objects.Source.ResourceUri != parameters.Scene.TerrainBundle.Source.ResourceUri ||
            terrain.Objects.Source.DecodedSha256 != parameters.Scene.TerrainBundle.Source.DecodedSha256)
            throw new InvalidDataException("GBI material and terrain sources differ.");
        LoadedTerrainShaderResources shader = TerrainShaderResources.Load(repository, parameters);
        if (shader.Shader.Source.DecodedSha256 != ShaderSha)
            throw new InvalidDataException("GBI map requires the verified ordinary Landscape shader implementation.");
        uint flags = U32(Scalar(parameters.TerrainRoot, 0x05D92ACC, 5).RawValue);
        if ((flags & 3) != 0) throw new InvalidDataException("GBI map requires ordinary mode with tessellation disabled.");
        uint extraAlbedo = U32(Scalar(terrain.FieldObject, 0x848CF8FC, 5).RawValue);
        uint extraNormal = U32(Scalar(terrain.FieldObject, 0x52CBB8AA, 5).RawValue);
        if (extraNormal != 0) throw new InvalidDataException("Additional normal texture path is not yet implemented.");
        byte[] raw = CreateGroup0Condition(flags, extraAlbedo != 0, false, false);
        G2ShaderFeatureRecord feature = G2ShaderFeatureReader.Read(shader.Shader.Source.Bytes).Match(raw)
            ?? throw new InvalidDataException("Original shader feature matcher did not match the replica's explicit ordinary draw conditions.");
        G2ShaderCombination combination = shader.Shader.ReadCombination(feature.Index);
        G2ShaderCombinationStage vs = combination.Stages.Single(stage => stage.StageCode == 1);
        G2ShaderCombinationStage ps = combination.Stages.Single(stage => stage.StageCode == 0);
        if (vs.Entry?.ProgramIndex != 0 || ps.Entry?.ProgramIndex is not (18 or 52) ||
            combination.Stages.Where(stage => stage.StageCode is 3 or 4 or 2 or 5).Any(stage => stage.Entry is not null))
            throw new InvalidDataException("Matched ordinary shader variant is not implemented by the map material adapter.");
        string pixelHash = Convert.ToHexString(SHA256.HashData(shader.Shader.Container.GetProgramBytes(shader.Shader.Source.Bytes, ps.Entry.ProgramIndex)));
        if (pixelHash != (ps.Entry.ProgramIndex == 18 ? TerrainGbiMapMaterialMath.PixelProgramSha256 : Pixel52Sha))
            throw new InvalidDataException("Matched pixel program changed.");
        G2ShaderFeatureRecord coarseFeature = G2ShaderFeatureReader.Read(shader.Shader.Source.Bytes)
            .Match(CreateGroup2Condition(extraAlbedo != 0, false, false))
            ?? throw new InvalidDataException("Original shader feature matcher did not match explicit coarse draw conditions.");
        G2ShaderCombination coarseCombination = shader.Shader.ReadCombination(coarseFeature.Index);
        G2ShaderCombinationStage coarseVs = coarseCombination.Stages.Single(stage => stage.StageCode == 1);
        G2ShaderCombinationStage coarsePs = coarseCombination.Stages.Single(stage => stage.StageCode == 0);
        if (coarseVs.Entry?.ProgramIndex != 2 || coarsePs.Entry?.ProgramIndex != 54 ||
            coarseCombination.Stages.Where(stage => stage.StageCode is 3 or 4 or 2 or 5).Any(stage => stage.Entry is not null))
            throw new InvalidDataException("Matched coarse shader variant is not implemented by the map material adapter.");
        if (Convert.ToHexString(SHA256.HashData(shader.Shader.Container.GetProgramBytes(shader.Shader.Source.Bytes, 2))) !=
                "1503D465E79F35C88CFDA8D2F6AC7AFC8E5397C47F94E08D7879D5DC70B39029" ||
            Convert.ToHexString(SHA256.HashData(shader.Shader.Container.GetProgramBytes(shader.Shader.Source.Bytes, 54))) != Pixel54Sha)
            throw new InvalidDataException("Matched coarse programs changed.");
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
        return new(parameters, shader, field, root, auxiliary, control, palette, feature, combination,
            BitConverter.Int32BitsToSingle(0x3F13B646), new(near, near + range), extraAlbedo != 0,
            coarseFeature, coarseCombination);
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

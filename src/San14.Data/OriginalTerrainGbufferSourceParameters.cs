using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The ordinary Landscape's owned fresh parameter table. CliffInfo
/// is the original slot2 float2 qword initialization, not a vector inferred
/// from a UI threshold, NearFar, or a diagnostic draw. Later named setters or
/// an imported/current native table require their own complete values.</summary>
public sealed class OriginalTerrainGbufferSourceParameters
{
    public const uint CliffXBits = 0x3F13B646;
    public const uint CliffYBits = 0;
    public const string ShaderSha256 = "837B0FB842CEE4F358F34D36BB676C94F252AED67E2822A6228D39B06C93EF33";
    private readonly Vector2 nearFar;
    private bool retained = true;

    private OriginalTerrainGbufferSourceParameters(LoadedTerrainGbiMapResources source, string locale, string exeSha,
        Vector2 sourceNearFar)
    {
        Locale = locale; ExecutableSha256 = exeSha; nearFar = sourceNearFar;
        SourceProvenance = $"ordinary-Landscape-owned-fresh-parameters/{locale}/{exeSha};" +
            $"{source.Shader.Shader.Source.ResourceUri}@{ShaderSha256};" +
            $"scene={source.Parameters.Scene.Scene.Source.ResourceUri}@{source.Parameters.Scene.Scene.Source.DecodedSha256};" +
            "slot2-vCliffInfo/type2-float2/group1D0;SC-FD4200/FD431F-TC-FD31C0/FD32DF;" +
            "qword000000003F13B646;ordinary-draw-retains-slot2;no-external-named-setter-history";
    }
    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public string SourceProvenance { get; }
    public uint NativeInitializerRva => Locale == "SC" ? 0xFD4200u : 0xFD31C0u;
    public uint NativeCliffQwordStoreRva => Locale == "SC" ? 0xFD431Fu : 0xFD32DFu;
    public bool RetainsOwnedConstructorCliffInfo => retained;

    /// <summary>Bind the proved SC/TC initializer to the exact ordinary
    /// source renderer and its actual48/50 program selections. Required source
    /// links are reparsed; changed scalar values or caller metadata cannot
    /// turn an edited renderer into a fresh source-default owner.</summary>
    public static OriginalTerrainGbufferSourceParameters ReadSourceDefaults(
        OriginalGameRepository repository, string locale, LoadedTerrainGbiMapResources loadedMaterial)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(loadedMaterial);
        if (locale is not ("SC" or "TC"))
            throw new InvalidDataException("Ordinary terrain parameter source requires exact SC or TC.");
        var parameters = loadedMaterial.Parameters;
        var shader = loadedMaterial.Shader.Shader;
        RequireOriginal(parameters.Scene.Scene.Source);
        RequireOriginal(parameters.Scene.TerrainBundle.Source);
        RequireOriginal(parameters.Wrapper.Source);
        RequireOriginal(shader.Source);
        if (shader.Source.FileId != 0xDC52FD1C || shader.Source.ResourceUri != GameResourceId.Rdb("system", 0xDC52FD1C).ToString() ||
            shader.Source.DecodedSha256 != ShaderSha256 ||
            loadedMaterial.Shader.Parameters.Scene.Scene.Source.DecodedSha256 != parameters.Scene.Scene.Source.DecodedSha256 ||
            loadedMaterial.Shader.Parameters.Wrapper.Source.DecodedSha256 != parameters.Wrapper.Source.DecodedSha256)
            throw new InvalidDataException("Terrain parameter owner differs from its exact ordinary shader/scene/wrapper source.");
        var wrapper = DokReader.Read(parameters.Wrapper.Source.Bytes).Objects.SingleOrDefault(item =>
            item.ObjectId == parameters.WrapperRoot.ObjectId && item.Kind == DokObjectKind.Inline && item.ClassId == 0x8E1ECB72) ??
            throw new InvalidDataException("Ordinary parameter owner requires its actual inline Landscape wrapper.");
        if (Scalar(wrapper, 0x3AF82377, 5) != 0xDC52FD1C || Scalar(wrapper, 0x445D803E, 5) != parameters.TerrainRoot.ObjectId)
            throw new InvalidDataException("Landscape wrapper does not identify this ordinary renderer and Composition.");
        var composition = DokReader.Read(parameters.Scene.TerrainBundle.Source.Bytes).Objects.SingleOrDefault(item =>
            item.ObjectId == parameters.TerrainRoot.ObjectId && item.Kind == DokObjectKind.Inline && item.ClassId == 0x774DDC07) ??
            throw new InvalidDataException("Ordinary parameter owner requires the exact Composition source.");
        if (Scalar(composition, 0x05D92ACC, 5) != 0x1C ||
            BitConverter.SingleToUInt32Bits(loadedMaterial.CliffThreshold) != CliffXBits)
            throw new InvalidDataException("Edited ordinary flags or scalar cliff value cannot select the constructor float2.");
        float offset = BitConverter.UInt32BitsToSingle(Scalar(composition, 0x70CB1A2D, 8));
        float range = BitConverter.UInt32BitsToSingle(Scalar(composition, 0x2913BB1D, 8));
        // Native SC D2EF62 / TC D2E232 performs one ADDSS, then passes both
        // floats to FD5EF0/FD4EE0 for slot3's two writes.
        float upper = offset + range;
        if (!float.IsFinite(offset) || !float.IsFinite(range) || !float.IsFinite(upper) || range <= 0 ||
            BitConverter.SingleToUInt32Bits(loadedMaterial.NearFar.X) != BitConverter.SingleToUInt32Bits(offset) ||
            BitConverter.SingleToUInt32Bits(loadedMaterial.NearFar.Y) != BitConverter.SingleToUInt32Bits(upper))
            throw new InvalidDataException("Current NearFar differs from the ordinary source record's ADDSS producer.");
        var container = G2ShaderContainerReader.Read(shader.Source.Bytes);
        var features = G2ShaderFeatureReader.Read(shader.Source.Bytes);
        var combinations = G2ShaderCombinationReader.Read(shader.Source.Bytes);
        ValidateSelection(loadedMaterial.Feature, loadedMaterial.Combination, 48, 0, 52);
        ValidateSelection(loadedMaterial.CoarseFeature, loadedMaterial.CoarseCombination, 50, 2, 54);
        foreach (int pixel in new[] { 52, 54 })
        {
            var rdef = DxbcResourceDefinitionReader.Read(container.GetProgramBytes(shader.Source.Bytes, pixel));
            var buffer = rdef.Buffers.Single();
            var cliff = buffer.Variables.Single(variable => variable.Name.Text == "vCliffInfo");
            var type = rdef.Types.Single(value => value.Offset == cliff.TypeOffset);
            if (cliff.StartOffset != (pixel == 52 ? 96 : 80) || cliff.ByteSize != 8 ||
                type.Class != 1 || type.BaseType != 3 || type.Rows != 1 || type.Columns != 2 || type.Elements != 0)
                throw new InvalidDataException("Actual near/coarse shader does not consume the original cliff float2 layout.");
        }
        string executableSha = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory, locale);
        return new(loadedMaterial, locale, executableSha, new(offset, upper));

        void ValidateSelection(G2ShaderFeatureRecord suppliedFeature, G2ShaderCombination suppliedCombination,
            int index, int vertex, int pixel)
        {
            var actualFeature = features.Records[index]; var actualCombination = combinations.Rows[index];
            if (suppliedFeature.Index != index || suppliedCombination.Index != index ||
                !suppliedFeature.RawBytes.AsSpan().SequenceEqual(actualFeature.RawBytes) ||
                !suppliedCombination.RawRow.AsSpan().SequenceEqual(actualCombination.RawRow) ||
                actualCombination.Stages.Single(stage => stage.StageCode == 1).Entry?.ProgramIndex != vertex ||
                actualCombination.Stages.Single(stage => stage.StageCode == 0).Entry?.ProgramIndex != pixel)
                throw new InvalidDataException("Ordinary source parameter selection differs from the actual48/50 program contract.");
        }
    }

    public Vector2 PrepareCliffInfo()
    {
        RequireRetained();
        return new(BitConverter.UInt32BitsToSingle(CliffXBits), BitConverter.UInt32BitsToSingle(CliffYBits));
    }
    /// <summary>Source ordinary per-group setter result, kept separate from
    /// slot2's constructor float2.</summary>
    public Vector2 PrepareNearFar() { RequireRetained(); return nearFar; }
    public void MarkImportedOrEdited(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter); retained = false; }

    private void RequireRetained()
    {
        if (!retained)
            throw new InvalidDataException("Terrain parameter source was imported, edited, or replaced; complete current cliff values are required.");
    }
    private static uint Scalar(DokObject value, uint name, uint type)
    {
        var property = value.Properties.SingleOrDefault(item => item.NameHash == name);
        if (property is null || property.TypeCode != type || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException($"Terrain source property{name:X8} lacks its exact typed scalar.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }
    private static void RequireOriginal(LoadedRdbAsset source)
    {
        if (source.OverrideSource is not null || Convert.ToHexString(SHA256.HashData(source.Bytes)) != source.DecodedSha256)
            throw new InvalidDataException("Ordinary source parameters require canonical unchanged source bytes.");
    }
}

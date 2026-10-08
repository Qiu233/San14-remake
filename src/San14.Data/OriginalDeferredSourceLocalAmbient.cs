using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum OriginalDeferredLocalAmbientStateScope
{
    SourceCommonGlobalInitializationNewDataNoAssignments,
    CurrentOrEditedState
}

/// <summary>The successful source Common initialization creates a new LAS data
/// owner (TC12FACE ->9B7330 ->9B7160), publishes it (12FAE2 ->9B7BB0), then a
/// new LocalAmbSpecMaps module captures that owner. No resource assignment or
/// global replacement may intervene. Ordinary LightData arrays do not prove
/// this state. There are no pixel bytes, typed texels or device handles here.</summary>
public sealed class OriginalDeferredSourceLocalAmbient
{
    public const string ShaderSourceSha256 = "95575321D097F29696A35F377B68DD91F37BB7F4EAE3061EB3CA501006021E38";
    public LoadedShaderContainer Shader { get; }
    public int CombinationIndex { get; }
    public OriginalDeferredLocalAmbientStateScope Scope { get; }
    public string ConstructionProvenance { get; }
    public G1ShaderConfigurationRecord Configuration { get; }
    public G2ShaderMetadataRecord CubeNamedMetadata { get; }
    public IReadOnlyList<uint> NullPixelRegisters { get; } = Array.AsReadOnly(new uint[] { 22, 23, 24 });
    /// <summary>Source s7 is shared with the nonnull global ambient t7. It is
    /// retained when the LAS cube-array samples are specialized.</summary>
    public uint SharedAmbientSamplerRegister => 7;
    public OriginalDeferredLocalAmbientInputs DataDefaults => OriginalDeferredLocalAmbientInputs.SuccessfulDataDefaults();
    public string SourceEvidence => ConstructionProvenance +
        ";SourceCommon12FACB:successful-new-LAS-data9B7330/9B7160;publish9B7BB0;module9B75A0:captures-same-global27DE608" +
        ";data18/20/30:null;pixel9B7860:t23/t24;pixel9B7954:ExtraSampler6+16=t22;fresh-module;no-global-replacement;no-data-assignments";

    private OriginalDeferredSourceLocalAmbient(LoadedShaderContainer shader, int combination,
        OriginalDeferredLocalAmbientStateScope scope, string provenance,
        G1ShaderConfigurationRecord configuration, G2ShaderMetadataRecord cube)
        => (Shader, CombinationIndex, Scope, ConstructionProvenance, Configuration, CubeNamedMetadata) =
            (shader, combination, scope, provenance, configuration, cube);

    public static OriginalDeferredSourceLocalAmbient FromFreshConstruction(LoadedShaderContainer shader,
        int explicitCombination, OriginalDeferredLocalAmbientStateScope explicitScope,
        string sourceConstructionProvenance)
    {
        ArgumentNullException.ThrowIfNull(shader);
        if (explicitScope != OriginalDeferredLocalAmbientStateScope.SourceCommonGlobalInitializationNewDataNoAssignments ||
            string.IsNullOrWhiteSpace(sourceConstructionProvenance))
            throw Error("requires successful source Common/global initialization, new LAS data/module and no intervening assignments");
        var (configuration, cube) = ReadSource(shader, explicitCombination);
        return new(shader, explicitCombination, explicitScope, sourceConstructionProvenance, configuration, cube);
    }

    public void Validate()
    {
        if (Scope != OriginalDeferredLocalAmbientStateScope.SourceCommonGlobalInitializationNewDataNoAssignments ||
            string.IsNullOrWhiteSpace(ConstructionProvenance))
            throw Error("current or edited state needs its actual applied LAS owner and resources");
        var (configuration, cube) = ReadSource(Shader, CombinationIndex);
        if (configuration.SourceOffset != Configuration.SourceOffset || cube.EncodedRange != CubeNamedMetadata.EncodedRange ||
            !cube.RawWords.SequenceEqual(CubeNamedMetadata.RawWords))
            throw Error("source declaration differs from the fresh construction proof");
    }

    private static (G1ShaderConfigurationRecord, G2ShaderMetadataRecord) ReadSource(
        LoadedShaderContainer shader, int combination)
    {
        if (shader.Source.OverrideSource is not null ||
            !shader.Source.ResourceUri.Equals("san14://rdb/system/9D8E811F", StringComparison.OrdinalIgnoreCase) ||
            !shader.Source.DecodedSha256.Equals(ShaderSourceSha256, StringComparison.OrdinalIgnoreCase) ||
            Convert.ToHexString(SHA256.HashData(shader.Source.Bytes)) != ShaderSourceSha256)
            throw Error("requires the complete exact original Deferred shader source");
        var resources = shader.ReadG1ResourceBindings(combination);
        var pixels = resources.Combination.Stages.Where(s => s.StageCode == 0 && s.Entry is not null).ToArray();
        if (pixels.Length != 1 || Convert.ToHexString(SHA256.HashData(shader.Container.GetProgramBytes(
            shader.Source.Bytes, pixels[0].Entry!.ProgramIndex))) != OriginalDeferredBufferSources.PixelSourceSha256)
            throw Error("requires the exact original PS1 combination");
        var configuration = G1ShaderConfigurationReader.Read(shader.Source.Bytes, shader.Container).Records
            .Single(r => r.Name == "LocalAmbSpecMaps" && r.NameHash == 0xC1BD0501);
        var expected = new (uint Hash, uint Value)[]
        {
            (0xC356A050, 23), (0xA119EC14, 24), (0x91F2D7E3, 0),
            (0x4B0D8EED, 3), (0xBAE590DA, 6), (0x3F8B880B, 3),
            (0xF6D96890, 6), (0xC514FB0A, 0), (0x4A3DC78C, 0)
        };
        if (configuration.RawSecondWord != 2 || !configuration.Parameters
                .Select(p => (p.NameHash, p.RawValue)).SequenceEqual(expected))
            throw Error("LocalAmbSpecMaps source configuration differs from its original constructor inputs");
        // LAS publishes its descriptor directly at ExtraSampler+16. It is not
        // one of the ordinary combination resource-name table's sixteen rows.
        var cubes = G2ShaderMetadataReader.Read(shader.Source.Bytes).Records.Where(e => e.Name?.Text == "sLclAmbSpc").ToArray();
        if (cubes.Length != 1 || cubes[0].RawWords[1] != 42 || cubes[0].RawWords[2] != 42)
            throw Error("local cube-array named metadata differs from the retained original source record");
        var definitions = shader.ReadResourceDefinitions(pixels[0].Entry!.ProgramIndex);
        foreach (var expectedBinding in new (uint Type, uint Slot, string Name, uint Dimension, uint Return)[]
        {
            (2, 22, "sLclAmbSpc", 10, 5), (2, 23, "tLasData", 1, 5),
            (2, 24, "tLasIndices", 1, 4), (3, 7, "__smpsAmbSpc", 0, 0)
        })
        {
            var bindings = definitions.Bindings.Where(b => b.InputType == expectedBinding.Type && b.BindPoint == expectedBinding.Slot).ToArray();
            if (bindings.Length != 1 || bindings[0].Name.Text != expectedBinding.Name || bindings[0].BindCount != 1 ||
                bindings[0].Dimension != expectedBinding.Dimension || bindings[0].ReturnType != expectedBinding.Return)
                throw Error("LAS null declaration differs from its exact original pixel consumer");
        }
        return (configuration, cubes[0]);
    }

    private static InvalidDataException Error(string message) => new("Deferred source LocalAmbient: " + message + ".");
}

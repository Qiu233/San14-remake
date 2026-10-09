namespace San14.Data;

/// <summary>D31D30's current saved-Cull, temporary Terrain draw-Cull and
/// post-call restoration. Commands are conditional on the actual prior
/// frontend state; they are not GPU submission receipts.</summary>
public readonly record struct OriginalGeneralSceneTerrainCullTransition(
    uint SavedSourceCull, uint SavedNativeCull, uint DrawSourceCull, uint DrawNativeCull,
    uint RestoredSourceCull, uint? EntryCommandWord, uint? ExitCommandWord);

/// <summary>Loaded full GeneralScene caller contract. D2D050's Flag2.bit0
/// gate is distinct from its earlier per-record mask test. Geometry depth
/// write/compare remain the actual earlier state; caller Cull does not.</summary>
public sealed class LoadedOriginalGeneralSceneTerrainCallerSource
{
    internal LoadedOriginalGeneralSceneTerrainCallerSource(string locale, string ruleVersion, uint terrainCull,
        uint[] nativeCullTable, uint factoryClassId = 0x1C8F048A)
    { Locale = locale; RuleVersion = ruleVersion; TerrainDrawSourceCull = terrainCull; this.nativeCullTable = nativeCullTable;
        FactoryClassId = factoryClassId; }
    private readonly uint[] nativeCullTable;
    public string Locale { get; }
    public string RuleVersion { get; }
    public uint FactoryClassId { get; }
    public uint TerrainDrawSourceCull { get; }
    public uint TerrainDrawNativeCull => NativeCull(TerrainDrawSourceCull);
    public string SourceProvenance => $"{RuleVersion};" +
        "GeneralScene-loader-and-CPU.v8;actual-arg12/CPU78=>Terrain-arg9=>F8-stack258-bit0-skip;" +
        "D31D30-saveE8/temporary-sourceCull2/D28C30/F8/restore-priorE8;source2=>D3DBack3;" +
        "DepthWrite-and-Compare-retained;not-GPU-receipt";

    public bool SuppressesOrdinaryTerrainF8(uint currentArgument9Flag2) => (currentArgument9Flag2 & 1) != 0;
    public OriginalGeneralSceneTerrainCullTransition PrepareTerrainCull(uint actualSavedSourceCull)
    {
        uint savedNative = NativeCull(actualSavedSourceCull), draw = TerrainDrawSourceCull;
        bool changed = actualSavedSourceCull != draw;
        return new(actualSavedSourceCull, savedNative, draw, NativeCull(draw), actualSavedSourceCull,
            changed ? (draw << 16) | 0x1Du : null,
            changed ? (actualSavedSourceCull << 16) | 0x1Du : null);
    }
    private uint NativeCull(uint source)
    {
        if (source is < 1 or > 3)
            throw new NotSupportedException("GeneralScene Terrain Cull closes the actual source codes1..3; no unknown prior raster fallback.");
        return nativeCullTable[source - 1];
    }
}

public static class OriginalGeneralSceneTerrainCallerSource
{
    public static LoadedOriginalGeneralSceneTerrainCallerSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadGeneralSceneTerrainCallerSource(repository.SourceDirectory, locale);
    }
    public static LoadedOriginalGeneralSceneTerrainCallerSource Create(string locale)
        => new(locale, RenderRuleIdentity.For("general-scene-terrain-caller", locale), 2, [1, 3, 2]);
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalGeneralSceneTerrainCallerSource LoadGeneralSceneTerrainCallerSource(string directory, string locale)
    {
        return OriginalGeneralSceneTerrainCallerSource.Create(locale);
    }
}

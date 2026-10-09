using San14.Data.Formats;

namespace San14.Data;

public sealed record ArmyTerrainSelection(ushort ArmyHexWord2A, int ResolvedHexSlot,
    byte RawHexTerrainByte10, S14TerrainTypeDefinition Terrain, SourceProvenance MapSource,
    SourceProvenance TerrainSource)
{
    // Complete successful CTerrainData array allocation: virtual slot3 is
    // source MOV EAX,1;RET. Null/unknown runtime tables use a separate API.
    public bool NativeTerrainValid => true;
    public bool UsesArmyByte1C => (Terrain.RawField2C & 2) != 0;
}

public sealed record ArmyFormationSelection(byte RawSelectedByte, int DefinitionSlot,
    string NativeField, S14FormationDefinition Definition, SourceProvenance FormationSource);

/// <summary>CE 22BB50 / 22B4B0 / 22B560 queries over explicitly complete
/// source map and fixed tables. This does not produce Army data from Core
/// state, choose a naval formation, infer special context, or support an expanded
/// map by truncating it into the original fixed-size pointer table.</summary>
public static class ArmyNativeFormationQueries
{
    public static ArmyTerrainSelection Terrain(LoadedMap map,LoadedTerrainTypes terrainTypes,
        ushort armyHexWord2A)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(terrainTypes);
        if (map.Map is null || map.Map.Width!=220 || map.Map.Height!=220 || map.Map.Cells is null || map.Map.Cells.Length!=48400)
            throw new InvalidDataException("Original Army terrain getter requires the complete original 48400-slot map profile.");
        if (terrainTypes.Catalog is null || terrainTypes.Catalog.Types is null || terrainTypes.Catalog.Types.Count!=31)
            throw new InvalidDataException("Original Army terrain getter requires all 31 source terrain definitions.");
        for (int i = 0; i < terrainTypes.Catalog.Types.Count; i++)
        {
            var row=terrainTypes.Catalog.Types[i];
            if (row is null || row.Slot!=i || row.RawBytes is null || row.RawBytes.Length!=51 || row.RawBytes[28]!=row.RawField2C)
                throw new InvalidDataException("Original Army terrain getter has a missing/mismatched source terrain row.");
        }
        int resolvedHex=armyHexWord2A<=48399 ? armyHexWord2A : 0;
        MapCell cell=map.Map.Cells[resolvedHex];
        if (cell.Id!=resolvedHex) throw new InvalidDataException("Original Army terrain source cell ID differs from its physical slot.");
        int terrainSlot=cell.SurfaceCode<=30 ? cell.SurfaceCode : 0;
        return new(armyHexWord2A,resolvedHex,cell.SurfaceCode,terrainTypes.Catalog.Types[terrainSlot],map.Source,terrainTypes.Source);
    }

    public static ArmyFormationSelection Formation(ArmyTerrainSelection terrain,
        LoadedSkillDefinitions definitions,byte? armyByte1B,byte? armyByte1C)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Formations?.Formations is not {Count:21} rows ||
            rows.Where((row,index)=>row is null || row.Slot!=index).Any())
            throw new InvalidDataException("Original Army formation getter requires all 21 source formation definitions.");
        byte selected=(terrain.UsesArmyByte1C ? armyByte1C : armyByte1B) ??
            throw new InvalidDataException("Original Army selected byte1B/1C is unknown; a Core formation is not substituted.");
        int slot=selected<=20 ? selected : 0;
        return new(selected,slot,terrain.UsesArmyByte1C ? "Army.byte1C" : "Army.byte1B",rows[slot],definitions.Source);
    }

    public static int ModelMode(ArmyTerrainSelection terrain,bool? contextPredicate16EEC0,byte? armyByte1B)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        return ArmyModelSelection.ChooseMode(new(terrain.NativeTerrainValid,
            terrain.Terrain.RawField2C,contextPredicate16EEC0,armyByte1B));
    }
}

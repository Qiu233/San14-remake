using System.Numerics;
using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

public sealed record OriginalTerritoryOwnershipPacket(byte[] ZoneLayers, Vector4[] CellColors,
    Vector4[] LineColors, IReadOnlyDictionary<int,int> ForcePaletteSlots,
    IReadOnlyDictionary<int,byte> AllocatedColorIndices);

/// <summary>Project the current world's ownership into source layer0. Other
/// source channels, reserved colors and source land eligibility are explicit.
/// This does not use scenario ownership to repair current territory.</summary>
public static class OriginalTerritoryOwnership
{
    public static OriginalTerritoryOwnershipPacket Apply(GameState world, string mapSha256,
        ReadOnlySpan<byte> completeSourceLayers, ReadOnlySpan<bool> sourceTintEligible,
        IReadOnlyDictionary<int,byte> sourceForceColorIndices, OriginalCeForceMapPalette palette,
        ReadOnlySpan<Vector4> reservedCellColors, ReadOnlySpan<Vector4> reservedLineColors,
        IReadOnlyDictionary<int,byte>? sourceAllocatedColorIndices=null)
    {
        ArgumentNullException.ThrowIfNull(world);ArgumentNullException.ThrowIfNull(sourceForceColorIndices);ArgumentNullException.ThrowIfNull(palette);
        int count=OriginalTerritoryConstants.CellsPerLayer;
        if(world.MapWidth!=220||world.MapHeight!=220||world.MapSha256!=mapSha256||world.Territory.Count!=count||
            completeSourceLayers.Length!=count*7||sourceTintEligible.Length!=count||reservedCellColors.Length!=128||reservedLineColors.Length!=128)
            throw new InvalidDataException("Current territory and the complete source map packet differ.");
        byte[] layers=completeSourceLayers.ToArray();var cells=reservedCellColors.ToArray();var lines=reservedLineColors.ToArray();
        var slots=new Dictionary<int,int>();var colorSlots=new Dictionary<byte,int>();
        var occupied=new HashSet<int>();
        if(sourceAllocatedColorIndices is not null)
            foreach(var prior in sourceAllocatedColorIndices)
            {
                if(prior.Key is <25 or >=128||prior.Value>59||!occupied.Add(prior.Key)||!colorSlots.TryAdd(prior.Value,prior.Key))
                    throw new InvalidDataException("Territory prior source color allocator requires unique regular colors in slots25..127.");
            }
        for(int i=count;i<layers.Length;i++)
            if(layers[i] is >=25 and <128&&!occupied.Contains(layers[i]))
                throw new InvalidDataException("A preserved territory channel refers to an allocated color whose source color index is unknown.");
        int next=25;
        for(int id=0;id<count;id++)
        {
            if(!world.Territory.TryGetValue(id,out var cell)||cell.Id!=id)
                throw new InvalidDataException($"Current territory source cell {id} is missing.");
            layers[id]=0;
            if(!sourceTintEligible[id]||cell.ForceId is null)continue;
            int force=cell.ForceId.Value;
            if(!world.Forces.ContainsKey(force)||!sourceForceColorIndices.TryGetValue(force,out byte color))
                throw new InvalidDataException($"Current force {force} has no source color index; no territory fallback is supplied.");
            if(color==60){slots[force]=0;continue;}
            if(color>59)throw new InvalidDataException($"Current force {force} uses a special unresolved source color index {color}.");
            if(!colorSlots.TryGetValue(color,out int slot))
            {
                while(next<128&&occupied.Contains(next))next++;
                if(next>=128)throw new InvalidDataException("Territory source palette allocator exhausted slots25..127.");
                slot=next++;colorSlots.Add(color,slot);var c=palette.MapColor(color);
                occupied.Add(slot);
                var vector=new Vector4(c.RedFloat,c.GreenFloat,c.BlueFloat,c.AlphaFloat);cells[slot]=vector;lines[slot]=vector;
            }
            slots[force]=slot;layers[id]=(byte)slot;
        }
        return new(layers,cells,lines,slots,colorSlots.ToDictionary(value=>value.Value,value=>value.Key));
    }

    /// <summary>Source CE18DD89 land eligibility: successful Hex terrain getter's
    /// native ushort2A must be positive. Raw map terrain codes above30 resolve
    /// to terrain row0 through CE231C70. No terrain-name classification occurs.</summary>
    public static bool[] ReadSourceTintEligibility(LoadedMap map,LoadedTerrainTypes terrain)
    {
        ArgumentNullException.ThrowIfNull(map);ArgumentNullException.ThrowIfNull(terrain);
        if(map.Map.Width!=220||map.Map.Height!=220||map.Map.Cells.Length!=48400||terrain.Catalog.Types.Count!=31)
            throw new InvalidDataException("Territory eligibility needs the complete original map and 31 terrain rows.");
        for(int i=0;i<31;i++)
        {
            var row=terrain.Catalog.Types[i];
            if(row.Slot!=i||row.RawBytes.Length!=51||BinaryPrimitives.ReadUInt16LittleEndian(row.RawBytes.AsSpan(26,2))!=row.RawField2A)
                throw new InvalidDataException("Territory terrain row differs from its raw source2A field.");
        }
        bool[] eligible=new bool[48400];
        for(int id=0;id<eligible.Length;id++)
        {
            var cell=map.Map.Cells[id];
            if(cell.Id!=id)throw new InvalidDataException("Territory map physical source slot differs.");
            eligible[id]=terrain.Catalog.Types[cell.SurfaceCode<=30?cell.SurfaceCode:0].RawField2A>0;
        }
        return eligible;
    }
}

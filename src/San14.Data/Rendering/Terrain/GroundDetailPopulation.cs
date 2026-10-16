namespace San14.Data;

/// <summary>Fully settled seasonal populations for one imported landscape and
/// current terrain Field. Camera admission and device streams consume these
/// banks independently; this preparation does not emulate an update budget.</summary>
public sealed class GroundDetailPopulation
{
    private GroundDetailPopulation(ProceduralPlacementDeclaration declaration,
        MapStaticModelGroundHeightSource currentField, ProceduralBlock[] springSummer,
        ProceduralBlock[] autumnWinter, int[] streamCapacities)
        => (Declaration, CurrentField, SpringSummer, AutumnWinter, StreamCapacities) =
            (declaration, currentField, Array.AsReadOnly(springSummer),
                Array.AsReadOnly(autumnWinter), Array.AsReadOnly(streamCapacities));

    public ProceduralPlacementDeclaration Declaration { get; }
    public MapStaticModelGroundHeightSource CurrentField { get; }
    public IReadOnlyList<ProceduralBlock> SpringSummer { get; }
    public IReadOnlyList<ProceduralBlock> AutumnWinter { get; }
    public IReadOnlyList<int> StreamCapacities { get; }

    public static GroundDetailPopulation Prepare(ProceduralPlacementDeclaration declaration,
        MapStaticModelGroundHeightSource currentField,
        IReadOnlyList<ProceduralTemplateBounds> actualTemplateBounds)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(currentField);
        ArgumentNullException.ThrowIfNull(actualTemplateBounds);
        var callbacks = MapProceduralTerrainCallbacksSource.BindCeTerrainType2(declaration, currentField);
        ProceduralBlock[] springSummer = PrepareGroup(true), autumnWinter = PrepareGroup(false);
        int[] capacities = new int[declaration.Templates.Count];
        IncludeCapacity(springSummer);
        IncludeCapacity(autumnWinter);
        return new(declaration, currentField, springSummer, autumnWinter, capacities);

        ProceduralBlock[] PrepareGroup(bool warmSeason)
        {
            // The landscape's seasonal writer exchanges these biome families.
            // Start each group from the imported map so neither preparation
            // mutates the declaration or depends on a preceding group request.
            ReadOnlySpan<byte> springSummerBiomes = [1, 3, 2, 0, 4, 6];
            ReadOnlySpan<byte> autumnWinterBiomes = [11, 12, 13, 8, 9, 10];
            ReadOnlySpan<byte> from = warmSeason ? autumnWinterBiomes : springSummerBiomes;
            ReadOnlySpan<byte> to = warmSeason ? springSummerBiomes : autumnWinterBiomes;
            byte[] map = declaration.ResourceMapBytes.ToArray();
            for (int cell = 0; cell < map.Length; cell++)
                for (int pair = 0; pair < from.Length; pair++)
                    if (map[cell] == from[pair])
                    {
                        if (to[pair] >= declaration.Biomes.Count)
                            throw new NotSupportedException("The seasonal biome is absent from this population directory.");
                        map[cell] = to[pair];
                        break;
                    }
            var seasonal = declaration with { ResourceMapBytes = map };
            var producer = new MapProceduralPlacementProducer(seasonal, actualTemplateBounds, callbacks.QueryHeight);
            var blocks = new List<ProceduralBlock>();
            for (int z = 0; z < declaration.BlocksZ; z++)
                for (int x = 0; x < declaration.BlocksX; x++)
                {
                    // Each block has independently seeded streams. An entirely
                    // unassigned block has no candidates in any biome, so it
                    // needs neither generation nor an empty published bank.
                    if (!HasAssignedCell(x, z)) continue;
                    ProceduralBlock block = producer.GenerateBlock(x, z);
                    if (block.InstanceCount != 0) blocks.Add(block);
                }
            return blocks.ToArray();

            bool HasAssignedCell(int blockX, int blockZ)
            {
                uint firstX = (uint)blockX * declaration.CellsPerBlockX;
                uint firstZ = (uint)blockZ * declaration.CellsPerBlockZ;
                for (uint z = firstZ; z < firstZ + declaration.CellsPerBlockZ; z++)
                    for (uint x = firstX; x < firstX + declaration.CellsPerBlockX; x++)
                        if (map[checked((int)(z * declaration.CellsX + x))] != byte.MaxValue) return true;
                return false;
            }
        }

        void IncludeCapacity(IReadOnlyList<ProceduralBlock> blocks)
        {
            int[] totals = new int[capacities.Length];
            foreach (ProceduralBlock block in blocks)
                foreach (ProceduralElementBank bank in block.Banks)
                    totals[bank.TemplateIndex] = checked(totals[bank.TemplateIndex] + bank.Instances.Count);
            for (int template = 0; template < totals.Length; template++)
                capacities[template] = Math.Max(capacities[template], totals[template]);
        }
    }
}

using System.Globalization;
using System.Text;

namespace San14.Data;

/// <summary>Local Native import encoding. Model media is written once by its
/// template owner, independently of population metadata and affine banks.</summary>
public static class GroundDetailInputEncoding
{
    public const string NativeDirectory = "ground-detail-input";
    public const string SeasonDirectory = "ground-detail-season-source-input";

    public static void WriteNativeInputs(string inputRoot, GroundDetailResources resources, uint currentAppFlags)
    {
        string directory = Path.Combine(inputRoot, NativeDirectory);
        Directory.CreateDirectory(directory);
        resources.Templates.WriteNativeInputs(Path.Combine(directory, "templates"));
        using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "population.bin"))))
            WritePopulation(writer, resources);
        File.WriteAllText(Path.Combine(directory, "common-source.txt"),
            FormattableString.Invariant($"{resources.PlacementFlags} {resources.FrustumKind} {resources.View.CullingType450} {resources.View.ScanningType454} {currentAppFlags & ushort.MaxValue:X}\n"));
        WriteWind(Path.Combine(directory, "wind"), resources.Wind);
        string seasons = Path.Combine(inputRoot, SeasonDirectory);
        WriteSeason(Path.Combine(seasons, "spring-summer"), resources.Population.SpringSummer);
        WriteSeason(Path.Combine(seasons, "autumn-winter"), resources.Population.AutumnWinter);
        File.WriteAllLines(Path.Combine(seasons, "stream-capacities.txt"),
            resources.Population.StreamCapacities.Select((count, index) =>
                index.ToString(CultureInfo.InvariantCulture) + " " + count.ToString(CultureInfo.InvariantCulture)));
    }

    private static void WritePopulation(BinaryWriter writer, GroundDetailResources resources)
    {
        writer.Write(Encoding.ASCII.GetBytes("GDPK0002"));
        var d = resources.Population.Declaration;
        foreach (uint word in new[] { d.FieldSizeX, d.FieldSizeZ, d.FieldOffsetX, d.FieldOffsetZ,
            d.CellsX, d.CellsZ, d.BlocksX, d.BlocksZ, d.UpdateDistance, d.FreeDistance, d.ViewDistance,
            d.AbsolutelyRenderDistanceH, d.AbsolutelyRenderDistanceV, d.TerrainType, d.GlobalFlags }) writer.Write(word);
        writer.Write((uint)resources.Templates.Metadata.Count);
        writer.Write((uint)d.Elements.Count);
        writer.Write((uint)CountBanks(resources.Population.SpringSummer));
        foreach (var template in resources.Templates.Metadata)
        {
            writer.Write((uint)template.Index); writer.Write(template.NameHash);
            var lo = template.Bounds.Minimum; var hi = template.Bounds.Maximum;
            foreach (float value in new[] { lo.X, lo.Y, lo.Z, hi.X, hi.Y, hi.Z }) writer.Write(value);
            foreach (var lod in template.LodDraws)
            {
                writer.Write((uint)lod.Count);
                foreach (int draw in lod) writer.Write((uint)draw);
            }
            writer.Write((uint)template.ProviderClasses.Count);
            foreach (uint provider in template.ProviderClasses) writer.Write(provider);
        }
        foreach (var element in d.Elements)
        {
            writer.Write(element.Flags);
            foreach (float distance in element.LodDistances) writer.Write(distance);
        }
        WriteBanks(writer, resources.Population.SpringSummer);
    }

    private static int CountBanks(IReadOnlyList<ProceduralBlock> blocks)
        => blocks.Sum(block => block.Banks.Count(bank => bank.Instances.Count != 0));

    private static void WriteSeason(string directory, IReadOnlyList<ProceduralBlock> blocks)
    {
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "banks.bin")));
        writer.Write((uint)CountBanks(blocks));
        WriteBanks(writer, blocks);
    }

    private static void WriteBanks(BinaryWriter writer, IReadOnlyList<ProceduralBlock> blocks)
    {
        byte[] affine = new byte[48];
        foreach (var block in blocks)
            foreach (var bank in block.Banks)
            {
                if (bank.Instances.Count == 0) continue;
                writer.Write((uint)block.X); writer.Write((uint)block.Z);
                writer.Write((uint)bank.ElementIndex); writer.Write((uint)bank.TemplateIndex);
                writer.Write((uint)bank.Instances.Count);
                foreach (var instance in bank.Instances)
                {
                    instance.Affine.Write(affine);
                    writer.Write(affine);
                }
            }
    }

    private static void WriteWind(string directory, GroundDetailWindResources wind)
    {
        Directory.CreateDirectory(Path.Combine(directory, "shader"));
        File.WriteAllBytes(Path.Combine(directory, "interaction-vector-r8uint.bin"), wind.Initial.InteractionVectorSides[0].InitialBytes);
        File.WriteAllBytes(Path.Combine(directory, "interaction-height-r16float.bin"), wind.Initial.InteractionHeightSides[0].InitialBytes);
        File.WriteAllBytes(Path.Combine(directory, "wind-phase-r32float.bin"), wind.Initial.WindPhaseSides[0].InitialBytes);
        File.WriteAllBytes(Path.Combine(directory, "wind-and-ripple-rgba16float.bin"), wind.Initial.WindTexture.InitialBytes);
        File.WriteAllBytes(Path.Combine(directory, "shader", "2801580A-program-0.dxbc"), wind.ComputeProgram.ToArray());
        File.WriteAllBytes(Path.Combine(directory, "wind-s8-s9-sampler-d3d11.bin"), wind.SamplerDescriptor.ToArray());
    }
}

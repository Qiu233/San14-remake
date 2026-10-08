using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace San14.Data;

/// <summary>The three2FCDA0/2ADD90 radii and2A7CD0 move tables. Loaded once
/// from the paired CE SC/TC executable; no research export is a runtime input.</summary>
public sealed class OriginalArmyMapGroupingProfile
{
    private readonly int[] _radii, _moves, _order;
    private OriginalArmyMapGroupingProfile(int[] radii, int[] moves, int[] order)
        => (_radii, _moves, _order) = (radii, moves, order);

    public static OriginalArmyMapGroupingProfile Load(string sourceDirectory, string language)
    {
        (int[] radii, int moves, int order) = language switch
        {
            "TC" => (new[] { 0x1A49C74, 0x1A49C80, 0x1A49C8C }, 0x13ADD30, 0x13ADD90),
            "SC" => (new[] { 0x1A4ACA4, 0x1A4ACB0, 0x1A4ACBC }, 0x13AED30, 0x13AED90),
            _ => throw new InvalidDataException("Army grouping requires CE SC/TC source tables.")
        };
        OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(sourceDirectory, language);
        using var stream = File.OpenRead(Path.Combine(sourceDirectory, $"SAN14CE_{language}.exe"));
        using var pe = new PEReader(stream);
        return new(radii.Select(rva => Read(rva, 1)[0]).ToArray(), Read(moves, 24), Read(order, 6));

        int[] Read(int rva, int count)
        {
            var bytes = pe.GetSectionData(rva).GetContent(0, count * 4);
            var result = new int[count];
            for (int i = 0; i < count; i++) result[i] = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i * 4, 4));
            return result;
        }
    }

    public int Radius(int index) => _radii[index];
    public (int X, int Y) Direction(int parity, int direction)
    {
        int at = parity * 12 + (direction - 1) * 2;
        return (_moves[at], _moves[at + 1]);
    }
    public IReadOnlyList<int> RingSideOrder => Array.AsReadOnly(_order);

    public IReadOnlyList<int> EnumerateNeighbors(int centerPhysical, int index)
    {
        var result = new List<int>();
        int centerX = centerPhysical % 220, centerY = centerPhysical / 220;
        for (int ring = 1; ring <= _radii[index]; ring++)
        {
            int x = centerX, y = centerY;
            for (int step = 0; step < ring; step++) Move(4);
            foreach (int direction in _order)
                for (int step = 0; step < ring; step++)
                {
                    Move(direction); //The original appends after moving, even the closing step.
                    if ((uint)x < 220 && (uint)y < 220) result.Add(y * 220 + x);
                }
            void Move(int direction)
            {
                int parity = (x % 2 + 2) % 2;
                (int dx, int dy) = Direction(parity, direction);
                x += dx; y += dy;
            }
        }
        return result.AsReadOnly();
    }
}

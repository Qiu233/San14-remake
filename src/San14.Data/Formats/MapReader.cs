using System.Buffers.Binary;

namespace San14.Data.Formats;

// Undecoded portions remain available as raw fields.
public readonly record struct MapCell(int Id, uint Field1, uint Field2, uint Field3)
{
    public byte SurfaceCode => (byte)Field1;
    public ushort AreaSlot => (ushort)(Field1 >> 16);
}

public sealed record MapDocument(int Width, int Height, MapCell[] Cells);

public static class MapReader
{
    public static MapDocument Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4) throw new InvalidDataException("Map header is truncated.");
        int count = BinaryPrimitives.ReadInt32LittleEndian(data);
        if (count < 1 || data.Length != 4L + 16L * count)
            throw new InvalidDataException("Map record count or length is invalid.");
        int side = (int)Math.Sqrt(count);
        if (side * side != count)
            throw new InvalidDataException("This map is not a square tile array.");

        var cells = new MapCell[count];
        for (int i = 0; i < count; i++)
        {
            var row = data.Slice(4 + i * 16, 16);
            int id = BinaryPrimitives.ReadInt32LittleEndian(row);
            if (id != i) throw new InvalidDataException($"Map cell ID mismatch at index {i}.");
            cells[i] = new MapCell(id,
                BinaryPrimitives.ReadUInt32LittleEndian(row[4..8]),
                BinaryPrimitives.ReadUInt32LittleEndian(row[8..12]),
                BinaryPrimitives.ReadUInt32LittleEndian(row[12..16]));
        }
        return new MapDocument(side, side, cells);
    }
}

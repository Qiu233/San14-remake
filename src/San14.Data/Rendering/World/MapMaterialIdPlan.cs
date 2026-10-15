using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The selected shader's static int32 material field layout. Current
/// constant bytes and the initialized table remain inputs to each validation.</summary>
public sealed class MapMaterialIdLayout
{
    internal MapMaterialIdLayout(G1ShaderConfigurationRecord gbuffer, uint register, int bufferBytes, int idOffset)
        => (Gbuffer, Register, BufferBytes, IdOffset) = (gbuffer, register, bufferBytes, idOffset);
    public G1ShaderConfigurationRecord Gbuffer { get; }
    public uint Register { get; }
    public int BufferBytes { get; }
    public int IdOffset { get; }

    public int ReadCurrent(IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> constants)
    {
        if (!constants.TryGetValue(Register, out var bytes) || bytes.Length != BufferBytes)
            throw new InvalidDataException("Material cache requires the actual complete selected PS constant buffer.");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.Span.Slice(IdOffset, 4));
    }
}

/// <summary>One actual model draw and shader combination's named material source.</summary>
public sealed class MapModelMaterialIdPlan
{
    internal MapModelMaterialIdPlan(MapMaterialIdSource source, MapMaterialIdLayout layout)
        => (Source, Layout) = (source, layout);
    public MapMaterialIdSource Source { get; }
    public MapMaterialIdLayout Layout { get; }
}

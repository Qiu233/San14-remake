using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

/// <summary>Explicit new current CFormationData allocation and CE static-load
/// projection. Reads subsequently use saved owned fields, never the source row.
/// Semantic offsets are SC labels; the paired TC serializer has the same layout.</summary>
public static class OriginalTacticRemakeCurrentFormations
{
    public const string ProfileId = "san14:ce-owned-formation-parameters-20261002";
    private const string Prefix = "ce-formation-current:";
    private static readonly (int Source, int Native, int Rotation)[] EncodedFields =
        [(111, 0x7F, 5), (112, 0x80, 7), (113, 0x81, 7), (115, 0x83, 1),
         (116, 0x84, 3), (117, 0x85, 5), (118, 0x86, 7), (149, 0xA6, 1)];

    public static string FieldKey(int physical, int nativeOffset) => $"{Prefix}{physical}:raw:{nativeOffset:X2}";
    public static string VectorKey(int physical, int ordinal) => $"{Prefix}{physical}:vector-a8:{ordinal}";
    public static string VectorSizeKey(int physical) => $"{Prefix}{physical}:vector-byte-count-a8";
    private static string ProfileKey(int physical) => $"{Prefix}{physical}:profile-version";

    public static GameState CreateFreshOwnedDefinitions(GameState state, LoadedSkillDefinitions loaded)
    {
        if (state.NativeArmySimulation is not { } simulation || state.Units.Count != 0 ||
            simulation.Parameters.Language is not ("SC" or "TC") ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.ContentHashes.GetValueOrDefault(loaded.Source.ResourceUri) != loaded.Source.Sha256 ||
            loaded.Formations is not { FormatMarker: 0x66, FormationRecordOffset: 185369,
                FormationRecordStride: 152, Formations.Count: 21 } ||
            simulation.Parameters.Int32Values.Keys.Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)))
            throw new InvalidDataException("Fresh owned formation parameters cannot replace a saved or incomplete owner.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var row in loaded.Formations.Formations)
        {
            if (row.Slot is < 0 or > 20 || row.RawBytes is not { Length: 152 })
                throw new InvalidDataException("Current CE formation row identity or size differs.");
            int physical = row.Slot;
            values[ProfileKey(physical)] = 1;
            values[FieldKey(physical, 0x7E)] = row.RawBytes[110];
            values[FieldKey(physical, 0x82)] = row.RawBytes[114];
            for (int index = 0; index < EncodedFields.Length; index++)
            {
                var field = EncodedFields[index];
                byte raw = row.RawBytes[field.Source];
                values[FieldKey(physical, field.Native)] = raw;
                values[VectorKey(physical, index)] = Encode(raw, field.Rotation, physical);
            }
            //The reset leaves a logically empty vector. Successful mode1 CE
            //load appends these eight i32 entries in this exact source order.
            values[VectorSizeKey(physical)] = 32;
            for (int index = 0; index < 10; index++)
                values[FieldKey(physical, 0x88 + index * 2)] =
                    BinaryPrimitives.ReadUInt16LittleEndian(row.RawBytes.AsSpan(119 + index * 2, 2));
            for (int index = 0; index < 5; index++)
                values[FieldKey(physical, 0x9C + index * 2)] =
                    BinaryPrimitives.ReadUInt16LittleEndian(row.RawBytes.AsSpan(139 + index * 2, 2));
            values[FieldKey(physical, 0xA7)] = row.RawBytes[150];
            values[FieldKey(physical, 0xC0)] = row.RawBytes[151];
        }
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public static int? ReadVectorByteCount(GameState state, int physical) =>
        Value(state, physical, VectorSizeKey(physical)) is int value && value >= 0 ? value : null;

    public static byte? ReadRawByte(GameState state, int physical, int nativeOffset) =>
        Value(state, physical, FieldKey(physical, nativeOffset)) is int value && value is >= 0 and <= 255
            ? (byte)value : null;

    public static ushort? ReadRawWord(GameState state, int physical, int nativeOffset) =>
        Value(state, physical, FieldKey(physical, nativeOffset)) is int value && value is >= 0 and <= 65535
            ? (ushort)value : null;

    /// <summary>Actual A8-size predicate and fresh slot9 decode. The raw fallback
    /// is reached only when the current vector byte count differs from32.</summary>
    public static byte? ReadParameter(GameState state, int physical, int nativeOffset)
    {
        int ordinal = Array.FindIndex(EncodedFields, field => field.Native == nativeOffset);
        if (ordinal < 0) return ReadRawByte(state, physical, nativeOffset);
        int? bytes = ReadVectorByteCount(state, physical);
        if (bytes is null) return null;
        if ((bytes.Value & ~3) != 32) return ReadRawByte(state, physical, nativeOffset);
        if (Value(state, physical, VectorKey(physical, ordinal)) is not int encoded) return null;
        byte value = unchecked((byte)encoded);
        int rotation = EncodedFields[ordinal].Rotation;
        return unchecked((byte)(((value << rotation) | (value >> (8 - rotation))) ^ physical));
    }

    private static int? Value(GameState state, int physical, string key) =>
        physical is >= 0 and <= 20 && state.NativeArmySimulation?.Parameters.Int32Values is { } values &&
        values.GetValueOrDefault(ProfileKey(physical)) == 1 && values.TryGetValue(key, out int value) ? value : null;

    private static byte Encode(byte value, int rotation, int physical)
    {
        int mixed = value ^ physical;
        return unchecked((byte)((mixed >> rotation) | (mixed << (8 - rotation))));
    }
}

using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Separate source-created CPU fields. ShaderState730 does not
/// choose the primitive bank stored in Packed728's RenderType byte.</summary>
public readonly record struct WorldGeometryNode58PackedParameters(
    ulong Packed728, uint ShaderState730);

/// <summary>E80190 graph loader to E6C3A0 constructor's actual bit packing.
/// This pure loaded-source projection does no EXE/graph IO or source hashing.
/// Native source acceptance remains with the enclosing load-only owner.</summary>
public static class WorldGeometryNode58PackedParameterSource
{
    private static readonly int[] flagBits = [31,4,7,5,6,19,20,21,32,33,34,35,36,37,38,39];
    private static readonly int[] flag2Bits = [40,41,43,44];

    public static WorldGeometryNode58PackedParameters Read(ModelQueueStateSource queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        // The existing named queue.Arguments contains only seven fields;
        // constructor728 also consumes the complete source Call arguments.
        return FromLoadedGraphArguments(queue.Node.Call.Arguments);
    }

    public static WorldGeometryNode58PackedParameters FromLoadedGraphArguments(
        IReadOnlyList<TaskGraphArgument> actualArguments)
    {
        ArgumentNullException.ThrowIfNull(actualArguments);
        ushort flag = BinaryPrimitives.ReadUInt16LittleEndian(Field(0x05D92ACC,0x03000001,2));
        uint flag2 = Word(0x5B2B79DA);
        byte renderType = Field(0x092BC250,0x00000001,1)[0];
        byte viewId = Field(0x9075EDA0,0x01000001,1)[0];
        uint propertyF0 = Word(0x7B9E9E85);
        uint shaderState = Field(0xA98962A5,0x01000001,1)[0];
        ulong packed = ((ulong)(propertyF0 & 7) << 16) |
            ((ulong)(viewId & 0x7F) << 24) | ((ulong)renderType << 8);
        for(int i=0;i<flagBits.Length;i++)
            if((flag & (1u << i)) != 0) packed |= 1UL << flagBits[i];
        for(int i=0;i<flag2Bits.Length;i++)
            if((flag2 & (1u << i)) != 0) packed |= 1UL << flag2Bits[i];
        if(Field(0xAC626C0D,0x01000001,1)[0] != 0) packed |= 1UL << 22;
        if(Field(0xB287F508,0x01000001,1)[0] != 0) packed |= 1UL << 23;
        // E80AD9..E80AF3 requires all three actual resource keys. This
        // constructor bit42 is not Flag2 bit4 or a shader selection result.
        if(Word(0x19804FF9) != 0 && Word(0x86BD2020) != 0 && Word(0x8D23EA0E) != 0)
            packed |= 1UL << 42;
        return new(packed,shaderState);

        uint Word(uint hash) => BinaryPrimitives.ReadUInt32LittleEndian(Field(hash,0x05000001,4));
        ReadOnlySpan<byte> Field(uint hash, uint descriptor, int bytes)
        {
            var field = actualArguments.Single(item=>item.NameHash==hash);
            if(field.DescriptorWord!=descriptor || field.RawValue.Length!=bytes)
                throw new InvalidDataException($"Node58 constructor field {hash:X8} has another source type or width.");
            return field.RawValue;
        }
    }
}

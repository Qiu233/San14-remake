using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record DokProperty(int Index, int DescriptorOffset, int ValueOffset,
    uint TypeCode, int ElementCount, uint NameHash, int ElementSize,
    byte[] RawDescriptor, byte[] RawValue);

public enum DokObjectKind { Inline, Reference }

public sealed record DokReference(uint ResourceId, uint ObjectId);

public sealed record DokObject(int Index, int Offset, int Length, uint ObjectId,
    uint? ClassId, IReadOnlyList<DokProperty> Properties, byte[] RawHeader, byte[] Padding)
{
    public DokObjectKind Kind { get; init; } = DokObjectKind.Inline;
    public DokReference? Reference { get; init; }
}

public sealed record DokDocument(uint FormatCode, uint Identifier,
    IReadOnlyList<DokObject> Objects, byte[] RawHeader);

/// <summary>
/// Reads verified _DOK0000 / IDOK0000 / RDOK0000 property containers. Property names, class
/// meanings and the role of individual integer values remain uninterpreted.
/// Reference targets and their property inheritance are not automatically resolved.
/// Unverified property widths and object signatures are rejected explicitly.
/// </summary>
public static class DokReader
{
    public static DokDocument Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 28 || !data[..8].SequenceEqual("_DOK0000"u8) ||
            U32(data,8) != 28 || U32(data,12) != 10 || U32(data,24) != data.Length)
            throw new InvalidDataException("Unsupported DOK header or invalid total size.");
        uint rawCount = U32(data,16);
        if (rawCount > (data.Length-28)/24)
            throw new InvalidDataException("DOK object count exceeds the file boundary.");
        int count=(int)rawCount, offset=28;
        var objects=new List<DokObject>(count);
        for (int index=0;index<count;index++)
        {
            if (offset > data.Length-24)
                throw new InvalidDataException($"DOK object {index} is truncated.");
            ReadOnlySpan<byte> signature=data.Slice(offset,8);
            DokObjectKind kind=signature.SequenceEqual("IDOK0000"u8) ? DokObjectKind.Inline :
                signature.SequenceEqual("RDOK0000"u8) ? DokObjectKind.Reference :
                throw new InvalidDataException($"Unsupported DOK object signature at {offset}.");
            int headerSize=kind==DokObjectKind.Inline ? 24 : 28;
            uint rawSize=U32(data,offset+8);
            if (rawSize < headerSize || rawSize > data.Length-offset)
                throw new InvalidDataException($"DOK object {index} has invalid boundaries.");
            uint rawProperties=U32(data,offset+headerSize-4);
            if (rawProperties > (rawSize-headerSize)/12)
                throw new InvalidDataException($"DOK object {index} has invalid property boundaries.");
            int size=(int)rawSize, propertyCount=(int)rawProperties;
            int values=offset+headerSize+12*propertyCount, end=offset+size;
            var properties=new List<DokProperty>(propertyCount);
            for (int propertyIndex=0;propertyIndex<propertyCount;propertyIndex++)
            {
                int descriptor=offset+headerSize+12*propertyIndex;
                uint type=U32(data,descriptor), rawElements=U32(data,descriptor+4);
                int width=type switch
                {
                    0 or 1 => 1,
                    2 or 3 => 2,
                    4 or 5 or 8 => 4,
                    10 => 16,
                    12 => 8,
                    _ => throw new InvalidDataException($"Unsupported DOK property type {type} at {descriptor}.")
                };
                if (rawElements > (end-values)/width)
                    throw new InvalidDataException($"DOK property {propertyIndex} exceeds object {index}.");
                int elements=(int)rawElements, length=elements*width;
                properties.Add(new(propertyIndex,descriptor,values,type,elements,U32(data,descriptor+8),width,
                    data.Slice(descriptor,12).ToArray(),data.Slice(values,length).ToArray()));
                values+=length;
            }
            if (values!=end)
                throw new InvalidDataException($"DOK object {index} has unparsed property bytes.");
            int padding=(4-(end&3))&3;
            if (padding>data.Length-end || data.Slice(end,padding).IndexOfAnyExcept((byte)0)>=0)
                throw new InvalidDataException($"DOK object {index} has invalid alignment padding.");
            objects.Add(new(index,offset,size,U32(data,offset+12),
                kind==DokObjectKind.Inline ? U32(data,offset+16) : null,properties,
                data.Slice(offset,headerSize).ToArray(),data.Slice(end,padding).ToArray())
            {
                Kind=kind,
                Reference=kind==DokObjectKind.Reference ? new(U32(data,offset+16),U32(data,offset+20)) : null
            });
            offset=end+padding;
        }
        if (offset!=data.Length)
            throw new InvalidDataException("DOK object chain does not cover the complete file.");
        return new(10,U32(data,20),objects.AsReadOnly(),data[..28].ToArray());
    }

    private static uint U32(ReadOnlySpan<byte> data,int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset,4));
}

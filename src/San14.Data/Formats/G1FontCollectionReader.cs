using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record G1FontGlyphRecord(int Index, int SourceOffset, byte Width, byte Height,
    sbyte RawSignedByte2, sbyte RawSignedByte3, byte RawByte4, sbyte RawSignedByte5,
    byte RawByte6, byte RawByte7, uint RawPixelReference, byte[] RawBytes)
{
    public int PixelEncoding => checked((int)(RawPixelReference >> 28));
    public int PixelPayloadOffset => checked((int)(RawPixelReference & 0x0FFFFFFF));
    public sbyte HorizontalOffsetX=>RawSignedByte2;
    public sbyte HorizontalOffsetY=>RawSignedByte3;
    public sbyte HorizontalAdvance=>unchecked((sbyte)RawByte4);
    public sbyte VerticalOffsetX=>RawSignedByte5;
    public sbyte VerticalOffsetY=>unchecked((sbyte)RawByte6);
    public sbyte VerticalAdvance=>unchecked((sbyte)RawByte7);
}

public sealed record G1FontFace(int Index, int SourceOffset, int EndOffset,
    IReadOnlyList<ushort> UnicodeGlyphMap, IReadOnlyList<G1FontGlyphRecord> Glyphs);

public sealed record G1FontPalette(int Index, int SourceOffset,
    IReadOnlyList<uint> PackedRgbaColors, byte[] RawBytes);

public sealed record G1FontCollection(int DeclaredLength, uint RawWord0C, uint RawWord10,
    int PixelPayloadOffset, IReadOnlyList<G1FontFace> Faces,
    IReadOnlyList<G1FontPalette> Palettes, byte[] RawHeader);

public sealed record G1FontGlyphPixels(ushort Utf16CodeUnit, int FaceIndex, int PaletteIndex,
    G1FontGlyphRecord Record, G1FontPalette Palette, int EncodedSourceOffset,
    byte[] EncodedBytes, RgbaTexture Pixels)
{
    // Packed native rows write the second nibble even for an odd metric width.
    // The source metric remains the glyph quad extent; this is the actual row write extent.
    public int NativeRowPixelCount=>Pixels.Width;
}

/// <summary>Source G1N maps, metrics and pixels. Native 1064EA0 preserves the file;
/// 10649F0 uses the low 28 pixel-reference bits as a byte offset and its high
/// nibble as the encoding. The caller supplies face and palette independently.</summary>
public static class G1FontCollectionReader
{
    public static G1FontCollection Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 32 || !bytes[..8].SequenceEqual("_N1G0000"u8))
            throw Error("header/version is unsupported");
        int length=Int(bytes,8), pixelStart=Int(bytes,0x14);
        int paletteCount=Int(bytes,0x18), faceCount=Int(bytes,0x1C);
        if(length!=bytes.Length || faceCount is <1 or >256 || paletteCount<1)
            throw Error("declared length, face or palette extent is invalid");
        int tableEnd=checked(32+faceCount*4);
        if(tableEnd>bytes.Length||paletteCount>(bytes.Length-tableEnd)/64)throw Error("palette extent is outside the source");
        int paletteEnd=checked(tableEnd+paletteCount*64);
        if(paletteEnd>bytes.Length || pixelStart<paletteEnd || pixelStart>bytes.Length)
            throw Error("palette or pixel payload is outside the source");
        var palettes=new List<G1FontPalette>(paletteCount);
        for(int index=0;index<paletteCount;index++)
        {
            int at=tableEnd+index*64;var colors=new uint[16];
            for(int color=0;color<16;color++)colors[color]=U(bytes,at+color*4);
            palettes.Add(new(index,at,Array.AsReadOnly(colors),bytes.Slice(at,64).ToArray()));
        }
        var faces=new List<G1FontFace>(faceCount);
        for(int index=0;index<faceCount;index++)
        {
            int at=Int(bytes,0x20+index*4),end=index+1<faceCount?Int(bytes,0x24+index*4):pixelStart;
            if(at<paletteEnd || end<at+0x20000L || end>pixelStart || (end-at-0x20000)%12!=0)
                throw Error("face map/metric extent is invalid");
            if(index==0&&at!=paletteEnd || index>0&&at!=faces[^1].EndOffset)
                throw Error("face offsets do not preserve the complete source regions");
            int count=(end-at-0x20000)/12;
            if(count is <1 or >65536)throw Error("glyph metric extent is unsupported");
            var map=new ushort[65536];var glyphs=new G1FontGlyphRecord[count];
            for(int code=0;code<map.Length;code++)
            {
                map[code]=BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(at+code*2,2));
                if(map[code]>=count)throw Error("Unicode map points outside its metric records");
            }
            for(int glyph=0;glyph<count;glyph++)
            {
                int recordAt=at+0x20000+glyph*12;var raw=bytes.Slice(recordAt,12);
                glyphs[glyph]=new(glyph,recordAt,raw[0],raw[1],unchecked((sbyte)raw[2]),
                    unchecked((sbyte)raw[3]),raw[4],unchecked((sbyte)raw[5]),raw[6],raw[7],U(raw,8),raw.ToArray());
                if((long)pixelStart+glyphs[glyph].PixelPayloadOffset>bytes.Length)
                    throw Error("glyph pixel offset is outside the source");
            }
            faces.Add(new(index,at,end,Array.AsReadOnly(map),Array.AsReadOnly(glyphs)));
        }
        return new(length,U(bytes,0xC),U(bytes,0x10),pixelStart,faces.AsReadOnly(),palettes.AsReadOnly(),bytes[..32].ToArray());
    }

    public static G1FontGlyphPixels DecodeGlyph(G1FontCollection collection, ReadOnlySpan<byte> bytes,
        ushort utf16CodeUnit, int explicitFaceIndex, int explicitPaletteIndex)
    {
        ArgumentNullException.ThrowIfNull(collection);
        if(bytes.Length!=collection.DeclaredLength || bytes.Length<32 ||
            !bytes[..32].SequenceEqual(collection.RawHeader))throw Error("source header differs from its parsed collection");
        if((uint)explicitFaceIndex>=collection.Faces.Count || (uint)explicitPaletteIndex>=collection.Palettes.Count)
            throw Error("requires an explicit valid face and palette");
        var face=collection.Faces[explicitFaceIndex];var palette=collection.Palettes[explicitPaletteIndex];
        int glyph=face.UnicodeGlyphMap[utf16CodeUnit];var record=face.Glyphs[glyph];
        if(U(bytes,0x20+explicitFaceIndex*4)!=face.SourceOffset ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(face.SourceOffset+utf16CodeUnit*2,2))!=glyph ||
            !bytes.Slice(record.SourceOffset,12).SequenceEqual(record.RawBytes) ||
            !bytes.Slice(palette.SourceOffset,64).SequenceEqual(palette.RawBytes))
            throw Error("glyph map, metric or palette differs from the source");
        if(record.PixelEncoding is <0 or >2)throw Error("glyph pixel encoding is unsupported");
        int width=record.Width,height=record.Height;
        int rowWidth=record.PixelEncoding==0?(width+1)&~1:width,pixelCount=checked(rowWidth*height);
        byte[] output=new byte[checked(pixelCount*4)];int at=checked(collection.PixelPayloadOffset+record.PixelPayloadOffset),cursor=at;
        void StoreColor(int pixel,uint rgba)=>BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(pixel*4),rgba);
        if(record.PixelEncoding==0)
        {
            int count=checked(pixelCount/2);Need(bytes,cursor,count);
            for(int pixel=0;pixel<pixelCount;pixel+=2)
            {byte packed=bytes[cursor++];StoreColor(pixel,palette.PackedRgbaColors[packed>>4]);StoreColor(pixel+1,palette.PackedRgbaColors[packed&15]);}
        }
        else if(record.PixelEncoding==1)
        {
            int pixel=0;
            while(pixel<pixelCount)
            {
                Need(bytes,cursor,1);byte packed=bytes[cursor++];int run=(packed>>4)+1;
                if(run>pixelCount-pixel)throw Error("glyph run exceeds its source pixel extent");
                uint color=palette.PackedRgbaColors[packed&15];for(int n=0;n<run;n++)StoreColor(pixel++,color);
            }
        }
        else
        {
            Need(bytes,cursor,pixelCount);
            for(int pixel=0;pixel<pixelCount;pixel++){uint value=bytes[cursor++];StoreColor(pixel,value*0x01010101u);}
        }
        return new(utf16CodeUnit,explicitFaceIndex,explicitPaletteIndex,record,palette,at,
            bytes.Slice(at,cursor-at).ToArray(),new(rowWidth,height,output));
    }

    private static void Need(ReadOnlySpan<byte> bytes,int at,int size)
    {if(at<0||size<0||(long)at+size>bytes.Length)throw Error("glyph pixel data is truncated");}
    private static uint U(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(at,4));
    private static int Int(ReadOnlySpan<byte> bytes,int at)
    {uint value=U(bytes,at);if(value>int.MaxValue)throw Error("source extent exceeds the supported range");return(int)value;}
    private static InvalidDataException Error(string message)=>new($"Source G1N font {message}.");
}

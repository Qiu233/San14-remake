using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct ArmyFlagGlyphAtlasRectangle(int X,int Y,int Width,int Height)
{
    public int Right=>X+Width;
    public int Bottom=>Y+Height;
}

public sealed record LoadedArmyFlagGlyphAtlasPrefix(LoadedArmyFlagFontSource Font,
    G1FontGlyphPixels Glyph,string CallerInitialTemplateSource,string InitialTemplateSha256,
    string WrittenAtlasSha256,byte[] Bytes,ArmyFlagGlyphAtlasRectangle LogicalRectangle,
    ArmyFlagGlyphAtlasRectangle NativeRowWriteRectangle,Vector4 UvRectangle,
    IReadOnlyList<ArmyFlagGlyphAtlasRectangle> BorderZeroRegions,
    int NextCursorX,int NextCursorY,int NextRowBottom,int SourceGlyphCount)
{
    public int Width=>1024;
    public int Height=>1024;
    public int RowPitch=>1024*4;
    public int SourcePadding=>1;
    public int MipCount=>1;
    public byte SourcePixelFormat=>1;
    public uint NativeResourceFormat=>90;
    public uint NativeShaderViewFormat=>87;
    public byte[] SourceSamplerKey=>Convert.FromHexString("1514300000000000");
    public uint SourceCacheKey=>(uint)(Glyph.Utf16CodeUnit|(Glyph.FaceIndex&255)<<16|(Glyph.PaletteIndex&255)<<24);
    public bool AtlasStateFidelityVerified=>false;
    public bool NativeDecodePrecedesBorderClear=>true;
    public string NativeSourceProvenance=>"SC BFAB50..BFABDE atlas1024/mip1/format1/pad1;1064510 fresh reset;10649F0/1064BC1..1064D4B decode;1064D5D->1064790 border clear;1064B0F..1064B90 UV;8779B0 fresh base SRV87";
}

/// <summary>The first insertion into an explicitly owned fresh-reset native font
/// cache. The complete caller atlas is retained outside the exact source writes;
/// its contents are not assumed to describe original VRAM or allocation bytes.</summary>
public static class ArmyFlagGlyphAtlasPrefix
{
    public const int AtlasByteLength=1024*1024*4;
    public static LoadedArmyFlagGlyphAtlasPrefix WriteFirst(
        LoadedArmyFlagFontSource font,G1FontGlyphPixels actualSourceGlyph,
        ReadOnlySpan<byte> completeCallerInitialTemplate,string callerInitialTemplateSource,
        bool explicitOwnedFreshResetState)
    {
        ArgumentNullException.ThrowIfNull(font);ArgumentNullException.ThrowIfNull(actualSourceGlyph);
        if(!explicitOwnedFreshResetState)throw Error("requires the explicitly owned fresh reset state; current atlas state is unsupported");
        if(string.IsNullOrWhiteSpace(callerInitialTemplateSource)||completeCallerInitialTemplate.Length!=AtlasByteLength)
            throw Error("requires a labelled complete1024x1024 four-byte caller initial template");
        if(!font.FreshHostDefaultFontFidelityVerified||font.ExplicitCommonArrayIndex!=0||
            (uint)actualSourceGlyph.FaceIndex>255||(uint)actualSourceGlyph.PaletteIndex>255||
            (uint)actualSourceGlyph.FaceIndex>=font.Collection.Faces.Count||
            (uint)actualSourceGlyph.PaletteIndex>=font.Collection.Palettes.Count)
            throw Error("requires the source fresh Host default collection and explicit byte-sized face/palette selectors");
        if(actualSourceGlyph.Utf16CodeUnit is 0 or 0xA)
            throw Error("source text terminator/newline does not insert a first glyph");
        var glyph=actualSourceGlyph;
        var face=font.Collection.Faces[glyph.FaceIndex];
        if(!ReferenceEquals(face.Glyphs[face.UnicodeGlyphMap[glyph.Utf16CodeUnit]],glyph.Record)||
            !ReferenceEquals(font.Collection.Palettes[glyph.PaletteIndex],glyph.Palette))
            throw Error("glyph belongs to a different font collection");
        int width=glyph.Record.Width,height=glyph.Record.Height;
        if(width==0||height==0||glyph.Record.PixelEncoding!=0||glyph.NativeRowPixelCount>1023||height>1022)
            throw Error("source first glyph extent/encoding is unsupported");
        // The native format1 atlas is BGRA8. The actual fresh palette has equal
        // RGB channels, so retain the source words byte-for-byte, without swizzling.
        for(int at=0;at<glyph.Pixels.Pixels.Length;at+=4)
            if(glyph.Pixels.Pixels[at]!=glyph.Pixels.Pixels[at+1]||glyph.Pixels.Pixels[at]!=glyph.Pixels.Pixels[at+2])
                throw Error("fresh source glyph does not preserve its native channel invariant");
        byte[] atlas=completeCallerInitialTemplate.ToArray();
        var logical=new ArmyFlagGlyphAtlasRectangle(1,1,width,height);
        var rowWrite=new ArmyFlagGlyphAtlasRectangle(1,1,glyph.NativeRowPixelCount,height);
        for(int row=0;row<height;row++)
            glyph.Pixels.Pixels.AsSpan(row*glyph.NativeRowPixelCount*4,glyph.NativeRowPixelCount*4)
                .CopyTo(atlas.AsSpan(((row+1)*1024+1)*4,glyph.NativeRowPixelCount*4));
        // 1064D5D calls1064790 AFTER the decode. The right strip therefore
        // overwrites an odd-width glyph's final low-nibble padding pixel.
        ArmyFlagGlyphAtlasRectangle[] regions=
        [new(0,1,1,height),new(1+width,1,1,height),new(1,0,width+1,1),new(1,1+height,width+1,1)];
        foreach(var region in regions)
            for(int row=region.Y;row<region.Bottom;row++)atlas.AsSpan((row*1024+region.X)*4,region.Width*4).Clear();
        var uv=new Vector4(1f/1024,1f/1024,(1+width)/1024f,(1+height)/1024f);
        return new(font,glyph,callerInitialTemplateSource,Hash(completeCallerInitialTemplate),Hash(atlas),atlas,
            logical,rowWrite,uv,Array.AsReadOnly(regions),width+2,1,height+2,1);
    }

    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static InvalidDataException Error(string message)=>new($"Army flag glyph atlas prefix {message}.");
}

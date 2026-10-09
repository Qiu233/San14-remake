using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Original row order and all complete native ScreenMap inputs.
/// Widths, flags and view selection are supplied by their source writer.</summary>
public sealed record TerritoryInputs(float[] ViewToWorldRows, float[] ProjectionToWorldRows,
    float[] WorldToViewRows, Vector4 Field, Vector4[] CellColors, Vector4[] LineColors,
    Vector4 FrameColor, float[] LineWidths, Vector4 Cells, Vector4 Material,
    Vector4 Reflectance, float Curvature, uint LayerViewMask, uint[] Flags);

public static class TerritoryConstants
{
    public const int ByteCount = 6576;
    public const int PaletteCount = 128;
    public const int LayerCount = 7;
    public const int CellsPerLayer = 220 * 220;
    private static readonly (string Name, int Offset, int Size)[] Fields =
    [ ("mV2W",0,64),("mP2W",64,64),("mW2V",128,64),("vFieldParam",192,16),
      ("vCellColorList",208,2048),("vLineColorList",2256,2048),("vFrameColor",4304,16),
      ("fLineWidthList",4320,132),("vCellParam",4464,16),("vMaterialParam",4480,16),
      ("vReflectanceParam",4496,16),("fCurvature",4512,4),("nLayerViewMask",4516,4),("nFlagList",4528,2036) ];

    public static byte[] Pack(DxbcResourceDefinitions definitions, ReadOnlySpan<byte> completeTemplate,
        TerritoryInputs input)
    {
        ArgumentNullException.ThrowIfNull(definitions); ArgumentNullException.ThrowIfNull(input);
        if (completeTemplate.Length != ByteCount || definitions.Buffers.Count != 1 ||
            definitions.Buffers[0].ByteSize != ByteCount || definitions.Buffers[0].Variables.Count != Fields.Length ||
            Fields.Any(f => definitions.Buffers[0].Variables.Count(v => v.Name.Text == f.Name && v.StartOffset == f.Offset && v.ByteSize == f.Size) != 1))
            throw new InvalidDataException("Territory constants differ from the complete original 6576B layout.");
        Check(input.ViewToWorldRows,16); Check(input.ProjectionToWorldRows,16); Check(input.WorldToViewRows,16);
        Check(input.LineWidths,9);
        if (input.CellColors is null || input.LineColors is null || input.Flags is null ||
            input.CellColors.Length != PaletteCount || input.LineColors.Length != PaletteCount || input.Flags.Length != PaletteCount ||
            !float.IsFinite(input.Curvature) || input.Cells.X != 220 || input.Cells.Y != 220 || input.Cells.Z != 468 || input.Cells.W != 7)
            throw new InvalidDataException("Territory input needs both 128-color tables, nine widths, 128 flags and the source 220x220/468/7 cells.");
        byte[] bytes = completeTemplate.ToArray();
        void Float(int offset,float value)
        { if(!float.IsFinite(value))throw new InvalidDataException("Nonfinite territory constant."); BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset,4),value); }
        void Vector(int offset, Vector4 value)
        { Float(offset,value.X);Float(offset+4,value.Y);Float(offset+8,value.Z);Float(offset+12,value.W); }
        for(int i=0;i<16;i++){Float(i*4,input.ViewToWorldRows[i]);Float(64+i*4,input.ProjectionToWorldRows[i]);Float(128+i*4,input.WorldToViewRows[i]);}
        Vector(192,input.Field);
        for(int i=0;i<128;i++){Vector(208+i*16,input.CellColors[i]);Vector(2256+i*16,input.LineColors[i]);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4528+i*16,4),input.Flags[i]);}
        Vector(4304,input.FrameColor);for(int i=0;i<9;i++)Float(4320+i*16,input.LineWidths[i]);
        Vector(4464,input.Cells);Vector(4480,input.Material);Vector(4496,input.Reflectance);Float(4512,input.Curvature);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4516,4),input.LayerViewMask);
        return bytes;
    }

    /// <summary>Each source layer has 48400 byte indices. R32_UINT is an own
    /// typed-buffer adapter; one returned uint per byte preserves PB2SM loads.
    /// The original writer supplies byte indices, and PS1 declares a typed
    /// uint Buffer, never a raw SRV. Native allocator format36 needs its own
    /// DXGI format proof; this explicit adapter does not infer that mapping.</summary>
    public static byte[] ExpandZoneIndices(ReadOnlySpan<byte> sevenSourceLayers)
    {
        if(sevenSourceLayers.Length!=LayerCount*CellsPerLayer)
            throw new InvalidDataException("Territory zone source requires all seven complete 48400-byte layers.");
        byte[] bytes=new byte[sevenSourceLayers.Length*4];
        for(int i=0;i<sevenSourceLayers.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4,4),sevenSourceLayers[i]);
        return bytes;
    }
    private static void Check(float[] values,int count)
    {if(values is null||values.Length!=count||values.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Incomplete or nonfinite territory vector.");}
}

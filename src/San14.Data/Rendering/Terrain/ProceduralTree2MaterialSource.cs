using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual per-material Tree2 parameters. Current View/graphics wind
/// fields are prepared separately by OriginalTree2WindFrameSource.</summary>
public static class ProceduralTree2MaterialSource
{
    public static Tree2MaterialParameters Read(G1DrawMaterialBinding draw)
    {
        byte[] Require(string name,int type,int length)
        {
            var p=draw.ParameterBlock.Parameters.Single(v=>v.Name==name);
            if(p.TypeCode!=type || p.RawValueBytes.Length!=length)
                throw new InvalidDataException($"Tree2 material {name} source type/width differs.");
            return p.RawValueBytes;
        }
        float F(string name)=>BinaryPrimitives.ReadSingleLittleEndian(Require(name,1,4));
        byte[] deform=Require("vObjDeform",2,8);
        return new(F("fFxdHgt"),F("fObjHgt"),new(BinaryPrimitives.ReadSingleLittleEndian(deform),
            BinaryPrimitives.ReadSingleLittleEndian(deform.AsSpan(4))),F("fObjAnmSpd"),F("fObjBndRte"),
            BinaryPrimitives.ReadUInt32LittleEndian(Require("nIntrctTyp",5,4)));
    }
    /// <summary>Only source material/constants; preserves current camera/wind
    /// regions. A loaded template is completed by the normal current producers.</summary>
    public static void WriteStatic(Span<byte> destination,Tree2MaterialParameters material)
    {
        if(destination.Length!=272)throw new ArgumentException("Tree2 template requires272 bytes.");
        WriteFloat(destination,184,material.FixedHeight);WriteFloat(destination,188,material.ObjectHeight);
        WriteFloat(destination,192,material.ObjectDeform.X);WriteFloat(destination,196,material.ObjectDeform.Y);
        WriteFloat(destination,200,material.AnimationSpeed);WriteFloat(destination,204,material.BendRate);
        BinaryPrimitives.WriteInt32LittleEndian(destination[224..],512);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[228..],material.InteractionType);
        BinaryPrimitives.WriteInt32LittleEndian(destination[256..],64);
    }
    private static void WriteFloat(Span<byte> bytes,int offset,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes[offset..],value);
}

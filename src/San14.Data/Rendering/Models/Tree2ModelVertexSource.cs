using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Noninstanced normal Tree2's actual 272-byte vertex ABI. Validation
/// belongs to resource loading; current wind and its resources remain with the
/// normal View/graphics owner, independently of this retained model template.</summary>
internal static class Tree2ModelVertexSource
{
    internal static void RequireLayout(DxbcResourceDefinitions definitions)
    {
        if (definitions.Buffers.Count != 1 || definitions.Buffers[0].Name.Text != "$Globals" ||
            definitions.Buffers[0].ByteSize != 272)
            throw new InvalidDataException("Root Tree2 requires its actual 272-byte Globals buffer.");
        (string Name, int Offset, int Size, int Class, int Base, int Rows, int Columns, int Elements)[] fields = [
            ("mW2P",0,64,2,3,4,4,0), ("mL2WT",64,48,1,3,1,4,3), ("mL2W",112,48,1,3,1,4,3),
            ("fAllWndScl",160,4,0,3,1,1,0), ("vWndDir",164,12,1,3,1,3,0),
            ("fWndScale",176,4,0,3,1,1,0), ("fWndPhase",180,4,0,3,1,1,0),
            ("fFxdHgt",184,4,0,3,1,1,0), ("fObjHgt",188,4,0,3,1,1,0),
            ("vObjDeform",192,8,1,3,1,2,0), ("fObjAnmSpd",200,4,0,3,1,1,0),
            ("fObjBndRte",204,4,0,3,1,1,0), ("vIntrctRng",208,16,1,3,1,4,0),
            ("nIntrctRso",224,4,0,2,1,1,0), ("nIntrctTyp",228,4,0,2,1,1,0),
            ("vWndRngX",232,8,1,3,1,2,0), ("vWndRngY",240,8,1,3,1,2,0),
            ("vWndRngZ",248,8,1,3,1,2,0), ("nWndRso",256,4,0,2,1,1,0)];
        var variables = definitions.Buffers[0].Variables;
        if (variables.Count != fields.Length)
            throw new InvalidDataException("Root Tree2 vertex variable inventory differs.");
        foreach (var field in fields)
        {
            var variable = variables.Single(v => v.Name.Text == field.Name);
            var type = definitions.Types.Single(t => t.Offset == variable.TypeOffset);
            if (variable.StartOffset != field.Offset || variable.ByteSize != field.Size ||
                type.Class != field.Class || type.BaseType != field.Base || type.Rows != field.Rows ||
                type.Columns != field.Columns || type.Elements != field.Elements || type.Members.Count != 0)
                throw new InvalidDataException($"Root Tree2 variable {field.Name} differs from its source layout.");
        }
        (string Name, uint Kind, uint Register, uint Dimension)[] bindings = [
            ("__smpsWind",3,8,0), ("__smpsWindRipple",3,9,0),
            ("tInteractionIBuffer",2,0,1), ("tInteractionFBuffer",2,1,1),
            ("tInteractionSphereBuffer",2,2,1), ("tWindPhaseBuffer",2,3,1),
            ("sWind",2,8,8), ("sWindRipple",2,9,8), ("$Globals",0,0,0)];
        if (definitions.Bindings.Count != bindings.Length || bindings.Any(expected =>
            !definitions.Bindings.Any(actual => actual.Name.Text == expected.Name &&
                actual.InputType == expected.Kind && actual.BindPoint == expected.Register &&
                actual.Dimension == expected.Dimension && actual.BindCount == 1)))
            throw new InvalidDataException("Root Tree2 VS current-resource bindings differ.");
    }

    internal static byte[] WriteMatrices(ReadOnlySpan<byte> template, ModelGbufferVertexInputs input)
    {
        if (template.Length != 272) throw new InvalidDataException("Root Tree2 vertex template requires 272 bytes.");
        byte[] bytes = template.ToArray();
        Write(0, input.WorldToProjectionRows, 16);
        Write(64, input.LocalToWorldNormalRows, 12);
        Write(112, input.LocalToWorldPositionRows, 12);
        return bytes;
        void Write(int offset, float[] values, int count)
        {
            if (values.Length != count || values.Any(v => !float.IsFinite(v)))
                throw new InvalidDataException("Root Tree2 requires complete finite matrix rows.");
            foreach (float value in values)
            {
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset), value);
                offset += 4;
            }
        }
    }
}

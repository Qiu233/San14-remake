using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual selected CView and current light/material module values.
/// Ambient values and clip planes are supplied by their current owners.</summary>
public sealed record ReflectionCaptureModelLighting(Vector4 Direction, Vector4 Color,
    Vector4 Ambient, Vector4 Eye, float AmbientMapOcclusionRate,
    Vector4 AmbientDiffuseScale, Vector4 AmbientSpecularScale);

public static class ReflectionCaptureModelParameters
{
    private sealed record Field(string Name, int Offset, int Bytes);
    private sealed record Layout(int Bytes, Field[] Fields);
    private static readonly ConditionalWeakTable<DxbcResourceDefinitions, Layout> pixelLayouts = new();

    /// <summary>BD73D0 writes the real probe position and (1/range,bias,0,0).
    /// D69760 supplies the current cell position, actual ViewRange and zero bias.</summary>
    public static byte[] Pixel(DxbcResourceDefinitions definitions, ReadOnlySpan<byte> retainedCompleteTemplate,
        ReflectionCaptureFieldCellGeometry actualCell, ReflectionCaptureModelLighting actualLighting,
        Vector4 actualSpecular, float? actualAlphaTest)
    {
        ArgumentNullException.ThrowIfNull(actualLighting);
        var layout = pixelLayouts.GetValue(definitions, CompilePixel);
        if (retainedCompleteTemplate.Length != layout.Bytes) throw Invalid("retained_pixel_template");
        byte[] bytes = retainedCompleteTemplate.ToArray();
        foreach (var field in layout.Fields)
        {
            Vector4 value = field.Name switch {
                "litDir" => actualLighting.Direction, "litCol" => actualLighting.Color,
                "ambLit" => actualLighting.Ambient, "vEye" => actualLighting.Eye,
                "cSpec" => actualSpecular,
                "vATest" => new(actualAlphaTest ?? throw Invalid("actual_alpha_test"),0,0,0),
                "vProbePosition" => actualCell.Position,
                "vDistanceConv" => new(1f / actualCell.ViewRange,0,0,0),
                "fAmbMapsDORate" => new(actualLighting.AmbientMapOcclusionRate,0,0,0),
                "vAmbDifScl" => actualLighting.AmbientDiffuseScale,
                "vAmbSpcScl" => actualLighting.AmbientSpecularScale,
                _ => throw Invalid("pixel_semantic") };
            Write(bytes,field.Offset,value,field.Bytes / 4);
        }
        if (actualAlphaTest.HasValue != layout.Fields.Any(value => value.Name == "vATest"))
            throw Invalid("alpha_profile");
        return bytes;
    }

    /// <summary>Rigid state12 source matrices and six actual CView clip planes.
    /// Neither an identity camera nor zero clip planes is inferred here.</summary>
    public static byte[] RigidVertex(DxbcResourceDefinitions definitions, ReadOnlySpan<byte> retainedCompleteTemplate,
        ModelGbufferVertexInputs input, IReadOnlyList<Vector4> actualClipPlanes)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(actualClipPlanes);
        var cb = definitions.Buffers.Single(value => value.Name.Text == "$Globals");
        if (cb.ByteSize != 256 || retainedCompleteTemplate.Length != 256 || actualClipPlanes.Count != 6 ||
            input.WorldToProjectionRows.Length != 16 || input.LocalToWorldNormalRows.Length != 12 || input.LocalToWorldPositionRows.Length != 12 ||
            !cb.Variables.Select(value => value.Name.Text).Order().SequenceEqual(new[]{"mW2P","mL2WT","mL2W","ksgClipPlane"}.Order()))
            throw Invalid("rigid_vertex_profile");
        byte[] bytes = retainedCompleteTemplate.ToArray();
        void Matrix(string name, int offset, float[] values, int variableClass, int rows, int elements)
        {
            var field = cb.Variables.Single(value => value.Name.Text == name);
            var type = definitions.Types.Single(value => value.Offset == field.TypeOffset);
            if (field.StartOffset != offset || field.ByteSize != values.Length * 4 || type.Class != variableClass ||
                type.BaseType != 3 || type.Rows != rows || type.Columns != 4 || type.Elements != elements || type.Members.Count != 0)
                throw Invalid("rigid_matrix");
            for(int index=0;index<values.Length;index++) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset+index*4),values[index]);
        }
        Matrix("mW2P",0,input.WorldToProjectionRows,2,4,0);
        Matrix("mL2WT",64,input.LocalToWorldNormalRows,1,1,3);
        Matrix("mL2W",112,input.LocalToWorldPositionRows,1,1,3);
        var planes = cb.Variables.Single(value => value.Name.Text == "ksgClipPlane");
        var planeType = definitions.Types.Single(value => value.Offset == planes.TypeOffset);
        if (planes.StartOffset != 160 || planes.ByteSize != 96 || planeType is not {Class:1,BaseType:3,Rows:1,Columns:4,Elements:6} || planeType.Members.Count != 0)
            throw Invalid("clip_planes");
        for(int index=0;index<6;index++) Write(bytes,160+index*16,actualClipPlanes[index],4);
        return bytes;
    }

    private static Layout CompilePixel(DxbcResourceDefinitions definitions)
    {
        var cb = definitions.Buffers.Single(value => value.Name.Text == "$Globals");
        string[] required = ["litDir","litCol","ambLit","vEye","cSpec","vProbePosition","vDistanceConv","fAmbMapsDORate","vAmbDifScl","vAmbSpcScl"];
        var names = cb.Variables.Select(value => value.Name.Text).ToArray();
        if (!names.Where(value=>value!="vATest").Order().SequenceEqual(required.Order()) || names.Distinct().Count()!=names.Length)
            throw Invalid("pixel_inventory");
        var fields = new List<Field>();
        foreach(var variable in cb.Variables)
        {
            var type = definitions.Types.Single(value => value.Offset == variable.TypeOffset);
            bool scalar = variable.Name.Text is "vATest" or "fAmbMapsDORate";
            int size = scalar ? 4 : 16;
            if (variable.StartOffset < 0 || variable.StartOffset > cb.ByteSize-size || variable.ByteSize != size ||
                type.Class != (scalar?0:1) || type.BaseType != 3 || type.Rows != 1 || type.Columns != (scalar?1:4) ||
                type.Elements != (variable.Name.Text is "litDir" or "litCol" ? 1 : 0) || type.Members.Count != 0 ||
                fields.Any(value=>variable.StartOffset<value.Offset+value.Bytes && variable.StartOffset+size>value.Offset))
                throw Invalid("pixel_layout."+variable.Name.Text);
            fields.Add(new(variable.Name.Text,variable.StartOffset,size));
        }
        return new(cb.ByteSize,fields.ToArray());
    }
    private static void Write(Span<byte> bytes,int offset,Vector4 value,int words)
    {
        Span<float> values=[value.X,value.Y,value.Z,value.W];
        for(int index=0;index<words;index++) BinaryPrimitives.WriteSingleLittleEndian(bytes[(offset+index*4)..],values[index]);
    }
    private static InvalidDataException Invalid(string reason)=>new("reflection.capture_model."+reason);
}

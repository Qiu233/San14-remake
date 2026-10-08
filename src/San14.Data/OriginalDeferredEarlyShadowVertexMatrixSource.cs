using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalDeferredEarlyShadowVertexMatrixPacket(byte[] Bytes,
    IReadOnlyList<TerrainConstantWrite> Writes, IReadOnlyList<string> RetainedVariables,
    uint ConstantBufferSlot, int VertexProgramIndex, string SourceEvidence);

/// <summary>The actual selected state3 VS's imported CB0 layout. Import once
/// per renderer/program; normal draws reuse its source names and offsets.</summary>
public sealed class LoadedOriginalDeferredEarlyShadowVertexMatrixProgram
{
    private readonly LoadedOriginalDeferredEarlyShadowVertexMatrixNativeSource native;
    internal LoadedOriginalDeferredEarlyShadowVertexMatrixProgram(
        LoadedOriginalDeferredEarlyShadowVertexMatrixNativeSource actualNative,
        LoadedShaderContainer shader, int index, int size, bool skin, bool localToProjection, bool cloth,
        string programHash)
        => (native, Shader, VertexProgramIndex, ByteSize, Skinned, UsesLocalToProjection, Cloth, ProgramSha256)
            = (actualNative, shader, index, size, skin, localToProjection, cloth, programHash);
    public LoadedShaderContainer Shader { get; }
    public int VertexProgramIndex { get; }
    public int ByteSize { get; }
    public bool Skinned { get; }
    public bool UsesLocalToProjection { get; }
    public bool Cloth { get; }
    public uint ConstantBufferSlot => 0;
    public string ProgramSha256 { get; }
    public string SourceEvidence => native.SourceEvidence + ";" + Shader.Source.ResourceUri +
        $"/VS{VertexProgramIndex}@{ProgramSha256};actual-selected-state3-CB0;not-draw-completion";

    /// <summary>97C920 receives the actual final three position dot rows at
    /// frontend68/token0. It transposes them into native affine rows, appends
    /// the original (0,0,0,1), then multiplies by the current published 5D0.
    /// The 112B profile instead consumes those two inputs separately.</summary>
    public OriginalDeferredEarlyShadowVertexMatrixPacket PrepareRigid(
        ReadOnlySpan<float> actualFinalPositionShaderRows, Matrix4x4 actualPublishedShadow5D0)
    {
        if (Skinned || Cloth)
            throw new InvalidDataException("This shadow VS has additional actual skin/Cloth state and needs its complete owner template.");
        // Both supported ordinary rigid profiles write every CB byte.
        return PrepareRigid(new byte[ByteSize], actualFinalPositionShaderRows, actualPublishedShadow5D0);
    }

    /// <summary>Cloth profiles retain their actual vCtrl template. Ordinary
    /// rigid callers can use the overload whose entire CB is source-written.</summary>
    public OriginalDeferredEarlyShadowVertexMatrixPacket PrepareRigid(
        ReadOnlySpan<byte> actualCompleteTemplate, ReadOnlySpan<float> actualFinalPositionShaderRows,
        Matrix4x4 actualPublishedShadow5D0)
    {
        if (Skinned) throw new InvalidDataException("The actual selected shadow VS requires skin rows.");
        OriginalMapStaticModelTransforms.ValidateRows(actualFinalPositionShaderRows.ToArray(), 12);
        byte[] bytes = Template(actualCompleteTemplate);
        var writes = new List<TerrainConstantWrite>();
        if (UsesLocalToProjection)
            Matrix(bytes, writes, "mL2P", native.ComposeRigidLocalToProjection(
                actualFinalPositionShaderRows, actualPublishedShadow5D0));
        else
        {
            Matrix(bytes, writes, "mW2P", actualPublishedShadow5D0);
            Floats(bytes, 64, actualFinalPositionShaderRows);
            writes.Add(new("mL2W", 64, 3, 16, 16));
        }
        return Packet(bytes, writes);
    }

    /// <summary>Ordinary skin CB0 from a complete current world palette and
    /// the actual three skin decisions. Inactive rows must already carry their
    /// caller-owned values. Rows include ModelWorld exactly once.</summary>
    public OriginalDeferredEarlyShadowVertexMatrixPacket PrepareSkinned(
        IReadOnlyList<Vector4> actualCompleteWorldPaletteRows, Matrix4x4 actualPublishedShadow5D0,
        IReadOnlyList<bool> actualSkinFlags)
    {
        if (!Skinned || Cloth)
            throw new InvalidDataException("This shadow VS needs its actual skin layout without additional Cloth state.");
        ArgumentNullException.ThrowIfNull(actualCompleteWorldPaletteRows);
        ArgumentNullException.ThrowIfNull(actualSkinFlags);
        if (actualCompleteWorldPaletteRows.Count != 384)
            throw new InvalidDataException("A shadow skin packet without a prior CB needs all 384 actual current palette rows.");
        // All declared values come from current owners. The shader does not
        // read the three bool registers' packing padding, initialized to zero.
        return PrepareSkinned(new byte[ByteSize], actualCompleteWorldPaletteRows,
            actualPublishedShadow5D0, actualSkinFlags);
    }

    /// <summary>D283CD copies the actual active rows. Other palette entries
    /// and Cloth vCtrl retain their actual prior shadow CB0. Rows include the
    /// real ModelWorld composition when D28311 applies. Optional skin flags
    /// come from the current owner; otherwise its actual bSkin bytes remain.</summary>
    public OriginalDeferredEarlyShadowVertexMatrixPacket PrepareSkinned(
        ReadOnlySpan<byte> actualCompleteTemplate, IReadOnlyList<Vector4> actualPreparedWorldPaletteRows,
        Matrix4x4 actualPublishedShadow5D0, IReadOnlyList<bool>? actualSkinFlags = null)
    {
        if (!Skinned) throw new InvalidDataException("The actual selected shadow VS requires rigid rows.");
        ArgumentNullException.ThrowIfNull(actualPreparedWorldPaletteRows);
        if (actualPreparedWorldPaletteRows.Count is < 3 or > 384 || actualPreparedWorldPaletteRows.Count % 3 != 0)
            throw new InvalidDataException("Shadow skin transport needs the actual complete active triplets within 128 matrices.");
        if (actualSkinFlags is not null && actualSkinFlags.Count != 3)
            throw new InvalidDataException("Shadow bSkin requires its actual three current owner flags.");
        byte[] bytes = Template(actualCompleteTemplate);
        var writes = new List<TerrainConstantWrite>();
        Matrix(bytes, writes, "mW2P", actualPublishedShadow5D0);
        float[] palette = actualPreparedWorldPaletteRows.SelectMany(v => new[] { v.X, v.Y, v.Z, v.W }).ToArray();
        Floats(bytes, 64, palette);
        writes.Add(new("mL2W", 64, actualPreparedWorldPaletteRows.Count, 16, 16));
        if (actualSkinFlags is not null)
        {
            for (int i = 0; i < 3; i++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(6208 + i * 16), actualSkinFlags[i] ? 1u : 0u);
            writes.Add(new("bSkin", 6208, 3, 16, 4));
        }
        return Packet(bytes, writes);
    }

    private byte[] Template(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteSize)
            throw new InvalidDataException("Shadow matrix transport needs the actual selected VS's complete prior CB0.");
        return bytes.ToArray();
    }
    private OriginalDeferredEarlyShadowVertexMatrixPacket Packet(byte[] bytes, List<TerrainConstantWrite> writes)
    {
        var retained = new List<string>();
        if (Skinned && !writes.Any(w => w.Name == "bSkin")) retained.Add("bSkin");
        if (Skinned && writes.Single(w => w.Name == "mL2W").ElementCount < 384) retained.Add("mL2W inactive rows");
        if (Cloth) retained.Add("vCtrl");
        return new(bytes, writes.AsReadOnly(), retained.AsReadOnly(), ConstantBufferSlot, VertexProgramIndex, SourceEvidence);
    }
    private static void Matrix(byte[] bytes, List<TerrainConstantWrite> writes, string name, Matrix4x4 m)
    {
        Floats(bytes, 0, [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
            m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44]);
        writes.Add(new(name, 0, 1, 64, 64));
    }
    private static void Floats(byte[] bytes, int offset, ReadOnlySpan<float> values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!float.IsFinite(values[i])) throw new InvalidDataException("Shadow matrix source contains a nonfinite word.");
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + i * 4), values[i]);
        }
    }
}

public static class OriginalDeferredEarlyShadowVertexMatrixSource
{
    /// <summary>Import/reload only. The caller supplies the actual current
    /// state3 renderer and selected VS; this does not choose a variant/pass.</summary>
    public static LoadedOriginalDeferredEarlyShadowVertexMatrixProgram Import(
        LoadedOriginalDeferredEarlyShadowVertexMatrixNativeSource native,
        LoadedOriginalDeferredEarlyShadowRenderer actualRenderer, int actualVertexProgramIndex)
    {
        ArgumentNullException.ThrowIfNull(native); ArgumentNullException.ThrowIfNull(actualRenderer);
        LoadedShaderContainer shader = actualRenderer.Shader;
        string expected = shader.Source.FileId switch
        {
            0x193FCD66 => "11BFCB4371B270DE4BC9B6A1E4ABADC5A24C29A6953E9D87A2F922F330C2FDDE",
            0x7B385503 => "696396F19DA4DD9F35F04CB6F3814144793973D8556E7009FFDEDF13DFB7E9C1",
            0x9DE72C62 => "6A4EE5D612C101F36A66518AC362664E19B1D169013D270BB909A8E832D54B41",
            0xA8E712FC => "223036BAAF9CE02F30C91919FAF79F724F6629F15DBD1B0E4CC16927B4F78D15",
            0x364F4C39 => "A5AAC1936E17A16E9BEB35C65C778D69CEA47ED81EC5B98E6DFE796B4ECEC73A",
            0xAF84F630 => "7A7D60735F8CA29FF95FE3558A208C94D0E8B335D3B6E94755F2C5C3ECEAAFF3",
            0x6C1A3F75 => "D593BE71E4B33F3053ED31FC48C6ABD7866D55F11CED9373A725B36B8E97CA23",
            0x33186A74 => "4F7C9D2B751A26614C1A653E19067F37A82F0B75AFCEBBD9A7D08FFB4EC14D0C",
            _ => throw new NotSupportedException("State3 vertex matrix transport has no imported profile for this actual G1S.")
        };
        if (actualRenderer.Selected.Interface?.Provider?.Object.ClassId != 0x39E93192 ||
            shader.Source.DecodedSha256 != expected || Convert.ToHexString(SHA256.HashData(shader.Source.Bytes)) != expected ||
            actualVertexProgramIndex is < 0 or > 7 ||
            !actualRenderer.Bindings.Combinations.Rows.Any(r => r.Stages.Any(s => s.StageCode == 1 &&
                s.Entry?.ProgramIndex == actualVertexProgramIndex)))
            throw new InvalidDataException("Shadow matrix import requires its verified actual generic state3 VS.");
        bool skin = actualVertexProgramIndex >= 4;
        bool cloth = shader.Source.FileId is 0x7B385503 or 0xAF84F630 or 0x33186A74;
        bool l2p = !skin && shader.Source.FileId != 0x9DE72C62;
        int size = (skin ? 6256 : l2p ? 64 : 112) + (cloth ? 8192 : 0);
        DxbcResourceDefinitions definitions = shader.ReadResourceDefinitions(actualVertexProgramIndex);
        var constantBindings = definitions.Bindings.Where(b => b.InputType == 0).ToArray();
        if (definitions.Buffers.Count != 1 || definitions.Buffers[0].Name.Text != "$Globals" ||
            definitions.Buffers[0].ByteSize != size ||
            constantBindings.Length != 1 || constantBindings[0] is not { BindPoint: 0, BindCount: 1 } ||
            constantBindings[0].Name.Text != "$Globals")
            throw new InvalidDataException("State3 matrix profile requires its exact single VS CB0 declaration.");
        DxbcRdefBuffer buffer = definitions.Buffers[0];
        var fields = new List<(string Name,int Offset,int Size,ushort Class,ushort Base,ushort Rows,ushort Columns,ushort Elements)>();
        fields.Add((l2p ? "mL2P" : "mW2P",0,64,2,3,4,4,0));
        if (!l2p) fields.Add(("mL2W",64,skin ? 6144 : 48,1,3,1,4,skin ? (ushort)384 : (ushort)3));
        if (skin) fields.Add(("bSkin",6208,36,0,1,1,1,3));
        if (cloth) fields.Add(("vCtrl",skin ? 6256 : 64,8192,1,3,1,4,512));
        if (buffer.Variables.Count != fields.Count) throw new InvalidDataException("Shadow VS variable inventory differs.");
        foreach (var f in fields)
        {
            var v = buffer.Variables.SingleOrDefault(v => v.Name.Text == f.Name) ??
                throw new InvalidDataException("Shadow VS lacks its source matrix field.");
            var t = definitions.Types.Single(t => t.Offset == v.TypeOffset);
            if (v.StartOffset != f.Offset || v.ByteSize != f.Size || t.Class != f.Class || t.BaseType != f.Base ||
                t.Rows != f.Rows || t.Columns != f.Columns || t.Elements != f.Elements || t.Members.Count != 0)
                throw new InvalidDataException($"Shadow VS source field {f.Name} layout differs.");
        }
        return new(native,shader,actualVertexProgramIndex,size,skin,l2p,cloth,
            Convert.ToHexString(SHA256.HashData(shader.Container.GetProgramBytes(shader.Source.Bytes,actualVertexProgramIndex))));
    }
}

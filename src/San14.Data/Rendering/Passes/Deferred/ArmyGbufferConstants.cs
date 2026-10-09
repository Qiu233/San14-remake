using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ArmyRigidTransformInputs(float[] NormalRows, float[] PositionRows);
public sealed record ArmySkinTransformInputs(IReadOnlyList<Vector4> PaletteRows,
    IReadOnlyList<bool> SkinFlags);
public sealed record ArmyVertexConstantInputs(float[]? WorldToProjectionRows = null,
    ArmyRigidTransformInputs? Rigid = null, ArmySkinTransformInputs? Skin = null,
    float[]? PreviousWorldToProjectionRows = null, float[]? WorldToShadowRows = null,
    Vector4? Fog = null);
public sealed record ArmyVertexConstantPacket(byte[] Bytes,
    IReadOnlyList<TerrainConstantWrite> Writes, IReadOnlyList<string> RetainedVariables,
    string ShaderResourceUri, string ShaderDecodedSha256, int ProgramIndex, string ProgramSha256);

/// <summary>Exact named CB0 writes for the source PB2-I2m vertex programs.
/// Matrices, all 384 palette rows and the three skin flags are explicit inputs.
/// This writer neither selects a runtime variant nor manufactures animation or
/// current bone matrices. A null input retains the complete caller template.</summary>
public static class ArmyGbufferConstants
{
    public const uint ShaderFileId = 0xD869FD7A;
    public const string ShaderSha256 = "83B543DAB21748A70372007591D4E81E950E95CEC6724E08A75B7E3E844BA7AE";

    public static ArmyVertexConstantPacket Vertex(LoadedShaderContainer shader, int programIndex,
        ReadOnlySpan<byte> explicitCompleteTemplate, ArmyVertexConstantInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(inputs);
        if (shader.Source.ResourceUri != GameResourceId.Rdb("system", ShaderFileId).ToString() ||
            shader.Source.DecodedSha256 != ShaderSha256 ||
            Convert.ToHexString(SHA256.HashData(shader.Source.Bytes)) != ShaderSha256 ||
            programIndex is < 0 or > 3 ||
            !shader.Container.StageTables.Any(t => t.StageCode == 1 && t.Entries.Any(e => e.ProgramIndex == programIndex)))
            throw new InvalidDataException("Army vertex constants require the exact supported PB2-I2m source program.");
        DxbcResourceDefinitions definitions = shader.ReadResourceDefinitions(programIndex);
        bool skin = programIndex is 1 or 3, fog = programIndex is 2 or 3;
        int size = (skin ? 6576 : 480) + (fog ? 16 : 0);
        if (definitions.Buffers.Count != 1 || definitions.Buffers[0].Name.Text != "$Globals" ||
            definitions.Buffers[0].ByteSize != size || explicitCompleteTemplate.Length != size ||
            definitions.Bindings.Count(b => b.Name.Text == "$Globals" && b.InputType == 0 && b.BindPoint == 0 && b.BindCount == 1) != 1)
            throw new InvalidDataException("Army vertex CB0 requires its complete original buffer template.");
        DxbcRdefBuffer buffer = definitions.Buffers[0];
        string[] inventory = skin ? ["mW2P", "mL2W", "bSkin", "mW2Pt", "mW2S"] :
            ["mW2P", "mL2WT", "mL2W", "mW2Pt", "mW2S"];
        if (fog) inventory = inventory.Append("fog").ToArray();
        if (!buffer.Variables.Select(v => v.Name.Text).Order().SequenceEqual(inventory.Order()))
            throw new InvalidDataException("Army vertex variable inventory differs from the source profile.");
        if (skin && inputs.Rigid is not null || !skin && inputs.Skin is not null || !fog && inputs.Fog is not null)
            throw new InvalidDataException("Army transform/fog inputs do not match the explicitly selected source program.");
        byte[] bytes = explicitCompleteTemplate.ToArray();
        var writes = new List<TerrainConstantWrite>();
        if (inputs.WorldToProjectionRows is { } projection)
            Float("mW2P", 0, projection, 2, 4, 4, 0);
        if (inputs.Rigid is { } rigid)
        {
            Float("mL2WT", 64, rigid.NormalRows, 1, 1, 4, 3);
            Float("mL2W", 112, rigid.PositionRows, 1, 1, 4, 3);
        }
        if (inputs.Skin is { } bones)
        {
            if (bones.PaletteRows is null || bones.PaletteRows.Count != 384 ||
                bones.SkinFlags is null || bones.SkinFlags.Count != 3)
                throw new InvalidDataException("Army skin input requires all 384 explicit float4 rows and three explicit flags.");
            Float("mL2W", 64, bones.PaletteRows.SelectMany(row => new[] { row.X, row.Y, row.Z, row.W }).ToArray(), 1, 1, 4, 384);
            Write("bSkin", 6208, bones.SkinFlags.Select(flag => flag ? 1u : 0u).ToArray(), 0, 1, 1, 1, 3);
        }
        if (inputs.PreviousWorldToProjectionRows is { } previous)
            Float("mW2Pt", skin ? 6256 : 160, previous, 2, 4, 4, 0);
        if (inputs.WorldToShadowRows is { } shadows)
            Float("mW2S", skin ? 6320 : 224, shadows, 2, 4, 4, 4);
        if (inputs.Fog is { } value)
            Float("fog", skin ? 6576 : 480, [value.X, value.Y, value.Z, value.W], 1, 1, 4, 0);
        string[] retained = buffer.Variables.Select(v => v.Name.Text)
            .Where(name => !writes.Any(write => write.Name == name)).ToArray();
        return new(bytes, writes.AsReadOnly(), Array.AsReadOnly(retained), shader.Source.ResourceUri,
            shader.Source.DecodedSha256, programIndex,
            Convert.ToHexString(SHA256.HashData(shader.Container.GetProgramBytes(shader.Source.Bytes, programIndex))));

        void Float(string name, int offset, float[]? values, ushort variableClass,
            ushort rows, ushort columns, ushort elements)
        {
            int count = Math.Max(1, (int)elements) * rows * columns;
            if (values is null || values.Length != count || values.Any(value => !float.IsFinite(value)))
                throw new InvalidDataException($"Army vertex field {name} requires {count} finite explicit float32 values.");
            Write(name, offset, values.Select(BitConverter.SingleToUInt32Bits).ToArray(), variableClass, 3, rows, columns, elements);
        }

        void Write(string name, int offset, uint[] words, ushort variableClass, ushort baseType,
            ushort rows, ushort columns, ushort elements)
        {
            DxbcRdefVariable variable = buffer.Variables.Single(v => v.Name.Text == name);
            DxbcRdefType type = definitions.Types.Single(t => t.Offset == variable.TypeOffset);
            int count = Math.Max(1, (int)elements), width = rows * columns * 4;
            int stride = elements == 0 ? width : Math.Max(16, width);
            int span = (count - 1) * stride + width;
            if (variable.StartOffset != offset || variable.ByteSize != span || type.Class != variableClass ||
                type.BaseType != baseType || type.Rows != rows || type.Columns != columns || type.Elements != elements ||
                type.Members.Count != 0 || words.Length != count * rows * columns || offset + span > bytes.Length)
                throw new InvalidDataException($"Army vertex field {name} differs from its exact RDEF shape.");
            for (int i = 0; i < count; i++) for (int j = 0; j < width / 4; j++)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + i * stride + j * 4, 4), words[i * width / 4 + j]);
            writes.Add(new(name, offset, count, stride, width));
        }
    }
}

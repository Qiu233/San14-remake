using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Initial named Army constants from the actual definition and skeleton.
/// Active palette entries have a constructor producer; unused capacity is storage only.</summary>
internal sealed class ArmyGeometryConstants
{
    readonly G1ModelTransforms skeleton;
    readonly IReadOnlyList<Matrix4x4> matrices;
    readonly MapCameraConstructionPacket camera;

    public ArmyGeometryConstants(LoadedModelTransforms transforms, UnitWorldTransformState root,
        MapCameraConstructionPacket initialCamera)
    {
        skeleton = transforms.Transforms; camera = initialCamera;
        var local = new ArmyAnimationLocalPose(0, 0, skeleton.Joints.SelectMany(j => j.RawBytes).ToArray(), []);
        matrices = ArmyAnimationHierarchy.Build(skeleton, local,
            new(new(root.Scale34, 0), root.Quaternion24, new(root.Position18, 1))).BuildMatrices();
    }

    public static uint[] SkinControls(ModelMatrixBindingInput matrix)
    {
        uint mode = matrix.CurrentWords[1];
        if (mode > 7) throw new NotSupportedException("Army skin mode has an unsupported control bit.");
        return [(mode & 2) != 0 ? 1u : 0u, (mode & 1) != 0 ? 1u : 0u, (mode & 4) != 0 ? 1u : 0u];
    }

    public byte[] Vertex(G1DrawRecord draw, ModelMatrixBindingInput matrix, DxbcResourceDefinitions definitions)
    {
        int index = checked((int)draw.UnknownValue8);
        var palette = skeleton.JointPalettes[index];
        var active = ArmyNativePaletteWriter.Build(skeleton, index, matrices, matrices.Count,
            new Vector4[384], ArmyNativePoseBufferKind.Current);
        if (active.WrittenEntryCount != palette.Entries.Count)
            throw new NotSupportedException("Army constructor leaves a referenced palette entry unresolved.");
        var rows = active.Rows.Take(checked(palette.Entries.Count * 3)).ToArray();
        int size = definitions.Buffers.Single().ByteSize;
        if (size == 6256)
            return CityGateAccessorySkinConstants.Vertex(definitions, camera.SourceWorldToProjection, rows, SkinControls(matrix));
        if (size != 160 || rows.Length != 3)
            throw new NotSupportedException("Army vertex constants require a supported rigid or skin interface.");
        float[] position = rows.SelectMany(v => new[] { v.X, v.Y, v.Z, v.W }).ToArray();
        return ModelGbufferConstants.Vertex(definitions, new byte[size], new(MatrixWords(camera.SourceWorldToProjection),
            UnitWorldTransformNormal.FromPositionShaderRows(position), position)).Bytes;
    }

    public byte[] Pixel(DxbcResourceDefinitions definitions, ModelGbufferMaterialSource material, ModelQueueStateSource queue)
    {
        var specular = ModelGbufferMaterialSourceReader.PrepareSpecular(material, 0);
        if (!specular.WritesValue || specular.Value is null || material.MaterialId is null)
            throw new NotSupportedException("Army material requires an actual initial specular and material ID producer.");
        float? alpha = definitions.Buffers.Single().Variables.Any(v => v.Name.Text == "vATest") ?
            ModelGbufferMaterialSourceReader.PrepareAlphaTest(material.SourceAlphaReference, queue.BackendAlphaEnabled) : null;
        return ModelGbufferConstants.Pixel(definitions, new byte[80], new(specular.Value.Value,
            MapStaticModelTransforms.ToShaderAffineRows(camera.SourceWorldToView), material.MaterialId.Value, alpha)).Bytes;
    }

    static float[] MatrixWords(Matrix4x4 m) => [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
        m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
}

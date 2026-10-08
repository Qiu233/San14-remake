using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data.Formats;

public sealed record G1ArmyBindPoseInfluence(int RegisterOffset, int PaletteEntryIndex,
    float Weight, bool ImplicitLastWeight);
public sealed record G1ArmyBindPoseDrawPositions(G1RestPoseDrawPositions Positions,
    int InfluenceCount, IReadOnlyList<IReadOnlyList<G1ArmyBindPoseInfluence>> Influences);

/// <summary>Source bind-pose preview for the verified Army storage profiles.
/// Applies source reference × source joint matrices, raw register-offset indices
/// and the original PB2-I2m VS1/3 implicit-last-weight subtraction. It does not
/// generate animation, choose current bSkin flags or infer original current pose.</summary>
public static class G1ArmyBindPoseReader
{
    public static G1ArmyBindPoseDrawPositions TransformPositions(G1ModelTransforms transforms,
        G1DrawRecord draw, G1DecodedVertexLayout layout)
    {
        ArgumentNullException.ThrowIfNull(transforms);
        ArgumentNullException.ThrowIfNull(draw);
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.Index != draw.LayoutIndex || draw.UnknownValue8 >= transforms.JointPalettes.Count)
            throw Error(draw, "references a mismatched layout or missing palette");
        G1DecodedAttribute[] positions = layout.Attributes.Where(a => a.UsageCode == 0).ToArray();
        G1DecodedAttribute[] weights = layout.Attributes.Where(a => a.UsageCode == 1).ToArray();
        G1DecodedAttribute[] indices = layout.Attributes.Where(a => a.UsageCode == 2).ToArray();
        if (positions.Length != 1 || weights.Length != 1 || indices.Length != 1)
            throw Error(draw, "requires one position, one stored weight and one index attribute");
        G1DecodedAttribute position = positions[0], weight = weights[0], selector = indices[0];
        int count = weight.ComponentCount + 1;
        if (position.UsageIndex != 0 || position.Encoding != G1AttributeEncoding.Float32x3 || position.ComponentCount != 3 ||
            position.BufferIndex != draw.PrimaryVertexBufferIndex || weight.UsageIndex != 0 ||
            weight.Encoding is not (G1AttributeEncoding.Float32x1 or G1AttributeEncoding.Float32x2 or G1AttributeEncoding.Float32x3) ||
            count is < 2 or > 4 || selector.UsageIndex != 0 || selector.ComponentCount != 4 ||
            selector.Encoding is not (G1AttributeEncoding.UInt8x4 or G1AttributeEncoding.SInt16x4) ||
            weight.BufferIndex != position.BufferIndex || selector.BufferIndex != position.BufferIndex ||
            weight.ElementCount != position.ElementCount || selector.ElementCount != position.ElementCount ||
            position.Values.Length != (long)position.ElementCount * 3 ||
            weight.Values.Length != (long)weight.ElementCount * weight.ComponentCount ||
            selector.Values.Length != (long)selector.ElementCount * 4 || draw.VertexStart < 0 || draw.VertexCount < 0 ||
            (long)draw.VertexStart + draw.VertexCount > position.ElementCount)
            throw Error(draw, "has an unsupported weighted Army attribute/window profile");
        G1JointPalette palette = transforms.JointPalettes[(int)draw.UnknownValue8];
        var matrices = new Dictionary<int, Matrix4x4>();
        var output = new Vector3[draw.VertexCount];
        var firstEntries = new int[draw.VertexCount];
        var influences = new IReadOnlyList<G1ArmyBindPoseInfluence>[draw.VertexCount];
        for (int i = 0; i < draw.VertexCount; i++)
        {
            int vertex = draw.VertexStart + i;
            ReadOnlySpan<float> sourcePosition = position.GetElement(vertex);
            ReadOnlySpan<float> stored = weight.GetElement(vertex);
            ReadOnlySpan<float> offsets = selector.GetElement(vertex);
            var perVertex = new G1ArmyBindPoseInfluence[count];
            float remaining = 1f;
            Vector3 result = default;
            // Original 2-weight branch computes bone1*(1-w0) first, then MADs
            // bone0*w0. The 3/4-weight branches accumulate stored weights and
            // append the implicit final influence.
            var transformed = new Vector3[count];
            for (int j = 0; j < count; j++)
            {
                float rawOffset = offsets[j];
                if (!float.IsFinite(rawOffset) || rawOffset < 0 || rawOffset > int.MaxValue || rawOffset % 3 != 0)
                    throw Error(draw, $"vertex {vertex} has an invalid three-row register offset");
                int entryIndex = (int)rawOffset / 3;
                if ((uint)entryIndex >= palette.Entries.Count) throw Error(draw, $"vertex {vertex} references a missing palette entry");
                if (!matrices.TryGetValue(entryIndex, out Matrix4x4 matrix))
                {
                    G1JointPaletteEntry entry = palette.Entries[entryIndex];
                    if ((uint)entry.MatrixIndex >= transforms.ReferenceMatrices.Count ||
                        (uint)entry.JointIndex >= transforms.JointModelMatrices.Count)
                        throw Error(draw, "palette references a missing source matrix or joint");
                    matrix = transforms.ReferenceMatrices[entry.MatrixIndex].Matrix * transforms.JointModelMatrices[entry.JointIndex];
                    if (!Finite(matrix)) throw Error(draw, "source reference/joint composition is non-finite");
                    matrices.Add(entryIndex, matrix);
                }
                float value = j == count - 1 ? remaining : stored[j];
                if (!float.IsFinite(value)) throw Error(draw, $"vertex {vertex} has non-finite source weights");
                if (j < count - 1) remaining = Subtract(remaining, value);
                perVertex[j] = new((int)rawOffset, entryIndex, value, j == count - 1);
                transformed[j] = Vector3.Transform(new(sourcePosition[0], sourcePosition[1], sourcePosition[2]), matrix);
            }
            if (count == 2)
            {
                result = Multiply(transformed[1], perVertex[1].Weight);
                result = Add(Multiply(transformed[0], perVertex[0].Weight), result);
            }
            else
            {
                result = Add(Multiply(transformed[0], perVertex[0].Weight), Multiply(transformed[1], perVertex[1].Weight));
                for (int j = 2; j < count; j++) result = Add(Multiply(transformed[j], perVertex[j].Weight), result);
            }
            if (!float.IsFinite(result.X) || !float.IsFinite(result.Y) || !float.IsFinite(result.Z))
                throw Error(draw, $"vertex {vertex} weighted bind pose is non-finite");
            output[i] = result;
            firstEntries[i] = perVertex[0].PaletteEntryIndex;
            influences[i] = Array.AsReadOnly(perVertex);
        }
        return new(new(draw.Index, (int)draw.UnknownValue8, draw.VertexStart,
            Array.AsReadOnly(output), Array.AsReadOnly(firstEntries)), count, Array.AsReadOnly(influences));
    }

    private static bool Finite(Matrix4x4 matrix) =>
        new[] { matrix.M11,matrix.M12,matrix.M13,matrix.M14,matrix.M21,matrix.M22,matrix.M23,matrix.M24,
            matrix.M31,matrix.M32,matrix.M33,matrix.M34,matrix.M41,matrix.M42,matrix.M43,matrix.M44 }.All(float.IsFinite);
    private static Vector3 Multiply(Vector3 value, float weight) =>
        new(Product(value.X,weight),Product(value.Y,weight),Product(value.Z,weight));
    private static Vector3 Add(Vector3 a, Vector3 b) => new(Sum(a.X,b.X),Sum(a.Y,b.Y),Sum(a.Z,b.Z));
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Product(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sum(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float a, float b) => a - b;
    private static InvalidDataException Error(G1DrawRecord draw, string message) =>
        new($"Army source bind pose draw {draw.Index} {message}.");
}

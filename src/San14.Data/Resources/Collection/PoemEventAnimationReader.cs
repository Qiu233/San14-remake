using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public sealed record PoemEventCurveKey(float Frame, float OutDx, float OutDy,
    float InDx, float InDy, float OutValue, float InValue, byte Interpolation);

public sealed record PoemEventCurve(float DefaultValue, float Minimum, float Maximum,
    IReadOnlyList<PoemEventCurveKey> Keys)
{
    /// <summary>The resource loader quantizes all seven doubles to float before
    /// the source curve evaluator performs its double-precision interpolation.</summary>
    public double Sample(float frame)
    {
        if (!float.IsFinite(frame)) throw new ArgumentOutOfRangeException(nameof(frame));
        if (Keys.Count == 0) return Clamp(DefaultValue);
        frame = Math.Clamp(frame, 0, Keys[^1].Frame);
        int next = 0;
        while (next < Keys.Count && frame >= Keys[next].Frame) next++;
        if (next == 0) return Clamp(Keys[0].OutValue);
        if (next == Keys.Count) return Clamp(Keys[^1].InValue);
        var a = Keys[next - 1];
        var b = Keys[next];
        if (frame <= a.Frame || a.Interpolation == 1) return Clamp(a.OutValue);
        if (a.Interpolation == 2)
        {
            double t = ((double)frame - a.Frame) / ((double)b.Frame - a.Frame);
            return Clamp(a.OutValue * (1 - t) + b.InValue * t);
        }
        if (a.Interpolation != 3)
            throw new InvalidDataException("poem-event.animation.curve_interpolation");
        double x0 = a.Frame, x1 = (double)a.Frame + a.OutDx;
        double x2 = (double)b.Frame + b.InDx, x3 = b.Frame;
        double u = FirstUnitRoot(x3 - 3 * x2 + 3 * x1 - x0,
            3 * x2 - 6 * x1 + 3 * x0, 3 * (x1 - x0), x0 - frame);
        double v = 1 - u;
        return Clamp(v * v * v * a.OutValue +
            3 * v * v * u * ((double)a.OutValue + a.OutDy) +
            3 * v * u * u * ((double)b.InValue + b.InDy) + u * u * u * b.InValue);
    }

    private double Clamp(double value) => value < Maximum
        ? Minimum <= value ? value : Minimum : Maximum;

    // Solve the authored time cubic and retain the first root in [0,1], as
    // the source evaluator does when a curve has more than one intersection.
    private static double FirstUnitRoot(double a, double b, double c, double d)
    {
        const double epsilon = 1.1920928955078125e-5;
        Span<double> roots = stackalloc double[3];
        int count = 0;
        if (Math.Abs(a) > epsilon)
        {
            double normalizedB = b / a;
            double p = c / a - normalizedB * normalizedB / 3;
            double q = 2 * normalizedB * normalizedB * normalizedB / 27 - normalizedB * c / (3 * a) + d / a;
            double discriminant = 18 * a * b * c * d - 4 * b * b * b * d +
                b * b * c * c - 4 * a * c * c * c - 27 * a * a * d * d;
            if (discriminant > epsilon && p < 0)
            {
                double radius = 2 * Math.Sqrt(-p / 3);
                double angle = Math.Acos(Math.Clamp(3 * q / p / radius, -1, 1)) / 3;
                for (int i = 0; i < 3; i++)
                    roots[count++] = radius * Math.Cos(angle + i * 2.0943951023931953) - normalizedB / 3;
            }
            else if (Math.Abs(discriminant) <= epsilon)
            {
                double cube = Math.Cbrt(-q / 2);
                roots[count++] = 2 * cube - normalizedB / 3;
                roots[count++] = -cube - normalizedB / 3;
            }
            else
            {
                double delta = Math.Sqrt(Math.Max(0, q * q / 4 + p * p * p / 27));
                roots[count++] = Math.Cbrt(-q / 2 + delta) + Math.Cbrt(-q / 2 - delta) - normalizedB / 3;
            }
        }
        else if (Math.Abs(b) > epsilon)
        {
            double discriminant = c * c - 4 * b * d;
            if (discriminant >= 0)
            {
                double squareRoot = Math.Sqrt(discriminant);
                roots[count++] = (-c + squareRoot) / (2 * b);
                roots[count++] = (-c - squareRoot) / (2 * b);
            }
        }
        else if (Math.Abs(c) > epsilon) roots[count++] = -d / c;
        double first = double.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (roots[i] >= 0 && roots[i] <= 1 && roots[i] < first) first = roots[i];
        return double.IsPositiveInfinity(first) ? 0 : first;
    }
}

/// <summary>Channels are the original LCSK property identities: 0/1/2 position,
/// 6/7 scale, 10 alpha, and 41/42 the second sprite-part UV translation.</summary>
public sealed record PoemEventAnimationTrack(int NodeId, byte Channel, PoemEventCurve Curve);
public sealed record PoemEventAnimationValue(int NodeId, byte Channel, double Value);

public sealed record PoemEventAnimationClip(string Name, float StartFrame, float EndFrame,
    uint Flags, IReadOnlyList<int> NodeIds, IReadOnlyList<PoemEventAnimationTrack> Tracks)
{
    public const float SecondsPerFrame = 0.0166667f;
    public double DurationSeconds => (EndFrame - StartFrame) * (double)SecondsPerFrame;
    public float FrameAfter(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds)) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        float advance = (float)Math.Max(elapsedSeconds, 0) / SecondsPerFrame;
        return Math.Min(StartFrame + advance, EndFrame);
    }
    public IReadOnlyList<PoemEventAnimationValue> Sample(float frame) => Array.AsReadOnly(Tracks
        .Select(track => new PoemEventAnimationValue(track.NodeId, track.Channel, track.Curve.Sample(frame))).ToArray());
}

/// <summary>Finite kind2 clip / kind1 curve reader for poem-event layouts. It
/// consumes the normal repository's retained LCSK pools, not offline snapshots.</summary>
public static class PoemEventAnimationReader
{
    private static readonly HashSet<string> Names =
        ["loop", "fade_out", "09_ev_p_01_all", "09_ev_p_01_all_jian", "txt_scrolling"];

    public static IReadOnlyList<PoemEventAnimationClip> Read(LcskDocument layout)
    {
        var animations = Block(layout, 4, 2);
        var groups = Block(layout, 3, 2);
        var curves = Block(layout, 9, 1);
        var bindings = Block(layout, 2, 1);
        if (animations.RecordBytes.Length != animations.RecordCount * 44 ||
            groups.RecordBytes.Length != groups.RecordCount * 4 ||
            curves.RecordBytes.Length != curves.RecordCount * 32 ||
            bindings.RecordBytes.Length != bindings.RecordCount * 12)
            throw Missing("record_boundaries");
        var names = Pool(animations, 9);
        var linkedGroups = Pool(animations, 10);
        var groupNodes = Pool(groups, 8);
        var keys = Pool(curves, 3);
        var curveValues = new PoemEventCurve[curves.RecordCount];
        for (int index = 0; index < curveValues.Length; index++)
        {
            var row = curves.RecordBytes.AsSpan(index * 32, 32);
            int keyIndex = U16(row, 24);
            if ((uint)keyIndex >= (uint)keys.Entries.Count) throw Missing("curve_key_reference");
            byte[] bytes = keys.Entries[keyIndex].Data;
            if (bytes.Length % 64 != 0) throw Missing("key_boundaries");
            var converted = new PoemEventCurveKey[bytes.Length / 64];
            for (int k = 0; k < converted.Length; k++)
            {
                var key = bytes.AsSpan(k * 64, 64);
                converted[k] = new(F64(key, 0), F64(key, 8), F64(key, 16), F64(key, 24),
                    F64(key, 32), F64(key, 40), F64(key, 48), key[56]);
                if (!float.IsFinite(converted[k].Frame) || (k > 0 && converted[k].Frame < converted[k - 1].Frame))
                    throw Missing("key_frame_order");
            }
            curveValues[index] = new(F64(row, 0), F64(row, 8), F64(row, 16), Array.AsReadOnly(converted));
        }
        var tracks = new List<PoemEventAnimationTrack>();
        int nodeCount = layout.Blocks.Single(block => block.Tag == 1).RecordCount;
        for (int i = 0; i < bindings.RecordCount; i++)
        {
            var row = bindings.RecordBytes.AsSpan(i * 12, 12);
            int curve = BinaryPrimitives.ReadInt32LittleEndian(row);
            int node = U16(row, 4);
            if ((uint)curve >= (uint)curveValues.Length || node >= nodeCount) throw Missing("binding_reference");
            tracks.Add(new(node, row[7], curveValues[curve]));
        }
        var result = new List<PoemEventAnimationClip>();
        for (int i = 0; i < animations.RecordCount; i++)
        {
            var row = animations.RecordBytes.AsSpan(i * 44, 44);
            int nameIndex = U16(row, 0), groupIndex = U16(row, 2);
            if (nameIndex >= names.Entries.Count || groupIndex >= linkedGroups.Entries.Count) throw Missing("clip_reference");
            string name = Encoding.UTF8.GetString(names.Entries[nameIndex].Data);
            if (!Names.Contains(name)) continue;
            var nodes = new HashSet<int>();
            foreach (int group in Words(linkedGroups.Entries[groupIndex].Data))
            {
                if ((uint)group >= (uint)groupNodes.Entries.Count) throw Missing("group_reference");
                foreach (int node in Words(groupNodes.Entries[group].Data))
                {
                    if (node >= nodeCount) throw Missing("group_node_reference");
                    nodes.Add(node);
                }
            }
            float start = U16(row, 8), end = U16(row, 10);
            if (end < start) throw Missing("clip_frame_order");
            result.Add(new(name, start, end, BinaryPrimitives.ReadUInt32LittleEndian(row[4..]),
                Array.AsReadOnly(nodes.Order().ToArray()),
                tracks.Where(track => nodes.Contains(track.NodeId)).ToList().AsReadOnly()));
        }
        return result.AsReadOnly();
    }

    private static LcskBlock Block(LcskDocument layout, int tag, int kind) =>
        layout.Blocks.SingleOrDefault(block => block.Tag == tag && block.Kind == kind) ?? throw Missing("block_" + tag);
    private static LcskPool Pool(LcskBlock block, int type) =>
        block.Pools.SingleOrDefault(pool => pool.Type == type) ?? throw Missing("pool_" + type);
    private static ushort U16(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static float F64(ReadOnlySpan<byte> bytes, int at) => (float)BinaryPrimitives.ReadDoubleLittleEndian(bytes[at..]);
    private static IEnumerable<int> Words(byte[] bytes)
    {
        if (bytes.Length % 2 != 0) throw Missing("index_list");
        for (int i = 0; i < bytes.Length; i += 2) yield return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i, 2));
    }
    private static InvalidDataException Missing(string field) => new("poem-event.animation." + field);
}

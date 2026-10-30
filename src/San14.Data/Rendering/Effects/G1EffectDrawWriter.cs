using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>CPU generic submit, common depth buckets and dynamic writer. Its
/// input is actual initialized live storage; declaration templates are never
/// submitted as particles. Shader feature/bank and GPU ownership remain with
/// the render consumer.</summary>
public static partial class G1EffectDrawWriter
{
    private sealed record Queued(G1EffectParticleRenderSource Source, byte[] Payload,
        int Bucket, long Ordinal, int? Subdraw);

    private struct MeshSelectionState
    {
        public int DistanceIndex;
        public uint? Subdraw;
    }

    public static G1EffectDrawBatch Build(G1EffectDrawCamera camera,
        IEnumerable<G1EffectParticleRenderSource> sources, int bucketCount, int queueCapacity,
        int dynamicCapacity32, int rowCapacity)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(sources);
        if (bucketCount <= 0 || queueCapacity < 0 || dynamicCapacity32 < 0 || rowCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(bucketCount));
        var queue = new List<Queued>();
        var missing = new Dictionary<(int Offset, uint Type, string Reason), int>();
        int culled = 0, capacityRejected = 0;
        long ordinal = 0;
        void Missing(G1EffectParticleRenderSource source, string reason)
        {
            var key = (source.Declaration.Record.FileOffset, source.Declaration.Record.Type!.Value, reason);
            missing[key] = missing.GetValueOrDefault(key) + 1;
        }
        foreach (var source in sources)
        {
            uint type = source.Declaration.Record.Type!.Value;
            // Nested systems are traversed by their actual live owner. Type ten
            // genuinely has no render submission callback.
            if (type is 3 or 10) continue;
            var generic = source.CurrentGenericBytes;
            if (generic.Length < 0x22C)
            { Missing(source, "Current generic render fields are incomplete."); continue; }
            if (type is not (0 or 1 or 2 or 7))
            { Missing(source, $"Generic render type {type} is not implemented."); continue; }
            if ((type is 0 or 2) && (U(generic.Span, 8) & 0x200) != 0)
            { Missing(source, "GPU generic submission requires its actual GPU pool."); continue; }
            var meshSelection = new MeshSelectionState();
            foreach (var slot in source.Particles)
            {
                if (!slot.IsAllocated) throw new InvalidOperationException("Cannot draw a released particle slot.");
                var particle = slot.Bytes;
                if (particle.Length < 0x60)
                { Missing(source, "Live particle render payload is incomplete."); continue; }
                try
                {
                    bool admitted = Cull(type, particle.Span, generic.Span, camera, out float depth);
                    if (!admitted)
                    {
                        culled++;
                        if ((generic.Span[0x210] & 8) != 0)
                        {
                            if (source.RetireCulled is null)
                                Missing(source, "Culled-particle cleanup owner is absent.");
                            else source.RetireCulled(slot);
                        }
                        continue;
                    }
                    if ((particle.Span[6] & 2) == 0) continue;
                    if (source.ModuleOpacity < 1.1920929e-5f && HasOpacityController(generic.Span)) continue;
                    int? subdraw = type == 7 ? SelectSubdraw(particle.Span, generic.Span,
                        camera, source.Resources.Model.Model.Subdraws.Count, ref meshSelection) : null;
                    float normalized = ApproximateLog(depth) /
                        ApproximateLog(camera.FarPlane / camera.NearPlane) + 1f;
                    if (!(normalized >= 0 && normalized < 1)) { culled++; continue; }
                    if (queue.Count >= queueCapacity) { capacityRejected++; continue; }
                    queue.Add(new(source, particle[0x20..].ToArray(), (int)(bucketCount * normalized), ordinal++, subdraw));
                }
                catch (NotSupportedException error) { Missing(source, error.Message); }
            }
        }
        // AA6710 prepends into each bucket. AA59C0 visits buckets descending.
        queue.Sort((a, b) => a.Bucket != b.Bucket ? b.Bucket.CompareTo(a.Bucket) : b.Ordinal.CompareTo(a.Ordinal));
        var packets = new List<G1EffectDrawPacket>(queue.Count);
        using var stream = new MemoryStream();
        foreach (var entry in queue)
        foreach (var payload in ExpandSpecialFrameBlend(entry.Payload))
        {
            if (packets.Count >= rowCapacity) { capacityRejected++; continue; }
            try
            {
                byte[] data = WriteParticle(payload, camera, out int stride, out int count,
                    out int renderType, out uint header, out uint flags, out byte alpha);
                if (data.Length / 32 > dynamicCapacity32 - stream.Length / 32)
                { capacityRejected++; continue; }
                int offset = checked((int)stream.Length);
                int configuration = H(payload, 0) == 6 ? H(payload, 8) & 7 : H(payload, 8);
                int texture = H(payload, 0xC);
                if (!entry.Source.Resources.TextureSlots.ContainsKey(texture))
                    throw new InvalidDataException($"Effect texture slot {texture} is not declared.");
                stream.Write(data);
                packets.Add(new(entry.Source.Resources, entry.Source.Declaration.Record.FileOffset,
                    entry.Source.Declaration.Record.Type!.Value, configuration, renderType,
                    header, flags, texture, alpha, offset, stride, count, entry.Subdraw,
                    entry.Bucket, entry.Ordinal, payload) { Source = entry.Source });
            }
            catch (NotSupportedException error) { Missing(entry.Source, error.Message); }
        }
        return new(camera, stream.ToArray(), packets.AsReadOnly(), missing.Select(pair =>
            new G1EffectDrawMissing(pair.Key.Offset, pair.Key.Type, pair.Key.Reason, pair.Value)).ToArray(),
            culled, capacityRejected);
    }

    internal static bool HasOpacityController(ReadOnlySpan<byte> generic)
    {
        foreach (var (countAt, pointerAt) in new[] { (0x208, 0x20C), (0x214, 0x218) })
        {
            int count = checked((int)U(generic, countAt));
            int table = Relative(generic, pointerAt);
            Range(generic, table, checked(count * 4));
            for (int index = 0; index < count; index++)
            {
                int at = Relative(generic, table + index * 4);
                uint type = U(generic, at + 4);
                if (type is >= 34 and <= 37) return true;
            }
        }
        return false;
    }

    private static int SelectSubdraw(ReadOnlySpan<byte> particle, ReadOnlySpan<byte> generic,
        G1EffectDrawCamera camera, int modelCount, ref MeshSelectionState state)
    {
        Range(generic, 0x240, 16);
        int mode = H(generic, 0x240), count = H(generic, 0x242);
        if (count == 0) throw new NotSupportedException("Mesh generic has no current subdraw selection.");
        int table = Relative(generic, 0x248);
        Range(generic, table, count * 4);
        state.Subdraw ??= U(generic, table);
        if (mode == 1)
        {
            Range(particle, 0x9C, 4);
            uint selected = U(particle, 0x9C);
            if (selected >= count) throw new InvalidDataException("Particle mesh selector leaves its table.");
            state.Subdraw = U(generic, table + (int)selected * 4);
        }
        else if (mode == 0)
        {
            if (count > 1)
            {
                int keys = Relative(generic, 0x244);
                Range(generic, keys, count * 4);
                float distance = Transform(V(particle, 0x80), camera.ClipMatrix).W * camera.FarPlane;
                int selected = state.DistanceIndex;
                // The source carries its distance cursor across live particles,
                // searches backwards first, then advances through equal keys.
                while (selected > 0 && !(F(generic, keys + (selected - 1) * 4) <= distance)) selected--;
                while (selected < count - 1 && F(generic, keys + selected * 4) <= distance) selected++;
                state.DistanceIndex = selected;
                state.Subdraw = U(generic, table + selected * 4);
            }
        }
        else if (mode is >= 2 and <= 5)
        {
            int keys = Relative(generic, 0x244);
            Range(generic, keys, count * 4);
            float age = F(particle, unchecked((int)U(generic, 0x2C)));
            if (mode >= 4) age /= F(particle, unchecked((int)U(generic, 0x30)));
            float phase = (mode & 1) != 0 ? F(particle, 0x9C) : 0f;
            // C# remainder retains the dividend's sign, as does the source
            // fmodf. Do not clamp normalized age or turn this into floor-modulo.
            float position = (age + phase) % F(generic, keys + (count - 1) * 4);
            for (int selected = 0; selected < count; selected++)
            {
                if (!(position < F(generic, keys + selected * 4))) continue;
                state.Subdraw = U(generic, table + selected * 4);
                break;
            }
            // If no strict comparison succeeds, the source retains the last
            // submitted model (initially table[0]); it does not clamp to the end.
        }
        else throw new NotSupportedException($"Mesh selector mode {mode} is not implemented.");
        uint subdraw = state.Subdraw.Value;
        if (subdraw >= modelCount) throw new InvalidDataException("Particle mesh subdraw leaves its model.");
        return (int)subdraw;
    }

    private static bool Cull(uint type, ReadOnlySpan<byte> particle, ReadOnlySpan<byte> generic,
        G1EffectDrawCamera camera, out float depth)
    {
        Range(particle, 0, type == 7 ? 0xA0 : type == 2 ? 0x90 : 0x60);
        float bias = camera.SortBiasScale * F(generic, 0x228);
        if (type is 0 or 1)
        {
            Vector4 position = Transform(V(particle, 0x50), camera.ClipMatrix);
            float w = MathF.Abs(position.W);
            float width = F(particle, 0x34), height = F(particle, 0x38);
            float x = type == 0 ? MathF.Abs(camera.Projection.M11 * 0.5f * width)
                : (width + height) * MathF.Abs(camera.Projection.M11 * 0.5f);
            float y = type == 0 ? MathF.Abs(camera.Projection.M22 * 0.5f * height)
                : (width + height) * MathF.Abs(camera.Projection.M22 * 0.5f);
            depth = w + bias;
            return -w < position.Z && position.Z < w && -w < position.X + x &&
                position.X - x < w && -w < position.Y + y && position.Y - y < w;
        }
        Vector4 center, radii;
        if (type == 2)
        {
            Vector4 x = Transform(V(particle, 0x50) * (F(particle, 0x34) * 0.5f), camera.ClipMatrix);
            Vector4 y = Transform(V(particle, 0x60), camera.ClipMatrix);
            radii = Abs(x) + Abs(y) * (F(particle, 0x38) * 0.5f);
            center = Transform(V(particle, 0x80), camera.ClipMatrix);
        }
        else
        {
            Range(generic, 0x230, 12);
            Matrix4x4 clip = Multiply(Matrix(particle, 0x50), camera.ClipMatrix);
            radii = Abs(Row(clip, 0)) * (F(particle, 0x34) * F(generic, 0x230)) +
                Abs(Row(clip, 1)) * (F(particle, 0x38) * F(generic, 0x234)) +
                Abs(Row(clip, 2)) * (F(particle, 0x3C) * F(generic, 0x238));
            center = Row(clip, 3);
        }
        float maximumW = MathF.Abs(center.W) + radii.W;
        depth = MathF.Min(maximumW, 0.9999881f) + bias;
        float bx = radii.X + maximumW, by = radii.Y + maximumW;
        return -MathF.Abs(center.W - radii.W) < center.Z + radii.Z &&
            center.Z - radii.Z < maximumW && -bx < center.X && center.X < bx &&
            -by < center.Y && center.Y < by;
    }

    private static float ApproximateLog(float value)
    {
        if (value <= 0) return -128;
        int exponent = Math.ILogB(value) + 1;
        float fraction = (float)Math.ScaleB(value, -exponent);
        return (fraction + fraction) + exponent - 2f;
    }

    private static Vector4 Abs(Vector4 v) => new(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z), MathF.Abs(v.W));
    private static Vector4 Transform(Vector4 v, Matrix4x4 m) => new(
        ((v.Y * m.M21 + v.X * m.M11) + v.Z * m.M31) + v.W * m.M41,
        ((v.Y * m.M22 + v.X * m.M12) + v.Z * m.M32) + v.W * m.M42,
        ((v.Y * m.M23 + v.X * m.M13) + v.Z * m.M33) + v.W * m.M43,
        ((v.Y * m.M24 + v.X * m.M14) + v.Z * m.M34) + v.W * m.M44);
    private static Matrix4x4 Multiply(Matrix4x4 a, Matrix4x4 b)
    {
        Vector4 R(Vector4 r) => (r.Z * Row(b, 2) + r.X * Row(b, 0)) + (r.W * Row(b, 3) + r.Y * Row(b, 1));
        return Matrix(R(Row(a, 0)), R(Row(a, 1)), R(Row(a, 2)), R(Row(a, 3)));
    }
    private static Vector4 Row(Matrix4x4 m, int row) => row switch
    {
        0 => new(m.M11, m.M12, m.M13, m.M14), 1 => new(m.M21, m.M22, m.M23, m.M24),
        2 => new(m.M31, m.M32, m.M33, m.M34), _ => new(m.M41, m.M42, m.M43, m.M44)
    };
    private static Matrix4x4 Matrix(Vector4 x, Vector4 y, Vector4 z, Vector4 w) => new(
        x.X, x.Y, x.Z, x.W, y.X, y.Y, y.Z, y.W, z.X, z.Y, z.Z, z.W, w.X, w.Y, w.Z, w.W);
    private static Matrix4x4 Matrix(ReadOnlySpan<byte> b, int at) => Matrix(V(b, at), V(b, at + 16), V(b, at + 32), V(b, at + 48));
    private static Vector4 V(ReadOnlySpan<byte> b, int at) => new(F(b, at), F(b, at + 4), F(b, at + 8), F(b, at + 12));
    private static ushort H(ReadOnlySpan<byte> b, int at) { Range(b, at, 2); return BinaryPrimitives.ReadUInt16LittleEndian(b[at..]); }
    private static uint U(ReadOnlySpan<byte> b, int at) { Range(b, at, 4); return BinaryPrimitives.ReadUInt32LittleEndian(b[at..]); }
    private static float F(ReadOnlySpan<byte> b, int at) { Range(b, at, 4); return BinaryPrimitives.ReadSingleLittleEndian(b[at..]); }
    private static int Relative(ReadOnlySpan<byte> b, int at)
    { Range(b, at, 4); return checked(at + BinaryPrimitives.ReadInt32LittleEndian(b[at..])); }
    private static void Range(ReadOnlySpan<byte> b, int at, int length)
    {
        if (at < 0 || length < 0 || at > b.Length - length)
            throw new InvalidDataException("Live effect render field escapes its current owner.");
    }
}

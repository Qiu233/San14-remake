using System.Buffers.Binary;
using System.Text;

namespace San14.Data;

/// <summary>A direct device allocation may retain its current selected normal-resource
/// material frame. Its draw reads the GPU expansion output and GPU draw arguments.</summary>
public interface IG1EffectDirectDrawAllocation : IG1EffectDirectComputeAllocation
{
    void SetDraw(G1EffectGpuPublication? publication, long submissionOrdinal);
}

/// <summary>Owned, bounded packages for the actual direct-compute device consumer.
/// These contain project shader source and current owner data, never executable snapshots.</summary>
public static class G1EffectDirectPublication
{
    public static byte[] EncodePackage(G1EffectDirectComputePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        uint genericType = package.Declaration.Record.Type ??
            throw new InvalidDataException("Direct compute package requires its generic type.");
        if (package.MissingKernels.Count != 0)
            throw new NotSupportedException("Direct compute kernels are missing: " + string.Join(", ", package.MissingKernels));
        using var b = new Blob();
        int header = b.Reserve(48), buffers = b.Reserve(checked(package.Buffers.Count * 32));
        var programs = package.Programs.ToArray();
        int shaders = b.Reserve(checked(programs.Length * 28));
        uint required = 0;
        foreach (var kernel in package.RequiredKernels) required |= 1u << (int)kernel;
        b.Words(header, 0x31444543, 1, package.Buffers.Count, buffers, programs.Length, shaders,
            required, genericType, package.VertexStride,
            package.IndirectByteOffset, G1EffectDirectComputePackage.VertexBuffer, G1EffectDirectComputePackage.ParticleBuffer);
        for (int i = 0; i < package.Buffers.Count; i++)
        {
            var value = package.Buffers[i]; int bytes = b.Append(value.InitialBytes.Span);
            b.Words(buffers + i * 32, value.Id, value.ByteLength, (uint)value.Usage,
                (uint)value.Format, value.StructureStride, bytes, value.InitialBytes.Length, 0);
        }
        for (int i = 0; i < programs.Length; i++)
        {
            var value = programs[i];
            byte[] source = Encoding.UTF8.GetBytes(value.Source), entry = Encoding.UTF8.GetBytes(value.EntryPoint),
                profile = Encoding.UTF8.GetBytes(value.Profile);
            b.Words(shaders + i * 28, (uint)value.Kernel, b.Append(source), source.Length,
                b.Append(entry), entry.Length, b.Append(profile), profile.Length);
        }
        return b.ToArray();
    }

    public static byte[] EncodeCommands(G1EffectDirectCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        using var b = new Blob();
        int header = b.Reserve(32), uploads = b.Reserve(checked(commands.Uploads.Count * 16)),
            dispatches = b.Reserve(checked(commands.Dispatches.Count * 48));
        b.Words(header, 0x31434543, 1, commands.Uploads.Count, uploads,
            commands.Dispatches.Count, dispatches, 0, 0);
        for (int i = 0; i < commands.Uploads.Count; i++)
        {
            var value = commands.Uploads[i]; int bytes = b.Append(value.Bytes.Span);
            b.Words(uploads + i * 16, value.BufferId, value.ByteOffset, bytes, value.Bytes.Length);
        }
        for (int i = 0; i < commands.Dispatches.Count; i++)
        {
            var value = commands.Dispatches[i];
            var constants = new List<G1EffectDirectConstantBinding>();
            if (!value.Constants.IsEmpty) constants.Add(new(0, value.Constants));
            if (value.AdditionalConstants is { } additional) constants.AddRange(additional);
            int cb = b.Reserve(checked(constants.Count * 12));
            int srvs = b.Reserve(checked(value.ShaderResources.Count * 8));
            int uavs = b.Reserve(checked(value.UnorderedAccess.Count * 8));
            for (int j = 0; j < constants.Count; j++)
            { var c = constants[j]; int bytes = b.Append(c.Bytes.Span, 16); b.Words(cb + j * 12, c.Slot, bytes, c.Bytes.Length); }
            for (int j = 0; j < value.ShaderResources.Count; j++)
            { var r = value.ShaderResources[j]; b.Words(srvs + j * 8, r.Slot, r.BufferId); }
            for (int j = 0; j < value.UnorderedAccess.Count; j++)
            { var r = value.UnorderedAccess[j]; b.Words(uavs + j * 8, r.Slot, r.BufferId); }
            b.Words(dispatches + i * 48, (uint)value.Kernel, value.GroupsX, value.GroupsY, value.GroupsZ,
                constants.Count, cb, value.ShaderResources.Count, srvs, value.UnorderedAccess.Count, uavs, 0, 0);
        }
        return b.ToArray();
    }

    private sealed class Blob : IDisposable
    {
        private readonly MemoryStream stream = new();
        internal int Reserve(int length, int alignment = 4)
        {
            int at = checked((int)((stream.Length + alignment - 1) & ~(alignment - 1L)));
            stream.SetLength(checked(at + length)); return at;
        }
        internal int Append(ReadOnlySpan<byte> bytes, int alignment = 4)
        { int at = Reserve(bytes.Length, alignment); bytes.CopyTo(stream.GetBuffer().AsSpan(at, bytes.Length)); return at; }
        internal void Words(int at, params long[] values)
        { foreach (long value in values) { BinaryPrimitives.WriteUInt32LittleEndian(stream.GetBuffer().AsSpan(at, 4), checked((uint)value)); at += 4; } }
        internal byte[] ToArray() => stream.ToArray();
        public void Dispose() => stream.Dispose();
    }
}

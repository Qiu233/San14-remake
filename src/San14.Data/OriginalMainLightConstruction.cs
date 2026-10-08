using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalMainLightConstructionWriteRange(int Offset, int Length,
    string Writer, uint? SourcePropertyHash = null);

/// <summary>An explicitly resolved remake Data owner, or the loader's explicit
/// null result. DataHandle is an opaque remake handle; it is never dereferenced
/// and does not claim an original process address.</summary>
public sealed record OriginalMainLightConstructionResourceBinding(uint SourceObjectKey,
    ulong DataHandle, string OwnerProvenance, bool CpuDataReady, uint InterfaceClassId);

public sealed record OriginalMainLightConstructionInputs(uint ContextFlags3E4,
    string RetainedTemplateProvenance,
    IReadOnlyList<OriginalMainLightConstructionResourceBinding> ResourceBindings);

public sealed class OriginalMainLightConstructionCpuField
{
    private readonly byte[] bytes;
    internal OriginalMainLightConstructionCpuField(int offset, uint hash, string name, byte[] value)
    { Offset = offset; SourcePropertyHash = hash; Name = name; bytes = value.ToArray(); }
    public int Offset { get; }
    public uint SourcePropertyHash { get; }
    public string Name { get; }
    public ReadOnlyMemory<byte> Bytes => bytes;
}

/// <summary>The complete class66 loader seed. It retains the native initializer's
/// unwritten bytes from the caller's explicit template. Camera, current modifier,
/// selected View and final GPU packet preparation happen after this construction.</summary>
public sealed class OriginalMainLightConstructionPacket
{
    private readonly byte[] bytes;
    internal OriginalMainLightConstructionPacket(LoadedOriginalMainLightBindingSource source,
        byte[] value, string provenance, IReadOnlyList<OriginalMainLightConstructionWriteRange> writes,
        IReadOnlyList<OriginalMainLightConstructionCpuField> cpuFields,
        IReadOnlyList<OriginalMainLightConstructionResourceBinding> resources, uint flags)
    {
        Source = source; bytes = value.ToArray(); SourceProvenance = provenance;
        WrittenRanges = Array.AsReadOnly(writes.ToArray()); CpuSourceFields = cpuFields;
        ResourceBindings = Array.AsReadOnly(resources.ToArray()); ContextFlags3E4 = flags;
    }
    public LoadedOriginalMainLightBindingSource Source { get; }
    public ReadOnlyMemory<byte> Bytes => bytes;
    public string SourceProvenance { get; }
    public IReadOnlyList<OriginalMainLightConstructionWriteRange> WrittenRanges { get; }
    public IReadOnlyList<OriginalMainLightConstructionCpuField> CpuSourceFields { get; }
    public IReadOnlyList<OriginalMainLightConstructionResourceBinding> ResourceBindings { get; }
    public uint ContextFlags3E4 { get; }
    public bool AllResourceBindingsReady => ResourceBindings.All(value => value.DataHandle != 0 && value.CpuDataReady);
    public bool CurrentModifiersApplied => false;
}

/// <summary>CDBEC0 + exact CBD460 class66 declaration + CBA530 classifier +
/// CB98F0/CBC6D0 prepare prefix. Only original43AE6F41:9E08F950 is supported.
/// All106 fields are validated, including the13 explicitly empty arrays.
/// Resource lookup results and retained bytes are explicit caller inputs.</summary>
public static partial class OriginalMainLightConstruction
{
    public const int LightDataBytes = 0x10C0;
    public const uint EnvmapInterfaceClassId = 0xB672FAA2;
    private static readonly uint[] RequiredResourceKeys =
        [0x5A53AD58, 0x80F9DB7B, 0x7F97260C, 0x54F8C0DE, 0x7D85DA95];

    public static OriginalMainLightConstructionPacket Apply(LoadedOriginalMainLightBindingSource source,
        ReadOnlySpan<byte> explicitComplete10C0, OriginalMainLightConstructionInputs input)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(input);
        if (explicitComplete10C0.Length != LightDataBytes)
            throw new InvalidDataException("Main-light construction requires the complete explicit10C0 retained template.");
        if (string.IsNullOrWhiteSpace(input.RetainedTemplateProvenance))
            throw new InvalidDataException("Main-light construction requires provenance for its retained template.");
        LoadedOriginalMainLightBindingSource identity = OriginalMainLightBindingSource.ReadGameDefault(source.Resource);
        if (source.Declaration.ObjectId != identity.Declaration.ObjectId ||
            source.Declaration.ClassId != identity.Declaration.ClassId ||
            source.GroupMask != identity.GroupMask || source.GroupMaskMain != identity.GroupMaskMain)
            throw new InvalidDataException("Main-light construction source and declaration identity disagree.");
        byte[][] values = ValidateSource(source.Declaration);
        OriginalMainLightConstructionResourceBinding[] resources = ValidateResources(input.ResourceBindings);
        byte[] bytes = explicitComplete10C0.ToArray();
        var writes = new List<OriginalMainLightConstructionWriteRange>();
        ApplyLoaderPrewrites(bytes, writes);
        foreach (DefaultRun run in NativeDefaults)
            Copy(bytes, run.Offset, Convert.FromHexString(run.Hex), writes, "CDBEC0/CDBD10");
        CopySourceFields(bytes, values, writes);
        for (int row = 0; row < 4; row++)
        {
            // The strictly validated actual source quaternion is exactly(0,1,0,0).
            // Its binary32 norm/inverse is exactly1, so its normalized bits copy.
            Copy(bytes, 0xA0 + row * 16, values[row * 2], writes, "CBD460:normalized-quaternion", SourceFields[row * 2].Hash);
            Write(bytes, row * 32, OriginalDirectionalLightSource.PrepareDirection(ReadFour(values[row * 2])),
                writes, "CBD460/CB98F0:direction", SourceFields[row * 2].Hash);
        }
        ApplyLoaderConstants(bytes, writes);
        Put(bytes, 0x1F4, 0xFFF3FFFF, writes, "CBA530:exact-source-classifier");
        Put(bytes, 0x1F8, 0x000001F6, writes, "CBA530:exact-source-classifier");
        foreach ((int offset, uint key) in new (int, uint)[]
            { (0xFE0, 0x5A53AD58), (0xFE8, 0x80F9DB7B), (0x1030, 0x7F97260C), (0x1050, 0x54F8C0DE) })
            Copy(bytes, offset, Handle(resources.Single(value => value.SourceObjectKey == key).DataHandle),
                writes, $"CBD460:explicit-resource:{key:X8}");
        Write(bytes, 0x120, OriginalDirectionalLightSource.RotatePreparedQuaternion(ReadFour(bytes.AsSpan(0xF0, 16)), true),
            writes, "CB98F0/CBC6D0:hemi-direction");
        OriginalMainLightConstructionResourceBinding lut = resources.Single(value => value.SourceObjectKey == 0x7D85DA95);
        string provenance = $"{source.Resource.Source.ResourceUri}#{source.Declaration.ObjectId:X8}@{source.Resource.Source.DecodedSha256};" +
            $"class=66ECB240;constructor=CDBEC0;loader=CBD460;classifier=CBA530;prepare=CB98F0;" +
            $"retained-template={input.RetainedTemplateProvenance}@{Convert.ToHexString(SHA256.HashData(explicitComplete10C0))};" +
            $"context={input.ContextFlags3E4:X8};extensions=0202020000020000;current-modifiers=none;" +
            string.Join(';', resources.Select(value => $"resource={value.SourceObjectKey:X8}:{value.DataHandle:X16}:{value.OwnerProvenance}"));
        return new(source, bytes, provenance, writes, CpuSourceFields(values, lut), resources, input.ContextFlags3E4);
    }

    private static byte[][] ValidateSource(DokObject declaration)
    {
        if (declaration.Properties.Count != SourceFields.Length)
            throw new InvalidDataException("Main-light construction requires the exact106-property source declaration.");
        var result = new byte[SourceFields.Length][];
        for (int index = 0; index < SourceFields.Length; index++)
        {
            SourceField expected = SourceFields[index];
            DokProperty[] matches = declaration.Properties.Where(value => value.NameHash == expected.Hash).ToArray();
            byte[] raw = Convert.FromHexString(expected.Hex);
            if (matches.Length != 1 || matches[0].TypeCode != expected.Type ||
                matches[0].ElementCount != expected.Count || !matches[0].RawValue.AsSpan().SequenceEqual(raw))
                throw new InvalidDataException($"Main-light construction requires the exact typed source field{expected.Name}/{expected.Hash:X8}.");
            result[index] = matches[0].RawValue.ToArray();
        }
        return result;
    }

    private static OriginalMainLightConstructionResourceBinding[] ValidateResources(
        IReadOnlyList<OriginalMainLightConstructionResourceBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Count != RequiredResourceKeys.Length || bindings.Any(value => value is null) ||
            bindings.Select(value => value.SourceObjectKey).Distinct().Count() != RequiredResourceKeys.Length ||
            !RequiredResourceKeys.All(key => bindings.Any(value => value.SourceObjectKey == key)))
            throw new InvalidDataException("Main-light construction requires explicit results for the exact five nonzero resource keys.");
        foreach (OriginalMainLightConstructionResourceBinding value in bindings)
        {
            if (string.IsNullOrWhiteSpace(value.OwnerProvenance) ||
                (value.DataHandle != 0 && (!value.CpuDataReady || value.InterfaceClassId != EnvmapInterfaceClassId)) ||
                (value.DataHandle == 0 && value.CpuDataReady))
                throw new InvalidDataException($"Main-light resource{value.SourceObjectKey:X8} requires explicit null or a ready B672FAA2 Data owner and provenance.");
        }
        return bindings.OrderBy(value => Array.IndexOf(RequiredResourceKeys, value.SourceObjectKey)).ToArray();
    }

    private static byte[] Handle(ulong value)
    { byte[] result = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(result, value); return result; }
    private static Vector4 ReadFour(ReadOnlySpan<byte> bytes) => new(
        BinaryPrimitives.ReadSingleLittleEndian(bytes), BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(4)),
        BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(8)), BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(12)));
    private static void Copy(byte[] bytes, int offset, byte[] value,
        List<OriginalMainLightConstructionWriteRange> writes, string writer, uint? hash = null)
    { value.CopyTo(bytes, offset); writes.Add(new(offset, value.Length, writer, hash)); }
    private static void Put(byte[] bytes, int offset, uint value,
        List<OriginalMainLightConstructionWriteRange> writes, string writer, uint? hash = null)
    { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value); writes.Add(new(offset, 4, writer, hash)); }
    private static void Write(byte[] bytes, int offset, Vector4 value,
        List<OriginalMainLightConstructionWriteRange> writes, string writer, uint? hash = null)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, 4), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4, 4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8, 4), value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 12, 4), value.W);
        writes.Add(new(offset, 16, writer, hash));
    }
}

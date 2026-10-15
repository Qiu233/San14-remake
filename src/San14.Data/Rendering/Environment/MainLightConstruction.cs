using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>An explicitly resolved remake Data owner, or the loader's explicit
/// null result. DataHandle is an opaque remake handle; it is never dereferenced
/// and does not claim an original process address.</summary>
public sealed record MainLightConstructionResourceBinding(uint SourceObjectKey,
    ulong DataHandle, string OwnerProvenance, bool CpuDataReady, uint InterfaceClassId);

public sealed record MainLightConstructionInputs(uint ContextFlags3E4,
    string RetainedTemplateProvenance,
    IReadOnlyList<MainLightConstructionResourceBinding> ResourceBindings);

/// <summary>The complete class66 loader seed. It retains the native initializer's
/// unwritten bytes from the caller's explicit template. Camera, current modifier,
/// selected View and final GPU packet preparation happen after this construction.</summary>
public sealed class MainLightConstructionPacket
{
    private readonly byte[] bytes;
    // Apply transfers its new buffers here after completing all writes and lookups.
    internal MainLightConstructionPacket(LoadedMainLightBindingSource source,
        byte[] ownedBytes, string provenance,
        MainLightConstructionResourceBinding[] ownedResources, uint flags,
        MapSourceCreatedLightingRetainedAllocation? retainedAllocation)
    {
        Source = source; bytes = ownedBytes; SourceProvenance = provenance;
        ResourceBindings = Array.AsReadOnly(ownedResources); ContextFlags3E4 = flags;
        AllResourceBindingsReady = ownedResources.All(value => value.DataHandle != 0 && value.CpuDataReady);
        RetainedAllocation = retainedAllocation;
    }
    public LoadedMainLightBindingSource Source { get; }
    public ReadOnlyMemory<byte> Bytes => bytes;
    public string SourceProvenance { get; }
    public IReadOnlyList<MainLightConstructionResourceBinding> ResourceBindings { get; }
    public uint ContextFlags3E4 { get; }
    internal MapSourceCreatedLightingRetainedAllocation? RetainedAllocation { get; }
    public bool AllResourceBindingsReady { get; }
    public bool CurrentModifiersApplied => false;
}

/// <summary>Encodes imported main-light controls into the transitional light-data layout.
/// Resource lookup results and retained bytes are explicit caller inputs.</summary>
public static partial class MainLightConstruction
{
    public const int LightDataBytes = 0x10C0;
    public const uint EnvmapInterfaceClassId = 0xB672FAA2;

    public static MainLightConstructionPacket Apply(LoadedMainLightBindingSource source,
        ReadOnlySpan<byte> explicitComplete10C0, MainLightConstructionInputs input)
        => Apply(source, explicitComplete10C0, input, null);

    internal static MainLightConstructionPacket Apply(LoadedMainLightBindingSource source,
        ReadOnlySpan<byte> explicitComplete10C0, MainLightConstructionInputs input,
        MapSourceCreatedLightingRetainedAllocation? retainedAllocation)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(input);
        if (explicitComplete10C0.Length != LightDataBytes)
            throw new InvalidDataException("Main-light construction requires the complete explicit10C0 retained template.");
        if (string.IsNullOrWhiteSpace(input.RetainedTemplateProvenance))
            throw new InvalidDataException("Main-light construction requires provenance for its retained template.");
        MainLightSettings settings = source.Settings;
        MainLightConstructionResourceBinding[] resources = ValidateResources(source, input.ResourceBindings);
        byte[] bytes = new byte[LightDataBytes];
        foreach (var extent in MapSourceCreatedLightingRetainedSource.Layout)
            explicitComplete10C0.Slice(extent.Offset, extent.Length).CopyTo(bytes.AsSpan(extent.Offset, extent.Length));
        WriteDefaultModifiers(bytes);
        CopySourceFields(bytes, settings);
        for (int row = 0; row < 4; row++)
        {
            MainDirectionalLightSettings light = settings.DirectionalLights[row];
            Vector4 quaternion = DirectionalLightSource.NormalizeQuaternion(light.WorldQuaternion);
            Write(bytes, 0xA0 + row * 16, quaternion);
            Write(bytes, row * 32, DirectionalLightSource.RotatePreparedQuaternion(quaternion));
        }
        CopyPreparedCelestialLights(bytes);
        LightModifierClassification.Apply(bytes, input.ContextFlags3E4);
        MainLightResourceReferences references = settings.Resources;
        foreach ((int offset, uint key) in new (int, uint)[]
        {
            (0xFE0, references.SpecularMap), (0xFE8, 0x80F9DB7B),
            (0xFF0, references.DiffuseMap), (0xFF8, references.IndexedRamp),
            (0x1000, references.HairHighlight), (0x1010, references.OutlineColor),
            (0x1030, 0x7F97260C), (0x1050, 0x54F8C0DE), (0x1070, references.EyeTwinkle)
        })
        {
            if (key == 0) continue; // All resource slots have already been initialized to null.
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, 8),
                resources.Single(value => value.SourceObjectKey == key).DataHandle);
        }
        Write(bytes, 0x120, DirectionalLightSource.RotatePreparedQuaternion(ReadFour(bytes.AsSpan(0xF0, 16)), true));
        string provenance = $"{source.Resource.Source.ResourceUri}#{source.Declaration.ObjectId:X8}@{source.Resource.Source.DecodedSha256};" +
            $"class=66ECB240;constructor=CDBEC0;loader=CBD460;classifier=CBA530;prepare=CB98F0;" +
            $"retained-template={input.RetainedTemplateProvenance}@{Convert.ToHexString(SHA256.HashData(explicitComplete10C0))};" +
            $"context={input.ContextFlags3E4:X8};extensions={Convert.ToHexString(settings.Extensions.Span)};current-modifiers=none;" +
            string.Join(';', resources.Select(value => $"resource={value.SourceObjectKey:X8}:{value.DataHandle:X16}:{value.OwnerProvenance}"));
        return new(source, bytes, provenance, resources, input.ContextFlags3E4, retainedAllocation);
    }

    private static MainLightConstructionResourceBinding[] ValidateResources(
        LoadedMainLightBindingSource source, IReadOnlyList<MainLightConstructionResourceBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Any(value => value is null || value.SourceObjectKey == 0) ||
            bindings.Select(value => value.SourceObjectKey).Distinct().Count() != bindings.Count)
            throw new InvalidDataException("Main-light construction requires unique nonzero resource lookup keys.");
        var supplied = bindings.ToDictionary(value => value.SourceObjectKey);
        var result = new List<MainLightConstructionResourceBinding>();
        foreach (uint key in source.RequiredResourceKeys)
        {
            if (!supplied.TryGetValue(key, out MainLightConstructionResourceBinding? value))
                throw new InvalidDataException($"Main-light resource {key:X8} has no explicit lookup result.");
            if (string.IsNullOrWhiteSpace(value.OwnerProvenance) ||
                (value.DataHandle != 0 && (!value.CpuDataReady || value.InterfaceClassId != EnvmapInterfaceClassId)) ||
                (value.DataHandle == 0 && value.CpuDataReady))
                throw new InvalidDataException($"Main-light resource {key:X8} requires explicit null or a ready B672FAA2 Data owner and provenance.");
            result.Add(value);
        }
        return result.ToArray();
    }

    private static Vector4 ReadFour(ReadOnlySpan<byte> bytes) => new(
        BinaryPrimitives.ReadSingleLittleEndian(bytes), BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(4)),
        BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(8)), BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(12)));
    private static void Copy(byte[] bytes, int offset, ReadOnlySpan<byte> value)
        => value.CopyTo(bytes.AsSpan(offset));
    private static void Put(byte[] bytes, int offset, uint value)
        => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static void Write(byte[] bytes, int offset, float value)
        => BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, 4), value);
    private static void Write(byte[] bytes, int offset, Vector3 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, 4), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4, 4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8, 4), value.Z);
    }
    private static void Write(byte[] bytes, int offset, Vector4 value)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset, 4), value.X);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 4, 4), value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 8, 4), value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset + 12, 4), value.W);
    }
}

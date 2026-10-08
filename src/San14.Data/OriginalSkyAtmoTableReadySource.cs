using System.Numerics;

namespace San14.Data;

public enum OriginalSkyAtmoReadyResult { Waiting, Ready, Failed }

/// <summary>Actual Static Data and its CPU.v28 texture descriptor. The
/// implementation owns the prepared source image; source bytes alone are
/// insufficient. Ready is the result of preparing that current Data.</summary>
public interface IOriginalSkyAtmoCurrentStaticTextureData
{
    uint ObjectKey { get; }
    uint FactoryClassId { get; }
    object? CurrentCpuOwner { get; }
    object? CurrentTextureDescriptor { get; }
    OriginalSkyAtmoReadyResult PrepareCurrent();
    void RequireCurrent();
}

/// <summary>Textures Data with the actual four source slots and CPU20.
/// A loaded texture table or a three-consumer Cloud binding is not this Data.</summary>
public sealed class OriginalSkyAtmoPlaneDataOwner
{
    private readonly IOriginalSkyAtmoCurrentStaticTextureData[] data;
    private readonly object?[] descriptors;
    private readonly IReadOnlyList<object?> descriptorView;
    private bool retained = true;
    internal OriginalSkyAtmoPlaneDataOwner(LoadedOriginalSkyAtmoTableReadySource source,
        IReadOnlyList<IOriginalSkyAtmoCurrentStaticTextureData> actualData)
    {
        Source = source;
        if (actualData.Count != 4) throw new InvalidDataException("Plane STA requires all four actual Static Data owners.");
        data = actualData.ToArray(); descriptors = new object?[4]; descriptorView = Array.AsReadOnly(descriptors);
        for (int i = 0; i < data.Length; i++)
            if (data[i].ObjectKey != source.Plane.Entries[i].Definition.ObjectId || data[i].FactoryClassId != 0xFF7DBFD4)
                throw new InvalidDataException("Plane STA current Static Data does not follow its complete literal CSV order.");
    }
    public LoadedOriginalSkyAtmoTableReadySource Source { get; }
    public uint ObjectKey => 0x98026BD5;
    public uint FactoryClassId => 0x3059B9C3;
    /// <summary>The actual owned Textures CPU exists before its ready stages.
    /// Its descriptor entries are published only by the stage1 loop.</summary>
    public object CurrentCpuOwner => this;
    public int PreparationStage { get; private set; }
    public IReadOnlyList<object?> CurrentTextureDescriptors => descriptorView;
    public string SourceEvidence => Source.SourceEvidence + ";Textures-C104B0/C15440;CPU20/CPU8-type20-count4;all-four-Static-014C60/v28";
    public OriginalSkyAtmoReadyResult PrepareCurrent()
    {
        ObjectDisposedException.ThrowIf(!retained, this);
        // The accepted CSV and actual ordered Data array have already been
        // retained at this source load boundary. Stage0 constructs the typed
        // four-entry group; it does not certify any texture entry.
        if (PreparationStage == 0) PreparationStage = 1;
        if (PreparationStage != 1) return PreparationStage == 2 ? OriginalSkyAtmoReadyResult.Ready : OriginalSkyAtmoReadyResult.Waiting;
        foreach (var entry in data)
        {
            var result = entry.PrepareCurrent();
            if (result != OriginalSkyAtmoReadyResult.Ready) return result;
        }
        // Native stage1 first prepares every Data, then publishes each CPU.v28
        // result in order. A late missing CPU leaves the earlier stores intact.
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].CurrentCpuOwner is null) return OriginalSkyAtmoReadyResult.Failed;
            data[i].RequireCurrent();
            descriptors[i] = data[i].CurrentTextureDescriptor;
            if (descriptors[i] is null)
                throw new NotSupportedException("Actual Plane Static CPU.v28 has no supported current texture descriptor.");
        }
        PreparationStage = 2;
        return OriginalSkyAtmoReadyResult.Ready;
    }
    public void RequireCurrent()
    {
        ObjectDisposedException.ThrowIf(!retained, this);
        if (PreparationStage != 2) throw new InvalidOperationException("Actual Plane STA Data has not completed both source ready stages.");
        for (int i = 0; i < data.Length; i++)
        {
            data[i].RequireCurrent();
            if (data[i].CurrentCpuOwner is null || !ReferenceEquals(data[i].CurrentTextureDescriptor, descriptors[i]))
                throw new InvalidDataException("Plane STA retained descriptor no longer belongs to its current Static Data.");
        }
    }
    public void Retire() => retained = false;
}

/// <summary>Actual B41860-created raw context: an owned complete stream copy
/// and exact byte length. It is distinct from a LoadedRdbAsset.</summary>
public sealed class OriginalSkyAtmoRegisteredRawContext
{
    private readonly byte[] payload;
    private bool retained = true;
    internal OriginalSkyAtmoRegisteredRawContext(LoadedRdbAsset source)
    {
        Source = source;
        using var stream = new MemoryStream(source.Bytes, false);
        payload = new byte[checked((int)stream.Length)];
        stream.ReadExactly(payload);
    }
    public LoadedRdbAsset Source { get; }
    public uint ResourceKey => Source.FileId;
    public uint ResourceType => OriginalSkyAtmoTableResources.RawResourceType;
    public int ByteLength => payload.Length;
    public ReadOnlyMemory<byte> Payload { get { RequireCurrent(); return payload; } }
    public string SourceEvidence => $"{Source.ResourceUri}@{Source.DecodedSha256};D7F47FB1/B41860;context0-owned-stream-copy/context8-length";
    public void RequireCurrent() => ObjectDisposedException.ThrowIf(!retained, this);
    public void Retire() => retained = false;
}

/// <summary>The current source preparation context owns the registered raw
/// contexts and actual Plane Data. Domain/lifetime checks belong to its owner.</summary>
public interface IOriginalSkyAtmoTablePreparationContext
{
    object AssetContextOwner { get; }
    object PreparationContextOwner { get; }
    OriginalSkyAtmoPlaneDataOwner ReadCurrentPlaneData(uint objectKey);
    OriginalSkyAtmoRegisteredRawContext? ReadRegisteredRawContext(uint resourceKey);
    void RequireCurrent();
}

/// <summary>Allocator/stream copy used by E49EB0. Returning a missing
/// allocation or short copy reproduces an actual preparation failure; this
/// is a memory service, never an authorization or readiness certificate.</summary>
public interface IOriginalSkyAtmoTableMemory
{
    byte[]? Allocate(int byteLength);
    int Copy(ReadOnlyMemory<byte> source, Memory<byte> destination);
    void Release(byte[] allocation);
}
public sealed class OriginalSkyAtmoManagedTableMemory : IOriginalSkyAtmoTableMemory
{
    public static OriginalSkyAtmoManagedTableMemory Instance { get; } = new();
    private OriginalSkyAtmoManagedTableMemory() { }
    public byte[] Allocate(int byteLength) => new byte[byteLength];
    public int Copy(ReadOnlyMemory<byte> source, Memory<byte> destination)
    { source.CopyTo(destination); return source.Length; }
    public void Release(byte[] allocation) { }
}

/// <summary>The typed 70B atmosphere CPU and its E49EB0 preparation cursor.
/// Constructor settings and actual pointer owners remain distinct from ready.
/// Numeric tables may be partially published when a later copy fails.</summary>
public sealed class OriginalSkyAtmoTableCpuOwner
{
    private readonly IOriginalSkyAtmoTablePreparationContext context;
    private readonly IOriginalSkyAtmoTableMemory memory;
    private readonly byte[]?[] tables = new byte[]?[2];
    private bool retained = true;
    internal OriginalSkyAtmoTableCpuOwner(LoadedOriginalSkyAtmoTableReadySource source,
        IOriginalSkyAtmoTablePreparationContext context, IOriginalSkyAtmoTableMemory memory)
    {
        Source = source; this.context = context; this.memory = memory; context.RequireCurrent();
        // E3FAD5..E3FB47 and E3F972 copy these exact accepted declaration
        // values into this CPU. They are numeric constructor fields, not a
        // buffer identity, texture pointer or substitute complete Camera.
        var settings = source.Tables;
        (MieExtinction30, RayleighExtinction40, MieHeight50, RayleighHeight54,
            PlanetRadius58, AtmoTopHeight5C, Width60, Height64, Flags68) =
            (settings.MieExtinction, settings.RayleighExtinction, settings.MieHeight, settings.RayleighHeight,
                settings.PlanetRadius, settings.AtmoTopHeight, settings.Width, settings.Height, settings.Flags);
        PlaneData = context.ReadCurrentPlaneData(source.Tables.PlaneStaObjectId);
        if (!ReferenceEquals(PlaneData.Source, source))
            throw new InvalidDataException("STA constructor received Plane Data from another loaded source generation.");
    }
    public LoadedOriginalSkyAtmoTableReadySource Source { get; }
    public OriginalSkyAtmoPlaneDataOwner PlaneData { get; }
    public Vector4 MieExtinction30 { get; }
    public Vector4 RayleighExtinction40 { get; }
    public float MieHeight50 { get; }
    public float RayleighHeight54 { get; }
    public float PlanetRadius58 { get; }
    public float AtmoTopHeight5C { get; }
    public int Width60 { get; }
    public int Height64 { get; }
    public uint Flags68 { get; }
    public int PreparationStage { get; private set; }
    public int TransmittanceByteLength => tables[0]?.Length ?? 0;
    public int Ambient2DByteLength => tables[1]?.Length ?? 0;
    public ReadOnlyMemory<byte> Transmittance => tables[0] ?? ReadOnlyMemory<byte>.Empty;
    public ReadOnlyMemory<byte> Ambient2D => tables[1] ?? ReadOnlyMemory<byte>.Empty;
    public string SourceEvidence => Source.SourceEvidence + ";E3F770-owned-CPU70/settings/Plane8;E49EB0-stage0-deps/Plane/copy0/copy1/stage1";
    public OriginalSkyAtmoReadyResult PrepareCurrent()
    {
        ObjectDisposedException.ThrowIf(!retained, this); context.RequireCurrent();
        if (PreparationStage != 0) return PreparationStage == 1 ? OriginalSkyAtmoReadyResult.Ready : OriginalSkyAtmoReadyResult.Waiting;
        // B40360 prepends constructor reservations [Amb2D,TTable], hence
        // B43790 index0 is TTable and index1 is Amb2D. B45450 gates both
        // registered contexts before preparing the retained Plane Data.
        uint[] keys = [OriginalSkyAtmoTableResources.TransmittanceFileId, OriginalSkyAtmoTableResources.Ambient2DFileId];
        var registered = new OriginalSkyAtmoRegisteredRawContext?[2];
        for (int i = 0; i < keys.Length; i++)
        {
            registered[i] = context.ReadRegisteredRawContext(keys[i]);
            if (registered[i] is null) return OriginalSkyAtmoReadyResult.Waiting;
            registered[i]!.RequireCurrent();
            if (registered[i]!.ResourceKey != keys[i] || registered[i]!.ResourceType != OriginalSkyAtmoTableResources.RawResourceType)
                throw new InvalidDataException("STA dependency index selected another registered raw context.");
        }
        var planeResult = PlaneData.PrepareCurrent();
        if (planeResult != OriginalSkyAtmoReadyResult.Ready) return planeResult;
        PlaneData.RequireCurrent();
        for (int i = 0; i < registered.Length; i++)
        {
            var raw = registered[i]!;
            byte[]? allocation = memory.Allocate(raw.ByteLength);
            if (allocation is null) return OriginalSkyAtmoReadyResult.Failed;
            if (allocation.Length != raw.ByteLength)
                throw new InvalidDataException("STA memory allocation did not retain its requested source byte length.");
            if (memory.Copy(raw.Payload, allocation) != raw.ByteLength)
            { memory.Release(allocation); return OriginalSkyAtmoReadyResult.Failed; }
            // E49FF0/FF9 then E4A0A4/AD: each successful length/pointer
            // pair is stored immediately. A retry in stage0 recopies table0.
            tables[i] = allocation;
        }
        PreparationStage++;
        return OriginalSkyAtmoReadyResult.Ready;
    }
    public void RequireCurrent()
    {
        ObjectDisposedException.ThrowIf(!retained, this); context.RequireCurrent(); PlaneData.RequireCurrent();
        if (PreparationStage != 1 || tables[0] is null || tables[1] is null)
            throw new InvalidOperationException("STA CPU has not completed both registered table copies.");
    }
    public void Retire()
    {
        if (!retained) return;
        retained = false;
        foreach (var allocation in tables) if (allocation is not null) memory.Release(allocation);
        Array.Clear(tables);
    }
}

/// <summary>Accepted executable/Static/CSV/G1T sources. Import hashes and
/// parsing execute once here; ready/cache requests use the owned objects.</summary>
public sealed class LoadedOriginalSkyAtmoTableReadySource
{
    internal LoadedOriginalSkyAtmoTableReadySource(string locale, string nativeEvidence,
        LoadedOriginalSkyAtmoTableSources tables, OriginalSkyCloudSourceTextureGroup plane)
        => (Locale, NativeEvidence, Tables, Plane) = (locale, nativeEvidence, tables, plane);
    public string Locale { get; }
    public string NativeEvidence { get; }
    public LoadedOriginalSkyAtmoTableSources Tables { get; }
    public OriginalSkyCloudSourceTextureGroup Plane { get; }
    public string SourceEvidence => Tables.SourceEvidence + ";" + Plane.SourceProvenance + ";" + NativeEvidence;
    public OriginalSkyAtmoPlaneDataOwner CreatePlaneData(IReadOnlyList<IOriginalSkyAtmoCurrentStaticTextureData> actualStaticData)
    { ArgumentNullException.ThrowIfNull(actualStaticData); return new(this, actualStaticData); }
    public OriginalSkyAtmoRegisteredRawContext CreateRegisteredRawContext(uint fileId)
        => new(fileId switch {
            OriginalSkyAtmoTableResources.TransmittanceFileId => Tables.Transmittance,
            OriginalSkyAtmoTableResources.Ambient2DFileId => Tables.Ambient2D,
            _ => throw new InvalidDataException("STA raw factory does not have this declared dependency.") });
    public OriginalSkyAtmoTableCpuOwner CreateFreshCpu(IOriginalSkyAtmoTablePreparationContext actualContext,
        IOriginalSkyAtmoTableMemory? actualMemory = null)
    { ArgumentNullException.ThrowIfNull(actualContext); return new(this, actualContext, actualMemory ?? OriginalSkyAtmoManagedTableMemory.Instance); }
}

public static class OriginalSkyAtmoTableReadySource
{
    public static LoadedOriginalSkyAtmoTableReadySource Load(OriginalGameRepository repository, string locale,
        LoadedOriginalSkyAtmoTableSources actualTables, OriginalSkyCloudSourceTextureGroup actualPlane)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualTables); ArgumentNullException.ThrowIfNull(actualPlane);
        OriginalSkyAtmoTableResources.Validate(actualTables);
        if (actualPlane.ObjectId != actualTables.PlaneStaObjectId || actualPlane.Entries.Count != 4 ||
            actualPlane.TableResourceUri != "san14://rdb/system/546F00D7" ||
            actualPlane.TableSha256 != "8B2BE83D0712260B78DA3A8E9E28D60091E0803C4627AD68AD2BB626BDCA14D6")
            throw new InvalidDataException("STA Plane requires its complete original four-slot Textures source.");
        uint[] expected = [0xF81F0562, 0xF8ED83E3, 0xF9BC0264, 0xFA8A80E5];
        for (int i = 0; i < expected.Length; i++)
        {
            var binding = actualPlane.Entries[i];
            if (binding.Definition.ObjectId != expected[i] || binding.Definition.ClassId != 0xFF7DBFD4 || binding.ImageIndex != i ||
                binding.Resource.Source.ResourceUri != "san14://rdb/system/7FC7BE7D" ||
                binding.Resource.Source.DecodedSha256 != "A40492CD7DF66B5750B137571DBFBC784D67B86417ECD60F4FE24C2416A25B2F")
                throw new InvalidDataException("STA Plane Static source/TID differs from its literal registered slot.");
        }
        string native = OriginalDeferredSamplerSources.LoadSkyAtmoTableReadyNativeSource(repository.SourceDirectory, locale);
        return new(locale, native, actualTables, actualPlane);
    }
}

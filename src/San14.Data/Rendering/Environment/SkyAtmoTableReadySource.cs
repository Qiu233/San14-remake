using System.Numerics;

namespace San14.Data;

public enum SkyAtmoReadyResult { Waiting, Ready, Failed }

/// <summary>Actual Static Data and its CPU.v28 texture descriptor. The
/// implementation owns the prepared source image; source bytes alone are
/// insufficient. Ready is the result of preparing that current Data.</summary>
public interface ISkyAtmoCurrentStaticTextureData
{
    uint ObjectKey { get; }
    uint FactoryClassId { get; }
    object? CurrentCpuOwner { get; }
    object? CurrentTextureDescriptor { get; }
    SkyAtmoReadyResult PrepareCurrent();
    void RequireCurrent();
}

/// <summary>Textures Data with the actual four source slots and CPU20.
/// A loaded texture table or a three-consumer Cloud binding is not this Data.</summary>
public sealed class SkyAtmoPlaneDataOwner
{
    private readonly ISkyAtmoCurrentStaticTextureData[] data;
    private readonly object?[] descriptors;
    private readonly IReadOnlyList<object?> descriptorView;
    private bool retained = true;
    internal SkyAtmoPlaneDataOwner(LoadedSkyAtmoTableReadySource source,
        IReadOnlyList<ISkyAtmoCurrentStaticTextureData> actualData)
    {
        Source = source;
        if (actualData.Count != 4) throw new InvalidDataException("Plane STA requires all four actual Static Data owners.");
        data = actualData.ToArray(); descriptors = new object?[4]; descriptorView = Array.AsReadOnly(descriptors);
        for (int i = 0; i < data.Length; i++)
            if (data[i].ObjectKey != source.Plane.Entries[i].Definition.ObjectId || data[i].FactoryClassId != 0xFF7DBFD4)
                throw new InvalidDataException("Plane STA current Static Data does not follow its complete literal CSV order.");
    }
    public LoadedSkyAtmoTableReadySource Source { get; }
    public uint ObjectKey => Source.Plane.ObjectId;
    public uint FactoryClassId => 0x3059B9C3;
    /// <summary>The actual owned Textures CPU exists before its ready stages.
    /// Its descriptor entries are published only by the stage1 loop.</summary>
    public object CurrentCpuOwner => this;
    public int PreparationStage { get; private set; }
    public IReadOnlyList<object?> CurrentTextureDescriptors => descriptorView;
    public string SourceEvidence => Source.SourceEvidence + ";Textures-C104B0/C15440;CPU20/CPU8-type20-count4;all-four-Static-014C60/v28";
    public SkyAtmoReadyResult PrepareCurrent()
    {
        ObjectDisposedException.ThrowIf(!retained, this);
        // The accepted CSV and actual ordered Data array have already been
        // retained at this source load boundary. Stage0 constructs the typed
        // four-entry group; it does not certify any texture entry.
        if (PreparationStage == 0) PreparationStage = 1;
        if (PreparationStage != 1) return PreparationStage == 2 ? SkyAtmoReadyResult.Ready : SkyAtmoReadyResult.Waiting;
        foreach (var entry in data)
        {
            var result = entry.PrepareCurrent();
            if (result != SkyAtmoReadyResult.Ready) return result;
        }
        // Native stage1 first prepares every Data, then publishes each CPU.v28
        // result in order. A late missing CPU leaves the earlier stores intact.
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].CurrentCpuOwner is null) return SkyAtmoReadyResult.Failed;
            data[i].RequireCurrent();
            descriptors[i] = data[i].CurrentTextureDescriptor;
            if (descriptors[i] is null)
                throw new NotSupportedException("Actual Plane Static CPU.v28 has no supported current texture descriptor.");
        }
        PreparationStage = 2;
        return SkyAtmoReadyResult.Ready;
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
public sealed class SkyAtmoRegisteredRawContext
{
    private readonly byte[] payload;
    private bool retained = true;
    internal SkyAtmoRegisteredRawContext(SkyAtmoRawTableData source)
    {Source=source;payload=source.CopyBytes();}
    public SkyAtmoRawTableData Source { get; }
    public uint ResourceKey => Source.FileId;
    public uint ResourceType => SkyAtmoTableResources.RawResourceType;
    public int ByteLength => payload.Length;
    internal ReadOnlyMemory<byte> Payload { get { RequireCurrent(); return payload; } }
    public byte[] CopyBytes(){RequireCurrent();return payload.ToArray();}
    public string SourceEvidence => $"{Source.ResourceUri}@{Source.DecodedSha256};D7F47FB1/B41860;context0-owned-stream-copy/context8-length";
    public void RequireCurrent() => ObjectDisposedException.ThrowIf(!retained, this);
    public void Retire() => retained = false;
}

/// <summary>The current source preparation context owns the registered raw
/// contexts and actual Plane Data. Domain/lifetime checks belong to its owner.</summary>
public interface ISkyAtmoTablePreparationContext
{
    object AssetContextOwner { get; }
    object PreparationContextOwner { get; }
    SkyAtmoPlaneDataOwner ReadCurrentPlaneData(uint objectKey);
    SkyAtmoRegisteredRawContext? ReadRegisteredRawContext(uint resourceKey);
    void RequireCurrent();
}

/// <summary>Allocator/stream copy used by E49EB0. Returning a missing
/// allocation or short copy reproduces an actual preparation failure; this
/// is a memory service, never an authorization or readiness certificate.</summary>
public interface ISkyAtmoTableMemory
{
    byte[]? Allocate(int byteLength);
    int Copy(ReadOnlyMemory<byte> source, Memory<byte> destination);
    void Release(byte[] allocation);
}
public sealed class SkyAtmoManagedTableMemory : ISkyAtmoTableMemory
{
    public static SkyAtmoManagedTableMemory Instance { get; } = new();
    private SkyAtmoManagedTableMemory() { }
    public byte[] Allocate(int byteLength) => new byte[byteLength];
    public int Copy(ReadOnlyMemory<byte> source, Memory<byte> destination)
    { source.CopyTo(destination); return source.Length; }
    public void Release(byte[] allocation) { }
}

/// <summary>The typed 70B atmosphere CPU and its E49EB0 preparation cursor.
/// Constructor settings and actual pointer owners remain distinct from ready.
/// Numeric tables may be partially published when a later copy fails.</summary>
public sealed class SkyAtmoTableCpuOwner
{
    private readonly ISkyAtmoTablePreparationContext context;
    private readonly ISkyAtmoTableMemory memory;
    private readonly byte[]?[] tables = new byte[]?[2];
    private bool retained = true;
    internal SkyAtmoTableCpuOwner(LoadedSkyAtmoTableReadySource source,
        ISkyAtmoTablePreparationContext context, ISkyAtmoTableMemory memory)
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
    public LoadedSkyAtmoTableReadySource Source { get; }
    public SkyAtmoPlaneDataOwner PlaneData { get; }
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
    public SkyAtmoReadyResult PrepareCurrent()
    {
        ObjectDisposedException.ThrowIf(!retained, this); context.RequireCurrent();
        if (PreparationStage != 0) return PreparationStage == 1 ? SkyAtmoReadyResult.Ready : SkyAtmoReadyResult.Waiting;
        // B40360 prepends constructor reservations [Amb2D,TTable], hence
        // B43790 index0 is TTable and index1 is Amb2D. B45450 gates both
        // registered contexts before preparing the retained Plane Data.
        SkyAtmoRawTableData[] dependencies=[Source.Tables.Transmittance,Source.Tables.Ambient2D];
        uint[] keys=[dependencies[0].FileId,dependencies[1].FileId];
        var registered = new SkyAtmoRegisteredRawContext?[2];
        for (int i = 0; i < keys.Length; i++)
        {
            registered[i] = context.ReadRegisteredRawContext(keys[i]);
            if (registered[i] is null) return SkyAtmoReadyResult.Waiting;
            registered[i]!.RequireCurrent();
            if (registered[i]!.ResourceKey != keys[i] || registered[i]!.ResourceType != SkyAtmoTableResources.RawResourceType||registered[i]!.ByteLength!=dependencies[i].ByteLength)
                throw new InvalidDataException("STA dependency index selected another registered raw context.");
        }
        var planeResult = PlaneData.PrepareCurrent();
        if (planeResult != SkyAtmoReadyResult.Ready) return planeResult;
        PlaneData.RequireCurrent();
        for (int i = 0; i < registered.Length; i++)
        {
            var raw = registered[i]!;
            byte[]? allocation = memory.Allocate(raw.ByteLength);
            if (allocation is null) return SkyAtmoReadyResult.Failed;
            if (allocation.Length != raw.ByteLength)
                throw new InvalidDataException("STA memory allocation did not retain its requested source byte length.");
            if (memory.Copy(raw.Payload, allocation) != raw.ByteLength)
            { memory.Release(allocation); return SkyAtmoReadyResult.Failed; }
            // E49FF0/FF9 then E4A0A4/AD: each successful length/pointer
            // pair is stored immediately. A retry in stage0 recopies table0.
            if(tables[i] is { } previous)memory.Release(previous);
            tables[i] = allocation;
        }
        PreparationStage++;
        return SkyAtmoReadyResult.Ready;
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
public sealed class LoadedSkyAtmoTableReadySource
{
    internal LoadedSkyAtmoTableReadySource(string locale, string nativeEvidence,
        LoadedSkyAtmoTableSources tables, SkyCloudSourceTextureGroup plane)
        => (Locale, NativeEvidence, Tables, Plane) = (locale, nativeEvidence, tables, plane);
    public string Locale { get; }
    public string NativeEvidence { get; }
    public LoadedSkyAtmoTableSources Tables { get; }
    public SkyCloudSourceTextureGroup Plane { get; }
    public string SourceEvidence => Tables.SourceEvidence + ";" + Plane.SourceProvenance + ";" + NativeEvidence;
    public SkyAtmoPlaneDataOwner CreatePlaneData(IReadOnlyList<ISkyAtmoCurrentStaticTextureData> actualStaticData)
    { ArgumentNullException.ThrowIfNull(actualStaticData); return new(this, actualStaticData); }
    public SkyAtmoRegisteredRawContext CreateRegisteredRawContext(uint fileId)
    {
        if(fileId==Tables.Transmittance.FileId)return new(Tables.Transmittance);
        if(fileId==Tables.Ambient2D.FileId)return new(Tables.Ambient2D);
        throw new InvalidDataException("STA raw factory does not have this declared dependency.");
    }
    public SkyAtmoTableCpuOwner CreateFreshCpu(ISkyAtmoTablePreparationContext actualContext,
        ISkyAtmoTableMemory? actualMemory = null)
    { ArgumentNullException.ThrowIfNull(actualContext); return new(this, actualContext, actualMemory ?? SkyAtmoManagedTableMemory.Instance); }
}

public static class SkyAtmoTableReadySource
{
    public static LoadedSkyAtmoTableReadySource Load(GameResourceRepository repository, string locale,
        LoadedSkyAtmoTableSources actualTables, SkyCloudSourceTextureGroup actualPlane)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualTables); ArgumentNullException.ThrowIfNull(actualPlane);
        if (actualPlane.ObjectId != actualTables.PlaneStaObjectId || actualPlane.Entries.Count != 4)
            throw new InvalidDataException("STA Plane requires its declared complete four-slot Textures source.");
        for (int i = 0; i < actualPlane.Entries.Count; i++)
        {
            var binding = actualPlane.Entries[i];
            if (binding.Definition.ClassId != 0xFF7DBFD4 ||
                (binding.Image.TextureSystem==2?8u:4u)!=(i==0?8u:4u))
                throw new InvalidDataException("STA Plane entry does not supply its declared Static texture dimension.");
        }
        string native = RenderSamplerSourceAdapters.LoadSkyAtmoTableReadyNativeSource(repository.SourceDirectory, locale);
        return new(locale, native, actualTables, actualPlane);
    }
}

using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The actual retained Camera.V418 consumer. The same CPU storage
/// and current preparation context are passed after the native cache/resource
/// stores. This is an integration boundary, not a replacement V418 producer.</summary>
public delegate void MainMapSeasonCameraSelfPreparation(
    Span<byte> currentCameraCpu, ulong currentPreparationContext, int gameCameraSlot);

public readonly record struct MainMapSeasonCameraCacheClear(int CpuOffset, int ByteCount);
public readonly record struct MainMapSeasonCameraResourceCopy(int SourceCpuOffset, int DestinationCpuOffset);

/// <summary>Retained camera cache and resource bindings used during a seasonal reload.</summary>
public sealed class LoadedMainMapSeasonCameraRefreshSource
{
    private readonly MainMapSeasonCameraCacheClear[] clears;
    private readonly MainMapSeasonCameraResourceCopy[] copies;
    private readonly int[] resourceZeroes;
    private readonly (int? Source, int Destination)[] resourceStores;

    internal LoadedMainMapSeasonCameraRefreshSource(string locale,
        MainMapSeasonCameraCacheClear[] clears,
        MainMapSeasonCameraResourceCopy[] copies, int[] resourceZeroes,
        (int? Source, int Destination)[] resourceStores, string evidence)
    {
        Locale = MainMapCameraRules.Language(locale); this.clears = clears;
        this.copies = copies; this.resourceZeroes = resourceZeroes;
        this.resourceStores = resourceStores; SourceEvidence = evidence;
        CacheClears = Array.AsReadOnly(clears);
        ResourceCopies = Array.AsReadOnly(copies);
        ResourceZeroes = Array.AsReadOnly(resourceZeroes);
    }

    public const uint HostResourceReloadBit = 1u << 30;
    // Highest actual CAAEE0 source read is sourceCPU+2F40, width8.
    public const int RequiredCurrentCpuByteLength = 0x2F48;
    public string Locale { get; }
    public string RuleVersion => MainMapCameraRules.Version;
    public string SourceEvidence { get; }
    public IReadOnlyList<MainMapSeasonCameraCacheClear> CacheClears { get; }
    public IReadOnlyList<MainMapSeasonCameraResourceCopy> ResourceCopies { get; }
    public IReadOnlyList<int> ResourceZeroes { get; }

    /// <summary>13C130's thirteen stores followed by the complete no-call
    /// CAAEE0(self,self) resource projection. This is only the prefix of the
    /// refresh operation; it does not execute or attest Camera.V418.</summary>
    public void ApplyCacheAndResourceStores(Span<byte> currentCameraCpu)
    {
        if (currentCameraCpu.Length < RequiredCurrentCpuByteLength)
            throw new InvalidDataException("Season Camera refresh requires current source CPU resource slots through2F47; a3C0 prepared prefix is insufficient.");
        foreach (var clear in clears)
            currentCameraCpu.Slice(clear.CpuOffset, clear.ByteCount).Clear();
        foreach (var store in resourceStores)
        {
            // All inputs and destinations refer to the same retained CPU.
            // Preserve the native order, rather than snapshotting old slots.
            ulong current = store.Source is int source
                ? BinaryPrimitives.ReadUInt64LittleEndian(currentCameraCpu.Slice(source, 8)) : 0UL;
            BinaryPrimitives.WriteUInt64LittleEndian(currentCameraCpu.Slice(store.Destination, 8), current);
        }
    }

    /// <summary>Consume the normal Host.bit30 event on its two existing
    /// Game+78/+80 Camera CPUs, in native order. The required consumer must
    /// implement the actual same-CPU V418 preparation with current context.
    /// Host.bit30 clears only after both consumers return. If a consumer
    /// throws, prior writes remain; this operation does not fake rollback.</summary>
    public bool RefreshRetainedCameras(ref uint currentHostFlags,
        Memory<byte> currentGameCamera78Cpu, Memory<byte> currentGameCamera80Cpu,
        ulong currentPreparationContext, MainMapSeasonCameraSelfPreparation prepareSameCamera)
    {
        if ((currentHostFlags & HostResourceReloadBit) == 0) return false;
        ArgumentNullException.ThrowIfNull(prepareSameCamera);
        ApplyCacheAndResourceStores(currentGameCamera78Cpu.Span);
        prepareSameCamera(currentGameCamera78Cpu.Span, currentPreparationContext, 0x78);
        ApplyCacheAndResourceStores(currentGameCamera80Cpu.Span);
        prepareSameCamera(currentGameCamera80Cpu.Span, currentPreparationContext, 0x80);
        currentHostFlags &= ~HostResourceReloadBit;
        return true;
    }
}

public static class MainMapSeasonCameraRefreshSource
{
    public static LoadedMainMapSeasonCameraRefreshSource CreateDefaults(string locale)
    {
        MainMapSeasonCameraCacheClear[] clears =
        [
            new(0xBFC,4),
            new(0x80C,4),
            new(0xB98,4),
            new(0xBAC,4),
            new(0x1244,8),
            new(0x124C,8),
            new(0xD80,4),
            new(0xDCC,4),
            new(0x5C0,4),
            new(0x5DC,4),
            new(0x6C0,4),
            new(0x7FC,4),
            new(0xCD8,4),
        ];
        (int? Source,int Destination)[] stores =
        [
            (0x2CC8,0x2BD8),
            (0x2CE0,0x2960),
            (0x2CE8,0x2968),
            (0x2CF0,0x2970),
            (0x2CF8,0x2978),
            (0x2D00,0x2980),
            (0x2D08,0x2988),
            (0x2D10,0x2990),
            (0x2D18,0x2998),
            (0x2D20,0x29A0),
            (0x2D28,0x29A8),
            (0x2D30,0x29B0),
            (0x2D38,0x29B8),
            (0x2D40,0x29C0),
            (0x2D48,0x29C8),
            (0x2D50,0x29D0),
            (0x2D58,0x29D8),
            (0x2D60,0x29E0),
            (0x2D68,0x29E8),
            (0x2D70,0x29F0),
            (0x2D78,0x29F8),
            (0x2D80,0x2A00),
            (0x2D88,0x2A08),
            (0x2D90,0x2A10),
            (0x2D98,0x2A18),
            (0x2DA0,0x2A20),
            (0x2DA8,0x2A28),
            (0x2DB0,0x2A30),
            (0x2DB8,0x2A38),
            (0x2DC0,0x2A40),
            (0x2DC8,0x2A48),
            (0x2DD0,0x2A50),
            (0x2DD8,0x2A58),
            (0x2DE0,0x2A60),
            (0x2DE8,0x2A68),
            (0x2DF0,0x2A70),
            (0x2DF8,0x2A78),
            (0x2E00,0x2A80),
            (0x2E08,0x2A88),
            (0x2E10,0x2A90),
            (0x2E18,0x2A98),
            (0x2E20,0x2AA0),
            (0x2E88,0x2B08),
            (0x2E90,0x2B10),
            (0x2E98,0x2B18),
            (0x2EA0,0x2B20),
            (0x2EA8,0x2B28),
            (0x2E28,0x2AA8),
            (0x2E30,0x2AB0),
            (0x2E38,0x2AB8),
            (0x2E40,0x2AC0),
            (0x2E48,0x2AC8),
            (0x2E50,0x2AD0),
            (0x2E58,0x2AD8),
            (0x2E60,0x2AE0),
            (0x2E68,0x2AE8),
            (0x2E70,0x2AF0),
            (0x2E78,0x2AF8),
            (0x2E80,0x2B00),
            (0x2EB0,0x2B30),
            (0x2EB8,0x2B38),
            (0x2EC0,0x2B40),
            (0x2EC8,0x2B48),
            (0x2ED0,0x2B50),
            (0x2EC8,0x2B58),
            (0x2ED0,0x2B60),
            (null,0x2B68),
            (null,0x2B70),
            (0x2ED8,0x2B78),
            (0x2EE0,0x2B80),
            (0x2EE8,0x2B88),
            (0x2EF0,0x2B90),
            (0x2EF8,0x2B98),
            (0x2F00,0x2BA0),
            (0x2F08,0x2BA8),
            (0x2F10,0x2BB0),
            (0x2F18,0x2BB8),
            (0x2F20,0x2BC0),
            (0x2F28,0x2BC8),
            (0x2F30,0x2BD0),
            (0x2F38,0x2BE0),
            (0x2F40,0x2BE8),
        ];
        return new(locale,clears,stores.Where(s=>s.Source is not null)
            .Select(s=>new MainMapSeasonCameraResourceCopy(s.Source!.Value,s.Destination)).ToArray(),
            stores.Where(s=>s.Source is null).Select(s=>s.Destination).ToArray(),stores,
            $"{MainMapCameraRules.Version};{locale};season-refresh;Game78-then80;ordered-retained-bindings;same-camera-preparation");
    }

    public static LoadedMainMapSeasonCameraRefreshSource Load(GameResourceRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return CreateDefaults(locale);
    }
}

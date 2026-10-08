using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The actual retained Camera.V418 consumer. The same CPU storage
/// and current preparation context are passed after the native cache/resource
/// stores. This is an integration boundary, not a replacement V418 producer.</summary>
public delegate void OriginalMainMapSeasonCameraSelfPreparation(
    Span<byte> currentCameraCpu, ulong currentPreparationContext, int gameCameraSlot);

public readonly record struct OriginalMainMapSeasonCameraCacheClear(int CpuOffset, int ByteCount);
public readonly record struct OriginalMainMapSeasonCameraResourceCopy(int SourceCpuOffset, int DestinationCpuOffset);

/// <summary>The original Host.bit30 retained-Camera refresh instruction plan.
/// Import executable windows once at load/reload. The normal methods only
/// transport the caller's current words; they never read files or hash state.</summary>
public sealed class LoadedOriginalMainMapSeasonCameraRefreshSource
{
    private readonly OriginalMainMapSeasonCameraCacheClear[] clears;
    private readonly OriginalMainMapSeasonCameraResourceCopy[] copies;
    private readonly int[] resourceZeroes;
    private readonly (int? Source, int Destination)[] resourceStores;

    internal LoadedOriginalMainMapSeasonCameraRefreshSource(string locale, string hash,
        OriginalMainMapSeasonCameraCacheClear[] clears,
        OriginalMainMapSeasonCameraResourceCopy[] copies, int[] resourceZeroes,
        (int? Source, int Destination)[] resourceStores, string evidence)
    {
        Locale = locale; ExecutableSha256 = hash; this.clears = clears;
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
    public string ExecutableSha256 { get; }
    public string SourceEvidence { get; }
    public IReadOnlyList<OriginalMainMapSeasonCameraCacheClear> CacheClears { get; }
    public IReadOnlyList<OriginalMainMapSeasonCameraResourceCopy> ResourceCopies { get; }
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
        ulong currentPreparationContext, OriginalMainMapSeasonCameraSelfPreparation prepareSameCamera)
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

public static class OriginalMainMapSeasonCameraRefreshSource
{
    public static LoadedOriginalMainMapSeasonCameraRefreshSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadMainMapSeasonCameraRefreshSource(repository.SourceDirectory, locale);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMainMapSeasonCameraRefreshSource LoadMainMapSeasonCameraRefreshSource(
        string sourceDirectory, string locale)
    {
        var profile = Profile(locale);
        byte[] executable = File.ReadAllBytes(Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe"));
        VerifyHash(executable, profile.ExeHash, "Season Camera refresh executable");
        var image = new NativeImage(executable);
        byte[] text = image.DecodeText(out int textRva);
        VerifyHash(text, profile.TextHash, "Season Camera refresh decoded text");
        bool tc = locale == "TC";
        ReadOnlySpan<byte> Window(int rva, int count, string hash, string name)
        {
            var bytes = text.AsSpan(rva - textRva, count);
            VerifyHash(bytes, hash, name);
            return bytes;
        }

        int helperRva = tc ? 0x13C130 : 0x13BED0;
        var helper = Window(helperRva, 136, tc
            ? "F3DAF022D214BD69DC6C26AD1314562BC7ECB5F16C5C9BCF1D3B208BAFED9F11"
            : "F89ED06D2491F5D608F068EC9061EA459D7E69CBADEA704C21443882ACD0209B", "complete13C130 self refresh");
        int copyRva = tc ? 0xCAAEE0 : 0xCAC5B0;
        var resources = Window(copyRva, 1137,
            "82D86E5044572EF770583A2E05EBFDDFF085503BE1B9B8F67534708751513DFC", "completeCAAEE0 self resource projection");
        Window(tc ? 0x14B827 : 0x14B5C7, 90, tc
            ? "7FECEC1E713560AE579698F8169C28E7E6265C4781E8F4A9810BDE43BAF50863"
            : "6CBB0600FE1B1185A008498E562846D7F54A3FAA1F5D71768F78E583FFDC849D", "Host bit30 two Camera calls and clear");
        int preparationRva = tc ? 0xC40870 : 0xC41F20;
        Window(preparationRva, 95, tc
            ? "0DE79B1A83169641B8D03ED1241EB4CC626E097A9603D89982D5514B5CAD112D"
            : "778B0BD36BD55CC70D8BB9F378A3D814AB21506C0945F7C088B809DD43CE700D", "retained Camera V418 shim");
        Window(tc ? 0xC409C0 : 0xC42070, 14824, tc
            ? "A828CFACB82D0351866B45358A998E5567B39058B435BFDDB1A0360136634EFA"
            : "F12F0E6520F264D435422735E1042756AFF066A46436502839A6CE1A0AB78ABA", "complete retained Camera preparation body");
        Window(tc ? 0xC44480 : 0xC45B30, 246, tc
            ? "B05F784FF5982D3EC0E7655A0BB59E96A048D9290C9B324C22C64375620EC315"
            : "6EEE37C52A9904908E71E0DD4CE5AC82EE9C60ABA00D63F58D0B301E2C348104", "complete retained Camera projection body");
        int vtableRva = tc ? 0x16B42E0 : 0x16B4F90;
        if (BinaryPrimitives.ReadUInt64LittleEndian(image.Raw(vtableRva + 0x418, 8)) != 0x140000000UL + (uint)preparationRva)
            throw new InvalidDataException("Retained Main Camera V418 differs from its source preparation body.");

        // Extract only the verified consecutive native stores at this source
        // boundary. The normal consumer retains this small immutable plan.
        var clears = new List<OriginalMainMapSeasonCameraCacheClear>();
        for (int at = 0x19; at < 0x69;)
        {
            bool wide = helper[at] == 0x48;
            int opcode = at + (wide ? 1 : 0);
            if (helper[opcode] != 0x89 || helper[opcode + 1] != 0x83)
                throw new InvalidDataException("Camera self refresh cache-store sequence differs.");
            int destination = BinaryPrimitives.ReadInt32LittleEndian(helper.Slice(opcode + 2, 4));
            clears.Add(new(destination, wide ? 8 : 4));
            at = opcode + 6;
        }
        var copies = new List<OriginalMainMapSeasonCameraResourceCopy>();
        var zeroes = new List<int>();
        var resourceStores = new List<(int? Source, int Destination)>();
        int cursor = 0;
        while (resources[cursor] != 0xC3)
        {
            if (resources[cursor] == 0x33 && resources[cursor + 1] == 0xC0)
            {
                cursor += 2;
                for (int i = 0; i < 2; i++, cursor += 7)
                {
                    int zeroDestination = ReadDestination(resources, cursor);
                    zeroes.Add(zeroDestination); resourceStores.Add((null, zeroDestination));
                }
                continue;
            }
            if (!resources.Slice(cursor, 3).SequenceEqual(new byte[] { 0x48, 0x8B, 0x82 }))
                throw new InvalidDataException("Camera self resource projection source read differs.");
            int source = BinaryPrimitives.ReadInt32LittleEndian(resources.Slice(cursor + 3, 4));
            int destination = ReadDestination(resources, cursor + 7);
            copies.Add(new(source, destination));
            resourceStores.Add((source, destination));
            cursor += 14;
        }
        if (clears.Count != 13 || copies.Count != 80 || zeroes.Count != 2 || cursor != resources.Length - 1)
            throw new InvalidDataException("Complete Camera self refresh inventory differs.");
        return new(locale, profile.ExeHash, clears.ToArray(), copies.ToArray(), zeroes.ToArray(), resourceStores.ToArray(),
            $"{locale}@{profile.ExeHash};Host.bit30/Game78-then80;{helperRva:X8};" +
            $"13-cache-stores;{copyRva:X8}(self,self):80-source-qword-copies+2-null-stores;" +
            $"sameCPU.V418={preparationRva:X8}-required-consumer;not-entry-far;not-fresh-camera;not-full-V418-port");
    }

    private static int ReadDestination(ReadOnlySpan<byte> program, int at)
    {
        if (!program.Slice(at, 3).SequenceEqual(new byte[] { 0x48, 0x89, 0x81 }))
            throw new InvalidDataException("Camera self resource projection destination store differs.");
        return BinaryPrimitives.ReadInt32LittleEndian(program.Slice(at + 3, 4));
    }
}

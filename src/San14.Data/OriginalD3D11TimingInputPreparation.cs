using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Actual retained clock state at initialization. Common time and
/// Apply history have separate owners; a later session must provide both.</summary>
public readonly record struct OriginalD3D11FrameClockInitialState(uint SettingsFlags10,
    float CurrentTime3348, float InitialApplyTime1E4);

/// <summary>Once-loaded source Common settings and the verified TC FPS table.
/// Writers prepare the existing small native input files, without selecting
/// a current settings31 mode, counter delta or App callback admission.</summary>
public sealed class OriginalD3D11TimingInputPreparation
{
    private OriginalD3D11TimingInputPreparation(LoadedOriginalMapFreshCommonSource common,
        string executableSha256, uint[] fps)
    {
        Common = common; ExecutableSha256 = executableSha256;
        FpsModeTable = Array.AsReadOnly(fps);
    }
    public LoadedOriginalMapFreshCommonSource Common { get; }
    public string ExecutableSha256 { get; }
    public IReadOnlyList<uint> FpsModeTable { get; }

    public static OriginalD3D11TimingInputPreparation Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (locale != "TC")
            throw new NotSupportedException("This FPS source leaf has the verified TC PE table binding only.");
        string sha = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory, locale);
        var common = LoadSourceCommon(repository);
        uint[] fps = ReadTcFpsTable(repository.SourceDirectory, sha);
        if (fps.Length != 2 || fps.Any(value => value == 0))
            throw new InvalidDataException("Source FPS manager requires its two nonzero mode rates.");
        return new(common, sha, fps);
    }

    public static LoadedOriginalMapFreshCommonSource LoadSourceCommon(OriginalGameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalMapFreshCommonResources.ReadSourceTemplate(
            repository.LoadObjects("KIDSSystemResource", OriginalMapFreshCommonResources.SourceFileId));
    }

    /// <summary>Only for newly source-created Common3348 and Apply1E4 owners,
    /// whose constructors write zero. Retained Title/Map history instead uses
    /// WriteFrameClockInputs with the caller's actual initial state.</summary>
    public static void WriteSourceCreatedFrameClockInputs(string directory, LoadedOriginalMapFreshCommonSource common)
    {
        ArgumentNullException.ThrowIfNull(common);
        WriteFrameClockInputs(directory, new(common.Flags10, 0, 0));
    }

    public static void WriteFrameClockInputs(string directory, OriginalD3D11FrameClockInitialState initial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!float.IsFinite(initial.CurrentTime3348) || !float.IsFinite(initial.InitialApplyTime1E4))
            throw new InvalidDataException("Frame clock initialization needs both finite actual owner times.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "frame-clock-profile.txt"), FormattableString.Invariant(
            $"{initial.SettingsFlags10:X8} {initial.CurrentTime3348:R} {initial.InitialApplyTime1E4:R} 3600 1 8\n"));
    }

    public void WriteAnimationCadenceInputs(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "fps-source.bin")));
        foreach (uint fps in FpsModeTable) writer.Write(fps);
    }

    // The existing accessory source loader uses this same table binding. It
    // retains its previous installed-source read behavior; the standalone
    // timing Load above also accepts the executable version at its load boundary.
    internal static uint[] ReadTcFpsTable(string sourceDirectory, string? expectedSha256 = null)
    {
        byte[] exe = File.ReadAllBytes(Path.Combine(sourceDirectory, "SAN14CE_TC.exe"));
        if (expectedSha256 is not null && Convert.ToHexString(SHA256.HashData(exe)) != expectedSha256)
            throw new InvalidDataException("FPS source executable changed while reading its table.");
        using var pe = new PEReader(new MemoryStream(exe, false)); const int rva = 0x1A8B5C8;
        var section = pe.PEHeaders.SectionHeaders.Single(s => rva >= s.VirtualAddress && rva + 8 <= s.VirtualAddress + s.SizeOfRawData);
        var data = exe.AsSpan(section.PointerToRawData + rva - section.VirtualAddress, 8);
        return [BinaryPrimitives.ReadUInt32LittleEndian(data), BinaryPrimitives.ReadUInt32LittleEndian(data[4..])];
    }
}

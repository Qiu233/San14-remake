namespace San14.Data;

/// <summary>Actual retained clock state at initialization. Common time and
/// Apply history have separate owners; a later session must provide both.</summary>
public readonly record struct OriginalD3D11FrameClockInitialState(uint SettingsFlags10,
    float CurrentTime3348, float InitialApplyTime1E4);

/// <summary>Loaded resource Common settings and project FPS rules.
/// Writers prepare the existing small native input files, without selecting
/// a current settings31 mode, counter delta or App callback admission.</summary>
public sealed class OriginalD3D11TimingInputPreparation
{
    private OriginalD3D11TimingInputPreparation(LoadedOriginalMapFreshCommonSource common,
        uint[] fps)
    {
        Common = common;
        FpsModeTable = Array.AsReadOnly(fps);
    }
    public LoadedOriginalMapFreshCommonSource Common { get; }
    public string RuleVersion => RenderRuleIdentity.Version;
    public IReadOnlyList<uint> FpsModeTable { get; }

    public static OriginalD3D11TimingInputPreparation Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        RenderRuleIdentity.For("animation-cadence", locale);
        var common = LoadSourceCommon(repository);
        return new(common, CreateFpsModeTable());
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

    internal static uint[] CreateFpsModeTable() => ArmyGraphicsRules.FpsModeRates.ToArray();
}

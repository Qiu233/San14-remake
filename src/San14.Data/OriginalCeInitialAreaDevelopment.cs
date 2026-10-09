using San14.Core;

namespace San14.Data;

public enum OriginalCeAreaDevelopmentField
{
    Commerce = 0x3C,
    Agriculture = 0x3E,
    Barracks = 0x40
}

/// <summary>Current queries used by the three native progress setters. Null is
/// unresolved input; a reference with no physical slot is a known null pointer.</summary>
public interface IOriginalCeAreaDevelopmentWriteContext
{
    ushort? ReadAreaProgress(OriginalAreaReference area, OriginalCeAreaDevelopmentField field);
    void WriteAreaProgress(OriginalAreaReference area, OriginalCeAreaDevelopmentField field, ushort value);
    ushort? ReadAreaRepresentativeHexIndex(OriginalAreaReference area);
    OriginalHexReference? ReadHexTableReference(int physicalSlot);
    byte? ReadHexForceSlot(OriginalHexReference hex);
    OriginalForceReference? ReadForceTableReference(int physicalSlot);
    bool? ReadForceNativeValidity(OriginalForceReference force);
    int? ReadForcePhysicalIdentity(OriginalForceReference force);
    short? ReadForceDevelopmentWord1BE(OriginalForceReference force);
    int? ReadCurrentDevelopmentLevelChangeCoefficient();
    void WriteForceDevelopmentWord1BE(OriginalForceReference force, short value);
}

public sealed record OriginalCeAreaDevelopmentWriteResult(ushort Progress, int? PreviousLevel,
    int? CurrentLevel, int? ChangedForceSlot, short? ChangedForceWord1BE);

/// <summary>The current SC/TC setters for area3C/3E/40, including their optional
/// level-change side effect. This callable writer does not certify startup/event
/// completion or choose a new-game input value.</summary>
public sealed class OriginalCeInitialAreaDevelopment
{
    public const string ProfileId = "san14:ce-area-development-writers-20261002";
    private const int LevelOneThreshold = 100;
    private const int LevelTwoThreshold = 300;
    private const int LevelThreeThreshold = 600;
    private const int LevelFourThreshold = 1000;
    private const int InitialForceDevelopmentLevelChangeCoefficient = 1;
    private readonly int[] _thresholds;

    public string Locale { get; }
    public string RuleVersion { get; }
    /// <summary>Fresh rule initial value only. A write consumes the context's
    /// current value, because the saved parameter may have changed.</summary>
    public int ImageInitialLevelChangeCoefficient { get; }
    public IReadOnlyList<int> LevelThresholds { get; }

    private OriginalCeInitialAreaDevelopment(string locale, string ruleVersion, int[] thresholds, int coefficient)
    {
        Locale = locale; RuleVersion = ruleVersion; _thresholds = thresholds;
        ImageInitialLevelChangeCoefficient = coefficient;
        LevelThresholds = Array.AsReadOnly(_thresholds);
    }

    public static OriginalCeInitialAreaDevelopment LoadCurrent(string sourceDirectory, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        return new(locale, SimulationRuleIdentity.ForLocale(locale),
            [LevelOneThreshold, LevelTwoThreshold, LevelThreeThreshold, LevelFourThreshold],
            InitialForceDevelopmentLevelChangeCoefficient);
    }

    public int ReadLevel(ushort progress)
    {
        for (int index = 0; index < _thresholds.Length; index++)
            if (progress < _thresholds[index]) return index + 1;
        return 5;
    }

    public OriginalCeAreaDevelopmentWriteResult WriteCurrentProgress(OriginalAreaReference area,
        OriginalCeAreaDevelopmentField field, int requestedValue, int notificationFlag,
        IOriginalCeAreaDevelopmentWriteContext context)
    {
        ArgumentNullException.ThrowIfNull(area); ArgumentNullException.ThrowIfNull(context);
        if (field is not (OriginalCeAreaDevelopmentField.Commerce or OriginalCeAreaDevelopmentField.Agriculture or OriginalCeAreaDevelopmentField.Barracks))
            throw new ArgumentOutOfRangeException(nameof(field));
        if (!area.PhysicalSlot.HasValue)
            throw new InvalidDataException("Native area progress setter dereferences a null receiver.");
        //Flag0 does not read the previous word, Hex or force. Every nonzero
        //flag, including a negative value, takes the level-change branch.
        int? oldLevel = notificationFlag != 0 ? ReadLevel(Known(context.ReadAreaProgress(area, field), "previous progress")) : null;
        ushort progress = (ushort)Math.Clamp(requestedValue, 0, 1000);
        context.WriteAreaProgress(area, field, progress);
        if (notificationFlag == 0) return new(progress, null, null, null, null);
        int newLevel = ReadLevel(progress);
        if (newLevel == oldLevel) return new(progress, oldLevel, newLevel, null, null);

        ushort hexId = Known(context.ReadAreaRepresentativeHexIndex(area), "area3A");
        OriginalHexReference hex = Pointer(context.ReadHexTableReference(hexId <= 48399 ? hexId : 0), "Hex lookup");
        if (!hex.PhysicalSlot.HasValue)
            throw new InvalidDataException("Native area level-change branch dereferences a null Hex.");
        byte forceId = Known(context.ReadHexForceSlot(hex), "Hex14");
        OriginalForceReference force = Pointer(context.ReadForceTableReference(forceId <= 51 ? forceId : 0), "force lookup");
        if (!Known(context.ReadForceNativeValidity(force), "force validity"))
            return new(progress, oldLevel, newLevel, null, null);
        if (Known(context.ReadForcePhysicalIdentity(force), "force slot12") == 0)
            return new(progress, oldLevel, newLevel, null, null);
        //The exclusion helper invokes slot12 again; changing queries may not
        //reuse the identity consumed by the preceding zero guard.
        int freshIdentity = Known(context.ReadForcePhysicalIdentity(force), "fresh force slot12");
        if (freshIdentity == 51 || unchecked((uint)(freshIdentity - 46)) <= 4u)
            return new(progress, oldLevel, newLevel, null, null);
        short current = Known(context.ReadForceDevelopmentWord1BE(force), "force1BE");
        int coefficient = Known(context.ReadCurrentDevelopmentLevelChangeCoefficient(), "current level-change coefficient");
        int updated = unchecked(current + (newLevel - oldLevel!.Value) * coefficient);
        short bounded = (short)Math.Clamp(updated, -1000, 1000);
        context.WriteForceDevelopmentWord1BE(force, bounded);
        return new(progress, oldLevel, newLevel, force.PhysicalSlot, bounded);
    }

    private static T Known<T>(T? value, string field) where T : struct => value
        ?? throw new InvalidDataException($"Current area development {field} is unknown.");
    private static T Pointer<T>(T? value, string field) where T : class => value
        ?? throw new InvalidDataException($"Current area development {field} is unknown.");
}

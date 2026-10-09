using System.Numerics;
using San14.Core;

namespace San14.Data;

public sealed record OriginalArmyInstancePoseCodeSource(string Language, string RuleVersion);

/// <summary>Project rules for the nineteen Army records and their flag children.
/// Instruction and data-window research remains outside the runtime source.</summary>
public static class OriginalArmyInstancePoseCodeSources
{
    public const int BannerRecordIndex = 7;
    public const float FlagHeightOffset = 100;
    public static Vector4 BannerScaleArgument => new(1, 1, 1, 0);
    public static Vector4 FlagScaleArgument => new(.7f, .7f, .7f, 0);
    public static IReadOnlyList<int> FlagRecordIndices { get; } = Array.AsReadOnly(new[] { 2, 11, 13 });
    public static OriginalArmyInstancePoseCodeSource ForLanguage(string language) => language switch
    {
        "SC" => Sc,
        "TC" => Tc,
        _ => throw new InvalidDataException("Army instance pose rules require SC or TC.")
    };

    public static IReadOnlyList<Vector3> FormationRows { get; } = Array.AsReadOnly(new Vector3[]
    {
        new(-5, 2, .2f), new(-2.5f, 2, .1f), new(0, 2, 0), new(2.5f, 2, .1f), new(5, 2, .2f),
        new(-5, 0, .4f), new(-2.5f, 0, .2f), new(0, 0, 0), new(2.5f, 0, .2f), new(5, 0, .4f),
        new(-5, -2, .8f), new(-2.5f, -2, .4f), new(0, -2, .3f), new(2.5f, -2, .4f), new(5, -2, .8f),
        new(3, -2.2f, 1), new(2, -2.2f, .8f), new(-2, -2.2f, .8f), new(-3, -2.2f, 1)
    });

    /// <summary>The twentieth native role is sentinel 7 and consumes no model.
    /// Fifteen zeros select descriptor778; four ones select descriptor750.</summary>
    public static IReadOnlyList<int> RecordRoles { get; } = Array.AsReadOnly(new[]
    { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 7 });

    public static readonly OriginalArmyInstancePoseCodeSource Sc = new("SC", SimulationRuleIdentity.Version);
    public static readonly OriginalArmyInstancePoseCodeSource Tc = new("TC", SimulationRuleIdentity.Version);
}

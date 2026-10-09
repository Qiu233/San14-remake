using San14.Data.Formats;

namespace San14.Data;

/// <summary>Explicit new-game choices. Neither option is inferred from difficulty.</summary>
public sealed record CreditOptions(bool InitialMeritEnabled, bool SurrenderedRankEnabled);

/// <summary>
/// The native credit fields at one initialization step, separate from raw scenario
/// bytes and the final independent game state. The two unnamed fields stay raw.
/// </summary>
public sealed record CreditState(byte Stage, short Merit, ushort RawValue1,
    byte FirstSelectionCount, byte SecondSelectionCount, byte RawResetCode, byte RawTailCode);

/// <summary>
/// The current CE SC325810 / TC3255D0 transformation. Callers must supply the resolved eligibility and
/// current status at this step; static affiliation alone does not establish them.
/// This adapter does not run on self-save loading or assert final startup state.
/// </summary>
public static class CreditInitialization
{
    /// <summary>
    /// Maps the static appendix as the original reader does. The reset code is not
    /// serialized in this appendix, so it must be supplied independently.
    /// </summary>
    public static CreditState ReadSource(S14CharacterCreditReference source, byte resetCode)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.RawSelectionCodes is not { Count: 3 })
            throw new InvalidDataException("CE credit initialization requires three source selection bytes.");
        return new(source.RawStageCode, unchecked((short)source.RawValue0), source.RawValue1,
            (byte)source.RawSelectionCodes.Count(value => value is >= 1 and <= 3),
            (byte)source.RawSelectionCodes.Count(value => value is >= 4 and <= 6),
            resetCode, source.RawTailCode);
    }

    /// <summary>
    /// Returns the input unchanged when the native step skips or retains it.
    /// Eligibility includes validity, status1/special-group/force/flag exclusions;
    /// false is a verified exclusion, never a substitute for unresolved context.
    /// </summary>
    public static CreditState Apply(CreditState current, bool eligible,
        byte statusCode, CreditOptions options, S14CreditRankCatalog definitions)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.FormatMarker != 0x66)
            throw new InvalidDataException("Credit initialization requires CE rank definitions.");
        if (!eligible) return current;
        bool unaffiliated = statusCode is 5 or 7 or 8;
        if (options.InitialMeritEnabled && (unaffiliated
                ? current.Stage == 0 && current.Merit == 0
                : current.Stage != 0))
            return current;

        byte stage = unaffiliated ? (byte)0 : (byte)2;
        short value = Threshold(stage);
        short lower = Threshold(options.SurrenderedRankEnabled ? 1 : 2);
        short upper = Threshold(6);
        // Preserve native lower-bound precedence even for inverted override limits.
        short merit = value < lower ? lower : Math.Min(value, upper);
        return current with
        {
            Stage = stage, Merit = merit, RawValue1 = 0,
            FirstSelectionCount = 0, SecondSelectionCount = 0, RawResetCode = 0
        };

        short Threshold(int slot)
        {
            var matches = definitions.Ranks.Where(rank => rank.Slot == slot).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException($"CE credit initialization requires exactly one rank definition {slot}.");
            return matches[0].SignedThreshold;
        }
    }
}

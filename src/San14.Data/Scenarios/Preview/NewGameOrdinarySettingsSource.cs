using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Ordinary CE source-format boundary. Radio order, member offsets and
/// widths stay here; domain options contain only explicit semantic choices.</summary>
internal static class NewGameOrdinarySettingsSource
{
    internal sealed record Setting<T>(int Member, int Group, int Row, int Title,
        T[] Values, int[] MessageIds) where T : struct;
    internal static readonly Setting<NewGameDifficulty> Difficulty = new(0x28, 0, 0, 0x4C2,
        [NewGameDifficulty.Beginner, NewGameDifficulty.Intermediate, NewGameDifficulty.Advanced, NewGameDifficulty.Super, NewGameDifficulty.Top], [0xD, 0xE, 0xF, 0x10, 0x11]);
    internal static readonly Setting<NewGameAmount> WoundedRecovery = new(0x80, 0, 2, 0x4C4,
        [NewGameAmount.Low, NewGameAmount.Standard, NewGameAmount.High], [0x21, 0x22, 0x23]);
    internal static readonly Setting<NewGameLifespan> Lifespan = new(0x2C, 0, 6, 0x4C6,
        [NewGameLifespan.Historical, NewGameLifespan.Long, NewGameLifespan.Unlimited], [0x12, 0x13, 0x14]);
    internal static readonly Setting<NewGameBattleDeathFrequency> BattleDeathFrequency = new(0x30, 0, 5, 0x4C5,
        [NewGameBattleDeathFrequency.Low, NewGameBattleDeathFrequency.Standard, NewGameBattleDeathFrequency.High, NewGameBattleDeathFrequency.None], [0x15, 0x16, 0x17, 0x18]);
    internal static readonly Setting<bool> DuelsEnabled = new(0xBC, 1, 1, 0x6CA,
        [true, false], [0x2C, 0x2D]);
    internal static readonly Setting<bool> EncirclementEnabled = new(0xC0, 1, 2, 0x6CB,
        [true, false], [0x2E, 0x2F]);
    internal static readonly Setting<NewGameAdjacentRegionEffect> AdjacentRegionEffects = new(0xC4, 1, 3, 0x6CC,
        [NewGameAdjacentRegionEffect.Standard, NewGameAdjacentRegionEffect.Medium, NewGameAdjacentRegionEffect.Large], [0x30, 0x31, 0x32]);
    internal static readonly Setting<bool> WoundedSoldiersEnabled = new(0xCC, 1, 5, 0x6CE,
        [true, false], [0x36, 0x37]);
    internal static readonly Setting<bool> OfficerInjuryEnabled = new(0xD0, 1, 6, 0x6CF,
        [true, false], [0x38, 0x39]);
    internal static readonly Setting<NewGameCooperationEffect> CooperationEffects = new(0xD8, 2, 1, 0x6D9,
        [NewGameCooperationEffect.Small, NewGameCooperationEffect.Standard], [0x3C, 0x3D]);
    internal static readonly Setting<NewGameMagnitude> AutomaticOccupationRange = new(0xE4, 2, 4, 0x6DC,
        [NewGameMagnitude.Standard, NewGameMagnitude.Large], [0x43, 0x44]);
    internal static readonly Setting<NewGameMagnitude> FireDamage = new(0xE8, 2, 5, 0x6DD,
        [NewGameMagnitude.Standard, NewGameMagnitude.Large], [0x45, 0x46]);
    internal static readonly Setting<NewGameMagnitude> MoraleEffects = new(0xEC, 2, 6, 0x6DE,
        [NewGameMagnitude.Standard, NewGameMagnitude.Large], [0x47, 0x48]);
    internal static readonly Setting<NewGameTransportMobility> TransportMobility = new(0x104, 3, 3, 0x7F1,
        [NewGameTransportMobility.Standard, NewGameTransportMobility.Fast], [0x4F, 0x50]);
    internal static readonly Setting<NewGameCaptureFrequency> CaptureFrequency = new(0x108, 3, 4, 0x7F2,
        [NewGameCaptureFrequency.Standard, NewGameCaptureFrequency.More, NewGameCaptureFrequency.None], [0x51, 0x52, 0x53]);
    internal static readonly Setting<NewGameAmount> LandmarkEffects = new(0x114, 4, 0, 0x893,
        [NewGameAmount.Low, NewGameAmount.Standard, NewGameAmount.High], [0x17A7, 0x17A8, 0x17A9]);
    internal static readonly Setting<bool> InitialCreditEnabled = new(0x120, 4, 3, 0x880,
        [true, false], [0x17AF, 0x17B0]);
    internal static readonly Setting<NewGameMeritGain> MeritGain = new(0x124, 4, 4, 0x87B,
        [NewGameMeritGain.Standard, NewGameMeritGain.More], [0x17B2, 0x17B3]);
    internal static readonly Setting<bool> CapturedOfficersEnabled = new(0x128, 4, 5, 0x87F,
        [true, false], [0x17B4, 0x17B5]);

    internal static NewGameDifficulty? DecodeDifficulty(string id) => id switch
    {
        "beginner" => NewGameDifficulty.Beginner, "intermediate" => NewGameDifficulty.Intermediate,
        "advanced" => NewGameDifficulty.Advanced, "super" => NewGameDifficulty.Super,
        "top" => NewGameDifficulty.Top, _ => null
    };

    internal static void ApplyOverrides(Dictionary<string, int> values,
        NewGameOptions options, int? scenarioMode)
    {
        options.ValidateOrdinarySettings();
        NewGameDifficulty difficulty = DecodeDifficulty(options.DifficultyId) ?? throw new InvalidDataException(
            "Current ordinary difficulty has no proved semantic setting identity.");
        Write(Difficulty, (NewGameDifficulty?)difficulty);
        Write(WoundedRecovery, (NewGameAmount?)options.WoundedRecovery);
        Write(Lifespan, (NewGameLifespan?)options.Lifespan);
        Write(BattleDeathFrequency, (NewGameBattleDeathFrequency?)options.BattleDeathFrequency);
        Write(DuelsEnabled, (bool?)options.DuelsEnabled);
        Write(EncirclementEnabled, (bool?)options.EncirclementEnabled);
        Write(AdjacentRegionEffects, (NewGameAdjacentRegionEffect?)options.AdjacentRegionEffects);
        Write(WoundedSoldiersEnabled, (bool?)options.WoundedSoldiersEnabled);
        Write(OfficerInjuryEnabled, (bool?)options.OfficerInjuryEnabled);
        Write(CooperationEffects, (NewGameCooperationEffect?)options.CooperationEffects);
        Write(AutomaticOccupationRange, (NewGameMagnitude?)options.AutomaticOccupationRange);
        Write(FireDamage, (NewGameMagnitude?)options.FireDamage);
        Write(MoraleEffects, (NewGameMagnitude?)options.MoraleEffects);
        Write(TransportMobility, (NewGameTransportMobility?)options.TransportMobility);
        Write(CaptureFrequency, (NewGameCaptureFrequency?)options.CaptureFrequency);
        Write(LandmarkEffects, (NewGameAmount?)options.LandmarkEffects);
        Write(InitialCreditEnabled, (bool?)options.InitialCreditEnabled);
        Write(MeritGain, (NewGameMeritGain?)options.MeritGain);
        Write(CapturedOfficersEnabled, (bool?)options.CapturedOfficersEnabled);
        //609420 normal entry fixes the mode2 lifetime branch before binding.
        if (scenarioMode == 2) Write(Lifespan, NewGameLifespan.Unlimited);
        //644140 entering top preserves the user's two previous controls in the
        //page, sets both to1 and locks them. Rebuilding from a fresh source on a
        //later non-top confirmation naturally restores their source values.
        if (difficulty == NewGameDifficulty.Top)
        {
            values[BattleRemakeCurrentSettings.FieldKey(0x88)] = 1; //byte proposal
            values[BattleRemakeCurrentSettings.FieldKey(0xD4)] = 1; //i32 partial information
        }
        void Write<T>(Setting<T> setting, T? value) where T : struct
        {
            if (value is not T chosen) return; //null retains the actual source field
            int index = Array.IndexOf(setting.Values, chosen);
            if (index < 0) throw new InvalidDataException("Unsupported ordinary new-game setting choice.");
            values[BattleRemakeCurrentSettings.FieldKey(setting.Member)] = index;
        }
    }

    internal static NewGameSettingsPreview Publish(NewGameOptions draft,
        S14ScenarioSettingsDocument? settings, int? scenarioMode,
        IReadOnlyList<MessageRecord> titles, IReadOnlyList<MessageRecord> captions)
    {
        var fields = settings?.Fields.ToDictionary(x => x.NativeMemberOffset);
        var issues = new List<string>();
        bool top = DecodeDifficulty(draft.DifficultyId) == NewGameDifficulty.Top;
        return new(draft)
        {
            Difficulty = Project(Difficulty, (NewGameDifficulty?)DecodeDifficulty(draft.DifficultyId)),
            WoundedRecovery = Project(WoundedRecovery, (NewGameAmount?)draft.WoundedRecovery),
            Lifespan = Project(Lifespan, (NewGameLifespan?)draft.Lifespan),
            BattleDeathFrequency = Project(BattleDeathFrequency, (NewGameBattleDeathFrequency?)draft.BattleDeathFrequency),
            DuelsEnabled = Project(DuelsEnabled, (bool?)draft.DuelsEnabled),
            EncirclementEnabled = Project(EncirclementEnabled, (bool?)draft.EncirclementEnabled),
            AdjacentRegionEffects = Project(AdjacentRegionEffects, (NewGameAdjacentRegionEffect?)draft.AdjacentRegionEffects),
            WoundedSoldiersEnabled = Project(WoundedSoldiersEnabled, (bool?)draft.WoundedSoldiersEnabled),
            OfficerInjuryEnabled = Project(OfficerInjuryEnabled, (bool?)draft.OfficerInjuryEnabled),
            CooperationEffects = Project(CooperationEffects, (NewGameCooperationEffect?)draft.CooperationEffects),
            AutomaticOccupationRange = Project(AutomaticOccupationRange, (NewGameMagnitude?)draft.AutomaticOccupationRange),
            FireDamage = Project(FireDamage, (NewGameMagnitude?)draft.FireDamage),
            MoraleEffects = Project(MoraleEffects, (NewGameMagnitude?)draft.MoraleEffects),
            TransportMobility = Project(TransportMobility, (NewGameTransportMobility?)draft.TransportMobility),
            CaptureFrequency = Project(CaptureFrequency, (NewGameCaptureFrequency?)draft.CaptureFrequency),
            LandmarkEffects = Project(LandmarkEffects, (NewGameAmount?)draft.LandmarkEffects),
            InitialCreditEnabled = Project(InitialCreditEnabled, (bool?)draft.InitialCreditEnabled),
            MeritGain = Project(MeritGain, (NewGameMeritGain?)draft.MeritGain),
            CapturedOfficersEnabled = Project(CapturedOfficersEnabled, (bool?)draft.CapturedOfficersEnabled),
            DifficultyEffects = new(top ? false : ReadBoolean(0x88, 1),
                top ? NewGameEnemyArmyInformation.Partial : ReadInformation(), top),
            UnresolvedInputs = issues.AsReadOnly()
        };

        int? Raw(int member, int width)
        {
            if (fields is not null && fields.TryGetValue(member, out var field) && field.Width == width)
                return width == 1 ? (byte)field.RawValue : unchecked((int)field.RawValue);
            issues.Add($"new-game.settings.source-field:{member:X}"); return null;
        }
        bool? ReadBoolean(int member, int width) => Raw(member, width) switch
        { 0 => true, 1 => false, _ => null };
        NewGameEnemyArmyInformation? ReadInformation() => Raw(0xD4, 4) switch
        { 0 => NewGameEnemyArmyInformation.All, 1 => NewGameEnemyArmyInformation.Partial, _ => null };
        static string? Text(IReadOnlyList<MessageRecord> table, int index) =>
            (uint)index < (uint)table.Count ? table[index].Text : null;
        NewGameSettingPreview<T> Project<T>(Setting<T> setting, T? chosen) where T : struct
        {
            int? raw = Raw(setting.Member, 4);
            T? source = raw is int sourceIndex && (uint)sourceIndex < (uint)setting.Values.Length
                ? setting.Values[sourceIndex] : null;
            string? unresolved = source is null ? $"new-game.settings.source-choice:{setting.Member:X}" : null;
            if (unresolved is not null) issues.Add(unresolved);
            T? effective = chosen ?? source;
            bool lifespan = setting.Member == 0x2C;
            if (lifespan && scenarioMode == 2)
                effective = setting.Values[2];
            var choices = new NewGameSettingChoice<T>[setting.Values.Length];
            for (int index = 0; index < choices.Length; index++)
            {
                //623280 uses20 as a bypass. Current CE36FFD0 proves IDs2/5
                //also return true; no conclusion is made for dynamic IDs7..19.
                bool? allowed = lifespan && index < 2
                    ? scenarioMode is int mode ? mode != 2 : null : true;
                string? gate = allowed is null ? "new-game.settings.World44" : null;
                choices[index] = new(setting.Values[index], index, setting.MessageIds[index],
                    Text(captions, setting.MessageIds[index]), allowed, gate);
            }
            return new(setting.Group, setting.Row, setting.Title, Text(titles, setting.Title),
                chosen, source, effective, Array.AsReadOnly(choices), unresolved);
        }
    }
}

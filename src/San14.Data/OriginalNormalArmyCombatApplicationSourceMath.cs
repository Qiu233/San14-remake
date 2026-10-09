using San14.Core;

namespace San14.Data;

public sealed record OriginalNormalArmyCombatApplicationSourceField(string Name,
    string Key, int Value);

/// <summary>Current numeric globals reached by the Army application stages.
/// Terrain category1 is a saved runtime allocation, distinct from backed data.</summary>
public sealed record OriginalNormalArmyCombatApplicationCurrentValues(
    int CharmScale, int CharmDenominator, int MoraleScale, int MoraleDenominator,
    int AdjacentAddend, int Terrain2Addend, int Terrain3Addend, int Terrain4Addend,
    int Terrain5Addend, int Terrain6Addend, int Terrain7Addend, int Terrain8Addend,
    int Terrain9Addend, int Terrain10Addend, int DifficultyPlayerAddend,
    int SpecialForceAddend, int WoundedSubtractionPercent, int MoraleProbabilityScale,
    int MoraleLossSamplerBound);

public sealed class OriginalNormalArmyCombatApplicationSourceParameters
{
    public string Language { get; }
    public SourceProvenance Source { get; }
    public OriginalBattleUnitSourceProfile SourceCodeProfile { get; }
    public IReadOnlyList<OriginalNormalArmyCombatApplicationSourceField> Fields { get; }
    public OriginalNormalArmyCombatApplicationCurrentValues CurrentValues { get; }
    public string? CurrentRevision { get; }
    public bool IsImageInitial => CurrentRevision is null;
    public float PercentDenominator => 100f;
    public float One => 1f;
    public float SamplingResolution => 10000f;

    internal OriginalNormalArmyCombatApplicationSourceParameters(string language,
        SourceProvenance source, OriginalBattleUnitSourceProfile profile,
        OriginalNormalArmyCombatApplicationSourceField[] fields,
        OriginalNormalArmyCombatApplicationCurrentValues values, string? revision = null)
    {
        Language = language; Source = source; SourceCodeProfile = profile;
        Fields = Array.AsReadOnly(fields); CurrentValues = values; CurrentRevision = revision;
    }

    public OriginalNormalArmyCombatApplicationSourceParameters WithCurrentValues(
        OriginalNormalArmyCombatApplicationCurrentValues values, string revision)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (string.IsNullOrWhiteSpace(revision))
            throw new InvalidDataException("Current normal application globals need an explicit revision.");
        return new(Language, Source, SourceCodeProfile, Fields.ToArray(), values, revision);
    }
}

public sealed record OriginalNormalArmyCombatRequestedLossReceipt(int RequestedLoss,
    ushort FrameWord20After, ushort FrameWord22, bool UsedMinimumOne);
public sealed record OriginalNormalArmyCombatTroopDebitReceipt(int AppliedLoss,
    ushort Troops16After, bool DidWrite);

/// <summary>Resolved native query outputs. Terrain42 is the current terrain
/// definition's category, not a UI terrain label or ruler title. Effect6F values
/// are explicit zero only after their active-effect queries were resolved false.
/// RuntimeTerrain1Addend is required only for a valid terrain category 1.</summary>
public sealed record OriginalNormalArmyCombatWoundedRatioInput(bool TargetCommanderValid,
    int TargetCharm4, int CappedTargetMorale, bool Adjacent,
    bool TargetTerrainValid, byte TerrainCategory42, int? RuntimeTerrain1Addend,
    int SelectorE58, int Difficulty28, bool TargetForceValid, bool TargetForceIsPlayer,
    bool AttackerArmyValid, bool AttackerCommanderValid, int TargetEffect6F,
    int AttackerEffect6F, bool TargetSpecialForcePerson);

public sealed record OriginalNormalArmyCombatWoundedReceipt(bool ChannelEnabled,
    int AddedBeforeAdvancedPercent, int AddedAfterAdvancedPercent, ushort AfterAddition18,
    int Subtracted, ushort Wounded18After);
public sealed record OriginalNormalArmyCombatSamplerDraw(string Phase, int Bound,
    int FirstSalt, int SecondSalt, int ThirdSalt, int FourthSalt, int Result,
    int? Threshold);
public sealed record OriginalNormalArmyCombatMoraleReceipt(int Loss,
    IReadOnlyList<OriginalNormalArmyCombatSamplerDraw> Draws)
{
    public int NativeAdjustment => unchecked(-Loss);
}

/// <summary>Pure numeric stages inside SC178C20 / TC178EC0 for an already
/// validated nontransport Army target. These methods do not resolve eligibility,
/// perform merit/trait/death/report callbacks, or write an Army or random stream.
/// The ordinary caller's frame20+22 request is distinct from common gross damage.</summary>
public static class OriginalNormalArmyCombatApplicationSourceMath
{
    public static OriginalNormalArmyCombatApplicationSourceParameters LoadInitial(
        OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        OriginalBattleUnitSourceProfile profile = ForLanguage(language);
        var fields = FieldDefinitions.Select(field => new OriginalNormalArmyCombatApplicationSourceField(
            field.Name, field.Key, field.Value)).Append(
                new("terrain1LoaderZero", SimulationParameterKeys.NormalCombatApplication.TerrainOneAddend, 0)).ToArray();
        return new(language, SimulationRuleSources.Provenance(
            "san14-remake://rules/normal-army-application/v1", language), profile, fields,
            Values(fields.Select(field => field.Value).ToArray()));
    }

    /// <summary>Append nineteen rule defaults and the initial terrain1
    /// allocation to a fresh owner. Old/current saved values are never repaired.</summary>
    public static GameState InstallFresh(GameState state,
        OriginalNormalArmyCombatApplicationSourceParameters source, string revision)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        if (!source.IsImageInitial || string.IsNullOrWhiteSpace(revision) ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } clock ||
            clock.AnchorDate != state.Date || state.Units is null || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { Parameters: { } parameters } simulation ||
            parameters.Language != source.Language ||
            !string.Equals(parameters.RuleVersion, source.Source.RuleVersion, StringComparison.OrdinalIgnoreCase) ||
            parameters.Int32Values is null || parameters.Float32Values is null ||
            string.IsNullOrWhiteSpace(parameters.Revision))
            throw new InvalidDataException("Normal application seed requires a fresh matching empty simulation owner.");
        foreach (var field in source.Fields)
        {
            string key = field.Key;
            if (parameters.Int32Values.Keys.Concat(parameters.Float32Values.Keys)
                .Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Normal application seed cannot overwrite an existing current key.");
        }
        var integers = new Dictionary<string, int>(parameters.Int32Values, parameters.Int32Values.Comparer);
        foreach (var field in source.Fields)
            integers.Add(field.Key, field.Value);
        var current = parameters with
        {
            Int32Values = integers,
            Float32Values = new(parameters.Float32Values, parameters.Float32Values.Comparer),
            Revision = $"{parameters.Revision}|{revision}:normal-application-rules:{source.Source.RuleVersion}"
        };
        GameState result = state with { NativeArmySimulation = simulation with { Parameters = current } };
        NativeSubdayStateValidation.Validate(result);
        return result;
    }

    /// <summary>A missing current key is unknown even if the image has a default.</summary>
    public static OriginalNormalArmyCombatApplicationSourceParameters? ReadCurrent(GameState state,
        OriginalNormalArmyCombatApplicationSourceParameters source)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            parameters.Language != source.Language || parameters.Int32Values is null ||
            parameters.Float32Values is null || string.IsNullOrWhiteSpace(parameters.Revision) ||
            !string.Equals(parameters.RuleVersion, source.Source.RuleVersion, StringComparison.OrdinalIgnoreCase)) return null;
        var values = new int[FieldDefinitions.Length];
        for (int index = 0; index < values.Length; index++)
        {
            string key = FieldDefinitions[index].Key;
            if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key, out values[index])) return null;
        }
        return source.WithCurrentValues(Values(values), parameters.Revision);
    }

    public static int? ReadCurrentTerrain1Addend(GameState state)
    {
        string key = SimulationParameterKeys.NormalCombatApplication.TerrainOneAddend;
        return state.NativeArmySimulation?.Parameters is { } owner &&
            !owner.Float32Values.ContainsKey(key) && owner.Int32Values.TryGetValue(key, out int value) ? value : null;
    }

    /// <summary>SC174856..17488C. Both frame inputs have already been narrowed
    /// to native words. Their sum is an i32 and is not narrowed a second time.</summary>
    public static OriginalNormalArmyCombatRequestedLossReceipt BuildNormalRequestedLoss(
        ushort frameWord20, ushort frameWord22)
    {
        bool floor = frameWord20 + frameWord22 == 0;
        ushort after = floor ? (ushort)1 : frameWord20;
        return new(after + frameWord22, after, frameWord22, floor);
    }

    /// <summary>SC178D89 cap followed by SC178E9F..178EBF. Callers must have
    /// closed the native target-validity and 2337C0 exclusion branches.</summary>
    public static OriginalNormalArmyCombatTroopDebitReceipt ApplyTroopDebit(
        ushort currentTroops16, int requestedLoss)
    {
        if (requestedLoss < 0)
            throw new InvalidDataException("Ordinary Army combat does not support a negative caller loss.");
        int applied = Math.Min(currentTroops16, requestedLoss);
        return new(applied, (ushort)Math.Clamp(unchecked(currentTroops16 - applied), 0, ushort.MaxValue), currentTroops16 != 0);
    }

    /// <summary>SC17CFD0 / TC17D260, with source scalar f32 sum order.
    /// Null identifies the reached runtime-only category-1 input or unsupported
    /// native stack-table overread; it never substitutes a guessed addend.</summary>
    public static float? CalculateWoundedRatio(OriginalNormalArmyCombatApplicationCurrentValues parameters,
        OriginalNormalArmyCombatWoundedRatioInput input)
    {
        ArgumentNullException.ThrowIfNull(parameters); ArgumentNullException.ThrowIfNull(input);
        if (parameters.MoraleDenominator == 0 || input.TargetCommanderValid && parameters.CharmDenominator == 0)
            throw new InvalidDataException("Current wounded ratio denominator is zero.");
        float charm = 0f;
        if (input.TargetCommanderValid)
        {
            charm = (float)input.TargetCharm4 * (float)parameters.CharmScale;
            charm = charm / (float)parameters.CharmDenominator;
        }
        float amount = (float)parameters.MoraleScale * (float)input.CappedTargetMorale;
        amount = amount / (float)parameters.MoraleDenominator;
        float adjacency = input.Adjacent ? (float)parameters.AdjacentAddend : 0f;
        float terrain = 0f;
        if (input.TargetTerrainValid)
        {
            int? addend = input.TerrainCategory42 switch
            {
                0 => 0, 1 => input.RuntimeTerrain1Addend,
                2 => parameters.Terrain2Addend, 3 => parameters.Terrain3Addend,
                4 => parameters.Terrain4Addend, 5 => parameters.Terrain5Addend,
                6 => parameters.Terrain6Addend, 7 => parameters.Terrain7Addend,
                8 => parameters.Terrain8Addend, 9 => parameters.Terrain9Addend,
                10 => parameters.Terrain10Addend, _ => null
            };
            if (addend is null) return null;
            terrain = (float)addend.Value;
        }
        float difficulty = input.SelectorE58 != 0 && input.TargetForceValid && input.TargetForceIsPlayer &&
            input.Difficulty28 >= 3 ? (float)parameters.DifficultyPlayerAddend : 0f;
        float effects = 0f;
        if (input.AttackerArmyValid && input.AttackerCommanderValid)
            effects = (float)input.TargetEffect6F + (float)input.AttackerEffect6F;
        float special = input.TargetSpecialForcePerson ? (float)parameters.SpecialForceAddend : 0f;
        amount = amount + charm;
        amount = amount + adjacency;
        amount = amount + terrain;
        amount = amount + difficulty;
        amount = amount + effects;
        amount = amount + special;
        // MAXSS(amount,+0) selects the second operand for an unordered value.
        amount = amount > 0f ? amount : 0f;
        return amount / 100f;
    }

    /// <summary>SC178EED..17913B. Selector/CC are the actual current settings
    /// fields, not difficulty aliases. advancedPercent7 is required only when
    /// E58==0. The two writes clamp independently after addition and subtraction.</summary>
    public static OriginalNormalArmyCombatWoundedReceipt? ApplyWoundedChannel(
        OriginalNormalArmyCombatApplicationCurrentValues parameters, ushort currentWounded18,
        int appliedLoss, float? ratio, int selectorE58, int settingsCC, int? advancedPercent7)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (appliedLoss is < 0 or > ushort.MaxValue)
            throw new InvalidDataException("Wounded application needs the already capped Army loss.");
        if (selectorE58 == 0 && advancedPercent7 is null) return null;
        bool enabled = selectorE58 != 0 ? settingsCC == 0 : advancedPercent7!.Value > 0;
        if (!enabled) return new(false, 0, 0, currentWounded18, 0, currentWounded18);
        if (ratio is null) return null;
        if (!float.IsFinite(ratio.Value) || ratio.Value < 0f)
            throw new InvalidDataException("Resolved wounded ratio must be finite and nonnegative.");
        int addition = TruncateNative(ratio.Value * (float)appliedLoss);
        int adjusted = selectorE58 == 0
            ? Math.Min(appliedLoss, unchecked(addition * advancedPercent7!.Value) / 100)
            : addition;
        ushort afterAddition = (ushort)Math.Clamp(unchecked(currentWounded18 + adjusted), 0, 20000);
        float subtraction = (float)parameters.WoundedSubtractionPercent / 100f;
        subtraction = subtraction * (float)appliedLoss;
        int removed = TruncateNative(subtraction);
        ushort afterSubtraction = (ushort)Math.Clamp(unchecked(afterAddition - removed), 0, 20000);
        return new(true, addition, adjusted, afterAddition, removed, afterSubtraction);
    }

    /// <summary>SC17D2F0 / TC17D580. The three reached samples are pure salted
    /// queries. A null world means a resolved absent native World; an unresolved
    /// World must be rejected by the caller before entering this method.</summary>
    public static OriginalNormalArmyCombatMoraleReceipt CalculateMoraleLoss(
        OriginalNormalArmyCombatApplicationCurrentValues parameters, ushort wounded18,
        ushort troops16, int leadership, int appliedLoss, OriginalTacticSourceSamplerWorld? world)
    {
        if (appliedLoss is < 0 or > ushort.MaxValue)
            throw new InvalidDataException("Morale sampling needs the already capped Army loss.");
        return CalculateBaseMoraleLoss(parameters, wounded18, troops16, leadership, appliedLoss, world);
    }

    /// <summary>TC17D580's int32 Base40/Base3C inputs. Salt words remain
    /// int32; the City caller cannot narrow them to Army words. All floating
    /// operations and sampler order share the existing source core.</summary>
    public static OriginalNormalArmyCombatMoraleReceipt CalculateBaseMoraleLoss(
        OriginalNormalArmyCombatApplicationCurrentValues parameters, int wounded40,
        int troops3C, int leadership, int appliedLoss, OriginalTacticSourceSamplerWorld? world)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (appliedLoss < 0)
            throw new InvalidDataException("Base morale sampling needs the already capped nonnegative loss.");
        var draws = new List<OriginalNormalArmyCombatSamplerDraw>();
        float troops = (float)troops3C;
        float denominator = (float)wounded40 + troops;
        float fraction = troops / denominator;
        float probability = 1f - fraction;
        probability = probability * (float)parameters.MoraleProbabilityScale;
        probability = probability / 100f;
        probability = probability * 100f;
        // MINSS(100,NaN) returns NaN. COMISS(0,NaN); JAE is false, so
        // a zero-total target still reaches the first salted query, then fails
        // the signed comparison against CVTTSS2SI's int.MinValue threshold.
        probability = float.IsNaN(probability) ? probability : MathF.Min(100f, probability);
        if (!float.IsNaN(probability) && 0f >= probability) return Receipt(0);
        float thresholdFloat = probability * 10000f;
        thresholdFloat = thresholdFloat / 100f;
        int threshold = TruncateNative(thresholdFloat);
        int first = Draw("wounded-probability", 10000, troops3C, appliedLoss, threshold);
        if (first >= threshold) return Receipt(0);
        int second = Draw("loss-magnitude", parameters.MoraleLossSamplerBound, wounded40, troops3C, null);
        int loss = unchecked(second + 1);
        int leadershipDeficit = Math.Max(0, unchecked(100 - leadership));
        float extraProbability = MathF.Min(100f, (float)leadershipDeficit);
        if (0f >= extraProbability) return Receipt(loss);
        float extraThreshold = extraProbability * 10000f;
        extraThreshold = extraThreshold / 100f;
        int extraLimit = TruncateNative(extraThreshold);
        int third = Draw("leadership-extra", 10000, wounded40, leadership, extraLimit);
        if (third < extraLimit) loss = unchecked(loss + 1);
        return Receipt(loss);

        int Draw(string phase, int bound, int firstSalt, int secondSalt, int? limit)
        {
            int value = OriginalTacticEffectSourceSamplerMath.Sample(bound, 1, firstSalt, secondSalt, 0, 0, world);
            draws.Add(new(phase, bound, firstSalt, secondSalt, 0, 0, value, limit));
            return value;
        }
        OriginalNormalArmyCombatMoraleReceipt Receipt(int loss) => new(loss, Array.AsReadOnly(draws.ToArray()));
    }

    private static int TruncateNative(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f
        ? int.MinValue : (int)value;

    private static OriginalNormalArmyCombatApplicationCurrentValues Values(int[] values) =>
        new(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7],
            values[8], values[9], values[10], values[11], values[12], values[13], values[14],
            values[15], values[16], values[17], values[18]);

    private const int CharmScaleDefault = 30;
    private const int CharmDenominatorDefault = 100;
    private const int MoraleScaleDefault = 10;
    private const int MoraleDenominatorDefault = 100;
    private const int AdjacentAddendDefault = 15;
    private const int Terrain2AddendDefault = 5;
    private const int Terrain3AddendDefault = 5;
    private const int Terrain4AddendDefault = -50;
    private const int Terrain5AddendDefault = -20;
    private const int Terrain6AddendDefault = -50;
    private const int Terrain7AddendDefault = 5;
    private const int Terrain8AddendDefault = 5;
    private const int Terrain9AddendDefault = 5;
    private const int Terrain10AddendDefault = 5;
    private const int DifficultyPlayerAddendDefault = -20;
    private const int SpecialForceAddendDefault = -20;
    private const int WoundedSubtractionPercentDefault = 15;
    private const int MoraleProbabilityScaleDefault = 125;
    private const int MoraleLossSamplerBoundDefault = 3;

    private static readonly (string Name, string Key, int Value)[] FieldDefinitions =
    [
        ("charmScale", SimulationParameterKeys.NormalCombatApplication.CharmScale, CharmScaleDefault),
        ("charmDenominator", SimulationParameterKeys.NormalCombatApplication.CharmDenominator, CharmDenominatorDefault),
        ("moraleScale", SimulationParameterKeys.NormalCombatApplication.MoraleScale, MoraleScaleDefault),
        ("moraleDenominator", SimulationParameterKeys.NormalCombatApplication.MoraleDenominator, MoraleDenominatorDefault),
        ("adjacentAddend", SimulationParameterKeys.NormalCombatApplication.AdjacentAddend, AdjacentAddendDefault),
        ("terrain2Addend", SimulationParameterKeys.NormalCombatApplication.Terrain2Addend, Terrain2AddendDefault),
        ("terrain3Addend", SimulationParameterKeys.NormalCombatApplication.Terrain3Addend, Terrain3AddendDefault),
        ("terrain4Addend", SimulationParameterKeys.NormalCombatApplication.Terrain4Addend, Terrain4AddendDefault),
        ("terrain5Addend", SimulationParameterKeys.NormalCombatApplication.Terrain5Addend, Terrain5AddendDefault),
        ("terrain6Addend", SimulationParameterKeys.NormalCombatApplication.Terrain6Addend, Terrain6AddendDefault),
        ("terrain7Addend", SimulationParameterKeys.NormalCombatApplication.Terrain7Addend, Terrain7AddendDefault),
        ("terrain8Addend", SimulationParameterKeys.NormalCombatApplication.Terrain8Addend, Terrain8AddendDefault),
        ("terrain9Addend", SimulationParameterKeys.NormalCombatApplication.Terrain9Addend, Terrain9AddendDefault),
        ("terrain10Addend", SimulationParameterKeys.NormalCombatApplication.Terrain10Addend, Terrain10AddendDefault),
        ("difficultyPlayerAddend", SimulationParameterKeys.NormalCombatApplication.DifficultyPlayerAddend, DifficultyPlayerAddendDefault),
        ("specialForceAddend", SimulationParameterKeys.NormalCombatApplication.SpecialForceAddend, SpecialForceAddendDefault),
        ("woundedSubtractionPercent", SimulationParameterKeys.NormalCombatApplication.WoundedSubtractionPercent, WoundedSubtractionPercentDefault),
        ("moraleProbabilityScale", SimulationParameterKeys.NormalCombatApplication.MoraleProbabilityScale, MoraleProbabilityScaleDefault),
        ("moraleLossSamplerBound", SimulationParameterKeys.NormalCombatApplication.MoraleLossSamplerBound, MoraleLossSamplerBoundDefault),
    ];

    public static OriginalBattleUnitSourceProfile ForLanguage(string language)
        => new(language, SimulationRuleIdentity.ForLocale(language));
}

using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Core;

namespace San14.Data;

public sealed record OriginalNormalArmyCombatApplicationSourceField(string Name,
    int SemanticScRva, int PhysicalRva, int Value, string Sha256);

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
    public const int Terrain1ScRva = 0x27D48A4;
    public const int Terrain1TcRva = 0x27D3844;
    public static OriginalNormalArmyCombatApplicationSourceParameters LoadInitial(
        OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        OriginalBattleUnitSourceProfile profile = ForLanguage(language);
        string relative = $"SAN14CE_{language}.exe";
        string path = Path.GetFullPath(Path.Combine(repository.SourceDirectory, relative));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 4096 or > 128 * 1024 * 1024)
            throw new InvalidDataException("Normal application executable exceeds its inspected bound.");
        byte[] bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (hash != profile.ExecutableSha256)
            throw new InvalidDataException("Normal application executable differs from the inspected source.");
        using var pe = new PEReader(new MemoryStream(bytes, writable: false));
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Normal application needs the inspected AMD64 PE32+ ABI.");
        var fields = new List<OriginalNormalArmyCombatApplicationSourceField>();
        foreach ((string name, int semantic, int expected) in FieldDefinitions)
        {
            int physical = semantic - (language == "TC" ? 0x1030 : 0);
            ReadOnlySpan<byte> raw = Read(physical, ".data");
            int value = BinaryPrimitives.ReadInt32LittleEndian(raw);
            if (value != expected)
                throw new InvalidDataException("Normal application global differs from its inspected initial value.");
            fields.Add(new(name, semantic, physical, value, Convert.ToHexString(SHA256.HashData(raw))));
        }
        int terrain1 = language == "TC" ? Terrain1TcRva : Terrain1ScRva;
        if (pe.PEHeaders.SectionHeaders.Count(section => section.Name == ".data" &&
            terrain1 >= (long)section.VirtualAddress + section.SizeOfRawData &&
            (long)terrain1 + 4 <= (long)section.VirtualAddress + section.VirtualSize) != 1)
            throw new InvalidDataException("Terrain1 wounded input is not its pinned PE loader zero-fill allocation.");
        //17D260/17CFD0 read this allocation; the paired source has no explicit
        //writer. Install its loader value once, never as a current-query fallback.
        fields.Add(new("terrain1LoaderZero", Terrain1ScRva, terrain1, 0,
            Convert.ToHexString(SHA256.HashData(new byte[4]))));
        foreach ((int semantic, float expected) in new[] { (0x1344B98, 1f), (0x1344C4C, 100f), (0x1344C8C, 10000f) })
        {
            ReadOnlySpan<byte> raw = Read(semantic - (language == "TC" ? 0x1000 : 0), ".rdata");
            if (BinaryPrimitives.ReadInt32LittleEndian(raw) != BitConverter.SingleToInt32Bits(expected))
                throw new InvalidDataException("Normal application literal differs from its inspected identity.");
        }
        var provenance = new SourceProvenance(relative, bytes.Length, hash, profile.ExecutableSha256)
        { ResourceUri = GameResourceId.File(relative).ToString(), PhysicalPath = path, IsOverride = false };
        return new(language, provenance, profile, fields.ToArray(), Values(fields.Select(field => field.Value).ToArray()));

        ReadOnlySpan<byte> Read(int rva, string name)
        {
            var sections = pe.PEHeaders.SectionHeaders.Where(section => section.Name == name &&
                rva >= section.VirtualAddress && (long)rva + 4 <= (long)section.VirtualAddress + section.SizeOfRawData).ToArray();
            if (sections.Length != 1)
                throw new InvalidDataException("Normal application numeric input is not backed by its expected PE section.");
            long offset = (long)sections[0].PointerToRawData + rva - sections[0].VirtualAddress;
            if (offset < 0 || offset > bytes.Length - 4)
                throw new InvalidDataException("Normal application numeric input exceeds the source bytes.");
            return bytes.AsSpan((int)offset, 4);
        }
    }

    /// <summary>Append nineteen backed globals and the proved terrain1 loader
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
            !string.Equals(parameters.ExecutableSha256, source.Source.Sha256, StringComparison.OrdinalIgnoreCase) ||
            parameters.Int32Values is null || parameters.Float32Values is null ||
            string.IsNullOrWhiteSpace(parameters.Revision))
            throw new InvalidDataException("Normal application seed requires a fresh matching empty simulation owner.");
        foreach (var field in source.Fields)
        {
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (parameters.Int32Values.Keys.Concat(parameters.Float32Values.Keys)
                .Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Normal application seed cannot overwrite an existing current key.");
        }
        var integers = new Dictionary<string, int>(parameters.Int32Values, parameters.Int32Values.Comparer);
        foreach (var field in source.Fields)
            integers.Add(OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva), field.Value);
        var current = parameters with
        {
            Int32Values = integers,
            Float32Values = new(parameters.Float32Values, parameters.Float32Values.Comparer),
            Revision = $"{parameters.Revision}|{revision}:normal-application-source:{source.Source.Sha256}"
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
            !string.Equals(parameters.ExecutableSha256, source.Source.Sha256, StringComparison.OrdinalIgnoreCase)) return null;
        var values = new int[FieldDefinitions.Length];
        for (int index = 0; index < values.Length; index++)
        {
            string key = OriginalTacticRemakeParameterSeed.Key(FieldDefinitions[index].Rva);
            if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key, out values[index])) return null;
        }
        return source.WithCurrentValues(Values(values), parameters.Revision);
    }

    public static int? ReadCurrentTerrain1Addend(GameState state)
    {
        string key = OriginalTacticRemakeParameterSeed.Key(Terrain1ScRva);
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

    private static readonly (string Name, int Rva, int Expected)[] FieldDefinitions =
    [
        ("charmScale", 0x1A4C3DC, 30), ("charmDenominator", 0x1A4C3E8, 100),
        ("moraleScale", 0x1A4C3F4, 10), ("moraleDenominator", 0x1A4C400, 100),
        ("adjacentAddend", 0x1A4C40C, 15), ("terrain2Addend", 0x1A4C420, 5),
        ("terrain3Addend", 0x1A4C42C, 5), ("terrain4Addend", 0x1A4C438, -50),
        ("terrain5Addend", 0x1A4C444, -20), ("terrain6Addend", 0x1A4C450, -50),
        ("terrain7Addend", 0x1A4C45C, 5), ("terrain8Addend", 0x1A4C468, 5),
        ("terrain9Addend", 0x1A4C474, 5), ("terrain10Addend", 0x1A4C480, 5),
        ("difficultyPlayerAddend", 0x1A4C498, -20), ("specialForceAddend", 0x1A4C4A4, -20),
        ("woundedSubtractionPercent", 0x1A4C48C, 15), ("moraleProbabilityScale", 0x1A4C3AC, 125),
        ("moraleLossSamplerBound", 0x1A4C3A0, 3),
    ];

    public static OriginalBattleUnitSourceProfile ForLanguage(string language) => language switch
    {
        "SC" => new("SC", "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
            "D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6",
            Array.AsReadOnly<OriginalBattleUnitSourceWindow>([
                new("application", 0x178C20, 0x179685, "022A1053C095F074839C0D038C09843C566461D48228433488CB7F8FD245EB9F"),
                new("woundedRatio", 0x17CFD0, 0x17D2EA, "2526FBE66B9D8C9F0F95D8E81BF4F7F0044B2C23119E5F88F5A6E5A03D44DE10"),
                new("moraleLoss", 0x17D2F0, 0x17D446, "6FA9AF431563ADC096BCDF45015EE176A5596E128D4673160F3CFC596B3E7B04"),
                new("normalFrameWords", 0x174856, 0x174891, "E7BBAFC73A16AFDAE22FCCF8CF8A55F1C0CF6995E699694E1DD07C7AF0024CC9")
            ])),
        "TC" => new("TC", "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
            "FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766",
            Array.AsReadOnly<OriginalBattleUnitSourceWindow>([
                new("application", 0x178EC0, 0x179925, "4DB0B4E900E61A88DFB378CF3A15033E73C3C6D30F632CF7E7F0C0401A6A390F"),
                new("woundedRatio", 0x17D260, 0x17D57A, "11136077A2DEC9BB59343213FB429D3D10074C493C71841E91E8D38506098ADA"),
                new("moraleLoss", 0x17D580, 0x17D6D6, "069AAF24F8800672DF012D72AEFE0288269B5F4A39AB71995197B65B7E7E3F2D"),
                new("normalFrameWords", 0x174AF6, 0x174B31, "E7BBAFC73A16AFDAE22FCCF8CF8A55F1C0CF6995E699694E1DD07C7AF0024CC9")
            ])),
        _ => throw new InvalidDataException("Normal application supports only the pinned SC/TC source profiles.")
    };
}

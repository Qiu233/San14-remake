using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using San14.Core;

namespace San14.Data;

public enum OriginalPersonPendingMeritOutcome { Committed, NativeSkip, CurrentInputsUnresolved }
public sealed record OriginalPersonPendingMeritReceipt(int OfficerId, int Requested, int Trait122,
    int TraitCorrection, int Panel50Correction, int DoctrineCorrection, int Stage1Correction,
    int BeforeScaling, int Earned, ushort Before1F8, ushort After1F8, string Revision);
public sealed record OriginalPersonPendingMeritResult(OriginalPersonPendingMeritOutcome Outcome,
    GameState State, OriginalPersonPendingMeritReceipt? Receipt);

/// <summary>Complete SC169320/TC1695C0 pending-word writer. Current doctrine
/// flag8 reaches localized-name validity and deliberately stays unresolved.
/// The separate merit settlement/report callback is not executed here.</summary>
public sealed class OriginalPersonPendingMeritSource
{
    public const string ProfileId = "san14:ce-owned-pending-merit-20261003";
    public const string VersionKey = "ce-pending-merit-current:version";
    public const string Panel50Key = "ce-pending-merit-current:panel50:7C";
    private static readonly int[] SemanticGlobals = [0x1A4BFE8, 0x1A4A7DC, 0x1A4C00C, 0x1A4C000, 0x1A4BFF4];
    public static string GlobalKey(int ordinal) => ordinal is >= 0 and < 5
        ? OriginalTacticRemakeParameterSeed.Key(SemanticGlobals[ordinal])
        : throw new ArgumentOutOfRangeException(nameof(ordinal));
    private readonly LoadedScenario _source;
    private readonly string _language, _executableHash;
    private readonly int[] _initial;
    private readonly short _panel50;

    private OriginalPersonPendingMeritSource(LoadedScenario source, string language, string hash,
        int[] initial, short panel50) => (_source, _language, _executableHash, _initial, _panel50) =
            (source, language, hash, initial, panel50);

    public static OriginalPersonPendingMeritSource Load(OriginalGameRepository repository, LoadedScenario source, string language)
    {
        string expected = language switch { "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,
            _ => throw new InvalidDataException("Pending merit requires CE SC/TC.") };
        var panels = repository.LoadDoctrinePanels(source.FixedDataSource.SourcePath);
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != language ||
            panels.Source.Sha256 != source.FixedDataSource.Sha256 || panels.Catalog.Panels.Count != 80)
            throw new InvalidDataException("Pending merit requires the actual complete CE panel source.");
        var panel = panels.Catalog.Panels.Single(row => row.Slot == 50);
        if (panel.RawBytes.Length != 33 || BinaryPrimitives.ReadInt16LittleEndian(panel.RawBytes.AsSpan(23, 2)) != panel.RawValue ||
            Convert.ToHexString(SHA256.HashData(panel.RawBytes)) != panel.RawSha256)
            throw new InvalidDataException("Pending merit panel50 projection differs from its native serializer.");
        byte[] exe = File.ReadAllBytes(Path.Combine(repository.SourceDirectory, $"SAN14CE_{language}.exe"));
        if (Convert.ToHexString(SHA256.HashData(exe)) != expected)
            throw new InvalidDataException("Pending merit globals require the pinned executable.");
        using var pe = new PEReader(new MemoryStream(exe, false));
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Pending merit source has a different ABI.");
        int[] initial = SemanticGlobals.Select(semantic => {
            int rva = language == "SC" ? semantic : semantic - 0x1030;
            var section = pe.PEHeaders.SectionHeaders.Single(s => s.Name == ".data" &&
                rva >= s.VirtualAddress && (long)rva + 4 <= (long)s.VirtualAddress + s.SizeOfRawData);
            return BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(section.PointerToRawData + rva - section.VirtualAddress, 4));
        }).ToArray();
        return new(source, language, expected, initial, panel.RawValue);
    }

    /// <summary>One new owned source allocation. Current globals/panel value
    /// are saved independently and never reloaded during a later query.</summary>
    public GameState InstallFresh(GameState state)
    {
        if (!Bound(state) || state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation!.Parameters.Int32Values.ContainsKey(VersionKey) ||
            state.NativeArmySimulation.Parameters.Int32Values.ContainsKey(Panel50Key) ||
            SemanticGlobals.Any(rva => state.NativeArmySimulation.Parameters.Int32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(rva)) ||
                state.NativeArmySimulation.Parameters.Float32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(rva))))
            throw new InvalidDataException("Fresh pending merit cannot replace a saved current bank.");
        var simulation = state.NativeArmySimulation!;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1, [Panel50Key] = _panel50 };
        for (int i = 0; i < SemanticGlobals.Length; i++) values.Add(OriginalTacticRemakeParameterSeed.Key(SemanticGlobals[i]), _initial[i]);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public OriginalPersonPendingMeritResult TryApply(GameState input, OriginalEventInitializationContext current,
        int personId, int requested) => Apply(input, current, personId, requested, true);

    internal OriginalPersonPendingMeritResult TryApplyWithinArmyOperation(GameState input, OriginalEventInitializationContext current,
        int personId, int requested) => Apply(input, current, personId, requested, false);

    private OriginalPersonPendingMeritResult Apply(GameState input, OriginalEventInitializationContext current,
        int personId, int requested, bool validateWorld)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(current);
        if (!Bound(input) || !input.Officers.TryGetValue(personId, out var person) ||
            person.NativeAbilityRuntime is not { } ability || person.NativeGrowthRuntime is not { } growth ||
            ability.Revision != OriginalTacticRemakeCurrentAbilities.ProfileId ||
            ability.Language != _language || ability.ExecutableSha256 != _executableHash ||
            ability.SourceScenarioUri != _source.ScenarioSource.ResourceUri || ability.SourceScenarioSha256 != _source.ScenarioSource.Sha256 ||
            current.StaticSource?.ResourceUri != ability.SourceScenarioUri || current.StaticSource.Sha256 != ability.SourceScenarioSha256 ||
            current.ReadPersonLookupReference(personId)?.PhysicalSlot != ability.PhysicalSlot ||
            current.ReadPersonIdentity(ability.PhysicalSlot) != personId ||
            ability.Status11E is not byte status || ability.Flags196 is not ushort flags ||
            current.ReadPhysicalPerson(ability.PhysicalSlot) is not { } record ||
            record.RawStatus11E != status || record.RawFlags196 != flags || record.RawFlag1FD != ability.Extra1FD)
            return Unknown();
        if (status is 1 or 5) return Skip();
        if (new[] { 4111, 4131, 4151, 4171, 4191 }.Any(start => unchecked((uint)(personId - start)) <= 9))
        {
            if (current.ReadPersonFirstLocation(ability.PhysicalSlot) is not ushort location) return Unknown();
            if (location is >= 1 and <= 61)
            {
                var bandForce = current.ReadPersonForce(ability.PhysicalSlot);
                if (bandForce?.PhysicalSlot is not int bandForceId) return Unknown();
                if (bandForceId is not (>= 46 and <= 50)) return Skip();
            }
        }
        if ((flags & 0x4000) != 0 || requested <= 0) return Skip();
        var force = current.ReadPersonForce(ability.PhysicalSlot);
        if (force?.PhysicalSlot is not int forceId || person.ForceId != (forceId == 0 ? null : (int?)forceId)) return Unknown();
        if (growth.Revision != OriginalPersonGrowthSource.ProfileId || growth.PendingMerit1F8 is not ushort pending ||
            string.IsNullOrWhiteSpace(growth.SourceExperienceUri) ||
            input.ContentHashes.GetValueOrDefault(growth.SourceExperienceUri) != growth.SourceExperienceSha256 ||
            current.ReadPersonNativeValidity(ability.PhysicalSlot) is not bool valid ||
            current.ReadForceNativeValidity(force) is not bool forceValid || ability.Secondary1F4 is not byte stage ||
            OriginalBattleRemakeCurrentSettings.ReadSelectorE58(input) is not int selector ||
            ReadGlobal(input, 0) is not int traitScale)
            return Unknown();
        int trait = 0, policy = 0, doctrine = 0;
        if (valid)
        {
            if (OriginalTacticRemakeCurrentTraits.CommanderEffectSum(current, ability.PhysicalSlot, 122) is not int value) return Unknown();
            trait = value;
        }
        if (forceValid)
        {
            if (!input.NativeArmySimulation!.Forces.TryGetValue(forceId, out var savedForce) ||
                savedForce.Flags12 is not byte forceFlags || ReadPanel50(input, savedForce) is not int panel) return Unknown();
            policy = panel;
            if ((forceFlags & 8) != 0) return Unknown(); //22C230 current doctrine/name validity owner absent.
        }
        int? stageScale = stage == 1 ? ReadGlobal(input, 2) : 0;
        int? scaling;
        if (selector == 0)
        {
            scaling = OriginalBattleRemakeCurrentSettings.ReadAdvancedParameter(input, 21);
            if (scaling is null) return Unknown();
        }
        else
        {
            if (OriginalBattleRemakeCurrentSettings.ReadInt32(input, 0x124) is not int mode) return Unknown();
            scaling = mode == 1 ? ReadGlobal(input, 3) : mode == 0 ? ReadGlobal(input, 4) : null;
            if (mode is 0 or 1 && scaling is null) return Unknown();
        }
        if (stageScale is null) return Unknown();
        var receipt = Calculate(personId, requested, pending, trait, traitScale, policy, doctrine,
            stage == 1, stageScale.Value, scaling, $"{ProfileId}:{input.NativeArmySimulation!.Parameters.Revision}:{input.NativeClock!.Revision}");
        GameState detached = JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(input))
            ?? throw new InvalidDataException("Pending-merit state could not be detached.");
        OfficerState next = detached.Officers[personId];
        GameState result = detached with { Officers = new(detached.Officers) { [personId] = next with {
            NativeGrowthRuntime = next.NativeGrowthRuntime! with { PendingMerit1F8 = receipt.After1F8 } } } };
        if (validateWorld) StateValidator.Validate(result);
        return new(OriginalPersonPendingMeritOutcome.Committed, result, receipt);
        OriginalPersonPendingMeritResult Unknown() => new(OriginalPersonPendingMeritOutcome.CurrentInputsUnresolved, input, null);
        OriginalPersonPendingMeritResult Skip() => new(OriginalPersonPendingMeritOutcome.NativeSkip, input, null);
    }

    /// <summary>Qualified callback arithmetic: every integer multiply/add is
    /// native signed-i32 wrap. Trait122 rounds only nonzero fractions to ±1;
    /// exact zero remains zero. The pending store clamps after wrapped add.</summary>
    public static OriginalPersonPendingMeritReceipt Calculate(int personId, int requested, ushort pending,
        int trait122, int traitScale, int panel50, int doctrineScale, bool stage1, int stageScale,
        int? percent, string revision)
    {
        if (requested <= 0 || string.IsNullOrWhiteSpace(revision))
            throw new InvalidDataException("Pending merit arithmetic needs a reached positive request and revision.");
        float traitProduct = (float)unchecked(traitScale * trait122) * (float)requested;
        float scaled = traitProduct / 100f;
        int trait = scaled < 0f && scaled > -1f ? -1 : scaled > 0f && scaled < 1f ? 1 : Truncate(scaled);
        int panel = Percent(panel50, requested), doctrine = Percent(doctrineScale, requested);
        int stage = stage1 ? Percent(stageScale, requested) : 0;
        int total = Math.Max(0, unchecked(unchecked(unchecked(unchecked(stage + doctrine) + panel) + trait) + requested));
        int earned = percent.HasValue ? Percent(percent.Value, total) : total;
        int sum = unchecked((int)pending + earned);
        ushort after = sum < 0 ? (ushort)0 : (ushort)Math.Min(sum, ushort.MaxValue);
        return new(personId, requested, trait122, trait, panel, doctrine, stage, total, earned, pending, after, revision);
        static int Percent(int coefficient, int amount) => unchecked(coefficient * amount) / 100;
        static int Truncate(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f
            ? int.MinValue : (int)value;
    }

    private int? ReadPanel50(GameState state, NativeArmyForceState force)
    {
        if (force.ActivePolicyBitmap1CB is not { Length: 10 } active) return null;
        const int index = 49, mask = 1 << (index & 7);
        if ((active[index >> 3] & mask) == 0) return 0;
        if (force.SuppressedPolicyBitmap1D5 is not { Length: 10 } suppressed) return null;
        if ((suppressed[index >> 3] & mask) != 0) return 0;
        var values = state.NativeArmySimulation!.Parameters.Int32Values;
        return values.GetValueOrDefault(VersionKey) == 1 && values.TryGetValue(Panel50Key, out int value) &&
            value is >= short.MinValue and <= short.MaxValue ? value : null;
    }
    private int? ReadGlobal(GameState state, int ordinal)
    {
        var p = state.NativeArmySimulation!.Parameters;
        string key = OriginalTacticRemakeParameterSeed.Key(SemanticGlobals[ordinal]);
        return p.Int32Values.GetValueOrDefault(VersionKey) == 1 && !p.Float32Values.ContainsKey(key) &&
            p.Int32Values.TryGetValue(key, out int value) ? value : null;
    }
    private bool Bound(GameState state) => state.NativeClock is not null && state.NativeArmySimulation?.Parameters is { } p &&
        p.Language == _language && p.ExecutableSha256 == _executableHash &&
        state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) == _source.ScenarioSource.Sha256 &&
        state.ContentHashes.GetValueOrDefault(_source.FixedDataSource.ResourceUri) == _source.FixedDataSource.Sha256;
}

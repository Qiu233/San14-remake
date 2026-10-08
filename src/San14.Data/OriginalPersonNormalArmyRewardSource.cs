using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using San14.Core;

namespace San14.Data;

public enum OriginalPersonNormalArmyRewardOutcome { Committed, NativeSkip, CurrentInputsUnresolved }
public sealed record OriginalPersonNormalArmyRewardRequests(int Kind, int Amount, int LeadershipGrowth, int PendingMerit);
public sealed record OriginalPersonNormalArmyRewardReceipt(int OfficerId, int? ArmyId,
    OriginalPersonNormalArmyRewardRequests Requests, OriginalPersonGrowthOutcome GrowthOutcome,
    OriginalPersonPendingMeritOutcome PendingOutcome, OriginalPersonGrowthReceipt? Growth,
    OriginalPersonPendingMeritReceipt? Pending, string Revision);
public sealed record OriginalPersonNormalArmyRewardResult(OriginalPersonNormalArmyRewardOutcome Outcome,
    GameState State, OriginalPersonNormalArmyRewardReceipt? Receipt, string? MissingPhase);

/// <summary>Actual SC2B9110/TC2B9290 Person receiver, current Army getter and
/// manager gate, followed by leadership growth then pending merit. The two
/// independently implemented native callbacks commit as one transaction.</summary>
public sealed class OriginalPersonNormalArmyRewardSource
{
    public const string ProfileId = "san14:ce-owned-normal-person-reward-20261003";
    public const string VersionKey = "ce-normal-person-reward-current:version";
    private static readonly int[] Globals = [0x1A4BDB4, 0x1A4BD50, 0x1A4BDC0, 0x1A4BD5C, 0x1A4BDCC, 0x1A4BD68, 0x1A4BDA8];
    public static string GlobalKey(int ordinal) => ordinal is >= 0 and < 7
        ? OriginalTacticRemakeParameterSeed.Key(Globals[ordinal]) : throw new ArgumentOutOfRangeException(nameof(ordinal));
    private readonly LoadedScenario _source;
    private readonly string _language, _hash;
    private readonly int[] _initial;
    private OriginalPersonNormalArmyRewardSource(LoadedScenario source, string language, string hash, int[] initial)
        => (_source, _language, _hash, _initial) = (source, language, hash, initial);

    public static OriginalPersonNormalArmyRewardSource Load(OriginalGameRepository repository, LoadedScenario source, string language)
    {
        string hash = language switch { "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,
            _ => throw new InvalidDataException("Normal Person reward needs SC/TC.") };
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != language)
            throw new InvalidDataException("Normal Person reward source is not the selected CE scenario.");
        byte[] exe = File.ReadAllBytes(Path.Combine(repository.SourceDirectory, $"SAN14CE_{language}.exe"));
        if (Convert.ToHexString(SHA256.HashData(exe)) != hash) throw new InvalidDataException("Normal reward EXE identity differs.");
        using var pe = new PEReader(new MemoryStream(exe, false));
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64) throw new InvalidDataException("Normal reward ABI differs.");
        int[] values = Globals.Select(semantic => {
            int rva = semantic - (language == "TC" ? 0x1030 : 0);
            var section = pe.PEHeaders.SectionHeaders.Single(s => s.Name == ".data" && rva >= s.VirtualAddress &&
                (long)rva + 4 <= (long)s.VirtualAddress + s.SizeOfRawData);
            return BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(section.PointerToRawData + rva - section.VirtualAddress, 4));
        }).ToArray();
        return new(source, language, hash, values);
    }

    public GameState InstallFresh(GameState input)
    {
        if (!Bound(input) || input.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || input.Units.Count != 0 ||
            input.NativeArmySimulation!.Parameters.Int32Values.ContainsKey(VersionKey) || Globals.Any(rva =>
                input.NativeArmySimulation.Parameters.Int32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(rva)) ||
                input.NativeArmySimulation.Parameters.Float32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(rva))))
            throw new InvalidDataException("Fresh normal reward cannot replace a saved current parameter bank.");
        var simulation = input.NativeArmySimulation!;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        for (int i = 0; i < Globals.Length; i++) values.Add(GlobalKey(i), _initial[i]);
        return input with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    public OriginalPersonNormalArmyRewardResult TryApply(GameState input, OriginalEventInitializationContext definitions,
        OriginalTacticRemakeCurrentContext current, OriginalOwnedArmyCampaignRules package,
        int personId, int kind, int amount) => Apply(input, definitions, current, package, personId, kind, amount, true);

    internal OriginalPersonNormalArmyRewardResult TryApplyWithinArmyOperation(GameState input, OriginalEventInitializationContext definitions,
        OriginalTacticRemakeCurrentContext current, OriginalOwnedArmyCampaignRules package,
        int personId, int kind, int amount) => Apply(input, definitions, current, package, personId, kind, amount, false);

    private OriginalPersonNormalArmyRewardResult Apply(GameState input, OriginalEventInitializationContext definitions,
        OriginalTacticRemakeCurrentContext current, OriginalOwnedArmyCampaignRules package,
        int personId, int kind, int amount, bool validateWorld)
    {
        if (!Bound(input) || !current.IsBoundCurrentState(input) || !input.Officers.TryGetValue(personId, out var person) ||
            person.NativeAbilityRuntime is not { } ability || ability.Language != _language || ability.ExecutableSha256 != _hash ||
            ability.Revision != OriginalTacticRemakeCurrentAbilities.ProfileId ||
            ability.SourceScenarioUri != _source.ScenarioSource.ResourceUri || ability.SourceScenarioSha256 != _source.ScenarioSource.Sha256 ||
            definitions.StaticSource?.ResourceUri != ability.SourceScenarioUri || definitions.StaticSource.Sha256 != ability.SourceScenarioSha256 ||
            definitions.ReadPersonLookupReference(personId)?.PhysicalSlot != ability.PhysicalSlot ||
            definitions.ReadPersonIdentity(ability.PhysicalSlot) != personId ||
            definitions.ReadPersonNativeValidity(ability.PhysicalSlot) is not bool valid)
            return Unknown("receiver");
        if (!valid) return Skip();
        if (definitions.ReadPersonFirstLocation(ability.PhysicalSlot) is not ushort first) return Unknown("first-location");
        if (first is >= 1 and <= 61)
        {
            if (definitions.ReadPersonForce(ability.PhysicalSlot)?.PhysicalSlot is not int force) return Unknown("special-force");
            if (force is >= 46 and <= 50) return Skip();
            if (new[] { 4111, 4131, 4151, 4171, 4191 }.Any(start => unchecked((uint)(personId - start)) <= 9)) return Skip();
        }
        //22A490(Person,1) decodes the independent saved second-location
        //word, selects the native Army slot and compares Commander12. A Core
        //Deployed flag cannot supply the missing source writer.
        if (person.NativeLocationRuntime?.Second11C is not ushort second ||
            definitions.ReadPersonSecondLocation(ability.PhysicalSlot) != second) return Unknown("second-location");
        OriginalNativeLocation location = OriginalPersonPositionRepair.DecodeSecondLocation(second);
        if (location.Kind != 27) return Skip(); //Real table0 invalid Army when second is not Army kind.
        UnitState[] matches = input.Units.Values.Where(u => u.NativeArmyOrigin?.NativeSlotIndex == location.Index).ToArray();
        if (matches.Length != 1) return Unknown("army-slot");
        UnitState army = matches[0];
        if (army.OfficerId != personId) return Skip(); //Real getter falls back to invalid Army0.
        if (current.ArmyValid(input, army) is not bool armyValid) return Unknown("army-validity");
        if (!armyValid) return Skip();
        if (current.ArmyManagerExcluded(input, army.Id) is not bool excluded) return Unknown("army-manager");
        if (excluded) return Skip();
        if (kind is < 0 or > 3) return Skip();
        if (ReadRequests(input, kind, amount) is not { } requests) return Unknown("request-parameters");
        return ApplyQualifiedRequests(input, personId, army.Id, requests,
            validateWorld ? package.TryApplyNativePersonGrowth : package.ApplyPersonGrowthDuringArmyOperation,
            validateWorld ? package.TryApplyNativePendingMerit : package.ApplyPendingMeritDuringArmyOperation,
            $"{ProfileId}:{input.NativeArmySimulation!.Parameters.Revision}", validateWorld);
        OriginalPersonNormalArmyRewardResult Unknown(string phase) => new(OriginalPersonNormalArmyRewardOutcome.CurrentInputsUnresolved, input, null, phase);
        OriginalPersonNormalArmyRewardResult Skip() => new(OriginalPersonNormalArmyRewardOutcome.NativeSkip, input, null, null);
    }

    /// <summary>Internal callback transaction for an already resolved native
    /// receiver and requests. It does not certify upstream Army eligibility.
    /// Scratch state protects the caller even when an explicit callback rejects
    /// after modifying its draft. Growth always precedes pending credit.</summary>
    public static OriginalPersonNormalArmyRewardResult TryApplyQualifiedRequests(GameState input, int personId, int? armyId,
        OriginalPersonNormalArmyRewardRequests requests,
        Func<GameState, int, int, int, int, OriginalPersonGrowthResult> growth,
        Func<GameState, int, int, OriginalPersonPendingMeritResult> pending, string revision) =>
        ApplyQualifiedRequests(input, personId, armyId, requests, growth, pending, revision, true);

    private static OriginalPersonNormalArmyRewardResult ApplyQualifiedRequests(GameState input, int personId, int? armyId,
        OriginalPersonNormalArmyRewardRequests requests,
        Func<GameState, int, int, int, int, OriginalPersonGrowthResult> growth,
        Func<GameState, int, int, OriginalPersonPendingMeritResult> pending, string revision, bool validateWorld)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(growth); ArgumentNullException.ThrowIfNull(pending);
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Qualified normal reward needs source revision.");
        GameState draft = JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(input))
            ?? throw new InvalidDataException("Normal reward state could not be detached.");
        var first = growth(draft, personId, 0, requests.LeadershipGrowth, 0);
        if (first.Outcome == OriginalPersonGrowthOutcome.CurrentInputsUnresolved) return Unknown("growth");
        var second = pending(first.State, personId, requests.PendingMerit);
        if (second.Outcome == OriginalPersonPendingMeritOutcome.CurrentInputsUnresolved) return Unknown("pending-merit");
        if (validateWorld) StateValidator.Validate(second.State);
        var receipt = new OriginalPersonNormalArmyRewardReceipt(personId, armyId, requests, first.Outcome, second.Outcome,
            first.Receipt, second.Receipt, revision);
        bool wrote = first.Outcome == OriginalPersonGrowthOutcome.Committed || second.Outcome == OriginalPersonPendingMeritOutcome.Committed;
        return new(wrote ? OriginalPersonNormalArmyRewardOutcome.Committed : OriginalPersonNormalArmyRewardOutcome.NativeSkip,
            wrote ? second.State : input, receipt, null);
        OriginalPersonNormalArmyRewardResult Unknown(string phase) => new(OriginalPersonNormalArmyRewardOutcome.CurrentInputsUnresolved, input, null, phase);
    }

    public OriginalPersonNormalArmyRewardRequests? ReadRequests(GameState input, int kind, int amount)
    {
        if (!Bound(input) || input.NativeArmySimulation!.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1 || kind is < 0 or > 3) return null;
        int growthIndex = kind switch { 0 => 0, 1 => 2, 2 => 4, _ => 6 };
        if (Read(growthIndex) is not int growth) return null;
        int merit = 0;
        if (kind < 3 && Read(kind * 2 + 1) is not int) return null;
        if (kind < 3) merit = Read(kind * 2 + 1)!.Value;
        return CalculateRequests(kind, amount, growth, merit);
        int? Read(int index)
        {
            var p = input.NativeArmySimulation!.Parameters; string key = GlobalKey(index);
            return !p.Float32Values.ContainsKey(key) && p.Int32Values.TryGetValue(key, out int value) ? value : null;
        }
    }

    public static OriginalPersonNormalArmyRewardRequests CalculateRequests(int kind, int amount, int growth, int merit)
        => kind switch {
            0 or 1 => new(kind, amount, unchecked(growth * amount) / 100,
                unchecked(unchecked(merit * amount) * 10) / 100),
            2 => new(kind, amount, growth, unchecked(merit * 10)),
            3 => new(kind, amount, growth, 0),
            _ => throw new InvalidDataException("Normal reward kind is outside the source switch.") };
    private bool Bound(GameState input) => input.NativeClock is not null && input.NativeArmySimulation?.Parameters is { } p &&
        p.Language == _language && p.ExecutableSha256 == _hash &&
        input.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) == _source.ScenarioSource.Sha256 &&
        input.ContentHashes.GetValueOrDefault(_source.FixedDataSource.ResourceUri) == _source.FixedDataSource.Sha256;
}

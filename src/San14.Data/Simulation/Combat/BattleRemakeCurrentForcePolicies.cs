using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Owned current CE Force policy personnel and Policy effect references.
/// The one fresh static projection is explicit; the policy bank is saved,
/// and the optional operation overload reads reached Person fields current.
/// Record-field keys preserve the serialized policy and personnel layouts.</summary>
public static class BattleRemakeCurrentForcePolicies
{
    public const string ProfileId = "san14:ce-owned-force-policy-bank-20261002";
    private const string Prefix = "ce-force-policy-current:";
    private const int Version = 1;
    private static string OwnerKey => $"{Prefix}profile-version";
    private static string ForceProfileKey(int id) => $"{Prefix}force:{id}:profile-version";
    private static string ForceIdentityKey(int id) => $"{Prefix}force:{id}:physical-slot";
    private static string PersonProfileKey(int id) => $"{Prefix}person:{id}:profile-version";
    private static string PersonIdentityKey(int id) => $"{Prefix}person:{id}:physical-slot";
    private static string PolicyProfileKey(int id) => $"{Prefix}policy:{id}:profile-version";

    public static string ForceFieldKey(int forceId, int nativeOffset) => $"{Prefix}force:{forceId}:field:{nativeOffset:X3}";
    public static string PersonFieldKey(int personId, int nativeOffset) => $"{Prefix}person:{personId}:field:{nativeOffset:X3}";
    public static string PolicyEffectSlotKey(int policyId, int ordinal) => $"{Prefix}policy:{policyId}:effect-slot:{ordinal}";
    public static string DepartmentPersonKey(int forceId, int group, int ordinal) => ForceFieldKey(forceId, 0x54 + group * 30 + ordinal * 2);
    public static string DepartmentFilterKey(int forceId, int group, int ordinal) => ForceFieldKey(forceId, 0x62 + group * 30 + ordinal * 2);
    public static string DepartmentTrailerKey(int forceId, int group) => ForceFieldKey(forceId, 0x70 + group * 30);

    /// <summary>Allocate a complete current owner after fresh native Person
    /// allocation. The CE329 source row includes an advisor and five29-byte
    /// department groups; each group expands to30 bytes in current native layout.
    /// No Core governance-slot mapping or later original initialization is assumed.</summary>
    public static GameState InstallFresh(GameState state, LoadedScenario source)
    {
        if (state.NativeArmySimulation is not { } simulation || !ProfileMatches(simulation.Parameters) ||
            state.Units.Count != 0 || state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != simulation.Parameters.Language ||
            source.Snapshot.Forces.Count != 52 || source.Snapshot.Characters.Count != 1851 ||
            source.Snapshot.PolicyParameters is not { FormatMarker: 0x66, PolicyRecordOffset: 239838,
                PolicyRecordStride: 148, Policies.Count: 121 } policies ||
            string.IsNullOrWhiteSpace(source.ScenarioSource.ResourceUri) || string.IsNullOrWhiteSpace(source.FixedDataSource.ResourceUri) ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            state.ContentHashes.GetValueOrDefault(source.FixedDataSource.ResourceUri) != source.FixedDataSource.Sha256 ||
            simulation.Parameters.Int32Values.Keys.Concat(simulation.Parameters.Float32Values.Keys)
                .Any(key => key.StartsWith(Prefix, StringComparison.Ordinal)))
            throw new InvalidDataException("Fresh owned Force policies cannot replace a saved or incomplete owner.");

        if (!source.Snapshot.Forces.Select(row => row.Slot).Order().SequenceEqual(Enumerable.Range(0, 52)) ||
            !source.Snapshot.Characters.Select(row => row.Slot).Order().SequenceEqual(Enumerable.Range(0, 1851)) ||
            !policies.Policies.Select(row => row.Slot).Order().SequenceEqual(Enumerable.Range(0, 121)) ||
            !state.Forces.Keys.Order().SequenceEqual(source.Snapshot.Forces.Where(row => row.Slot != 0).Select(row => row.Slot).Order()) ||
            !state.Officers.Keys.Order().SequenceEqual(source.Snapshot.Characters.Where(row => row.CharacterId != 0)
                .Select(row => row.CharacterId).Order()))
            throw new InvalidDataException("Current Force/Person/Policy identities do not match the complete CE source tables.");

        //Only this local clone is written. A late invalid row cannot modify the
        //input state, and no partial owner survives a failed static projection.
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        values.Add(OwnerKey, Version);
        foreach (ForcePreviewRecord row in source.Snapshot.Forces.Where(row => row.Slot != 0))
        {
            if (!state.Forces.TryGetValue(row.Slot, out ForceState? force) || force.Id != row.Slot ||
                force.RulerId != (row.RulerSlot == 0 ? null : (int?)row.RulerSlot) ||
                !simulation.Forces.ContainsKey(force.Id) || row.RemainingBytesHex.Length != 327 * 2)
                throw new InvalidDataException("Current Force identity or CE329 source row differs.");
            byte[] tail;
            try { tail = Convert.FromHexString(row.RemainingBytesHex); }
            catch (FormatException exception) { throw new InvalidDataException("CE Force source bytes are malformed.", exception); }
            var bytes = new byte[329];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, row.RulerSlot);
            tail.CopyTo(bytes, 2);
            values.Add(ForceProfileKey(force.Id), Version);
            values.Add(ForceIdentityKey(force.Id), row.Slot);
            Add(0x10, row.RulerSlot);
            Add(0x52, Word(59));
            Add(0x16C, Word(324));
            for (int group = 0; group < 5; group++)
            {
                for (int ordinal = 0; ordinal < 7; ordinal++)
                {
                    values.Add(DepartmentPersonKey(force.Id, group, ordinal), Word(61 + group * 29 + ordinal * 2));
                    values.Add(DepartmentFilterKey(force.Id, group, ordinal), Word(75 + group * 29 + ordinal * 2));
                }
                values.Add(DepartmentTrailerKey(force.Id, group), bytes[89 + group * 29]);
            }
            void Add(int offset, int value) => values.Add(ForceFieldKey(force.Id, offset), value);
            ushort Word(int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at, 2));
        }
        var identities = new OfficerSourceDirectory();
        identities.ClearLogicalReferences();
        foreach (var row in source.Snapshot.Characters)
        {
            identities.SetRecordIdentity(row.Slot, row.CharacterId);
            if (row.Slot == 0 || row.CharacterId != 0) identities.SetLogicalReference(row.CharacterId, row.Slot);
        }
        foreach (CharacterPreviewRecord row in source.Snapshot.Characters.Where(row => row.CharacterId != 0))
        {
            OfficerState person = state.Officers[row.CharacterId];
            if (person.Id != row.CharacterId || identities.ResolveOfficer(person.Id) != row.Slot ||
                row.Slot is < 1 or > 1850 || row.RawPolicySlot is not byte policy ||
                row.RawPolicyBaseCost is not byte rawBase || person.PolicyId != (policy is >= 1 and <= 120 ? policy : null) ||
                person.PolicyBaseCost != OfficerSourceNormalization.PolicyBaseCost(rawBase, 0x66))
                throw new InvalidDataException("Current Person policy/physical identity differs from its source allocation.");
            values.Add(PersonProfileKey(person.Id), Version);
            values.Add(PersonIdentityKey(person.Id), row.Slot);
            values.Add(PersonFieldKey(person.Id, 0x185), policy);
            values.Add(PersonFieldKey(person.Id, 0x1BA), person.PolicyBaseCost!.Value);
        }
        foreach (S14PolicyDefinition row in policies.Policies)
        {
            if (row.RawBytes is not { Length: 148 } || row.RawEffectSlots.Count != 8 ||
                !row.RawEffectSlots.SequenceEqual(row.RawBytes.AsSpan(136, 8).ToArray()))
                throw new InvalidDataException("Current CE Policy row has an incomplete eight-slot source.");
            values.Add(PolicyProfileKey(row.Slot), Version);
            for (int ordinal = 0; ordinal < 8; ordinal++)
                values.Add(PolicyEffectSlotKey(row.Slot, ordinal), row.RawEffectSlots[ordinal]);
        }
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };

    }

    public static int? ReadForceEffect10(GameState state, ForceState force) => ReadForceEffect(state, force, 10);

    /// <summary>SC41DC70/TC41D690's current ordered bank. A no-match does not
    /// consume its contribution. Matching policies use source-proved kind3
    /// zero branches; positive-count credit modifiers remain unresolved.
    /// Source files are checked at loading, never fingerprinted by this query.</summary>
    public static int? ReadForceEffect(GameState state, ForceState force, int nativeEffectIndex) =>
        ReadForceEffectCore(state, force, nativeEffectIndex, null);

    /// <summary>The same numeric policy bank with reached Person reads from
    /// the operation's current context. The current context does not own the
    /// kind3 credit-stage/counter fields, so only its proved status1/flags4000
    /// zero branches are supported when a contribution matches. It does not
    /// borrow saved credit values after a current Person writer.</summary>
    public static int? ReadForceEffect(GameState state, ForceState force, int nativeEffectIndex,
        SourceWorldContext current) =>
        ReadForceEffectCore(state, force, nativeEffectIndex, current);

    /// <summary>3130E0 returns the signed level, rather than the mapped
    /// CPolicyEffect value. Shares the same ordered personnel aggregation.</summary>
    public static int? ReadForceLevel(GameState state, ForceState force, int nativeEffectIndex) =>
        ReadForceRawLevelCore(state, force, nativeEffectIndex, null) is int raw ? Math.Min(raw, 10) : null;

    //29C430's advisor refresh precedes the freedom credit/status writers.
    //Its caller supplies the current published credit state, including any
    //earlier completed Person. No uncommitted credit writer lies between it
    //and this query; the existing general current overload remains narrower.
    internal static int? ReadForceRawLevelBeforeFreedomCurrent(GameState state, ForceState force,
        int nativeEffectIndex, SourceWorldContext current) =>
        ReadForceRawLevelCore(state, force, nativeEffectIndex, current, true);

    internal static int? MapEffectAtLevel(GameState state, int nativeEffectIndex, int rawLevel)
    {
        if (rawLevel <= 0) return 0;
        return state.PolicyEffects.TryGetValue(nativeEffectIndex, out var effectDefinition) &&
            effectDefinition.Id == nativeEffectIndex && effectDefinition.LevelValues is { Length: 10 } levels &&
            levels[Math.Min(rawLevel, 10) - 1] is >= 0 and <= ushort.MaxValue ? levels[Math.Min(rawLevel, 10) - 1] : null;
    }

    private static int? ReadForceEffectCore(GameState state, ForceState force, int nativeEffectIndex,
        SourceWorldContext? operationCurrent) =>
        ReadForceRawLevelCore(state, force, nativeEffectIndex, operationCurrent) is int raw
            ? MapEffectAtLevel(state, nativeEffectIndex, raw) : null;

    private static int? ReadForceRawLevelCore(GameState state, ForceState force, int nativeEffectIndex,
        SourceWorldContext? operationCurrent, bool unwrittenFreedomCredit = false)
    {
        if (nativeEffectIndex is < 1 or > 100 || state.NativeArmySimulation is not { } simulation || !ProfileMatches(simulation.Parameters) ||
            !state.Forces.TryGetValue(force.Id, out ForceState? current) || current.Id != force.Id ||
            !simulation.Forces.ContainsKey(force.Id)) return null;
        Dictionary<string, int> values = simulation.Parameters.Int32Values;
        if (values.GetValueOrDefault(OwnerKey) != Version || values.GetValueOrDefault(ForceProfileKey(force.Id)) != Version ||
            !values.TryGetValue(ForceIdentityKey(force.Id), out int physical) || physical != force.Id || physical is < 1 or > 51 ||
            ReadWord(0x10) is not ushort ruler) return null;
        if (!NativeValidity.Force(physical, ruler)!.Value) return 0;
        if (current.RulerId != ruler) return null;
        if (ReadWord(0x52) is not ushort advisor || ReadWord(0x16C) is not ushort advisorFilter) return null;

        int level = 0;
        if (!Consume(ruler) || advisorFilter == 0 && !Consume(advisor)) return null;
        for (int group = 0; group < 5; group++)
        {
            for (int ordinal = 0; ordinal < 7; ordinal++)
            {
                if (ReadWordKey(DepartmentPersonKey(force.Id, group, ordinal)) is not ushort person ||
                    ReadWordKey(DepartmentFilterKey(force.Id, group, ordinal)) is not ushort filter) return null;
                if (filter == 0 && !Consume(person)) return null;
            }
        }
        return level;

        ushort? ReadWord(int offset) => ReadWordKey(ForceFieldKey(force.Id, offset));
        ushort? ReadWordKey(string key) => values.TryGetValue(key, out int value) && value is >= 0 and <= 65535 ? (ushort)value : null;
        byte? ReadByte(string key) => values.TryGetValue(key, out int value) && value is >= 0 and <= 255 ? (byte)value : null;
        bool Consume(int personId)
        {
            //The native manager's zero reference is its invalid Person slot.
            if (personId == 0) return true;
            if (!state.Officers.TryGetValue(personId, out OfficerState? person) || person.Id != personId ||
                values.GetValueOrDefault(PersonProfileKey(personId)) != Version ||
                !values.TryGetValue(PersonIdentityKey(personId), out int personPhysical) || personPhysical is < 1 or > 1850) return false;
            if (operationCurrent is not null &&
                (operationCurrent.StaticSource is not { } source ||
                 state.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256 ||
                 operationCurrent.ResolveOfficerSource(person.Id) != personPhysical)) return false;
            bool? valid = operationCurrent is null
                ? NativeValidity.PersonWithBehavior(personPhysical, checked((ushort)person.Id),
                    person.Behavior, OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus))
                : operationCurrent.ReadPersonNativeValidity(personPhysical);
            if (valid is null) return false;
            if (!valid.Value) return true;
            if (ReadByte(PersonFieldKey(personId, 0x185)) is not byte rawPolicy ||
                person.PolicyId != (rawPolicy is >= 1 and <= 120 ? rawPolicy : null)) return false;
            int policyId = rawPolicy <= 120 ? rawPolicy : 0;
            if (values.GetValueOrDefault(PolicyProfileKey(policyId)) != Version) return false;
            if (policyId == 0) return true; //The actual sentinel is an invalid Policy.
            var effects = new List<int>(8);
            int matches = 0;
            for (int ordinal = 0; ordinal < 8; ordinal++)
            {
                if (ReadByte(PolicyEffectSlotKey(policyId, ordinal)) is not byte effect) return false;
                if (effect != 0) effects.Add(effect);
                if (effect == nativeEffectIndex) matches++;
            }
            //The typed definition is a view of these current native slots.
            //A writer must maintain both; repeats and order are meaningful.
            if (!state.Policies.TryGetValue(policyId, out PolicyDefinition? policy) ||
                policy.EffectIds is null || !policy.EffectIds.SequenceEqual(effects)) return false;
            if (matches == 0) return true;
            //Every early exit in2AA170/2A9FF0 is0 for kind3. These known
            //fields also make every remaining body path return0, without
            //guessing the unconsumed world/UI mode or static reward choices.
            bool zeroKind3 = operationCurrent is null
                ? person.ServiceStatus == OfficerServiceStatus.Ruler || person.Behavior?.UsesSpecialPersonRules == true ||
                    person.MeritState?.RankStageId == 0 || OfficerMeritRules.HasNoSelections(person.MeritState)
                : operationCurrent.ReadPersonStatus(personPhysical) == 1 ||
                    operationCurrent.ReadPersonBehavior(personPhysical)?.UsesSpecialPersonRules == true ||
                    unwrittenFreedomCredit && (person.MeritState?.RankStageId == 0 || OfficerMeritRules.HasNoSelections(person.MeritState));
            if (!zeroKind3 || ReadByte(PersonFieldKey(personId, 0x1BA)) is not byte basic || person.PolicyBaseCost != basic)
                return false;
            int contribution = Math.Min((int)basic, 10);
            for (int i = 0; i < matches; i++) level = Math.Max(unchecked(Math.Min(level, 10) + contribution), 0);
            return true;
        }
    }

    private static bool ProfileMatches(NativeArmyParameterState parameters) =>
        parameters.Language is ("SC" or "TC");
}

using San14.Core;

namespace San14.Data;

public sealed record OfficerCommandCapacityResult(int? Capacity, string? UnresolvedInput)
{
    public bool Available => Capacity.HasValue && UnresolvedInput is null;
}

public sealed record OfficerCommandBonusResult(int? Bonus, string? UnresolvedInput)
{
    public bool Available => Bonus.HasValue && UnresolvedInput is null;
}

/// <summary>Current Person command capacity, shared by list cells and draft
/// production. The caller supplies one already overlaid operation context with
/// the existing morale-wave office/rank readers bound. No world is mutated.</summary>
internal interface IOfficerCommandCapacityInputs
{
    int? ReadForceRank(int forcePhysical);
    int? ReadOffice(int personPhysical);
    int? ReadOfficeCapacity(int office);
    int? ReadParameter(string key);
    bool? ReadPolicyEnabled(int forcePhysical, int effect);
    int? ReadPolicyEffect(ForceReference force, int effect);
    OfficerCommandBonusResult ReadCreditBonus(int personPhysical);
}

public static class OfficerCommandCapacity
{
    public static OfficerCommandCapacityResult Resolve(GameState state, SourceWorldContext current, int personPhysical)
        => Resolve(state, current, personPhysical, new SavedCapacityInputs(state, current));

    //Normal pre-start callers lend their actual read-only constructor/static
    //inputs. The capacity formula and every early exit remain shared.
    internal static OfficerCommandCapacityResult Resolve(GameState state, SourceWorldContext current, int personPhysical,
        IOfficerCommandCapacityInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(inputs);
        try
        {
            if (!Need(current.ReadPersonNativeValidity(personPhysical), "person_validity")) return new(0, null);
            int personId = Need(current.ReadPersonIdentity(personPhysical), "person_identity");
            if (!state.Officers.TryGetValue(personId, out var person) || current.ResolveOfficerSource(personId) != personPhysical)
                throw Unknown("person_owner");
            byte status = Need(current.ReadPersonStatus(personPhysical), "person_status");
            var forceReference = current.ReadPersonForce(personPhysical) ?? throw Unknown("person_force");
            int forcePhysical = Need(forceReference.PhysicalSlot, "force_pointer");
            int capacity;
            if (status == 1)
            {
                int rank = Need(inputs.ReadForceRank(forcePhysical), "force_rank");
                rank = rank < 13 ? rank : 0;
                capacity = state.ForceRanks.GetValueOrDefault(rank)?.CommandLimit is int value && value is >= 0 and <= 65535
                    ? value : throw Unknown("rank_capacity");
            }
            else
            {
                int post = Need(inputs.ReadOffice(personPhysical), "person_office");
                capacity = Need(inputs.ReadOfficeCapacity(post), "office_capacity");
            }
            ushort firstLocation = Need(current.ReadPersonFirstLocation(personPhysical), "first_location");
            bool specialForce = firstLocation is >= 1 and <= 61 && forcePhysical is >= 46 and <= 50;
            bool specialPerson = (unchecked((uint)(personId - 4111)) <= 9 || unchecked((uint)(personId - 4131)) <= 9 ||
                unchecked((uint)(personId - 4151)) <= 9 || unchecked((uint)(personId - 4171)) <= 9 ||
                unchecked((uint)(personId - 4191)) <= 9) && firstLocation is >= 1 and <= 61 && forcePhysical is not (>= 46 and <= 50);
            if (specialForce || specialPerson)
            {
                capacity = Need(inputs.ReadParameter(SimulationParameterKeys.Tactics.SpecialOfficerCommandCapacity),
                    "special_capacity_parameter");
            }
            else
            {
                var traits = TacticRemakeCurrentTraits.CommanderCapacityTraits(current, personPhysical)
                    ?? throw Unknown("current_personalities");
                int coefficient = Need(inputs.ReadParameter(SimulationParameterKeys.Tactics.HealingCapacityCoefficient),
                    "personality_coefficient");
                capacity = unchecked(Math.Min(unchecked(capacity + traits.Group256To355 * coefficient), 20000) + traits.OtherGroup * coefficient);
                if (status != 1)
                {
                    var bonus = inputs.ReadCreditBonus(personPhysical);
                    capacity = unchecked(capacity + (bonus.Bonus ?? throw new InvalidDataException(bonus.UnresolvedInput)));
                }
            }
            if (Need(current.ReadForceNativeValidity(forceReference), "force_validity"))
            {
                bool enabled = Need(inputs.ReadPolicyEnabled(forcePhysical, 0x37), "reform_activation");
                int policy = enabled ? Need(inputs.ReadPolicyEffect(forceReference, 0x37), "active_reform_effect") : 0;
                capacity = unchecked(capacity + policy);
            }
            return new(capacity, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private sealed class SavedCapacityInputs(GameState state, SourceWorldContext current) : IOfficerCommandCapacityInputs
    {
        public int? ReadForceRank(int forcePhysical) => current.ReadMoraleWaveForceRank47(forcePhysical);
        public int? ReadOffice(int personPhysical) => current.ReadMoraleWavePersonPostE7(personPhysical);
        public int? ReadOfficeCapacity(int office) => OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, office);
        public int? ReadParameter(string key)
        {
            var parameters = state.NativeArmySimulation?.Parameters;
            if (parameters is null || key == SimulationParameterKeys.Tactics.SpecialOfficerCommandCapacity &&
                parameters.Float32Values.ContainsKey(key)) return null;
            return parameters.Int32Values.TryGetValue(key, out int value) ? value : null;
        }
        public bool? ReadPolicyEnabled(int forcePhysical, int effect)
        {
            if (!state.Forces.TryGetValue(forcePhysical, out var force)) throw Unknown("force_owner");
            return force.PolicyEffects?.IsEnabled(effect);
        }
        public int? ReadPolicyEffect(ForceReference force, int effect) => current.BaseForceModifierReader?.Invoke(force, effect);
        public OfficerCommandBonusResult ReadCreditBonus(int personPhysical)
            => ResolveCreditBonus(state, current, personPhysical);
    }

    private static T Need<T>(T? value, string input) where T : struct => value ?? throw Unknown(input);
    private static InvalidDataException Unknown(string input) => new($"officer.command-capacity.{input}");

    /// <summary>2AA170(Person,1), also used by list emphasis. It is independent
    /// of the office/rank base and does not substitute the total capacity.</summary>
    public static OfficerCommandBonusResult ResolveCreditBonus(GameState state, SourceWorldContext current, int personPhysical) =>
        ResolveCreditKindBonus(state, current, personPhysical, 1);

    //Only the actual selector kind1/kind3 consumers share this wrapper. Kind2
    //retains its existing appointment owner and ruler-specific result.
    internal static OfficerCommandBonusResult ResolveCreditKindBonus(GameState state, SourceWorldContext current,
        int personPhysical, int kind, bool? userSelectionServicePresent = null)
    {
        if (kind is not (1 or 3)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(current);
        try
        {
            if (!Need(current.ReadPersonNativeValidity(personPhysical), "person_validity")) return new(0, null);
            if (Need(current.ReadPersonStatus(personPhysical), "person_status") == 1) return new(0, null);
            int id = Need(current.ReadPersonIdentity(personPhysical), "person_identity");
            if (!state.Officers.TryGetValue(id, out var person) || current.ResolveOfficerSource(id) != personPhysical) throw Unknown("person_owner");
            var force = current.ReadPersonForce(personPhysical) ?? throw Unknown("person_force");
            int forcePhysical = Need(force.PhysicalSlot, "force_pointer");
            if (forcePhysical is >= 46 and <= 50) return new(0, null);
            bool band = unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
                unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 || unchecked((uint)(id - 4191)) <= 9;
            if (band && Need(current.ReadPersonFirstLocation(personPhysical), "first_location") is >= 1 and <= 61) return new(0, null);
            ushort flags = Need(current.ReadArmyReturnPersonFlags196(personPhysical), "person_flags");
            // All wrapper early exits also return zero. These saved branches
            // prove the result without inventing an unused app-state owner.
            if ((flags & 0x4000) != 0 || person.MeritState?.RankStageId == 0 || OfficerMeritRules.HasNoSelections(person.MeritState)) return new(0, null);
            return new(ReadCreditSelectionBonus(state, person, kind, userSelectionServicePresent), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    internal static int ReadCreditSelectionBonus(GameState state, OfficerState person, int kind,
        bool? userSelectionServicePresent = null)
    {
        //2AA170's current-user check cannot suppress this call when BC=-1,
        //regardless of the external manager pointer. A different current user
        //or the custom-person-selection mode needs its actual app-state owner.
        if (userSelectionServicePresent is true)
        {
            if (state.CampaignEventWorld?.CurrentUserIndex is not sbyte selection) throw Unknown("credit_current_user_gate");
            if (selection != -1) return 0;
        }
        else if (userSelectionServicePresent is null && state.CampaignEventWorld?.CurrentUserIndex != -1)
            throw Unknown("credit_current_user_gate");
        if (state.CampaignEventWorld?.ScenarioMode is not int mode) throw Unknown("credit_scenario_mode");
        if (mode == 3) throw Unknown("credit_person_selection_mode");
        return SumCreditSelections(person.MeritState,kind,
            slot=>OwnedArmyMoraleWaveInputs.ReadCreditEffectKind(state,slot),
            slot=>OwnedArmyMoraleWaveInputs.ReadCreditEffectAddition(state,slot));
    }

    internal static int SumCreditSelections(OfficerMeritState? credit,int kind,
        Func<int,byte?> effectKind,Func<int,ushort?> effectAddition)
    {
        byte stage = Need(OfficerMeritSourceAdapter.EncodeStage(credit), "credit_stage");
        if (stage == 0) return 0;
        int first = Need(OfficerMeritSourceAdapter.EncodeFirstSelectionCount(credit), "credit_first_count");
        int second = Need(OfficerMeritSourceAdapter.EncodeSecondSelectionCount(credit), "credit_second_count");
        int sum = 0;
        for (int i = 0; i < first; i++) Add(i + 1 < 7 ? i + 1 : 0);
        for (int i = 0; i < second; i++) Add(i + 4 < 7 ? i + 4 : 0);
        return sum;
        void Add(int slot)
        {
            if (Need(effectKind(slot), "credit_effect_kind") == kind)
                sum = unchecked(sum + Need(effectAddition(slot), "credit_effect_addition"));
        }
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public OfficerCommandCapacityResult QueryOfficerCommandCapacity(GameState state, int officerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_definitions.ResolveOfficerSource(officerId) is not int physical)
            return new(null, "officer.command-capacity.person_identity");
        try
        {
            return OfficerCommandCapacity.Resolve(state, CaptureOfficerCommandCapacityCurrent(state), physical);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    internal SourceWorldContext CaptureOfficerCommandCapacityCurrent(GameState state)
    {
        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        _moraleWaveInputs.BindCurrent(state, current);
        _baseCapacityInputs.BindCurrent(state, current);
        return current;
    }

    public IReadOnlyDictionary<int, OfficerCommandCapacityResult> QueryOfficerCommandCapacities(GameState state,
        IReadOnlyList<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(officerIds);
        var results = new Dictionary<int, OfficerCommandCapacityResult>();
        if (officerIds.Count != 0)
        {
            SourceWorldContext? current = null; string? unresolved = null;
            try { current = CaptureOfficerCommandCapacityCurrent(state); }
            catch (InvalidDataException error) { unresolved = error.Message; }
            foreach (int id in officerIds)
                if (!results.ContainsKey(id))
                    results.Add(id, current is null ? new(null, unresolved) :
                        _definitions.ResolveOfficerSource(id) is int physical ? OfficerCommandCapacity.Resolve(state, current, physical) :
                        new(null, "officer.command-capacity.person_identity"));
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int, OfficerCommandCapacityResult>(results);
    }
}

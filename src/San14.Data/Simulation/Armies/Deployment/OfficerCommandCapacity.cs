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
public static class OfficerCommandCapacity
{
    public static OfficerCommandCapacityResult Resolve(GameState state, SourceWorldContext current, int personPhysical)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(current);
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
                int rank = Need(current.ReadMoraleWaveForceRank47(forcePhysical), "force_rank");
                rank = rank < 13 ? rank : 0;
                capacity = state.ForceRanks.GetValueOrDefault(rank)?.CommandLimit is int value && value is >= 0 and <= 65535
                    ? value : throw Unknown("rank_capacity");
            }
            else
            {
                int post = Need(current.ReadMoraleWavePersonPostE7(personPhysical), "person_office");
                capacity = Need(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, post), "office_capacity");
            }
            ushort firstLocation = Need(current.ReadPersonFirstLocation(personPhysical), "first_location");
            bool specialForce = firstLocation is >= 1 and <= 61 && forcePhysical is >= 46 and <= 50;
            bool specialPerson = (unchecked((uint)(personId - 4111)) <= 9 || unchecked((uint)(personId - 4131)) <= 9 ||
                unchecked((uint)(personId - 4151)) <= 9 || unchecked((uint)(personId - 4171)) <= 9 ||
                unchecked((uint)(personId - 4191)) <= 9) && firstLocation is >= 1 and <= 61 && forcePhysical is not (>= 46 and <= 50);
            if (specialForce || specialPerson) throw Unknown("special_capacity_parameter");
            var traits = TacticRemakeCurrentTraits.CommanderCapacityTraits(current, personPhysical)
                ?? throw Unknown("current_personalities");
            if (state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(SimulationParameterKeys.Tactics.HealingCapacityCoefficient, out int coefficient) != true)
                throw Unknown("personality_coefficient");
            capacity = unchecked(Math.Min(unchecked(capacity + traits.Group256To355 * coefficient), 20000) + traits.OtherGroup * coefficient);
            if (status != 1)
            {
                var bonus = ResolveCreditBonus(state, current, personPhysical);
                capacity = unchecked(capacity + (bonus.Bonus ?? throw new InvalidDataException(bonus.UnresolvedInput)));
            }
            if (Need(current.ReadForceNativeValidity(forceReference), "force_validity"))
            {
                if (!state.Forces.TryGetValue(forcePhysical, out var force)) throw Unknown("force_owner");
                bool enabled = Need(force.PolicyEffects?.IsEnabled(0x37), "reform_activation");
                int policy = enabled ? Need(current.BaseForceModifierReader?.Invoke(forceReference, 0x37), "active_reform_effect") : 0;
                capacity = unchecked(capacity + policy);
            }
            return new(capacity, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
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
        int personPhysical, int kind)
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
            return new(ReadCreditSelectionBonus(state, person, kind), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    internal static int ReadCreditSelectionBonus(GameState state, OfficerState person, int kind)
    {
        //2AA170's current-user check cannot suppress this call when BC=-1,
        //regardless of the external manager pointer. A different current user
        //or the custom-person-selection mode needs its actual app-state owner.
        if (state.CampaignEventWorld?.CurrentUserIndex != -1) throw Unknown("credit_current_user_gate");
        if (state.CampaignEventWorld.ScenarioMode == 3) throw Unknown("credit_person_selection_mode");
        byte stage = Need(OfficerMeritSourceAdapter.EncodeStage(person.MeritState), "credit_stage");
        if (stage == 0) return 0;
        int first = Need(OfficerMeritSourceAdapter.EncodeFirstSelectionCount(person.MeritState), "credit_first_count");
        int second = Need(OfficerMeritSourceAdapter.EncodeSecondSelectionCount(person.MeritState), "credit_second_count");
        int sum = 0;
        for (int i = 0; i < first; i++) Add(i + 1 < 7 ? i + 1 : 0);
        for (int i = 0; i < second; i++) Add(i + 4 < 7 ? i + 4 : 0);
        return sum;
        void Add(int slot)
        {
            if (Need(OwnedArmyMoraleWaveInputs.ReadCreditEffectKind(state, slot), "credit_effect_kind") == kind)
                sum = unchecked(sum + Need(OwnedArmyMoraleWaveInputs.ReadCreditEffectAddition(state, slot), "credit_effect_addition"));
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

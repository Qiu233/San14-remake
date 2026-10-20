using San14.Core;

namespace San14.Data;

public sealed partial class CityDomesticDraft
{
    public AreaAdministrationQuery<CityDomesticPolicyState> QueryPolicy()
    {
        try { EnsureOpen(); return AreaAdministrationQuery<CityDomesticPolicyState>.Known(_policyState! with
            { Requested = _policy, Effective = AreaAdministrationQuery<CityDomesticPolicy?>.Known(_policy) }); }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<CityDomesticPolicyState>.Unknown(unknown.Message); }
    }
    private void RefreshPolicy()
    {
        bool any = false, commerce = true, agriculture = true, barracks = true;
        foreach (int area in _held)
        {
            if (K(_current.ReadPersonNativeValidity(PersonAreaAppointmentSource.Resolve(_current, Person(area))), "policy_person_validity")) any = true;
            var row = _state.Areas.GetValueOrDefault(area)?.Development ?? throw Missing("policy_progress");
            commerce &= Progress(row.Commerce, "commerce") >= 1000;
            agriculture &= Progress(row.Agriculture, "agriculture") >= 1000;
            barracks &= Progress(row.Barracks, "barracks") >= 1000;
        }
        //Node120's old mask4 is Enabled. Each no-person refresh overwrites
        //cache510, even if the current selection is already Current.
        if (!any) { _cachedPolicy = _policy; _policy = CityDomesticPolicy.Current; }
        else if (!_delegatedEnabled) _policy = _cachedPolicy;
        _delegatedEnabled = any;
        _policyState = new(_policy, AreaAdministrationQuery<CityDomesticPolicy?>.Known(_policy),
            Known(any && !commerce), Known(any && !agriculture), Known(any && !barracks), Known(any))
            { CommerceComplete = Known(commerce), AgricultureComplete = Known(agriculture), BarracksComplete = Known(barracks) };
        static int Progress(int? value, string name) => value is >= 0 and <= 65535 ? value.Value : throw Missing("policy_" + name);
    }
    public AreaAdministrationQuery<IReadOnlyList<CityDomesticCandidate>> QueryCandidates(CityDomesticDuty duty)
    {
        try
        {
            EnsureOpen(); CheckDuty(duty);
            var rows = AreaAdministrationExecution.OrderedCandidates(_current, CityId);
            AreaAdministrationAutomaticOrder.SortAreas(rows, (a, b) =>
            {
                int av = Ability(a.Physical, 1);
                if (duty == CityDomesticDuty.Trainer) av = unchecked(av + Ability(a.Physical, 0));
                int bv = Ability(b.Physical, 1);
                if (duty == CityDomesticDuty.Trainer) bv = unchecked(bv + Ability(b.Physical, 0));
                return av > bv;
            });
            return AreaAdministrationQuery<IReadOnlyList<CityDomesticCandidate>>.Known(rows.Select(x => new CityDomesticCandidate(x.Identity, x.Physical)).ToArray());
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<IReadOnlyList<CityDomesticCandidate>>.Unknown(unknown.Message); }
    }
    public AreaAdministrationQuery<string?> QueryTraitTooltip(int lookupTraitId)
    {
        try { EnsureOpen(); return _card.ReadTraitTooltip(lookupTraitId); }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<string?>.Unknown(unknown.Message); }
    }
    public AreaAdministrationQuery<CityDomesticDutyView> QueryDuty(CityDomesticDuty duty)
    {
        try
        {
            EnsureOpen(); CheckDuty(duty); ushort raw = RoleRaw(duty);
            int physical = PersonAreaAppointmentSource.Resolve(_current, raw);
            bool valid = K(_current.ReadPersonNativeValidity(physical), "duty_person_validity");
            AreaAdministrationQuery<AreaAdministrationOfficerCard> card;
            try
            {
                //The reused card's AreaId is known not applicable to this city page.
                //Identity belongs to the enclosing CityId/Duty wrapper; do not
                //add a map lookup which the source card never consumes.
                card = AreaAdministrationQuery<AreaAdministrationOfficerCard>.Known(_card.ReadCity(Revision, physical, valid,
                    duty == CityDomesticDuty.Recruiter ? 13 : 14));
            }
            catch (InvalidDataException unknown) { card = AreaAdministrationQuery<AreaAdministrationOfficerCard>.Unknown(unknown.Message); }
            var fee = ReadInt(() => valid ? duty == CityDomesticDuty.Recruiter ? unchecked(Fee(City(), true) * _held.Length) : Fee(City(), false) : 0);
            var before = ReadInt(() => duty == CityDomesticDuty.Recruiter ? Troops(City()) : Morale(City()));
            var after = ReadInt(() =>
            {
                int cost = Required(fee), old = Required(before), gain = 0;
                if (cost <= Gold(City()))
                {
                    if (duty == CityDomesticDuty.Recruiter)
                    {
                        //The shared area consumer binds current CA. Preserve the
                        //city caller's additional identity lookup; unsupported
                        //nonidentity chaining stays Unknown rather than reading
                        //the wrong recruiter's abilities.
                        int identity = K(_current.ReadPersonIdentity(physical), "recruit_identity");
                        if (PersonAreaAppointmentSource.Resolve(_current, checked((ushort)identity)) != physical)
                            throw Missing("recruit_identity_lookup_alias");
                        foreach (int area in _held) gain = unchecked(gain + Required(_preview.Soldiers(area)));
                    }
                    else gain = TrainingGain(City());
                }
                int cap = duty == CityDomesticDuty.Recruiter ? TroopCap(City()) : MoraleCap(City());
                int sum = unchecked(old + gain); return sum < 0 ? 0 : Math.Min(sum, cap);
            });
            return AreaAdministrationQuery<CityDomesticDutyView>.Known(new(CityId, Revision, duty, Known((int?)raw), Known((int?)physical),
                Known(valid), card, valid ? fee : AreaAdministrationQuery<int?>.Known(null), before, after));
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<CityDomesticDutyView>.Unknown(unknown.Message); }
    }
    /// <summary>One page publication; duties are each read once. UI can retain
    /// this immutable result through ordinary redraws.</summary>
    public AreaAdministrationQuery<CityDomesticPage> QueryPage()
    {
        try
        {
            EnsureOpen();
            //Original refresh reads recruiter then trainer, books then warning.
            var recruiter = QueryDuty(CityDomesticDuty.Recruiter);
            var trainer = QueryDuty(CityDomesticDuty.Trainer);
            var books = CommandBooks();
            var warning = ReadBool(() =>
            {
                int value = unchecked(Gold(City()) - DutyCost(trainer));
                int day = _state.NativeMapSeason?.ThirdDate?.Day ?? throw Missing("world_third_day");
                if (day is >= 21 and <= 30) value = unchecked(value - DutyCost(recruiter));
                return value < 0;
            });
            var summary = new CityDomesticSummary(CityId, _city.Name, Revision, ReadInt(() => Gold(City())), warning, books);
            return AreaAdministrationQuery<CityDomesticPage>.Known(new(summary, recruiter, trainer, QueryPolicy()));
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<CityDomesticPage>.Unknown(unknown.Message); }
        static int DutyCost(AreaAdministrationQuery<CityDomesticDutyView> query)
        {
            if (!query.Available) throw new InvalidDataException(query.UnresolvedInput);
            var fee = query.Value!.Fee;
            if (!fee.Available) throw new InvalidDataException(fee.UnresolvedInput);
            return fee.Value ?? 0; //Known(null) is source-invalid's blank fee/zero cache.
        }
    }
    public AreaAdministrationQuery<CityDomesticSummary> QuerySummary()
    {
        var page = QueryPage();
        return page.Available ? AreaAdministrationQuery<CityDomesticSummary>.Known(page.Value!.Summary)
            : AreaAdministrationQuery<CityDomesticSummary>.Unknown(page.UnresolvedInput!);
    }
    /// <summary>Radio's books-only consumer. Does not re-read duties, gold,
    /// held progress, training gain, abilities or traits.</summary>
    public AreaAdministrationQuery<CityDomesticCommandBooks> QueryCommandBooks()
    {
        try { EnsureOpen(); return AreaAdministrationQuery<CityDomesticCommandBooks>.Known(CommandBooks()); }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<CityDomesticCommandBooks>.Unknown(unknown.Message); }
    }
    private CityDomesticCommandBooks CommandBooks()
    {
        var current = ReadInt(() =>
        {
            var district = _current.ReadCityDistrictReference(City()) ?? throw Missing("books_district_pointer");
            int id = K(district.PhysicalSlot, "books_district_identity");
            if (_city.DistrictId != id) throw Missing("books_district_binding");
            return CurrentBooks(id);
        });
        var required = ReadInt(() =>
        {
            _ = Required(current); //The district book byte is the original prefix.
            ushort recruiterRaw = RoleRaw(CityDomesticDuty.Recruiter);
            int sum = _policy != CityDomesticPolicy.Current ? Parameter("policyCommandCost") : 0;
            int recruiter = PersonAreaAppointmentSource.Resolve(_current, recruiterRaw);
            if (K(_current.ReadPersonIdentity(recruiter), "books_recruiter_identity") != _original.Recruiter)
                sum = unchecked(sum + Parameter("recruiterCommandCost"));
            int trainer = PersonAreaAppointmentSource.Resolve(_current, RoleRaw(CityDomesticDuty.Trainer));
            if (K(_current.ReadPersonIdentity(trainer), "books_trainer_identity") != _original.Trainer)
                sum = unchecked(sum + Parameter("trainerCommandCost"));
            return sum;
        });
        return new(current, required, ReadInt(() => Math.Max(0, unchecked(Required(current) - Required(required)))),
            ReadBool(() => { int cost = Required(required); return cost != 0 && Required(current) >= cost; }));
    }
    private int Fee(BaseReference city, bool recruit)
    {
        var force = Force(city);
        int basis = Parameter(recruit ? "recruitBaseCost" : "trainingBaseCost");
        if (recruit) basis = unchecked(basis * 3);
        int effect = Policy(force, 38);
        int scale = Parameter("costPolicyScale");
        float discount = (float)((float)unchecked(scale * effect) * (float)basis) / 100f;
        int reduction = discount <= 0f ? 0 : discount < 1f ? 1 : Cvtt(discount);
        return Math.Max(0, unchecked(basis - reduction));
    }
    private int TrainingGain(BaseReference original)
    {
        var associated = CurrentGateMapQueries.FindCity(original, _current);
        int id = K(associated.PhysicalSlot, "training_associated_city");
        ushort raw = K(_current.ReadAssignmentCityCC(id), "training_associated_trainer");
        int trainer = PersonAreaAppointmentSource.Resolve(_current, raw);
        if (!K(_current.ReadPersonNativeValidity(trainer), "training_trainer_validity")) return 0;
        int fee = Fee(associated, false);
        if (fee > Gold(associated)) return 0;
        if (raw == 0) return 0;
        int morale = Morale(original), upper = MoraleCap(original);
        if (morale >= upper) return 0;
        if (Troops(original) == 0) return 0;
        return TrainingInner(original, trainer);
    }
    private int TrainingInner(BaseReference city, int trainer)
    {
        int initialMorale = Morale(city); if (initialMorale >= 200) return 0;
        int troops = Troops(city); if (troops == 0) return 0;
        ushort flags = K(OfficerBehaviorSourceAdapter.EncodeFlags(_current.ReadPersonBehavior(trainer)), "training_flags");
        bool half = (flags & 1) != 0;
        if (!half)
        {
            half = K(PersonArmyLocationSource.ReadCommanderArmyValidity(_state, _current, trainer), "training_army_validity");
            if (!half)
            {
                var linked = PersonLinkedEntityLookup.Find(trainer, _preview);
                half = K(_preview.ReadLinkedEntityNativeValidity(linked), "training_linked_validity");
            }
        }
        int leadership = Ability(trainer, 0); if (half) leadership /= 2;
        int strength = Ability(trainer, 1); if (half) strength /= 2;
        int baseParameter = Parameter("trainingBaseGain");
        int sqrtMorale = Morale(city);
        int sqrtScale = Parameter("trainingMoraleSqrtScale");
        float sqrt = MathF.Sqrt((float)unchecked(sqrtMorale * sqrtScale));
        int divisor = Parameter("trainingTroopsDivisor");
        float troopPart = (float)troops / (float)divisor;
        float firstSum = (float)(sqrt + troopPart);
        int offset = Parameter("trainingDenominatorOffset");
        int rawDenominator = Cvtt((float)(firstSum + (float)offset));
        _ = MoraleCap(city);
        int traitScale = Parameter("trainingTraitScale");
        int denominator = Math.Max(1, rawDenominator);
        int basis = unchecked(baseParameter + unchecked(unchecked(leadership + strength) * 2) / denominator);
        int trait = K(TacticRemakeCurrentTraits.CommanderEffectMaximum(_current, trainer, 16), "training_trait16");
        int traitGain = unchecked(unchecked(trait * traitScale) * basis) / 100;
        var force = _current.ReadPersonForce(trainer) ?? throw Missing("training_person_force");
        int policy = Policy(force, 31);
        int scale = Parameter("trainingPolicyScale");
        int policyGain = unchecked(unchecked(scale * policy) * basis) / 100;
        return unchecked(unchecked(policyGain + traitGain) + basis);
    }
    private int Ability(int physical, int dimension)
    {
        int identity = K(_current.ReadPersonIdentity(physical), "ability_identity");
        if (_definitions.ResolveOfficerSource(identity) != physical) throw Missing("ability_owner_binding");
        var abilities = new TacticRemakeCurrentAbilities(_current, _scenario, _language, SimulationRuleIdentity.ForLocale(_language));
        return K(abilities.ResolveDimension(_state, identity, dimension, (force, effect) => _panels.ReadPanelModifier(_state, force, effect)), "ability." + dimension);
    }
    private int Policy(ForceReference force, int effect)
    {
        if (!K(_current.ReadForceNativeValidity(force), "policy_force_validity")) return 0;
        var owner = _state.Forces.GetValueOrDefault(K(force.PhysicalSlot, "policy_force_identity")) ?? throw Missing("policy_force_owner");
        return K(BattleRemakeCurrentForcePolicies.ReadForceEffect(_state, owner, effect, _current), "policy_effect." + effect);
    }
    private int Parameter(string name) => K(CityDomesticParameters.Read(_state, name), "parameter." + name);
    private int Gold(BaseReference city) => K(_current.ReadBaseResource(city, BaseResourceKind.Gold), "gold");
    private int Troops(BaseReference city) => K(_current.ReadBaseResource(city, BaseResourceKind.Troops), "troops");
    private int Morale(BaseReference city) => K(_current.ReadBaseMorale44(city), "morale");
    private int TroopCap(BaseReference city)
    { if (!_capacityBound) throw Missing("capacity_inputs"); return BasePostLoad.ReadCapacities(city, _current).Troops; }
    private int MoraleCap(BaseReference city)
    { if (!_capacityBound) throw Missing("capacity_inputs"); return BasePostLoad.ReadMoraleUpper(Force(city), _current); }
    private static int Cvtt(float value) => value >= 2147483648f || value < -2147483648f || float.IsNaN(value) ? int.MinValue : (int)value;
    private static int Required(AreaAdministrationQuery<int?> field) => field.Available && field.Value is int value ? value : throw new InvalidDataException(field.UnresolvedInput ?? "city-domestic.required_value");
    private static AreaAdministrationQuery<int?> Known(int? value) => AreaAdministrationQuery<int?>.Known(value);
    private static AreaAdministrationQuery<bool?> Known(bool value) => AreaAdministrationQuery<bool?>.Known(value);
    private static AreaAdministrationQuery<int?> ReadInt(Func<int> read)
    { try { return Known(read()); } catch (InvalidDataException unknown) { return AreaAdministrationQuery<int?>.Unknown(unknown.Message); } }
    private static AreaAdministrationQuery<bool?> ReadBool(Func<bool> read)
    { try { return Known(read()); } catch (InvalidDataException unknown) { return AreaAdministrationQuery<bool?>.Unknown(unknown.Message); } }
}

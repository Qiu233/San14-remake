using System.Collections.Immutable;
using System.Numerics;
using San14.Core;

namespace San14.Data;

internal sealed record PersonnelHireForceEconomy(int MonthlyGoldIncome, int MonthlyFoodIncome,
    int GoldExpense, int MonthlyTroopRecruitment);

/// <summary>The mutable permutations belonging to one complete template refresh.
/// Publishing replaces the saved candidate pools; it never mutates saved arrays.</summary>
internal sealed class PersonnelHireGenerationSession
{
    internal int GeneratedCount;
    internal List<int> Types;
    internal List<int> Formations;
    internal List<int>[][] Traits;
    internal List<int>[] Tactics;

    internal PersonnelHireGenerationSession(PersonnelHireGenerationCatalog catalog)
    {
        Types = catalog.TypeCodes.ToList(); Formations = catalog.FormationIds.ToList();
        Traits = Enumerable.Range(0, 5).Select(type => new[] {
            catalog.TraitIds(type, 0).ToList(), catalog.TraitIds(type, 1).ToList() }).ToArray();
        Tactics = Enumerable.Range(0, 5).Select(type => catalog.TacticIds(type).ToList()).ToArray();
    }

    internal PersonnelHireGenerationSession(PersonnelHireGenerationPools saved)
    {
        Types = saved.TypeCodes.ToList(); Formations = saved.FormationIds.ToList();
        Traits = Enumerable.Range(0, 5).Select(_ => new[] { new List<int>(), new List<int>() }).ToArray();
        Tactics = Enumerable.Range(0, 5).Select(_ => new List<int>()).ToArray();
        foreach (var bank in saved.Traits)
        {
            if (bank.KindId is < 1 or > 4 || bank.Sex is not (0 or 1))
                throw new InvalidDataException("personnel.hire.saved_trait_pool");
            Traits[bank.KindId][bank.Sex.Value] = bank.Candidates.ToList();
        }
        foreach (var bank in saved.Tactics)
        {
            if (bank.KindId is < 1 or > 4 || bank.Sex is not null)
                throw new InvalidDataException("personnel.hire.saved_tactic_pool");
            Tactics[bank.KindId] = bank.Candidates.ToList();
        }
    }

    internal PersonnelHireGenerationPools Publish() => new(Types.ToImmutableArray(), Formations.ToImmutableArray(),
        Enumerable.Range(1, 4).SelectMany(type => Enumerable.Range(0, 2).Select(sex =>
            new PersonnelHireGenerationPool(type, sex, Traits[type][sex].ToImmutableArray()))).ToImmutableArray(),
        Enumerable.Range(1, 4).Select(type =>
            new PersonnelHireGenerationPool(type, null, Tactics[type].ToImmutableArray())).ToImmutableArray());
}

public sealed partial class ArmyCampaignRuleAdapter
{
    // TC/SC definition arrays and scalar consumers are independently matched offline.
    // SC also has its own full-name filter; these common arrays contain no generated results.
    // These are definitions, not recorded generated candidates or an EXE reader.
    private static readonly int[] HirePortraits = [0,0,0,0,2502,2500,2501,2501,2504,2509,
        2503,2503,2508,2508,2507,2507,2506,2506,2505,2505];
    private static readonly int[] HireDialogue = [0,0,0,0,0,0,0,0,9,2,3,4,13,13,10,10,
        9,4,4,6,13,13,11,11,9,9,6,6,13,13,11,11,9,6,7,8,13,11,11,12];
    private static readonly int[] HireVoices = [0,0,0,0,0,0,0,0,9,2,3,4,13,13,10,10,
        9,4,4,6,13,13,11,11,9,9,6,6,13,13,11,11,9,6,7,8,13,11,11,12];
    private static readonly int[][] HireAbilityRanges = [
        [0,0,0,0,0], [10,10,10,10,15], [10,14,14,10,10],
        [10,10,14,10,10], [10,10,10,10,15]];
    // The corresponding five base additions are all zero in these definitions.
    private static readonly IReadOnlySet<(int, int)> HireForbiddenNames = new HashSet<(int, int)> {
        (23,239),(38,69),(38,218),(38,321),(39,355),(44,266),(50,66),(50,218),
        (74,112),(92,199),(116,235),(127,58),(127,177),(127,189),(127,205),
        (147,29),(170,9),(170,140),(170,262),(179,354),(209,385),(213,204),
        (213,399),(217,100),(223,185),(251,62),(251,177),(251,199),(251,268),
        (286,349),(292,110),(292,183),(324,204),(327,124),(327,258),(335,95),
        (335,220),(335,375),(345,189),(352,112),(352,266),(358,69),(358,134),
        (358,137),(358,140),(358,172),(358,204),(358,223),(358,318),(358,323),
        (358,340),(358,350),(378,136),(378,191),(379,376),(380,294),(396,182)
    };

    //2216E0 ->2277B0 constructs independent receivers; their identity is zero
    //until334EF0 selects the receiver and writes the generated identity.
    internal static ImmutableDictionary<int, PersonnelHireTemplate> CreatePersonnelHireTemplateCatalogue()
    {
        var result = ImmutableDictionary.CreateBuilder<int, PersonnelHireTemplate>();
        for (int slot = 1; slot <= 520; slot++)
        {
            var person = new OfficerState(0, "", null, null, false)
            {
                Sex = OfficerSex.Male, AppearanceYear = 0, DoctrineId = null,
                PolicyId = null, PolicyBaseCost = 0, BaseAbilities = new(0,0,0,0,0),
                BaseFormationIds = [], BaseTacticIds = [], AbilityState = new(120,100,0),
                Behavior = OfficerBehaviorSourceAdapter.Decode(0,0),
                ServiceStatus = OfficerServiceStatusSourceAdapter.Decode(0),
                Appointment = OfficerAppointment.Synchronize(0), Loyalty = 0,
                LocationState = new(OfficerPosition.Unplaced, OfficerDistrictMembership.Unaffiliated),
                CurrentSurname = "", CurrentGivenName = "", CurrentCourtesyName = "",
                CurrentSurnameSortKey = "", CurrentGivenNameSortKey = "", CurrentNameSortKey = "",
                PortraitAppearanceIndex = 0, DialogueStyle = 0, VoiceType = byte.MaxValue,
                BirthYear = 0, Lifespan = new(0,0), FamilyGeneration = 0,
                CurrentTraitIds = new int[9], CurrentFormationMask = 0, CurrentTacticSlots = new byte[10],
                TraitActivation = new([],new int[9]), MeritState = new(0,0,0,0,0,0),
                Growth = new(new ulong?[] {0,0,0,0,0}, []), Resentment = new(0,0),
                AllegianceDisposition = new(0), Health = OfficerHealth.Healthy,
                DuelCondition = new(0,100), AppearanceNotificationPersonId = 0
            };
            result.Add(slot, new(slot, person) { PreviousForceId = 0 });
        }
        return result.ToImmutable();
    }

    internal static PersonnelHireTemplate ResetPersonnelHireTemplate(PersonnelHireTemplate template) =>
        ResetPersonnelHireTemplateSnapshot(template) with { PreviousForceId = 0 };

    internal PersonnelHireGenerationSession CreatePersonnelHireGenerationSession(GameState state,
        SourceWorldContext current, bool initializePools = false)
    {
        var parameters = state.NativeArmySimulation?.Parameters ?? throw HireMissing("generation_parameters");
        if (Seed.Language is not ("TC" or "SC") || parameters.Language != Seed.Language ||
            parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
            throw HireMissing("generation_rule_owner");
        //Refresh this operation's filter through the existing resource/override resolver.
        //Candidate-name queries do not reopen or hash the resource.
        _personnelHireNameFilter = Seed.Language == "SC" ? _repository.LoadPersonnelHireNameFilter() : null;
        if (initializePools) return new(PersonnelHireGenerationCatalog.Load(state, current));
        return new(state.PersonnelHire?.GenerationPools ?? throw HireMissing("generation_saved_pools"));
    }

    internal static GameState CompletePersonnelHireGeneration(GameState state, PersonnelHireGenerationSession session)
    {
        var owner = state.PersonnelHire ?? throw HireMissing("generation_complete_manager");
        return state with { PersonnelHire = owner with { GenerationPools = session.Publish() } };
    }

    /// <summary>The complete334EF0 generation leaf. World450 samples are pure;
    /// only name selection advances the independently saved CRT stream.</summary>
    internal GameState GeneratePersonnelHireTemplatesCurrent(GameState state, SourceWorldContext current,
        int forcePhysical, PersonnelHireGenerationSession session)
    {
        var force = new ForceReference(forcePhysical);
        if (!HireNeed(current.ReadForceNativeValidity(force), "generation_Force_validity")) return state;
        ushort rulerId = current.ReadForceRecord(forcePhysical)?.RawRuler10 ?? throw HireMissing("generation_ruler");
        var ruler = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId) : current.ReadTransferPersonTableReference(0))
            ?? throw HireMissing("generation_ruler_pointer");
        int rulerPhysical = HireNeed(ruler.PhysicalSlot, "generation_ruler_receiver");
        var capital = BaseLookup.Find(HireNeed(current.ReadPersonFirstLocation(rulerPhysical), "generation_capital_E2"), current);
        if (!HireNeed(current.ReadBaseNativeValidity(capital), "generation_capital_validity")) return state;

        var owner = state.PersonnelHire ?? throw HireMissing("generation_manager");
        var templates = owner.Templates ?? throw HireMissing("generation_template_catalogue");
        var roster = owner.Forces.GetValueOrDefault(forcePhysical) ?? throw HireMissing("generation_Force_roster");
        var offers = new List<int>(); //2266D0 clears only this force's offer list.
        var sampler = ReadArmyDestructionSampler(state, current) ?? throw HireMissing("generation_World450_45C");
        int day = sampler.Day450;
        int year = state.NativeMapSeason?.ThirdDate.Year ?? throw HireMissing("generation_year34");
        int Sample(int bound, int first, int second, int third = 0) =>
            TacticEffectSourceSamplerMath.Sample(bound, 1, first, second, third, 0, sampler);

        //2243A0/22AB60 visit all52 city table receivers and filter only owner.
        //The cached binary leaves the three stack accumulators uninitialized.
        //The independent algorithm defines these sums with the additive identity,
        //then uses every actual city contribution. It does not reproduce stack
        //garbage and therefore does not promise identical original sample salts.
        int cities = 0, gold = 0, food = 0, troops = 0;
        for (int slot = 0; slot < 52; slot++)
        {
            var city = current.ReadCityTableReference(slot) ?? throw HireMissing("generation_City_pointer");
            if (HireNeed(current.ReadBaseForceSlot(city), "generation_City_Force") != forcePhysical) continue;
            cities++;
            gold = unchecked(gold + HireNeed(current.ReadBaseResource(city, BaseResourceKind.Gold), "generation_City_gold"));
            food = unchecked(food + HireNeed(current.ReadBaseResource(city, BaseResourceKind.Supplies), "generation_City_food"));
            troops = unchecked(troops + HireNeed(current.ReadBaseResource(city, BaseResourceKind.Troops), "generation_City_troops"));
        }
        int countBase = cities == 1 ? 5 : cities == 2 ? 10 : 15;
        int count = unchecked(countBase + Sample(countBase, day, forcePhysical, cities));
        var economy = ReadPersonnelHireForceEconomyCurrent(state, current, forcePhysical);
        int[] salts = [gold,food,troops,economy.MonthlyGoldIncome,economy.MonthlyFoodIncome];
        int capitalGold = HireNeed(current.ReadBaseResource(capital, BaseResourceKind.Gold), "generation_capital_gold");
        int capitalFood = HireNeed(current.ReadBaseResource(capital, BaseResourceKind.Supplies), "generation_capital_food");
        int capitalTroops = HireNeed(current.ReadBaseResource(capital, BaseResourceKind.Troops), "generation_capital_troops");
        var homeArea = PersonFreedomHomeSource.FindBaseArea229FD0(current, capital);
        int areaId = HireNeed(homeArea.PhysicalSlot, "generation_capital_Area_pointer");
        int security = state.Areas.GetValueOrDefault(areaId)?.Security ?? throw HireMissing("generation_capital_Area54");
        if ((uint)security > 255) throw HireMissing("generation_capital_Area54_range");
        var abilities = SearchAbilities(current);
        int Ability(int dimension) => HireNeed(abilities.ResolveDimension(state, rulerId, dimension,
            (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)), "generation_ruler_ability" + dimension);
        int charm = Ability(4), leadership = Ability(0);

        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            int index = session.GeneratedCount;
            if (index >= 520) throw HireMissing("generation_template_capacity");
            int templateId = index + 1;
            var template = ResetPersonnelHireTemplate(templates.GetValueOrDefault(templateId)
                ?? throw HireMissing("generation_template_receiver"));
            int sex = Sample(100, index, day) > 60 ? 1 : 0;
            var name = GeneratePersonnelHireNameCurrent(state, current, sex + 3, HireForbiddenNames);
            state = name.State;
            int birth = Math.Clamp(year - (19 + Sample(20, day, index, capitalGold)), 0, 350);
            int terminal = Math.Clamp(birth + 40 + Sample(30, day, index, capitalTroops), 0, 350);
            int formationCount = HireGenerationSelectionCount(Sample(100, day, index, capitalFood));
            HireGenerationShuffle(session.Formations, unchecked(day + capitalFood + index), sampler);
            uint mask = 0;
            foreach (int id in session.Formations.Take(formationCount)) if ((uint)id < 21) mask |= 1u << id;
            if (formationCount == 1 && (mask & (1u << 7)) != 0)
            {
                HireGenerationShuffle(session.Formations, unchecked(day + capitalFood + index), sampler);
                foreach (int id in session.Formations)
                    if (id != 7) { if ((uint)id < 21) mask |= 1u << id; break; }
            }
            HireGenerationShuffle(session.Types, unchecked(day + security + index), sampler);
            if (session.Types.Count == 0) throw HireMissing("generation_type_head");
            int type = session.Types[0];
            if (type is < 1 or > 4) throw HireMissing("generation_type");
            int face = HirePortraits[(sex + type * 2) * 2 + Sample(2, day, index, charm)];
            int voice = (sex + type * 2) * 4 + Sample(4, day, index, leadership);
            int[] dimensions = Enumerable.Range(0, 5).Select(d =>
                Math.Clamp(Sample(HireAbilityRanges[type][d], day, index, salts[d]), 0, 100)).ToArray();
            int traitCount = HireGenerationSelectionCount(Sample(100, day, index, economy.MonthlyTroopRecruitment));
            var traitBank = session.Traits[type][sex];
            HireGenerationShuffle(traitBank, unchecked(day + economy.MonthlyTroopRecruitment + index), sampler);
            var traits = new int[9];
            traitBank.Take(traitCount).Order().Select(id => Math.Clamp(id, 0, 355)).ToArray().CopyTo(traits, 0);
            int tacticCount = HireGenerationSelectionCount(Sample(100, day, index, economy.GoldExpense));
            var tacticBank = session.Tactics[type];
            HireGenerationShuffle(tacticBank, unchecked(day + economy.GoldExpense + index), sampler);
            var tactics = new byte[10];
            tacticBank.Take(tacticCount).Select(id => (byte)Math.Clamp(id, 1, 200)).ToArray().CopyTo(tactics, 0);
            var person = template.Person with
            {
                Id = templateId, Name = name.Surname + name.GivenName, Sex = (OfficerSex)sex,
                CurrentSurname = name.Surname, CurrentGivenName = name.GivenName,
                CurrentSurnameSortKey = name.SurnameSortKey, CurrentGivenNameSortKey = name.GivenNameSortKey,
                CurrentNameSortKey = name.SurnameSortKey + name.GivenNameSortKey,
                BirthYear = (ushort)birth, Lifespan = new((ushort)terminal, 0),
                CurrentFormationMask = mask, Behavior = OfficerBehaviorSourceAdapter.Decode(0x4000, (byte)type),
                PortraitAppearanceIndex = unchecked((ushort)Math.Max(0, face)),
                DialogueStyle = (byte)Math.Clamp(HireDialogue[voice], 0, 16),
                VoiceType = (byte)Math.Clamp(HireVoices[voice], 0, 97),
                BaseAbilities = new(dimensions[0],dimensions[1],dimensions[2],dimensions[3],dimensions[4]),
                CurrentTraitIds = traits, CurrentTacticSlots = tactics, AbilityState = new(120,100,0)
            };
            templates = templates.SetItem(templateId, template with { Person = person, PreviousForceId = forcePhysical });
            if (offers.Count < 30) offers.Add(templateId); //2236F0's actual append limit.
            session.GeneratedCount++;
        }
        //241FD0/222890: valid trait count descending, formation bit count
        //descending, then actual Person identity ascending. No ability sort.
        int TraitCount(int id) => templates[id].Person.CurrentTraitIds!.Count(trait =>
        {
            var pointer = current.ReadPersonalityTableReference(trait <= 355 ? trait : 0)
                ?? throw HireMissing("generation_sort_trait_pointer");
            return HireNeed(current.ReadSkillPersonalityValidity(pointer), "generation_sort_trait_validity");
        });
        var ordered = offers.OrderByDescending(TraitCount).ThenByDescending(id =>
            BitOperations.PopCount(templates[id].Person.CurrentFormationMask!.Value)).ThenBy(id => templates[id].Person.Id).ToImmutableArray();
        owner = state.PersonnelHire ?? throw HireMissing("generation_result_manager");
        return state with { PersonnelHire = owner with {
            Templates = templates, GenerationPools = session.Publish(),
            Forces = owner.Forces.SetItem(forcePhysical, roster with { AvailableTemplateOfficerIds = ordered }) } };
    }

    private static int HireGenerationSelectionCount(int roll) => roll <= 20 ? 1 : roll <= 80 ? 2 : 3;

    //1D70E0 repeatedly swaps payloads; it is not a Fisher-Yates shuffle and
    //does not consume or reseed either the CRT or application random stream.
    private static void HireGenerationShuffle(List<int> bank, int seed, TacticSourceSamplerWorld world)
    {
        int count = bank.Count;
        if (count < 2) return;
        int rounds = unchecked((count / 2) * count);
        for (int i = 0; i < rounds; i++)
        {
            int first = TacticEffectSourceSamplerMath.Sample(count,1,seed,0,0,0,world);
            int second = count - 1 - TacticEffectSourceSamplerMath.Sample(count,1,unchecked(seed + 1),0,0,0,world);
            seed = unchecked(seed + 2);
            (bank[first], bank[second]) = (bank[second], bank[first]);
        }
    }
}

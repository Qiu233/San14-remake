using San14.Core;

namespace San14.Data;

internal sealed record CreditCommandPerson(int Id, int Physical, string Name, int Merit,
    int CurrentStage, int NextStage, int FirstSelectionCount, int SecondSelectionCount, int SelectionSlotCount)
{
    public int MeritAfterGive { get; init; }
    internal byte Status { get; init; }
    internal byte Office { get; init; }
    internal bool TutorialChoiceRestriction { get; init; }
}

internal sealed record CreditCommandCurrentRules(SourceWorldContext Current, int ForceId, int DistrictId,
    int PayerLocationId, int AvailableGold, int AvailableBooks, int GoldPerPerson, int BooksPerPerson,
    int FixedBookCost, int MeritAddition, IReadOnlyList<CreditCommandPerson> People)
{
    internal int PaymentDistrictId { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal CreditCommandCurrentRules CaptureCreditCommandCurrentRules(GameState state, int districtId, bool rankup)
        => CaptureCreditCommandCurrentRules(state, CaptureOwnedArmyReturnWorld(state).Context, districtId, rankup);

    internal CreditCommandCurrentRules CaptureCreditCommandCurrentRules(GameState state, SourceWorldContext current,
        int districtId, bool rankup)
    {
        _moraleWaveInputs.BindCurrent(state, current);
        var district = current.ReadDistrictTableReference(districtId) ?? throw CreditMissing("District_pointer");
        if (!CreditNeed(current.ReadDistrictNativeValidity(district), "District_validity")) throw CreditMissing("District_inactive");
        int forceId = CreditNeed(current.ReadDistrictForceId(CreditNeed(district.PhysicalSlot, "District_physical")), "District_Force10");
        var force = current.ReadForceTableReference(forceId < 52 ? forceId : 0) ?? throw CreditMissing("Force_pointer");
        if (!CreditNeed(current.ReadForceNativeValidity(force), "Force_validity")) throw CreditMissing("Force_inactive");
        int ruler = TalentRulerCurrent(current, force);
        int rulerPhysical = current.ReadPersonLookupReference(ruler)?.PhysicalSlot ?? throw CreditMissing("ruler_pointer");
        if (!CreditNeed(current.ReadPersonNativeValidity(rulerPhysical), "ruler_validity")) throw CreditMissing("ruler_inactive");
        //22D4E0: District leader12 -> Person's current HomeE2 -> Base lookup.
        int leader = CreditNeed(current.ReadDistrictLeader(districtId), "District_leader12");
        int leaderPhysical = current.ReadPersonLookupReference(leader <= 6000 ? leader : 0)?.PhysicalSlot ?? throw CreditMissing("leader_pointer");
        int payerId = CreditNeed(current.ReadPersonFirstLocation(leaderPhysical), "leader_HomeE2");
        var payer = BaseLookup.Find(payerId, current);
        if (!CreditNeed(current.ReadBaseNativeValidity(payer), "payer_validity")) throw CreditMissing("payer_inactive");
        var paymentDistrict = current.ReadBaseDistrictReference(payer) ?? throw CreditMissing("payer_District");
        if (!CreditNeed(current.ReadDistrictNativeValidity(paymentDistrict), "payer_District_validity")) throw CreditMissing("payer_District_inactive");
        //The formal writer debits the Base's actual District, not an inferred Force primary.
        int payerDistrict = CreditNeed(paymentDistrict.PhysicalSlot, "payer_District_physical");
        int gold = BaseCurrencyAdditionSource.ReadCurrentGold(payer, current);
        int books = state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining ?? throw CreditMissing("District_books14");
        var primary = ForcePrimaryDistrictSource.Resolve(current, force);
        bool allForce = TalentForceIsPlayerCurrent(state, current, force) && primary == district;
        var people = new List<CreditCommandPerson>();
        foreach (int? item in current.ReadRegisteredPersonPhysicalSlots() ?? throw CreditMissing("Person_registry"))
        {
            int physical = CreditNeed(item, "Person_registry_pointer");
            if (CreditNeed(current.ReadPersonOldForceSlot(physical), "Person_Force") != forceId) continue;
            byte status = CreditNeed(current.ReadPersonStatus(physical), "PersonE6");
            if (status is < 1 or > 4) continue;
            if (!allForce && CreditNeed(current.ReadPersonDistrictSlot(physical), "Person_DistrictE0") != districtId) continue;
            if (!CreditEligibleCurrent(state, current, physical, rankup)) continue;
            int id = CreditNeed(current.ReadPersonIdentity(physical), "Person_identity10");
            var officer = state.Officers.GetValueOrDefault(id) ?? throw CreditMissing("Person_domain");
            var merit = officer.MeritState ?? throw CreditMissing("Person_merit_owner");
            int stage = CreditNeed(merit.RankStageId, "Person_stage1BC");
            int next = CreditNextStageCurrent(state, officer);
            int slots = 0;
            var ranks = CreditRanks();
            for (int rank = next; rank > 0; rank--) if (ranks[rank].RawTailValue != 0) slots++;
            people.Add(new(id, physical, officer.Name, CreditNeed(merit.Merit, "Person_merit1BE"), stage, next,
                CreditNeed(merit.FirstSelectionCount, "Person_first1C2"), CreditNeed(merit.SecondSelectionCount, "Person_second1C3"), slots)
            {
                Status = status, Office = rankup ? CreditNeed(current.ReadMoraleWavePersonPostE7(physical), "Person_postE7") : (byte)0,
                TutorialChoiceRestriction = rankup && CreditTutorialChoicesCurrent(current),
                MeritAfterGive = ReadCreditGiveMeritAfterCurrent(state, officer)
            });
        }
        //69CEA0 /6B0080 total comparison; native identity resolves all remaining ties.
        var ordered = rankup
            ? people.OrderByDescending(p => p.NextStage > p.CurrentStage).ThenByDescending(p => p.CurrentStage)
                .ThenByDescending(p => p.Merit).ThenBy(p => p.Status).ThenBy(p => p.Office).ThenBy(p => p.Id).ToArray()
            : people.OrderByDescending(p => p.Merit).ThenBy(p => p.Status).ThenBy(p => p.Id).ToArray();
        return new(current, forceId, districtId, payerId, gold, books,
            CreditParameter(state, rankup ? "rankup-gold" : "give-gold", rankup ? 100 : 2500),
            rankup ? CreditParameter(state, "rankup-books", 1) : 0,
            rankup ? 0 : CreditParameter(state, "give-books", 1),
            rankup ? 0 : CreditParameter(state, "give-merit", 1000), ordered) { PaymentDistrictId = payerDistrict };
    }

    internal CommandResult ApplyPersonnelCreditGive(GameState state, ForceState force, int districtId,
        IReadOnlyList<int> officerIds, CommandContext context)
    {
        try
        {
            var rules = CaptureCreditCommandCurrentRules(state, districtId, false);
            RequireCreditOrder(state, force, rules, officerIds);
            if (officerIds.Count == 0) return CommandResult.Accept(state);
            var working = state;
            var current = rules.Current;
            var promoted = new List<int>();
            foreach (int id in officerIds)
            {
                var row = rules.People.First(p => p.Id == id);
                var owner = working.Officers[id];
                var before = owner.MeritState ?? throw CreditMissing("give_merit_owner");
                int stage = CreditNeed(before.RankStageId, "give_stage1BC");
                int reset = CreditNeed(before.ResetCode, "give_reset1C4");
                int after = ReadCreditGiveMeritAfterCurrent(working, owner);
                if (stage == 1 && after >= CreditRanks()[2].SignedThreshold)
                {
                    stage = 2; reset = 0;
                    var personForce = current.ReadPersonForce(row.Physical) ?? throw CreditMissing("give_promoted_Force");
                    if (TalentForceIsPlayerCurrent(working, current, personForce) &&
                        (CreditNeed(current.ReadWorldTransferServiceFlags(), "give_World16A8") & 0x100) == 0)
                        promoted.Add(id);
                }
                ushort flags = (ushort)(CreditNeed(current.ReadArmyReturnPersonFlags196(row.Physical), "give_Person15E") | 0x8000);
                current.WriteArmyReturnPersonFlags196(row.Physical, flags);
                working = working with { Officers = new(working.Officers) { [id] = owner with
                {
                    MeritState = new(after, before.PendingMerit, stage, before.FirstSelectionCount, before.SecondSelectionCount, reset),
                    Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(owner.Behavior, flags)
                } } };
            }
            working = PayCreditOrderCurrent(working, rules, officerIds.Count);
            var effects = new List<CampaignEffectRequest>();
            promoted = promoted.OrderByDescending(id => CreditNeed(working.Officers[id].MeritState?.Merit, "promotion_sort_merit"))
                .ThenBy(id => CreditNeed(current.ReadPersonStatus(CreditNeed(current.ResolveOfficerSource(id), "promotion_sort_physical")), "promotion_sort_status"))
                .ThenBy(id => id).ToList();
            working = ApplyPersonnelCreditReportsCurrent(working, current, force.Id, officerIds, false, effects, state, promoted);
            return CommandResult.Accept(AppendCreditEffects(working, effects));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("personnel-credit.give", error.Message); }
    }

    internal CommandResult ApplyPersonnelCreditRankup(GameState state, ForceState force, int districtId,
        IReadOnlyList<OfficerCreditChoice> choices, CommandContext context)
    {
        try
        {
            var rules = CaptureCreditCommandCurrentRules(state, districtId, true);
            int[] ids = choices.Select(c => c.OfficerId).ToArray();
            RequireCreditOrder(state, force, rules, ids);
            if (ids.Length == 0) return CommandResult.Accept(state);
            foreach (var choice in choices)
                if (!CreditChoiceSource.IsLegal(rules.People.First(p => p.Id == choice.OfficerId), choice.ChoiceCode))
                    throw CreditMissing("rankup_choice_code");
            //6CD870 collects presentation groups in the accepted UI list order,
            //before the formal numerical map is consumed in Person ID order.
            var presentation = PreparePersonnelCreditRankupPresentationCurrent(state, rules, choices);
            var working = state;
            var current = rules.Current;
            //1EC640 consumes the native map in ascending Person identity order.
            foreach (var choice in choices.OrderBy(c => c.OfficerId))
            {
                int id = choice.OfficerId;
                var row = rules.People.First(p => p.Id == id);
                if (!CreditEligibleCurrent(working, current, row.Physical, true)) throw CreditMissing("rankup_person_no_longer_eligible");
                var owner = working.Officers[id];
                var before = owner.MeritState ?? throw CreditMissing("rankup_merit_owner");
                int next = CreditNextStageCurrent(working, owner);
                int points = ReadCreditRankupPointAddition(row);
                int doctrine = CreditNeed(OfficerDoctrineRules.ReadDoctrineId(owner.DoctrineId, owner.AbilityState), "rankup_Person14C");
                if (doctrine is >= 1 and <= 6 && points > 0)
                {
                    var reserve = working.ForceCreditReserves ?? throw CreditMissing("Force_doctrine_reserve_owner");
                    if (!reserve.PointsByForce.TryGetValue(force.Id, out var old) || old.Length != 6)
                        throw CreditMissing("Force_doctrine_reserve_vector");
                    var updated = old.SetItem(doctrine - 1, (byte)Math.Clamp(unchecked(old[doctrine - 1] + points), 0, 99));
                    working = working with { ForceCreditReserves = reserve with { PointsByForce = reserve.PointsByForce.SetItem(force.Id, updated) } };
                }

                ushort flags = (ushort)(CreditNeed(current.ReadArmyReturnPersonFlags196(row.Physical), "rankup_Person15E") | 0x8000);
                current.WriteArmyReturnPersonFlags196(row.Physical, flags);
                working = working with { Officers = new(working.Officers) { [id] = owner with
                {
                    MeritState = new(before.Merit, before.PendingMerit, Math.Clamp(next, 0, 6),
                        choice.ChoiceCode & 15, choice.ChoiceCode >> 4, before.ResetCode),
                    Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(owner.Behavior, flags)
                } } };
            }
            working = PayCreditOrderCurrent(working, rules, ids.Length);
            var effects = new List<CampaignEffectRequest>();
            working = ApplyPersonnelCreditRankupPresentationCurrent(working, current, effects, state, presentation);
            return CommandResult.Accept(AppendCreditEffects(working, effects));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("personnel-credit.rankup", error.Message); }
    }

    private static void RequireCreditOrder(GameState state, ForceState force, CreditCommandCurrentRules rules, IReadOnlyList<int> ids)
    {
        if (state.Phase != TurnPhase.Strategy || force.Id != rules.ForceId) throw CreditMissing("command_scope");
        if (ids.Distinct().Count() != ids.Count || ids.Any(id => rules.People.All(p => p.Id != id)))
            throw CreditMissing("selected_Person_eligibility");
        int gold = checked(ids.Count * rules.GoldPerPerson);
        int books = checked(ids.Count * rules.BooksPerPerson + rules.FixedBookCost);
        if (rules.AvailableGold < gold) throw CreditMissing("insufficient_gold");
        if (rules.AvailableBooks < books) throw CreditMissing("insufficient_command_books");
    }

    private GameState PayCreditOrderCurrent(GameState state, CreditCommandCurrentRules rules, int count)
    {
        var current = rules.Current;
        var payer = BaseLookup.Find(rules.PayerLocationId, current);
        var paid = BaseCurrencyAdditionSource.Add(state, current, payer, unchecked(-rules.GoldPerPerson * count),
            city => BasePostLoad.ReadResourceCapacities(city, current).Gold).State;
        var district = paid.Districts.GetValueOrDefault(rules.PaymentDistrictId) ?? throw CreditMissing("payment_District_owner");
        int before = CreditNeed(district.OrdersRemaining, "payment_District14");
        int cost = unchecked(rules.FixedBookCost + rules.BooksPerPerson * count);
        int after = Math.Clamp(unchecked(before - cost), 0, 200);
        current.SetDistrictRawByte(district.Id, 0x14, (byte)after);
        var saved = paid.NativeForceUnregisterCurrent ?? throw CreditMissing("payment_District_saved_fields");
        var window = saved.DistrictWindows10.GetValueOrDefault(district.Id)?.ToArray()
            ?? throw CreditMissing("payment_District_saved_window");
        window[4] = (byte)after;
        paid = paid with
        {
            Districts = new(paid.Districts) { [district.Id] = district with { OrdersRemaining = after } },
            NativeForceUnregisterCurrent = saved with { DistrictWindows10 = new(saved.DistrictWindows10) { [district.Id] = window } }
        };
        if (HasCommandAccountingUser(paid) &&
            BaseCaptureRewardSource.ReadPlayerRulerDistrict(current,
                current.ReadDistrictTableReference(district.Id) ?? throw CreditMissing("App_payment_District"), CreditMissing))
            paid = ApplyCommandBookUsageCurrent(paid, current, cost);
        return paid;
    }

    private static GameState AppendCreditEffects(GameState state, List<CampaignEffectRequest> effects)
    {
        if (effects.Count == 0) return state;
        var requests = state.CampaignEffectRequests ?? throw CreditMissing("pending_effect_owner");
        long revision = state.NativeMapSeason?.AdvancedCalendarSteps ?? throw CreditMissing("calendar_owner");
        var result = state with { CampaignEffectRequests = requests.Append(CampaignEffectSource.PersonnelAdministrationCommands, revision, effects) };
        return result;
    }

    internal int ReadCreditGiveMeritAfterCurrent(GameState state, OfficerState officer)
    {
        int before = CreditNeed(officer.MeritState?.Merit, "give_merit1BE");
        var ranks = CreditRanks();
        int selector = CreditNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "merit_bound_selector");
        int option = CreditNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x4C4 : 0x128), "merit_lower_option");
        int lower = ranks[option == 1 ? 2 : 1].SignedThreshold;
        int requested = unchecked(before + CreditParameter(state, "give-merit", 1000));
        return unchecked((short)(requested < lower ? lower : Math.Min(requested, ranks[6].SignedThreshold)));
    }

    internal byte ApplyCreditAutomaticChoiceCurrent(GameState state, CreditCommandCurrentRules rules,
        CreditCommandPerson person, int mode)
    {
        return CreditChoiceSource.ApplyAutomatic(person, mode, () =>
        {
            var bank = state.NativeArmySimulation?.Parameters ?? throw CreditMissing("automatic_parameter_owner");
            var abilities = new TacticRemakeCurrentAbilities(rules.Current, _scenario, bank.Language, bank.RuleVersion);
            int politics = CreditNeed(abilities.ResolveDimension(state, person.Id, 3), "automatic_Politics");
            int intelligence = CreditNeed(abilities.ResolveDimension(state, person.Id, 2), "automatic_Intelligence");
            int strength = CreditNeed(abilities.ResolveDimension(state, person.Id, 1), "automatic_Strength");
            int leadership = CreditNeed(abilities.ResolveDimension(state, person.Id, 0), "automatic_Leadership");
            return unchecked(leadership + strength) < unchecked(politics + intelligence);
        });
    }

    internal int ReadCreditRankupPointAddition(CreditCommandPerson person)
    {
        int total = 0;
        var ranks = CreditRanks();
        for (int rank = person.NextStage; rank > person.CurrentStage; rank--)
            total = unchecked(total + ranks[rank].DoctrinePointAddition);
        return total;
    }

    private static bool CreditTutorialChoicesCurrent(SourceWorldContext current)
    {
        var service = current.ArmyReturnTransferMode ?? throw CreditMissing("choice_tutorial570850");
        return service.Present && CreditNeed(service.Value, "choice_tutorial_value") != 0;
    }

    private IReadOnlyList<Formats.S14CreditRankDefinition> CreditRanks()
    {
        var ranks = _scenario.Snapshot.CreditRankParameters?.Ranks;
        return ranks is { Count: 7 } ? ranks : throw CreditMissing("CreditRank_definitions");
    }

    private int CreditNextStageCurrent(GameState state, OfficerState officer)
    {
        int stage = CreditNeed(officer.MeritState?.RankStageId, "stage1BC");
        int merit = CreditNeed(officer.MeritState?.Merit, "merit1BE");
        var ranks = CreditRanks();
        for (int rank = 6; rank > 0; rank--)
            if (ranks[rank].SignedThreshold <= merit) return Math.Min(rank, stage + 1);
        return 0;
    }

    private bool CreditEligibleCurrent(GameState state, SourceWorldContext current, int physical, bool rankup)
    {
        if (!CreditNeed(current.ReadPersonNativeValidity(physical), "Person_validity")) return false;
        if (CreditNeed(current.ReadPersonStatus(physical), "PersonE6") == 1 || DuelAftermathSpecialPerson(current, physical)) return false;
        ushort flags = CreditNeed(current.ReadArmyReturnPersonFlags196(physical), "Person15E");
        if ((flags & 0xC000) != 0) return false;
        var linked = current.ReadPersonLinkedEntity(physical) ?? throw CreditMissing("Person_associated22AC00");
        if (CreditNeed(current.ReadLinkedEntityNativeValidity(linked), "associated_validity") &&
            CreditAssociatedElapsedCurrent(state, current, linked) != 0) return false;
        bool armyValid = CreditNeed(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, physical, out var army), "Person_Army22A200");
        if (armyValid)
        {
            int slot = CreditNeed(army?.PhysicalSlot, "Person_Army_physical");
            var arena = state.ReadArmyLifecycle() ?? throw CreditMissing("Army_arena");
            var source = arena.Slots[slot];
            var receiver = arena.Receivers.Find(source.ReceiverId) ?? throw CreditMissing("Person_Army_receiver");
            var image = receiver.ActiveUnitId is int unit ? state.Units.GetValueOrDefault(unit) : receiver.PendingImage;
            if (image is null) throw CreditMissing("Person_Army_image");
            if (CreditNeed(ArmyConfigurationSourceAdapter.ReadAction(state, image), "Person_Army37") != 0) return false;
        }
        int id = CreditNeed(current.ReadPersonIdentity(physical), "Person10");
        var owner = state.Officers.GetValueOrDefault(id) ?? throw CreditMissing("Person_owner");
        int merit = CreditNeed(owner.MeritState?.Merit, "Person_merit1BE");
        if (!rankup) return merit < CreditRanks()[6].SignedThreshold;
        int stage = CreditNeed(owner.MeritState?.RankStageId, "Person_stage1BC");
        return stage > 1 && stage < CreditNextStageCurrent(state, owner);
    }

    private static int CreditAssociatedElapsedCurrent(GameState state, SourceWorldContext current, PersonLinkedEntityReference linked)
    {
        int id = CreditNeed(linked.Identity, "associated_identity");
        var owner = current.ReadOwnedPersonCommandManager() ?? throw CreditMissing("associated_manager");
        var now = state.NativeMapSeason?.ThirdDate ?? throw CreditMissing("World_date34");
        int currentDate = PersonReturnCommands.CalendarOrdinal(now);
        if (owner.Returns.TryGetValue(id, out var ret))
            return Math.Max(0, unchecked(currentDate - PersonReturnCommands.CalendarOrdinal(ret.StartedOn)));
        if (owner.Talents?.TryGetValue(id, out var talent) == true)
            return unchecked(currentDate - PersonReturnCommands.CalendarOrdinal(talent.StartDate));
        if (owner.Diplomacies?.TryGetValue(id, out var diplomacy) == true)
            return unchecked(currentDate - PersonReturnCommands.CalendarOrdinal(diplomacy.StartDate));
        if (owner.Caravans?.TryGetValue(id, out var caravan) == true)
            return unchecked(currentDate - PersonReturnCommands.CalendarOrdinal(caravan.StartDate));
        if (owner.Intelligences?.TryGetValue(id, out var intelligence) == true)
            return unchecked(currentDate - PersonReturnCommands.CalendarOrdinal(intelligence.StartDate));
        if (owner.Disappearances?.TryGetValue(id, out var disappear) == true)
            return unchecked(currentDate - PersonReturnCommands.CalendarOrdinal(disappear.StartedOn));
        throw CreditMissing("associated_virtualB0");
    }

    private static int CreditParameter(GameState state, string name, int tcValue)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw CreditMissing("parameters");
        string key = "personnel-credit:" + name;
        if (bank.Float32Values.ContainsKey(key)) throw CreditMissing("parameter_type_" + name);
        if (bank.Int32Values.TryGetValue(key, out int value)) return value;
        //TC immutable i32 catalog:1A49828/834/84C/858/864.
        if (bank.Language == "TC" && bank.RuleVersion == SimulationRuleIdentity.ForLocale("TC")) return tcValue;
        throw CreditMissing("parameter_" + name);
    }

    private static T CreditNeed<T>(T? value, string field) where T : struct => value ?? throw CreditMissing(field);
    private static InvalidDataException CreditMissing(string field) => new("personnel-credit." + field);
}

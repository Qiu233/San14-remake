using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal OriginalAiCommandPreparation PrepareOriginalAiSearchCurrent(GameState state,
        SourceWorldContext current, int targetHex, int officerId)
    {
        if ((uint)targetHex >= 48400) throw AiPersonnelMissing("search_hex_identity");
        var hex = current.ReadHexTableReference(targetHex) ?? throw AiPersonnelMissing("search_hex_pointer");
        if (hex.PhysicalSlot is not int physicalHex) return new(false, null);
        int person = AiPersonnelPerson(current, officerId);
        if (!AiPersonnelKnown(current.ReadPersonNativeValidity(person), "search_person_validity")) return new(false, null);
        if (PersonCurrentBusySource.Read(current, person, AiPersonnelMissing)) return new(false, null);
        int owner = AiPersonnelKnown(current.ReadHexForceSlot(hex), "search_Hex14_Force");
        int force = AiPersonnelKnown(current.ReadPersonOldForceSlot(person), "search_Person_Force");
        if (owner != force) return new(false, null);
        int district = AiPersonnelPersonDistrict(current, person);
        int cost = PersonReturnCommands.ReadTalentBookCost(state, TalentOrderKind.Search);
        if (AiPersonnelKnown(current.ReadDistrictRawByte(district, 0x14), "search_District_books") < cost)
            return new(false, null);

        //The normal formal consumer currently describes City-home searches.
        //Do not turn a source-valid wider case into an ordinary rejection.
        int home = AiPersonnelKnown(current.ReadPersonFirstLocation(person), "search_home_E2");
        if (home is >= 52 and <= 61) throw AiPersonnelMissing("search_gate_home_contract");
        if (home is < 1 or > 51) throw AiPersonnelMissing("search_home_contract");
        int rawArea = AiPersonnelKnown(current.ReadHexAreaSlot(hex), "search_Hex_Area");
        var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0) ?? throw AiPersonnelMissing("search_Area_pointer");
        if (!AiPersonnelKnown(current.ReadAreaNativeValidity(area), "search_Area_validity") ||
            AiPersonnelKnown(current.ReadAreaCurrentForceSlot(area), "search_Area_Force") != owner)
            throw AiPersonnelMissing("search_hex_owner_contract");
        return new(true, new PrepareTalentSearchOrder(force, home, officerId) { TargetHexId = physicalHex });
    }

    internal OriginalAiCommandPreparation PrepareOriginalAiCreditCurrent(GameState state, SourceWorldContext current,
        int district, IReadOnlyList<OriginalAiEntity> people, bool rankup)
    {
        if (!AiPersonnelKnown(current.ReadDistrictNativeValidity(district), "credit_District_validity")) return new(false, null);
        var payer = AiPersonnelDistrictHome(current, district);
        if (!AiPersonnelKnown(current.ReadBaseNativeValidity(payer), "credit_payer_validity")) return new(false, null);
        int gold = BaseCurrencyAdditionSource.ReadCurrentGold(payer, current);
        int books = AiPersonnelKnown(current.ReadDistrictRawByte(district, 0x14), "credit_books");
        int goldCost = CreditParameter(state, rankup ? "rankup-gold" : "give-gold", rankup ? 100 : 2500);
        int bookCost = CreditParameter(state, rankup ? "rankup-books" : "give-books", 1);
        //Both node gates use the original pointer-list length, before valid
        //Person filtering (and before rankup's ordered-map de-duplication).
        if (unchecked((ulong)((long)goldCost * people.Count)) > unchecked((ulong)(long)gold) ||
            (rankup ? unchecked((ulong)((long)bookCost * people.Count)) > unchecked((ulong)books) : books < bookCost))
            return new(false, null);
        int force = AiPersonnelKnown(current.ReadDistrictForceId(district), "credit_Force");
        var ids = new List<int>();
        var choices = new SortedDictionary<int, OfficerCreditChoice>();
        var bank = state.NativeArmySimulation?.Parameters ?? throw AiPersonnelMissing("credit_parameters");
        TacticRemakeCurrentAbilities? abilities = null;
        foreach (var entity in people)
        {
            if (entity.Kind != WorldEntityKind.Officer) throw AiPersonnelMissing("credit_list_type");
            int person = AiPersonnelPerson(current, entity.Id);
            if (!AiPersonnelKnown(current.ReadPersonNativeValidity(person), "credit_Person_validity")) continue;
            if (!rankup) { ids.Add(entity.Id); continue; }
            //0429A0/03FC30: ruler, special-person and4000 branches cannot
            //reach a positive next stage. Do not substitute the UI list's
            //additional selection filters for this actual source predicate.
            if (AiPersonnelKnown(current.ReadPersonStatus(person), "rankup_PersonE6") == 1 ||
                DuelAftermathSpecialPerson(current, person) ||
                (AiPersonnelKnown(current.ReadArmyReturnPersonFlags196(person), "rankup_Person15E") & 0x4000) != 0) continue;
            var owner = state.Officers.GetValueOrDefault(entity.Id) ?? throw AiPersonnelMissing("rankup_person_current");
            var merit = owner.MeritState ?? throw AiPersonnelMissing("rankup_merit");
            int before = AiPersonnelKnown(merit.RankStageId, "rankup_stage");
            int next = CreditNextStageCurrent(state, owner);
            if (before >= next) continue;
            int slots = 0;
            var ranks = CreditRanks();
            for (int stage = next; stage > 0; stage--) if (ranks[stage].RawTailValue != 0) slots++;
            var row = new CreditCommandPerson(entity.Id, person, owner.Name, AiPersonnelKnown(merit.Merit, "rankup_merit_value"),
                before, next, AiPersonnelKnown(merit.FirstSelectionCount, "rankup_first_count"),
                AiPersonnelKnown(merit.SecondSelectionCount, "rankup_second_count"), slots);
            byte code = CreditChoiceSource.ApplyAutomatic(row, 3, () =>
            {
                abilities ??= new(current, _scenario, bank.Language, bank.RuleVersion);
                int politics = AiPersonnelKnown(abilities.ResolveDimension(state, entity.Id, 3), "rankup_Politics");
                int intelligence = AiPersonnelKnown(abilities.ResolveDimension(state, entity.Id, 2), "rankup_Intelligence");
                int strength = AiPersonnelKnown(abilities.ResolveDimension(state, entity.Id, 1), "rankup_Strength");
                int leadership = AiPersonnelKnown(abilities.ResolveDimension(state, entity.Id, 0), "rankup_Leadership");
                return unchecked(strength + leadership) < unchecked(politics + intelligence);
            });
            choices[entity.Id] = new(entity.Id, code);
        }
        if (rankup)
            //The native node ignores the writer's empty-map return and still
            //returns Success. No debit or promotion occurs for an empty map.
            return choices.Count == 0 || AiPersonnelWriterDistrict(current, payer) is null ? new(true, null) : new(true,
                new PromoteOfficerCreditOrder(force, district, choices.Values.ToArray()));
        if (ids.Count == 0) return new(true, null); //1EC370 empty-list no-op, node returns1.
        if (AiPersonnelWriterDistrict(current, payer) is null) return new(true, null);
        if (ids.Distinct().Count() != ids.Count) throw AiPersonnelMissing("credit_give_repeated_person_order_contract");
        return new(true, new GrantOfficerMeritOrder(force, district, ids.AsReadOnly()));
    }

    internal OriginalAiCommandPreparation PrepareOriginalAiPrizeCurrent(GameState state, SourceWorldContext current,
        int district, IReadOnlyList<OriginalAiEntity> people, int requestedBudget)
    {
        var payer = AiPersonnelDistrictHome(current, district);
        int stock = BaseCurrencyAdditionSource.ReadCurrentGold(payer, current);
        int budget = Math.Min(stock, requestedBudget);
        var selected = new List<int>();
        foreach (var entity in people)
        {
            //02E290 subtracts100 BEFORE its strict-positive test. Equality
            //does not select the next person and the original order is kept.
            budget = unchecked(budget - 100);
            if (budget <= 0) break;
            if (entity.Kind != WorldEntityKind.Officer) throw AiPersonnelMissing("prize_list_type");
            selected.Add(entity.Id);
        }
        if (selected.Count == 0) return new(false, null);
        int? payingDistrict = AiPersonnelWriterDistrict(current, payer);
        if (payingDistrict is not int payment) return new(true, null); //1F2A50 early return, ignored by02E290.
        int books = AiPersonnelKnown(state.Districts.GetValueOrDefault(payment)?.OrdersRemaining, "prize_payment_books");
        int cost = ReadPersonnelRewardParameter(state, PersonnelRewardBookCostKey);
        if (books - CommandBookAvailability.Reserved(state, payment) < cost)
            throw AiPersonnelMissing("prize_command_books_contract");
        if (selected.Distinct().Count() != selected.Count) throw AiPersonnelMissing("prize_repeated_person_order_contract");
        int force = AiPersonnelKnown(current.ReadDistrictForceId(district), "prize_Force");
        return new(true, new RewardOfficersOrder(force, district, selected.AsReadOnly()));
    }

    internal OriginalAiCommandPreparation PrepareOriginalAiItemCurrent(GameState state, SourceWorldContext current,
        int recipient, int itemId)
    {
        int person = AiPersonnelPerson(current, recipient);
        if (!AiPersonnelKnown(current.ReadPersonNativeValidity(person), "item_recipient_validity")) return new(false, null);
        var item = current.ReadRegistryTableReference(RegistryKind.Item, itemId) ?? throw AiPersonnelMissing("item_pointer");
        if (!AiPersonnelKnown(current.ReadRegistryObjectNativeValidity(item), "item_validity")) return new(false, null);
        if (state.Items?.GetValueOrDefault(itemId)?.Holder is not { } holder) throw AiPersonnelMissing("item_holder");
        int previous = AiPersonnelPerson(current, holder.OfficerId ?? 0);
        int district = AiPersonnelPersonDistrict(current, previous);
        int cost = PersonReturnCommands.ReadTalentBookCost(state, TalentOrderKind.Search); //Shared1A4954C pre-call gate.
        if (AiPersonnelKnown(current.ReadDistrictRawByte(district, 0x14), "item_old_holder_books") < cost) return new(false, null);
        //053330/1F03F0 transfers an already-held item and charges recipient
        //Force HQ. The current item formal consumer has an independent direct
        //control/availability contract. Preserve this source-valid gap until
        //that shared contract is extended; do not disguise it as rejection.
        throw AiPersonnelMissing("item_formal_transfer_scope_contract");
    }

    private static BaseReference AiPersonnelDistrictHome(SourceWorldContext current, int district)
    {
        ushort leader = AiPersonnelKnown(current.ReadDistrictLeader(district), "District12_leader");
        int person = AiPersonnelPerson(current, leader);
        int home = AiPersonnelKnown(current.ReadPersonFirstLocation(person), "leader_HomeE2");
        return BaseLookup.Find(home, current);
    }
    /// <summary>The shared early-return chain of1EC370/1EC640/1F2A50.
    /// A proven invalid receiver causes no writes; unknown fields still fail.</summary>
    private static int? AiPersonnelWriterDistrict(SourceWorldContext current, BaseReference payer)
    {
        if (!AiPersonnelKnown(current.ReadBaseNativeValidity(payer), "writer_Base_validity")) return null;
        var district = current.ReadBaseDistrictReference(payer) ?? throw AiPersonnelMissing("writer_District_pointer");
        if (!AiPersonnelKnown(current.ReadDistrictNativeValidity(district), "writer_District_validity")) return null;
        int rawForce = AiPersonnelKnown(current.ReadDistrictForceSlot(district), "writer_Force10");
        var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw AiPersonnelMissing("writer_Force_pointer");
        if (!AiPersonnelKnown(current.ReadForceNativeValidity(force), "writer_Force_validity")) return null;
        int physical = AiPersonnelKnown(force.PhysicalSlot, "writer_Force_identity");
        int ruler = AiPersonnelKnown(current.ReadForceRecord(physical)?.RawRuler10, "writer_ruler");
        if (!AiPersonnelKnown(current.ReadPersonNativeValidity(AiPersonnelPerson(current, ruler)), "writer_ruler_validity")) return null;
        return AiPersonnelKnown(district.PhysicalSlot, "writer_District_identity");
    }
    private static int AiPersonnelPerson(SourceWorldContext current, int identity)
    {
        var at = identity is >= 0 and <= 6000 ? current.ReadPersonLookupReference(identity) : current.ReadTransferPersonTableReference(0);
        return at?.PhysicalSlot ?? throw AiPersonnelMissing("Person_lookup");
    }
    private static int AiPersonnelPersonDistrict(SourceWorldContext current, int person)
    {
        byte raw = AiPersonnelKnown(current.ReadPersonDistrictSlot(person), "PersonE0_District");
        return (current.ReadDistrictTableReference(raw < 52 ? raw : 0) ?? throw AiPersonnelMissing("Person_District_pointer"))
            .PhysicalSlot ?? throw AiPersonnelMissing("Person_District_identity");
    }
    private static T AiPersonnelKnown<T>(T? value, string field) where T : struct => value ?? throw AiPersonnelMissing(field);
    private static InvalidDataException AiPersonnelMissing(string field) => new("original_ai.personnel." + field);
}

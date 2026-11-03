using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //891A80 consumes these ten real admission/help producers. All readers
    //borrow the header's current; none constructs a page or captures a World.
    private MapMainCommandEntryPresentation ReadPersonnelCommandEntry(GameState state,
        SourceWorldContext current, MapMenuCommand command, bool? restricted, Action bindCurrent, Action bindItems)
    {
        try
        {
            if (command is MapMenuCommand.Search or MapMenuCommand.Employ or MapMenuCommand.Merit or
                MapMenuCommand.Award or MapMenuCommand.Expel)
            {
                if (restricted is null) throw MainCommandMissing("personnel_application_selection");
                if (restricted.Value) return new(command, false, 0x11171);
            }
            var force = current.CurrentPlayerForce ?? throw MainCommandMissing("personnel_player");
            var district = ForcePrimaryDistrictSource.Resolve(current, force);
            int districtId = MainNeed(district.PhysicalSlot, "personnel_primary_district");
            int Books() => MainNeed(current.ReadDistrictRawByte(districtId, 0x14), "personnel_books");
            int Gold() => BaseCurrencyAdditionSource.ReadCurrentGold(ReadPersonnelHireTreasuryCurrent(current, districtId), current);
            MapMainCommandEntryPresentation Result(bool allowed, int message) => new(command, allowed, message);

            switch (command)
            {
                case MapMenuCommand.Search:
                case MapMenuCommand.Recruit:
                {
                    bool recruitment = command == MapMenuCommand.Recruit;
                    if (Books() < PersonReturnCommands.ReadTalentBookCost(state,
                        recruitment ? TalentOrderKind.Recruit : TalentOrderKind.Search)) return Result(false, 0x57C7);
                    bindCurrent();
                    //6DC650 has no optional person callback in this entrance;
                    //its ordinary predicate is the same6DC4E0 candidate body.
                    bool any = CountPersonnelMenuCandidates(current,
                        PersonnelMenuTalentExecutors(current, districtId)).Count != 0;
                    return Result(any, recruitment ? any ? 0x582C : 0x582E : any ? 0x5829 : 0x582B);
                }
                case MapMenuCommand.Employ:
                    return ReadPersonnelHireCommandEntry(state, current, force, district, districtId, bindCurrent);
                case MapMenuCommand.Items:
                {
                    if (Books() < MainNeed(PersonnelCommandParameters.Read(state, PersonnelCommandParameters.ItemCommandBooks),
                        "personnel_items_book_cost")) return Result(false, 0x57C7);
                    bindCurrent();
                    int rawForce = MainNeed(current.ReadDistrictForceId(districtId), "personnel_items_district_force");
                    return WithPersonnelMenuCandidates(current, PersonnelMenuItems(state, current, rawForce, bindItems), (items, _) =>
                    {
                        if (items == 0) return Result(false, 0x5831);
                        var owner = PersonnelMenuDistrictForce(current, districtId);
                        return WithPersonnelMenuCandidates(current, PersonnelMenuItemRecipients(current, owner), (people, first) =>
                        {
                            if (people == 0) return Result(false, 0x5832);
                            if (people == 1)
                            {
                                int person = first is int physical ? physical : throw MainCommandMissing("personnel_items_first_person");
                                if (MainNeed(current.ReadPersonNativeValidity(person), "personnel_items_first_validity") &&
                                    MainNeed(current.ReadPersonStatus(person), "personnel_items_first_status") == 1)
                                    return Result(false, 0x5832);
                            }
                            return Result(true, 0x582F);
                        });
                    });
                }
                case MapMenuCommand.Reward:
                {
                    //70FE10 explicitly writes help0 before testing District.
                    if (!MainNeed(current.ReadDistrictNativeValidity(district), "personnel_reward_district_validity"))
                        return Result(false, 0);
                    bindCurrent();
                    if (CountPersonnelMenuCandidates(current,
                        ReadRewardCandidatesCurrent(state, current, districtId).Select(row => row.PhysicalSlot)).Count == 0)
                        return Result(false, 0x57DB);
                    if (Books() < ReadPersonnelRewardParameter(state, PersonnelRewardBookCostKey)) return Result(false, 0x57C7);
                    if (Gold() < 100) return Result(false, 0x57DA);
                    return Result(true, 0x57D8);
                }
                case MapMenuCommand.Merit:
                case MapMenuCommand.Award:
                {
                    bool rankup = command == MapMenuCommand.Merit;
                    if (!MainNeed(current.ReadDistrictNativeValidity(district), "personnel_credit_district_validity"))
                        return Result(false, 0);
                    if (Books() < CreditParameter(state, rankup ? "rankup-books" : "give-books", 1))
                        return Result(false, rankup ? 0x5C6D : 0x5C71);
                    if (Gold() < CreditParameter(state, rankup ? "rankup-gold" : "give-gold", rankup ? 100 : 2500))
                        return Result(false, rankup ? 0x5C6E : 0x5C72);
                    bindCurrent();
                    bool any = CountPersonnelMenuCandidates(current,
                        PersonnelMenuCreditPeople(state, current, districtId, rankup)).Count != 0;
                    return Result(any, rankup ? any ? 0x5C6C : 0x5C6F : any ? 0x5C70 : 0x5C73);
                }
                case MapMenuCommand.Expel:
                {
                    bindCurrent();
                    bool any = CountPersonnelMenuCandidates(current,
                        PersonnelMenuExpelPeople(state, current, districtId)).Count > 0;
                    return Result(any, any ? 0x5833 : 0x5834);
                }
                case MapMenuCommand.Learn:
                {
                    //70BE30 does not write the existing help on invalid District.
                    if (!MainNeed(current.ReadDistrictNativeValidity(district), "personnel_learn_district_validity"))
                        return new(command, false, null) { RetainGameMessage = true };
                    if (Books() < LearnParameter(state, "command-books", 1)) return Result(false, 0x57C7);
                    bindCurrent();
                    return WithPersonnelMenuCandidates(current, PersonnelMenuLearnPeople(current, force), (people, _) =>
                    {
                        if (people < 1) return Result(false, 0x5983);
                        bool books = CountPersonnelMenuCandidates(current, PersonnelMenuLearnBooks(state, current, force)).Count > 0;
                        return Result(books, books ? 0x2AD2 : 0x5982);
                    });
                }
                case MapMenuCommand.Introduce:
                {
                    if (!MainNeed(current.ReadDistrictNativeValidity(district), "personnel_introduce_district_validity"))
                        return new(command, false, null) { RetainGameMessage = true };
                    if (restricted is null) throw MainCommandMissing("personnel_introduce_application_selection");
                    if (restricted.Value) return Result(false, 0x11171);
                    if (Books() < MediationParameter(state, MediationBooksKey)) return Result(false, 0x57C7);
                    if (Gold() < MediationParameter(state, MediationGoldKey)) return Result(false, 0x57DA);
                    bindCurrent();
                    bool available = PersonnelMenuHasMediationPeople(state, current, PersonnelMenuDistrictForce(current, districtId));
                    return Result(available, available ? 0x5835 : 0x5836);
                }
                default: throw new ArgumentOutOfRangeException(nameof(command));
            }
        }
        catch (InvalidDataException error) { return new(command, null, null, error.Message); }
    }

    private MapMainCommandEntryPresentation ReadPersonnelHireCommandEntry(GameState state, SourceWorldContext current,
        ForceReference force, DistrictReference district, int districtId, Action bindCurrent)
    {
        const MapMenuCommand command = MapMenuCommand.Employ;
        if (!MainNeed(current.ReadForceNativeValidity(force), "personnel_hire_force_validity") ||
            !MainNeed(current.ReadDistrictNativeValidity(district), "personnel_hire_district_validity")) return new(command, false, 0x5CF8);
        var home = ReadPersonnelHireTreasuryCurrent(current, districtId);
        if (!MainNeed(current.ReadBaseNativeValidity(home), "personnel_hire_home_validity")) return new(command, false, 0x5CF8);
        if (!PersonnelHireServiceAllowedCurrent(state)) return new(command, false, 0x5CF9);
        int own = MainNeed(force.PhysicalSlot, "personnel_hire_force_identity");
        var manager = state.PersonnelHire ?? throw MainCommandMissing("personnel_hire_manager");
        var roster = manager.Forces.GetValueOrDefault(own) ?? throw MainCommandMissing("personnel_hire_roster");
        int rawRank = MainNeed(current.ReadMoraleWaveForceRank47(own), "personnel_hire_rank");
        var rank = current.ReadStaticTableReference(0x78490, rawRank < 13 ? rawRank : 0)
            ?? throw MainCommandMissing("personnel_hire_rank_pointer");
        int rankId = MainNeed(rank.PhysicalSlot, "personnel_hire_rank_identity");
        int capacity = rankId is < 1 or > 12 ? 0 : HireParameter(state,
            rankId is >= 4 and <= 9 ? $"rank-{rankId}-limit" : "other-rank-limit");
        if (Math.Min(manager.RemainingSlots, capacity - roster.HiredOfficerIds.Length) < 1) return new(command, false, 0x5CE0);
        if (MainNeed(current.ReadDistrictRawByte(districtId, 0x14), "personnel_hire_books") < HireParameter(state, "command-book-cost"))
            return new(command, false, 0x57C7);
        if (BaseCurrencyAdditionSource.ReadCurrentGold(home, current) < HireParameter(state, "gold-per-person"))
            return new(command, false, 0x5A1E);
        bindCurrent();
        return WithPersonnelMenuCandidates(current, Templates(), (offers, _) =>
        {
            if (offers == 0) return new MapMainCommandEntryPresentation(command, false, 0x5CF8);
            bool cities = CountPersonnelMenuCandidates(current, Cities()).Count > 0;
            return new MapMainCommandEntryPresentation(command, cities, cities ? 0x5CF7 : 0x5CF8);
        });

        IEnumerable<PersonnelHireTemplate> Templates()
        {
            var catalog = manager.Templates ?? throw MainCommandMissing("personnel_hire_templates");
            foreach (int id in roster.AvailableTemplateOfficerIds)
            {
                var template = catalog.GetValueOrDefault(id) ?? throw MainCommandMissing("personnel_hire_template");
                var person = template.Person;
                if (MainNeed(NativeValidity.PersonWithBehavior(template.Id, checked((ushort)person.Id), person.Behavior,
                    OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus)), "personnel_hire_template_validity")) yield return template;
            }
        }
        IEnumerable<BaseReference> Cities()
        {
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw MainCommandMissing("personnel_hire_city_pointer");
                if (MainNeed(current.ReadBaseForceSlot(city), "personnel_hire_city_force") != own ||
                    !MainNeed(current.ReadBaseNativeValidity(city), "personnel_hire_city_validity")) continue;
                yield return city;
            }
        }
    }

    private static ForceReference PersonnelMenuDistrictForce(SourceWorldContext current, int districtId)
    {
        int raw = MainNeed(current.ReadDistrictForceId(districtId), "personnel_district_force");
        return current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw MainCommandMissing("personnel_district_force_pointer");
    }

    private static IEnumerable<int> PersonnelMenuServingPeople(SourceWorldContext current, int forceId,
        int districtId, bool wholeForce)
    {
        int ordinal = wholeForce ? forceId : districtId - MainNeed(
            (current.ReadDistrictTableReference(0) ?? throw MainCommandMissing("personnel_district_anchor")).PhysicalSlot,
            "personnel_district_anchor_identity");
        if (ordinal is < 1 or > 51) yield break;
        foreach (int? value in current.ReadRegisteredPersonPhysicalSlots() ?? throw MainCommandMissing("personnel_person_registry"))
        {
            int person = MainNeed(value, "personnel_person_receiver");
            if (wholeForce ? MainNeed(current.ReadPersonOldForceSlot(person), "personnel_person_force") != ordinal :
                MainNeed(current.ReadPersonDistrictSlot(person), "personnel_person_district") != ordinal) continue;
            if (MainNeed(current.ReadPersonStatus(person), "personnel_person_status") is >= 1 and <= 4) yield return person;
        }
    }

    private IEnumerable<int> PersonnelMenuTalentExecutors(SourceWorldContext current, int districtId)
    {
        foreach (int person in PersonnelMenuServingPeople(current, 0, districtId, false))
        {
            if (MainNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "personnel_talent_health") >= 2 ||
                (MainNeed(current.ReadArmyReturnPersonFlags196(person), "personnel_talent_flags") & 1) != 0 ||
                DuelAftermathSpecialPerson(current, person) || PersonCurrentBusySource.Read(current, person, MainCommandMissing)) continue;
            yield return person;
        }
    }

    private IEnumerable<int> PersonnelMenuItemRecipients(SourceWorldContext current, ForceReference force)
    {
        if (!MainNeed(current.ReadForceNativeValidity(force), "personnel_items_force_validity")) yield break;
        foreach (int person in PersonnelMenuServingPeople(current, MainNeed(force.PhysicalSlot, "personnel_items_force_identity"), 0, true))
        {
            if (MainNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "personnel_items_health") >= 2 ||
                (MainNeed(current.ReadArmyReturnPersonFlags196(person), "personnel_items_flags") & 1) != 0 ||
                DuelAftermathSpecialPerson(current, person) ||
                (MainNeed(current.ReadArmyReturnPersonFlags196(person), "personnel_items_special_flags") & 0x4000) != 0 ||
                PersonCurrentBusySource.Read(current, person, MainCommandMissing)) continue;
            yield return person;
        }
    }

    private static IEnumerable<RegistryObjectReference> PersonnelMenuItems(GameState state, SourceWorldContext current, int forceId, Action bindItems)
    {
        foreach (var item in current.ReadRegistryReferences(RegistryKind.Item) ?? throw MainCommandMissing("personnel_item_registry"))
        {
            if (item is not { Kind: RegistryKind.Item, PhysicalSlot: int physical }) throw MainCommandMissing("personnel_item_receiver");
            bindItems(); //The header shares the actual Item4A/50 owner binding across reached queries.
            if (!MainNeed(current.ReadRegistryObjectNativeValidity(item), "personnel_item_validity")) continue;
            byte flags = state.Items?.GetValueOrDefault(physical)?.Lifecycle?.RetainedFlags ?? throw MainCommandMissing("personnel_item_flags");
            if ((flags & 1) == 0) continue;
            int raw = MainNeed(current.ReadCaptureItemOwner4A(physical), "personnel_item_holder");
            var pointer = current.ReadPersonLookupReference(raw <= 6000 ? raw : 6000) ?? throw MainCommandMissing("personnel_item_holder_pointer");
            int person = MainNeed(pointer.PhysicalSlot, "personnel_item_holder_identity");
            if (!MainNeed(current.ReadPersonNativeValidity(person), "personnel_item_holder_validity") ||
                MainNeed(current.ReadPersonOldForceSlot(person), "personnel_item_holder_force") != forceId) continue;
            var linked = current.ReadPersonLinkedEntity(person) ?? throw MainCommandMissing("personnel_item_holder_linked");
            if (MainNeed(current.ReadLinkedEntityNativeValidity(linked), "personnel_item_holder_linked_validity") ||
                MainNeed(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, person), "personnel_item_holder_army")) continue;
            byte status = MainNeed(current.ReadPersonStatus(person), "personnel_item_holder_status");
            if (status is >= 1 and <= 5 or > 9) yield return item;
        }
    }

    private IEnumerable<int> PersonnelMenuCreditPeople(GameState state, SourceWorldContext current, int districtId, bool rankup)
    {
        var force = PersonnelMenuDistrictForce(current, districtId);
        if (!MainNeed(current.ReadForceNativeValidity(force), "personnel_credit_force_validity")) yield break;
        int id = MainNeed(force.PhysicalSlot, "personnel_credit_force_identity");
        bool wholeForce = TalentForceIsPlayerCurrent(state, current, force) &&
            ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot == districtId;
        foreach (int person in PersonnelMenuServingPeople(current, id, districtId, wholeForce))
            if (CreditEligibleCurrent(state, current, person, rankup)) yield return person;
    }

    private IEnumerable<int> PersonnelMenuLearnPeople(SourceWorldContext current, ForceReference force)
    {
        if (!MainNeed(current.ReadForceNativeValidity(force), "personnel_learn_force_validity")) yield break;
        foreach (int person in PersonnelMenuServingPeople(current, MainNeed(force.PhysicalSlot, "personnel_learn_force_identity"), 0, true))
        {
            if ((MainNeed(current.ReadArmyReturnPersonFlags196(person), "personnel_learn_flags") & 1) != 0 ||
                PersonCurrentBusySource.Read(current, person, MainCommandMissing) || DuelAftermathSpecialPerson(current, person) ||
                (MainNeed(current.ReadArmyReturnPersonFlags196(person), "personnel_learn_special_flags") & 0x4000) != 0) continue;
            yield return person;
        }
    }

    private static IEnumerable<GemReference> PersonnelMenuLearnBooks(GameState state, SourceWorldContext current, ForceReference force)
    {
        if (!MainNeed(current.ReadForceNativeValidity(force), "personnel_learn_book_force_validity")) yield break;
        for (int slot = 1; slot <= 30; slot++)
        {
            var gem = current.ReadSkillGemReference(slot) ?? throw MainCommandMissing("personnel_learn_gem_pointer");
            if (MainNeed(TalentGemAcquisitionSource.Read(state.GemForceInventory, force, gem), "personnel_learn_gem_count") != 0 &&
                MainNeed(current.ReadSkillGemValidity(gem), "personnel_learn_gem_validity")) yield return gem;
        }
    }

    private IEnumerable<int> PersonnelMenuExpelPeople(GameState state, SourceWorldContext current, int districtId)
    {
        var force = PersonnelMenuDistrictForce(current, districtId);
        if (!MainNeed(current.ReadForceNativeValidity(force), "personnel_expel_force_validity")) yield break;
        int own = MainNeed(force.PhysicalSlot, "personnel_expel_force_identity");
        foreach (int person in PersonnelMenuServingPeople(current, own, districtId, true))
        {
            if (!MainNeed(current.ReadPersonNativeValidity(person), "personnel_expel_person_validity") ||
                MainNeed(current.ReadPersonStatus(person), "personnel_expel_person_status") == 1 || DuelAftermathSpecialPerson(current, person)) continue;
            if (AtOwnedBase(person) && (MainNeed(current.ReadArmyReturnPersonFlags196(person), "personnel_expel_person_flags") & 1) == 0)
                yield return person;
        }
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw MainCommandMissing("personnel_expel_captive_registry"))
        {
            int person = MainNeed(entry, "personnel_expel_captive_receiver");
            if (MainNeed(current.ReadPersonStatus(person), "personnel_expel_captive_status") == 6 && AtOwnedBase(person)) yield return person;
        }

        bool AtOwnedBase(int person)
        {
            ushort raw = MainNeed(current.ReadPersonSecondLocation(person), "personnel_expel_position");
            if (raw > 61) return false;
            var home = BaseLookup.Find(raw, current);
            return MainNeed(current.ReadBaseNativeValidity(home), "personnel_expel_position_validity") &&
                MainNeed(current.ReadBaseForceSlot(home), "personnel_expel_position_force") == own;
        }
    }

    private static bool PersonnelMenuHasMediationPeople(GameState state, SourceWorldContext current, ForceReference force)
    {
        int males = 0, females = 0, swornMales = 0, ordinary = 0;
        foreach (int person in PersonnelMenuServingPeople(current, MainNeed(force.PhysicalSlot, "personnel_introduce_force_identity"), 0, true))
        {
            int id = MainNeed(current.ReadPersonIdentity(person), "personnel_introduce_person_identity");
            var sex = MainNeed(state.Officers.GetValueOrDefault(id)?.Sex, "personnel_introduce_person_sex");
            if (MediationRelation(state, person, 0xBA) == 0)
            { if (sex == OfficerSex.Male) males++; else if (sex == OfficerSex.Female) females++; }
            //70C070's two BC counters both increment only on A9==0.
            if (MediationRelation(state, person, 0xBC) == 0 && sex == OfficerSex.Male) swornMales++;
            if (id is >= 1 and <= 1000 or >= 2001 and <= 2088 or >= 3001 and <= 3150) ordinary++;
            if ((males > 0 && females > 0 || swornMales > 1) && ordinary > 1) return true;
        }
        return false;
    }

    private static (int Count, object? First) CountPersonnelMenuCandidates<T>(SourceWorldContext current, IEnumerable<T> candidates)
        => WithPersonnelMenuCandidates(current, candidates, static (count, first) => (Count: count, First: first));

    //The source's first filled list remains owned while the second list is
    //built. The nested reader runs before finally returns its nodes/descriptor.
    private static TResult WithPersonnelMenuCandidates<T, TResult>(SourceWorldContext current, IEnumerable<T> candidates,
        Func<int, object?, TResult> read)
    {
        var list = FinalizationTemporary(current);
        object? first = null;
        try
        {
            current.ClearTemporaryHexList(list);
            foreach (var value in candidates)
            {
                if (MainNeed(current.ReadRegistryLockEnabled(), "personnel_menu_append_lock")) current.EnterRegistryLock();
                try
                {
                    var node = current.AllocateHexListNode(list);
                    if (node.Identity is not int cursor) continue;
                    current.WriteCurrentListPayload(cursor, value);
                    first ??= value;
                }
                finally { if (MainNeed(current.ReadRegistryLockEnabled(), "personnel_menu_append_unlock")) current.ExitRegistryLock(); }
            }
            return read(MainNeed(current.ReadTemporaryHexListCount(list), "personnel_menu_candidate_count"), first);
        }
        finally { FinalizationRelease(current, list); }
    }
}

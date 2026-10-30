using San14.Core;

namespace San14.Data;

internal sealed record TalentSearchCompletionResult(GameState? State, int SavedSample,
    int? FoundPersonId, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    //These are queries only. Advisor construction must not discover or recruit a person.
    internal TalentSearchPersonResult TrySearchCurrent(GameState state, SourceWorldContext current,
        int executorPhysical, AreaReference area, int savedSample, Func<TacticSourceSamplerWorld> readSampler)
    {
        var manager = current.ReadOwnedPersonCommandManager() ?? throw SearchOutcomeMissing("command_manager");
        var disappearances = manager.Disappearances ?? throw SearchOutcomeMissing("Disappear_manager");
        var excluded = new List<int>();
        foreach (var entry in manager.DispatchOrder())
        {
            if (entry.Kind != PersonnelCommandKind.Disappear) continue;
            var task = disappearances[entry.Id];
            if (task.MemberIds.Length == 0) continue;
            int raw = task.MemberIds[0];
            var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw SearchOutcomeMissing("Disappear_first_member_pointer");
            excluded.Add(SearchNeed(person.PhysicalSlot, "Disappear_first_member"));
        }
        return TalentSearchSource.ResolvePerson(state, current, executorPhysical, area, excluded,
            readSampler, SearchAbilities(current), savedSample);
    }

    internal TalentSearchFallbackTrialResult TrySearchSceneryCurrent(GameState state, SourceWorldContext current,
        int executorPhysical, AreaReference area, int savedSample, Func<TacticSourceSamplerWorld> readSampler)
        => TalentSearchFallbackSource.TryScenery(state, current, executorPhysical, area, savedSample,
            readSampler, SearchAbilities(current), SearchFallbackInputs(state, current));

    internal TalentSearchFallbackTrialResult TrySearchItemCurrent(GameState state, SourceWorldContext current,
        int executorPhysical, AreaReference area, int savedSample, Func<TacticSourceSamplerWorld> readSampler)
        => TalentSearchFallbackSource.TryItem(state, current, executorPhysical, area, savedSample,
            readSampler, SearchAbilities(current), SearchFallbackInputs(state, current));

    internal TalentSearchFallbackTrialResult TrySearchGoldCurrent(GameState state, SourceWorldContext current,
        int executorPhysical, AreaReference area, int savedSample, Func<TacticSourceSamplerWorld> readSampler)
        => TalentSearchFallbackSource.TryGold(state, current, executorPhysical, area, savedSample,
            readSampler, SearchAbilities(current), SearchFallbackInputs(state, current));

    private TacticRemakeCurrentAbilities SearchAbilities(SourceWorldContext current)
        => new(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));

    private TalentSearchFallbackInputs SearchFallbackInputs(GameState state, SourceWorldContext current)
        => new(area => current.ReadAssignmentArea36(SearchNeed(area.PhysicalSlot, "Area_pointer")),
            (gem, index) => TalentGemDiscoveryInputs.Read(state.TalentGemDiscoveries, gem, index) is { } site
                ? new(site.Area, site.Remaining) : null,
            () => ReadTalentSceneryControlCurrent(state, current),
            () => state.NativeMapSeason?.SecondDate ?? throw SearchOutcomeMissing("calendar_date"));

    //1DC420. The enclosing personnel stage owns capture, retirement and publication.
    internal TalentSearchCompletionResult CompleteSearchCurrent(GameState input, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        int sample = task.SavedSample;
        int? foundId = null;
        try
        {
            if (task.MemberIds.Length == 0) throw SearchOutcomeMissing("executor_member");
            int executor = current.ResolveOfficerSource(task.MemberIds[0]) ?? throw SearchOutcomeMissing("executor");
            var target = current.ReadHexTableReference(task.TargetHexId ?? throw SearchOutcomeMissing("target_Hex"))
                ?? throw SearchOutcomeMissing("target_Hex_pointer");
            int rawArea = SearchNeed(current.ReadHexAreaSlot(target), "target_Hex12");
            var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0)
                ?? throw SearchOutcomeMissing("target_Area_pointer");
            //Capture040020/32C7D0/0400E0 before discovery or any join writer.
            byte rawDistrict = SearchNeed(current.ReadPersonDistrictSlot(executor), "executor_E0");
            var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
                ?? throw SearchOutcomeMissing("executor_District");
            var home = BaseLookup.Find(SearchNeed(current.ReadPersonFirstLocation(executor), "executor_E2"), current);
            byte rawForce = SearchNeed(current.ReadDistrictForceId(SearchNeed(district.PhysicalSlot, "District_pointer")), "District10");
            var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                ?? throw SearchOutcomeMissing("executor_Force");
            ushort rulerId = SearchNeed(current.ReadForceRecord(SearchNeed(force.PhysicalSlot, "Force_pointer"))?.RawRuler10, "Force_ruler10");
            var ruler = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId) : current.ReadTransferPersonTableReference(0))
                ?? throw SearchOutcomeMissing("captured_ruler");
            TacticSourceSamplerWorld? sampler = null;
            TacticSourceSamplerWorld Sampler() => sampler ??= ReadArmyDestructionSampler(input, current)
                ?? throw SearchOutcomeMissing("source_sampler");
            var selected = TrySearchCurrent(input, current, executor, area, sample, Sampler);
            var personReceipt = selected.Receipt ?? throw SearchOutcomeMissing(selected.UnresolvedOwner ?? "Person_trial");
            sample = personReceipt.UsedSample;
            var state = input;
            int outcome, merit;
            if (personReceipt.FoundPersonPhysical is int found && SearchNeed(current.ReadPersonNativeValidity(found), "found_validity"))
            {
                foundId = SearchNeed(current.ReadPersonIdentity(found), "found_Person10");
                state = ApplyTalentSearchDiscoveryNoticeCurrent(state, current, executor, found, area, effects);
                state = Discover(state, found, foundId.Value);
                //The recruitment attempt has an independent local sample initialized to-1.
                var trial = TryRecruitCurrent(state, current, found, executor, -1, Sampler);
                merit = unchecked(TalentRecruitParameters.Read(state, "search-common-merit") * 10);
                if (trial.Accepted)
                {
                    int strong = 0;
                    var abilities = SearchAbilities(current);
                    for (int dimension = 0; dimension < 5; dimension++)
                        if ((abilities.ResolveDimension(state, foundId.Value, dimension,
                            (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)) ?? throw SearchOutcomeMissing("found_ability22FE40")) > 80) strong++;
                    int bonus = unchecked(TalentRecruitParameters.Read(state, "employ-success-ability-count-scale") * strong);
                    merit = unchecked(merit + unchecked(bonus + TalentRecruitParameters.Read(state, "employ-success-merit-base")) * 10);
                }
                state = SearchConversation(state, current, executor, found, trial.Accepted, district, effects);
                state = ApplyTalentSearchPersonReportCurrent(state, current, task, date, executor, found, trial.Accepted, effects);
                if (trial.Accepted)
                {
                    state = ApplyRecruitJoinCurrent(state, current, found, district, home, date, () => Sampler().Day450, effects).State;
                    state = ApplyRecruitInitialLoyaltyCurrent(state, current, found, ruler);
                    task = task with { TargetOfficerId = foundId, MemberIds = task.MemberIds.Append(foundId.Value).ToArray() };
                    current.SetLinkedEntityMemberIds(task.Id, task.MemberIds.Select(id => (uint?)id).ToArray());
                    outcome = 1;
                }
                else { state = Discover(state, found, foundId.Value); outcome = 2; }
            }
            else
            {
                var fallback = TalentSearchFallbackSource.Resolve(state, current, executor, area, sample,
                    Sampler, SearchAbilities(current), SearchFallbackInputs(state, current));
                var receipt = fallback.Receipt ?? throw SearchOutcomeMissing(fallback.UnresolvedOwner ?? "fallback_trial");
                sample = receipt.SampleAfter;
                switch (receipt.Kind)
                {
                    case TalentSearchFallbackKind.Gold:
                        merit = unchecked(TalentRecruitParameters.Read(state, "search-gold-merit") * 10);
                        state = BaseCurrencyAdditionSource.Add(state, current, home, receipt.Gold,
                            city => BasePostLoad.ReadGoldCapacity(city, current)).State;
                        if (SearchNeed(current.ReadDistrictNativeValidity(district), "gold_District_validity") &&
                            BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, SearchOutcomeMissing))
                        {
                            var city = SearchAreaCity(current, area);
                            state = AppendTalentSearchHistoryCurrent(state, current, task, date, executor,
                                0xC39F, 0x51C5, new(Person0: SearchNeed(current.ReadPersonIdentity(executor), "executor_ID"),
                                    Person1: SearchNeed(current.ReadPersonIdentity(SearchNeed(ruler.PhysicalSlot, "ruler_pointer")), "ruler_ID"),
                                    Area0: SearchNeed(area.PhysicalSlot, "Area_pointer"), Base0: SearchNeed(current.ReadBaseObjectLink(city), "parent_City4E"),
                                    Integers: [receipt.Gold]), effects, 0, 0, hexOverride: 0,
                                mainOperands: new(Person0: SearchNeed(current.ReadPersonIdentity(executor), "executor_ID"), Integers: [receipt.Gold]));
                        }
                        outcome = 4;
                        break;
                    case TalentSearchFallbackKind.Scenery:
                        merit = unchecked(TalentRecruitParameters.Read(state, "search-scenery-merit") * 10);
                        var scenery = current.ReadSceneryTableReference(receipt.PhysicalSlot ?? throw SearchOutcomeMissing("found_Scenery_pointer"))
                            ?? throw SearchOutcomeMissing("found_Scenery_table");
                        state = ApplyTalentSceneryOutcomeCurrent(state, current, task, date, executor, scenery, force, effects);
                        outcome = 5;
                        break;
                    case TalentSearchFallbackKind.Item:
                        merit = unchecked(TalentRecruitParameters.Read(state, "search-item-merit") * 10);
                        int physical = receipt.PhysicalSlot ?? throw SearchOutcomeMissing("found_Item_pointer");
                        state = SearchAcquireItem(state, current, physical, rulerId);
                        if (SearchNeed(current.ReadForceNativeValidity(force), "Item_report_Force_validity") &&
                            TalentForceIsPlayerCurrent(state, current, force))
                        {
                            var item = state.Items![physical];
                            string name = state.ItemDefinitions.TryGetValue(item.DefinitionId, out var definition)
                                ? definition.Name : throw SearchOutcomeMissing("Item_name");
                            state = AppendTalentSearchHistoryCurrent(state, current, task, date, executor,
                                0xC39D, 0x51BD, new(Person0: SearchNeed(current.ReadPersonIdentity(executor), "executor_ID"),
                                    Person1: SearchNeed(current.ReadPersonIdentity(SearchNeed(ruler.PhysicalSlot, "ruler_pointer")), "ruler_ID"),
                                    String0: name, Area0: SearchNeed(area.PhysicalSlot, "Area_pointer"),
                                    Base0: SearchNeed(current.ReadBaseObjectLink(SearchAreaCity(current, area)), "parent_City4E")), effects, 1, 1,
                                mainOperands: new(Person0: SearchNeed(current.ReadPersonIdentity(executor), "executor_ID"), String0: name));
                        }
                        outcome = 3;
                        break;
                    case TalentSearchFallbackKind.Gem:
                        merit = unchecked(TalentRecruitParameters.Read(state, "search-item-merit") * 10);
                        int gemPhysical = receipt.PhysicalSlot ?? throw SearchOutcomeMissing("found_Gem_pointer");
                        var gem = current.ReadSkillGemReference(gemPhysical) ?? throw SearchOutcomeMissing("found_Gem_table");
                        state = ApplyTalentGemAcquisitionCurrent(state, current, force, gem, area);
                        if (SearchNeed(current.ReadForceNativeValidity(force), "Gem_report_Force_validity") &&
                            TalentForceIsPlayerCurrent(state, current, force))
                        {
                            string name = _skills.Gems.Gems[gemPhysical].Name ?? throw SearchOutcomeMissing("Gem_name");
                            state = AppendTalentSearchHistoryCurrent(state, current, task, date, executor,
                                0xC39D, 0x51BD, new(Person0: SearchNeed(current.ReadPersonIdentity(executor), "executor_ID"),
                                    Person1: SearchNeed(current.ReadPersonIdentity(SearchNeed(ruler.PhysicalSlot, "ruler_pointer")), "ruler_ID"),
                                    String0: name, Area0: SearchNeed(area.PhysicalSlot, "Area_pointer"),
                                    Base0: SearchNeed(current.ReadBaseObjectLink(SearchAreaCity(current, area)), "parent_City4E")), effects, 1, 1,
                                mainOperands: new(Person0: SearchNeed(current.ReadPersonIdentity(executor), "executor_ID"), String0: name));
                        }
                        outcome = 3;
                        break;
                    default:
                        int specialPerson = TrySpecialSearchCurrent(state, current, date, task, executor, area, 5006, Sampler)
                            ? 5006 : TrySpecialSearchCurrent(state, current, date, task, executor, area, 5008, Sampler) ? 5008 : 0;
                        if (specialPerson != 0)
                        {
                            state = ApplySpecialSearchEncounterCurrent(state, current, executor, area, specialPerson, effects);
                            merit = unchecked(TalentRecruitParameters.Read(state, "search-common-merit") * 10);
                            outcome = 1;
                            break;
                        }
                        merit = unchecked(TalentRecruitParameters.Read(state, "search-no-find-merit") * 10);
                        if (SearchNeed(current.ReadDistrictNativeValidity(district), "no_find_District") &&
                            BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, SearchOutcomeMissing))
                        {
                            var city = SearchAreaCity(current, area);
                            state = AppendTalentSearchHistoryCurrent(state, current, task, date, executor,
                                0xC3A9, null, new(Person0: SearchNeed(current.ReadPersonIdentity(executor), "executor_ID"),
                                    Base0: SearchNeed(current.ReadBaseObjectLink(city), "parent_City4E")),
                                effects, 0, 0, hexOverride: 0);
                        }
                        outcome = 2;
                        break;
                }
            }
            task = task with { SavedSample = sample };
            var manager = current.ReadOwnedPersonCommandManager() ?? throw SearchOutcomeMissing("completion_manager");
            manager.Talents![task.Id] = task;
            current.SetOwnedPersonCommandManager(manager);
            state = state with { PersonnelCommands = manager };
            state = ApplyTalentCommonRewardsCurrent(state, current, executor, 3, outcome);
            state = ApplyTalentPendingMeritCurrent(state, current, executor, merit);
            state = ScheduleTalentCompletionReturnCurrent(state, current, task.Id, task.MemberIds, date,
                TalentForceIsPlayerCurrent(state, current, force));
            return new(state, sample, foundId, null);
        }
        catch (InvalidDataException error) { return new(null, sample, foundId, error.Message); }

        GameState Discover(GameState state, int physical, int id)
        {
            if (!state.Officers.TryGetValue(id, out var person)) throw SearchOutcomeMissing("found_domain_Person");
            current.WritePersonStatus(physical, 5);
            return state with { Officers = new(state.Officers) { [id] = person with
            { ServiceStatus = OfficerServiceStatus.Free, Role = OfficerRole.Free, Presence = OfficerPresence.Present } } };
        }
    }

    private GameState SearchConversation(GameState state, SourceWorldContext current,
        int executor, int found, bool accepted, DistrictReference district, List<CampaignEffectRequest> effects)
    {
        if (!SearchNeed(current.ReadDistrictNativeValidity(district), "conversation_District_validity") ||
            !BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, SearchOutcomeMissing)) return state;
        var force = current.ReadPersonForce(found) ?? throw SearchOutcomeMissing("conversation_found_Force");
        if (TalentForceIsPlayerCurrent(state, current, force)) return state;
        return AppendTalentRecruitConversationCurrent(state, current, executor, found, accepted, effects);
    }

    private static BaseReference SearchAreaCity(SourceWorldContext current, AreaReference area)
    {
        byte raw = SearchNeed(current.ReadAreaCitySlot(area), "Area35");
        return current.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw SearchOutcomeMissing("Area_parent_City");
    }

    private static GameState SearchAcquireItem(GameState state, SourceWorldContext current, int physical, ushort rulerId)
    {
        if (state.Items is not { } items || !items.TryGetValue(physical, out var item) || item.Lifecycle is not { } lifecycle)
            throw SearchOutcomeMissing("found_Item_owned_lifecycle");
        byte flags = SearchNeed(lifecycle.RetainedFlags, "Item4F");
        //The caller clears bit3 even when324580 rejects an invalid ruler ID.
        item = item with { Lifecycle = lifecycle with { RetainedFlags = (byte)(flags & 0xF7) } };
        state = state with { Items = new(items) { [physical] = item } };
        if (rulerId is < 1 or > 6000) return state;
        var pointer = current.ReadRegistryTableReference(RegistryKind.Item, physical) ?? throw SearchOutcomeMissing("Item_table_pointer");
        byte previous = SearchNeed(current.ReadRegistryItemRawField50(pointer), "Item50");
        current.SetCaptureItemOwner4A(physical, rulerId);
        item = item with { Holder = new(rulerId) };
        if (previous != 1)
        {
            current.SetRegistryItemRawField50(physical, 1);
            item = item with { Lifecycle = item.Lifecycle! with { Status = ItemStatusKnowledge.Exact(1) } };
            //31F990 uses the existing shared descriptor and allocation-failure semantics.
            _ = ObjectRegistration.Register(pointer, current);
        }
        return state with { Items = new(state.Items!) { [physical] = item } };
    }
    private static T SearchNeed<T>(T? value, string field) where T : struct => value ?? throw SearchOutcomeMissing(field);
    private static InvalidDataException SearchOutcomeMissing(string field) => new("talent-search." + field);
}

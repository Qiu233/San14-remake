using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableArmyFoodShortageRules
{
    public GameState ApplyArmyFoodShortages(GameState state, IReadOnlyList<PlayableArmyFoodShortage> shortages)
    {
        if (shortages.Count == 0) return state;
        return ApplyArmyFoodShortagesCurrent(state, CaptureOwnedArmyReturnWorld(state).Context, shortages);
    }

    private GameState ApplyArmyFoodShortagesCurrent(GameState state, SourceWorldContext current,
        IReadOnlyList<PlayableArmyFoodShortage> shortages)
    {
        bool panelsBound = _baseCapacityInputs.BindCurrent(state, current);
        current.MaterializeFixedScenery(_landmarkDefinitions);
        for (int slot = 0; slot < 101; slot++)
            current.SetSceneryFlags47(slot, state.LandmarkInvestments?.GetValueOrDefault(slot)?.Flags);
        var pool = current.ReadOwnedCurrentListPool() ?? throw FoodShortageMissing("current_pool");
        var working = state;
        var reports = new List<PeriodReportEntry>();
        var effects = new List<CampaignEffectRequest>();
        var traitRefreshes = new List<CampaignEffectRequest>();
        var changedPeople = new HashSet<int>();
        TacticSourceSamplerWorld? sampler = null;
        foreach (var shortage in shortages)
        {
            //A returned/destroyed Army is not recreated from the quantity cut.
            if (!working.Units.TryGetValue(shortage.UnitId, out var image) ||
                image.ForceId != shortage.ForceId || image.OfficerId != shortage.OfficerId) continue;
            int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw FoodShortageMissing("Army_receiver");
            var army = current.ReadArmyUnitTableReference(physical) ?? throw FoodShortageMissing("Army_pointer");
            if (!FoodShortageKnown(current.ReadArmyUnitNativeValidity(army), "Army_validity")) continue;
            ushort hex = FoodShortageKnown(current.ReadArmyUnitRawHex2A(army), "Army_hex");
            var date = working.NativeMapSeason?.ThirdDate ?? throw FoodShortageMissing("current_date");
            var fatigue = ReadArmyFieldFatigueAmount(working, current, army, hex, date.Hour,
                includeBaseline: true, panelsBound, Sampler);
            int adjustment = unchecked(-3 * fatigue.Amount);
            var applied = ArmyMoraleZeroRoutingCurrent.ApplyImmediate(working, current, army, adjustment,
                TrapMobility(), Immediate);
            working = applied.State ?? throw FoodShortageMissing(applied.UnresolvedOwner ?? "morale_callback");
            if (applied.Receipt is { } receipt)
                reports.Add(new("food_unit_morale", shortage.ForceId, shortage.FoodCityId, shortage.OfficerId,
                    receipt.Before1A - receipt.After1A)
                { UnitId = shortage.UnitId, RuleId = "CE-current-food-shortage-2CCBD0" });
            //Routing/destruction effects belong to their existing owner.
            //Finish that source segment before this separate App report.
            PublishPeople();
            FlushDefeatEffects();
            var commander = Commander();
            ushort homeLocation = FoodShortageKnown(current.ReadPersonNativeValidity(commander), "report_Commander_validity")
                ? FoodShortageKnown(current.ReadPersonFirstLocation(commander), "report_CommanderE2") : (ushort)0;
            //22A730 does not test the post-routing Army or Base validity.
            var home = BaseLookup.Find(homeLocation, current);
            if (adjustment < 0)
            {
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current)
                    ?? throw FoodShortageMissing("report_player");
                if (FoodShortageKnown(current.ReadForceNativeValidity(player), "report_player_validity"))
                {
                    int raw = FoodShortageKnown(current.ReadBaseForceSlot(home), "report_Home_force");
                    var owner = current.ReadForceTableReference(raw <= 51 ? raw : 0)
                        ?? throw FoodShortageMissing("report_Home_force_pointer");
                    if (owner == player) Report();
                }
            }

            int Commander()
            {
                ushort raw = FoodShortageKnown(current.ReadArmyUnitCommanderId(army), "report_Commander12");
                return FoodShortageKnown((raw <= 6000 ? current.ReadPersonLookupReference(raw)
                    : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "report_Commander_pointer");
            }
            void Report()
            {
                //22BB10 rereads Commander and its raw District after routing.
                int freshCommander = Commander();
                int districtSlot = FoodShortageKnown(current.ReadPersonNativeValidity(freshCommander), "report_District_Commander_validity")
                    ? FoodShortageKnown(current.ReadPersonDistrictSlot(freshCommander), "report_CommanderE0") : 0;
                var district = current.ReadDistrictTableReference(districtSlot <= 51 ? districtSlot : 0)
                    ?? throw FoodShortageMissing("report_District_pointer");
                var selection = new PersonDeathHistorySelection(working, current, pool, SearchAbilities(current),
                    _governorInputs, (id, effect) => _baseCapacityInputs.ReadPanelModifier(working, id, effect));
                var speaker = selection.Select(district);
                int speakerPhysical = FoodShortageKnown(speaker.PhysicalSlot, "report_speaker_pointer");
                int speakerId = FoodShortageKnown(current.ReadPersonIdentity(speakerPhysical), "report_speaker_identity");
                ushort ruler = FoodShortageKnown(current.ReadMoraleWaveRuler22EAC0(current.ReadArmyUnitForceReference(army)), "report_ruler");
                freshCommander = Commander();
                ushort primaryHome = FoodShortageKnown(current.ReadPersonNativeValidity(freshCommander), "message_Commander_validity")
                    ? FoodShortageKnown(current.ReadPersonFirstLocation(freshCommander), "message_CommanderE2") : (ushort)0;
                var primary = FormatNormalTurnReport(working, current, 0x5E3E,
                    new(Army0: army, Base0: primaryHome, Person0: speakerId, Person1: ruler), value => working = value);
                working = primary.State;
                var supplemental = FormatNormalTurnReport(working, current, 0x5E42,
                    new(Army0: army, Base0: FoodShortageKnown(current.ReadBaseObjectLink(home), "history_Home4E")), value => working = value);
                working = supplemental.State;
                speakerId = FoodShortageKnown(current.ReadPersonIdentity(speakerPhysical), "history_speaker_identity");
                var reportEffects = new List<CampaignEffectRequest>();
                working = WriteHistory(working, current, new(5,
                    FoodShortageKnown(current.ReadBaseDistrictSlot(home), "history_Home_virtual78"),
                    ReadReportBaseMarker(current, home, FoodShortageMissing), Sampler().Day450,
                    supplemental.Text, primary.Text, speakerId, MarkBit0: 1, MarkBit4: 1), reportEffects);
                //Unlike the Garrison branch, Army queues event6 after history.
                reportEffects.Add(new(null, null) { MonthlyMaintenance =
                    new(MonthlyMaintenanceOperation.ControllerQueueEvent, 6, null, null) });
                reportEffects.Add(new(null, null) { MonthlyMaintenance =
                    new(MonthlyMaintenanceOperation.ConditionalSpeakerText, null,
                        FoodShortageKnown(current.ReadPersonIdentity(speakerPhysical), "speech_speaker_identity"), primary.Text) });
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw FoodShortageMissing("report_effect_owner")).Append(CampaignEffectSource.MonthlyMaintenance,
                        working.NativeMapSeason?.AdvancedCalendarSteps ?? throw FoodShortageMissing("report_calendar_revision"), reportEffects) };
            }
        }
        PublishPeople();
        long revision = working.NativeMapSeason?.AdvancedCalendarSteps ?? throw FoodShortageMissing("calendar_revision");
        var pending = working.CampaignEffectRequests ?? throw FoodShortageMissing("effect_owner");
        if (traitRefreshes.Count != 0) pending = pending.Append(CampaignEffectSource.ArmyTraitMaintenance, revision, traitRefreshes);
        return working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() }) with
        { PeriodReports = [.. working.PeriodReports, .. reports], CampaignEffectRequests = pending };

        void FlushDefeatEffects()
        {
            if (effects.Count == 0) return;
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                ?? throw FoodShortageMissing("effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                    working.NativeMapSeason?.AdvancedCalendarSteps ?? throw FoodShortageMissing("calendar_revision"), effects) };
            effects.Clear();
        }

        TacticSourceSamplerWorld Sampler() => sampler ??= Context(current).ReadCurrentSamplerWorld(working)
            ?? throw FoodShortageMissing("current_sampler");

        GameState Immediate(GameState value, UnitDefeatRequest request)
        {
            working = value;
            PublishPeople();
            if (current.ReadOwnedPersonCommandManager() is null)
            {
                if (working.PersonnelCommands is null) throw FoodShortageMissing("personnel_manager");
                PersonReturnCommands.BindCurrent(working, current);
            }
            working = working with { PersonnelCommands = current.ReadOwnedPersonCommandManager()! };
            BindActorlessDestructionInputs(working, current);
            working = new OwnedArmyDestructionStage(this).ApplyBattleDefeatCurrent(working, current, pool,
                request, Sampler(), working.NativeMapSeason!.ThirdDate, effects,
                receiver => traitRefreshes.Add(new(null, null)
                { ArmyTraitViewRefresh = new(ArmyDestructionQueue.ResolveUnitId(working, receiver.PhysicalSlot)
                    ?? throw FoodShortageMissing("trait_refresh_Army")) }),
                person => changedPeople.Add(person));
            return working;
        }

        void PublishPeople()
        {
            if (changedPeople.Count == 0) return;
            var people = new Dictionary<int, OfficerState>(working.Officers);
            foreach (int physical in changedPeople)
            {
                int id = FoodShortageKnown(current.ReadPersonIdentity(physical), "Person_identity");
                var person = people.GetValueOrDefault(id) ?? throw FoodShortageMissing("Person_owner");
                var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
                if (person.TraitActivation is not { DurationSlotCount: 9, ActiveSlots: not null } ||
                    activation is not { DurationSlotCount: 9, ActiveSlots: not null })
                    throw FoodShortageMissing("Person_full_activation");
                people[id] = person with { TraitActivation = activation };
            }
            working = working with { Officers = people };
            changedPeople.Clear();
        }
    }

    private static T FoodShortageKnown<T>(T? value, string field) where T : struct =>
        value ?? throw FoodShortageMissing(field);
    private static InvalidDataException FoodShortageMissing(string field) => new("food_upkeep.Army_shortage." + field);
}

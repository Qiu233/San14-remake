using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private TalentSearchSceneryControl ReadTalentSceneryControlCurrent(GameState state, SourceWorldContext current)
    {
        //570850 is the actual tutorial controller service. Its bitmap is
        //separate from the ordinary World48 field. Consume the operation
        //working state so a later task in the same stage sees a cleared bit.
        var mode = current.ArmyReturnTransferMode ?? throw SearchOutcomeMissing("Scenery_570850_owner");
        if (!mode.Present || SearchNeed(mode.Value, "Scenery_570850_value") == 0)
            return TalentSearchSceneryControl.Ordinary;
        return SearchNeed(TacticRemakeCurrentBattleControl.Read(state, 100),
            "Scenery_tutorial_control100") ? TalentSearchSceneryControl.SpecialWithDiscovery
                : TalentSearchSceneryControl.SpecialWithoutDiscovery;
    }

    //33C390 ->33C850 ->33C8C0. No new current, capture, pool export or ACK.
    internal GameState ApplyTalentSceneryOutcomeCurrent(GameState input, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date, int executor,
        SceneryReference scenery, ForceReference capturedForce, List<CampaignEffectRequest> effects)
    {
        var state = input;
        bool valid = SearchNeed(current.ReadSceneryNativeValidity(scenery), "Scenery_validity");
        if (valid && SearchNeed(current.ReadPersonNativeValidity(executor), "Scenery_executor_validity") &&
            SearchNeed(current.ReadForceNativeValidity(capturedForce), "Scenery_captured_Force_validity"))
        {
            state = ClearTalentSceneryObject(state, current, scenery, capturedForce, executor, effects);
            int physical = SearchNeed(scenery.PhysicalSlot, "Scenery_pointer");
            var owner = state.LandmarkInvestments?.GetValueOrDefault(physical)
                ?? throw SearchOutcomeMissing("Scenery_owned_development");
            //32E240 sees the current flags of all101 receivers. The newly
            //visited entry still has bit0 clear while the kind is selected.
            var enabled = new List<LandmarkDevelopmentState>();
            for (int slot = 0; slot < 101; slot++)
            {
                var entry = current.ReadSceneryTableReference(slot) ?? throw SearchOutcomeMissing("Scenery_type_table");
                if (!SearchNeed(current.ReadSceneryNativeValidity(entry), "Scenery_type_validity") ||
                    (SearchNeed(current.ReadSceneryFlags47(entry), "Scenery_type_flags47") & 1) == 0) continue;
                enabled.Add(state.LandmarkInvestments?.GetValueOrDefault(SearchNeed(entry.PhysicalSlot, "Scenery_type_pointer"))?.Development
                    ?? throw SearchOutcomeMissing("Scenery_type_current_development"));
            }
            var sampler = ReadArmyDestructionSampler(state, current) ?? throw SearchOutcomeMissing("Scenery_sampler");
            ushort hex = SearchNeed(current.ReadSceneryHexIndex(scenery), "Scenery44");
            //33C390 first selects the ordinary kind, then replaces it before
            //32E880 computes the ordinary effect. The latter still runs.
            bool tutorialDiscovery = ReadTalentSceneryControlCurrent(state, current) ==
                TalentSearchSceneryControl.SpecialWithDiscovery;
            var development = _landmarkDevelopment.Initialize(state, physical, hex, enabled, sampler,
                tutorialDiscovery ? (byte)2 : null);
            if (ReadTalentSceneryControlCurrent(state, current) == TalentSearchSceneryControl.SpecialWithDiscovery)
            {
                development = development with { Effect = 10 };
                state = SetTutorialLessonEventCurrent(state, 100, false);
            }
            byte flags = (byte)(SearchNeed(current.ReadSceneryFlags47(scenery), "Scenery_flags47") | 1);
            state = state with { LandmarkInvestments = new(state.LandmarkInvestments!)
                { [physical] = owner with { Phase = 1, Development = development, Flags = flags } } };
            current.SetSceneryFlags47(physical, flags);
            //2240A0/model creation and the optional map-manager callback are
            //presentation work; map readers consume these saved flags.
            var popularity = new OwnedAreaDevelopmentContext(state, current, _normalAftermathInputs);
            int before = SearchNeed(popularity.ReadForceDevelopmentWord1BE(capturedForce), "Scenery_Force1BE");
            popularity.WriteForceDevelopmentWord1BE(capturedForce, (short)Math.Clamp(unchecked(before +
                TalentRecruitParameters.Read(state, "scenery-discovery-popularity")), -1000, 1000));
            state = popularity.State;
        }

        //33C850 is independent of the persistent effect just initialized.
        //It always makes a fresh shuffled1/2/3 choice for a valid Scenery.
        int rewardKind = 0, amount = 0;
        if (valid)
        {
            int physical = SearchNeed(scenery.PhysicalSlot, "Scenery_reward_pointer");
            int hex = SearchNeed(current.ReadSceneryHexIndex(scenery), "Scenery_reward44");
            var sampler = ReadArmyDestructionSampler(state, current) ?? throw SearchOutcomeMissing("Scenery_reward_sampler");
            var kinds = new List<int> { 1, 2, 3 };
            ShuffleSceneryReward(kinds, unchecked(physical + sampler.Day450 + hex), sampler);
            rewardKind = kinds[0];
            amount = ReadTalentSceneryRewardAmount(state, current, scenery, rewardKind);
            state = ApplyTalentSceneryReward(state, current, scenery, rewardKind, amount, effects);
        }

        //1DC420's captured Force validity precedes its player gate.
        if (SearchNeed(current.ReadForceNativeValidity(capturedForce), "Scenery_report_Force_validity") &&
            TalentForceIsPlayerCurrent(state, current, capturedForce))
        {
            int physical = SearchNeed(scenery.PhysicalSlot, "Scenery_report_pointer");
            int executorId = SearchNeed(current.ReadPersonIdentity(executor), "Scenery_report_executor10");
            string name = current.SceneryDefinitions?.Sceneries[physical].Name
                ?? throw SearchOutcomeMissing("Scenery_report_name");
            state = AppendTalentSceneryRewardPresentationCurrent(state, current, scenery, executor,
                rewardKind, amount, name, effects);
            var area = TalentSearchAreaCurrent(current, task);
            state = AppendTalentSearchHistoryCurrent(state, current, task, date, executor, 0xC97B, 0x53C5,
                new(Person0: executorId, Person1: TalentRulerCurrent(current, current.ReadPersonForce(executor)
                    ?? throw SearchOutcomeMissing("Scenery_report_executor_Force")), String0: name,
                    Base0: SearchNeed(current.ReadBaseObjectLink(SearchAreaCity(current, area)), "Scenery_report_parent4E")),
                effects, 1, 1, hexOverride: SearchNeed(current.ReadSceneryHexIndex(scenery), "Scenery_report44"),
                mainOperands: new(Person0: executorId, String0: name));
            state = AppendTalentSceneryChronicleCurrent(state, current, scenery, capturedForce, executor, name);
        }
        return state;
    }

    private GameState AppendTalentSceneryRewardPresentationCurrent(GameState state, SourceWorldContext current,
        SceneryReference scenery, int executor, int rewardKind, int amount, string sceneryName,
        List<CampaignEffectRequest> effects)
    {
        int sceneryId = SearchNeed(scenery.PhysicalSlot, "presentation_Scenery_identity");
        int executorId = SearchNeed(current.ReadPersonIdentity(executor), "presentation_executor10");
        int area = SearchNeed(TalentSceneryArea(current, scenery).PhysicalSlot, "presentation_Area_identity");
        var city = TalentSceneryCity(current, scenery);
        ushort cityObject = SearchNeed(current.ReadBaseObjectLink(city), "presentation_City4E");
        //6C6210(kind5)/75C5A0's actual EventView line precedes the separate
        //423C20 bonus announcement. Both remain source-time, selected text.
        var discovery = FormatReportMessage(state, current, 0xC970,
            new(Person0: executorId, String0: sceneryName, Area0: area, Base0: cityObject),
            ReportMessageOrigin.Talent, null,
            _personnelNarrationVoice ?? throw SearchOutcomeMissing("presentation_voice_settings"));
        int rewardRoot = rewardKind switch { 1 => 0x5C74, 2 => 0x5C75, 3 => 0x5C76,
            _ => throw SearchOutcomeMissing("presentation_reward_kind") };
        int? force = rewardKind == 3 ? SearchNeed(current.ReadForcePhysicalSlot(
            current.ReadPersonForce(executor) ?? throw SearchOutcomeMissing("presentation_executor_Force")),
            "presentation_executor_Force60") : null;
        var reward = FormatTalentReportCurrent(discovery.State, current, rewardRoot,
            new(String0: sceneryName, Area0: area, Base0: rewardKind == 2 ? cityObject : null,
                Force0: force, Integers: [amount]));
        var officer = state.Officers.GetValueOrDefault(executorId) ?? throw SearchOutcomeMissing("presentation_executor_owner");
        string name = (officer.CurrentSurname ?? throw SearchOutcomeMissing("presentation_executor_surname")) +
            (officer.CurrentGivenName ?? throw SearchOutcomeMissing("presentation_executor_given_name"));
        if (discovery.Voices.Count > 1) throw SearchOutcomeMissing("presentation_multiple_voice_requests");
        effects.Add(new(null, null) { TalentCommandPresentation = new(
            TalentCommandPresentationOperation.SoundCue, 5, null, null, null, null) });
        effects.Add(new(null, null) { TalentSceneryReward = new(sceneryId, executorId, rewardKind, amount)
        {
            DiscoveryTextAtSource = discovery.Text,
            ExecutorNameAtSource = name, ExecutorPortraitAppearanceIndex = current.ReadPersonPortraitAppearance(executor),
            SceneryNameAtSource = sceneryName,
            Voice = discovery.Voices.Count == 0 ? null : SaveVoice(discovery.Voices[0])
        } });
        effects.Add(new(null, null) { TalentCommandPresentation = new(
            TalentCommandPresentationOperation.Announcement, null, null, null, null, reward.Text) });
        return reward.State;
    }

    private GameState ClearTalentSceneryObject(GameState state, SourceWorldContext current,
        SceneryReference scenery, ForceReference capturedForce, int executor, List<CampaignEffectRequest> effects)
    {
        ushort rawHex = SearchNeed(current.ReadSceneryHexIndex(scenery), "visit44");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw SearchOutcomeMissing("visit_Hex");
        if (!hex.PhysicalSlot.HasValue) return state; //Every nonnull CHex is native-valid.
        ushort rawObject = SearchNeed(current.ReadHexObjectLink(hex), "visit_Hex1A");
        var obj = current.ReadObjectTableReference(rawObject < 3001 ? rawObject : 0) ?? throw SearchOutcomeMissing("visit_Object");
        if (!SearchNeed(current.ReadObjectNativeValidity(obj), "visit_Object_validity")) return state;
        var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
        var area = TalentSceneryArea(current, scenery);
        if (!SearchNeed(current.ReadObjectDefinitionValidity(definition), "visit_definition_validity") ||
            !SearchNeed(current.ReadAreaNativeValidity(area), "visit_Area_validity")) return state;
        var city = SearchAreaCity(current, area);
        if (SearchNeed(current.ReadBaseNativeValidity(city), "visit_parent_City_validity") &&
            SearchNeed(current.ReadBaseForceSlot(city), "visit_parent_City_Force") ==
            SearchNeed(current.ReadForcePhysicalSlot(capturedForce), "visit_captured_Force60"))
        {
            int refund = FacilityConstructionDefinitions.Read(current, definition).BaseGoldCost;
            state = BaseCurrencyAdditionSource.Add(state, current, city, refund,
                home => BasePostLoad.ReadGoldCapacity(home, current)).State;
            int districtSlot = SearchNeed(current.ReadBaseDistrictSlot(city), "visit_City_District");
            var district = current.ReadDistrictTableReference(districtSlot < 52 ? districtSlot : 0)
                ?? throw SearchOutcomeMissing("visit_City_District_pointer");
            if (SearchNeed(current.ReadDistrictNativeValidity(district), "visit_City_District_validity") &&
                BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, SearchOutcomeMissing))
            {
                string name = current.SceneryDefinitions?.Sceneries[SearchNeed(scenery.PhysicalSlot, "visit_Scenery_pointer")].Name
                    ?? throw SearchOutcomeMissing("visit_Scenery_name");
                var text = FormatTalentReportCurrent(state, current, 0x34A0, new(String0: name));
                state = WriteHistory(text.State, current, new(1,
                    SearchNeed(current.ReadPersonDistrictSlot(executor), "visit_executor_E0"),
                    SearchNeed(hex.PhysicalSlot, "visit_Hex_identity"),
                    (ReadArmyDestructionSampler(text.State, current) ?? throw SearchOutcomeMissing("visit_day450")).Day450,
                    text.Text, CampaignHistoryWriter.Supplemental(text.State),
                    SearchNeed(current.ReadPersonIdentity(executor), "visit_executor10"), MarkBit0: 0, MarkBit4: 1), effects);
            }
        }
        //294FD0(object,null,0) has no Army trait reward, speech or text report.
        var removal = ApplyDemolitionNumbers(state, current, obj, new ArmyUnitReference(null));
        if (removal is null) return state;
        string key = MapObjectSourceDirectory.DefinitionKey(removal.DefinitionSlot);
        effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionVisual,
            removal.Receiver, removal.HexId, key) });
        effects.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionSound,
            removal.Receiver, removal.HexId, key) });
        return removal.State;
    }

    private int ReadTalentSceneryRewardAmount(GameState state, SourceWorldContext current,
        SceneryReference scenery, int kind)
    {
        var force = TalentSceneryForce(current, scenery);
        if (!SearchNeed(current.ReadForceNativeValidity(force), "reward_Force_validity")) return 0;
        ushort rulerId = SearchNeed(current.ReadForceRecord(SearchNeed(force.PhysicalSlot, "reward_Force_pointer"))?.RawRuler10, "reward_ruler10");
        var ruler = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId) : current.ReadTransferPersonTableReference(0))
            ?? throw SearchOutcomeMissing("reward_ruler_pointer");
        if (ruler.PhysicalSlot is not int person || !SearchNeed(current.ReadPersonNativeValidity(person), "reward_ruler_validity")) return 0;
        if (kind == 1) return TalentRecruitParameters.Read(state, "scenery-immediate-security");
        if (kind == 2) return TalentRecruitParameters.Read(state, "scenery-immediate-morale");
        if (kind != 3) return 0;
        int basis = TalentRecruitParameters.Read(state, "scenery-immediate-force-experience");
        int trait = unchecked(SearchNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 76), "reward_trait76") *
            SceneryArmyParameter(state, SimulationParameterKeys.ArmyDestruction.ForceExperienceTraitMultiplier));
        var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 6);
        int effect = SearchNeed(advantage.Value, advantage.UnresolvedOwner ?? "reward_effect6");
        int amount = unchecked(basis + unchecked(SceneryArmyParameter(state,
            SimulationParameterKeys.ArmyDestruction.ForceExperienceLandAdvantageMultiplier) * basis * effect) / 100 +
            unchecked(trait * basis) / 100);
        int ordinal = SearchNeed(force.PhysicalSlot, "reward_Force_pointer");
        byte flags = SearchNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(ordinal)?.Flags12, "reward_Force12");
        if (SearchNeed(current.ReadForceNativeValidity(force), "reward_late_Force_validity") && (flags & 8) != 0)
            amount = unchecked(amount + unchecked(SceneryArmyParameter(state,
                SimulationParameterKeys.ArmyDestruction.ForceExperienceFlagBonusPercent) * basis) / 100);
        return amount;
    }

    private GameState ApplyTalentSceneryReward(GameState state, SourceWorldContext current,
        SceneryReference scenery, int kind, int amount, List<CampaignEffectRequest> effects)
    {
        if (kind == 1)
        {
            var area = TalentSceneryArea(current, scenery);
            if (!SearchNeed(current.ReadAreaNativeValidity(area), "reward_Area_validity")) return state;
            int id = SearchNeed(area.PhysicalSlot, "reward_Area_pointer");
            var owner = state.Areas.GetValueOrDefault(id) ?? throw SearchOutcomeMissing("reward_Area_owner");
            int value = Math.Clamp(unchecked(SearchNeed(owner.Security, "reward_Area54") + amount), 0, 100);
            int force = SearchNeed(current.ReadAreaCurrentForceSlot(area), "reward_Area_Force");
            if (force is >= 46 and <= 50) return state;
            state = state with { Areas = new(state.Areas) { [id] = owner with { Security = value } } };
            var latest = state;
            current.ConditionAreaSecurityReader = physical => latest.Areas.GetValueOrDefault(physical)?.Security is int security &&
                security is >= 0 and <= 255 ? (byte)security : null;
            return state;
        }
        if (kind == 2)
        {
            var city = TalentSceneryCity(current, scenery);
            if (!SearchNeed(current.ReadBaseNativeValidity(city), "reward_City_validity")) return state;
            int rawForce = SearchNeed(current.ReadBaseForceSlot(city), "reward_City_ForceB0");
            var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw SearchOutcomeMissing("reward_City_Force");
            int cap = BasePostLoad.ReadMoraleUpper(force, current);
            int value = Math.Clamp(unchecked(SearchNeed(current.ReadBaseMorale44(city), "reward_City44") + amount), 0, cap);
            if (SearchNeed(force.PhysicalSlot, "reward_City_Force60") is >= 46 and <= 50) return state;
            BasePostLoad.WriteMorale(city, Math.Clamp(value, 0, BasePostLoad.ReadMoraleUpper(force, current)), current);
            int id = SearchNeed(city.PhysicalSlot, "reward_City_identity");
            var owner = state.Cities.GetValueOrDefault(id) ?? throw SearchOutcomeMissing("reward_City_owner");
            byte after = SearchNeed(current.ReadBaseMorale44(city), "reward_City44_after");
            return state with { Cities = new(state.Cities) { [id] = owner with { Morale = after,
                NativeReturnRuntime = (owner.NativeReturnRuntime ?? throw SearchOutcomeMissing("reward_City_runtime")) with { Morale44 = after } } } };
        }
        if (kind != 3) return state;
        var receiver = TalentSceneryForce(current, scenery);
        if (!SearchNeed(current.ReadForceNativeValidity(receiver), "reward_experience_Force_validity") || amount < 0) return state;
        int physical = SearchNeed(receiver.PhysicalSlot, "reward_experience_Force_pointer");
        _force164Inputs.BindCurrent(state, current);
        int before = SearchNeed(current.ReadForce164(receiver), "reward_experience164");
        int oldLevel = SearchNeed(OwnedForce164Inputs.ReadLevel(state, before), "reward_experience_old_level");
        int afterValue = Math.Clamp(unchecked(before + amount), 0, 9999999);
        int newLevel = SearchNeed(OwnedForce164Inputs.ReadLevel(state, afterValue), "reward_experience_new_level");
        var packet = new ArmyDestructionForce164Packet(current, receiver, requested: amount, before: before,
            after: afterValue, oldLevel: oldLevel, newLevel: newLevel);
        ArmyDestructionForce164.ApplyContext(packet);
        state = ArmyDestructionForce164.PublishCurrent(state, packet);
        if (TalentForceIsPlayerCurrent(state, current, receiver))
        {
            if (amount > 0 && oldLevel != 10)
            {
                int shown = newLevel == 10 ? unchecked(SearchNeed(OwnedForce164Inputs.ReadMaximumThreshold(state), "reward_experience_maximum") - before) : amount;
                if (shown > 0) effects.Add(new(null, null) { TalentForceExperiencePresentation = new(
                    TalentForceExperiencePresentationOperation.ExperienceGain, physical, shown) { PreviousLevel = oldLevel } });
            }
            if (oldLevel < newLevel && newLevel == 10) effects.Add(new(null, null) { TalentForceExperiencePresentation = new(
                TalentForceExperiencePresentationOperation.LevelTenReached, physical, 0) { PreviousLevel = oldLevel } });
        }
        return state;
    }

    //22AA10 uses the Area representative's ownership, independently of the
    //Scenery Hex ownership. A mismatch selects the actual City0 receiver.
    private static BaseReference TalentSceneryCity(SourceWorldContext current, SceneryReference scenery)
    {
        var area = TalentSceneryArea(current, scenery);
        ushort representative = SearchNeed(current.ReadAreaHexIndex(area), "reward_Area3A");
        var hex = current.ReadHexTableReference(representative < 48400 ? representative : 0)
            ?? throw SearchOutcomeMissing("reward_Area_Hex");
        var force = TalentSceneryForce(current, scenery);
        byte raw = SearchNeed(current.ReadHexForceSlot(hex), "reward_Area_Hex14") ==
            SearchNeed(force.PhysicalSlot, "reward_Scenery_Force")
                ? SearchNeed(current.ReadAreaCitySlot(area), "reward_Area35") : (byte)0;
        return current.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw SearchOutcomeMissing("reward_City_pointer");
    }

    private static AreaReference TalentSceneryArea(SourceWorldContext current, SceneryReference scenery)
    {
        ushort raw = SearchNeed(current.ReadSceneryHexIndex(scenery), "area_Scenery44");
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw SearchOutcomeMissing("area_Scenery_Hex");
        int ordinal = hex.PhysicalSlot.HasValue ? SearchNeed(current.ReadHexAreaSlot(hex), "area_Hex12") : 0;
        return current.ReadAreaTableReference(ordinal < 501 ? ordinal : 0) ?? throw SearchOutcomeMissing("area_pointer");
    }
    private static ForceReference TalentSceneryForce(SourceWorldContext current, SceneryReference scenery)
    {
        ushort raw = SearchNeed(current.ReadSceneryHexIndex(scenery), "force_Scenery44");
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw SearchOutcomeMissing("force_Scenery_Hex");
        int ordinal = 0;
        if (hex.PhysicalSlot.HasValue && (SearchNeed(current.ReadSceneryFlags47(scenery), "force_Scenery47") & 1) != 0)
        {
            byte value = SearchNeed(current.ReadHexForceSlot(hex), "force_Hex14");
            if (value < 52) ordinal = value;
        }
        return current.ReadForceTableReference(ordinal) ?? throw SearchOutcomeMissing("force_pointer");
    }
    private static int SceneryArmyParameter(GameState state, string key) =>
        ArmyDestructionParameterSeed.ReadCurrent(state, key) ?? throw SearchOutcomeMissing("Scenery_parameter_" + key);
    private static void ShuffleSceneryReward(List<int> list, int salt, TacticSourceSamplerWorld sampler)
    {
        for (int n = (list.Count / 2) * list.Count; n > 0; n--)
        {
            int a = TacticEffectSourceSamplerMath.Sample(list.Count, 1, salt, 0, 0, 0, sampler);
            int b = list.Count - 1 - TacticEffectSourceSamplerMath.Sample(list.Count, 1, unchecked(salt + 1), 0, 0, 0, sampler);
            salt = unchecked(salt + 2);
            (list[a], list[b]) = (list[b], list[a]);
        }
    }
}

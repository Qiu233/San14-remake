using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPlayableGarrisonFoodShortageRules
{
    public PlayableGarrisonFoodShortageSettlement BeginGarrisonFoodShortageSettlement()
    {
        SourceWorldContext? current = null;
        // Capture lazily once per accounting batch. No shortage means no extra
        // world capture; later bills reuse the values written by earlier bills.
        return (state, shortage) =>
        {
            if (current is null)
            {
                current = CaptureOwnedArmyReturnWorld(state).Context;
                _baseCapacityInputs.BindCurrent(state, current);
            }
            return ApplyGarrisonFoodShortageCurrent(state, current, shortage);
        };
    }

    private GameState ApplyGarrisonFoodShortageCurrent(GameState state, SourceWorldContext current,
        PlayableGarrisonFoodShortage shortage)
    {
        var target = BaseLookup.Find(shortage.LocationId, current);
        if (!GarrisonKnown(current.ReadBaseNativeValidity(target), "Base_validity")) return state;
        var working = state;
        int charm = 0;
        ushort governor = GarrisonKnown(current.ReadArmyReturnBaseGovernor2E(target), "Governor2E");
        var person = (governor <= 6000 ? current.ReadPersonLookupReference(governor) :
            current.ReadTransferPersonTableReference(0)) ?? throw GarrisonMissing("Governor_pointer");
        int physical = GarrisonKnown(person.PhysicalSlot, "Governor_physical");
        if (GarrisonKnown(current.ReadPersonNativeValidity(physical), "Governor_validity"))
        {
            int id = GarrisonKnown(current.ReadPersonIdentity(physical), "Governor_identity");
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                SimulationRuleIdentity.ForLocale(Seed.Language));
            charm = GarrisonKnown(abilities.ResolveDimension(working, id, 4,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(working, force, effect)), "Governor_charm");
        }
        int bound = P("random-bound");
        int draw = 0;
        if (bound >= 2)
        {
            var snapshot = working.CampaignAppRandom32 ?? throw GarrisonMissing("App32_state");
            var random = new SharedSourceRandom32(snapshot.State);
            draw = random.Next(bound);
            working = working with { CampaignAppRandom32 = new(random.State) };
        }
        int before = GarrisonKnown(current.ReadBaseResource(target, BaseResourceKind.Troops), "Base3C");
        int proportional = unchecked((P("troop-rate") - charm / Divisor("troop-charm-divisor")) * before) / 100;
        int coefficient = Math.Max(unchecked(P("minimum-troop-coefficient") + draw), proportional);
        int requested = unchecked(coefficient * shortage.ShortagePercent) / 100;
        var debit = AdjustCurrentBaseCombatQuantity(BaseCombatQuantity.ReadyTroops, before,
            ~requested, () => BasePostLoad.ReadTroopCapacity(target, current));
        current.SetBaseResource(target, BaseResourceKind.Troops, debit.After);
        working = PublishCurrentBaseCombatQuantities(working, current, target, true, false, false);
        var reports = new List<PeriodReportEntry>
        {
            new("food_desertion", shortage.ForceId,
                target.Kind == BaseKind.City ? shortage.LocationId : null, Amount: before - debit.After)
            { LocationId = shortage.LocationId, RuleId = "CE-current-garrison-food-shortage" }
        };
        var effects = new List<CampaignEffectRequest>();
        if (requested >= 0)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current)
                ?? throw GarrisonMissing("player_Force");
            if (GarrisonKnown(current.ReadForceNativeValidity(player), "player_validity"))
            {
                int raw = GarrisonKnown(current.ReadBaseForceSlot(target), "Base_Force");
                var owner = current.ReadForceTableReference(raw <= 51 ? raw : 0)
                    ?? throw GarrisonMissing("Base_Force_pointer");
                if (owner == player) Report(owner);
            }
        }
        int moraleCoefficient = Math.Clamp(P("morale-rate") - charm / Divisor("morale-charm-divisor"), 2, 14);
        int moraleAdjustment = ~(unchecked(moraleCoefficient * shortage.ShortagePercent) / 100);
        int forceSlot = GarrisonKnown(current.ReadBaseForceSlot(target), "morale_Force");
        var forcePointer = current.ReadForceTableReference(forceSlot <= 51 ? forceSlot : 0)
            ?? throw GarrisonMissing("morale_Force_pointer");
        int upper = BasePostLoad.ReadMoraleUpper(forcePointer, current);
        byte oldMorale = GarrisonKnown(current.ReadBaseMorale44(target), "Base44");
        int bounded = Math.Clamp(unchecked(oldMorale + moraleAdjustment), 0, upper);
        bool moraleWritten = BasePostLoad.WriteMoraleCurrent(target, bounded, current);
        working = PublishCurrentBaseCombatQuantities(working, current, target, false, false, moraleWritten);
        if (moraleWritten)
            reports.Add(new("food_garrison_morale", shortage.ForceId,
                target.Kind == BaseKind.City ? shortage.LocationId : null,
                Amount: oldMorale - GarrisonKnown(current.ReadBaseMorale44(target), "final_Base44"))
            { LocationId = shortage.LocationId, RuleId = "CE-current-garrison-food-shortage" });
        var pending = working.CampaignEffectRequests ?? throw GarrisonMissing("effect_owner");
        if (effects.Count != 0)
            pending = pending.Append(CampaignEffectSource.ArmyProgressNotifications,
                working.NativeMapSeason?.AdvancedCalendarSteps ?? throw GarrisonMissing("calendar_revision"), effects);
        var pool = current.ReadOwnedCurrentListPool() ?? throw GarrisonMissing("current_pool");
        return working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ProjectOwnedRoles() }) with
        { PeriodReports = [.. working.PeriodReports, .. reports], CampaignEffectRequests = pending };

        int P(string key) => GarrisonFoodShortageParameters.Read(working, key);
        int Divisor(string key) => P(key) is int value && value != 0 ? value : throw GarrisonMissing(key);
        void Report(ForceReference force)
        {
            var pool = current.ReadOwnedCurrentListPool() ?? throw GarrisonMissing("report_pool");
            var selection = new PersonDeathHistorySelection(working, current, pool,
                new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
                    SimulationRuleIdentity.ForLocale(Seed.Language)), _governorInputs,
                (slot, effect) => _baseCapacityInputs.ReadPanelModifier(working, slot, effect));
            var district = current.ReadBaseDistrictReference(target) ?? throw GarrisonMissing("report_District");
            var speaker = selection.Select(district);
            int speakerId = GarrisonKnown(current.ReadPersonIdentity(
                GarrisonKnown(speaker.PhysicalSlot, "speaker_pointer")), "speaker_identity");
            ushort ruler = GarrisonKnown(current.ReadMoraleWaveRuler22EAC0(force), "report_ruler");
            ushort location = GarrisonKnown(current.ReadBaseObjectLink(target), "report_Base4E");
            var primary = FormatGarrisonFoodShortageReport(working, current, 0x5E3D,
                speakerId, ruler, location, value => working = value);
            working = primary.State;
            var supplemental = FormatGarrisonFoodShortageReport(working, current, 0x5E41,
                0, 0, location, value => working = value);
            working = supplemental.State;
            int recipient = GarrisonKnown(current.ReadBaseDistrictSlot(target), "report_virtual78");
            int hex = ReadReportBaseMarker(current, target, GarrisonMissing);
            var season = working.NativeMapSeason ?? throw GarrisonMissing("report_calendar");
            int day = unchecked((working.NativeArmySimulation?.World.SamplerEpoch
                ?? throw GarrisonMissing("report_Day450")).Day450 + (int)season.AdvancedDays);
            working = WriteHistory(working, current, new(5, recipient, hex, day,
                supplemental.Text, primary.Text, speakerId, MarkBit0: 1, MarkBit4: 1), effects);
            // Source-time text/history are real. The later controller event6
            // and speaker playback remain explicit intentions, never an ACK.
            reports.Add(new("food_shortage_report_unresolved", shortage.ForceId, shortage.FoodCityId, speakerId)
            { LocationId = shortage.LocationId,
                MissingRuleKeys = ["food_upkeep.garrison_shortage.application_event6_speaker_playback"] });
        }
    }

    private static T GarrisonKnown<T>(T? value, string field) where T : struct =>
        value ?? throw GarrisonMissing(field);
    private static InvalidDataException GarrisonMissing(string field) => new("food_upkeep.garrison_shortage." + field);
}

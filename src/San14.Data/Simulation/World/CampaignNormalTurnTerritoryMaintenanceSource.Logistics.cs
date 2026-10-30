using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnTerritoryMaintenanceSource
    {
        private ArmyTraitFeedback? logisticsFeedback;
        private NativeMapSeasonDate Date => state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");

        private void UpdateLogistics(IEnumerable<int>? selectedArmies = null)
        {
            var preparation = new ArmyLogisticsPreparation(package.Context(current), current, package._map, package._terrain);
            logisticsFeedback = new(state, current, Date, effects, () => state,
                speechConsumer: package.CreateArmyTraitSpeechConsumer(current, () => state, next => state = next));
            foreach (int physical in selectedArmies ?? ArmyOrder())
            {
                var receiver = new ArmyUnitReference(physical);
                if (!N(current.ReadArmyUnitNativeValidity(receiver), "logistics_Army_validity")) continue;
                var queries = new ArmyMapGroupingCurrentQueries(state, current, package._scenario, package._governorInputs,
                    package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
                if (N(queries.IsAtHome2C96E0(physical), "logistics_at_home") ||
                    N(queries.IsExcluded233560(physical), "logistics_excluded")) continue;
                var owner = current.ReadArmyUnitForceReference(receiver);
                int force = ForceId(owner);
                if (force is >= 46 and <= 51) continue;
                var image = Image(physical);
                var runtime = image.NativeTacticRuntime ?? throw Missing("logistics_Army_runtime");
                bool former = (runtime.Flags26 & 2) != 0;
                bool connected = N(preparation.ResolveLogisticsConnection(state, image), "logistics_mode8_path");
                WriteImage(physical, image with { NativeTacticRuntime = runtime with
                    { Flags26 = (byte)((runtime.Flags26 & 0xFD) | (connected ? 2 : 0)) } });
                if (runtime.Status10 == 2)
                {
                    bool marked = (N(current.ReadArmyUnitFlags150(receiver), "logistics_transport150") & 8) != 0;
                    if (marked)
                    {
                        if (!OwnedArmyDestructorFlags.SetSourceBit(current, physical, 3, false)) throw Missing("logistics_transport_clear150");
                        state = state.WithArmyLifecycle(OwnedArmyDestructorFlags.PublishCurrent(state.ReadArmyLifecycle()!, current));
                    }
                    if (connected && (marked || !former))
                    {
                        image = Image(physical);
                        var march = image.March ?? throw Missing("logistics_transport_route48");
                        WriteImage(physical, image with { March = march with { NextStep = ArmyMarchDestination.None } });
                        //301DD0's actual route owner writes48/1F8 using this
                        //same current. It does not clear transport cargo/traits.
                        state = package.FRoute(state, image.Id, true, current, publishPool: false);
                    }
                }
                if (former)
                {
                    if (!connected) DisconnectLogistics(receiver, physical);
                    continue;
                }
                if (!connected) continue;
                int gain = ArmyParameter(SimulationParameterKeys.Morale.LogisticsGain);
                byte raw = N(current.ReadArmyReturnResourceFields(receiver)?.Morale1A, "logistics_reconnect1A");
                int definitionCap = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(receiver), current);
                int getterCap = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(receiver), current);
                int setterCap = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(receiver), current);
                byte after = ArmyMoraleSource.ReconnectByte276AB0(raw, getterCap, definitionCap, gain, setterCap);
                current.WriteArmyReturnMorale(physical, after);
                state = ArmyReturnImagePublication.Publish(state, current, []);
                if (package.TalentForceIsPlayerCurrent(state, current, current.ReadArmyUnitForceReference(receiver)))
                    LogisticsReport(receiver, 0x5EA6, new(Army0: receiver),
                        N(current.ReadArmyUnitCommanderId(receiver), "logistics_reconnect_Commander12"));
            }
            state = logisticsFeedback.Publish(state);
        }

        private void DisconnectLogistics(ArmyUnitReference receiver, int physical)
        {
            int difficulty = N(BattleRemakeCurrentSettings.ReadInt32(state, 0xE0), "logistics_loss_settingE0");
            int loss = difficulty switch { 0 => 7, 2 => 30, _ => ArmyParameter(SimulationParameterKeys.Morale.LogisticsGain) };
            bool advanced = N(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "logistics_advanced_setting");
            int adjustment = unchecked(-loss);
            if (advanced) adjustment = unchecked(-ArmyParameter(SimulationParameterKeys.Morale.LogisticsGain) *
                N(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 9), "logistics_advanced9")) / 100;
            var applied = ArmyMoraleZeroRoutingCurrent.ApplyImmediate(state, current, receiver, adjustment,
                package.TrapMobility(), ImmediateLogisticsDefeat);
            state = applied.State ?? throw Missing(applied.UnresolvedOwner ?? "logistics_morale2709A0");
            state = ArmyReturnImagePublication.Publish(state, current, []);
            int commander = Person(N(current.ReadArmyUnitCommanderId(receiver), "logistics_loss_Commander12"));
            int immunity = N(TacticRemakeCurrentTraits.CommanderEffectSum(current, commander, 0x60), "logistics_effect96");
            if (immunity == 0)
            {
                //Immutable TC duration tables: index1=20/40/80 and
                //index0=30/60/80. Advanced9 scales the normal40/60 basis.
                int immobilization = difficulty switch { 0 => 20, 2 => 80, _ => 40 };
                int confusion = difficulty switch { 0 => 30, 2 => 80, _ => 60 };
                if (advanced)
                {
                    immobilization = unchecked(N(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 9), "logistics_immobilization_advanced9") * 40) / 100;
                    confusion = unchecked(N(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 9), "logistics_confusion_advanced9") * 60) / 100;
                }
                ApplyLogisticsAbnormal(receiver, physical, 1, immobilization);
                ApplyLogisticsAbnormal(receiver, physical, 0, confusion);
            }
            else
            {
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("logistics_immunity_player");
                var primary = ForcePrimaryDistrictSource.Resolve(current, player);
                if (N(current.ReadDistrictNativeValidity(primary), "logistics_immunity_primary_validity") &&
                    N(primary.PhysicalSlot, "logistics_immunity_primary") == ReadFacilityHistoryDistrict(current, receiver))
                {
                    var trait = FirstPersonality(commander, 0x60);
                    if (trait is not null)
                        LogisticsReport(receiver, 0x3228, new(Army0: receiver, String0: current.ReadPersonalityName(trait)), 5012);
                }
            }
            QueueLogisticsDisconnectSpeech(receiver, physical);
        }

        //276DB0?2772F0 calls1B4AB0(receiver,19,null) after the actual
        //morale/abnormal/trait report branches.1B4720 owns its admission and
        //delay/App writes here, before a later Army or pending70 cleanup.
        private void QueueLogisticsDisconnectSpeech(ArmyUnitReference receiver, int physical)
        {
            if (!N(current.ReadArmyUnitNativeValidity(receiver), "logistics_feedback_Army_validity")) return;
            int commander = Person(N(current.ReadArmyUnitCommanderId(receiver), "logistics_feedback_Commander12"));
            var at = PersonReturnCommands.ResolvePersonPosition(state, current, commander, () => Date);
            if (current.ReadOwnedPersonCommandManager() is { } tasks) state = state with { PersonnelCommands = tasks };
            int hex = at.PhysicalSlot.HasValue ? N(current.ReadHexPhysicalSlot(at), "logistics_feedback_Hex_identity") : 48400;
            //Native packet is null: no Base/trait context operand is supplied.
            state = package.QueueArmyMapSpeechCurrent(state, current, Image(physical).Id,
                PersonId(commander), 19, hex);
        }

        private void ApplyLogisticsAbnormal(ArmyUnitReference receiver, int physical, int index, int duration,
            string? explicitReportSource = null)
        {
            if (!N(current.ReadArmyUnitNativeValidity(receiver), "logistics_abnormal_Army_validity")) return;
            int person = Person(N(current.ReadArmyUnitCommanderId(receiver), "logistics_abnormal_Commander"));
            bool general = N(TacticRemakeCurrentTraits.CommanderImmune(current, person, 21), "logistics_abnormal_immunity21");
            bool? specific = general ? null : N(TacticRemakeCurrentTraits.CommanderImmune(current, person, 22 + index), "logistics_abnormal_specific_immunity");
            TacticAbnormalSourceInput input;
            if (general || specific == true)
                input = new(index, duration, 0, true, general, specific, null, null, null, null, null, null, null, null);
            else
            {
                var traits = TacticRemakeCurrentTraits.ArmyContributions(current, person,
                    current.ReadArmyReturnTraitFields(receiver)?.Traits64) ?? throw Missing("logistics_abnormal_traits");
                input = new(index, duration, 0, true, false, false, traits[9], traits[10 + index], 0,
                    ArmyParameter(SimulationParameterKeys.Tactics.AbnormalGeneralResistanceRate),
                    ArmyParameter(index == 0 ? SimulationParameterKeys.Tactics.AbnormalType0ResistanceRate : SimulationParameterKeys.Tactics.AbnormalType1ResistanceRate),
                    0, null, null);
            }
            var adjustment = TacticEffectSourceAbnormal.ResolveAdjustment(input) ?? throw Missing("logistics_abnormal_adjustment");
            //Ordinary Story passes the source's actual empty string. It still
            //uses this complete counter/trait/feedback writer, while omitting
            //only the source-name reports and their otherwise unused inputs.
            int? district = explicitReportSource is null ? ReadFacilityHistoryDistrict(current, receiver) : null;
            int District() => district ??= ReadFacilityHistoryDistrict(current, receiver);
            string source = explicitReportSource ?? package.ReadHistoryDataText(0, 0x26A);
            if (!adjustment.CounterWritten)
            {
                if (source.Length != 0) state = package.ApplyAutomaticArmyAbnormalReport(state, current, null,
                    Image(physical), District(), index, adjustment.CauseCode, source, Day, effects);
                return;
            }
            bool visible = true;
            if (adjustment.HasTraitAdjustment)
            {
                int slot = District();
                var pointer = current.ReadDistrictTableReference(slot <= 51 ? slot : 0) ?? throw Missing("logistics_abnormal_District");
                visible = BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, pointer, Missing);
            }
            var image = Image(physical);
            var runtime = image.NativeTacticRuntime ?? throw Missing("logistics_abnormal_runtime");
            byte before = index == 0 ? runtime.Abnormal23 : runtime.Abnormal24;
            var result = TacticEffectSourceAbnormal.Resolve(input with { CurrentCounter = before,
                CurrentUpperBound = ArmyParameter(SimulationParameterKeys.Tactics.AbnormalCounterUpperBound) }, runtime.ProvocationOwner)
                ?? throw Missing("logistics_abnormal_result");
            current.SetTacticAbnormalRawByte(new(RegistryKind.Army, physical), 0x23 + index, result.CounterAfter);
            WriteImage(physical, image with { NativeTacticRuntime = index == 0
                ? runtime with { Abnormal23 = result.CounterAfter } : runtime with { Abnormal24 = result.CounterAfter } });
            if (result.IncreasingNotification)
            {
                logisticsFeedback ??= new(state, current, Date, effects, () => state,
                    speechConsumer: package.CreateArmyTraitSpeechConsumer(current, () => state, next => state = next));
                logisticsFeedback.AbnormalIncreased(receiver, 23 + index);
            }
            if (before == 0 && result.UnclampedSum > 0 && source.Length != 0)
                state = package.ApplyAutomaticArmyAbnormalReport(state, current, null, Image(physical), District(),
                    index, visible ? result.CauseCode : 0, source, Day, effects);
        }

        private GameState ImmediateLogisticsDefeat(GameState reached, UnitDefeatRequest request)
        {
            state = reached; PublishPeople();
            if (current.ReadOwnedPersonCommandManager() is null) PersonReturnCommands.BindCurrent(state, current);
            state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw Missing("logistics_defeat_Person_manager") };
            package.BindActorlessDestructionInputs(state, current);
            state = new OwnedArmyDestructionStage(package).ApplyBattleDefeatCurrent(state, current, pool, request,
                package.ReadArmyDestructionSampler(state, current) ?? throw Missing("logistics_defeat_sampler"), Date,
                effects, logisticsFeedback!.Refresh, person => changedPeople.Add(person));
            PublishPeople();
            return state;
        }

        private PersonalityReference? FirstPersonality(int person, int effect)
        {
            if (!N(current.ReadPersonNativeValidity(person), "logistics_trait_Person_validity")) return null;
            for (int slot = 0; slot < 9; slot++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, slot) ?? throw Missing("logistics_trait_pointer");
                if (!N(current.ReadSkillPersonalityValidity(trait), "logistics_trait_validity")) continue;
                if (N(current.ReadPersonalityByte(trait, 0xB6), "logistics_trait_first") == effect ||
                    N(current.ReadPersonalityByte(trait, 0xBA), "logistics_trait_second") == effect) return trait;
            }
            return null;
        }
        private int ArmyParameter(string key) => N(ArmyDestructionParameterSeed.ReadCurrent(state, key), key);
        private void LogisticsReport(ArmyUnitReference receiver, int root, HistoryMessageOperands operands, int person)
        {
            var text = package.FormatNormalTurnReport(state, current, root, operands);
            state = text.State;
            var district = ForcePrimaryDistrictSource.Resolve(current, current.ReadArmyUnitForceReference(receiver));
            state = package.WriteHistory(state, current, new(5, N(district.PhysicalSlot, "logistics_report_District"),
                N(current.ReadArmyUnitRawHex2A(receiver), "logistics_report_Hex"), Day(), text.Text,
                CampaignHistoryWriter.Supplemental(state), person, MarkBit4: 1), effects);
        }
    }
}

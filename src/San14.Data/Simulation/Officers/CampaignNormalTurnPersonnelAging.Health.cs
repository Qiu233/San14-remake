using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class CampaignNormalTurnPersonnelAgingSource
    {
        /// <summary>TC27CB30, the monthly health action. All four temporaries,
        /// health writes and later deaths borrow the enclosing Turn's pool.</summary>
        internal GameState RunHealth()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("health.shared_pool");
            int setting = HealthLifespanSetting();
            var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("health.ThirdDate");
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("health.player_pointer");
            PersonArmyCaptureInputs.BindCurrent(state, current);
            HexListHandle? sick = null, recovered = null, deaths = null, excluded = null;
            try
            {
                sick = HealthTemporary(); recovered = HealthTemporary();
                deaths = HealthTemporary(); excluded = HealthTemporary();
                //The linked native16 iterator consumes only each receiver's
                //first member, including its actual node-allocation outcome.
                new PersonAppearanceSource(package, state, current, pool).FillExcluded(excluded);
                foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots()
                    ?? throw Missing("health.Person_registry"))
                {
                    int person = Need(entry, "health.Person_pointer");
                    if (HealthPeople(excluded).Contains(person)) continue;
                    var life = HealthOwner(person).Lifespan ?? throw Missing("health.Person_lifespan");
                    int terminal = unchecked(life.TerminalYear + (life.Kind == 1 ? 10 : 0)
                        + (setting == 1 ? 20 : 0) + HealthItemExtension(person)
                        + ((HealthFlags(person) & 8) != 0 ? HealthRule(HealthParameter.LongLifeTerminalExtension) : 0));
                    int illness = HealthIllnessProbability(person, terminal, setting);
                    if (HealthElapsed() < 37) illness = 0;
                    int sample = HealthSample(1, unchecked(date.Year + date.Month + date.Day), HealthIdentity(person));
                    if (sample < illness)
                    {
                        int appearance = HealthOwner(person).AppearanceYear ?? throw Missing("health.Person_appearance_year_AA");
                        int severe = Math.Clamp((unchecked((short)date.Year) - appearance) / 10, 0, 100);
                        HealthWrite(person, sample < severe ? 2 : 1);
                        if (HealthIsPlayerForce(player, HealthForce(person))) HealthAppend(sick, person);
                        continue;
                    }
                    if (HealthSpecialForce(person) || HealthBandAtOrdinaryBase(person)) continue;
                    int recovery = HealthRecoveryProbability(person);
                    if (HealthSample(1, HealthIdentity(person), 0) < recovery)
                    {
                        HealthWrite(person, Math.Clamp(HealthByte(person) - 1, 0, 3));
                        if (HealthIsPlayerForce(player, HealthForce(person))) HealthAppend(recovered, person);
                        continue;
                    }
                    int death = HealthSevereDeathProbability(person, terminal, setting);
                    if (HealthSample(10, HealthIdentity(person), Need(current.ReadPersonSecondLocation(person), "health.death_E4")) < death)
                        HealthAppend(deaths, person);
                }
                foreach (int person in HealthPeople(sick)) HealthReport(person, 0x5D7F);
                foreach (int person in HealthPeople(recovered)) HealthReport(person, 0x5D80);
                HealthSortDeaths(deaths);
                var result = package.ConsumeNormalTurnDeathPeopleCurrent(state, current, pool,
                    HealthPeople(deaths).ToArray(), observe);
                state = result.State;
                RemainingDeathPersonPhysicalSlots = result.Remaining;
                Stopped = result.Stop;
                if (result.Remaining is not null || result.Stop) return state;
                Observe("health.completed");
                return state;
            }
            finally
            {
                //The native explicit destruction order is reverse creation.
                if (excluded is not null) current.ClearPersonInjuryTemporary(excluded, true);
                if (deaths is not null) current.ClearPersonInjuryTemporary(deaths, true);
                if (recovered is not null) current.ClearPersonInjuryTemporary(recovered, true);
                if (sick is not null) current.ClearPersonInjuryTemporary(sick, true);
            }
        }

        private int HealthIllnessProbability(int person, int terminal, int setting)
        {
            if (!Need(current.ReadPersonNativeValidity(person), "health.illness_validity")) return 0;
            var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("health.illness_date");
            byte status = Need(current.ReadPersonStatus(person), "health.illness_status");
            if (setting == 2 || status is 0 or 7 or 9 || HealthByte(person) != 0 || (HealthFlags(person) & 0x400) != 0) return 0;
            int basis = unchecked((unchecked((short)date.Year) - terminal + 1) * HealthRule(HealthParameter.IllnessYearRate));
            int trait = Need(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x2F), "health.illness_trait47");
            var force = HealthForce(person);
            int protection = 0;
            if (Need(current.ReadForceNativeValidity(force), "health.illness_force_validity") && (HealthForceFlags(force) & 8) != 0)
                protection = HealthRule(HealthParameter.ForceIllnessProtection);
            return Math.Clamp(unchecked(basis - protection - trait), 0, 100);
        }

        private int HealthRecoveryProbability(int person, bool knownSpecialPerson = false)
        {
            if (!Need(current.ReadPersonNativeValidity(person), "health.recovery_validity")) return 0;
            byte status = Need(current.ReadPersonStatus(person), "health.recovery_status");
            if (status is 0 or 7 or 9 || HealthByte(person) == 0 ||
                HealthBusy(person) ||
                (HealthFlags(person) & 1) != 0) return 0;
            int mode = state.CampaignEventWorld?.ScenarioMode ?? throw Missing("health.recovery_world_mode");
            int basis;
            if (mode == 2) basis = HealthRule(HealthParameter.SpecialModeRecovery);
            else
            {
                int age;
                if (knownSpecialPerson) age = HealthRule(HealthParameter.SpecialPersonRecoveryAge);
                else
                {
                    age = HealthAge(person);
                    if (HealthSpecialForce(person) || HealthBandAtOrdinaryBase(person)) age = HealthRule(HealthParameter.SpecialPersonRecoveryAge);
                }
                //Keep the source's f32 divide, multiply, add then truncate.
                float scaled = ((float)age - 15f) / 85f;
                scaled *= HealthRule(HealthParameter.RecoveryAgeSlope); scaled += HealthRule(HealthParameter.RecoveryAgeOffset);
                basis = Math.Clamp(unchecked(100 - HealthTruncate(scaled)), 0, 100);
            }
            var force = HealthForce(person);
            int difficultyPercent = 100;
            if (Need(current.ReadForceNativeValidity(force), "health.recovery_force_validity") && HealthPlayerControlled(force))
            {
                int difficulty = HealthDifficulty();
                difficultyPercent = difficulty == 0 ? HealthRule(HealthParameter.EasyPlayerRecoveryPercent) : difficulty == 1 ? HealthRule(HealthParameter.NormalPlayerRecoveryPercent) : 100;
            }
            basis = unchecked(difficultyPercent * basis) / 100;
            int policy = 0;
            force = HealthForce(person);
            if (Need(current.ReadForceNativeValidity(force), "health.recovery_policy_force_validity"))
                policy = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, force, 0x29, true).Value;
            policy = unchecked(HealthRule(HealthParameter.PolicyRecoveryScale) * policy);
            int healer = 0;
            if (status is >= 1 and <= 4)
            {
                int location = Need(current.ReadPersonFirstLocation(person), "health.recovery_home");
                var home = BaseLookup.Find(location, current);
                int homeLocation = Need(current.ReadBaseObjectLink(home), "health.recovery_Base4E");
                if (homeLocation is >= 1 and <= 61)
                    foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("health.healer_registry"))
                    {
                        int other = Need(entry, "health.healer_pointer");
                        if (Need(current.ReadPersonFirstLocation(other), "health.healer_home") != homeLocation ||
                            Need(current.ReadPersonStatus(other), "health.healer_status") is not (>= 1 and <= 4)) continue;
                        if (HealthBusy(other)) continue;
                        healer = Need(TacticRemakeCurrentTraits.PersonContribution(current, other, 0x2C), "health.healer_trait44");
                        if (healer != 0) break;
                    }
            }
            int healerContribution = unchecked(HealthRule(HealthParameter.HealerRecoveryScale) * healer * basis) / 100;
            if (healer > 0) healerContribution = Math.Max(1, healerContribution);
            else if (healer < 0) healerContribution = Math.Min(-1, healerContribution);
            else healerContribution = 0;
            return Math.Clamp(unchecked(healerContribution + unchecked(policy * basis) / 100 + basis), 0, 100);
        }

        private int HealthSevereDeathProbability(int person, int terminal, int setting)
        {
            if (!Need(current.ReadPersonNativeValidity(person), "health.severe_validity")) return 0;
            byte status = Need(current.ReadPersonStatus(person), "health.severe_status");
            if (setting == 2 || status is 0 or 7 or 9 || HealthByte(person) != 3 || (HealthFlags(person) & 0x400) != 0) return 0;
            int age = HealthAge(person);
            int rate = age / HealthRule(HealthParameter.SevereAgeDivisor);
            if (terminal < unchecked((short)(state.NativeMapSeason?.ThirdDate.Year ?? throw Missing("health.severe_year"))))
                rate = HealthAge(person) / HealthRule(HealthParameter.ExpiredSevereAgeDivisor);
            return Math.Max(HealthRule(HealthParameter.MinimumSevereDeathProbability), rate);
        }

        private void HealthReport(int person, int root)
        {
            int id = HealthIdentity(person);
            var formatted = package.FormatHistoryReport(state, current, root, new(Person0: id));
            state = formatted.State;
            //22D6C0's virtual task position remains live and may normalize its
            //owned task; keep that actual owner for subsequent people.
            int district = Need(current.ReadPersonDistrictSlot(person), "health.report_E0");
            var hex = PersonReturnCommands.ResolvePersonPosition(state, current, person,
                () => state.NativeMapSeason?.ThirdDate ?? throw Missing("health.report_date"));
            if (current.ReadOwnedPersonCommandManager() is { } tasks) state = state with { PersonnelCommands = tasks };
            int position = Need(hex.PhysicalSlot, "health.report_null_Hex_22DEF0");
            int day = (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("health.report_World450")).Day450;
            var effects = new List<CampaignEffectRequest>();
            state = package.WriteHistory(state, current, new(0, district, position, day, formatted.Text,
                CampaignHistoryWriter.Supplemental(state), id, MarkBit4: 1), effects);
            if (effects.Count != 0)
                state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("health.effect_queue"))
                    .Append(CampaignEffectSource.MonthlyMaintenance,
                        state.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("health.report_revision"), effects) };
        }

        private int HealthItemExtension(int person) =>
            Need(current.ReadConditionPersonLifespanExtension((ushort)HealthIdentity(person)), "health.item_extension");
        private int HealthLifespanSetting()
        {
            int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "health.settings_selector");
            return Need(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x1C4 : 0x2C), "health.lifespan_setting");
        }
        private int HealthDifficulty()
        {
            int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "health.difficulty_selector");
            return Need(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28), "health.difficulty");
        }
        private int HealthElapsed() => unchecked((state.CampaignEventWorld ?? throw Missing("health.elapsed_owner")).ElapsedPeriodOffset
            + (int)(state.NativeMapSeason?.AdvancedPeriods ?? throw Missing("health.elapsed_calendar")));
        private int HealthSample(int divisor, int first, int second) => TacticEffectSourceSamplerMath.Sample(100, divisor, first, second, 0, 0,
            package.ReadArmyDestructionSampler(state, current) ?? throw Missing("health.sampler_World450_45C"));
        private int HealthAge(int person)
        {
            int mode = state.CampaignEventWorld?.ScenarioMode ?? throw Missing("health.age_mode");
            if (mode is 2 or 3) throw Missing("health.special_mode_age");
            int birth = HealthOwner(person).BirthYear ?? throw Missing("health.birth_year");
            return Math.Max(0, unchecked((short)(state.NativeMapSeason?.ThirdDate.Year ?? throw Missing("health.age_year"))) - birth + 1);
        }
        private bool HealthSpecialForce(int person)
        {
            int location = Need(current.ReadPersonFirstLocation(person), "health.special_home");
            return location is >= 1 and <= 61 && Need(current.ReadForcePhysicalSlot(HealthForce(person)), "health.special_force") is >= 46 and <= 50;
        }
        private bool HealthBandAtOrdinaryBase(int person)
        {
            if (!BattleRemakeCurrentRegionFactor.IsNativeBandPerson((ushort)HealthIdentity(person))) return false;
            int location = Need(current.ReadPersonFirstLocation(person), "health.band_home");
            return location is >= 1 and <= 61 && Need(current.ReadForcePhysicalSlot(HealthForce(person)), "health.band_force") is not (>= 46 and <= 50);
        }
        private ForceReference HealthForce(int person) => current.ReadPersonForce(person) ?? throw Missing("health.Person_force");
        private bool HealthIsPlayerForce(ForceReference player, ForceReference force) =>
            Need(current.ReadForceNativeValidity(player), "health.player_validity") && player.PhysicalSlot == force.PhysicalSlot;
        private bool HealthPlayerControlled(ForceReference force)
        {
            //2338E0 compares ordinals without the report caller's separate
            //player native-validity gate.
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("health.current_player");
            int id = Need(current.ReadForcePhysicalSlot(player), "health.player_ordinal");
            return id != 0 && id == Need(current.ReadForcePhysicalSlot(force), "health.force_ordinal");
        }
        private bool HealthBusy(int person)
        {
            byte status = Need(current.ReadPersonStatus(person), "health.busy_status");
            if (status is >= 1 and <= 4 && PersonPositionRepair.DecodeFirstLocation(
                    Need(current.ReadPersonFirstLocation(person), "health.busy_home")) !=
                PersonPositionRepair.DecodeSecondLocation(Need(current.ReadPersonSecondLocation(person), "health.busy_position"))) return true;
            var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("health.busy_linked");
            if (!Need(current.ReadLinkedEntityNativeValidity(linked), "health.busy_linked_validity")) return false;
            return Need(new PersonReturnGovernorQueries(state, package._governorInputs, current)
                .ReadCurrentAssociatedVirtual98(linked), "health.busy_virtual98") == 16;
        }
        private byte HealthForceFlags(ForceReference force)
        {
            int physical = Need(current.ReadForcePhysicalSlot(force), "health.force_flags_pointer");
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.GetValueOrDefault(physical) is { Length: > 0x12 } image) return Need(image[0x12], "health.reset_force_flags12");
            return state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.Flags12 ?? throw Missing("health.force_flags12");
        }
        private int HealthIdentity(int person) => Need(current.ReadPersonIdentity(person), "health.Person_identity");
        private ushort HealthFlags(int person) => Need(current.ReadOwnedReturnPersonFlags(person), "health.Person_flags15E");
        private byte HealthByte(int person) => Need(OfficerHealthSourceAdapter.ReadByte(current, person), "health.Person161");
        private OfficerState HealthOwner(int person)
        {
            int id = HealthIdentity(person);
            return current.ResolveOfficerSource(id) == person && state.Officers.TryGetValue(id, out var owner)
                ? owner : throw Missing("health.Person_owner");
        }
        private void HealthWrite(int person, int value)
        {
            var owner = HealthOwner(person); var health = new OfficerHealth(value);
            current.SetOfficerHealth(owner.Id, health);
            state = state with { Officers = new(state.Officers) { [owner.Id] = owner with { Health = health } } };
        }
        private HexListHandle HealthTemporary() => current.CreatePersonInjuryTemporary();
        private void HealthAppend(HexListHandle list, int person)
        {
            if (Need(current.ReadRegistryLockEnabled(), "health.append_lock")) current.EnterRegistryLock();
            try
            {
                var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("health.temporary_node");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, person));
            }
            finally { if (Need(current.ReadRegistryLockEnabled(), "health.append_unlock")) current.ExitRegistryLock(); }
        }
        private IEnumerable<int> HealthPeople(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("health.list_head");
            while (node.Identity.HasValue)
            {
                var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("health.list_payload");
                if (payload.Kind != RegistryKind.Person) throw Missing("health.list_payload_kind");
                yield return Need(payload.PhysicalSlot, "health.list_person");
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("health.list_next");
            }
        }
        private void HealthSortDeaths(HexListHandle list)
        {
            var nodes = new List<HexListNodeReference>();
            var node = current.ReadHexListHead(list) ?? throw Missing("health.sort_head");
            while (node.Identity.HasValue) { nodes.Add(node); node = current.ReadHexListNodeNext(node) ?? throw Missing("health.sort_next"); }
            static int Rank(byte status) => status switch { 8 => 0, 5 => 1, 6 => 2, 4 => 3, 3 => 4, 2 => 5, 1 => 6, _ => 7 };
            int Person(HexListNodeReference value) => Need((current.ReadRegistryNodePayload(new(value.Identity))
                ?? throw Missing("health.sort_payload")).PhysicalSlot, "health.sort_person");
            for (int index = 1; index < nodes.Count; index++)
            {
                var moving = nodes[index]; int p = Person(moving), at = index;
                while (at > 0)
                {
                    int q = Person(nodes[at - 1]);
                    int a = Rank(Need(current.ReadPersonStatus(p), "health.sort_status"));
                    int b = Rank(Need(current.ReadPersonStatus(q), "health.sort_previous_status"));
                    if (!(a < b || a == b && HealthIdentity(p) < HealthIdentity(q))) break;
                    at--;
                }
                if (at == index) continue;
                nodes.RemoveAt(index); nodes.Insert(at, moving); current.ReorderCurrentList(list, nodes);
            }
        }
        private enum HealthParameter
        {
            IllnessYearRate, ForceIllnessProtection, LongLifeTerminalExtension,
            SpecialModeRecovery, RecoveryAgeSlope, SpecialPersonRecoveryAge,
            RecoveryAgeOffset, SevereAgeDivisor, ExpiredSevereAgeDivisor,
            MinimumSevereDeathProbability, PolicyRecoveryScale, HealerRecoveryScale,
            EasyPlayerRecoveryPercent, NormalPlayerRecoveryPercent
        }
        private int HealthRule(HealthParameter key)
        {
            //TC immutable .data, extracted offline (health-static65.json).
            //No executable or cached execution input is read by the game.
            if (package.Seed.Language != "TC") throw Missing("health.SC_rule_constants");
            return key switch
            {
                HealthParameter.IllnessYearRate => 10, //TC1A4A438
                HealthParameter.ForceIllnessProtection => 10, //TC1A49770
                HealthParameter.LongLifeTerminalExtension => 20, //TC1A4A478
                HealthParameter.SpecialModeRecovery => 50, //TC1A4A458
                HealthParameter.RecoveryAgeSlope => 60, //TC1A4A460
                HealthParameter.SpecialPersonRecoveryAge => 7, //TC1A4A468
                HealthParameter.RecoveryAgeOffset => 25, //TC1A4A470
                HealthParameter.SevereAgeDivisor => 10, //TC1A4A488
                HealthParameter.ExpiredSevereAgeDivisor => 3, //TC1A4A480
                HealthParameter.MinimumSevereDeathProbability => 1, //TC1A4A48C
                HealthParameter.PolicyRecoveryScale => 1, //TC1A4A490
                HealthParameter.HealerRecoveryScale => 1, //TC1A4A494
                HealthParameter.EasyPlayerRecoveryPercent => 150, //TC1A4A4A4
                HealthParameter.NormalPlayerRecoveryPercent => 125, //TC1A4A4AC
                _ => throw new ArgumentOutOfRangeException(nameof(key))
            };
        }
        private static int HealthTruncate(float value) => !float.IsFinite(value) || value >= 2147483648f || value < -2147483648f
            ? int.MinValue : (int)MathF.Truncate(value);
    }
}

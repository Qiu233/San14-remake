using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    // The ordinary handlers share the actual five positions, but their last
    // two operands differ: commander/force, city, or stored success/score.
    internal bool IsArmyValid(ProposalEntry entry, bool completed)
    {
        var army = ProposalArmy(entry.Arguments.Parameter2);
        if (!K(Current.ReadArmyUnitNativeValidity(army), "army_validity")) return false;
        return entry.Kind switch
        {
            ProposalKind.MoraleUp => ProposalArmyMorale(army) < ProposalArmyMoraleUpper(army),
            ProposalKind.MoraleDown => ProposalArmyMorale(army) > 0 && ProposalEnemy(entry.ProposerPersonId, army),
            ProposalKind.RecoverWoundedSoldiers => K(ProposalArmyResources(army).Wounded18, "army_wounded") != 0,
            ProposalKind.StunEnemy or ProposalKind.ConfuseEnemy or ProposalKind.BurnEnemy => ProposalEnemy(entry.ProposerPersonId, army),
            ProposalKind.BefriendAlly or ProposalKind.ExtinguishFriendlyFire => true,
            ProposalKind.FillTroops => ProposalArmyVacancy(army) > 0 &&
                K(Current.ReadBaseResource(ProposalArmyCity(entry.Arguments.Parameter3), BaseResourceKind.Troops), "fill_city_troops") >= Rule("fill-troops-amount", 3000),
            _ => false
        };
    }

    internal void ApplyArmy(ProposalEntry entry)
    {
        var army = ProposalArmy(entry.Arguments.Parameter2);
        int physical = K(army.PhysicalSlot, "army_physical");
        int person = entry.ProposerPersonId;
        int leadership = 0, intelligence = 0;
        int amount = 0;
        switch (entry.Kind)
        {
            case ProposalKind.MoraleUp:
                leadership = PersonAbility(person, 0);
                amount = leadership / Rule("morale-up-leadership-divisor", 5) + Rule("morale-up-base", 10);
                int beforeUp = ProposalArmyMorale(army);
                AdjustArmyMorale(physical, amount);
                ProposalArmyReport(entry, army, ProposalArmyMorale(army) - beforeUp);
                Reward(person, (int)entry.Kind, 0, leadership / Rule("morale-up-merit-divisor", 20) + Rule("morale-up-merit-base", 10), true);
                break;
            case ProposalKind.MoraleDown:
                intelligence = PersonAbility(person, 2);
                amount = intelligence / Rule("morale-down-intelligence-divisor", 10) + Rule("morale-down-base", 10);
                int beforeDown = ProposalArmyMorale(army);
                AdjustArmyMorale(physical, -amount);
                ProposalArmyReport(entry, army, beforeDown - ProposalArmyMorale(army));
                Reward(person, (int)ProposalKind.MoraleDown, 2, intelligence / Rule("morale-down-merit-divisor", 20) + Rule("morale-down-merit-base", 10), true);
                break;
            case ProposalKind.RecoverWoundedSoldiers:
            {
                int charm = PersonAbility(person, 4);
                int recovery = unchecked(Rule("recovery-base", 1000) + charm * Rule("recovery-charm-scale", 10) + Next(charm * Rule("recovery-random-scale", 10)));
                var fields = ProposalArmyResources(army);
                int wounded = K(fields.Wounded18, "recovery_wounded");
                amount = Math.Min(wounded, recovery);
                int readyAfter = Math.Clamp(unchecked(K(fields.Ready16, "recovery_troops") + amount), 0, 65535);
                int woundedAfter = Math.Clamp(unchecked(wounded - amount), 0, 20000);
                Current.BindArmyReturnResourceFields(physical, fields with { Ready16 = (ushort)readyAfter, Wounded18 = (ushort)woundedAfter });
                ProposalWriteArmyImage(physical, ProposalArmyImage(physical) with { Troops = readyAfter, WoundedTroops = woundedAfter });
                ProposalArmyReport(entry, army, amount);
                Reward(person, (int)entry.Kind, 4, amount / Rule("recovery-merit-divisor", 200) + Rule("recovery-merit-base", 20), true);
                break;
            }
            case ProposalKind.BefriendAlly:
                if (entry.Arguments.Parameter3 != 0) ProposalClearArmyAbnormal(physical);
                ProposalArmyReport(entry, army, 0);
                leadership = PersonAbility(person, 0);
                Reward(person, (int)entry.Kind, 0, leadership / Rule("befriend-merit-divisor", 20) + Rule("befriend-merit-base", 10), true);
                break;
            case ProposalKind.StunEnemy:
            case ProposalKind.ConfuseEnemy:
                intelligence = PersonAbility(person, 2);
                if (entry.Arguments.Parameter3 != 0)
                    ProposalApplyArmyAbnormal(physical, entry.Kind == ProposalKind.StunEnemy ? 1 : 0,
                        unchecked((intelligence / Rule(entry.Kind == ProposalKind.StunEnemy ? "stun-duration-divisor" : "confuse-duration-divisor", 120)) * 32 +
                            Rule(entry.Kind == ProposalKind.StunEnemy ? "stun-duration-base" : "confuse-duration-base", 8)));
                ProposalArmyReport(entry, army, 0);
                Reward(person, (int)ProposalKind.MoraleDown, 2, intelligence / Rule("enemy-abnormal-merit-divisor", 20) + Rule("enemy-abnormal-merit-base", 10), true);
                break;
            case ProposalKind.BurnEnemy:
                intelligence = PersonAbility(person, 2);
                if (entry.Arguments.Parameter3 != 0)
                    ProposalWriteFire(ProposalArmyHex(army), unchecked((intelligence / Rule("burn-duration-divisor", 120)) * 32 + Rule("burn-duration-base", 8)));
                ProposalArmyReport(entry, army, 0);
                Reward(person, (int)ProposalKind.MoraleDown, 2, intelligence / Rule("burn-merit-divisor", 20) + Rule("burn-merit-base", 10), true);
                break;
            case ProposalKind.ExtinguishFriendlyFire:
                if (entry.Arguments.Parameter3 != 0) ProposalWriteFire(ProposalArmyHex(army), 0);
                ProposalArmyReport(entry, army, 0);
                leadership = PersonAbility(person, 0);
                Reward(person, (int)entry.Kind, 0, leadership / Rule("extinguish-merit-divisor", 20) + Rule("extinguish-merit-base", 10), true);
                break;
            case ProposalKind.FillTroops:
            {
                var city = ProposalArmyCity(entry.Arguments.Parameter3);
                var fields = ProposalArmyResources(army);
                int ready = K(fields.Ready16, "fill_troops");
                amount = Math.Min(ProposalArmyVacancy(army), Rule("fill-troops-amount", 3000));
                int cityMorale = K(Current.ReadBaseMorale44(city), "fill_city_morale");
                int morale = ProposalArmyMorale(army);
                int readyAfter = Math.Clamp(unchecked(ready + amount), 0, 65535);
                int cityReady = K(Current.ReadBaseResource(city, BaseResourceKind.Troops), "fill_city_ready");
                int cityAfter = Math.Clamp(unchecked(cityReady - amount), 0, BasePostLoad.ReadTroopCapacity(city, Current));
                // The source setter takes a second fresh capacity read.
                cityAfter = Math.Clamp(cityAfter, 0, BasePostLoad.ReadTroopCapacity(city, Current));
                Current.SetBaseResource(city, BaseResourceKind.Troops, cityAfter);
                int cityId = K(city.PhysicalSlot, "fill_city_identity");
                var cityOwner = State.Cities.GetValueOrDefault(cityId) ?? throw Missing("fill_city_owner");
                State = State with { Cities = new(State.Cities) { [cityId] = cityOwner with { Troops = cityAfter } } };
                byte afterMorale = (byte)Math.Clamp(unchecked(amount * cityMorale + morale * ready) / unchecked(amount + ready), 0, ProposalArmyMoraleUpper(army));
                Current.BindArmyReturnResourceFields(physical, fields with { Ready16 = (ushort)readyAfter, Morale1A = afterMorale });
                ProposalWriteArmyImage(physical, ProposalArmyImage(physical) with { Troops = readyAfter,
                    MoraleState = ProposalArmyImage(physical).MoraleState with { CurrentValue = afterMorale } });
                ProposalArmyReport(entry, army, amount);
                leadership = PersonAbility(person, 0);
                Reward(person, (int)entry.Kind, 0, leadership / Rule("fill-merit-divisor", 20) + Rule("fill-merit-base", 10), true);
                break;
            }
            default: throw Missing("army_handler_kind");
        }
    }

    private void ProposalArmyReport(ProposalEntry entry, ArmyUnitReference army, int amount)
    {
        int hex = ProposalArmyHex(army);
        var hexPointer = Current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("report_army_hex");
        int area = K(Current.ReadHexAreaSlot(hexPointer), "report_army_area");
        int ruler = K(Current.ReadMoraleWaveRuler22EAC0(PlayerForce), "report_player_ruler");
        bool success = entry.Arguments.Parameter3 != 0;
        int speech, history;
        HistoryMessageOperands main = new(Person0: entry.ProposerPersonId, Person1: ruler, Army0: army);
        HistoryMessageOperands log = new(Person0: entry.ProposerPersonId, Army0: army);
        switch (entry.Kind)
        {
            case ProposalKind.MoraleUp:
                speech = 0x4F30; history = 0x4F38;
                main = main with { Integers = [amount] };
                log = log with { Integers = [ProposalArmyMorale(army)] };
                break;
            case ProposalKind.MoraleDown:
                speech = 0x4F39; history = 0x4F41;
                main = main with { Person2 = entry.Arguments.Parameter3, Force0 = ProposalArmyForce(army), Area0 = area, Integers = [amount] };
                log = new(Army0: army, Force0: ProposalArmyForce(army), Integers: [ProposalArmyMorale(army)]);
                break;
            case ProposalKind.RecoverWoundedSoldiers:
                speech = 0x4F42; history = 0x4F4A;
                main = main with { Integers = [amount] };
                log = log with { Integers = [K(ProposalArmyResources(army).Ready16, "report_recovery_ready")] };
                break;
            case ProposalKind.BefriendAlly:
                speech = success ? 0x4FB0 : 0x4FB8; history = success ? 0x4FB9 : 0x4FBA;
                main = main with { Area0 = area }; log = log with { Area0 = area };
                break;
            case ProposalKind.StunEnemy:
            case ProposalKind.ConfuseEnemy:
            case ProposalKind.BurnEnemy:
                speech = entry.Kind switch { ProposalKind.StunEnemy => success ? 0x4FBB : 0x4FC3,
                    ProposalKind.ConfuseEnemy => success ? 0x4FC6 : 0x4FCE, _ => success ? 0x4FD1 : 0x4FD9 };
                history = entry.Kind switch { ProposalKind.StunEnemy => success ? 0x4FC4 : 0x4FC5,
                    ProposalKind.ConfuseEnemy => success ? 0x4FCF : 0x4FD0, _ => success ? 0x4FDA : 0x4FDB };
                main = main with { Force0 = ProposalArmyForce(army), Area0 = area };
                log = log with { Force0 = ProposalArmyForce(army), Area0 = area };
                break;
            case ProposalKind.ExtinguishFriendlyFire:
                speech = success ? 0x4FDC : 0x4FE4; history = success ? 0x4FE5 : 0x4FE6;
                main = main with { Area0 = area }; log = log with { Area0 = area };
                break;
            case ProposalKind.FillTroops:
                speech = 0x4FE7; history = 0x4FEF;
                main = main with { Area0 = area, Base0 = K(Current.ReadBaseObjectLink(ProposalArmyCity(entry.Arguments.Parameter3)), "report_fill_city"), Integers = [amount] };
                log = new(Army0: army, Area0: area, Integers: [amount]);
                break;
            default: throw Missing("report_army_kind");
        }
        Report(entry, speech, main, history, log, hex);
    }

    private ArmyUnitReference ProposalArmy(int physical) => Current.ReadArmyUnitTableReference(physical < 501 ? physical : 0) ?? throw Missing("army_pointer");
    private BaseReference ProposalArmyCity(int physical) => Current.ReadCityTableReference(physical < 52 ? physical : 0) ?? throw Missing("army_city_pointer");
    private ArmyReturnResourceFields ProposalArmyResources(ArmyUnitReference army) => Current.ReadArmyReturnResourceFields(army) ?? throw Missing("army_resources");
    private int ProposalArmyMoraleUpper(ArmyUnitReference army) => BasePostLoad.ReadMoraleUpper(Current.ReadArmyUnitForceReference(army), Current);
    private int ProposalArmyMorale(ArmyUnitReference army) => Math.Min(K(ProposalArmyResources(army).Morale1A, "army_morale"), ProposalArmyMoraleUpper(army));
    private int ProposalArmyHex(ArmyUnitReference army) => K(Current.ReadArmyUnitRawHex2A(army), "army_hex");
    private int ProposalArmyCommander(ArmyUnitReference army) => K(Current.ReadArmyUnitCommanderId(army), "army_commander");
    private int ProposalArmyVacancy(ArmyUnitReference army) => unchecked(PersonTroopCapacity(ProposalArmyCommander(army)) - K(ProposalArmyResources(army).Wounded18, "army_wounded") - K(ProposalArmyResources(army).Ready16, "army_ready"));
    private int ProposalArmyForce(ArmyUnitReference army) => K(Current.ReadForcePhysicalSlot(Current.ReadArmyUnitForceReference(army) ?? throw Missing("army_force")), "army_force_ordinal");
    private bool ProposalEnemy(int person, ArmyUnitReference army)
    {
        var own = Current.ReadPersonForce(PersonPhysical(person)) ?? throw Missing("proposer_force");
        var other = Current.ReadArmyUnitForceReference(army) ?? throw Missing("army_force");
        if (!K(Current.ReadForceNativeValidity(own), "own_force_validity") || !K(Current.ReadForceNativeValidity(other), "enemy_force_validity")) return true;
        return !K(ArmyMarchDevelopmentSource.ReadFriendlyRelation(State, own, other), "army_treaty");
    }
    private UnitState ProposalArmyImage(int physical)
    {
        var receiver = State.ReadArmyLifecycle()?.ReceiverSlots().ElementAtOrDefault(physical) ?? throw Missing("army_receiver");
        return receiver.ActiveUnitId is int id ? State.Units.GetValueOrDefault(id) ?? throw Missing("army_active_image") : receiver.PendingImage ?? throw Missing("army_retained_image");
    }
    private void ProposalWriteArmyImage(int physical, UnitState image)
    {
        if (State.Units.ContainsKey(image.Id)) State = State with { Units = new(State.Units) { [image.Id] = image } };
        else
        {
            var arena = State.ReadArmyLifecycle() ?? throw Missing("army_lifecycle");
            var receiver = arena.ReceiverSlots()[physical].Receiver;
            if (receiver.PendingImage?.Id != image.Id) throw Missing("army_retained_identity");
            State = State.WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(receiver with { PendingImage = image }) });
        }
    }
    private void ProposalWriteFire(int rawHex, int value)
    {
        var hex = Current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw Missing("fire_hex");
        if (hex.PhysicalSlot is not int physical) return;
        Current.SetCaptureHexWord16(physical, (ushort)Math.Clamp(value, 0, 720));
        State = BaseCaptureCurrentInputs.PublishCurrent(State, Current);
    }
    private void ProposalClearArmyAbnormal(int physical)
    {
        var image = ProposalArmyImage(physical);
        var runtime = image.NativeTacticRuntime ?? throw Missing("clear_runtime");
        byte Clear(byte value, int index) => value == 0 ? value : unchecked((byte)Math.Min(0,
            K(ArmyDestructionParameterSeed.ReadCurrent(State, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "clear_upper")));
        byte a = Clear(runtime.Abnormal23, 0), b = Clear(runtime.Abnormal24, 1), c = Clear(runtime.Abnormal25, 2);
        Current.SetTacticAbnormalRawByte(new(RegistryKind.Army, physical), 0x23, a);
        Current.SetTacticAbnormalRawByte(new(RegistryKind.Army, physical), 0x24, b);
        Current.SetTacticAbnormalRawByte(new(RegistryKind.Army, physical), 0x25, c);
        if (runtime.Abnormal25 != 0)
        {
            Current.WriteTacticClearByte(new(RegistryKind.Army, physical), 0x3C, 255);
            Current.WriteTacticClearUInt16(new(RegistryKind.Army, physical), 0x3E, 0);
        }
        image = image with { NativeTacticRuntime = runtime with { Abnormal23 = a, Abnormal24 = b, Abnormal25 = c,
            ProvocationOwner = runtime.Abnormal25 != 0 ? null : runtime.ProvocationOwner },
            OverrideTarget = runtime.Abnormal25 != 0 ? ArmyOverrideTargetState.Cleared : image.OverrideTarget };
        ProposalWriteArmyImage(physical, image);
        if (runtime.Abnormal25 != 0)
        {
            var arena = State.ReadArmyLifecycle() ?? throw Missing("clear_override_lifecycle");
            var receiver = arena.ReceiverSlots()[physical].Receiver;
            State = State.WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(receiver with { OverrideTarget = ArmyOverrideTargetState.Cleared }) });
            State = ArmyOverrideTargetSourceAdapter.ReconcileOwner(State, image);
        }
    }
    private void ProposalApplyArmyAbnormal(int physical, int index, int duration)
    {
        var army = ProposalArmy(physical);
        int person = PersonPhysical(ProposalArmyCommander(army));
        bool general = K(TacticRemakeCurrentTraits.CommanderImmune(Current, person, 21), "abnormal_general_immunity");
        bool? specific = general ? null : K(TacticRemakeCurrentTraits.CommanderImmune(Current, person, 22 + index), "abnormal_specific_immunity");
        TacticAbnormalSourceInput input;
        if (general || specific == true) input = new(index, duration, 0, true, general, specific, null, null, null, null, null, null, null, null);
        else
        {
            var traits = TacticRemakeCurrentTraits.ArmyContributions(Current, person, Current.ReadArmyReturnTraitFields(army)?.Traits64) ?? throw Missing("abnormal_traits");
            int P(string key) => K(ArmyDestructionParameterSeed.ReadCurrent(State, key), key);
            input = new(index, duration, 0, true, false, false, traits[9], traits[10 + index], 0,
                P(SimulationParameterKeys.Tactics.AbnormalGeneralResistanceRate),
                P(index == 0 ? SimulationParameterKeys.Tactics.AbnormalType0ResistanceRate : SimulationParameterKeys.Tactics.AbnormalType1ResistanceRate), 0, null, null);
        }
        var adjustment = TacticEffectSourceAbnormal.ResolveAdjustment(input) ?? throw Missing("abnormal_adjustment");
        if (!adjustment.CounterWritten) return;
        var image = ProposalArmyImage(physical);
        var runtime = image.NativeTacticRuntime ?? throw Missing("abnormal_runtime");
        var result = TacticEffectSourceAbnormal.Resolve(input with { CurrentCounter = index == 0 ? runtime.Abnormal23 : runtime.Abnormal24,
            CurrentUpperBound = K(ArmyDestructionParameterSeed.ReadCurrent(State, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound), "abnormal_upper") }, runtime.ProvocationOwner) ?? throw Missing("abnormal_result");
        Current.SetTacticAbnormalRawByte(new(RegistryKind.Army, physical), 0x23 + index, result.CounterAfter);
        ProposalWriteArmyImage(physical, image with { NativeTacticRuntime = index == 0 ? runtime with { Abnormal23 = result.CounterAfter } : runtime with { Abnormal24 = result.CounterAfter } });
    }
}

using San14.Core;

namespace San14.Data;

internal sealed record BaseTargetApplicationResult(GameState State, BaseReference Target, bool NativeReturn);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>178EC0's Base target for a known-null actor. This operates
    /// in the caller's current world; no capture, clone, index import or final
    /// world publication is performed here. Unknown belongs to that caller's
    /// complete transaction rollback.</summary>
    internal BaseTargetApplicationResult ApplyFireDamageBaseTarget(GameState state, SourceWorldContext current,
        BaseKind targetKind, int nativeTargetIdentity, int requestedLoss,
        Func<TacticSourceSamplerWorld> currentSampler,
        Func<GameState, BaseReference, GameState>? applyFatal = null, bool sourcePresent = false)
    {
        int lookup = targetKind switch
        {
            BaseKind.City => nativeTargetIdentity < 52 ? nativeTargetIdentity : 0,
            BaseKind.Gate => nativeTargetIdentity is >= 1 and <= 10 ? nativeTargetIdentity + 51 : 0,
            _ => throw Missing("fire_Base_target_kind")
        };
        var target = BaseLookup.Find(lookup, current);
        if (!Known(current.ReadBaseNativeValidity(target), "fire_Base_validity")) return new(state, target, false);
        int oldTroops = Known(current.ReadBaseResource(target, BaseResourceKind.Troops), "fire_Base3C_cap");
        int applied = Math.Min(requestedLoss, oldTroops);
        bool troopsWritten = false, woundedWritten = false, moraleWritten = false;
        if (oldTroops > 0)
        {
            if (current.ArmyReturnTransferMode is not { Present: false }) throw Missing("current.challenge_570850");
            //169C00 obtains a fresh3C independently of the request cap.
            int before = Known(current.ReadBaseResource(target, BaseResourceKind.Troops), "fire_Base3C_before");
            var debit = AdjustCurrentBaseCombatQuantity(BaseCombatQuantity.ReadyTroops, before,
                unchecked(-applied), () => BasePostLoad.ReadTroopCapacity(target, current));
            current.SetBaseResource(target, BaseResourceKind.Troops, debit.After);
            troopsWritten = true;
        }

        //17967D reads CC once and saves it through both channel gates.
        //Both E58 and each reached advanced-enabled/index7 query are fresh.
        int cc = Known(BattleRemakeCurrentSettings.ReadInt32(state, 0xCC), "fire_settings_CC");
        if (WoundedGate())
        {
            //22D640 resolves the linked Object before governor/abilities.
            var hex = BaseTargetHex();
            SurfaceTypeReference terrain = new(null);
            if (hex.PhysicalSlot.HasValue)
            {
                byte surface = Known(current.ReadHexSurfaceCode(hex), "fire_Base_Hex10");
                terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
                    ?? throw Missing("fire_Base_Terrain_pointer");
            }
            byte rawMorale = Known(current.ReadBaseMorale44(target), "fire_Base_raw44_ratio");
            var governor = Governor();
            bool valid = PersonValid(governor);
            int charm = valid ? Ability(governor, 4) : 0;
            bool terrainValid = terrain.PhysicalSlot.HasValue;
            byte category = terrainValid ? Known(current.ReadSurfaceTypeWoundedCategory42(terrain), "fire_Base_Terrain42") : (byte)0;
            int selector = Known(BattleRemakeCurrentSettings.ReadSelectorE58(state), "fire_ratio_E58");
            int difficulty = 0; bool forceValid = false, player = false;
            if (selector != 0)
            {
                difficulty = Known(BattleRemakeCurrentSettings.ReadInt32(state, 0x28), "fire_ratio_difficulty28");
                var force = current.ReadPersonForce(Physical(governor)) ?? throw Missing("fire_ratio_Person_Force");
                forceValid = Known(current.ReadForceNativeValidity(force), "fire_ratio_Force_validity");
                if (forceValid)
                {
                    var currentPlayer = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                        ?? throw Missing("fire_ratio_current_player");
                    int identity = Known(current.ReadForcePhysicalSlot(currentPlayer), "fire_ratio_player_identity");
                    player = identity != 0 && identity == Known(current.ReadForcePhysicalSlot(force), "fire_ratio_target_Force_identity");
                }
            }
            //232A50 is reached even for an invalid governor; its first
            //location gate is independent of native Person validity.
            ushort first = Known(current.ReadPersonFirstLocation(Physical(governor)), "fire_ratio_Person_E2");
            bool special = false;
            if (first is >= 1 and <= 61)
            {
                var force = current.ReadPersonForce(Physical(governor)) ?? throw Missing("fire_ratio_special_Force");
                special = Known(current.ReadForcePhysicalSlot(force), "fire_ratio_special_Force_identity") is >= 46 and <= 50;
            }
            var input = new NormalArmyCombatWoundedRatioInput(valid, charm, rawMorale, false,
                terrainValid, category, null, selector, difficulty, forceValid, player, false, false, 0, 0, special);
            float ratio = NormalArmyCombatApplicationSourceMath.CalculateCurrentWoundedRatio(input, Parameter)
                ?? throw Missing("fire_Base_wounded_ratio_17D260");
            int addition = NativeTruncate(ratio * (float)applied);
            if (Known(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "fire_addition_advanced_enabled"))
                addition = Math.Min(applied, unchecked(Known(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 7),
                    "fire_addition_advanced7") * addition) / 100);
            WriteWounded(addition);
        }
        if (WoundedGate())
        {
            float percent = (float)Parameter(SimulationParameterKeys.NormalCombatApplication.WoundedSubtractionPercent) / 100f;
            int subtraction = NativeTruncate(percent * (float)applied);
            WriteWounded(unchecked(-subtraction));
        }
        byte morale = Known(current.ReadBaseMorale44(target), "fire_Base_raw44_after_wounded");
        if (morale != 0)
        {
            var governor = Governor();
            int leadership = PersonValid(governor) ? Ability(governor, 0) : 0;
            int wounded = Known(current.ReadArmyReturnBaseWounded40(target), "fire_Base40_morale");
            int troops = Known(current.ReadBaseResource(target, BaseResourceKind.Troops), "fire_Base3C_morale");
            var loss = NormalArmyCombatApplicationSourceMath.CalculateCurrentBaseMoraleLoss(wounded, troops,
                leadership, applied, Parameter, currentSampler);
            if (loss.Loss > 0)
            {
                int rawForce = Known(current.ReadBaseForceSlot(target), "fire_morale_fresh_Force_slot");
                if (rawForce < 0) throw Missing("fire_morale_negative_Force_index");
                var force = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0)
                    ?? throw Missing("fire_morale_fresh_Force_pointer");
                int upper = BasePostLoad.ReadMoraleUpper(force, current);
                byte before = Known(current.ReadBaseMorale44(target), "fire_morale_fresh_raw44");
                int bounded = Bound(unchecked((int)before - loss.Loss), upper);
                moraleWritten = BasePostLoad.WriteMoraleCurrent(target, bounded, current);
            }
        }
        int finalForce = Known(current.ReadBaseForceSlot(target), "fire_Base_fresh_virtualB0");
        int finalTroops = Known(current.ReadBaseResource(target, BaseResourceKind.Troops), "fire_Base_final3C");
        bool fatal = finalTroops <= 0 && (finalForce != 0 || sourcePresent);
        var working = PublishCurrentBaseCombatQuantities(state, current, target,
            troopsWritten, woundedWritten, moraleWritten);
        if (fatal)
            return new((applyFatal ?? throw Missing("base_null_actor_fatal1745B0"))(working, target), target, true);
        return new(working, target, false);

        int Parameter(string key) => NormalArmyCombatApplicationSourceMath.ReadCurrentParameter(state, _normalApplicationSeed, key);
        bool WoundedGate()
        {
            int selector = Known(BattleRemakeCurrentSettings.ReadSelectorE58(state), "fire_wounded_E58");
            if (selector != 0 && cc == 0) return true;
            return Known(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "fire_wounded_advanced_enabled") &&
                Known(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 7), "fire_wounded_advanced7") > 0;
        }
        void WriteWounded(int adjustment)
        {
            int before = Known(current.ReadArmyReturnBaseWounded40(target), "fire_Base40_before");
            var write = AdjustCurrentBaseCombatQuantity(BaseCombatQuantity.WoundedTroops, before,
                adjustment, () => BasePostLoad.ReadTroopCapacity(target, current));
            current.SetArmyReturnBaseWounded40(target, write.After); woundedWritten = true;
        }
        HexReference BaseTargetHex()
        {
            ushort link = Known(current.ReadBaseObjectLink(target), "fire_Base4E");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("fire_Base_Object_pointer");
            int lookupHex = 0;
            if (Known(current.ReadObjectNativeValidity(obj), "fire_Base_Object_validity"))
            {
                ushort raw = Known(current.ReadObjectHexIndex(obj), "fire_Base_Object10");
                if (raw < 48400) lookupHex = raw;
            }
            return current.ReadHexTableReference(lookupHex) ?? throw Missing("fire_Base_Hex_pointer");
        }
        TransferPersonReference Governor()
        {
            ushort raw = Known(current.ReadArmyReturnBaseGovernor2E(target), "fire_Base_Governor2E");
            return (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("fire_Base_Governor_pointer");
        }
        int Physical(TransferPersonReference person) => Known(person.PhysicalSlot, "fire_Governor_physical");
        bool PersonValid(TransferPersonReference person) => person.PhysicalSlot.HasValue &&
            Known(current.ReadPersonNativeValidity(person.PhysicalSlot.Value), "fire_Governor_validity");
        int Ability(TransferPersonReference person, int dimension)
        {
            int identity = Known(current.ReadPersonIdentity(Physical(person)), "fire_Governor_identity");
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            return Known(abilities.ResolveDimension(state, identity, dimension), "fire_Governor_ability" + dimension);
        }
    }

    private enum BaseCombatQuantity { ReadyTroops, WoundedTroops }

    /// <summary>The caller owns the before read and the single write. This
    /// receipt computation performs exactly two independent fresh cap reads.</summary>
    private static NormalCityCapacityWriteReceipt AdjustCurrentBaseCombatQuantity(BaseCombatQuantity field,
        int before, int adjustment, Func<int> readFreshTroopCapacity)
    {
        int firstCapacity = readFreshTroopCapacity();
        int first = Bound(unchecked(before + adjustment), firstCapacity);
        int secondCapacity = readFreshTroopCapacity();
        return new(field == BaseCombatQuantity.ReadyTroops ? "3C" : "40", before, adjustment,
            firstCapacity, first, secondCapacity, Bound(first, secondCapacity));
    }

    private static GameState PublishCurrentBaseCombatQuantities(GameState state, SourceWorldContext current,
        BaseReference target, bool troopsWritten, bool woundedWritten, bool moraleWritten)
    {
        if (!troopsWritten && !woundedWritten && !moraleWritten) return state;
        int physical = Known(target.PhysicalSlot, "publish_Base_pointer");
        if (target.Kind == BaseKind.City)
        {
            var city = state.Cities.GetValueOrDefault(physical) ?? throw Missing("publish_canonical_City");
            int troops = troopsWritten ? Known(current.ReadBaseResource(target, BaseResourceKind.Troops), "City_publish3C") : city.Troops;
            int? morale = moraleWritten ? Known(current.ReadBaseMorale44(target), "City_publish44") : city.Morale;
            var runtime = city.NativeReturnRuntime;
            if (woundedWritten || moraleWritten)
            {
                runtime = runtime ?? throw Missing("publish_City_runtime");
                runtime = runtime with {
                    Wounded40 = woundedWritten ? Known(current.ReadArmyReturnBaseWounded40(target), "City_publish40") : runtime.Wounded40,
                    Morale44 = moraleWritten ? Known(current.ReadBaseMorale44(target), "City_publish_raw44") : runtime.Morale44 };
            }
            return state with { Cities = new(state.Cities) { [physical] = city with { Troops = troops, Morale = morale, NativeReturnRuntime = runtime } } };
        }
        int location = checked(physical + 51);
        var gate = state.Gates.GetValueOrDefault(location) ?? throw Missing("publish_canonical_Gate");
        int ready = troopsWritten ? Known(current.ReadBaseResource(target, BaseResourceKind.Troops), "Gate_publish3C") : gate.Troops;
        var gateRuntime = gate.NativeCombatRuntime;
        if (woundedWritten || moraleWritten)
        {
            gateRuntime = gateRuntime ?? throw Missing("publish_Gate_runtime");
            gateRuntime = gateRuntime with {
                Wounded40 = woundedWritten ? Known(current.ReadArmyReturnBaseWounded40(target), "Gate_publish40") : gateRuntime.Wounded40,
                Morale44 = moraleWritten ? Known(current.ReadBaseMorale44(target), "Gate_publish_raw44") : gateRuntime.Morale44 };
        }
        return state with { Gates = new(state.Gates) { [location] = gate with { Troops = ready, NativeCombatRuntime = gateRuntime } } };
    }
}

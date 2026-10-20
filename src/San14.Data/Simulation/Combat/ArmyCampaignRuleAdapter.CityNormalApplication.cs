using San14.Core;

namespace San14.Data;

public enum NormalCityApplicationOutcome { CommittedNonfatal, NativeTargetSkipped, CurrentInputsUnresolved, CommittedFatalApplication }
public sealed record NormalCityCapacityWriteReceipt(string Field, int Before, int Adjustment,
    int FirstCapacity, int AfterFirstClamp, int SecondCapacity, int After);
public sealed record NormalCityWoundedReceipt(bool ChannelEnabled,
    NormalArmyCombatWoundedRatioInput? RatioInput, float? Ratio,
    int AddedBeforeAdvancedPercent, int AddedAfterAdvancedPercent,
    NormalCityCapacityWriteReceipt? Addition, int Subtracted,
    NormalCityCapacityWriteReceipt? Subtraction);
public sealed record NormalCityMoraleWriteReceipt(byte Before, int Adjustment,
    int FirstCapacity, int AfterFirstClamp, byte After);
public sealed record NormalCityApplicationReceipt(int CityId, int AttackerArmyId, int RequestedLoss,
    int AppliedLoss, PersonNormalArmyRewardResult AttackerReward,
    NormalCityCapacityWriteReceipt? TroopDebit, NormalCityWoundedReceipt? Wounded,
    NormalArmyCombatMoraleReceipt? MoraleLoss, NormalCityMoraleWriteReceipt? MoraleWrite,
    bool NativeReturn, string Revision);
public sealed record NormalCityApplicationResult(NormalCityApplicationOutcome Outcome,
    GameState State, NormalCityApplicationReceipt? Receipt, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>TC178EC0's ordinary actual Army→City nonfatal body. The
    /// source attacker reward precedes the signed City cap. Troops3C and
    /// wounded40 stay int32 and use two fresh capacity reads per write. A
    /// nonfatal City returns nativefalse before the Army-only Force/personality
    /// tail. Gate, challenge receiver, special Force and fatal City are outside
    /// this finite operation. Unknown discards the detached draft.</summary>
    public NormalCityApplicationResult TryApplyNativeNormalCityApplication(GameState input,
        int attackerArmyId, int cityId, int requestedLoss,
        NativeArmyCombatCycleSampler? operationSampler = null)
    {
        var result = TryApplyNativeNormalBaseApplication(input, attackerArmyId, BaseKind.City,
            cityId, requestedLoss, operationSampler);
        var r = result.Receipt;
        return new(result.Outcome, result.State, r is null ? null : new(r.LocationId, r.AttackerArmyId,
            r.RequestedLoss, r.AppliedLoss, r.AttackerReward, r.TroopDebit, r.Wounded, r.MoraleLoss,
            r.MoraleWrite, r.NativeReturn, r.Revision), result.UnresolvedOwner);
    }

    private sealed record NormalBaseApplicationReceipt(int LocationId, int AttackerArmyId, int RequestedLoss,
        int AppliedLoss, PersonNormalArmyRewardResult AttackerReward,
        NormalCityCapacityWriteReceipt? TroopDebit, NormalCityWoundedReceipt? Wounded,
        NormalArmyCombatMoraleReceipt? MoraleLoss, NormalCityMoraleWriteReceipt? MoraleWrite,
        bool NativeReturn, string Revision)
    {
        public BaseFatalAdmissionReceipt? FatalAdmission { get; init; }
    }
    private sealed record NormalBaseApplicationResult(NormalCityApplicationOutcome Outcome,
        GameState State, NormalBaseApplicationReceipt? Receipt, string? UnresolvedOwner);

    /// <summary>Shared ordinary Base kind5/6 body. The caller supplies an
    /// actual logical location; source manager80 resolves its native receiver.</summary>
    private NormalBaseApplicationResult TryApplyNativeNormalBaseApplication(GameState input,
        int attackerArmyId, BaseKind kind, int locationId, int requestedLoss,
        NativeArmyCombatCycleSampler? operationSampler, bool allowFatal = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        bool isCity = kind == BaseKind.City;
        string label = isCity ? "City" : "Gate";
        int physical = isCity ? locationId : locationId - 51;
        if (requestedLoss < 0 || (isCity ? locationId is < 1 or > 51 || !input.Cities.ContainsKey(locationId)
            : physical is < 1 or > 10 || !input.Gates.ContainsKey(locationId)))
            return Unknown($"current.ordinary_{label}_nonnegative_loss");
        GameState working = GameStateCopy.Copy(input);
        try
        {
            if (!working.Units.TryGetValue(attackerArmyId, out var attacker) ||
                CaptureCurrentDefinitions(working) is not { } attackerDefinitions ||
                Context(attackerDefinitions, operationSampler).ArmyValid(working, attacker) != true)
                return Unknown("current.actual_attacker_Army");
            var reward = requestedLoss > 0
                ? TryApplyNativeNormalPersonRewardDuringArmyOperation(working, attacker.OfficerId, 0, requestedLoss)
                : new(PersonNormalArmyRewardOutcome.NativeSkip, working, null, null);
            if (reward.Outcome == PersonNormalArmyRewardOutcome.CurrentInputsUnresolved)
                return Unknown("current.attacker_reward:" + reward.MissingPhase);
            working = reward.State;
            var world = CaptureOwnedArmyReturnWorld(working); var current = world.Context;
            var target = BaseLookup.Find(locationId, current);
            if (target.Kind != kind || target.PhysicalSlot != physical)
                return Unknown($"current.actual_{label}_manager80_pointer");
            bool validCity = Known(current.ReadBaseNativeValidity(target), label + "_validity");
            if (!validCity)
                return new(NormalCityApplicationOutcome.NativeTargetSkipped, working,
                    new(locationId, attackerArmyId, requestedLoss, 0, reward, null, null, null, null, false, Revision), null);
            if (current.ReadBaseObjectLink(target) is not ushort link || link != locationId ||
                current.ReadBaseForceSlot(target) is not int forceSlot || forceSlot is < 1 or > 45 ||
                current.ReadForceTableReference(forceSlot) is not { } force || current.ReadForceNativeValidity(force) != true ||
                (isCity ? working.Cities[locationId].NativeReturnRuntime is null : working.Gates[locationId].NativeCombatRuntime is null))
                return Unknown($"current.ordinary_{label}_Force_and_saved_runtime");
            //The independent ordinary campaign owns no570850 tutorial/
            //challenge receiver; the same source-known absent branch is
            //installed by CaptureOwnedArmyReturnWorld for normal operations.
            if (current.ArmyReturnTransferMode is not { Present: false })
                return Unknown("current.challenge_570850");
            int beforeTroops = Known(current.ReadBaseResource(target, BaseResourceKind.Troops), label + "3C");
            int applied = Math.Min(beforeTroops, requestedLoss);
            if (beforeTroops <= 0 || applied < 0) return Unknown($"branch.{label}_zero_or_negative_troops");
            NormalCityCapacityWriteReceipt debit = AdjustCurrentBaseCombatQuantity(BaseCombatQuantity.ReadyTroops,
                beforeTroops, unchecked(-applied), () => BasePostLoad.ReadResourceCapacities(target, current).Troops);
            if (debit.After <= 0 && !allowFatal) return Unknown($"branch.{label}_fatal_1745B0");
            current.SetBaseResource(target, BaseResourceKind.Troops, debit.After);
            var parameters = NormalArmyCombatApplicationSourceMath.ReadCurrent(working, _normalApplicationSeed)
                ?? throw Missing("application_current_parameters");
            int selector = Known(BattleRemakeCurrentSettings.ReadSelectorE58(working), "settings_E58");
            int cc = selector != 0 ? Known(BattleRemakeCurrentSettings.ReadInt32(working, 0xCC), "settings_CC") : 0;
            int? advanced = selector == 0
                ? Known(BattleRemakeCurrentSettings.ReadAdvancedParameter(working, 7), "advanced_wounded7") : null;
            bool channel = selector != 0 ? cc == 0 : advanced > 0;
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            int? governorPhysical = null; bool? governorValid = null;
            int added = 0, adjusted = 0, subtracted = 0;
            NormalArmyCombatWoundedRatioInput? ratioInput = null; float? ratio = null;
            NormalCityCapacityWriteReceipt? addition = null, subtraction = null;
            if (channel)
            {
                int beforeWounded = Known(current.ReadArmyReturnBaseWounded40(target), label + "40");
                if (beforeWounded < 0) return Unknown($"current.{label}40_signed_negative");
                var governor = Governor();
                int charm = governorValid == true ? Known(abilities.Resolve(working, governor)?.Charm, "Governor_full_charm4") : 0;
                ushort hex = current.ReadBaseCurrentHexIndex(target);
                if (hex >= 48400 || current.ReadHexTableReference(hex) is not { } targetHex || targetHex.PhysicalSlot != hex)
                    return Unknown($"current.{label}_Object_Hex_22D640");
                byte surface = Known(current.ReadHexSurfaceCode(targetHex), label + "_Hex_surface10");
                var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
                    ?? throw Missing(label + "_Terrain_pointer_231A10");
                byte category = Known(current.ReadSurfaceTypeWoundedCategory42(terrain), label + "_Terrain42");
                int attackerSlot = Known(attacker.NativeArmyOrigin?.NativeSlotIndex, "attacker_physical_slot");
                var nativeArmy = current.ReadArmyUnitTableReference(attackerSlot) ?? throw Missing("attacker_Army_pointer");
                if (current.ReadArmyUnitNativeValidity(nativeArmy) != true) return Unknown("current.attacker_Army_validity");
                ushort attackerHex = Known(current.ReadArmyUnitRawHex2A(nativeArmy), "attacker_raw_Hex2A");
                if (attackerHex >= 48400) return Unknown("current.attacker_Hex2A_outside_map");
                int difficulty = selector != 0 ? Known(BattleRemakeCurrentSettings.ReadInt32(working, 0x28), "wounded_difficulty28") : 0;
                bool governorForceValid = false;
                ushort governorFirst = Known(current.ReadPersonFirstLocation(governorPhysical!.Value), "Governor_E2_special_predicate");
                if (governorFirst is >= 1 and <= 61 &&
                    (current.ReadPersonForce(governorPhysical.Value) ?? throw Missing("Governor_special_Force_pointer")).PhysicalSlot is >= 46 and <= 50)
                    return Unknown("branch.Governor_special_Force");
                if (governorValid == true)
                {
                    var governorForce = current.ReadPersonForce(governorPhysical!.Value) ?? throw Missing("Governor_Force_pointer");
                    governorForceValid = Known(current.ReadForceNativeValidity(governorForce), "Governor_Force_validity");
                    if (governorForceValid && governorForce.PhysicalSlot is not (>= 1 and <= 45))
                        return Unknown("branch.Governor_special_Force");
                }
                if (selector != 0 && difficulty >= 3 && governorForceValid)
                    return Unknown("current.wounded_2338E0_player_predicate");
                //17D260 receives the raw Base44, terrain, null param5 and
                //param6=0. Its effect6F window is genuinely disabled even
                //though the actual attacking Army above is valid.
                ratioInput = new(governorValid == true, charm, Known(current.ReadBaseMorale44(target), label + "_raw44"),
                    SourceDistance(hex, attackerHex) == (link < 52 ? 2 : 1), true, category,
                    category == 1 ? NormalArmyCombatApplicationSourceMath.ReadCurrentTerrain1Addend(working) : null,
                    selector, difficulty, governorForceValid, false, false, false, 0, 0, false);
                ratio = NormalArmyCombatApplicationSourceMath.CalculateWoundedRatio(parameters.CurrentValues, ratioInput);
                if (ratio is null || !float.IsFinite(ratio.Value) || ratio < 0)
                    return Unknown($"current.{label}_wounded_ratio_17D260");
                added = NativeTruncate(ratio.Value * (float)applied);
                adjusted = selector == 0 ? Math.Min(applied, unchecked(advanced!.Value * added) / 100) : added;
                addition = AdjustCurrentBaseCombatQuantity(BaseCombatQuantity.WoundedTroops,
                    beforeWounded, adjusted, () => BasePostLoad.ReadResourceCapacities(target, current).Troops);
                current.SetArmyReturnBaseWounded40(target, addition.After);
                float removed = (float)parameters.CurrentValues.WoundedSubtractionPercent / 100f;
                subtracted = NativeTruncate(removed * (float)applied);
                subtraction = AdjustCurrentBaseCombatQuantity(BaseCombatQuantity.WoundedTroops,
                    addition.After, unchecked(-subtracted), () => BasePostLoad.ReadResourceCapacities(target, current).Troops);
                current.SetArmyReturnBaseWounded40(target, subtraction.After);
            }
            var wounds = new NormalCityWoundedReceipt(channel, ratioInput, ratio, added, adjusted, addition, subtracted, subtraction);
            NormalArmyCombatMoraleReceipt? moraleLoss = null; NormalCityMoraleWriteReceipt? moraleWrite = null;
            byte rawMorale = Known(current.ReadBaseMorale44(target), label + "_raw44_after_wounded");
            if (rawMorale != 0)
            {
                int governor = Governor();
                int leadership = governorValid == true ? Known(abilities.Resolve(working, governor)?.Leadership, "Governor_full_leadership0") : 0;
                int finalWounded = Known(current.ReadArmyReturnBaseWounded40(target), label + "40_after_wounded");
                //For positive troops and zero wounds the exact native f32
                //fraction is1, so17D580 returns before any World draw. Null
                //here is an unconsumed formal, not asserted World absence.
                var sampler = finalWounded == 0 && debit.After > 0 ? null
                    : Context(current, operationSampler).ReadCurrentSamplerWorld(working)
                        ?? throw Missing("current_cycle_sampler_world");
                moraleLoss = NormalArmyCombatApplicationSourceMath.CalculateBaseMoraleLoss(parameters.CurrentValues,
                    finalWounded, debit.After, leadership, applied, sampler);
                if (moraleLoss.Loss > 0)
                {
                    int firstCap = BasePostLoad.ReadMoraleUpper(force, current);
                    int first = Bound(unchecked((int)rawMorale - moraleLoss.Loss), firstCap);
                    //WriteMorale performs the two fresh Force queries,
                    //special-Force setter gate and second capacity/clamp.
                    BasePostLoad.WriteMorale(target, first, current);
                    moraleWrite = new(rawMorale, moraleLoss.NativeAdjustment, firstCap, first,
                        Known(current.ReadBaseMorale44(target), label + "44_after_223A70"));
                }
            }
            //Fresh virtualB0 precedes the source3C>0 return. There is no
            //Army-only Force result/personality or fatal queue in this branch.
            _ = Known(current.ReadBaseForceSlot(target), label + "_fresh_virtualB0");
            bool fatal = Known(current.ReadBaseResource(target, BaseResourceKind.Troops), label + "_final3C") <= 0;
            BaseFatalAdmissionReceipt? admission = null;
            if (fatal)
            {
                if (!allowFatal) return Unknown($"branch.{label}_fatal_after_callback");
                //This actual caller's Army is valid, so178EC0's joined final
                //condition reaches1745B0 even if virtualB0 happens to be0.
                admission = ApplyOrdinaryBaseFatalAdmission(working, current, attacker, target);
                if (admission.ImmediateFallback) return Unknown("branch.Base_immediate2851B0");
            }
            //This branch wrote only target3C/40/44. Retain every unrelated
            //Person/Base field from the reward state; the general return
            //publisher also projects district/governor/person fields and is
            //not this source operation's writer.
            working = PublishCurrentBaseCombatQuantities(working, current, target,
                troopsWritten: true, woundedWritten: channel, moraleWritten: moraleWrite is not null);
            if (admission is not null)
                working = working with { PendingCombatSettlements = current.OwnedDestructionQueue };
            StateValidator.Validate(working);
            return new(fatal ? NormalCityApplicationOutcome.CommittedFatalApplication : NormalCityApplicationOutcome.CommittedNonfatal, working,
                new(locationId, attackerArmyId, requestedLoss, applied, reward, debit, wounds, moraleLoss, moraleWrite,
                    fatal, $"native-{label}-178EC0-{(fatal ? "fatal-admission" : "nonfatal")}:{Revision}")
                { FatalAdmission = admission }, null);

            int Governor()
            {
                if (governorPhysical is null)
                {
                    ushort id = Known(current.ReadArmyReturnBaseGovernor2E(target), label + "_Governor2E");
                    var person = current.ReadPersonLookupReference(id <= 6000 ? id : 0) ?? throw Missing("Governor_Person_pointer");
                    governorPhysical = Known(person.PhysicalSlot, "Governor_physical");
                    governorValid = Known(current.ReadPersonNativeValidity(governorPhysical.Value), "Governor_native_validity");
                }
                return Known(current.ReadPersonIdentity(governorPhysical.Value), "Governor_identity");
            }
        }
        catch (InvalidDataException missing) { return Unknown(missing.Message); }
        NormalBaseApplicationResult Unknown(string field) => new(NormalCityApplicationOutcome.CurrentInputsUnresolved, input, null, field);
    }
    private static int Bound(int input, int upper) => input < 0 ? 0 : Math.Min(input, upper);
    private static int NativeTruncate(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f
        ? int.MinValue : (int)value;
    private static int SourceDistance(int first, int second)
    {
        int x1 = first % 220, y1 = first / 220, x2 = second % 220, y2 = second / 220;
        int dx = Math.Abs(x2 - x1), dy = Math.Abs(2 * (y2 - y1) - (x1 % 2 == 0 ? 1 : 0) + (x2 % 2 == 0 ? 1 : 0));
        return dy < dx ? dx : dx + (dy - dx) / 2;
    }
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("Current Base178EC0 owner is unknown: " + field);
}


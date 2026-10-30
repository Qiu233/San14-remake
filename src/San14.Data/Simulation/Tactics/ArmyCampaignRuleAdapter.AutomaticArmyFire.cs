using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record AutomaticArmyFirePrefix(int Duration, int SourceForce);

public sealed partial class TacticRemakeCurrentContext
{
    //177C60 reads16B6D0, the stage product, then16D900 before conversion.
    //Its Army commander lookup preserves the22E390 native-person gate.
    internal TacticEffectScaleSourceInput ResolveAutomaticArmyFireScale(GameState state,
        SourceWorldContext current, UnitState caster, TacticSourceRow source,
        S14TacticEffectInstruction instruction, int stageOrdinal)
    {
        if (!ReferenceEquals(current, _definitions) || !Bound(state) || instruction.Opcode != 17 ||
            instruction.Index is < 0 or > 1)
            throw FireMissing("instruction_current");
        var army = current.ReadArmyUnitTableReference(Need(caster.NativeArmyOrigin?.NativeSlotIndex,
            "source_Army_physical")) ?? throw FireMissing("source_Army_pointer");
        if (!Need(current.ReadArmyUnitNativeValidity(army), "source_Army_validity"))
            throw FireMissing("invalid_source_Army_fallback_Base");
        int strength = Ability(1), intelligence = Ability(2);
        byte selector = FactorSelector(source) ?? throw FireMissing("selector6A");
        int divisor = P(selector == 1 ? SimulationParameterKeys.Tactics.StrengthEffectDivisor
            : SimulationParameterKeys.Tactics.IntelligenceEffectDivisor);
        if (divisor == 0) throw FireMissing("ability_zero_divisor");
        float ability = (float)(selector == 1 ? strength : intelligence) / (float)divisor;
        ability += 1f;
        int stage = P(SimulationParameterKeys.Tactics.EffectStageScale);
        var native = caster.NativeTacticRuntime ?? throw FireMissing("source_waiting21_22");
        float waiting = native.Waiting21 != 0 ? 1f :
            (float)P(native.Waiting22 == 0 ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent
                : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent) / 100f;
        return new(instruction.EffectValue, ability, stageOrdinal, stage, waiting);

        int Ability(int dimension)
        {
            ushort commander = Need(current.ReadArmyUnitCommanderId(army), "source_Commander12");
            var person = commander <= 6000 ? current.ReadPersonLookupReference(commander)
                : current.ReadTransferPersonTableReference(0);
            int physical = Need(person?.PhysicalSlot, "source_Commander_pointer");
            if (!Need(current.ReadPersonNativeValidity(physical), "source_Commander_validity")) return 0;
            int id = Need(current.ReadPersonIdentity(physical), "source_Commander_identity");
            int? value = _abilities is TacticRemakeCurrentAbilities owned ? owned.ResolveDimension(state, id, dimension)
                : dimension == 1 ? _abilities?.Resolve(state, id)?.Strength : _abilities?.Resolve(state, id)?.Intelligence;
            return value ?? throw FireMissing("source_ability_dimension" + dimension);
        }
        int P(string key) => ReadAutomaticAllModifierParameter(state, key);
        static T Need<T>(T? value, string key) where T : struct => value ?? throw FireMissing(key);
        static InvalidDataException FireMissing(string key) => new("automatic_Army.fire." + key);
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Army177C60 prefix, reached once even for an empty vector.
    /// Policy19 scales the already truncated duration, before its final add.</summary>
    internal AutomaticArmyFirePrefix BeginBorrowedAutomaticArmyFireCurrent(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId,
        TacticSourceRow row, S14TacticEffectInstruction instruction, int stageOrdinal)
    {
        var caster = RequireLatestAutomaticActorImageCurrent(state, current, casterId);
        var army = current.ReadArmyUnitTableReference(FireNeed(caster.NativeArmyOrigin?.NativeSlotIndex,
            "source_Army_physical")) ?? throw FireMissing("source_Army_pointer");
        var scale = context.ResolveAutomaticArmyFireScale(state, current, caster, row, instruction, stageOrdinal);
        int duration = TacticEffectSourceMath.Scale(scale);
        float policyFactor = 0f;
        var force = current.ReadArmyUnitForceReference(army);
        if (FireNeed(current.ReadForceNativeValidity(force), "source_policy_Force_validity"))
        {
            int identity = FireNeed(current.ReadForcePhysicalSlot(force), "source_policy_Force60");
            if (identity is not (>= 46 and <= 51))
            {
                var owner = state.Forces.GetValueOrDefault(identity) ?? throw FireMissing("source_policy_Force_owner");
                int policy = FireNeed(BattleRemakeCurrentForcePolicies.ReadForceEffect(state, owner, 19, current), "source_policy19");
                int multiplier = TacticRemakeCurrentContext.OriginStatic(state, "fire-policy-multiplier");
                policyFactor = (float)unchecked(multiplier * policy) / 100f;
            }
        }
        //The wrapper reads22CD80 again after16E600; this identity is used by
        //the asymmetric Object/Army relation gates during the ordered walk.
        int sourceForce = current.ReadArmyUnitRawForceSlot(army);
        int additional = FireTruncate(policyFactor * (float)duration);
        return new(unchecked(duration + additional), sourceForce);
    }

    /// <summary>Run before generic occupant decoding, including empty/Object
    /// Hexes. Only the original selected Hex may produce its actual marker.</summary>
    internal BaseOriginAutomaticFireResult ApplyBorrowedAutomaticArmyFireHexCurrent(GameState state,
        SourceWorldContext current, AutomaticArmyFirePrefix prefix, int nodeHex, int primaryHex,
        NativeArmyCombatCycleSampler sampler, NativeMapSeasonDate date, long revision,
        List<CampaignEffectRequest> effects)
        => ApplyAutomaticFireHexCurrent(state, current, prefix.Duration, prefix.SourceForce, nodeHex, primaryHex,
            reached => (sampler.Read(reached) ?? throw FireMissing("report_Day450")).Day450, revision, effects,date);

    //Source-independent177C60 Hex body shared with the real Base wrapper.
    //It retains2C6CE0/2C6D20,2CA650,2E3CB0,17A1C0 in their native order.
    private BaseOriginAutomaticFireResult ApplyAutomaticFireHexCurrent(GameState state,
        SourceWorldContext current, int duration, int sourceForce, int nodeHex, int primaryHex,
        Func<GameState, int> readDay, long revision, List<CampaignEffectRequest> effects,NativeMapSeasonDate date)
    {
        var hex = current.ReadHexTableReference(nodeHex) ?? throw FireMissing("target_Hex_pointer");
        if (hex.PhysicalSlot is not int physical) return new(state, false, null);
        ushort link = FireNeed(current.ReadHexObjectLink(hex), "target_Hex1A");
        //2C6CE0 tests nonzero Hex1A, not an Object definition/category.
        if (link != 0)
        {
            var obj = current.ReadObjectTableReference(link < 3001 ? link : 0)
                ?? throw FireMissing("target_Object_pointer");
            if (FireNeed(current.ReadObjectNativeValidity(obj), "target_Object_validity") &&
                !Allowed(FireNeed(current.ReadObjectForceSlot13(obj), "target_Object13")))
                return new(state, false, null);
        }
        //2C6D20's real1..500 signed lookup gate precedes native validity.
        int armyIndex = unchecked((short)FireNeed(current.ReadHexRaw1C(hex), "target_Hex1C"));
        if (armyIndex is >= 1 and <= 500)
        {
            var army = current.ReadArmyUnitTableReference(armyIndex) ?? throw FireMissing("target_Army_pointer");
            if (FireNeed(current.ReadArmyUnitNativeValidity(army), "target_Army_validity") &&
                !Allowed(FireNeed(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army)), "target_Army_Force22CD80")))
                return new(state, false, null);
        }
        var changes = new List<HexFireChange>();
        var announcements = new List<ArmyFireAnnouncement>();
        var working = IgniteCurrentHex(state, current, physical, duration, true,
            readDay, revision, changes, announcements, effects,date);
        if (announcements.Count != 0)
            working = working with { PendingArmyFireAnnouncements =
                [.. working.PendingArmyFireAnnouncements, .. announcements] };
        //Hex16 stays in the operation's current write set. The enclosing
        //execution publishes it once before its normal return or save pause.
        if (physical != primaryHex) return new(working, false, null);
        var selected = FieldHexOccupantSource.ReadCurrent(working, current, physical);
        var marker = selected.Receipt ?? throw FireMissing(selected.UnresolvedOwner ?? "marker16EB20");
        //17A1C0 excludes only255. Kind11 is an actual Object marker;
        //the enclosing presentation translates it to its real receiver ID.
        return new(working, marker.NativeKind != 255, marker.NativeKind == 255 ? null : marker);

        bool Allowed(int targetForce) => FireNeed(
            ArmyLogisticsPreparation.ReadNativeRelationGate(state, sourceForce, targetForce), "relation2CA650");
    }
}

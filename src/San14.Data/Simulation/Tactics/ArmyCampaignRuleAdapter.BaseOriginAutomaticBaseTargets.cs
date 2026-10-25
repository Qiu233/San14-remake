using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>176C10's live Base receiver admission for a real Base actor.
    /// The numeric pipeline is shared with its Army-receiver branch; only
    /// the receiver's special-governor gate precedes those reached queries.</summary>
    internal AutomaticArmyBaseTroopDamageAmount ResolveBaseOriginAutomaticBaseTroopDamage(
        GameState state, BaseReference source, FieldHexOccupantReceipt target,
        TacticSourceRow row, S14TacticEffectInstruction instruction,
        BaseOriginAutomaticTroopPrefix prefix)
    {
        if (target.NativeKind is not (5 or 6) || instruction.Opcode != 1)
            throw OriginAutomaticMissing("Base_troop_receiver");
        var receiver = new BaseReference(target.NativeKind == 5 ? BaseKind.City : BaseKind.Gate,
            target.NativePhysicalSlot);
        if (!OriginNeed(_definitions.ReadBaseNativeValidity(receiver), "Base_troop_validity"))
            return new(false, false, null, "invalid_Base");
        bool special = OriginNeed(_definitions.ReadConditionBaseSpecialGovernor(receiver), "Base_troop_special232950");
        if (special && OriginNeed(_definitions.ReadBaseObjectLink(receiver), "Base_troop4E") <= 51)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, _definitions)
                ?? throw OriginAutomaticMissing("Base_troop_player_pointer");
            if (!OriginNeed(_definitions.ReadForceNativeValidity(player), "Base_troop_player_validity"))
                return new(true, false, null, "special_City_invalid_player");
            if (prefix.SourceForce != OriginNeed(_definitions.ReadForcePhysicalSlot(player), "Base_troop_player_identity"))
                return new(true, false, null, "special_City_nonplayer_caster");
        }
        return new(true, true, ResolveBaseOriginAutomaticTroopDamage(state, source, target,
            row, instruction, prefix), null);
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>178EC0 with a real Base source and Base target. No Army reward
    /// or attacker-direction term is reached. The caller owns1745B0's saved
    /// District admission and immediate2851B0 fallback in the same operation.</summary>
    internal BaseTargetApplicationResult ApplyBaseOriginAutomaticBaseDamageCurrent(
        GameState state, SourceWorldContext current, BaseReference origin,
        BaseReference target, int requestedLoss, NativeArmyCombatCycleSampler sampler,
        Func<GameState, BaseReference, DistrictReference, GameState> applyFatal)
    {
        ArgumentNullException.ThrowIfNull(applyFatal);
        int physical = target.PhysicalSlot ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("Base_target_pointer");
        _baseCapacityInputs.BindCurrent(state, current);
        return ApplyFireDamageBaseTarget(state, current, target.Kind, physical, requestedLoss,
            () => sampler.Read(state) ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("Base_target_sampler"),
            (working, recipient) =>
            {
                //1745B0 resolves the source again after the numeric callbacks.
                //An invalid source retains District0; a valid Base uses its
                //actual virtual80 District, without a synthetic Commander.
                var district = current.ReadBaseNativeValidity(origin)
                    ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("Base_source_fatal_validity");
                var receiver = district ? current.ReadBaseDistrictReference(origin)
                    : current.ReadDistrictTableReference(0);
                return applyFatal(working, recipient, receiver
                    ?? throw TacticRemakeCurrentContext.OriginAutomaticMissing("Base_source_fatal_District80"));
            }, sourcePresent: true);
    }

    //177610 has an Army-native-validity entry gate (TC17761F..17762C).
    //A Base actor therefore skips opcode18 before its numeric prefix and
    //target vector, with no wall write, target marker or sampler draw.
}

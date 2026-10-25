using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record AutomaticBaseWallTacticRequest(int SourceForceSlot,
    int ScaledRequest, int Request);
internal sealed record BaseWallTacticApplication(GameState State, bool TargetResultMarker,
    AutomaticBaseWallTacticRequest Prefix, ArmyBaseWallApplicationReceipt? Wall);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>TC177610's actual Base/Object branch, after its instruction
    /// prefix. This is29EC80 wall durability, not178EC0 Base troop damage.
    /// The caller retains the ordered vector and records17A1C0 using the
    /// vector Hex after this writer; pending capture does not remove it here.</summary>
    internal BaseWallTacticApplication ApplyBorrowedBaseWallTactic(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, int casterId,
        BaseReference target, TacticSourceRow source, S14TacticEffectInstruction instruction,
        int stageOrdinal, NativeArmyCombatCycleSampler sampler,
        List<CampaignEffectRequest> effects, AutomaticBaseWallTacticRequest prefix)
    {
        ArgumentNullException.ThrowIfNull(sampler); ArgumentNullException.ThrowIfNull(prefix);
        var caster = BaseTacticCaster(state, current, context, casterId, source, target, instruction, 18);
        if (state.NativeArmySimulation!.Parameters.Language is not ("TC" or "SC"))
            throw WallTacticMissing("wrapper_source_not_qualified");
        int physical = caster.NativeArmyOrigin?.NativeSlotIndex ?? throw WallTacticMissing("caster_physical");
        var actor = current.ReadArmyUnitTableReference(physical) ?? throw WallTacticMissing("caster_pointer");
        ushort link = WallNeed(current.ReadBaseObjectLink(target), "tactic_Base4E");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
            ?? throw WallTacticMissing("target_Object_pointer");
        if (!WallNeed(current.ReadObjectNativeValidity(obj), "tactic_Object_validity"))
            return new(state, false, prefix, null);
        int objectPhysical = WallNeed(obj.PhysicalSlot, "tactic_Object_physical");
        if (objectPhysical is < 1 or > 61) throw WallTacticMissing("target_Object_is_not_Base");
        //232780/22DFF0 classify the actual Object. Base native validity is
        //not an additional wrapper gate. City special governors compare the
        //source Force captured before the instruction's ability queries.
        if (objectPhysical <= 51)
        {
            var city = current.ReadCityTableReference(objectPhysical)
                ?? throw WallTacticMissing("special_City_pointer");
            if (WallNeed(current.ReadConditionBaseSpecialGovernor(city), "tactic_special232950"))
            {
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                    ?? throw WallTacticMissing("special_player_pointer");
                if (!WallNeed(current.ReadForceNativeValidity(player), "tactic_special_player_validity") ||
                    prefix.SourceForceSlot != WallNeed(current.ReadForcePhysicalSlot(player), "tactic_special_player_identity"))
                    return new(state, false, prefix, null);
            }
        }
        var applied = ApplyBorrowedBaseWallDamage(state, current, context, caster, actor, obj,
            prefix.Request, null, effects);
        //The source ignores29EC80's return here and still records a marker
        //for an eligible wrapper receiver, including an already pending Base.
        return new(applied.State, true, prefix, applied.Receipt);
    }

    private static InvalidDataException WallTacticMissing(string owner) =>
        new("automatic_Base_wall_tactic." + owner);
}

public sealed partial class TacticRemakeCurrentContext
{
    //The separate177610 reduction key is shared by the qualified wrappers.
    //SC fresh defaults have their own source profile; an absent TC saved
    //current remains unresolved rather than borrowing that language's value.
    internal const string BaseWallTacticSmallDamageReductionPercent =
        "simulation.tactics.base-wall-small-damage-reduction-percent";

    /// <summary>177610's once-per-instruction22CD80,16B6D0,stage,16D900,
    /// current B8/E58 and advanced-index2 prefix. It is reached even when the
    /// complete instruction vector is empty, and consumes no gameplay draw.</summary>
    internal AutomaticBaseWallTacticRequest ResolveAutomaticBaseWallTacticRequest(GameState state,
        UnitState caster, TacticSourceRow source, S14TacticEffectInstruction instruction, int stageOrdinal)
    {
        if (instruction.Opcode != 18 || instruction.Index is < 0 or > 1)
            throw WallPrefixMissing("instruction18");
        if (!Bound(state) || ArmyValid(state, caster) != true ||
            caster.NativeArmyOrigin?.NativeSlotIndex is not int physical)
            throw WallPrefixMissing("current_caster");
        if (state.NativeArmySimulation!.Parameters.Language is not ("TC" or "SC"))
            throw WallPrefixMissing("wrapper_source_not_qualified");
        var actor = _definitions.ReadArmyUnitTableReference(physical)
            ?? throw WallPrefixMissing("caster_pointer");
        int force = _definitions.ReadArmyUnitRawForceSlot(actor);
        var scale = ResolveBorrowedBaseTacticMoraleScale(state, caster, source, instruction, stageOrdinal);
        int scaled = TacticEffectSourceMath.Scale(scale);
        int reduction = 0;
        if (scaled > 0)
        {
            int setting = BattleRemakeCurrentSettings.ReadInt32(state, 0xB8)
                ?? throw WallPrefixMissing("current_setting_B8");
            if (setting == 0)
                reduction = unchecked(P(BaseWallTacticSmallDamageReductionPercent) * scaled) / 100;
        }
        int selector = BattleRemakeCurrentSettings.ReadSelectorE58(state)
            ?? throw WallPrefixMissing("current_setting_E58");
        int request;
        if (selector != 0) request = unchecked(scaled - reduction);
        else
        {
            //3EC6C0 performs a fresh E58==0 query. It is not tutorial48.
            bool advanced = BattleRemakeCurrentSettings.ReadAdvancedSettings(state)
                ?? throw WallPrefixMissing("current_advanced_E58");
            request = scaled;
            if (advanced)
            {
                int percent = BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 2, 0)
                    ?? throw WallPrefixMissing("current_advanced_index2_row0");
                request = unchecked(percent * scaled) / 100;
                //367BC0 is a release diagnostic, with no gameplay write.
            }
        }
        return new(force, scaled, request);

        int P(string key)
        {
            return AutomaticTacticDamageCurrentParameters.ReadCurrent(state, key)
                ?? throw WallPrefixMissing(key);
        }
        static InvalidDataException WallPrefixMissing(string owner) =>
            new("automatic_Base_wall_tactic.prefix." + owner);
    }
}

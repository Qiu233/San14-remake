using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record AutomaticHexWallTacticApplication(GameState State,
    bool TargetResultMarker, int? BaseLocationId, ArmyBaseWallApplicationReceipt? Wall);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>TC177610 resolves Hex1A to an Object before any result-entity
    /// classification. An Army on that Hex is not an Army-damage operand.
    /// The caller has already run the instruction prefix once, retains this
    /// current/pool, and re-resolves the Hex for17A1C0 only after a true marker.</summary>
    internal AutomaticHexWallTacticApplication ApplyBorrowedAutomaticHexWallTactic(
        GameState state, SourceWorldContext current, TacticRemakeCurrentContext context,
        int casterId, int nodeHex, TacticSourceRow source, S14TacticEffectInstruction instruction,
        int stageOrdinal, NativeArmyCombatCycleSampler sampler,
        List<CampaignEffectRequest> effects, AutomaticBaseWallTacticRequest prefix)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(instruction);
        ArgumentNullException.ThrowIfNull(sampler); ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(prefix);
        if (instruction.Opcode != 18 || instruction.Index is < 0 or > 1 || nodeHex is < 0 or >= 48400)
            throw new InvalidDataException("Hex wall tactic needs the reached instruction and actual vector Hex.");
        if (state.NativeArmySimulation?.Parameters.Language is not ("TC" or "SC"))
            throw HexWallTacticMissing("wrapper_source_not_qualified");

        //22FC70 reads only Hex1A and clamps the Object-table lookup to slot0.
        //Do not use16EB20's Army/Base/generic-Object priority to select a writer.
        var hex = current.ReadHexTableReference(nodeHex) ?? throw HexWallTacticMissing("node_Hex_pointer");
        ushort link = WallNeed(current.ReadHexObjectLink(hex), "tactic_node_Hex1A");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
            ?? throw HexWallTacticMissing("node_Object_pointer");
        if (!WallNeed(current.ReadObjectNativeValidity(obj), "tactic_node_Object_validity"))
            return new(state, false, null, null);

        int physical = WallNeed(current.ReadObjectPhysicalSlot(obj), "tactic_node_Object_physical");
        if (physical is < 1 or > 61)
        {
            //A valid facility underneath an Army is still a real29EC80 target.
            //The current coarse StructureAttacks local writer cannot borrow
            //this operation and fire's actorless writer has a different tail.
            //Never silently skip it or turn its wall loss into Army casualties.
            //29EC80 still has its real early challenge/pending gates before
            //the unshared active-actor facility numeric body. A pending
            //receiver skips that body, but177610 nevertheless records a marker.
            if (current.ArmyReturnTransferMode is not { Present: false })
                throw HexWallTacticMissing("nonBase_challenge570850_498020");
            var pending = current.ReadOwnedCurrentListPool()?.PendingObjectRetirements
                ?? throw HexWallTacticMissing("nonBase_pending_F0");
            var directory = current.CurrentMapObjectDirectory
                ?? throw HexWallTacticMissing("nonBase_Object_directory");
            var receiver = directory.ReceiverAtPhysical(physical);
            foreach (var retired in pending)
                if (retired == receiver) return new(state, true, null, null);
            throw HexWallTacticMissing("actual_nonBase_Object29EC80_writer");
        }
        if (physical <= 51)
        {
            var city = current.ReadCityTableReference(physical)
                ?? throw HexWallTacticMissing("special_City_pointer");
            if (WallNeed(current.ReadConditionBaseSpecialGovernor(city), "tactic_node_special232950"))
            {
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                    ?? throw HexWallTacticMissing("special_player_pointer");
                if (!WallNeed(current.ReadForceNativeValidity(player), "tactic_node_special_player_validity") ||
                    prefix.SourceForceSlot != WallNeed(current.ReadForcePhysicalSlot(player), "tactic_node_special_player_identity"))
                    return new(state, false, physical, null);
            }
        }

        //The shared Base29EC80 owner needs the actual still-live Army caster.
        //No target-Army identity, readiness, healing or B8/C8 query is made.
        if (!context.IsBoundCurrentState(state) || !state.Units.TryGetValue(casterId, out var caster) ||
            caster.NativeArmyOrigin?.NativeSlotIndex is not int actorPhysical || context.ArmyValid(state, caster) != true)
            throw HexWallTacticMissing("current_Army_caster");
        var actor = current.ReadArmyUnitTableReference(actorPhysical)
            ?? throw HexWallTacticMissing("caster_pointer");
        var applied = ApplyBorrowedBaseWallDamage(state, current, context, caster, actor, obj,
            prefix.Request, null, effects);
        //Even a pending Object which29EC80 skipped retains the wrapper marker.
        //NativeReturn is deliberately not converted to a troop-stage skip or98.
        return new(applied.State, true, physical, applied.Receipt);
    }

    private static InvalidDataException HexWallTacticMissing(string owner) =>
        new("automatic_Hex_wall_tactic." + owner);
}

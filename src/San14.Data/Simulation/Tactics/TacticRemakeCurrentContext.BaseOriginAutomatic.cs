using San14.Core;

namespace San14.Data;

internal sealed record BaseOriginAutomaticSelection(BaseReference Source, int SourceForce,
    int GovernorPhysical, int GovernorId, int SourceHex, AutomaticTacticBaseCandidateSource? Candidate,
    TacticAutomaticActivationTrial? Trial);

public sealed partial class TacticRemakeCurrentContext
{
    //17D6E0/17D9F0 ->16C9E0 ->16BF20 ->16B910. Base48 is the
    //single actual tactic slot; a governor supplies abilities, never an Army.
    internal BaseOriginAutomaticSelection? ResolveBaseOriginAutomaticCurrent(GameState state,
        BaseReference source, NormalCombatMixedPair pair, BattleRemakeCurrentRegionFactor region)
    {
        if (!Bound(state)) throw OriginAutomaticMissing("current_context");
        if (!OriginNeed(_definitions.ReadBaseNativeValidity(source), "source_validity")) return null;
        var fields = _definitions.ReadCaptureBaseThreatFields(source) ?? throw OriginAutomaticMissing("source_fields");
        byte slot = OriginNeed(fields.Byte48, "Base48");
        if (slot == 0) return null;
        ushort rawGovernor = OriginNeed(_definitions.ReadArmyReturnBaseGovernor2E(source), "Governor2E");
        int governor = OriginNeed((rawGovernor <= 6000 ? _definitions.ReadPersonLookupReference(rawGovernor)
            : _definitions.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Governor_pointer");
        if (!OriginNeed(_definitions.ReadPersonNativeValidity(governor), "Governor_validity")) return null;
        var position = PersonPositionRepair.DecodeSecondLocation(OriginNeed(
            _definitions.ReadPersonSecondLocation(governor), "GovernorE4"));
        ushort link = OriginNeed(_definitions.ReadBaseObjectLink(source), "Base4E");
        if (link <= 51 && position != new NativeLocation(5, link)) return null;
        if (link is >= 52 and <= 61 && position != new NativeLocation(6, link - 51)) return null;
        byte health = OriginNeed(OfficerHealthSourceAdapter.ReadByte(_definitions, governor), "Governor161");
        if (health is not (0 or 1)) return null;
        if (OriginNeed(fields.Word4C, "Base4C") != 0 || OriginNeed(fields.Byte49, "Base49") != 0) return null;
        if (!ForcedTargetMatches()) return null;
        //17D6E0 repeats this reached target gate after17D9F0.
        if (!ForcedTargetMatches()) return null;

        int force = OriginNeed(_definitions.ReadBaseForceSlot(source), "source_ForceB0");
        int governorId = OriginNeed(_definitions.ReadPersonIdentity(governor), "Governor10");
        int sourceHex = Hex(_definitions.ReadBaseCurrentHexIndex(source));
        var tactic = _definitions.ReadSkillTacticReference(slot <= 200 ? slot : 0)
            ?? throw OriginAutomaticMissing("Base48_tactic_pointer");
        if (!OriginNeed(_definitions.ReadSkillTacticValidity(tactic), "Base48_tactic_validity"))
            return new(source, force, governor, governorId, sourceHex, null, null);
        int tacticSlot = OriginNeed(_definitions.ReadSkillTacticPhysicalSlot(tactic), "Base48_tactic_identity");
        var row = _catalog.Rows[tacticSlot];
        var program = ReadCurrentProgram(state, row) ?? throw OriginAutomaticMissing("current_program");
        if (program.Instructions.Count != 2 || !program.Instructions.Select(i => i.Index).SequenceEqual([0, 1]))
            throw OriginAutomaticMissing("two_instruction_program");
        var vectors = program.Instructions.Select(instruction =>
            ResolveAutomaticInstructionHexTargets(state, force, row, instruction, pair.Actor.HexId, pair.Target.HexId)
                ?? throw OriginAutomaticMissing("instruction" + instruction.Index + "_targets")).ToArray();
        if (vectors.All(vector => vector.OrderedTargets.Length == 0))
            return new(source, force, governor, governorId, sourceHex, null, null);
        var candidate = new AutomaticTacticBaseCandidateSource(tacticSlot, program, vectors);
        byte category = OriginNeed(_definitions.ReadSkillTacticRawByte(tactic, 0x40), "tactic_category40");
        var hex = _definitions.ReadHexTableReference(sourceHex) ?? throw OriginAutomaticMissing("source_Hex");
        byte surfaceCode = OriginNeed(_definitions.ReadHexSurfaceCode(hex), "source_surface");
        var surface = _definitions.ReadSurfaceTypeTableReference(surfaceCode <= 30 ? surfaceCode : 0)
            ?? throw OriginAutomaticMissing("source_surface_pointer");
        ushort terrainRate = OriginNeed(_definitions.ReadSurfaceTypeTacticRateWord(surface, Math.Min(category, (byte)8)), "terrain_rate2E");
        int selector = OriginNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
        int difficulty = OriginNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28), "difficulty");
        var forcePointer = _definitions.ReadForceTableReference(force < 52 ? force : 0)
            ?? throw OriginAutomaticMissing("source_Force_pointer");
        if (!OriginNeed(_definitions.ReadForceNativeValidity(forcePointer), "source_Force_validity"))
            return new(source, force, governor, governorId, sourceHex, null, null);
        ushort ruler = OriginNeed(_definitions.ReadConditionForceRulerPersonId(new(OriginNeed(forcePointer.PhysicalSlot, "source_Force_physical"))), "source_Force_ruler");
        int rulerPhysical = OriginNeed((ruler <= 6000 ? _definitions.ReadPersonLookupReference(ruler)
            : _definitions.ReadTransferPersonTableReference(0))?.PhysicalSlot, "source_ruler_pointer");
        if (!OriginNeed(_definitions.ReadPersonNativeValidity(rulerPhysical), "source_ruler_validity"))
            return new(source, force, governor, governorId, sourceHex, null, null);
        var player = _definitions.ReadCurrentPlayerForce() ?? throw OriginAutomaticMissing("player_Force_pointer");
        bool own = forcePointer == player;
        string key = own ? difficulty switch { 0 => SimulationParameterKeys.Tactics.PlayerDifficulty0TrialRate,
            1 => SimulationParameterKeys.Tactics.PlayerDifficulty1TrialRate, _ => SimulationParameterKeys.Tactics.PlayerDifficulty2TrialRate }
            : difficulty >= 4 ? SimulationParameterKeys.Tactics.HighDifficultyNonplayerTrialRate : SimulationParameterKeys.Tactics.NonplayerAutomaticTrialRate;
        int baseline = OriginParameter(state, key);
        int rate = link <= 51 ? OriginStatic(state, "city-trial-rate")
            : link <= 61 ? OriginStatic(state, "gate-trial-rate") : 0;
        var factor = region.ResolveHexForceReceipt(state, force, checked((ushort)sourceHex))
            ?? throw OriginAutomaticMissing("source_Hex_Force_2B09B0");
        int targetHex = pair.Target.Kind == 27
            ? Hex(OriginNeed(_definitions.ReadArmyUnitRawHex2A(new(pair.Target.PhysicalSlot)), "target_Hex2A"))
            : Hex(_definitions.ReadBaseCurrentHexIndex(new(pair.Target.Kind == 5 ? BaseKind.City : BaseKind.Gate, pair.Target.PhysicalSlot)));
        var input = new TacticAutomaticActivationRateInput(checked((ushort)rate), baseline, terrainRate,
            factor.Factor, sourceHex, targetHex, 5);
        var trial = TacticAutomaticActivationSourceMath.Trial(input,
            TacticAutomaticActivationSourceMath.RequiresSample(TacticAutomaticActivationSourceMath.CalculateRate(input))
                ? ReadCurrentSamplerWorld(state) ?? throw OriginAutomaticMissing("trial_sampler") : null);
        return new(source, force, governor, governorId, sourceHex, trial.Accepted ? candidate : null, trial);

        bool ForcedTargetMatches()
        {
            var reached = _definitions.ReadCaptureBaseThreatFields(source) ?? throw OriginAutomaticMissing("fresh_forced_target");
            return OriginNeed(reached.Byte4B, "Base4B") == 0 ||
                OriginNeed(reached.Byte62, "Base62") == pair.Target.Kind && OriginNeed(reached.Word64, "Base64") == pair.Target.PhysicalSlot;
        }
        int Hex(int raw) => OriginNeed((_definitions.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0))?.PhysicalSlot, "Hex_physical");
    }

    internal static int OriginStatic(GameState state, string name)
        => BaseAutomaticRuleCatalog.Read(state, name);
    internal static int OriginParameter(GameState state, string key)
    {
        return AutomaticTacticDamageCurrentParameters.ReadCurrent(state, key)
            ?? throw OriginAutomaticMissing(key);
    }
    internal static T OriginNeed<T>(T? value, string owner) where T : struct => value ?? throw OriginAutomaticMissing(owner);
    internal static InvalidDataException OriginAutomaticMissing(string owner) => new("automatic_Base_origin." + owner);
}

using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState? ApplyOrdinaryMarchDevelopment(GameState state, SourceWorldContext current, UnitState army)
    {
        bool? valid = Context(current).ArmyValid(state, army);
        if (valid is null) return null;
        return valid.Value ? ArmyMarchDevelopmentSource.TryApply(state, current, army, _normalAftermathInputs) : state;
    }
}

/// <summary>TC29DB90's current-Hex development loss. This is not territory
/// capture: only the three progress setters and their level notifications run.</summary>
internal static class ArmyMarchDevelopmentSource
{
    internal static GameState? TryApply(GameState state, SourceWorldContext current, UnitState army,
        NormalArmyAftermathInputs inputs)
    {
        if (current.ReadForceTableReference(army.ForceId) is not { } own ||
            current.ReadForceNativeValidity(own) is not bool ownValid) return null;
        if (!ownValid) return state;
        int effect;
        try
        {
            //These movement consumers have no unpublished Person credit/status
            //writer. Its operation-local scratch is created and released by
            //the existing query; it writes no persistent domain index.
            effect = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, own, 22, true).Value;
        }
        catch (InvalidDataException) { return null; }
        if (effect < 1) return state;
        if (ArmyMarchSourceAdapter.ReadCurrentHex(state, army) is not ushort position ||
            current.ReadHexTableReference(position < ArmyMarchSourceAdapter.SourceHexCount ? position : 0) is not { } hex) return null;
        if (hex.PhysicalSlot is null) return state;
        if (current.ReadHexAreaSlot(hex) is not ushort areaId ||
            current.ReadAreaTableReference(areaId <= 500 ? areaId : 0) is not { } area ||
            current.ReadAreaNativeValidity(area) is not bool areaValid) return null;
        if (!areaValid) return state;
        if (current.ReadAreaCurrentForceSlot(area) is not byte ownerId ||
            current.ReadForceTableReference(ownerId <= 51 ? ownerId : 0) is not { } owner ||
            current.ReadForceNativeValidity(owner) is not bool ownerValid) return null;
        if (!ownerValid) return state;
        bool? friendly = ReadFriendlyRelation(state, own, owner);
        if (friendly is null) return null;
        if (friendly.Value) return state;
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            !parameters.Int32Values.TryGetValue(SimulationParameterKeys.ArmyMarch.DevelopmentLossMultiplier, out int multiplier) ||
            parameters.Float32Values.ContainsKey(SimulationParameterKeys.ArmyMarch.DevelopmentLossMultiplier)) return null;
        var writer = new OwnedAreaDevelopmentContext(state, current, inputs);
        foreach (var field in new[] { CeAreaDevelopmentField.Commerce, CeAreaDevelopmentField.Agriculture, CeAreaDevelopmentField.Barracks })
        {
            if (writer.ReadAreaProgress(area, field) is not ushort before) return null;
            //Native low32 products are reinterpreted signed before IDIV100.
            int after = Math.Max(0, unchecked(before - unchecked(multiplier * before * effect) / 100));
            try { inputs.Development.WriteCurrentProgress(area, field, after, 1, writer); }
            catch (InvalidDataException) { return null; }
        }
        return writer.State;
    }

    /// <summary>2C3CA0, including the selected-force2C4170 guard. This is
    /// distinct from the asymmetric2CA650 hostility gate used by path costs.</summary>
    internal static bool? ReadFriendlyRelation(GameState state, ForceReference first, ForceReference second)
    {
        if (first == second) return true;
        if (first.PhysicalSlot is not int a || second.PhysicalSlot is not int b ||
            state.NativeArmySimulation is not { } simulation ||
            simulation.World.SelectedForce1656 is not byte selected) return null;
        var left = simulation.Forces.GetValueOrDefault(a);
        var right = simulation.Forces.GetValueOrDefault(b);
        if (selected == a)
        {
            if (right?.Flags12 is not byte rightFlags) return null;
            if ((rightFlags & 2) != 0) return false;
        }
        if (left?.Flags12 is not byte leftFlags) return null;
        if ((leftFlags & 2) != 0 && selected == b) return false;
        if (left.RelationsEA is not { Length: 52 } relation) return null;
        if ((relation[b] & 0x7F) != 0) return true;
        if ((leftFlags & 2) != 0)
        {
            if (right?.Flags12 is not byte rightFlags) return null;
            if ((rightFlags & 2) != 0) return true;
        }
        if (left.Flags194 is not byte leftPartner) return null;
        if (leftPartner == b) return true;
        return right?.Flags194 is byte rightPartner ? rightPartner == a : null;
    }
}

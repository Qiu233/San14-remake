using San14.Core;

namespace San14.Data;

internal static class DeploymentDraftMarchSource
{
    internal sealed class Motion(GameState state, SourceWorldContext current,
        DeploymentDraftMarchInputs input) : ArmyRouteSearch.Movement
    {
        private bool? wallPermission;
        internal override bool AvoidHostile => false;
        internal override byte LandFormation => input.LandFormation;
        internal override byte NavalFormation => input.NavalFormation;
        internal override int Force => input.ForcePhysicalSlot;
        internal override int HomeHex => input.HomeHex;
        internal override bool WallPermission => wallPermission ??= !Need(TacticRemakeCurrentTraits.CommanderImmune(current,
            input.CommanderPhysicalSlot, 102), "commander_wall_permission");
        internal override byte? Action => 0;
        internal override bool HasNoDestination => true;
        internal override float ReadMobility(int formation, byte explicitSurface)
        {
            var result = ArmyRouteMobilityEvaluation.Read(state, current, MobilityInput(input.HomeHex, false), formation, explicitSurface);
            return result.Value ?? throw new InvalidDataException(result.UnresolvedOwner);
        }
        internal float EtaMobility(int formation, int previousHex)
        {
            var result = ArmyRouteMobilityEvaluation.Read(state, current, MobilityInput(previousHex, true), formation, null);
            return result.Value ?? throw new InvalidDataException(result.UnresolvedOwner);
        }
        private ArmyRouteMobilityInputs MobilityInput(int hex, bool conditions) => new(input.CommanderPhysicalSlot,
            input.ForcePhysicalSlot, 1, hex, input.LandFormation, input.NavalFormation, input.Troops,
            conditions, () => (byte)0);
    }

    internal static int[] Search(GameState state, SourceWorldContext current, Motion motion,
        int startHex, int goalHex, out int expanded)
    {
        expanded = 0;
        var goal = current.ReadHexTableReference(goalHex) ?? throw Missing("goal_hex_pointer");
        // Every nonnull CHex, including physical0, has true native validity.
        if (!goal.PhysicalSlot.HasValue) return [];
        byte terrainId = Need(current.ReadHexSurfaceCode(goal), "goal_surface");
        var terrain = current.ReadSurfaceTypeTableReference(terrainId <= 30 ? terrainId : 0)
            ?? throw Missing("goal_surface_pointer");
        if (Need(current.ReadSurfaceTypeRawField2A(terrain), "goal_surface_raw_cost") == 0) return [];
        // The reset preview action/kind are distinct from the final Move order.
        return ArmyRouteSearch.Search(new ArmyRouteSearch.Queries(state, current, motion,
            startHex, goalHex, byte.MaxValue, 0, null), out expanded);
    }

    internal static int Eta(GameState state, SourceWorldContext current, Motion motion,
        IReadOnlyList<int> etaPath) => Eta(state, current, motion.LandFormation,
            motion.NavalFormation, motion.EtaMobility, etaPath);

    //266860 is also called with353FE0's transport reset descriptor. The
    //caller supplies that descriptor's formations and actual2B0660 inputs.
    internal static int Eta(GameState state, SourceWorldContext current,
        byte landFormation, byte navalFormation, Func<int, int, float> readMobility,
        IReadOnlyList<int> etaPath)
    {
        float total = 0;
        for (int index = 1; index < etaPath.Count; index++)
        {
            int arrival = etaPath[index], previous = etaPath[index - 1];
            var hex = current.ReadHexTableReference(arrival) ?? throw Missing("eta_hex_pointer");
            byte code = Need(current.ReadHexSurfaceCode(hex), "eta_surface");
            var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw Missing("eta_surface_pointer");
            int physical = terrain.PhysicalSlot ?? throw Missing("eta_surface_receiver");
            int cost = ArmyRouteTerrainCost.Read(state, current, terrain);
            byte flags = Need(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, physical), "eta_surface_flags");
            int formation = (flags & 2) != 0 ? navalFormation : landFormation;
            float rawMobility = readMobility(formation, previous);
            if (!float.IsFinite(rawMobility) || rawMobility < int.MinValue || rawMobility >= 2147483648f)
                throw Missing("eta_cvttss2si_boundary");
            int mobility = BattleUnitSourceMath.TruncateSingleToInt32(rawMobility);
            if (mobility == 0 || mobility == -1 && cost == int.MinValue) throw Missing("eta_signed_division_boundary");
            int quotient = cost / mobility;
            total = total + (float)quotient;
        }
        float days = total / 12f;
        if (!float.IsFinite(days) || days < int.MinValue || days >= 2147483648f)
            throw Missing("eta_days_cvttss2si_boundary");
        return BattleUnitSourceMath.TruncateSingleToInt32(days);
    }
    private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("deployment.march." + name);
}

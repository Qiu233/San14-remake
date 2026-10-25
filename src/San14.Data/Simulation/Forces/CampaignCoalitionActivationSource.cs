using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>288FC0 activates the coalition in the borrowed current world.
    /// Member order, the optional leading leader and pairwise relation writes
    /// precede every member's adjustment toward the target.</summary>
    internal GameState ApplyNormalTurnCoalitionActivationCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference target,
        ForceReference leader, IReadOnlyList<int> orderedMemberForceIds,
        List<CampaignEffectRequest> effects)
    {
        if (!CoalitionActivationNeed(current.ReadForceNativeValidity(target), "target_validity") ||
            !CoalitionActivationNeed(current.ReadForceNativeValidity(leader), "leader_validity")) return state;
        int targetId = CoalitionActivationNeed(current.ReadForcePhysicalSlot(target), "target_identity");
        int leaderId = CoalitionActivationNeed(current.ReadForcePhysicalSlot(leader), "leader_identity");
        var native = state.NativeArmySimulation ?? throw CoalitionActivationMissing("native_owner");
        var interaction = state.NativeForceUnregisterCurrent ?? throw CoalitionActivationMissing("World1657_owner");
        state = state with
        {
            NativeArmySimulation = native with { World = native.World with
                { SelectedForce1656 = (byte)Math.Clamp(targetId, 0, 51), Flags165C = 24 } },
            NativeForceUnregisterCurrent = interaction with { OtherForce1657 = (byte)Math.Clamp(leaderId, 0, 51) }
        };
        var members = new List<ForceReference>(orderedMemberForceIds.Count + 1);
        if (!orderedMemberForceIds.Contains(leaderId)) members.Add(leader);
        foreach (int id in orderedMemberForceIds) members.Add(new(id));
        foreach (var member in members)
        {
            int id = CoalitionActivationNeed(member.PhysicalSlot, "member_identity");
            native = state.NativeArmySimulation ?? throw CoalitionActivationMissing("member_flags_owner");
            var force = native.Forces.GetValueOrDefault(id) ?? throw CoalitionActivationMissing("member_Force_owner");
            byte flags = CoalitionActivationNeed(force.Flags12, "member_Force12");
            state = state with { NativeArmySimulation = native with
                { Forces = new(native.Forces) { [id] = force with { Flags12 = (byte)((flags & ~4) | 2) } } } };
        }
        //TC cached typed/data.tsv141A4AC3C=20 and141A4AC48=-100.
        //The original effect8 output marker records an owned matching State,
        //including a matching effect whose magnitude is zero.
        for (int first = 0; first < members.Count; first++)
        {
            var a = members[first];
            for (int second = first + 1; second < members.Count; second++)
            {
                var b = members[second];
                int before = Relation(a, b);
                var left = PersonReturnPathSource.ReadForceAdvantage(state, current, a, 8, defaultValue: 100);
                int leftValue = left.Value ?? throw CoalitionActivationMissing(left.UnresolvedOwner ?? "first_effect8");
                var right = PersonReturnPathSource.ReadForceAdvantage(state, current, b, 8, defaultValue: 100);
                int rightValue = right.Value ?? throw CoalitionActivationMissing(right.UnresolvedOwner ?? "second_effect8");
                int effect = left.QualifiedStates.Length != 0 ? leftValue : rightValue;
                int after = Math.Clamp(unchecked(unchecked(effect * 20) / 100 + before), 20, 200);
                state = ApplyNormalTurnForceRelationCurrent(state, current, pool, a, b, unchecked(after - before));
            }
        }
        foreach (var member in members)
        {
            int before = Relation(member, target);
            int after = Math.Clamp(unchecked(before - 100), 0, 200);
            state = ApplyNormalTurnForceRelationCurrent(state, current, pool, member, target, unchecked(after - before));
        }
        //1A3E60 refreshes the City views' two derived relation indicators.
        //The presentation owner consumes this intent only when it refreshes.
        effects.Add(new(null, null) { ForceCoalitionVisual = new(
            ForceCoalitionVisualOperation.CityProvinceAdvantageIndicatorsRefresh, null) });
        return state;

        int Relation(ForceReference first, ForceReference second)
        {
            int other = CoalitionActivationNeed(current.ReadForcePhysicalSlot(second), "relation_other_identity");
            return other < 52 ? DRelations(state,
                CoalitionActivationNeed(first.PhysicalSlot, "relation_owner"))[other] : 0;
        }
    }

    private static T CoalitionActivationNeed<T>(T? value, string field) where T : struct
        => value ?? throw CoalitionActivationMissing(field);
    private static InvalidDataException CoalitionActivationMissing(string field)
        => new("normal.turn.coalition.activation." + field);
}

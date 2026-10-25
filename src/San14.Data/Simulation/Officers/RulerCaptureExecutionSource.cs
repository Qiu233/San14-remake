using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //Reuse the one saved coefficient already owned by destruction rules.
    //The TC offline full-block reference/escape audit established loader-zero
    //lifetime (one read, no writer/address escape), independently of samples.
    internal const string RulerExecutionRelationCurrentKey = SimulationParameterKeys.ArmyDestruction.RulerExecutionRelationPenalty;
    internal const string RulerExecutionPopularityKey = "simulation.ruler-capture.execution-popularity-penalty";

    internal GameState ApplyRulerCaptureExecutionCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, RulerCaptureDecisionResult decision, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects)
        => new RulerCaptureExecutionSource(this).ApplyExecute(state, current, pool, decision, date, effects);

    /// <summary>2F86F0's actual execution branch. Decision sampling belongs to
    /// the caller; this operation shares its current world and pool and never
    /// captures/exports another world or substitutes release/captivity.</summary>
    internal sealed class RulerCaptureExecutionSource(ArmyCampaignRuleAdapter package)
    {
        internal GameState ApplyExecute(GameState state, SourceWorldContext current, WorldIndexContext pool,
            RulerCaptureDecisionResult decision, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("current_pool_binding");
            if (decision.Decision != RulerCaptureDecision.ExecuteRequired || decision.Receipt is not { } receipt)
                throw Missing(decision.UnresolvedOwner ?? "execution_decision");
            int person = receipt.PersonPhysical;
            var winner = current.ReadForceTableReference(receipt.WinnerForcePhysical)
                ?? throw Missing("winner_Force_pointer");
            var original = current.ReadPersonForce(person) ?? throw Missing("original_Force22CB40");
            //2F8DFB passes notify1/fifth0, including the same-source special
            //personnel tail. The existing monthly owner keeps these reports in
            //their actual MonthlyMaintenance source, rather than forging them
            //as battle or Person-death payloads in the caller's feedback list.
            int penalty = CurrentRelationPenalty(state);
            state = new MonthlyCampaignMaintenanceSource(package, state, current, pool)
                .ApplyRelation(winner, original, unchecked(-penalty), 1);
            state = package.ApplyRulerExecutionConsequencesCurrent(state, current, person, winner);
            if (Need(current.ReadForceNativeValidity(winner), "popularity_Force_validity"))
            {
                int force = Need(current.ReadForcePhysicalSlot(winner), "popularity_Force60");
                //1EF590 is exactly Force51 or46..50, not a host/UI service.
                if (force != 0 && force != 51 && force is not (>= 46 and <= 50))
                    state = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(state, current,
                        force, unchecked(-PopularityPenalty(state)));
            }
            var killer = new TransferPersonReference(receipt.WinnerRulerPhysical);
            //TC2F8EAA calls295EE0(Person,killer,2,1,1,0,0,1,0).
            //It is the common full death operation: associated Army teardown,
            //ruler succession/Force retirement, items, relationships, reports
            //and final unregister. Its native early return1 is retained there;
            //2F86F0 itself returns1 after this call in either case.
            var death = new PersonDeathSource(package, state, current, pool)
                .ApplyCombatDeath(person, killer, 2, true, true, true, false);
            return death.State;
        }

        private int CurrentRelationPenalty(GameState state)
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("relation_parameter_owner");
            //Both current CE callers consume this semantic coefficient. It
            //is an owned mutable rule input, not a static value inferred from
            //an image's zero-filled section. Missing saved knowledge stays missing.
            return !parameters.Float32Values.ContainsKey(RulerExecutionRelationCurrentKey) &&
                parameters.Int32Values.TryGetValue(RulerExecutionRelationCurrentKey, out int value)
                ? value : throw Missing("current_parameter_" + RulerExecutionRelationCurrentKey);
        }
        private int PopularityPenalty(GameState state)
        {
            var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("popularity_parameter_owner");
            if (parameters.Float32Values.ContainsKey(RulerExecutionPopularityKey))
                throw Missing("integer_parameter_" + RulerExecutionPopularityKey);
            if (parameters.Int32Values.TryGetValue(RulerExecutionPopularityKey, out int value)) return value;
            //The paired current CE data sources independently contain20:
            //TC1A49904 and SC1A4A934. Runtime consumes this project rule only;
            //it never opens either executable or manufactures a current key.
            if (parameters.Language is "SC" or "TC" && parameters.RuleVersion == SimulationRuleIdentity.Version) return 20;
            throw Missing("rule_catalog_" + RulerExecutionPopularityKey);
        }
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("ruler-capture.execution." + field);
    }
}

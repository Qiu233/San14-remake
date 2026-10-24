using San14.Core;

namespace San14.Data;

internal sealed record ArmyNeighborCurrentFactorResult(float? Factor,
    ArmyChainCurrentCandidates Candidates, int? SettingsCoefficient,
    int? PolicyContribution, string? UnresolvedOwner);

/// <summary>The existing Army Common's neighbor term. The actual local
/// candidate list and current settings/Force owners remain separate inputs;
/// this reader neither resolves a waiting chain nor changes any saved state.</summary>
internal static class ArmyNeighborCurrentFactor
{
    internal static ArmyNeighborCurrentFactorResult Read(GameState state,
        SourceWorldContext current, UnitState actor)
    {
        var candidates = ArmyChainCurrentCandidates.Read(state, current, actor);
        if (candidates.Count is not int count)
            return new(null, candidates, null, null, candidates.UnresolvedOwner);
        // These pure coefficients cannot change an exact int32 product0.
        // No missing setting or policy is replaced by a guessed value here.
        if (count == 0) return new(1f, candidates, null, null, null);
        int? coefficient = null, policy = null;
        try
        {
            coefficient = K(BattleRemakeCurrentSettings.ReadNeighborCoefficient(state), "settings_coefficient");
            //22CD80: commander raw District -> current District validity ->
            //raw Force. The final raw value is a lookup, never a physical ID.
            var person = (actor.OfficerId is >= 0 and <= 6000 ? current.ReadPersonLookupReference(actor.OfficerId)
                : current.ReadTransferPersonTableReference(0)) ?? throw Missing("commander_pointer");
            byte districtLookup = person.PhysicalSlot is int personPhysical &&
                K(current.ReadPersonNativeValidity(personPhysical), "commander_validity")
                ? K(current.ReadPersonDistrictSlot(personPhysical), "commander_district") : (byte)0;
            var district = current.ReadDistrictTableReference(districtLookup <= 51 ? districtLookup : 0)
                ?? throw Missing("district_pointer");
            byte forceLookup = K(current.ReadDistrictNativeValidity(district), "district_validity")
                ? K(current.ReadDistrictForceSlot(district), "district_force") : (byte)0;
            var force = current.ReadForceTableReference(forceLookup <= 51 ? forceLookup : 0)
                ?? throw Missing("force_pointer");
            //22C250(effect38) returns0 for an invalid Force; a valid Force
            //uses the existing active/suppressed current policy-panel reader.
            policy = K(current.ReadForceNativeValidity(force), "force_validity")
                ? K(current.BaseForceModifierReader?.Invoke(force, 38), "active_policy38") : 0;
            return new(Calculate(count, coefficient.Value, policy.Value), candidates, coefficient, policy, null);
        }
        catch (InvalidDataException error) { return new(null, candidates, coefficient, policy, error.Message); }
    }

    /// <summary>Native signed32 ADD/IMUL precede the conversion to float32.
    /// There is no count cap, percentage clamp or final factor clamp.</summary>
    internal static float Calculate(int count, int settingsCoefficient, int policyContribution)
    {
        int coefficient = unchecked(settingsCoefficient + policyContribution);
        int product = unchecked(coefficient * count);
        float factor = product;
        factor /= 100f;
        factor += 1f;
        return factor;
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.army_neighbor." + field);
}

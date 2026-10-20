using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>7F7F90's ordered, at-most-six land source candidates. Reads
    /// current saved knowledge once; never edits or normalizes the draft.</summary>
    public DeploymentLandFormationChoices QueryDeploymentLandFormationChoices(GameState state, int officerId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            if (!state.Officers.ContainsKey(officerId) || _definitions.ResolveOfficerSource(officerId) is not int physical)
                throw LandChoiceMissing("current_person_owner");
            var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
            var p = state.NativeArmySimulation?.Parameters;
            if (p is null || p.Language != Seed.Language || p.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                throw LandChoiceMissing("rule_profile");
            var result = new List<DeploymentLandFormationChoice>(); uint? mask = null; int? personId = null;
            //The installed complete native21-slot Formation table has stable
            //physical identity: native v3 rejects only its default receiver0.
            for (int formation = 1; formation < 21; formation++)
            {
                byte kind = TacticRemakeCurrentFormations.ReadRawByte(state, formation, 0x7E)
                    ?? throw LandChoiceMissing("current_formation_kind");
                if (kind == 2) continue;
                mask ??= current.ReadPersonFormationMask164(physical) ?? throw LandChoiceMissing("current_formation_capabilities");
                if ((mask.Value & (1u << formation)) == 0) continue;
                //Only the14 branch consumes Person.word10. Other rows do not
                //demand unrelated identity knowledge to prove eligibility.
                int identity = officerId;
                if (formation == 14)
                {
                    personId ??= current.ReadPersonIdentity(physical) ?? throw LandChoiceMissing("current_person_identity");
                    identity = personId.Value;
                }
                if (!DeploymentFormationEligibility.Allows(mask.Value, formation, identity)) continue;
                var definition = state.Formations.GetValueOrDefault(formation) ?? throw LandChoiceMissing("current_formation_name");
                result.Add(new(formation, definition.Name, kind, new(LandChoiceIcon(formation), 25), true));
                if (result.Count == 6) break;
            }
            return new(result.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    //8AAA50: ordinary selector icon, deliberately distinct from fuchi8AA980.
    private static int LandChoiceIcon(int formation) => formation switch
    {
        2 => 0x1A8, 3 => 0x1A9, 4 => 0x1AA, 5 => 0x1AC, 6 => 0x1AD,
        7 => 0x1AB, 8 => 0x1AE, 9 => 0x1AF, 10 => 0x1A3, 11 => 0x1A4,
        12 => 0x1A5, 13 => 0x1A6, 14 => 0x3EF, >= 17 and <= 20 => 0x1A7,
        _ => 0x1A2
    };
    private static InvalidDataException LandChoiceMissing(string field) => new("deployment.land_choices." + field);
}

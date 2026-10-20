using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // Offline extracted CE SC/TC rule data, compared with the current byte44.
    private const int DeploymentLowMoraleWarningThreshold = 40;

    /// <summary>All ordinary kind0 rows in their original order, regardless
    /// of selection. The current domain supports retreat raw0/raw1/unknown;
    /// no other packet states are inferred. This does not confirm an order.</summary>
    public DeploymentDraftConfirmation QueryDeploymentDraftConfirmation(GameState state,
        IReadOnlyList<DeploymentDraftRow> rows)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rows);
        SourceWorldContext? current = null;
        foreach (var row in rows)
        {
            int id = row.Request.OfficerId;
            try
            {
                var behavior = row.Request.Behavior ?? throw ConfirmationMissing("allow_retreat");
                // Read only this setting, not the unrelated preference fields.
                int retreatRaw = behavior.AllowRetreat ? 1 : 0;
                if (retreatRaw != 1) continue;
                if (current is null)
                {
                    current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
                    OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
                }
                if (id < 0) throw ConfirmationMissing("negative_person_identity");
                var person = current.ReadPersonLookupReference(id <= 6000 ? id : 0)
                    ?? throw ConfirmationMissing("person_pointer");
                int physical = person.PhysicalSlot ?? throw ConfirmationMissing("person_receiver");
                if (!ConfirmationNeed(current.ReadPersonNativeValidity(physical), "person_validity"))
                    throw ConfirmationMissing("invalid_person");
                var location = PersonPositionRepair.DecodeSecondLocation(
                    ConfirmationNeed(current.ReadPersonSecondLocation(physical), "person_second_location"));
                BaseReference? home = location.Kind switch
                {
                    5 => current.ReadCityTableReference(location.Index < 52 ? location.Index : 0),
                    6 => current.ReadGateTableReference(location.Index < 11 ? location.Index : 0),
                    _ => null
                };
                if (home is null) throw ConfirmationMissing("current_home_base");
                if (!ConfirmationNeed(current.ReadBaseNativeValidity(home), "home_base_validity"))
                    throw ConfirmationMissing("invalid_home_base");
                byte morale = ConfirmationNeed(current.ReadBaseMorale44(home), "home_current_morale");
                if (morale < DeploymentLowMoraleWarningThreshold)
                    return new(DeploymentDraftConfirmationKind.LowMoraleWarning, id, null);
            }
            catch (InvalidDataException error)
            { return new(DeploymentDraftConfirmationKind.Unknown, id, error.Message); }
        }
        return new(DeploymentDraftConfirmationKind.Normal, null, null);
    }
    private static T ConfirmationNeed<T>(T? value, string field) where T : struct => value ?? throw ConfirmationMissing(field);
    private static InvalidDataException ConfirmationMissing(string field) => new("deployment.confirmation." + field);
}

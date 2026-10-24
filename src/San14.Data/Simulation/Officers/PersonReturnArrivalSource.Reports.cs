using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal PersonReturnArrivalResult ApplyPersonReturnArrivalCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int receiverIdentity, BaseReference target,
        IArmyReturnGovernorCurrentQueries queries, bool borrowedCurrent, NativeMapSeasonDate? operationDate = null)
    {
        var effects = new List<CampaignEffectRequest>();
        var result = PersonReturnArrivalSource.ApplyWithReportsCurrent(state, current, pool, receiverIdentity,
            target, queries, borrowedCurrent, (working, person, assignment) =>
                ApplyPersonAssociationRemovalReports(working, current, person, assignment,
                    () => (ReadArmyDestructionSampler(working, current)
                        ?? throw new InvalidDataException("person-return.arrival_report_World450")).Day450, effects), this, operationDate);
        if (result.State is not { } completed || effects.Count == 0) return result;
        return result with { State = completed with
        {
            CampaignEffectRequests = (completed.CampaignEffectRequests
                ?? throw new InvalidDataException("person-return.arrival_report_effect_owner")).Append(
                    CampaignEffectSource.ForceAssociationRemoval,
                    completed.NativeMapSeason?.AdvancedCalendarSteps
                        ?? throw new InvalidDataException("person-return.arrival_report_calendar"), effects.ToArray())
        } };
    }

    internal int ReadReturnDistrictLeadership(GameState state, SourceWorldContext current, int person)
    {
        if (current.ReadPersonNativeValidity(person) is not bool valid)
            throw new InvalidDataException("person-return.district.leadership_validity");
        if (!valid) return 0;
        int id = current.ReadPersonIdentity(person) ?? throw new InvalidDataException("person-return.district.leadership_ID");
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        return abilities.ResolveDimension(state, id, 0, (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect))
            ?? throw new InvalidDataException("person-return.district.current_leadership");
    }
}

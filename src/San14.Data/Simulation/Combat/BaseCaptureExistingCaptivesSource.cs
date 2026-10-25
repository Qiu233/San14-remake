using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2851B0's230DD0 list. A valid winner releases only its own people;
    //neutralization restores an affiliated captive through actual260310.
    //Other inherited foreign captives keep their complete custody unchanged.
    internal GameState ApplyBaseCaptureExistingCaptiveCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference holder,
        int personPhysical, NativeMapSeasonDate? sourceDate, List<CampaignEffectRequest> effects,
        ForceReference? winningForce = null, DistrictReference? winningDistrict = null)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw ExistingCaptiveMissing("current_pool_binding");
        var working = state;
        TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(working, current)
            ?? throw ExistingCaptiveMissing("World450_45C");
        int Day() => Sampler().Day450;
        var winner = winningForce ?? current.ReadForceTableReference(0) ?? throw ExistingCaptiveMissing("winner_Force0");
        bool winnerValid = ExistingCaptiveNeed(current.ReadForceNativeValidity(winner), "winner_Force_validity");
        var force = current.ReadPersonForce(personPhysical) ?? throw ExistingCaptiveMissing("original_Force");
        if (winnerValid)
        {
            //A different valid winning Force inherits the existing captivity.
            //There is no release or capture reroll for this list entry.
            if (force.PhysicalSlot != winner.PhysicalSlot) return working;
        }
        else if (!ExistingCaptiveNeed(current.ReadForceNativeValidity(force), "original_Force_validity"))
            return ApplyCaptiveFreedomCurrent(working, current, pool, personPhysical, sourceDate, Day, Sampler, effects);

        var holderDistrict = current.ReadBaseDistrictReference(holder) ?? throw ExistingCaptiveMissing("holder_District");
        //241D30 is an E6-only store (TC241D30..241D4C). It does not repair
        //E2/E4 or create a journey;260310 owns its real busy predicate.
        current.WritePersonStatus(personPhysical, 4);
        int id = ExistingCaptiveNeed(current.ReadPersonIdentity(personPhysical), "Person10");
        var person = working.Officers.GetValueOrDefault(id) ?? throw ExistingCaptiveMissing("Person_owner");
        var status = OfficerServiceStatusSourceAdapter.Decode(4);
        working = working with { Officers = new(working.Officers) { [id] = person with
            { ServiceStatus = status, Role = OfficerServiceRules.PublishedRole(status, person.Role) } } };
        if (!winnerValid)
        {
            var operation = CreateCaptureDistrictOperationQueries(current, () => working, next => working = next,
                sourceDate, effects);
            var home = CreateCaptureReturningPersonRules().TryApplyReleasedCaptiveCurrent(working, current,
                personPhysical, sourceDate, operation, effects);
            working = home.State ?? throw ExistingCaptiveMissing(home.UnresolvedOwner ?? "260310");
            working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
            working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current,
                operation.ChangedAreas, operation.ChangedGates);
        }
        return ApplyBaseExistingCaptiveReportsCurrent(working, current, holder, holderDistrict,
            personPhysical, Day, effects, winnerValid ? winner : null, winningDistrict);
    }

    //This2851B0 report follows260310. Unlike2E7890 it does not require a
    //valid holder District when the released Person's current Force is player.
    private GameState ApplyBaseExistingCaptiveReportsCurrent(GameState state,
        SourceWorldContext current, BaseReference holder, DistrictReference holderDistrict,
        int person, Func<int> day, List<CampaignEffectRequest> effects,
        ForceReference? winningForce, DistrictReference? winningDistrict)
    {
        bool winnerPlayer = winningForce is not null && TalentForceIsPlayerCurrent(state, current, winningForce);
        bool ownPlayer = false;
        if (winningForce is null)
        {
            var own = current.ReadPersonForce(person) ?? throw ExistingCaptiveMissing("report_Person_Force");
            ownPlayer = ExistingCaptiveNeed(current.ReadForceNativeValidity(own), "report_Person_Force_validity") &&
                TalentForceIsPlayerCurrent(state, current, own);
        }
        if (!(winnerPlayer || ownPlayer) && !PlayerDistrict(holderDistrict)) return state;
        int id = ExistingCaptiveNeed(current.ReadPersonIdentity(person), "report_Person10");
        ushort location = ExistingCaptiveNeed(current.ReadBaseObjectLink(holder), "report_Base4E");
        var main = FormatCaptiveReleaseReport(state, current, 0x3240, new(Person0: 5012, Person1: id, Base0: location));
        state = main.State;
        var detail = FormatCaptiveReleaseReport(state, current, 0x53A7, new(Person0: 5012, Person1: id, Base0: location));
        state = detail.State;
        var selected = winningForce is not null && winnerPlayer
            ? winningDistrict ?? throw ExistingCaptiveMissing("report_winning_District_operand") : holderDistrict;
        if (winningForce is null && !PlayerDistrict(selected))
        {
            int raw = ExistingCaptiveNeed(current.ReadPersonDistrictSlot(person), "report_PersonE0");
            selected = current.ReadDistrictTableReference(raw < 52 ? raw : 0) ?? throw ExistingCaptiveMissing("report_Person_District");
            if (!PlayerDistrict(selected))
                selected = ForcePrimaryDistrictSource.Resolve(current, OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                    ?? throw ExistingCaptiveMissing("report_player_Force"));
        }
        int district = ExistingCaptiveNeed(current.ReadDistrictPhysicalSlot(selected), "report_District22DD20");
        var hex = CurrentGateMapQueries.FindHex(holder, current);
        int hexId = ExistingCaptiveNeed(current.ReadHexPhysicalSlot(hex), "report_Hex22DEF0");
        //9354F0 category1, payload5012; marks0=1,3/4/5/6=0.
        return WriteHistory(state, current, new(1, district, hexId, day(), main.Text, detail.Text, 5012,
            MarkBit0: 1), effects);

        bool PlayerDistrict(DistrictReference district)
        {
            if (!ExistingCaptiveNeed(current.ReadDistrictNativeValidity(district), "report_District_validity")) return false;
            return BaseCaptureRewardSource.ReadPlayerDistrict(current, district, ExistingCaptiveMissing);
        }
    }
    private static T ExistingCaptiveNeed<T>(T? value, string field) where T : struct => value ?? throw ExistingCaptiveMissing(field);
    private static InvalidDataException ExistingCaptiveMissing(string field) => new("Base-capture.existing-captive." + field);
}

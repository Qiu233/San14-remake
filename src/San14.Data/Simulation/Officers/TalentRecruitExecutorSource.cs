using San14.Core;

namespace San14.Data;

internal sealed record TalentRecruitExecutorCandidate(int OfficerId, int PhysicalSlot, ushort HomeLocationId);

public sealed partial class ArmyCampaignRuleAdapter
{
    //6DC4E0 borrows the real District mode0 iterator. It neither sorts this
    //registry sequence nor performs the later command-admission checks.
    private static IReadOnlyList<TalentRecruitExecutorCandidate> ReadTalentRecruitExecutorsCurrent(
        SourceWorldContext current, int districtPhysical)
    {
        var registered = current.ReadRegisteredPersonPhysicalSlots() ?? throw RecruitExecutorMissing("Person_registry100");
        int anchor = Need((current.ReadDistrictTableReference(0)
            ?? throw RecruitExecutorMissing("District_anchor")).PhysicalSlot, "District_anchor_receiver");
        int ordinal = districtPhysical - anchor;
        var result = new List<TalentRecruitExecutorCandidate>();
        if (ordinal is < 1 or > 51) return result.AsReadOnly();
        foreach (int? row in registered)
        {
            int person = RecruitExecutorNeed(row, "Person_payload");
            if (ReadTalentRecruitExecutorCurrent(current, person, ordinal) is { } candidate) result.Add(candidate);
        }
        return result.AsReadOnly();

        static T Need<T>(T? value, string field) where T : struct => value ?? throw RecruitExecutorMissing(field);
    }

    private static TalentRecruitExecutorCandidate? ReadTalentRecruitExecutorCurrent(SourceWorldContext current, int person, int ordinal)
    {
        if (ordinal is < 1 or > 51 || RecruitExecutorNeed(current.ReadPersonDistrictSlot(person), "PersonE0") != ordinal ||
            RecruitExecutorNeed(current.ReadPersonStatus(person), "PersonE6") is < 1 or > 4) return null;
        if (RecruitExecutorNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "Person161") >= 2 ||
            (RecruitExecutorNeed(current.ReadArmyReturnPersonFlags196(person), "Person15E") & 1) != 0 ||
            DuelAftermathSpecialPerson(current, person) || PersonCurrentBusySource.Read(current, person, RecruitExecutorMissing)) return null;
        return new(RecruitExecutorNeed(current.ReadPersonIdentity(person), "Person10"), person,
            RecruitExecutorNeed(current.ReadPersonFirstLocation(person), "PersonE2"));
    }

    private TalentRecruitCommandSource ReadTalentRecruitCommandSource(GameState state, int forceId, int sourceBaseId, int executorId)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int person = _definitions.ResolveOfficerSource(executorId) ?? throw RecruitExecutorMissing("command_Person");
            if (!RecruitExecutorNeed(current.ReadPersonNativeValidity(person), "command_Person_validity")) return new(false, null, null, null);
            int raw = RecruitExecutorNeed(current.ReadPersonDistrictSlot(person), "command_PersonE0");
            var member = current.ReadDistrictTableReference(raw < 52 ? raw : 0) ?? throw RecruitExecutorMissing("command_member_District");
            int membership = RecruitExecutorNeed(member.PhysicalSlot, "command_member_identity");
            var own = current.ReadPersonForce(person) ?? throw RecruitExecutorMissing("command_Person_Force");
            if (RecruitExecutorNeed(current.ReadForcePhysicalSlot(own), "command_Force_identity") != forceId ||
                RecruitExecutorNeed(current.ReadPersonFirstLocation(person), "command_PersonE2") != sourceBaseId)
                return new(false, null, null, null);
            var home = BaseLookup.Find(sourceBaseId, current);
            if (!RecruitExecutorNeed(current.ReadBaseNativeValidity(home), "command_Base_validity")) return new(false, null, null, null);
            int baseForceRaw = RecruitExecutorNeed(current.ReadBaseForceSlot(home), "command_Base_Force");
            var baseForce = current.ReadForceTableReference(baseForceRaw is >= 0 and < 52 ? baseForceRaw : 0)
                ?? throw RecruitExecutorMissing("command_Base_Force_pointer");
            if (RecruitExecutorNeed(current.ReadForcePhysicalSlot(baseForce), "command_Base_Force_identity") != forceId)
                return new(false, null, null, null);
            int payment = RecruitExecutorNeed(current.ReadBaseDistrictSlot(home), "command_payment_District");
            return new(true, membership, payment, null);
        }
        catch (InvalidDataException missing) { return new(null, null, null, missing.Message); }
    }

    private static T RecruitExecutorNeed<T>(T? value, string field) where T : struct => value ?? throw RecruitExecutorMissing(field);
    private static InvalidDataException RecruitExecutorMissing(string field) => new("talent.recruit.executors." + field);
}

using San14.Core;

namespace San14.Data;

internal enum RulerCaptureBranch { Ordinary, LastCityRuler, DispositionRequired }
internal sealed record RulerCaptureBranchResult(RulerCaptureBranch Branch,
    RulerCaptureDecisionResult? Decision);

internal static class PersonRulerCaptureBranchSource
{
    //2E1516..1552: only an actual City position and exactly one current
    //City bypass2F86F0. Gate(kind6) is not a City(kind5).
    internal static bool IsLastCityRulerCurrent(SourceWorldContext current, int person)
    {
        if (Need(current.ReadPersonStatus(person), "PersonE6") != 1) return false;
        ushort position = Need(current.ReadPersonSecondLocation(person), "PersonE4");
        if (PersonPositionRepair.DecodeSecondLocation(position).Kind != 5) return false;
        var force = current.ReadPersonForce(person) ?? throw Missing("Force22CB40");
        return Need(PersonArmyCaptureSource.ReadOwnedCityCount(current, force), "ForceCityCount22AB60") == 1;
    }

    internal static GameState WriteLastCityRulerTargetCurrent(GameState state,
        SourceWorldContext current, int person)
    {
        int id = Need(current.ReadPersonIdentity(person), "Person10");
        var previous = current.ReadOfficerAftermath(person) ?? throw Missing("Person162_owner");
        //This source stores162 only; it never resets the independent164 byte.
        current.SetOfficerAftermath(person, previous with
        {
            Resentment = new(Math.Min(id, 6000), previous.Resentment?.Intensity),
            SourceKnowledge = previous.SourceKnowledge is { } residue
                ? residue with { UnmappedResentmentTarget = null } : null
        });
        return current.PublishOfficerAftermath(state, id);
    }

    private static T Need<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new("ruler-capture.branch." + owner);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal RulerCaptureBranchResult ReadRulerCaptureBranchCurrent(GameState state,
        SourceWorldContext current, int personPhysical, ForceReference winningForce,
        TacticSourceSamplerWorld? sampler)
    {
        byte status = current.ReadPersonStatus(personPhysical)
            ?? throw new InvalidDataException("ruler-capture.branch.PersonE6");
        if (status != 1) return new(RulerCaptureBranch.Ordinary, null);
        if (PersonRulerCaptureBranchSource.IsLastCityRulerCurrent(current, personPhysical))
            return new(RulerCaptureBranch.LastCityRuler, null);
        return new(RulerCaptureBranch.DispositionRequired,
            RulerCaptureReleaseSource.Decide(state, current, personPhysical, winningForce, sampler));
    }
}

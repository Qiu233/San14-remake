using San14.Core;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Closed ordinary ruler branch of Person231560→2B2B30. Its
    /// rank capacity and effect77 collection are actual current inputs. The
    /// separate non-ruler2A9F90 branch remains explicitly unresolved.</summary>
    private int? ReadOwnedRulerHealingCapacity(GameState state, UnitState target, OfficerState officer)
    {
        int? physical = CommanderPhysical(state, target);
        if (physical is null || officer.Role != OfficerRole.Ruler ||
            officer.ServiceStatus != OfficerServiceStatus.Ruler ||
            _definitions.ResolveOfficerSource(officer.Id) != physical || !Bound(state) ||
            _definitions.StaticSource is not { } scenario ||
            state.ContentHashes.GetValueOrDefault(scenario.ResourceUri) != scenario.Sha256 ||
            !state.Forces.TryGetValue(target.ForceId, out ForceState? force) ||
            force.RulerId != officer.Id || force.RankId is not int rankId ||
            rankId is < 0 or > 12 ||
            !state.ForceRanks.TryGetValue(rankId, out ForceRankDefinition? rank) ||
            rank.CommandLimit is < 0 or > ushort.MaxValue) return null;
        //Person V18 reaches the current appendix tail for flags4000. A stale
        //serialized ruler status cannot turn an invalid current Person valid.
        bool? valid = NativeValidity.PersonWithBehavior(physical.Value, checked((ushort)officer.Id), officer.Behavior, OfficerServiceStatusSourceAdapter.Encode(officer.ServiceStatus));
        if (valid is null) return null;
        if (!valid.Value) return 0;
        //232CB0 can succeed only with current native Force IDs46..50;
        //2334A0's first2C70D0 test accepts only the five ten-ID bands.
        //A normal ruler in an ordinary force skips both special replacements.
        if (target.ForceId is >= 46 and <= 50 || IsCapacitySpecialPerson(officer.Id)) return null;
        TacticCommanderCapacityTraits? traits = TacticRemakeCurrentTraits.CommanderCapacityTraits(
            _definitions, physical.Value);
        int? coefficient = Parameter(state, SimulationParameterKeys.Tactics.HealingCapacityCoefficient), policy = Policy(state, target.ForceId, 0x37);
        if (traits is null || coefficient is null || policy is null) return null;
        int first = unchecked(rank.CommandLimit + unchecked(traits.Group256To355 * coefficient.Value));
        int value = unchecked(Math.Min(first, 20000) + unchecked(traits.OtherGroup * coefficient.Value));
        return unchecked(value + policy.Value);
    }

    private static bool IsCapacitySpecialPerson(int id) =>
        unchecked((uint)(id - 4111)) <= 9 || unchecked((uint)(id - 4131)) <= 9 ||
        unchecked((uint)(id - 4151)) <= 9 || unchecked((uint)(id - 4171)) <= 9 ||
        unchecked((uint)(id - 4191)) <= 9;

    public TacticSourceSamplerWorld? ReadCurrentSamplerWorld(GameState state)
    {
        if (_operationSampler is not null)
            return Bound(state) ? _operationSampler.Read(state) : null;
        if (!Bound(state) || state.NativeClock is not { } clock ||
            state.NativeArmySimulation!.World.SamplerEpoch is not { } epoch ||
            state.NativeMapSeason is not { } season || epoch.NativeClockRevision != clock.Revision ||
            season.NativeClockRevision != clock.Revision || season.ProcessedNativeOrdinal != clock.UpdateOrdinal ||
            season.SourceScenarioUri != epoch.SourceScenarioUri ||
            season.SourceScenarioSha256 != epoch.SourceScenarioSha256 || season.SourceWorldSha256 != epoch.SourceWorldSha256 ||
            state.ContentHashes.GetValueOrDefault(epoch.SourceScenarioUri) != epoch.SourceScenarioSha256)
            return null;
        try { MapSeasonRuntime.ValidateOwned(season); }
        catch (InvalidDataException) { return null; }
        return new(unchecked(epoch.Day450 + (int)season.AdvancedDays), epoch.Salt45C);
    }
}

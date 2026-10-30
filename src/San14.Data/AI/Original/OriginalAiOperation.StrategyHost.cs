using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    // The source clears this before each root update, then consumes it in the
    // same host call. It is independent of node cursors and saved blackboards.
    internal bool StrategyTreeRestartRequested { get; private set; }
    internal void BeginStrategyTreeUpdate() => StrategyTreeRestartRequested = false;
    internal void RequestStrategyTreeRestart() => StrategyTreeRestartRequested = true;

    internal void PrepareForceStrategyBlackboard()
    {
        SetInteger(ResourceNameHash.Compute("AgentType"), (int)OriginalAiAgentKind.Force);
        SetCandidateList(ResourceNameHash.Compute("TARGET_FORCE_LIST"), WorldEntityKind.Force, []);
    }

    internal void PrepareArmyStrategyBlackboard()
        => SetInteger(ResourceNameHash.Compute("AgentType"), (int)OriginalAiAgentKind.Army);

    internal void PrepareDistrictStrategyBlackboard()
    {
        SetInteger(ResourceNameHash.Compute("AgentType"), (int)OriginalAiAgentKind.District);
        SetCandidateList(ResourceNameHash.Compute("CAUTION_FOOTHOLD_LIST"), WorldEntityKind.City, []);
        SetCandidateList(ResourceNameHash.Compute("CAUTION_TROOPS_LIST"), WorldEntityKind.ArmyCautionGroup, []);
        SetCandidateList(ResourceNameHash.Compute("CAUTION_ARMY_LIST"), WorldEntityKind.ArmyCaution, []);
        SetCandidateList(ResourceNameHash.Compute("AREA_OF_INFLUENCE"), WorldEntityKind.Area, []);
    }

    internal bool PopulateDistrictStrategyCautionGroups()
    {
        uint key = ResourceNameHash.Compute("CAUTION_TROOPS_LIST");
        // B5350 returns before tree execution if this actual object is absent.
        if (!TryTypedCandidates(key, CandidateListKind.CautionGroup, out var list)) return false;
        var rows = Adapter.CaptureOriginalAiDistrictCautionGroupsCurrent(Current, InputsCurrent,
            Actor.DistrictId ?? throw new InvalidDataException("original_ai.strategy_host.District_identity"));
        list.Values.Clear(); list.Values.AddRange(rows);
        return true;
    }

    internal ImmutableArray<int>? CaptureDistrictStrategyCautionGroups()
    {
        if (!TryTypedCandidates(ResourceNameHash.Compute("CAUTION_TROOPS_LIST"), CandidateListKind.CautionGroup, out var list))
            return null;
        return list.Values.Select(value => value.Kind == WorldEntityKind.ArmyCautionGroup ? value.Id
            : throw new InvalidDataException("original_ai.strategy_host.caution_group_list_payload")).ToImmutableArray();
    }
}

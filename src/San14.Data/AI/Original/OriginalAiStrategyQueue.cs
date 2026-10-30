using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

public enum OriginalAiStrategyQueueStage { BeforePlayer, AfterPlayer }

/// <summary>One reached source row, or an actual player/end barrier. The
/// dispatcher advances a local cursor and publishes it once with its bank.</summary>
public sealed record OriginalAiStrategyQueueBatch(ImmutableArray<OriginalAiStrategyInvocation> Invocations,
    OriginalAiStrategySchedule NextSchedule, bool Stop);

public delegate OriginalAiStrategyQueueBatch OriginalAiStrategyQueueReader(GameState state,
    SourceWorldContext current, OriginalAiStrategySchedule schedule, OriginalAiStrategyQueueStage stage);

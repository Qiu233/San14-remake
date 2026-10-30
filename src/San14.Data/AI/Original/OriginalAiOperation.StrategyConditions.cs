using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal bool StrategyDistrictResourceSetting(uint key, int resource, int desired) =>
        TryEntity(key, out var entity) && entity.Kind == WorldEntityKind.District &&
        Adapter.ReadOriginalAiStrategyDistrictResourceSetting(InputsCurrent, entity.Id, resource, desired);

    internal bool StrategyDifficulty(int literal) =>
        Adapter.ReadOriginalAiStrategyDifficulty(Current, literal);

    internal bool StrategyDoctrine(uint key, int literal) =>
        TryEntity(key, out var entity) && entity.Kind == WorldEntityKind.Officer &&
        Adapter.ReadOriginalAiStrategyDoctrine(Current, InputsCurrent, entity.Id, literal);
}

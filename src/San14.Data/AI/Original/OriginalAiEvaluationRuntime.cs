using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>The shared receiver-score owner for one policy operation. Source
/// evaluation writers modify receiver metadata, not a per-actor blackboard.
/// Snapshots occur at the operation/save boundary, not before each query.</summary>
public sealed partial class OriginalAiEvaluationRuntime
{
    internal Dictionary<OriginalAiEntity, int> Entities { get; } = [];
    internal Dictionary<OriginalAiHex, int> Hexes { get; } = [];
    internal Dictionary<OriginalAiHireOffer, int> HireOffers { get; } = [];
    internal Dictionary<OriginalAiPersonSkillReference, int> Skills { get; } = [];
    internal Dictionary<OriginalAiDefinitionReference, int> Definitions { get; } = [];

    public OriginalAiEvaluationRuntime(ImmutableArray<OriginalAiBlackboardEvaluation> saved)
    {
        if (saved.IsDefault) throw new InvalidDataException("original_ai.evaluations.unknown");
        foreach (var item in saved)
            switch (item.Receiver)
            {
                case OriginalAiEntityValue value: Entities.Add(new(value.EntityKind, value.Id), item.Value); break;
                case OriginalAiHexValue value: Hexes.Add(new(value.Id), item.Value); break;
                case OriginalAiHireOfferValue value: HireOffers.Add(new(value.Id), item.Value); break;
                case OriginalAiFormationValue value: Skills.Add(new(OriginalAiPersonSkillKind.Formation, value.Id), item.Value); break;
                case OriginalAiTacticValue value: Skills.Add(new(OriginalAiPersonSkillKind.Tactic, value.Id), item.Value); break;
                case OriginalAiFacilityDefinitionValue value: Definitions.Add(new(OriginalAiDefinitionKind.Facility, value.Id), item.Value); break;
                case OriginalAiGemDefinitionValue value: Definitions.Add(new(OriginalAiDefinitionKind.Gem, value.Id), item.Value); break;
                default: throw new InvalidDataException("original_ai.evaluations.receiver_type");
            }
    }

    public ImmutableArray<OriginalAiBlackboardEvaluation> Capture()
    {
        var values = ImmutableArray.CreateBuilder<OriginalAiBlackboardEvaluation>();
        foreach (var item in Entities) values.Add(new(new OriginalAiEntityValue(item.Key.Kind, item.Key.Id), item.Value));
        foreach (var item in Hexes) values.Add(new(new OriginalAiHexValue(item.Key.Id), item.Value));
        foreach (var item in HireOffers) values.Add(new(new OriginalAiHireOfferValue(item.Key.TemplateId), item.Value));
        foreach (var item in Skills)
            values.Add(new(item.Key.Kind == OriginalAiPersonSkillKind.Formation
                ? new OriginalAiFormationValue(item.Key.PhysicalSlot) : new OriginalAiTacticValue(item.Key.PhysicalSlot), item.Value));
        foreach (var item in Definitions)
            values.Add(new(item.Key.Kind == OriginalAiDefinitionKind.Facility
                ? new OriginalAiFacilityDefinitionValue(item.Key.PhysicalSlot) : new OriginalAiGemDefinitionValue(item.Key.PhysicalSlot), item.Value));
        return values.ToImmutable();
    }

    /// <summary>Called by the actual receiver retirement, never by a query
    /// that infers retirement from a missing current view.</summary>
    public void RemoveEntity(WorldEntityKind kind, int id) => Entities.Remove(new(kind, id));
}

public sealed partial class OriginalAiOperation
{
    private OriginalAiEvaluationRuntime? evaluationRuntime;

    public void UseEvaluationRuntime(OriginalAiEvaluationRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (evaluationRuntime is not null)
        {
            if (!ReferenceEquals(evaluationRuntime, runtime))
                throw new InvalidOperationException("An AI invocation cannot replace its shared receiver-score owner.");
            return;
        }
        if (candidateEvaluations.Count != 0 || hexEvaluations.Count != 0 ||
            hireOfferEvaluations.Count != 0 || personSkillEvaluations.Count != 0 || definitionEvaluations.Count != 0)
            throw new InvalidOperationException("Receiver scores must be bound before an AI invocation writes scores.");
        evaluationRuntime = runtime;
        candidateEvaluations = runtime.Entities; hexEvaluations = runtime.Hexes;
        hireOfferEvaluations = runtime.HireOffers; personSkillEvaluations = runtime.Skills; definitionEvaluations = runtime.Definitions;
    }
}

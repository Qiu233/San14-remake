using San14.Core;

namespace San14.Data.AI.Original;

public enum OriginalAiDefinitionKind { Facility, Gem }
public readonly record struct OriginalAiDefinitionReference(OriginalAiDefinitionKind Kind, int PhysicalSlot);

public sealed partial class OriginalAiOperation
{
    private readonly Dictionary<uint, OriginalAiDefinitionReference> definitions = [];
    private Dictionary<OriginalAiDefinitionReference, int> definitionEvaluations = [];
    private GameState? definitionConstructionOwner;
    private SourceWorldContext? definitionConstructionCurrent;

    public bool TryDefinition(uint key, out OriginalAiDefinitionReference value)
    {
        if (integers.ContainsKey(key) || entities.ContainsKey(key) || candidateLists.ContainsKey(key) ||
            candidateNullEntities.Contains(key) || hexes.ContainsKey(key) || troops.ContainsKey(key) ||
            tradeCountries.ContainsKey(key) || hireOffers.ContainsKey(key) || personSkills.ContainsKey(key) || regions.ContainsKey(key))
        { value = default; return false; }
        return definitions.TryGetValue(key, out value);
    }
    public OriginalAiNodeStatus SetDefinition(uint key, OriginalAiDefinitionReference value)
    {
        if (value.PhysicalSlot is < 0 or > 30 || !Enum.IsDefined(value.Kind))
            throw DefinitionMissing("reference_range");
        integers.Remove(key); entities.Remove(key); candidateLists.Remove(key);
        ClearCandidateNull(key); definitions[key] = value;
        return OriginalAiNodeStatus.Success;
    }
    internal void ClearDefinitionVariable(uint key) => definitions.Remove(key);
    internal bool TryCopyDefinitionVariable(uint source, uint destination)
    {
        if (!TryDefinition(source, out var value)) return false;
        SetDefinition(destination, value); return true;
    }
    internal bool TryCompareDefinitionVariables(uint first, uint second, out bool equal)
    {
        bool left = TryDefinition(first, out var a), right = TryDefinition(second, out var b);
        equal = left && right && a == b; return left || right;
    }
    private static bool IsDefinitionCandidate(CandidateListKind kind) => kind is CandidateListKind.Facility or CandidateListKind.Gem;
    private static CandidateListKind DefinitionCandidateKind(OriginalAiDefinitionKind kind) => kind switch
    {
        OriginalAiDefinitionKind.Facility => CandidateListKind.Facility,
        OriginalAiDefinitionKind.Gem => CandidateListKind.Gem,
        _ => throw DefinitionMissing("kind")
    };
    private static bool DefinitionMatches(OriginalAiDefinitionReference value, CandidateListKind kind) => DefinitionCandidateKind(value.Kind) == kind;

    public void ReplaceDefinitionCandidates(uint output, OriginalAiDefinitionKind kind, IReadOnlyList<int> physicalSlots)
    {
        var list = GetOrCreateTypedCandidates(output, DefinitionCandidateKind(kind));
        list.DefinitionValues.Clear();
        foreach (int physical in physicalSlots)
        {
            if (physical is < 0 or > 30) throw DefinitionMissing("list_reference_range");
            list.DefinitionValues.Add(new(kind, physical));
        }
    }
    internal OriginalAiNodeStatus SetDefinitionEvaluation(uint input, uint scoreKey, OriginalAiDefinitionKind kind)
    {
        if (!TryDefinition(input, out var value) || value.Kind != kind || !TryInteger(scoreKey, out int score))
            return OriginalAiNodeStatus.Failure;
        // 084B20 writes common+8 without a native-validity test.
        definitionEvaluations[value] = score; return OriginalAiNodeStatus.Success;
    }
    private int DefinitionEvaluation(OriginalAiDefinitionReference value) => definitionEvaluations.TryGetValue(value, out int score)
        ? score : throw DefinitionMissing("receiver_evaluation." + value.Kind + "." + value.PhysicalSlot);

    internal OriginalAiNodeStatus AddPossibleFacilityCandidates(uint personKey, uint hexKey, uint output)
    {
        if (!TryEntity(personKey, out var person) || person.Kind != WorldEntityKind.Officer || !TryHex(hexKey, out var hex))
            return OriginalAiNodeStatus.Failure;
        var current = InputsCurrent;
        bool prepare = !ReferenceEquals(definitionConstructionOwner, Current) || !ReferenceEquals(definitionConstructionCurrent, current);
        var result = Adapter.ReadOriginalAiPossibleFacilityDefinitionsCurrent(Current, current, person.Id, hex.Id, prepare);
        if (result is null) return OriginalAiNodeStatus.Failure;
        definitionConstructionOwner = Current; definitionConstructionCurrent = current;
        ReplaceDefinitionCandidates(output, OriginalAiDefinitionKind.Facility, result);
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus LoopGemDefinitions(int mode, uint input, uint output,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children)
    {
        if (mode == 2) return LoopTypedCandidates(input, output, CandidateListKind.Gem, children);
        if (mode == 0)
        {
            if (Actor.AgentKind is null) throw DefinitionMissing("gem_loop_actor_kind");
            if (Actor.AgentKind != OriginalAiAgentKind.Force) return OriginalAiNodeStatus.Success;
            // The source builds a temporary snapshot of the Force's actual
            // book counts before running any child, retaining repetitions.
            foreach (int physical in Adapter.ReadOriginalAiForceGemDefinitionsCurrent(Current, InputsCurrent, Actor.ForceId))
            {
                SetDefinition(output, new(OriginalAiDefinitionKind.Gem, physical));
                if (RunSourceLoopElement(children)) break;
            }
            return OriginalAiNodeStatus.Success;
        }
        if (mode == 1)
        {
            // Unlike Force inventory, global mode visits0..30 once, without
            // Gem validity or an inventory-count gate.
            for (int slot = 0; slot < 31; slot++)
            {
                var pointer = InputsCurrent.ReadSkillGemReference(slot) ?? throw DefinitionMissing("gem_loop_pointer");
                int physical = pointer.PhysicalSlot ?? throw DefinitionMissing("gem_loop_identity");
                SetDefinition(output, new(OriginalAiDefinitionKind.Gem, physical));
                if (RunSourceLoopElement(children)) break;
            }
            return OriginalAiNodeStatus.Success;
        }
        throw DefinitionMissing("gem_loop_mode");
    }
    private static InvalidDataException DefinitionMissing(string field) => new("original_ai.definition." + field);
}

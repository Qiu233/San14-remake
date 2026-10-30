using San14.Core;

namespace San14.Data.AI.Original;

public enum OriginalAiPersonSkillKind { Formation, Tactic }
/// <summary>A normal immutable definition receiver, never an Officer identity.</summary>
public readonly record struct OriginalAiPersonSkillReference(OriginalAiPersonSkillKind Kind, int PhysicalSlot);

public sealed partial class OriginalAiOperation
{
    private readonly Dictionary<uint, OriginalAiPersonSkillReference> personSkills = [];
    private Dictionary<OriginalAiPersonSkillReference, int> personSkillEvaluations = [];

    internal OriginalAiNodeStatus PersonInformation(uint input, int mode, uint output)
    {
        if (mode == 27) return PersonAbilitySum(input, output);
        if (TryHireOffer(input, out var offer)) return HireOfferInformation(offer, mode, output);
        if (!TryEntity(input, out var person) || person.Kind != WorldEntityKind.Officer)
            return OriginalAiNodeStatus.Failure;
        int physical = Adapter.ReadOriginalAiPersonInformationReceiver(InputsCurrent, person.Id);
        if (!(InputsCurrent.ReadPersonNativeValidity(physical) ?? throw PersonInformationMissing("Person_validity")))
            return OriginalAiNodeStatus.Failure;
        if (mode is 1 or 2)
            return SetAncestorEntity(output, Adapter.ReadOriginalAiPersonInformationEntity(InputsCurrent, physical, mode));
        if (mode is 22 or 23)
        {
            var kind = mode == 22 ? OriginalAiPersonSkillKind.Formation : OriginalAiPersonSkillKind.Tactic;
            var list = GetOrCreateTypedCandidates(output, SkillCandidateKind(kind));
            // Capture the source before replacing a potentially aliased output key.
            var values = Adapter.ReadOriginalAiPersonInformationSkills(Current, InputsCurrent, physical, kind);
            list.SkillValues.Clear(); list.SkillValues.AddRange(values);
            return OriginalAiNodeStatus.Success;
        }
        if (mode is 28 or 29)
        {
            var values = Adapter.ReadOriginalAiPersonInformationAffections(Current, InputsCurrent, physical, mode == 29);
            ReplaceTypedCandidates(output, WorldEntityKind.Officer, values);
            return OriginalAiNodeStatus.Success;
        }
        if (mode == 25) return SetInteger(output, CandidateEvaluation(person));
        return SetInteger(output, Adapter.ReadOriginalAiPersonInformationScalar(Current, InputsCurrent, physical, mode));
    }

    internal bool TryPersonSkill(uint key, out OriginalAiPersonSkillReference value)
    {
        // Other typed writers may leave an older entry in an auxiliary map.
        // Respect the currently active object/integer/list tag before reading it.
        if (integers.ContainsKey(key) || entities.ContainsKey(key) || candidateLists.ContainsKey(key) ||
            candidateNullEntities.Contains(key) || hexes.ContainsKey(key) || troops.ContainsKey(key) ||
            tradeCountries.ContainsKey(key) || hireOffers.ContainsKey(key))
        { value = default; return false; }
        return personSkills.TryGetValue(key, out value);
    }
    internal void ClearPersonSkillVariable(uint key) => personSkills.Remove(key);
    internal bool TryComparePersonSkillVariables(uint leftKey, uint rightKey, out bool equal)
    {
        bool left = TryPersonSkill(leftKey, out var a), right = TryPersonSkill(rightKey, out var b);
        equal = left && right && a == b; return left || right;
    }
    private bool TryCopyPersonSkillVariable(uint source, uint destination)
    {
        if (!TryPersonSkill(source, out var value)) return false;
        SetPersonSkill(destination, value); return true;
    }
    private OriginalAiNodeStatus SetPersonSkill(uint key, OriginalAiPersonSkillReference value)
    {
        ClearCandidateNull(key); integers.Remove(key); entities.Remove(key); candidateLists.Remove(key);
        personSkills[key] = value;
        return OriginalAiNodeStatus.Success;
    }
    private static bool IsSkillCandidate(CandidateListKind kind) => kind is CandidateListKind.Formation or CandidateListKind.Tactic;
    private static CandidateListKind SkillCandidateKind(OriginalAiPersonSkillKind kind) => kind switch
    {
        OriginalAiPersonSkillKind.Formation => CandidateListKind.Formation,
        OriginalAiPersonSkillKind.Tactic => CandidateListKind.Tactic,
        _ => throw PersonInformationMissing("skill_kind")
    };
    private static bool SkillMatches(OriginalAiPersonSkillReference value, CandidateListKind kind) => SkillCandidateKind(value.Kind) == kind;
    private int PersonSkillEvaluation(OriginalAiPersonSkillReference skill) => personSkillEvaluations.TryGetValue(skill, out int value)
        ? value : throw PersonInformationMissing("skill_evaluation." + skill.Kind + "." + skill.PhysicalSlot);
    internal OriginalAiNodeStatus LoopObjectCandidates(uint key, uint output,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children)
    {
        if (integers.ContainsKey(key) || entities.ContainsKey(key) || candidateNullEntities.Contains(key) ||
            TryPersonSkill(key, out _) || TryHex(key, out _) || TryTroops(key, out _) || TryHireOffer(key, out _) ||
            !candidateLists.TryGetValue(key, out var list)) return OriginalAiNodeStatus.Success;
        return LoopObjectCandidateValues(list, output, children);
    }
    private OriginalAiNodeStatus LoopObjectCandidateValues(CandidateList list, uint output,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children)
    {
        //The source retains the list object, not its blackboard key. A child
        //may replace/clear that key without replacing an already retained list.
        int Count() => IsDefinitionCandidate(list.Kind) ? list.DefinitionValues.Count
            : IsSkillCandidate(list.Kind) ? list.SkillValues.Count
            : list.Kind == CandidateListKind.Hex ? list.HexValues.Count
            : list.Kind == CandidateListKind.Troops ? list.TroopsValues.Count
            : list.HoldsHireOffers ? list.HireOfferValues.Count : list.Values.Count;
        for (int index = 0; index < Count(); index++)
        {
            if (IsDefinitionCandidate(list.Kind)) SetDefinition(output, list.DefinitionValues[index]);
            else if (IsSkillCandidate(list.Kind)) SetPersonSkill(output, list.SkillValues[index]);
            else if (list.Kind == CandidateListKind.Hex) SetHex(output, list.HexValues[index]);
            else if (list.Kind == CandidateListKind.Troops) SetTroops(output, list.TroopsValues[index]);
            else if (list.HoldsHireOffers) SetHireOffer(output, list.HireOfferValues[index]);
            else SetEntity(output, list.Values[index]);
            foreach (var child in children)
            {
                var status = child(this);
                if (TryInteger(LoopBreakKey, out int requested) && requested > 0) break;
                if (status != OriginalAiNodeStatus.Success) break;
            }
            if (TryInteger(LoopBreakKey, out int stop) && stop > 0)
            { SetInteger(LoopBreakKey, 0); break; }
        }
        return OriginalAiNodeStatus.Success;
    }
    private static InvalidDataException PersonInformationMissing(string field) => new("original_ai.person_information." + field);
}

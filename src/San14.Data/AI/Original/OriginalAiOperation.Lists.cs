using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>A map-hex object identity, distinct from an integer blackboard value.</summary>
public readonly record struct OriginalAiHex(int Id);

public sealed partial class OriginalAiOperation
{
    private readonly Dictionary<uint, OriginalAiHex> hexes = [];
    private Dictionary<OriginalAiHex, int> hexEvaluations = [];

    public bool TryHex(uint key, out OriginalAiHex value)
    {
        // Existing object/list/null writers already replace the active tag.
        // An older Hex value must not remain visible behind another type.
        if (integers.ContainsKey(key) || entities.ContainsKey(key) ||
            candidateLists.ContainsKey(key) || candidateNullEntities.Contains(key) || troops.ContainsKey(key))
        { value = default; return false; }
        return hexes.TryGetValue(key, out value);
    }

    public OriginalAiNodeStatus SetHex(uint key, OriginalAiHex value)
    {
        integers.Remove(key); entities.Remove(key); candidateLists.Remove(key);
        candidateNullEntities.Remove(key); ClearDefinitionVariable(key); ClearRegionVariable(key); ClearTradeCountryVariable(key); ClearTroopsVariable(key); ClearHireOfferVariable(key); hexes[key] = value;
        return OriginalAiNodeStatus.Success;
    }

    internal void ClearHexVariable(uint key) => hexes.Remove(key);

    internal bool TryCompareHexVariables(uint left, uint right, out bool equal)
    {
        bool hasLeft = TryHex(left, out var a), hasRight = TryHex(right, out var b);
        equal = hasLeft && hasRight && a == b;
        return hasLeft || hasRight;
    }

    internal bool TryCopyHexVariable(uint source, uint destination)
    {
        if (!TryHex(source, out var value)) return false;
        SetHex(destination, value);
        return true;
    }

    internal bool TryCopyListVariable(uint source, uint destination)
    {
        if (integers.ContainsKey(source) || entities.ContainsKey(source) || TryHex(source, out _) || troops.ContainsKey(source) ||
            candidateNullEntities.Contains(source) || !candidateLists.TryGetValue(source, out var value)) return false;
        // B276F0 -> B27E20 copies the original object pointer, including self-assignment.
        integers.Remove(destination); entities.Remove(destination); ClearCandidateNull(destination);
        candidateLists[destination] = value;
        return true;
    }

    private static bool CandidateValueMatches(OriginalAiEntity entity, CandidateListKind kind) => kind switch
    {
        CandidateListKind.Officer => entity.Kind == WorldEntityKind.Officer,
        CandidateListKind.Area => entity.Kind == WorldEntityKind.Area,
        CandidateListKind.Force => entity.Kind == WorldEntityKind.Force,
        CandidateListKind.Army => entity.Kind == WorldEntityKind.Army,
        CandidateListKind.District => entity.Kind == WorldEntityKind.District,
        CandidateListKind.Caution => entity.Kind == WorldEntityKind.ArmyCaution,
        CandidateListKind.CautionGroup => entity.Kind == WorldEntityKind.ArmyCautionGroup,
        CandidateListKind.MapObject => entity.Kind == WorldEntityKind.MapObject,
        CandidateListKind.Landmark => entity.Kind == WorldEntityKind.Landmark,
        CandidateListKind.Foothold => entity.Kind is WorldEntityKind.City or WorldEntityKind.Gate,
        _ => false
    };

    private bool TryTypedCandidates(uint key, CandidateListKind kind, out CandidateList list)
    {
        if (!integers.ContainsKey(key) && !entities.ContainsKey(key) && !TryHex(key, out _) && !troops.ContainsKey(key) &&
            !candidateNullEntities.Contains(key) && candidateLists.TryGetValue(key, out list!) && list.Kind == kind) return true;
        list = null!; return false;
    }

    private CandidateList GetOrCreateTypedCandidates(uint key, CandidateListKind kind)
    {
        if (TryTypedCandidates(key, kind, out var list)) return list;
        integers.Remove(key); entities.Remove(key); ClearCandidateNull(key);
        candidateLists[key] = list = new(kind);
        return list;
    }

    internal void ReplaceTypedCandidates(uint key, WorldEntityKind kind, IReadOnlyList<OriginalAiEntity> values)
    {
        var type = CandidateKind(kind);
        foreach (var value in values)
            if (!CandidateValueMatches(value, type)) throw new InvalidDataException("original_ai.list_entity_type." + key);
        var list = GetOrCreateTypedCandidates(key, type);
        list.HoldsHireOffers = false;
        list.HireOfferValues.Clear();
        // Snapshot only if the caller explicitly passes this list's own view.
        var incoming = ReferenceEquals(values, list.Values) ? values.ToArray() : values;
        list.Values.Clear(); list.Values.AddRange(incoming);
    }

    internal void ReplaceFootholdCandidates(uint key, IReadOnlyList<OriginalAiEntity> values) =>
        ReplaceTypedCandidates(key, WorldEntityKind.City, values);

    internal void ReplaceHireOfferCandidates(uint key, IReadOnlyList<OriginalAiHireOffer> values)
    {
        // Both source tables use the same raw4 list object, not interchangeable
        // person identities. Replacing its contents keeps blackboard aliases.
        var list = GetOrCreateTypedCandidates(key, CandidateListKind.Officer);
        var incoming = ReferenceEquals(values, list.HireOfferValues) ? values.ToArray() : values;
        list.HoldsHireOffers = false;
        list.Values.Clear();
        list.HireOfferValues.Clear(); list.HireOfferValues.AddRange(incoming);
        list.HoldsHireOffers = true;
    }

    internal bool TryReadHireOfferCandidates(uint key, out IReadOnlyList<OriginalAiHireOffer> values)
    {
        values = [];
        if (!TryTypedCandidates(key, CandidateListKind.Officer, out var list)) return false;
        if (!list.HoldsHireOffers && list.Values.Count != 0)
            throw new InvalidDataException("original_ai.hire.non_template_source");
        values = list.HireOfferValues;
        return true;
    }

    public void ReplaceHexCandidates(uint key, IReadOnlyList<OriginalAiHex> values)
    {
        var list = GetOrCreateTypedCandidates(key, CandidateListKind.Hex);
        var incoming = ReferenceEquals(values, list.HexValues) ? values.ToArray() : values;
        list.HexValues.Clear(); list.HexValues.AddRange(incoming);
    }

    internal OriginalAiNodeStatus ClearTypedCandidates(uint key, CandidateListKind kind)
    {
        if (kind == CandidateListKind.Caution) return ClearArmyCautionCandidates(key);
        var list = GetOrCreateTypedCandidates(key, kind);
        list.HoldsHireOffers = false; list.HireOfferValues.Clear();
        list.Values.Clear(); list.HexValues.Clear(); list.TroopsValues.Clear(); list.SkillValues.Clear(); list.DefinitionValues.Clear();
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus AddTypedCandidate(uint input, uint output, CandidateListKind kind, bool unique)
    {
        if (IsDefinitionCandidate(kind))
        {
            if (!TryDefinition(input, out var definition) || !DefinitionMatches(definition, kind)) return OriginalAiNodeStatus.Failure;
            var list = GetOrCreateTypedCandidates(output, kind);
            if (!unique || !list.DefinitionValues.Contains(definition)) list.DefinitionValues.Add(definition);
        }
        else if (IsSkillCandidate(kind))
        {
            if (!TryPersonSkill(input, out var skill) || !SkillMatches(skill, kind)) return OriginalAiNodeStatus.Failure;
            var list = GetOrCreateTypedCandidates(output, kind);
            if (!unique || !list.SkillValues.Contains(skill)) list.SkillValues.Add(skill);
        }
        else if (kind == CandidateListKind.Hex)
        {
            if (!TryHex(input, out var hex)) return OriginalAiNodeStatus.Failure;
            var list = GetOrCreateTypedCandidates(output, kind);
            if (!unique || !list.HexValues.Contains(hex)) list.HexValues.Add(hex);
        }
        else if (kind == CandidateListKind.Troops)
        {
            if (!TryTroops(input, out var value)) return OriginalAiNodeStatus.Failure;
            var list = GetOrCreateTypedCandidates(output, kind);
            if (!unique || !list.TroopsValues.Contains(value)) list.TroopsValues.Add(value);
        }
        else if (kind == CandidateListKind.Officer && TryHireOffer(input, out var offer))
        {
            var list = GetOrCreateTypedCandidates(output, kind);
            if (!list.HoldsHireOffers && list.Values.Count != 0)
                throw new InvalidDataException("original_ai.hire.mixed_person_list");
            list.HoldsHireOffers = true;
            if (!unique || !list.HireOfferValues.Contains(offer)) list.HireOfferValues.Add(offer);
        }
        else
        {
            if (!TryEntity(input, out var entity) || !CandidateValueMatches(entity, kind)) return OriginalAiNodeStatus.Failure;
            var list = GetOrCreateTypedCandidates(output, kind);
            if (list.HoldsHireOffers && list.HireOfferValues.Count != 0)
                throw new InvalidDataException("original_ai.hire.mixed_person_list");
            list.HoldsHireOffers = false;
            if (!unique || !list.Values.Contains(entity)) list.Values.Add(entity);
        }
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus RemoveTypedCandidate(uint input, uint key, CandidateListKind kind)
    {
        if (!TryTypedCandidates(key, kind, out var list)) return OriginalAiNodeStatus.Failure;
        if (IsDefinitionCandidate(kind))
        {
            if (!TryDefinition(input, out var definition) || !DefinitionMatches(definition, kind)) return OriginalAiNodeStatus.Failure;
            list.DefinitionValues.RemoveAll(value => value == definition);
        }
        else if (IsSkillCandidate(kind))
        {
            if (!TryPersonSkill(input, out var skill) || !SkillMatches(skill, kind)) return OriginalAiNodeStatus.Failure;
            list.SkillValues.RemoveAll(value => value == skill);
        }
        else if (kind == CandidateListKind.Hex)
        {
            if (!TryHex(input, out var hex)) return OriginalAiNodeStatus.Failure;
            list.HexValues.RemoveAll(value => value == hex);
        }
        else if (kind == CandidateListKind.Troops)
        {
            if (!TryTroops(input, out var troopsValue)) return OriginalAiNodeStatus.Failure;
            list.TroopsValues.RemoveAll(value => value == troopsValue);
        }
        else if (kind == CandidateListKind.Officer && TryHireOffer(input, out var offer))
        {
            if (list.HoldsHireOffers) list.HireOfferValues.RemoveAll(value => value == offer);
        }
        else
        {
            if (!TryEntity(input, out var entity) || !CandidateValueMatches(entity, kind)) return OriginalAiNodeStatus.Failure;
            if (list.HoldsHireOffers) return OriginalAiNodeStatus.Success;
            list.Values.RemoveAll(value => value == entity);
        }
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus TypedCandidateLength(uint key, uint output, CandidateListKind kind) =>
        TryTypedCandidates(key, kind, out var list)
            ? SetInteger(output, IsDefinitionCandidate(kind) ? list.DefinitionValues.Count : IsSkillCandidate(kind) ? list.SkillValues.Count : kind == CandidateListKind.Hex ? list.HexValues.Count : kind == CandidateListKind.Troops ? list.TroopsValues.Count
                : list.HoldsHireOffers ? list.HireOfferValues.Count : list.Values.Count)
            : OriginalAiNodeStatus.Failure;

    internal bool TypedCandidateContains(uint input, uint key, CandidateListKind kind)
    {
        if (!TryTypedCandidates(key, kind, out var list)) return false;
        if (kind == CandidateListKind.Officer && TryHireOffer(input, out var offer))
            return list.HoldsHireOffers && list.HireOfferValues.Contains(offer);
        if (list.HoldsHireOffers) return false;
        return IsDefinitionCandidate(kind)
            ? TryDefinition(input, out var definition) && DefinitionMatches(definition, kind) && list.DefinitionValues.Contains(definition)
            : IsSkillCandidate(kind)
            ? TryPersonSkill(input, out var skill) && SkillMatches(skill, kind) && list.SkillValues.Contains(skill)
            : kind == CandidateListKind.Hex
            ? TryHex(input, out var hex) && list.HexValues.Contains(hex)
            : kind == CandidateListKind.Troops
                ? TryTroops(input, out var value) && list.TroopsValues.Contains(value)
                : TryEntity(input, out var entity) && CandidateValueMatches(entity, kind) && list.Values.Contains(entity);
    }

    internal OriginalAiNodeStatus TypedCandidateByIndex(uint key, uint indexKey, uint output, CandidateListKind kind)
    {
        if (!TryInteger(indexKey, out int index) || !TryTypedCandidates(key, kind, out var list)) return OriginalAiNodeStatus.Failure;
        int count = IsDefinitionCandidate(kind) ? list.DefinitionValues.Count : IsSkillCandidate(kind) ? list.SkillValues.Count : kind == CandidateListKind.Hex ? list.HexValues.Count : kind == CandidateListKind.Troops ? list.TroopsValues.Count
            : list.HoldsHireOffers ? list.HireOfferValues.Count : list.Values.Count;
        if ((uint)index < (uint)count)
            return IsDefinitionCandidate(kind) ? SetDefinition(output, list.DefinitionValues[index])
                : IsSkillCandidate(kind) ? SetPersonSkill(output, list.SkillValues[index])
                : kind == CandidateListKind.Hex ? SetHex(output, list.HexValues[index])
                : kind == CandidateListKind.Troops ? SetTroops(output, list.TroopsValues[index])
                : list.HoldsHireOffers ? SetHireOffer(output, list.HireOfferValues[index])
                : SetEntity(output, list.Values[index]);
        SetVariableNullObject(output);
        return OriginalAiNodeStatus.Failure;
    }

    internal OriginalAiNodeStatus SetTypedCandidateEvaluation(uint input, uint scoreKey, CandidateListKind kind)
    {
        if (kind is CandidateListKind.Caution or CandidateListKind.Troops)
            throw new InvalidDataException("original_ai.list_evaluation_source_unsupported." + kind);
        if (!TryInteger(scoreKey, out int score)) return OriginalAiNodeStatus.Failure;
        if (IsDefinitionCandidate(kind))
        {
            if (!TryDefinition(input, out var definition) || !DefinitionMatches(definition, kind)) return OriginalAiNodeStatus.Failure;
            definitionEvaluations[definition] = score;
        }
        else if (IsSkillCandidate(kind))
        {
            if (!TryPersonSkill(input, out var skill) || !SkillMatches(skill, kind)) return OriginalAiNodeStatus.Failure;
            personSkillEvaluations[skill] = score;
        }
        else if (kind == CandidateListKind.Hex)
        {
            if (!TryHex(input, out var hex)) return OriginalAiNodeStatus.Failure;
            hexEvaluations[hex] = score;
        }
        else if (kind == CandidateListKind.Officer && TryHireOffer(input, out var offer))
            SetHireOfferEvaluation(offer, score);
        else
        {
            if (!TryEntity(input, out var entity) || !CandidateValueMatches(entity, kind)) return OriginalAiNodeStatus.Failure;
            if (entity.Kind == WorldEntityKind.Army)
                (evaluationRuntime ?? throw new InvalidDataException("original_ai.Army.receiver_evaluation_runtime"))
                    .SetArmyEvaluation(Current, entity.Id, score);
            else candidateEvaluations[entity] = score;
        }
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus SortTypedCandidates(uint key, CandidateListKind kind, bool ascending)
    {
        if (kind is CandidateListKind.Caution or CandidateListKind.Troops)
            throw new InvalidDataException("original_ai.list_sort_source_unsupported." + kind);
        if (!TryTypedCandidates(key, kind, out var list)) return OriginalAiNodeStatus.Failure;
        if (IsDefinitionCandidate(kind))
        {
            for (int index = 1; index < list.DefinitionValues.Count; index++)
            {
                var value = list.DefinitionValues[index]; int score = DefinitionEvaluation(value), target = index;
                while (target > 0 && (ascending ? score < DefinitionEvaluation(list.DefinitionValues[target - 1]) : score > DefinitionEvaluation(list.DefinitionValues[target - 1])))
                { list.DefinitionValues[target] = list.DefinitionValues[target - 1]; target--; }
                list.DefinitionValues[target] = value;
            }
        }
        else if (list.HoldsHireOffers)
        {
            for (int index = 1; index < list.HireOfferValues.Count; index++)
            {
                var value = list.HireOfferValues[index]; int score = HireOfferEvaluation(value), target = index;
                while (target > 0 && (ascending ? score < HireOfferEvaluation(list.HireOfferValues[target - 1]) : score > HireOfferEvaluation(list.HireOfferValues[target - 1])))
                { list.HireOfferValues[target] = list.HireOfferValues[target - 1]; target--; }
                list.HireOfferValues[target] = value;
            }
        }
        else if (IsSkillCandidate(kind))
        {
            for (int index = 1; index < list.SkillValues.Count; index++)
            {
                var value = list.SkillValues[index]; int score = PersonSkillEvaluation(value), target = index;
                while (target > 0 && (ascending ? score < PersonSkillEvaluation(list.SkillValues[target - 1]) : score > PersonSkillEvaluation(list.SkillValues[target - 1])))
                { list.SkillValues[target] = list.SkillValues[target - 1]; target--; }
                list.SkillValues[target] = value;
            }
        }
        else if (kind != CandidateListKind.Hex)
        {
            // The shared insertion-sort implementation preserves equal-score order.
            for (int index = 1; index < list.Values.Count; index++)
            {
                var value = list.Values[index]; int score = CandidateEvaluation(value), target = index;
                while (target > 0 && (ascending ? score < CandidateEvaluation(list.Values[target - 1]) : score > CandidateEvaluation(list.Values[target - 1])))
                { list.Values[target] = list.Values[target - 1]; target--; }
                list.Values[target] = value;
            }
        }
        else
        {
            int Score(OriginalAiHex hex) => hexEvaluations.TryGetValue(hex, out int value) ? value
                : throw new InvalidDataException("original_ai.hex_evaluation_input." + hex.Id);
            for (int index = 1; index < list.HexValues.Count; index++)
            {
                var value = list.HexValues[index]; int score = Score(value), target = index;
                while (target > 0 && (ascending ? score < Score(list.HexValues[target - 1]) : score > Score(list.HexValues[target - 1])))
                { list.HexValues[target] = list.HexValues[target - 1]; target--; }
                list.HexValues[target] = value;
            }
        }
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus LoopTypedCandidates(uint key, uint output, CandidateListKind kind,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children)
    {
        if (kind == CandidateListKind.Caution)
            throw new InvalidDataException("original_ai.list_loop_resolver_unsupported." + kind);
        if (!TryTypedCandidates(key, kind, out var list)) return OriginalAiNodeStatus.Success;
        if (IsDefinitionCandidate(kind) || IsSkillCandidate(kind) || kind == CandidateListKind.Troops) return LoopObjectCandidateValues(list, output, children);
        if (kind is not (CandidateListKind.Officer or CandidateListKind.Hex)) return LoopCandidateValues(list.Values, output, children);
        for (int index = 0; index < (kind == CandidateListKind.Hex ? list.HexValues.Count
            : list.HoldsHireOffers ? list.HireOfferValues.Count : list.Values.Count); index++)
        {
            if (kind == CandidateListKind.Hex) SetHex(output, list.HexValues[index]);
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
}

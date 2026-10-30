using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal enum CandidateListKind { Officer, Area, Foothold, Force, Army, District, Hex, Caution, Troops, Formation, Tactic, CautionGroup, MapObject, Landmark, Facility, Gem }

    internal sealed class CandidateList(CandidateListKind kind)
    {
        internal CandidateListKind Kind { get; } = kind;
        private readonly List<OriginalAiEntity> values = [];
        // Main-person consumers must not mistake an independent offer list for
        // an empty roster. Generic raw4 operations dispatch the payload first.
        internal List<OriginalAiEntity> Values => HoldsHireOffers && HireOfferValues.Count != 0
            ? throw new InvalidDataException("original_ai.hire.template_list_as_main_person") : values;
        internal bool HoldsHireOffers { get; set; }
        internal List<OriginalAiHireOffer> HireOfferValues { get; } = [];
        internal List<OriginalAiHex> HexValues { get; } = [];
        internal List<OriginalAiTroopsReference> TroopsValues { get; } = [];
        internal List<OriginalAiPersonSkillReference> SkillValues { get; } = [];
        internal List<OriginalAiDefinitionReference> DefinitionValues { get; } = [];
    }

    private readonly Dictionary<uint, CandidateList> candidateLists = [];
    private Dictionary<OriginalAiEntity, int> candidateEvaluations = [];
    private readonly HashSet<uint> candidateNullEntities = [];
    private GameState? domesticCurrentOwner;
    private OriginalAiDomesticCurrent? domesticCurrent;
    private static readonly uint LoopBreakKey = ResourceNameHash.Compute("IsLoopBreak");

    internal void ClearCandidateNull(uint key)
    {
        candidateNullEntities.Remove(key);
        ClearHexVariable(key);
        ClearTradeCountryVariable(key);
        ClearTroopsVariable(key);
        ClearHireOfferVariable(key);
        ClearPersonSkillVariable(key);
        ClearRegionVariable(key);
        ClearDefinitionVariable(key);
    }
    internal bool CandidateNullKeysEqual(uint left, uint right) =>
        candidateNullEntities.Contains(left) && candidateNullEntities.Contains(right);

    private OriginalAiDomesticCurrent DomesticCurrent
    {
        get
        {
            if (!ReferenceEquals(domesticCurrentOwner, Current))
            {
                domesticCurrent = new(Adapter, Current, Actor);
                domesticCurrentOwner = Current;
            }
            return domesticCurrent!;
        }
    }

    /// <summary>Explicit ancestor-produced blackboard list input. The policy
    /// host must supply its actual ordered entities; no World roster is seeded.</summary>
    public void SetCandidateList(uint key, WorldEntityKind kind, IReadOnlyList<OriginalAiEntity> values)
    {
        var list = new CandidateList(CandidateKind(kind));
        foreach (var value in values)
        {
            if (!CandidateValueMatches(value, CandidateKind(kind))) throw new InvalidDataException($"original_ai.list_entity_type.{key:X8}");
            list.Values.Add(value);
        }
        integers.Remove(key); entities.Remove(key); ClearCandidateNull(key);
        candidateLists[key] = list;
    }

    private bool TryCandidateList(uint key, WorldEntityKind kind, out CandidateList list)
    {
        if (!integers.ContainsKey(key) && !entities.ContainsKey(key) && !hexes.ContainsKey(key) && !troops.ContainsKey(key) &&
            candidateLists.TryGetValue(key, out list!) && list.Kind == CandidateKind(kind)) return true;
        list = null!; return false;
    }

    private static CandidateListKind CandidateKind(WorldEntityKind kind) => kind switch
    {
        WorldEntityKind.Officer => CandidateListKind.Officer,
        WorldEntityKind.Area => CandidateListKind.Area,
        WorldEntityKind.Force => CandidateListKind.Force,
        WorldEntityKind.Army => CandidateListKind.Army,
        WorldEntityKind.District => CandidateListKind.District,
        WorldEntityKind.ArmyCaution => CandidateListKind.Caution,
        WorldEntityKind.ArmyCautionGroup => CandidateListKind.CautionGroup,
        WorldEntityKind.MapObject => CandidateListKind.MapObject,
        WorldEntityKind.Landmark => CandidateListKind.Landmark,
        WorldEntityKind.City or WorldEntityKind.Gate => CandidateListKind.Foothold,
        _ => throw new InvalidDataException("original_ai.homogeneous_list_kind." + kind)
    };

    internal OriginalAiNodeStatus ClearCandidates(uint key, WorldEntityKind kind) =>
        ClearTypedCandidates(key, CandidateKind(kind));

    internal OriginalAiNodeStatus AddCandidate(uint input, uint output, WorldEntityKind kind, bool unique) =>
        AddTypedCandidate(input, output, CandidateKind(kind), unique);

    internal OriginalAiNodeStatus RemoveCandidate(uint input, uint key, WorldEntityKind kind) =>
        RemoveTypedCandidate(input, key, CandidateKind(kind));

    internal OriginalAiNodeStatus SetCandidateEvaluation(uint input, uint score, WorldEntityKind kind) =>
        SetTypedCandidateEvaluation(input, score, CandidateKind(kind));

    private int CandidateEvaluation(OriginalAiEntity entity) => entity.Kind == WorldEntityKind.Army
        ? evaluationRuntime is { } runtime ? runtime.ReadArmyEvaluation(Current, entity.Id)
            : unchecked((int)(ArmyAiEvaluationSource.Read(Current, entity.Id).Value
                ?? throw new InvalidDataException("original_ai.Army.retained_receiver_evaluation_word8")))
        : candidateEvaluations.TryGetValue(entity, out int value)
            ? value : throw new InvalidDataException($"original_ai.entity_evaluation_input.{entity.Kind}.{entity.Id}");

    internal OriginalAiNodeStatus SortCandidates(uint key, WorldEntityKind kind, bool ascending) =>
        SortTypedCandidates(key, CandidateKind(kind), ascending);

    internal OriginalAiNodeStatus CandidateByIndex(uint key, uint indexKey, uint output, WorldEntityKind kind) =>
        TypedCandidateByIndex(key, indexKey, output, CandidateKind(kind));

    internal OriginalAiNodeStatus LoopCandidates(uint key, uint output, WorldEntityKind kind,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children) =>
        LoopTypedCandidates(key, output, CandidateKind(kind), children);

    private OriginalAiNodeStatus LoopCandidateValues(List<OriginalAiEntity> values, uint output,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children)
    {
        for (int index = 0; index < values.Count; index++)
        {
            SetEntity(output, values[index]);
            foreach (var child in children)
            {
                var status = child(this);
                if (TryInteger(LoopBreakKey, out int requested) && requested > 0) break;
                if (status != OriginalAiNodeStatus.Success) break;
            }
            if (TryInteger(LoopBreakKey, out int stop) && stop > 0)
            {
                SetInteger(LoopBreakKey, 0);
                break;
            }
        }
        return OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus DomesticInformation(uint input, int field, uint output, WorldEntityKind kind)
    {
        if (kind == WorldEntityKind.Officer && TryHireOffer(input, out var offer))
            return HireOfferInformation(offer, field, output);
        if (!TryEntity(input, out var entity) || !CandidateValueMatches(entity, CandidateKind(kind))) return OriginalAiNodeStatus.Failure;
        if (kind is WorldEntityKind.City or WorldEntityKind.Gate && field == 29)
        {
            if (!DomesticCurrent.Condition(22, [entity], [])) return OriginalAiNodeStatus.Failure;
            var rows = DomesticCurrent.FootholdAreas(entity.Id);
            SetCandidateList(output, WorldEntityKind.Area, rows.Select(id => new OriginalAiEntity(WorldEntityKind.Area, id)).ToArray());
            return OriginalAiNodeStatus.Success;
        }
        int? value = kind switch
        {
            WorldEntityKind.Area => DomesticCurrent.AreaScalar(entity.Id, field),
            WorldEntityKind.Officer when field == 25 => DomesticCurrent.Condition(22, [entity], [])
                ? CandidateEvaluation(entity) : null,
            WorldEntityKind.Officer => DomesticCurrent.PersonScalar(entity.Id, field),
            _ => null
        };
        return value is int known ? SetInteger(output, known) : OriginalAiNodeStatus.Failure;
    }

    internal bool DomesticCondition(uint query, IReadOnlyList<uint> parameters)
    {
        int[] literals = query switch
        {
            2 => [unchecked((int)parameters[0])],
            11 when parameters.Count == 2 => [-1, unchecked((int)parameters[1])],
            11 => [unchecked((int)parameters[1]), unchecked((int)parameters[2])],
            113 or 114 or 118 => [unchecked((int)parameters[1])],
            _ => []
        };
        if (query is not (2 or 11 or 12 or 112 or 118) && TryHireOffer(parameters[0], out var offer))
            return HireOfferCondition(offer, query, literals);
        int keyCount = query == 112 ? 2 : 1;
        var operands = new OriginalAiEntity[keyCount];
        int first = query == 2 ? 1 : 0;
        for (int i = 0; i < keyCount; i++)
        {
            if (!TryEntity(parameters[first + i], out operands[i])) return false;
            WorldEntityKind expected = query switch
            {
                2 or 11 or 12 => WorldEntityKind.Area,
                112 => i == 0 ? WorldEntityKind.Area : WorldEntityKind.Force,
                118 => WorldEntityKind.District,
                _ => WorldEntityKind.Officer
            };
            if (query != 22 && operands[i].Kind != expected) return false;
        }
        return DomesticCurrent.Condition(query, operands, literals);
    }
}

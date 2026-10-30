using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

public enum OriginalAiNodeStatus { Success = 1, Failure = 2, Running = 3 }

/// <summary>Logical source identity, never an original-process pointer or an
/// arbitrary CLR object. Officer identities are not physical table slots.</summary>
public readonly record struct OriginalAiEntity(WorldEntityKind Kind, int Id);
public readonly record struct OriginalAiIntegerBinding(uint Key, int Value);
public readonly record struct OriginalAiEntityBinding(uint Key, OriginalAiEntity Value);
public sealed record OriginalAiInitialBindings(ImmutableArray<OriginalAiIntegerBinding> Integers,
    ImmutableArray<OriginalAiEntityBinding> Entities)
{
    public static OriginalAiInitialBindings Empty { get; } = new([], []);
}
public enum OriginalAiAgentKind { Army = 1, Troops = 2, District = 3, Force = 4 }
public readonly record struct OriginalAiActor(int ForceId, int? DistrictId, OriginalAiAgentKind? AgentKind = null)
{
    /// <summary>Reference to the actual world-owned Army group for this
    /// borrowed tree invocation. It is neither a District nor a copied Army.</summary>
    public OriginalAiTroopsReference? Troops { get; init; }
    /// <summary>The actual logical unit selected by the Army host. This is
    /// not a physical Army table slot or a member chosen from a Troops group.</summary>
    public int? ArmyId { get; init; }
    /// <summary>The corresponding source table receiver captured by the Army
    /// scheduler for this borrowed invocation, independently of the unit ID.</summary>
    public int? ArmyPhysicalSlot { get; init; }
}

/// <summary>One borrowed command-planning operation. Only Apply publishes a
/// change to its candidate. Temporary values do not escape into the policy.</summary>
public sealed partial class OriginalAiOperation : IOriginalAiConditionScalarValues
{
    private readonly CommandPlanningContext planning;
    private readonly PlayableRuleSet rules;
    private readonly ArmyCampaignRuleAdapter adapter;
    private readonly Dictionary<uint, int> integers = [];
    private readonly Dictionary<uint, OriginalAiEntity> entities = [];
    private readonly CommandContext commandContext;
    private readonly IOrderAuthority authority;
    public OriginalAiActor Actor { get; }
    public GameState Current => planning.Current;
    internal ArmyCampaignRuleAdapter Adapter => adapter;
    public CommandResult? LastCommand { get; private set; }

    public OriginalAiOperation(CommandPlanningContext planning, PlayableRuleSet rules,
        ArmyCampaignRuleAdapter adapter, OriginalAiActor actor, OriginalAiInitialBindings bindings)
    {
        this.planning = planning; this.rules = rules; this.adapter = adapter; Actor = actor;
        foreach (var item in bindings.Integers) integers.Add(item.Key, item.Value);
        foreach (var item in bindings.Entities)
        {
            if (integers.ContainsKey(item.Key)) throw new InvalidDataException($"original_ai.binding_type.{item.Key:X8}");
            entities.Add(item.Key, item.Value);
        }
        string name = $"original-ai:{actor.ForceId}" + (actor.DistrictId is int district ? $":district:{district}" : "");
        commandContext = new(name, actor.DistrictId);
        authority = new SingleForceAuthority(actor.ForceId, name, actor.DistrictId is int selected ? [selected] : []);
    }

    public bool TryInteger(uint key, out int value) => integers.TryGetValue(key, out value);
    public bool TryReadInteger(uint key, out int value) => TryInteger(key, out value);
    public bool ObjectKeysEqual(uint leftKey, uint rightKey)
    {
        if (TryCompareDefinitionVariables(leftKey, rightKey, out bool definitionEqual)) return definitionEqual;
        if (TryCompareRegionVariables(leftKey, rightKey, out bool regionEqual)) return regionEqual;
        if (TryComparePersonSkillVariables(leftKey, rightKey, out bool skillsEqual)) return skillsEqual;
        if (TryCompareHireOfferVariables(leftKey, rightKey, out bool offersEqual)) return offersEqual;
        if (TryCompareTroopsVariables(leftKey, rightKey, out bool troopsEqual)) return troopsEqual;
        if (TryCompareTradeCountryVariables(leftKey, rightKey, out bool countryEqual)) return countryEqual;
        if (TryCompareHexVariables(leftKey, rightKey, out bool hexEqual)) return hexEqual;
        if (entities.TryGetValue(leftKey, out var left) && entities.TryGetValue(rightKey, out var right)) return left == right;
        if (entities.ContainsKey(leftKey) || entities.ContainsKey(rightKey) || integers.ContainsKey(leftKey) || integers.ContainsKey(rightKey))
            return false;
        if (CandidateNullKeysEqual(leftKey, rightKey)) return true;
        return candidateLists.TryGetValue(leftKey, out var leftList) && candidateLists.TryGetValue(rightKey, out var rightList) &&
            ReferenceEquals(leftList, rightList);
    }
    public bool TryEntity(uint key, out OriginalAiEntity value) => entities.TryGetValue(key, out value);
    public int Integer(uint key) => TryInteger(key, out int value) ? value :
        throw new InvalidDataException($"original_ai.integer_input.{key:X8}");
    public OriginalAiEntity Entity(uint key, WorldEntityKind kind)
    {
        if (!entities.TryGetValue(key, out var value))
            throw new InvalidDataException($"original_ai.entity_input.{key:X8}.{kind}");
        if (value.Kind != kind) throw new InvalidDataException($"original_ai.entity_type.{key:X8}.{kind}.{value.Kind}");
        return value;
    }
    public OriginalAiNodeStatus SetInteger(uint key, int value)
    { ClearCandidateNull(key); entities.Remove(key); candidateLists.Remove(key); integers[key] = value; return OriginalAiNodeStatus.Success; }
    public OriginalAiNodeStatus SetEntity(uint key, OriginalAiEntity value)
    { ClearCandidateNull(key); integers.Remove(key); candidateLists.Remove(key); entities[key] = value; return OriginalAiNodeStatus.Success; }

    internal OriginalAiNodeStatus CallScript(Func<OriginalAiScriptArguments, int> script,
        uint destination, ImmutableArray<uint> argumentKeys)
    {
        bool available = true;
        int Read(int index)
        {
            uint key = index < argumentKeys.Length ? argumentKeys[index] : 0;
            if (key == 0) return 0;
            if (TryInteger(key, out int value)) return value;
            available = false; return 0;
        }
        var arguments = new OriginalAiScriptArguments(Read(0), Read(1), Read(2), Read(3), Read(4), Read(5), Read(6));
        if (!available) return OriginalAiNodeStatus.Failure;
        return SetInteger(destination, script(arguments));
    }
    internal OriginalAiNodeStatus Development(uint areaKey, uint output, int commerce, int agriculture, int barracks)
    {
        int area = Entity(areaKey, WorldEntityKind.Area).Id;
        if (area == 0 || Current.Areas.TryGetValue(area, out var row) && row.CityId == 0)
            return OriginalAiNodeStatus.Failure;
        int focus = adapter.ReadOriginalAiDevelopmentFocusCurrent(Current, Actor.DistrictId ?? -1,
            area, commerce, agriculture, barracks);
        return SetInteger(output, focus);
    }
    internal OriginalAiNodeStatus Appoint(uint areaKey, uint officerKey, uint focusKey)
    {
        int area = Entity(areaKey, WorldEntityKind.Area).Id;
        int officer = Entity(officerKey, WorldEntityKind.Officer).Id;
        var prepared = adapter.CreateOriginalAiAreaAppointmentCurrent(Current, Actor.DistrictId ?? -1,
            area, officer, Integer(focusKey));
        if (!prepared.Success) return OriginalAiNodeStatus.Failure;
        if (prepared.Order is not { } order) return OriginalAiNodeStatus.Success;
        if (order.ForceId != Actor.ForceId) return OriginalAiNodeStatus.Failure;
        var result = planning.Apply(order, rules.OrderRules, commandContext, authority);
        LastCommand = result;
        if (result.MissingRuleKey is { } missing)
            throw new InvalidDataException($"original_ai.command.{missing}: {result.Error}");
        return result.Accepted ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
    }
}

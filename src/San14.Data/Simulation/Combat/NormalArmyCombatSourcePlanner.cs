using San14.Core;

namespace San14.Data;

/// <summary>The reached32-stage scheduler position before its calendar writer.
/// CalendarCycleOrdinal counts actual two-hour World+34 writer cycles. It is
/// independent of NativeClock's forty waiting-manager updates per period.</summary>
public sealed record NormalArmyCombatPhaseMoment(long CalendarCycleOrdinal,
    NativeMapSeasonDate WorldThirdDate, byte SchedulerStageIndex, string SourceScenarioUri,
    string SourceScenarioSha256, string SourceWorldSha256,
    string Revision);

/// <summary>16FFA0/170660 beginning-of-phase descriptor, not current UI stats.
/// AcceptedPrimaryCount is the complete actor count stored at descriptor14;
/// later combat may change live quantities without rewriting this snapshot.</summary>
public sealed record NormalArmyCombatDescriptor(int ArmyId, ushort NativePoolSlot,
    byte CappedMorale8, ushort TroopsA, int NativeHexC, int NativeForce10,
    byte AcceptedPrimaryCount14);
public sealed record NormalArmyCombatPair(int Index,
    NormalArmyCombatDescriptor Attacker, NormalArmyCombatDescriptor Target);
public sealed record NormalArmyCombatPlan(NormalArmyCombatPhaseMoment Moment,
    NormalArmyCombatPair[] OrderedPairs, ulong RandomState, string Revision)
{
    //16FFA0 also generates Base/Gate-origin pairs. This finite receipt contains
    //only Army-origin versus Army targets, never a complete world phase claim.
    public bool IncludesBaseOrGateOriginPairs => false;
}

/// <summary>Operation-local queries. Every Army in the owned allocation needs
/// an actual descriptor and complete170660 membership, including empty lists.
/// An unresolved reached Base/Object is unknown, not a dropped candidate.</summary>
public interface INormalArmyCombatCurrentQueries
{
    NormalArmyCombatDescriptor? ReadDescriptor(GameState state, UnitState army);
    int[]? ReadCompletePrimaryArmyIds(GameState state, UnitState army);
}

/// <summary>Current production projection over already source-created Armies.
/// It borrows one captured context, and never fills missing native morale from
/// Core.Morale or chooses a native slot from the logical Army ID.</summary>
public sealed class NormalArmyCombatCurrentQueries : INormalArmyCombatCurrentQueries
{
    private readonly TacticRemakeCurrentContext context;
    public NormalArmyCombatCurrentQueries(TacticRemakeCurrentContext current) =>
        context = current ?? throw new ArgumentNullException(nameof(current));
    public int[]? ReadCompletePrimaryArmyIds(GameState state, UnitState army) =>
        context.ResolveArmyNativePrimaryCandidates(state, army)?.OrderedArmyIds.ToArray();
    public NormalArmyCombatDescriptor? ReadDescriptor(GameState state, UnitState army)
    {
        if (context.ArmyValid(state, army) != true || army.NativeArmyOrigin?.NativeSlotIndex is not int slot ||
            slot is < 1 or > 500 || ArmyMoraleSourceAdapter.Read(army) is not byte morale ||
            context.ForceMoraleMaximum(state, army.ForceId) is not int maximum || maximum is < 0 or > 255 ||
            army.Troops is < 0 or > ushort.MaxValue ||
            context.ResolveArmyNativePrimaryCandidates(state, army) is not { } candidates ||
            candidates.OrderedArmyIds.Length > byte.MaxValue) return null;
        return new(army.Id, (ushort)slot, (byte)Math.Min(morale, maximum), (ushort)army.Troops,
            army.HexId, army.ForceId, (byte)candidates.OrderedArmyIds.Length);
    }
}

/// <summary>SC44D680/TC44D480 stage13 ->17A820 ->16FFA0 selection/sort receipt.
/// It does not execute169A40, stage6 tactics,178C20 aftermath, or any Core period
/// combat. Equal comparisons use the saved native registry generation order;
/// an unavailable order leaves that reached branch unresolved.</summary>
public sealed class NormalArmyCombatSourcePlanner
{
    public const byte SchedulerStageIndex = 13;
    public const string ProfileId = "san14:ce-normal-army-phase13-20261003";
    private readonly INormalArmyCombatCurrentQueries queries;
    public string Language { get; }
    public string RuleVersion { get; }
    public string Revision { get; }
    public NormalArmyCombatSourcePlanner(INormalArmyCombatCurrentQueries currentQueries,
        string language, string ruleVersion, string revision)
    {
        queries = currentQueries ?? throw new ArgumentNullException(nameof(currentQueries));
        if (language is not ("SC" or "TC") ||
            ruleVersion != BattleUnitSourceCodeSources.ForLanguage(language).RuleVersion ||
            string.IsNullOrWhiteSpace(revision))
            throw new InvalidDataException("Normal combat planner requires its compatible project rule version and revision.");
        Language = language; RuleVersion = ruleVersion; Revision = revision;
    }

    /// <summary>The source branch is signedSubday/2==5. Ordinary calendar
    /// writes only even0..22, so its one reached hour is10, once per native day.</summary>
    public static bool IsDueAtSourceCalendarPhase(NormalArmyCombatPhaseMoment moment)
    {
        ValidateMoment(moment);
        return moment.WorldThirdDate.Hour / 2 == 5;
    }

    public NormalArmyCombatPlan? Resolve(GameState state, NormalArmyCombatPhaseMoment moment)
    {
        ArgumentNullException.ThrowIfNull(state); ValidateMoment(moment);
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            parameters.Language != Language || parameters.RuleVersion != RuleVersion ||
            state.NativeMapSeason is not { } season ||
            season.SourceScenarioUri != moment.SourceScenarioUri ||
            season.SourceScenarioSha256 != moment.SourceScenarioSha256 ||
            season.SourceWorldSha256 != moment.SourceWorldSha256 ||
            state.ContentHashes.GetValueOrDefault(moment.SourceScenarioUri) != moment.SourceScenarioSha256) return null;
        string revision = $"{ProfileId}:{Revision}:{parameters.Revision}:{moment.Revision}";
        if (!IsDueAtSourceCalendarPhase(moment)) return new(moment with { }, [], state.RandomState, revision);
        var descriptors = new Dictionary<int, NormalArmyCombatDescriptor>();
        var accepted = new Dictionary<int, int[]>();
        var slots = new HashSet<ushort>();
        foreach (UnitState army in state.Units.Values)
        {
            if (army.NativeArmyOrigin is not { } origin || origin.Language != Language ||
                origin.RuleVersion != RuleVersion || origin.NativeSlotIndex is not int slot ||
                queries.ReadDescriptor(state, army) is not { } descriptor ||
                queries.ReadCompletePrimaryArmyIds(state, army) is not { } ids) return null;
            if (descriptor.ArmyId != army.Id || descriptor.NativePoolSlot != slot ||
                descriptor.TroopsA != army.Troops || descriptor.NativeHexC != army.HexId ||
                descriptor.NativeForce10 != army.ForceId || descriptor.NativePoolSlot is < 1 or > 500 ||
                !slots.Add(descriptor.NativePoolSlot) || ids.Length != descriptor.AcceptedPrimaryCount14 ||
                ids.Contains(army.Id) || ids.Distinct().Count() != ids.Length)
                throw new InvalidDataException("Normal combat descriptor/membership belongs to another or incomplete owned Army capture.");
            descriptors.Add(army.Id, descriptor with { }); accepted.Add(army.Id, ids.ToArray());
        }
        var pairs = new List<NormalArmyCombatPair>();
        foreach (var actor in accepted)
            foreach (int targetId in actor.Value)
            {
                if (!descriptors.TryGetValue(targetId, out var target)) return null;
                pairs.Add(new(0, descriptors[actor.Key], target));
            }
        //1684F0's true-equal branch inserts the later-generated Army pair
        //before an earlier one. Target identity is part of the equal key, so
        //the raw World60 actor order determines this group's generation order.
        bool hasEqualKeys = false;
        for (int left = 0; left < pairs.Count; left++)
            for (int right = left + 1; right < pairs.Count; right++)
                if (CompareArmyKeys(pairs[left], pairs[right]) == 0) hasEqualKeys = true;
        Dictionary<int, int>? generationOrder = null;
        if (hasEqualKeys)
        {
            var rawOrder = NormalArmyCombatGenerationOrder.ReadArmyManager60(state).OrderedNativeArmySlots;
            if (rawOrder is null || rawOrder.Distinct().Count() != rawOrder.Length) return null;
            generationOrder = rawOrder.Select((slot, index) => (slot, index)).ToDictionary(row => row.slot, row => row.index);
            if (descriptors.Values.Any(row => !generationOrder.ContainsKey(row.NativePoolSlot))) return null;
        }
        pairs.Sort((left, right) => {
            if (ReferenceEquals(left, right)) return 0;
            int comparison = CompareArmyKeys(left, right);
            return comparison != 0 ? comparison : generationOrder![right.Attacker.NativePoolSlot]
                .CompareTo(generationOrder[left.Attacker.NativePoolSlot]);
        });
        return new(moment with { }, pairs.Select((pair, index) => pair with { Index = index }).ToArray(),
            state.RandomState, revision);
    }

    public static int CompareArmyKeys(NormalArmyCombatPair left, NormalArmyCombatPair right)
    {
        int order = left.Target.CappedMorale8.CompareTo(right.Target.CappedMorale8);
        if (order != 0) return order;
        order = left.Target.TroopsA.CompareTo(right.Target.TroopsA); if (order != 0) return order;
        order = left.Target.NativePoolSlot.CompareTo(right.Target.NativePoolSlot); if (order != 0) return order;
        order = right.Attacker.CappedMorale8.CompareTo(left.Attacker.CappedMorale8); if (order != 0) return order;
        return right.Attacker.TroopsA.CompareTo(left.Attacker.TroopsA);
    }

    private static void ValidateMoment(NormalArmyCombatPhaseMoment moment)
    {
        ArgumentNullException.ThrowIfNull(moment);
        if (moment.CalendarCycleOrdinal < 0 || moment.SchedulerStageIndex != SchedulerStageIndex ||
            moment.WorldThirdDate is not { Month: >= 1 and <= 12, Day: >= 1 and <= 30, Hour: >= 0 and <= 22 } date ||
            (date.Hour & 1) != 0 || string.IsNullOrWhiteSpace(moment.SourceScenarioUri) ||
            !Hash(moment.SourceScenarioSha256) || !Hash(moment.SourceWorldSha256) ||
            string.IsNullOrWhiteSpace(moment.Revision))
            throw new InvalidDataException("Normal combat needs its reached pre-calendar stage13 cursor, source epoch, and ordinary native two-hour date.");
    }
    private static bool Hash(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
}

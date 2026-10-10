using San14.Core;

namespace San14.Data;

/// <summary>Owned projection of the logistics mask02 write at the explicit
/// CTurnState step36 boundary. Later native morale/trait aftermath is not a
/// produced Army attribute; scalar rules requiring it remain unavailable.</summary>
public sealed class ArmyLogisticsPreparation : IPlayableNativeArmyPreparationRules
{
    private readonly TacticRemakeCurrentContext _current;
    private readonly SourceWorldContext _definitions;
    private readonly LoadedMap _map;
    private readonly LoadedTerrainTypes _terrain;
    private readonly bool[] _eligible;

    public ArmyLogisticsPreparation(TacticRemakeCurrentContext current,
        SourceWorldContext definitions, LoadedMap map, LoadedTerrainTypes terrain)
    {
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _eligible = TerritoryOwnership.ReadSourceTintEligibility(map, terrain);
    }

    public bool? ResolveLogisticsConnection(GameState state, UnitState army)
    {
        bool? valid = _current.ArmyValid(state, army);
        if (valid is null || army.NativeTacticRuntime is not { } runtime) return null;
        bool former = (runtime.Flags26 & 2) != 0;
        if (!valid.Value) return former;
        if (state.MapSha256 != _map.Source.Sha256 ||
            state.ContentHashes.GetValueOrDefault(_terrain.Source.ResourceUri) != _terrain.Source.Sha256) return null;

        //2C9560/276AB0 use the commander's CURRENT affiliation, not the
        //deployment's retained SourceLocationId. Fresh at-Base Armies exit
        //before1845A0 and retain their fresh clear bit even on a connected map.
        bool? excluded = _current.ArmyManagerExcluded(state, army.Id);
        if (excluded is null) return null;
        if (excluded.Value || army.ForceId is >= 46 and <= 51) return former;
        int? home = ReadCommanderBaseHex(state, army);
        if (home is null) return null;
        if (home == army.HexId) return former;
        if (home is < 0 or >= 48400 || army.HexId is < 0 or >= 48400) return null;

        //Mode8/count1 reverse-seeds the affiliation Base Hex. Its constant
        //10000 cost makes reachability equivalent to this finite breadth-first
        //walk. Seed and destination bypass2C6AA0; intermediates do not.
        var pending = new Queue<int>(); pending.Enqueue(home.Value);
        var visited = new bool[48400]; visited[home.Value] = true;
        bool unknown = false;
        while (pending.TryDequeue(out int hex))
        {
            if (hex == army.HexId) return true;
            foreach (int next in HexNeighborhood.RadiusOneCandidates(hex))
            {
                if (visited[next]) continue;
                visited[next] = true;
                HexReference? reference = _definitions.ReadHexTableReference(next);
                if (reference is null) { unknown = true; continue; }
                if (reference.PhysicalSlot is null) continue;
                if (reference.PhysicalSlot != next) { unknown = true; continue; }
                if (next == army.HexId) return true;
                if (!_eligible[next]) continue;
                if (!state.Territory.TryGetValue(next, out TerritoryState? cell) || cell.Id != next)
                { unknown = true; continue; }
                bool? hostile = ReadNativeRelationGate(state, army.ForceId, cell.ForceId ?? 0);
                if (hostile is null) { unknown = true; continue; }
                if (!hostile.Value) pending.Enqueue(next);
            }
        }
        return unknown ? null : false;
    }

    public bool? PreservesTraitRoster(GameState state, UnitState army)
    {
        bool? valid = _current.ArmyValid(state, army);
        if (valid is null) return null;
        if (!valid.Value) return true;
        bool? excluded = _current.ArmyManagerExcluded(state, army.Id);
        if (excluded is null) return null;
        if (excluded.Value || army.ForceId is >= 46 and <= 51) return true;
        int? home = ReadCommanderBaseHex(state, army);
        if (home is null) return null;
        if (home == army.HexId) return true;
        if (army.NativeTacticRuntime is not { Status10: 1 or 2 } runtime) return null;
        bool? next = ResolveLogisticsConnection(state, army);
        if (next is null) return null;
        bool former = (runtime.Flags26 & 2) != 0;
        //Unchanged bits reach the epilogue directly. The0→1 branch changes
        //only morale1A, then formats actual5EA6 and appends reportType5. Its
        //finite name/string closure writes no Army64 data. Type2's301DD0
        //writes only48/1F8 on the receiver, so it cannot invalidate64.
        //The distinct1→0 aftermath remains outside this preservation receipt.
        return next == former || (!former && next.Value) ? true : null;
    }

    public byte? ResolveFlags150AfterLogistics(GameState state, UnitState army)
    {
        if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical ||
            state.ReadArmyLifecycle()?.ArmyFlags150 is not { } flags || flags[physical] is not byte current)
            return null;
        bool? valid = _current.ArmyValid(state, army);
        if (valid is null) return null;
        if (!valid.Value) return current;
        if (army.NativeTacticRuntime is not { Status10: 2 }) return current;
        bool? excluded = _current.ArmyManagerExcluded(state, army.Id);
        int? home = ReadCommanderBaseHex(state, army);
        if (excluded is null || home is null) return null;
        if (excluded.Value || army.ForceId is >= 46 and <= 51 || home == army.HexId) return current;
        //TC276DB0 always clears a present bit3 before its route requery.
        return (byte)(current & 0xF7);
    }
    public ICampaignResourceState? ResolveResourceKnowledgeAfterLogistics(GameState state, UnitState army,
        ICampaignResourceState? stagedKnowledge, bool traitRosterPreserved) => UpdateResourceKnowledge(army, stagedKnowledge, traitRosterPreserved,
            () => ResolveFlags150AfterLogistics(state, army));

    internal static ICampaignResourceState? UpdateResourceKnowledge(UnitState army,
        ICampaignResourceState? stagedKnowledge, bool traitRosterPreserved, Func<byte?> resolve)
    {
        var knowledge = CampaignSourceKnowledge.From(stagedKnowledge);
        if (!traitRosterPreserved && knowledge.FindArmyTraits(army.Id) is not null)
        {
            knowledge = knowledge.WithoutArmyTraits(army.Id);
            stagedKnowledge = knowledge;
        }
        if (knowledge.ArmyLifecycleSource?.ArmyFlags150 is not { } current ||
            army.NativeArmyOrigin?.NativeSlotIndex is not int physical) return stagedKnowledge;
        var flags = current.ToArray();
        flags[physical] = resolve();
        return knowledge.WithArmyLifecycleSource(knowledge.ArmyLifecycleSource with { ArmyFlags150 = flags });
    }

    /// <summary>Current affiliation Base center consumed by native2C9560.
    /// Exposed for the primary battle-candidate predicate, which shares it.</summary>
    public int? ReadCommanderBaseHex(GameState state, UnitState army)
    {
        if (!state.Officers.TryGetValue(army.OfficerId, out OfficerState? commander) ||
            commander.LocationId is not int location) return null;
        BaseReference? reference;
        if (PlayableSkeleton.IsPlayableRuleset(state.RulesetId))
            reference = new ArmyMoraleSource(_current, _definitions, _current.Revision)
                .ResolveCommanderBase(state, army);
        else
        {
            //The independent generic current-definition adapter can supply
            //these complete fields explicitly. Production owned campaigns use
            //their current campaign fields above, including behavior and status.
            if (_definitions.ResolveOfficerSource(commander.Id) is not int physical ||
                _definitions.ReadPersonNativeValidity(physical) is not bool personValid) return null;
            if (!personValid) return 48400;
            if (_definitions.ReadPersonFirstLocation(physical) is not ushort first) return null;
            try { reference = BaseLookup.Find(first, _definitions); }
            catch (InvalidDataException) { return null; }
        }
        if (reference is null || _definitions.ReadBaseNativeValidity(reference) is not bool valid) return null;
        if (!valid) return 48400;
        ushort? link = _definitions.ReadBaseObjectLink(reference);
        if (link is null) return null;
        ObjectReference? obj = _definitions.ReadObjectTableReference(link.Value <= 3000 ? link.Value : 0);
        if (obj is null || _definitions.ReadObjectNativeValidity(obj) is not bool objectValid) return null;
        if (!objectValid) return 48400;
        ushort? hex = _definitions.ReadObjectHexIndex(obj);
        int? ownedHex = location <= 51 ? state.Cities.GetValueOrDefault(location)?.HexId
            : state.Gates.GetValueOrDefault(location)?.HexId;
        //Base positions currently have no gameplay writer. A divergent source
        //object is unavailable rather than silently replacing the owned Base.
        return hex is < 48400 && ownedHex == hex ? hex : null;
    }

    /// <summary>Exact2CA4D0 boolean over current owned Force fields. This is
    /// separate from the skeleton's diplomatic level/treaty calculations.</summary>
    public static bool? ReadNativeRelationGate(GameState state, int firstId, int secondId)
    {
        if (firstId < 0 || secondId < 0 || state.NativeArmySimulation is not { } simulation) return null;
        if (firstId > 51) firstId = 0;
        if (secondId > 51) secondId = 0;
        if (firstId == 0 || firstId == secondId) return false;
        if (secondId == 0 && firstId is >= 46 and <= 51) return false;
        if (simulation.World.Flags165C is not byte worldFlag) return null;
        NativeArmyForceState? first = simulation.Forces.GetValueOrDefault(firstId);
        NativeArmyForceState? second = simulation.Forces.GetValueOrDefault(secondId);
        if (worldFlag > 0)
        {
            if (first?.Flags12 is not byte firstFlags) return null;
            if ((firstFlags & 2) != 0)
            {
                if (second?.Flags12 is not byte secondFlags) return null;
                if ((secondFlags & 2) != 0) return false;
            }
        }
        if (firstId == 51 || secondId == 51 || secondId == 0) return true;
        if (first?.Flags194 is not byte firstPartner) return null;
        if (firstPartner == secondId) return false;
        if (second?.Flags194 is not byte secondPartner) return null;
        if (secondPartner == firstId) return false;
        return first.RelationsEA is { Length: 52 } relation ? (relation[secondId] & 0x7F) == 0 : null;
    }
}

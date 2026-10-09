using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current queries over this remake's explicitly source-created
/// Armies, owned parameter/Force/World records and source-bound Hex footprints.
/// Unimplemented commander/policy/chain pipelines remain nullable.</summary>
public sealed partial class TacticRemakeCurrentContext : ITacticEffectSourceContext,
    ITacticEffectSubdaySourceContext
{
    private readonly SourceWorldContext _definitions;
    private readonly TacticSourceCatalog _catalog;
    private readonly LoadedMap _map;
    private readonly LoadedTerrainTypes _terrain;
    private readonly TacticOrdinaryArmyUsageSourceProfile _usage;
    private readonly IEffectiveOfficerAbilities? _abilities;
    private readonly ITacticRemakeUnresolvedQueries? _remaining;
    private readonly BattleRemakeCurrentDamage? _normalDamage;
    private readonly TacticEffectSourceOwnedContext _programs;
    private readonly NativeArmyCombatCycleSampler? _operationSampler;
    public string Revision { get; }
    public bool ProvidesNativeInstructionTargets => true;

    public TacticRemakeCurrentContext(SourceWorldContext definitions,
        TacticSourceCatalog catalog, LoadedMap map, LoadedTerrainTypes terrain,
        TacticOrdinaryArmyUsageSourceProfile usage, string revision,
        IEffectiveOfficerAbilities? abilities = null, ITacticRemakeUnresolvedQueries? remaining = null,
        BattleRemakeCurrentDamage? normalDamage = null,
        NativeArmyCombatCycleSampler? operationSampler = null)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _usage = usage ?? throw new ArgumentNullException(nameof(usage));
        if (string.IsNullOrWhiteSpace(revision) || map.Map is not { Width: 220, Height: 220, Cells.Length: 48400 } ||
            terrain.Catalog is not { FormatMarker: 0x66, RecordOffset: 182121, RecordStride: 51, Types.Count: 31 } ||
            terrain.Source.ResourceUri != catalog.Source.ResourceUri ||
            !terrain.Source.Sha256.Equals(catalog.Source.Sha256, StringComparison.OrdinalIgnoreCase) ||
            terrain.Catalog.DecodedSha256 != catalog.DecodedSha256)
            throw new InvalidDataException("Owned tactic context needs the complete source map/terrain and a revision.");
        Revision = revision; _abilities = abilities; _remaining = remaining; _normalDamage = normalDamage;
        _operationSampler = operationSampler;
        _programs = new(definitions, revision, (_, _, _, _) => null);
    }

    public TacticRuleAdapter CreateRules()
    {
        var routes = new PlayableLandRoutes(new HexTravelMap(_map));
        return new(new TacticEffectSourceRules(_catalog, this, routes,
            new TacticEffectSourceSubdayRules(this)));
    }

    public S14TacticEffectProgram? ReadCurrentProgram(GameState state, TacticSourceRow source) =>
        Bound(state) ? _programs.ReadCurrentProgram(state, source) : null;
    public byte? ReadCurrentTargetFlags(GameState state, TacticSourceRow source) =>
        Bound(state) ? _programs.ReadCurrentTargetFlags(state, source) : null;

    public TacticEffectUsageSource? ResolveUsage(GameState state, UnitState? caster,
        TacticSourceRow source, S14TacticEffectProgram program)
    {
        if (caster is null || !Bound(state) || ArmyValid(state, caster) is not true ||
            caster.NativeTacticRuntime is not { } native || EffectiveFormation(state, caster) is not int formation ||
            AvailableTactics(state, caster, formation) is not int[] available ||
            state.NativeArmySimulation!.World.SelectedCommanderCount165D is not 0 ||
            state.NativeArmySimulation.Forces.GetValueOrDefault(caster.ForceId)?.Flags193 is not byte forceFlags)
            return null;
        bool selectedPrimary = program.Instructions.Any(instruction => instruction.Opcode == 23);
        TacticPrimaryArmyCandidatesSource? primaryCandidates = null;
        if (selectedPrimary)
        {
            if (_normalDamage is null || TacticRemakeCurrentBattleControl.Read(state, 0x48) != false ||
                ResolveArmyNativePrimaryCandidates(state, caster) is not { } candidates) return null;
            primaryCandidates = candidates;
            //Each actual candidate must have a complete stage5 input, before
            //a declaration can expose the selected-primary source program.
            foreach (int id in candidates.OrderedArmyIds)
                if (_normalDamage.Resolve(state, caster, state.Units[id]) is null) return null;
        }
        //When action is outside4/5/7 and25 is zero, both possible outcomes of
        //the unresolved ordinary-target equality branch pass17D450.
        byte? action = caster.NativeArmyConfiguration?.Action37;
        bool? outer = native.Abnormal25 == 0 && action is byte knownAction && knownAction is not (4 or 5 or 7)
            ? true : native.ProvocationOwner is { Kind: DeploymentTargetKind.Unit } owner &&
                owner.Id == caster.Id ? true : null;
        if (outer is null) return null;
        TacticRemakeChainResolution? chain = ResolveChain(state, caster, source);
        if (chain is null || chain.RandomState != state.RandomState || chain.ChainCount != 0) return null;
        int? primary = Parameter(state, SimulationParameterKeys.Tactics.PrimaryWaitingCap), second = Parameter(state, SimulationParameterKeys.Tactics.SecondaryWaitingCap), third = Parameter(state, SimulationParameterKeys.Tactics.TertiaryWaitingCap);
        int? adjustment = formation switch { 1 => Parameter(state, SimulationParameterKeys.Tactics.Formation1AutomaticWaitingAdjustment), 2 => Parameter(state, SimulationParameterKeys.Tactics.Formation2AutomaticWaitingAdjustment),
            14 => Parameter(state, SimulationParameterKeys.Tactics.Formation14AutomaticWaitingAdjustment), _ => 0 };
        int? reduction = (forceFlags & 0x40) != 0 ? Parameter(state, SimulationParameterKeys.Tactics.ForceFlagWaitingReduction) : 0;
        TacticReference? receiver = _definitions.ReadSkillTacticReference(source.Slot);
        byte? category = receiver is null ? null : _definitions.ReadSkillTacticRawByte(receiver, 0x40);
        int? policyReduction = category switch { 5 => Policy(state, caster.ForceId, 0x24),
            6 => Policy(state, caster.ForceId, 0x25), null => null, _ => 0 };
        int? rate = Parameter(state, SimulationParameterKeys.Tactics.OrdinaryWaitingRate);
        if (primary is not >= 0 or > 255 || second is not >= 0 or > 255 || third is not >= 0 or > 255 ||
            adjustment is null || reduction is null || policyReduction is null)
            return null;
        var waiting = new TacticArmyWaitingSourceInput(chain.SumOfNativeWaitingContributions,
            chain.ChainCount, adjustment.Value, reduction.Value, policyReduction.Value,
            primary.Value, second.Value, third.Value);
        bool provocationMatches = native.Abnormal25 == 0 || native.ProvocationOwner is
            { Kind: DeploymentTargetKind.Unit } provoking && provoking.Id == caster.Id;
        TacticEffectUsageSource result = _usage.ResolveNative(program, waiting, new(true, native.Status10, available.Length != 0,
            available.Contains(source.Slot), native.Waiting20, native.Abnormal23, native.Abnormal25,
            provocationMatches, outer.Value, (native.Flags26 & 2) != 0, false, rate)) with
            { Revision = $"{_usage.Language}:{_usage.RuleVersion}:{state.NativeArmySimulation.Parameters.Revision}:{Revision}" };
        return primaryCandidates is null ? result : result with
        {
            PrimaryGeometry = new(PlayableTacticTargetRelation.Hostile, primaryCandidates.RangeHexes,
                $"{Revision}:native-primary-16FFA0-170660"),
            Eligibility = primaryCandidates.OrderedArmyIds.Length == 0
                ? new(false, "tactic.no_native_primary") : result.Eligibility
        };
    }

    private TacticRemakeChainResolution? ResolveChain(GameState state, UnitState caster, TacticSourceRow source)
    {
        //An abnormal24 caster passes17D5D0 but2AFA40 returns an empty chain.
        //A complete owned allocation can also prove that every other Army
        //fails the native force/abnormal filters, independent of chain radius.
        if (caster.NativeTacticRuntime is { } native &&
            (native.Abnormal23 > 0 || native.Abnormal24 > 0 || native.Abnormal25 > 0) ||
            HasCompleteOwnedEmptyChain(state, caster))
        {
            ushort? contribution = _programs.ReadCurrentWaitingContribution(source);
            return contribution is null ? null : new(0, contribution.Value, state.RandomState);
        }
        return _remaining?.ResolveChain(state, caster, source.Slot);
    }

    private bool HasCompleteOwnedEmptyChain(GameState state, UnitState caster)
    {
        if (state.Units.GetValueOrDefault(caster.Id) != caster ||
            ArmyValid(state, caster) != true || caster.HexId is < 0 or >= 48400) return false;
        //2AFA40 cannot retain a different-force Army or an Army with any
        //abnormal counter. Prove this over the complete owned allocation, so
        //the still-unread ring radius and personality threshold cannot change
        //the empty result. Every identity/position must itself be source-owned.
        var seenHexes = new HashSet<int>();
        var seenSlots = new HashSet<int>();
        foreach (UnitState candidate in state.Units.Values)
        {
            if (ArmyValid(state, candidate) != true || candidate.HexId is < 0 or >= 48400 ||
                !seenHexes.Add(candidate.HexId) || candidate.NativeArmyOrigin?.NativeSlotIndex is not int slot ||
                !seenSlots.Add(slot)) return false;
            if (candidate.Id != caster.Id && candidate.ForceId == caster.ForceId &&
                candidate.NativeTacticRuntime is { Abnormal23: 0, Abnormal24: 0, Abnormal25: 0 }) return false;
        }
        //The native chain radius remains unqueried on this empty-candidate
        //proof. Establish the entire owned Hex allocation, including absence
        //of aliases outside the effect radius, before skipping that query.
        for (int hex = 0; hex < 48400; hex++)
            if (_definitions.ReadHexTableReference(hex)?.PhysicalSlot != hex) return false;
        return true;
    }

    /// <summary>Common2A8FD0 uses the same2AFA40 count. A complete empty
    /// allocation, or its native abnormal early return, proves neighbor1.
    /// The preceding coefficient/settings helpers have no simulation writes;
    /// their numeric value cannot alter an int32 multiplication by zero.</summary>
    public float? ReadNativeEmptyNeighborFactor(GameState state, UnitState army) =>
        ArmyValid(state, army) == true && (army.NativeTacticRuntime is { } native &&
            (native.Abnormal23 > 0 || native.Abnormal24 > 0 || native.Abnormal25 > 0) ||
            HasCompleteOwnedEmptyChain(state, army)) ? 1f : null;

    public TacticMoraleIncreaseSourceInput? ResolveArmyMoraleIncrease(GameState state,
        UnitState caster, UnitState target, TacticSourceRow source, S14TacticEffectInstruction instruction)
    {
        TacticEffectScaleSourceInput? scale = Scale(state, caster, source, instruction);
        int? first = ForceMoraleMaximum(state, target.ForceId), second = ForceMoraleMaximum(state, target.ForceId),
            third = ForceMoraleMaximum(state, target.ForceId);
        return scale is null || first is null || second is null || third is null ||
            ArmyValid(state, target) != true || target.NativeMorale1A is not byte morale ? null :
            new(scale, morale, first.Value, second.Value, third.Value);
    }

    public int? ReadArmyHealingSelectionCapacity(GameState state, UnitState target) =>
        ArmyValid(state, target) == true && state.Officers.TryGetValue(target.OfficerId, out OfficerState? officer)
            ? ReadOwnedRulerHealingCapacity(state, target, officer) ?? _remaining?.ReadCommanderCapacity(state, officer) : null;

    public TacticEffectQueryResult<TacticHealingSourceInput>? ResolveArmyHealing(GameState state,
        UnitState caster, UnitState target, TacticSourceRow source, S14TacticEffectInstruction instruction)
    {
        TacticEffectScaleSourceInput? scale = Scale(state, caster, source, instruction);
        int? capacity = ReadArmyHealingSelectionCapacity(state, target), floor = Parameter(state, SimulationParameterKeys.Tactics.HealingMinimum);
        return scale is null || capacity is null || floor is null || target.WoundedTroops is not int wounded ? null :
            new(new(scale, target.Troops, wounded, capacity.Value, floor.Value, 20000), state.RandomState);
    }

    private TacticEffectScaleSourceInput? Scale(GameState state, UnitState caster,
        TacticSourceRow source, S14TacticEffectInstruction instruction)
    {
        if (ArmyValid(state, caster) != true || _abilities?.Resolve(state, caster.OfficerId) is not { } ability ||
            ResolveChain(state, caster, source) is not { ChainCount: 0 } ||
            FactorSelector(source) is not byte selector || caster.NativeTacticRuntime is not { } native)
            return null;
        int? divisor = Parameter(state, selector == 1 ? SimulationParameterKeys.Tactics.StrengthEffectDivisor : SimulationParameterKeys.Tactics.IntelligenceEffectDivisor);
        int? stage = Parameter(state, SimulationParameterKeys.Tactics.EffectStageScale);
        int? shared = native.Waiting21 != 0 ? 100 : Parameter(state, native.Waiting22 == 0 ? SimulationParameterKeys.Tactics.Waiting0SharedFactorPercent : SimulationParameterKeys.Tactics.Waiting1SharedFactorPercent);
        if (divisor is null || divisor == 0 || stage is null || shared is null) return null;
        float factor = (float)(selector == 1 ? ability.Strength : ability.Intelligence) / (float)divisor.Value;
        factor += 1f;
        return new(instruction.EffectValue, factor, 0, stage.Value, (float)shared.Value / 100f);
    }

    private byte? FactorSelector(TacticSourceRow source)
    {
        TacticReference? receiver = _definitions.ReadSkillTacticReference(source.Slot);
        if (receiver is null || _definitions.ReadTacticParameterEnd78(receiver) is not ulong end ||
            _definitions.ReadTacticParameterBegin70(receiver) is not { } first) return null;
        if ((unchecked(end - first.ByteAddress) & ~3UL) != 40)
            return _definitions.ReadSkillTacticRawByte(receiver, 0x6A);
        var begin = _definitions.ReadTacticParameterBegin70(receiver);
        var decoder = _definitions.CaptureTacticParameterDecoderTable(receiver);
        if (begin is null || decoder is null || _definitions.ReadTacticParameterPhysicalSlot(receiver) is not int physical ||
            _definitions.ReadTacticParameterEncodedByte(new(begin, 9, unchecked(begin.ByteAddress + 36))) is not byte encoded)
            return null;
        return decoder.Decode(receiver, encoded, 7, physical);
    }

    public int? ForceMoraleMaximum(GameState state, int forceId)
    {
        if (!Bound(state) || !state.Forces.TryGetValue(forceId, out ForceState? force)) return null;
        int? baseline = Parameter(state, SimulationParameterKeys.Morale.Maximum), coefficient = Parameter(state, SimulationParameterKeys.Morale.PolicyScale);
        if (baseline is null) return null;
        if (!force.IsActive) return baseline;
        int? effect = forceId is >= 46 and <= 51 ? 0 : _remaining?.ReadForceEffect10(state, force)
            ?? BattleRemakeCurrentForcePolicies.ReadForceEffect10(state, force);
        int? policy = Policy(state, forceId, 0x47);
        if (effect is null || policy is null || coefficient is null) return null;
        int amount = unchecked(100 + unchecked(effect.Value * coefficient.Value));
        return unchecked(unchecked(amount * baseline.Value) / 100 + policy.Value);
    }

    private int? Policy(GameState state, int forceId, int effect)
    {
        if (!Bound(state) || effect is < 1 or > 79 || !state.Forces.TryGetValue(forceId, out ForceState? force)) return null;
        NativeArmyForceState? native = state.NativeArmySimulation!.Forces.GetValueOrDefault(forceId);
        if (native?.ActivePolicyBitmap1CB is { Length: 10 } active)
        {
            int index = effect - 1, mask = 1 << (index & 7);
            //22C510 returns before reading suppressed bitmap/definition when
            //the current active bit is zero. Preserve this reached-read order.
            if ((active[index >> 3] & mask) == 0) return 0;
            if (native.SuppressedPolicyBitmap1D5 is not { Length: 10 } suppressed) return null;
            if ((suppressed[index >> 3] & mask) != 0) return 0;
        }
        return _remaining?.ReadForcePolicyEffect(state, force, effect);
    }

    private bool Bound(GameState state) => state.MapWidth == 220 && state.MapHeight == 220 &&
        state.MapSha256.Equals(_map.Source.Sha256, StringComparison.OrdinalIgnoreCase) &&
        state.NativeClock is not null && state.NativeArmySimulation?.Parameters is { } parameters &&
        parameters.Language == _usage.Language && parameters.RuleVersion.Equals(_usage.RuleVersion,
            StringComparison.OrdinalIgnoreCase) && state.ContentHashes.GetValueOrDefault(_catalog.Source.ResourceUri)
            ?.Equals(_catalog.Source.Sha256, StringComparison.OrdinalIgnoreCase) == true;

    public bool IsBoundCurrentState(GameState state) => Bound(state);

    public bool? ArmyValid(GameState state, UnitState unit)
    {
        if (!Bound(state) || unit.NativeArmyOrigin is not { } origin ||
            origin.Revision != ArmyConstruction.ProfileId &&
                (origin.Revision != ArmyCampaignRuleAdapter.TransportCreationRevision ||
                 unit.Mission?.Kind != UnitMissionKind.Transport || unit.NativeTransport is null) ||
            origin.Language != _usage.Language || !origin.RuleVersion.Equals(_usage.RuleVersion, StringComparison.OrdinalIgnoreCase) ||
            unit.NativeTacticRuntime is not { } native || unit.NativeArmyConfiguration is null)
            return null;
        if (origin.NativeSlotIndex is not >= 1 or > 500) return null;
        if (!state.Units.TryGetValue(unit.Id, out UnitState? owned) || owned != unit) return null;
        if (unit.Id <= 0 || unit.OfficerId == 0 || native.Status10 == 0) return false;
        if (!state.Officers.TryGetValue(unit.OfficerId, out OfficerState? commander) ||
            commander.Presence is null || commander.ForceId is null || commander.DistrictId is null)
            return null;
        if (commander.Presence != OfficerPresence.Present || !commander.Deployed) return false;
        if (commander.ForceId != unit.ForceId || !state.Districts.TryGetValue(commander.DistrictId.Value, out DistrictState? district) ||
            district.ForceId != unit.ForceId || !state.Forces.ContainsKey(unit.ForceId)) return null;
        return true;
    }

    public int? EffectiveFormation(GameState state, UnitState unit)
    {
        if (ArmyValid(state, unit) != true || unit.HexId is < 0 or >= 48400) return null;
        if (CurrentSurfaceCode(unit) is not byte code) return null;
        int slot = code <= 30 ? code : 0;
        S14TerrainTypeDefinition terrain = _terrain.Catalog.Types[slot];
        return (terrain.RawField2C & 2) != 0 ? unit.NativeArmyConfiguration!.Formation1C : unit.NativeArmyConfiguration!.Formation1B;
    }

    private int[]? AvailableTactics(GameState state, UnitState unit, int formation)
    {
        if (unit.TacticIds is null || formation is < 0 or > 20) return null;
        if (CurrentSurfaceCode(unit) is not byte terrainCode) return null;
        S14TerrainTypeDefinition terrain = _terrain.Catalog.Types[terrainCode <= 30 ? terrainCode : 0];
        //Naval branch consumes current commander ten-tactic fields; the base
        //capability projection is not a current reference-list substitute.
        if ((terrain.RawField2C & 2) != 0) return null;
        var result = new List<int>();
        foreach (int id in unit.TacticIds.Take(3))
        {
            TacticReference? reference = _definitions.ReadSkillTacticReference(id <= 200 && id >= 0 ? id : 0);
            if (reference is null) return null;
            bool? valid = _definitions.ReadSkillTacticValidity(reference);
            if (valid is null) return null;
            if (valid == true) result.Add(id);
        }
        return result.ToArray();
    }

    private byte? CurrentSurfaceCode(UnitState unit)
    {
        HexReference? hex = _definitions.ReadHexTableReference(unit.HexId);
        return hex?.PhysicalSlot == unit.HexId ? _definitions.ReadHexSurfaceCode(hex) : null;
    }

    private int? Parameter(GameState state, string key) => Bound(state) &&
        state.NativeArmySimulation!.Parameters.Int32Values.TryGetValue(key, out int value)
        ? value : null;

    public IReadOnlyList<int>? ReadCurrentAbnormalUpperBounds(GameState state, UnitState caster, UnitState target)
    { int? bound = Parameter(state, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound); return bound is null ? null : new[] { bound.Value, bound.Value, bound.Value }; }
    public int? ReadCurrentArmyAbnormalUpperBound(GameState state, UnitState target, int nativeIndex) => Parameter(state, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound);
    public ulong? FinishArmyAbnormalClear(GameState state, UnitState caster, UnitState target,
        TacticSourceRow source, S14TacticEffectInstruction instruction) =>
        ArmyValid(state, target) == true && target.HexId is >= 0 and < 48400
            ? state.RandomState : null;

    public TacticUseEligibility? ResolvePrimaryTargetEligibility(GameState state, UnitState caster,
        UnitState primaryTarget, TacticSourceRow source)
    {
        if (!Bound(state) || ReadCurrentProgram(state, source) is not { } program ||
            !program.Instructions.Any(instruction => instruction.Opcode == 23) || _normalDamage is null ||
            TacticRemakeCurrentBattleControl.Read(state, 0x48) is not bool blocked) return null;
        if (blocked) return new(false, "tactic.native_tutorial_control");
        TacticPrimaryArmyCandidatesSource? candidates = ResolveArmyNativePrimaryCandidates(state, caster);
        if (candidates is null) return null;
        if (!candidates.OrderedArmyIds.Contains(primaryTarget.Id)) return new(false, "tactic.native_primary_membership");
        if (ArmyValid(state, primaryTarget) != true || primaryTarget.Troops is < 0 or > ushort.MaxValue) return null;
        BattleRemakeCurrentDamageReceipt? damage = _normalDamage.Resolve(state, caster, primaryTarget);
        if (damage is null) return null;
        //16FC12 uses unsigned16 comparison after16A6D0's low-word write.
        return damage.NativeFrameWord20 >= primaryTarget.Troops
            ? new(false, "tactic.native_predicted_damage") : new(true);
    }
    public bool? ReadArmyValidity(GameState state, UnitState unit, NativeSubdayMoment moment) => ArmyValid(state, unit);
    public bool? ReadExpirationLoggingForceNonzero(GameState state, UnitState unit) => ArmyValid(state, unit) is true ? unit.ForceId != 0 : null;
    public int? ReadAbnormalUpperBound(GameState state, UnitState unit, int nativeIndex) => Parameter(state, SimulationParameterKeys.Tactics.AbnormalCounterUpperBound);
    public bool? ReadSpecialWaitingModeActive(GameState state, UnitState unit) =>
        Bound(state) && state.NativeArmySimulation!.World.SelectedCommanderCount165D == 0 ? false : null;
    public int? ReadOrdinaryWaitingRate(GameState state, UnitState unit) => Parameter(state, SimulationParameterKeys.Tactics.OrdinaryWaitingRate);
    public int? ReadWaitingUpperBound(GameState state, UnitState unit, int nativeIndex) =>
        Parameter(state, nativeIndex switch { 0 => SimulationParameterKeys.Tactics.PrimaryWaitingCap, 1 => SimulationParameterKeys.Tactics.SecondaryWaitingCap, 2 => SimulationParameterKeys.Tactics.TertiaryWaitingCap,
            _ => throw new InvalidDataException("Owned waiting index is invalid.") });
}

using San14.Core;

namespace San14.Data;

/// <summary>Explicit fields of the generic native creation packet. These are
/// source numbers, not a guessed translation of Core military action keys.</summary>
public sealed record OriginalArmyCreationConfiguration(int NavalFormation10,
    int Action34, int Category44, int Flag48Input, int Flag4CInput)
{
    /// <summary>Independently written fields in SC564E60/TC5652E0.
    /// The equipped tactics and commander/troops are supplied by this game.</summary>
    public static OriginalArmyCreationConfiguration ObservedShortPacketFields => new(11, 13, 1, 0, 0) { TargetKind38 = 9 };
    public OriginalArmyOrdinaryNavalFallback? NavalFallback { get; init; }
    public byte? TargetKind38 { get; init; }
    /// <summary>Actual generic packet index16 (+40); creator clamps0..9.
    /// Unmodeled packet input is unknown, even when the free reset was0.</summary>
    public int? RouteHold40Input { get; init; }
}

/// <summary>Fresh CArmy constructor/reset plus the generic ordinary creator's
/// proven tactic-state fields. Existing deployment accounting and movement
/// remain in the explicitly provisional skeleton. Morale caps, final combat
/// attributes and unmodeled packet words are not produced by this projection.</summary>
public sealed class OriginalArmyConstruction : IPlayableArmyCreationRules
{
    public const string ProfileId = "san14:ce-owned-army-construction-20261002";
    private readonly OriginalArmyFormationWriterSource _formations;
    private readonly OriginalArmyCreationConfiguration _configuration;
    public string Language => _formations.CodeSource.Language;
    public string RuleVersion => _formations.CodeSource.RuleVersion;
    public OriginalArmyCreationConfiguration Configuration => _configuration;
    internal OriginalArmyFormationWriterSource FormationWriterSource => _formations;

    /// <summary>TC2D2CA0 uses PersonE2's current Base and22D880's
    /// Base4E→Object10 getter before storing Army2A/152. The creator applies
    /// its signed clamp even when an invalid Object returns BD10.</summary>
    public static ushort? ResolveCurrentHex2A(OriginalEventInitializationContext current, int officerId)
    {
        var person = current.ReadPersonLookupReference(officerId);
        if (person?.PhysicalSlot is not int physical || current.ReadPersonFirstLocation(physical) is not ushort location) return null;
        OriginalBaseReference home;
        try { home = OriginalBaseLookup.Find(location, current); }
        catch (InvalidDataException) { return null; }
        if (current.ReadBaseObjectLink(home) is not ushort link ||
            current.ReadObjectTableReference(link <= 3000 ? link : 0) is not { } obj ||
            current.ReadObjectNativeValidity(obj) is not bool valid) return null;
        if (!valid) return 48399;
        return current.ReadObjectHexIndex(obj) is ushort raw ? (ushort)Math.Clamp((int)raw, 0, 48399) : null;
    }

    private OriginalArmyConstruction(OriginalArmyFormationWriterSource formations,
        OriginalArmyCreationConfiguration configuration)
    { _formations = formations; _configuration = configuration; }

    public static OriginalArmyConstruction LoadCurrent(OriginalGameRepository repository,
        string language, OriginalArmyCreationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(configuration);
        var formations = OriginalArmyFormationWriter.ReadSource(
            repository.LoadSkillDefinitions($"0010_{language}/fixdataexce.s14"), language);
        return new(formations, configuration with { });
    }

    public PlayableArmyCreationResolution? Create(GameState state, PreparedDeployment deployment, UnitState army)
    {
        if (state.NativeClock is not { } clock || state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Language != Language || simulation.Parameters.RuleVersion != RuleVersion ||
            state.ContentHashes.GetValueOrDefault(_formations.Source.ResourceUri) != _formations.Source.Sha256 ||
            simulation.Forces.GetValueOrDefault(army.ForceId)?.Flags193 is not byte forceFlags ||
            army.OfficerId is < 1 or > 6000 || army.Id < 0 || state.Units.ContainsKey(army.Id) ||
            army.Troops is <= 0 or > ushort.MaxValue || army.FormationId != deployment.FormationId ||
            army.OfficerId != deployment.OfficerId || army.ForceId != deployment.ForceId ||
            army.SourceLocationId != deployment.SourceLocationId || army.TacticIds is not { } tactics ||
            !tactics.SequenceEqual(deployment.TacticIds) || tactics.Length > 3 || tactics.Any(id => id is < 1 or > 200) ||
            army.HexId < 0 || army.HexId >= checked(state.MapWidth * state.MapHeight) ||
            !state.Officers.TryGetValue(army.OfficerId, out OfficerState? commander) ||
            commander.ForceId != army.ForceId || commander.Deployed || commander.Presence != OfficerPresence.Present ||
            commander.LocationId != army.SourceLocationId || commander.DistrictId != deployment.DistrictId ||
            !state.Districts.TryGetValue(deployment.DistrictId, out DistrictState? district) || district.ForceId != army.ForceId)
            return null;

        OriginalArmyFormationWriteResult formation;
        try
        {
            formation = OriginalArmyFormationWriter.CreateOrdinary(_formations,
                new(deployment.FormationId, _configuration.NavalFormation10), _configuration.NavalFallback);
        }
        catch (InvalidDataException error) when (error.Message.StartsWith("Original Army naval ", StringComparison.Ordinal) &&
            error.Message.EndsWith(" is unknown.", StringComparison.Ordinal)) { return null; }
        if (formation.Bytes.Byte1B != deployment.FormationId || formation.Bytes.Byte1C is null) return null;

        int? initial = Parameter(simulation, SimulationParameterKeys.Tactics.InitialWaiting);
        int? adjustment = deployment.FormationId switch
        { 1 => Parameter(simulation, SimulationParameterKeys.Tactics.Formation1CreationWaitingAdjustment), 2 => Parameter(simulation, SimulationParameterKeys.Tactics.Formation2CreationWaitingAdjustment),
          14 => Parameter(simulation, SimulationParameterKeys.Tactics.Formation14CreationWaitingAdjustment), _ => 0 };
        int? reduction = (forceFlags & 0x40) == 0 ? 0 : Parameter(simulation, SimulationParameterKeys.Tactics.ForceFlagWaitingReduction);
        int? cap0 = Parameter(simulation, SimulationParameterKeys.Tactics.PrimaryWaitingCap), cap1 = Parameter(simulation, SimulationParameterKeys.Tactics.SecondaryWaitingCap),
            cap2 = Parameter(simulation, SimulationParameterKeys.Tactics.TertiaryWaitingCap);
        if (initial is null || adjustment is null || reduction is null || cap0 is null || cap1 is null || cap2 is null)
            return null;
        int slot;
        NativeArmyReturnResources? returnResources;
        NativeArmyOverrideTargetRuntime? overrideTarget;
        ushort[]? traitSlots;
        if (state.NativeArmyLifecycle is { } arena)
        {
            //2D2D62 scans the actual501 headers, including pending receivers.
            //The creator does not run a new constructor or reset this slot.
            slot = OriginalOwnedArmyLifecycleCurrent.FirstCreationSlot(state) ?? 0;
            if (slot == 0 || arena.Slots[slot].Gold5C is not int gold || arena.Slots[slot].Supplies60 is not int supplies)
                return null;
            returnResources = new(gold, supplies);
            overrideTarget = arena.Slots[slot].OverrideTarget;
            //This producer retains explicit reset receivers until construction.
            //An older foreign arena with no64 owner cannot acquire its zeros.
            traitSlots = arena.Revision == OriginalOwnedArmyLifecycleCurrent.Revision ? new ushort[36] : null;
        }
        else
        {
            var usedSlots = state.Units.Values.Where(unit => unit.NativeArmyOrigin is not null)
                .Select(unit => unit.NativeArmyOrigin!.NativeSlotIndex).ToHashSet();
            slot = Enumerable.Range(1, 500).FirstOrDefault(index => !usedSlots.Contains(index));
            returnResources = new(0, 0); traitSlots = new ushort[36];
            overrideTarget = new(0xFF, 0); //The actual new receiver runs2269E0.
        }
        if (slot == 0) return null;
        int requested = unchecked(unchecked(initial.Value + adjustment.Value) - reduction.Value);
        byte primary = CreationCounter(requested, cap0.Value);
        byte flags = (byte)((_configuration.Flag48Input == 1 ? 4 : 0) | (_configuration.Flag4CInput == 1 ? 8 : 0));
        // Fresh reset defines the three abnormal fields, six modifier/timers,
        // wounded quantity, and FF/0 no-owner pair. Registration does not set02.
        var runtime = new NativeArmyTacticRuntime(1, flags, primary,
            CreationCounter(cap1.Value, cap1.Value), CreationCounter(cap2.Value, cap2.Value),
            0, 0, 0, new sbyte[6], new byte[6], null, primary == 0 ? clock.UpdateOrdinal : null);
        return new(runtime, 0, new(ProfileId, Language, RuleVersion) { NativeSlotIndex = slot }, state.RandomState)
        { NativeReturnResources = returnResources, NativeOverrideTarget = overrideTarget,
          NativeRouteHold56 = _configuration.RouteHold40Input is int hold ? (byte)Math.Clamp(hold, 0, 9) : null,
          Configuration = new(formation.Bytes.Byte1B, formation.Bytes.Byte1C,
            (byte)Math.Clamp(_configuration.Action34, 0, 17), (byte)Math.Clamp(_configuration.Category44, 0, 2))
            { TraitSlots64 = traitSlots } };
    }

    /// <summary>Exact signed branch and low-byte write at SC2D2E64..2D2E90.
    /// A negative request skips the cap, while a negative cap can wrap on store.</summary>
    public static byte CreationCounter(int request, int cap) => request < 0 ? (byte)0 : unchecked((byte)Math.Min(request, cap));

    private static int? Parameter(NativeArmySimulationState simulation, string key) =>
        simulation.Parameters.Int32Values.TryGetValue(key, out int value) ? value : null;
}

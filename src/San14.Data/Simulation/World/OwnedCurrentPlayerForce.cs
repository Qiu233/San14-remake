using San14.Core;

namespace San14.Data;

/// <summary>Independent saved World.byte3A. The current parameter bank is only
/// its existing serialization container; this is a mutable World field, not an
/// image default or an alias of UI1656/session command authority.</summary>
public static class OwnedCurrentPlayerForce
{
    public const string VersionKey = "ce-world-current-player:version";
    public const string RawForce3AKey = "ce-world-current-player:byte3A";

    /// <summary>The finite SC33CF50/TC33CCA0 field producer, fed by the actual
    /// confirmed Person. Person118/E0 goes through the current District table
    /// and validity before the returned raw Force is clamped to0..51.</summary>
    public static byte? ResolveConfirmedPersonStore(SourceWorldContext current, int personId)
    {
        ArgumentNullException.ThrowIfNull(current);
        TransferPersonReference? person = current.ReadPersonLookupReference(personId);
        if (person?.PhysicalSlot is not int physical || current.ReadPersonOldForceSlot(physical) is not byte force)
            return null;
        return (byte)Math.Clamp((int)force, 0, 51);
    }

    /// <summary>Normal new-game confirmation's one explicit saved field store.
    /// The caller passes the confirmed ruler Person, not hovered Force/UI1656.
    /// Remaining native setter/application tails are outside this finite leaf.</summary>
    public static GameState ConfirmNewGamePlayerPerson(GameState state,
        SourceWorldContext current, int confirmedPersonId)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(current);
        if (!Bound(state) || state.Phase != TurnPhase.Strategy || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation!.Parameters.Int32Values.ContainsKey(VersionKey) ||
            state.NativeArmySimulation.Parameters.Int32Values.ContainsKey(RawForce3AKey) ||
            !state.Officers.TryGetValue(confirmedPersonId, out OfficerState? person) ||
            person.Role != OfficerRole.Ruler || current.ResolveOfficerSource(confirmedPersonId) is null ||
            person.ForceId is not int forceId || !state.Forces.TryGetValue(forceId, out ForceState? force) ||
            !force.IsActive || force.RulerId != person.Id ||
            ResolveConfirmedPersonStore(current, confirmedPersonId) is not byte raw)
            throw new InvalidDataException("Current player field needs the actual confirmed ruler Person of a fresh owned campaign.");
        NativeArmySimulationState simulation = state.NativeArmySimulation!;
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
            { Int32Values = new(simulation.Parameters.Int32Values) { [VersionKey] = 1, [RawForce3AKey] = raw } } } };
    }

    public static byte? ReadRawForce3A(GameState state) => Bound(state) &&
        state.NativeArmySimulation!.Parameters.Int32Values.GetValueOrDefault(VersionKey) == 1 &&
        state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(RawForce3AKey, out int raw) &&
        raw is >= 0 and <= byte.MaxValue ? (byte)raw : null;

    /// <summary>SC32F200/TC32EFA0 returns the current Force table reference.
    /// Known overflow goes through Force0; missing current owner stays unknown.
    /// It does not reread the ruler's later District or infer a player on load.</summary>
    public static ForceReference? ReadCurrentReference(GameState state,
        SourceWorldContext current) => ReadRawForce3A(state) is byte raw
            ? current.ReadForceTableReference(raw <= 51 ? raw : 0) : null;

    private static bool Bound(GameState state) => state.NativeClock is not null &&
        state.NativeArmySimulation?.Parameters is { } parameters &&
        (parameters.Language is ("SC" or "TC"));
}

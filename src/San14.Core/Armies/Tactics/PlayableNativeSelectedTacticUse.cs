namespace San14.Core;

public sealed record PlayableNativeSelectedTacticUse(int ActorUnitId, int PrimaryUnitId,
    PlayableTacticDefinition Definition, TacticUseSourceResolution OrderedEffects);

/// <summary>The actual automatic phase owner supplies a complete selected use.
/// This does not grant manual command permission or synthesize a pending order.</summary>
public interface IPlayableNativeSelectedTacticUseSource : IPlayableNativeTacticActivationTailSource
{
    PlayableNativeSelectedTacticUse? ResolveSelectedUse(GameState current);
}

public static partial class PlayableTacticRuntime
{
    /// <summary>One selected native non-damage activation transaction. Source
    /// effects read old waiting, then commander tail, then waiting is assigned.
    /// Unknown source/tail publishes nothing. Period and other pending uses are
    /// preserved; normal debit/tree/frame publication belongs to the pair owner.</summary>
    public static PlayableNativeTacticActivationResult? TryApplySelectedNativeUseAtCurrentTick(GameState state,
        IPlayableNativeSelectedTacticUseSource source)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(source);
        if (state.NativeClock is null || !source.HasNativeActivationCommanderTail)
            throw new InvalidDataException("Selected native use lacks its clock or necessary commander-tail owner.");
        GameState working = GameStateSnapshot.Detach(state);
        PlayableNativeSelectedTacticUse? use = source.ResolveSelectedUse(working);
        if (use is null) return null;
        var definition = use.Definition;
        PlayableTacticStateValidation.ValidateDefinition(definition);
        if (definition.NativeActivationWaiting is not { } waiting ||
            !working.Units.TryGetValue(use.ActorUnitId, out UnitState? actor) ||
            !working.Units.ContainsKey(use.PrimaryUnitId) || actor.TacticRuntime is null ||
            actor.NativeTacticRuntime is null || use.OrderedEffects.Steps is null)
            throw new InvalidDataException("Selected native use has incomplete current actor, primary, waiting or effects.");
        var units = new Dictionary<int, UnitState>(working.Units);
        var officers = new Dictionary<int, OfficerState>(working.Officers);
        ICampaignResourceState? resourceState = working.ResourceState;
        var details = new List<TacticReportEntry>();
        int reportPeriod = checked(state.PeriodNumber + 1);
        details.Add(new("used", reportPeriod, actor.ForceId, actor.Id, actor.OfficerId,
            definition.TacticId, use.PrimaryUnitId) { NativeUpdateOrdinal = state.NativeClock.UpdateOrdinal });
        foreach (TacticSourceEffectStep step in use.OrderedEffects.Steps)
        {
            if (!units.TryGetValue(step.TargetUnitId, out UnitState? target))
                throw new InvalidDataException("Selected native effect lost its current target.");
            PlayableTacticStateValidation.ValidateDefinition(definition with { Effects = step.Effects });
            foreach (PlayableTacticEffect effect in step.Effects)
            {
                UnitState changed = ApplyCurrentNativeEffect(working, target, effect);
                if (changed.Troops <= 0 || changed.Morale <= 0)
                    throw new InvalidDataException("Selected native non-damage execution does not own retirement.");
                int amount = effect.Kind == PlayableTacticEffectKind.ClearAbnormalStatuses
                    ? target.TacticRuntime!.Statuses.Length - changed.TacticRuntime!.Statuses.Length : effect.Amount;
                units[target.Id] = changed; target = changed;
                details.Add(new("effect", reportPeriod, actor.ForceId, actor.Id, actor.OfficerId,
                    definition.TacticId, target.Id, effect.Kind, amount)
                    { NativeUpdateOrdinal = state.NativeClock.UpdateOrdinal });
            }
            resourceState = step.ResourceStateAfter ?? resourceState;
        }
        GameState afterEffects = working with { Units = units, Officers = officers,
            RandomState = use.OrderedEffects.RandomState,
            ResourceState = resourceState };
        OfficerState? commander = source.ResolveNativeActivationCommanderTail(afterEffects,
            units[actor.Id], definition);
        if (commander is null) return null;
        if (commander.Id != actor.OfficerId || !officers.ContainsKey(commander.Id))
            throw new InvalidDataException("Selected activation tail returned another commander.");
        officers[commander.Id] = commander;
        UnitState currentActor = units[actor.Id];
        var native = currentActor.NativeTacticRuntime!;
        var ready = new Dictionary<int, int>(currentActor.TacticRuntime!.ReadyAtPeriod);
        ready.Remove(definition.TacticId);
        units[actor.Id] = currentActor with { NativeTacticRuntime = native with {
            Waiting20 = waiting.Waiting20, Waiting21 = waiting.Waiting21, Waiting22 = waiting.Waiting22,
            PrimaryWaitingReadyAtUpdateOrdinal = waiting.Waiting20 == 0 ? state.NativeClock.UpdateOrdinal : null },
            TacticRuntime = currentActor.TacticRuntime with { ReadyAtPeriod = ready } };
        var summaries = new[] { new PeriodReportEntry("tactic_used", actor.ForceId, null,
            actor.OfficerId, definition.TacticId, actor.HexId, working.Units[use.PrimaryUnitId].HexId) };
        GameState result = afterEffects with { Units = units, Officers = officers,
            TacticReports = (working.TacticReports ?? []).Concat(details).ToArray() };
        NativeSubdayStateValidation.Validate(result);
        return new(result, details.ToArray(), summaries);
    }

    // Shared by commanded and automatic native execution. Generic damage,
    // morale/status periods and retirement stay in their existing scheduler.
    private static UnitState ApplyCurrentNativeEffect(GameState state, UnitState target, PlayableTacticEffect effect)
    {
        if (effect.Kind == PlayableTacticEffectKind.ArmyHealing)
        {
            if (target.WoundedTroops is null || effect.HealingResult is not { } healed ||
                (long)healed.TroopsAfter - target.Troops != effect.Amount)
                throw new InvalidDataException("Native healing has unknown wounds or a stale troop receipt.");
            return target with { Troops = healed.TroopsAfter, WoundedTroops = healed.WoundedAfter };
        }
        if (effect.Kind == PlayableTacticEffectKind.NativeMoraleChange)
        {
            if (target.NativeMorale1A is not byte before || effect.NativeMoraleAfter is not byte after ||
                after - before != effect.Amount)
                throw new InvalidDataException("Native morale has an unknown byte or stale absolute receipt.");
            return target with { NativeMorale1A = after };
        }
        if (effect.Kind is not (PlayableTacticEffectKind.NativeAbnormalChange or PlayableTacticEffectKind.ClearAbnormalStatuses) ||
            target.NativeTacticRuntime is not { } native || effect.NativeAbnormalAfter is not { } abnormal)
            throw new InvalidDataException("Selected automatic program reached an unsupported native effect.");
        NativeSubdayStateValidation.ValidateOwner(state, abnormal.ProvocationOwner);
        UnitState changed = target with { NativeTacticRuntime = native with {
            Abnormal23 = abnormal.Abnormal23, Abnormal24 = abnormal.Abnormal24, Abnormal25 = abnormal.Abnormal25,
            ProvocationOwner = abnormal.ProvocationOwner } };
        changed = ArmyOverrideTargetWrites.Apply(changed, abnormal.OverrideTarget);
        if (effect.Kind == PlayableTacticEffectKind.ClearAbnormalStatuses)
            changed = changed with { TacticRuntime = changed.TacticRuntime! with {
                Statuses = changed.TacticRuntime!.Statuses.Where(status => !Abnormal(status)).ToArray() } };
        return changed;
    }
}

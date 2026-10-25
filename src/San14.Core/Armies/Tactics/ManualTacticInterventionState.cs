using System.Collections.Immutable;
using System.Text.Json.Serialization;
namespace San14.Core;

public enum ManualTacticTargetKind { Army, BaseLocation, Hex }
/// <summary>Army targets name a reusable receiver, rather than the unit that
/// happened to occupy it when the command was issued.</summary>
public sealed record ManualTacticTarget(ManualTacticTargetKind Kind, int Id);
public sealed record ManualTacticRequest(ArmyReceiverId Actor, int TacticId, ManualTacticTarget? Target);
public enum TacticPresentationKind { OrdinaryEffects, UniqueDirection }
public sealed record TacticEffectPresentation(int Instruction, ArmyReceiverId Target,
    PlayableTacticEffectKind Kind, int Amount, TacticArmyHealingResult? Healing,
    NativeTacticAbnormalAssignment? Abnormal, int? MoraleAfter);
/// <summary>Result identities refer to allocated receivers, or a canonical City/Gate location.</summary>
public enum TacticResultTargetKind { ArmyReceiver, BaseLocation, MapObjectReceiver }
/// <summary>A captured effect marker. Amount retains the source low word, rather than
/// a report delta or a reconstructed applied loss. HexId is the captured target Hex.</summary>
public sealed record TacticResultPresentation(TacticResultTargetKind TargetKind,
    int TargetId, int HexId, ushort Amount);
public sealed record TacticActorPresentation(ArmyReceiverId Actor, int TacticId,
    ManualTacticTarget? Target, int[][] InstructionArmyReceivers)
{
    public int? BaseCasterLocationId { get; init; }
    [JsonRequired] public TacticEffectPresentation[] Effects { get; init; } = [];
    /// <summary>When present, rows 0 and 1 retain all actual results in instruction order,
    /// including repeated targets. Null retains the legacy Army-only representation.</summary>
    public TacticResultPresentation[][]? InstructionResults { get; init; }
}
public sealed record TacticPresentationRequest(long Sequence, TacticPresentationKind Kind,
    TacticActorPresentation[] Actors);
public sealed record TacticPresentationReceipt(long Sequence, TacticPresentationKind Kind);
/// <summary>The real ordinary node has begun. Result publication is presentation
/// only; all gameplay numbers were applied before this queue entry was created.</summary>
public sealed record OrdinaryTacticPlaybackState(long Sequence, bool ResultsPublished);

/// <summary>One campaign's commanded intervention and the independent effect
/// manager/Direction queues. An absent owner is unknown, not an empty manager.</summary>
public sealed record ManualTacticInterventionState(ManualTacticRequest? Pending,
    bool EffectBusy, long LastPresentationSequence,
    TacticPresentationRequest[] Effects, TacticPresentationRequest[] Directions)
{
    public ImmutableArray<OrdinaryTacticPlaybackState> OrdinaryPlaybacks { get; init; } = [];
    public static ManualTacticInterventionState Empty => new(null, false, 0, [], []);
    public ManualTacticInterventionState Copy() => this with
    { Effects = Effects.Select(CopyRequest).ToArray(), Directions = Directions.Select(CopyRequest).ToArray() };
    public static TacticPresentationRequest CopyRequest(TacticPresentationRequest value) => value with
    {
        Actors = value.Actors.Select(a => a with
        {
            InstructionArmyReceivers = a.InstructionArmyReceivers.Select(x => x.ToArray()).ToArray(),
            Effects = a.Effects.ToArray(),
            InstructionResults = a.InstructionResults?.Select(row => row.ToArray()).ToArray()
        }).ToArray()
    };
    public ManualTacticInterventionState Enqueue(TacticPresentationKind kind, TacticActorPresentation[] actors)
    {
        //168C40 applies the ordinary manager's outer admission before selecting
        //the ordinary or Direction route. Gameplay numbers are already applied.
        if (Effects.Length >= 563) return this;
        long sequence = checked(LastPresentationSequence + 1);
        var request = CopyRequest(new(sequence, kind, actors));
        return kind switch
        {
            TacticPresentationKind.OrdinaryEffects => this with { LastPresentationSequence = sequence, Effects = [.. Effects, request] },
            TacticPresentationKind.UniqueDirection => this with { LastPresentationSequence = sequence, Directions = [.. Directions, request] },
            _ => throw new InvalidDataException("Unknown commanded tactic presentation.")
        };
    }
    public ManualTacticInterventionState Acknowledge(TacticPresentationReceipt receipt)
    {
        if (receipt.Kind == TacticPresentationKind.OrdinaryEffects)
            throw new InvalidOperationException("Ordinary tactic nodes require their actual start, result and retirement lifecycle.");
        var queue = receipt.Kind == TacticPresentationKind.UniqueDirection ? Directions : Effects;
        if (!Enum.IsDefined(receipt.Kind) || queue.Length == 0 || queue[0].Sequence != receipt.Sequence)
            throw new InvalidDataException("Commanded tactic presentation receipt is stale or out of order.");
        return receipt.Kind == TacticPresentationKind.UniqueDirection
            ? this with { Directions = Directions[1..] } : this with { Effects = Effects[1..] };
    }
    public static void Validate(ManualTacticInterventionState? value)
    {
        if (value is null) return;
        if (value.LastPresentationSequence < 0 || value.Effects is null || value.Directions is null)
            throw new InvalidDataException("Commanded tactic manager is incomplete.");
        if (value.OrdinaryPlaybacks.IsDefault || value.OrdinaryPlaybacks.Length > value.Effects.Length)
            throw new InvalidDataException("Ordinary tactic playback collection is incomplete.");
        for (int index = 0; index < value.OrdinaryPlaybacks.Length; index++)
            if (value.OrdinaryPlaybacks[index] is not { Sequence: > 0 } playback ||
                value.Effects[index] is not { Kind: TacticPresentationKind.OrdinaryEffects } request ||
                request.Sequence != playback.Sequence)
                throw new InvalidDataException("Ordinary tactic playbacks do not follow their manager's append order.");
        if (value.Pending is { } pending && (pending.Actor.Value < 1 || pending.TacticId is < 0 or > 200))
            throw new InvalidDataException("Commanded tactic request has an invalid receiver.");
        if (value.Pending?.Target is { Kind: not ManualTacticTargetKind.Hex })
            throw new InvalidDataException("A saved manual tactic request must retain its captured Hex.");
        ValidateTarget(value.Pending?.Target);
        var sequences = new HashSet<long>();
        foreach (var (queue, kind) in new[] { (value.Effects, TacticPresentationKind.OrdinaryEffects), (value.Directions, TacticPresentationKind.UniqueDirection) })
        {
            long previous = 0;
            foreach (var request in queue)
            {
                if (request is null || request.Kind != kind || request.Sequence <= previous ||
                    request.Sequence > value.LastPresentationSequence || !sequences.Add(request.Sequence) || request.Actors is not { Length: > 0 })
                    throw new InvalidDataException("Commanded tactic presentation queue is invalid.");
                previous = request.Sequence;
                foreach (var actor in request.Actors)
                {
                    if (actor is null || (actor.BaseCasterLocationId is int sourceBase
                            ? sourceBase is < 1 or > 61 || actor.Actor.Value != 0 : actor.Actor.Value < 1) ||
                        actor.TacticId is < 1 or > 200 ||
                        actor.InstructionArmyReceivers is not { Length: 2 } || actor.InstructionArmyReceivers.Any(x => x is null || x.Any(id => id < 1)) ||
                        actor.Effects is null || actor.Effects.Any(e => e is null || e.Instruction is < 0 or > 1 || e.Target.Value < 1 || !Enum.IsDefined(e.Kind)))
                        throw new InvalidDataException("Commanded tactic presentation actor is incomplete.");
                    ValidateTarget(actor.Target);
                    if (actor.InstructionResults is { } results)
                    {
                        if (results.Length != 2 || results.Any(row => row is null || row.Any(result =>
                            result is null || !Enum.IsDefined(result.TargetKind) || result.TargetId < 1 ||
                            result.HexId is < 0 or >= 48400 ||
                            result.TargetKind == TacticResultTargetKind.BaseLocation && result.TargetId > 61)))
                            throw new InvalidDataException("Tactic presentation result markers are invalid.");
                        for (int instruction = 0; instruction < 2; instruction++)
                            if (!actor.InstructionArmyReceivers[instruction].SequenceEqual(results[instruction]
                                .Where(result => result.TargetKind == TacticResultTargetKind.ArmyReceiver)
                                .Select(result => result.TargetId)))
                                throw new InvalidDataException("Tactic Army receiver projection does not match its result markers.");
                    }
                    foreach (var effect in actor.Effects)
                    {
                        if (!actor.InstructionArmyReceivers[effect.Instruction].Contains(effect.Target.Value) ||
                            (effect.Kind == PlayableTacticEffectKind.ArmyHealing) != (effect.Healing is not null) ||
                            (effect.Kind is PlayableTacticEffectKind.NativeAbnormalChange or PlayableTacticEffectKind.ClearAbnormalStatuses) != (effect.Abnormal is not null) ||
                            (effect.Kind == PlayableTacticEffectKind.NativeMoraleChange) != (effect.MoraleAfter is not null) ||
                            effect.Healing is { } healed && (healed.TroopsAfter is < 0 or > ushort.MaxValue || healed.WoundedAfter is < 0 or > ushort.MaxValue))
                            throw new InvalidDataException("Tactic presentation has an inconsistent numerical receipt.");
                    }
                }
            }
        }
    }
    public static void Validate(GameState state)
    {
        var value = state.ManualTacticIntervention; Validate(value);
        if (value is null || value.Pending is null && value.Effects.Length == 0 && value.Directions.Length == 0) return;
        var roster = state.ArmyLifecycle?.Receivers ?? throw new InvalidDataException("Manual tactic receivers have no campaign roster.");
        if (value.Pending is { } pending && roster.Find(pending.Actor) is null)
            throw new InvalidDataException("Manual tactic actor is outside the saved campaign roster.");
        foreach (var actor in value.Effects.Concat(value.Directions).SelectMany(x => x.Actors))
        {
            if ((actor.BaseCasterLocationId is int sourceBase
                    ? sourceBase <= 51 ? !state.Cities.ContainsKey(sourceBase) : !state.Gates.ContainsKey(sourceBase)
                    : roster.Find(actor.Actor) is null) ||
                actor.InstructionArmyReceivers.SelectMany(x => x).Any(id => roster.Find(new(id)) is null))
                throw new InvalidDataException("Tactic presentation names a foreign campaign receiver.");
            if (actor.InstructionResults is not { } results) continue;
            foreach (var result in results.SelectMany(row => row))
            {
                // Army identities were checked through the exact ordered projection above.
                // Retired receivers remain allocated; neither current occupancy nor pose
                // is required to match a previously captured presentation marker.
                bool allocated = result.TargetKind switch
                {
                    TacticResultTargetKind.ArmyReceiver => true,
                    TacticResultTargetKind.BaseLocation => result.TargetId <= 51
                        ? state.Cities.ContainsKey(result.TargetId) : state.Gates.ContainsKey(result.TargetId),
                    TacticResultTargetKind.MapObjectReceiver => state.MapObjects?.Find(new(result.TargetId)) is not null,
                    _ => false
                };
                if (!allocated)
                    throw new InvalidDataException("Tactic presentation result names a foreign campaign target.");
            }
        }
    }
    private static void ValidateTarget(ManualTacticTarget? target)
    {
        if (target is not null && (!Enum.IsDefined(target.Kind) || target.Id < 0 ||
            target.Kind == ManualTacticTargetKind.Army && target.Id < 1 ||
            target.Kind == ManualTacticTargetKind.Hex && target.Id >= 48400))
            throw new InvalidDataException("Commanded tactic target is invalid.");
    }
}
public sealed partial record GameState
{
    public ManualTacticInterventionState? ManualTacticIntervention { get; init; }
}

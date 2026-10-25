using San14.Core;

namespace San14.Data;

/// <summary>The original caster and ordered result payload remain intact.
/// Base casters retain ArmyReceiverId0 and their independent Base identity.</summary>
public sealed record OrdinaryTacticActorPublication(TacticActorPresentation Actor,
    OrdinaryTacticPresentationSource Source)
{
    //172C80/172D50 only call405AC0 for an actual Army receiver. A Base
    //caster can show its name and beams without inventing an Army voice.
    public int? CasterSpatialVoiceId => Actor.BaseCasterLocationId is null ? Source.ArmySpatialVoiceId : null;
}

public sealed record OrdinaryTacticPresentationPublication(long Sequence,
    IReadOnlyList<OrdinaryTacticActorPublication> Actors)
{
    public OrdinaryTacticBeamPublication Beams { get; } = OrdinaryTacticBeamPublication.Read(Actors);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Publishes the normal resource declarations for an existing
    /// ordinary-effect request. It does not reselect targets, alter App/CRT,
    /// claim native allocation, or acknowledge an unimplemented host.</summary>
    public AreaAdministrationQuery<OrdinaryTacticPresentationPublication> QueryOrdinaryTacticPresentation(
        GameState state, TacticPresentationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            if (request.Kind != TacticPresentationKind.OrdinaryEffects)
                throw new InvalidDataException("tactic.presentation.unique_direction_host");
            if (request.Actors.Length is < 1 or > 11)
                throw new InvalidDataException("tactic.presentation.linked_actor_count");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var actors = new List<OrdinaryTacticActorPublication>(request.Actors.Length);
            foreach (var actor in request.Actors)
            {
                if (actor.TacticId is < 1 or > 200 || actor.TacticId >= _catalog.Rows.Count ||
                    actor.InstructionResults is not { Length: 2 })
                    throw new InvalidDataException("tactic.presentation.source_payload");
                var source = _catalog.Rows[actor.TacticId];
                if (source.Slot != actor.TacticId)
                    throw new InvalidDataException("tactic.presentation.definition_identity");
                var tactic = current.ReadSkillTacticReference(actor.TacticId)
                    ?? throw new InvalidDataException("tactic.presentation.definition_pointer");
                bool neutral = current.ReadTacticEffectOpcode(tactic, 0) == 20 ||
                    current.ReadTacticEffectOpcode(tactic, 1) == 20;
                actors.Add(new(actor, TacticPresentationSource.Read(source, neutral)));
            }
            return AreaAdministrationQuery<OrdinaryTacticPresentationPublication>.Known(
                new(request.Sequence, actors.AsReadOnly()));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OrdinaryTacticPresentationPublication>.Unknown(error.Message); }
    }
}

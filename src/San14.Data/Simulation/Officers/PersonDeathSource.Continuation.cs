using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IRulerSuccessionRules
{
    public CommandResult ConfirmRulerSuccession(GameState state, ConfirmRulerSuccessionOrder order, CommandContext context)
    {
        if (state.PersonDeathContinuation is not { } saved || saved.PendingSuccession.ForceId != order.ForceId ||
            !saved.PendingSuccession.CandidateOfficerIds.Contains(order.OfficerId) || context.DelegatedDistrictId is not null)
            return CommandResult.Reject("This successor is not awaiting selection.", "ruler.succession_pending");
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var result = new PersonDeathSource(this, state, world.Context, world.Pool).ResumeRulerSuccession(saved, order.OfficerId);
            var working = result.State;
            if (result.NativeReturn == 1)
            {
                if (working.ArmyDefeatContinuation is { } army)
                    working = working with { ArmyDefeatContinuation = army with { DeathReturnedEarly = true } };
                if (working.NativeNormalTurnContinuation?.NextAction is 3 or 7 or 8)
                    working = working with { NativeNormalTurnContinuation = null };
            }
            working = PublishNormalTurnCurrent(working, world.Context, world.Pool);
            working = PersonAssignmentPrecallInputs.PublishCurrent(working, world.Context);
            return CommandResult.Accept(working);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    internal sealed partial class PersonDeathSource
    {
        private Func<RulerSuccessionRequest, PersonDeathContinuation>? continuationFactory;

        private PersonDeathContinuation CaptureDeathContinuation(int person, BaseReference home, HexReference position,
            ForceReference force, byte status, int office, TransferPersonReference sworn, int stage,
            PersonLinkedEntityReference associated, RulerSuccessionRequest request)
            => new(Identity(person), person, killer.PhysicalSlot, deathCause, retireAssociatedArmy,
                emitReports, emitHistory, suppressAllReports, CaptureBase(home), CaptureBase(deathOriginalCity),
                position.PhysicalSlot, force.PhysicalSlot, status, office, sworn.PhysicalSlot, stage, associated.Identity, request);

        private static PersonDeathBaseContext? CaptureBase(BaseReference? value) => value is null ? null :
            new(value.Kind == BaseKind.City ? PersonDeathBaseKind.City : PersonDeathBaseKind.Gate, value.PhysicalSlot);
        private static BaseReference RestoreBase(PersonDeathBaseContext? value) => value is null
            ? throw Missing("continuation_Base_owner") :
            new(value.Kind == PersonDeathBaseKind.City ? BaseKind.City : BaseKind.Gate, value.PhysicalSlot);

        private int? SuspendRulerSuccession(int deceased, ForceReference force, HexListHandle candidates)
        {
            var ids = ImmutableArray.CreateBuilder<int>();
            for (var node = ItemHead(candidates); node.Identity.HasValue; node = ItemNext(node))
                ids.Add(Identity(ItemPerson(node)));
            if (ids.Count == 0) throw Missing("successor_empty_candidates");
            int forceId = Known(force.PhysicalSlot, "successor_Force_pointer");
            var prompt = package.FormatNormalTurnReport(latest, current, 0x2A3D, new(Force0: forceId));
            latest = prompt.State;
            var warning = package.FormatNormalTurnReport(latest, current, 0x2A3C, new(Force0: forceId));
            latest = warning.State;
            var request = new RulerSuccessionRequest(forceId, Identity(deceased), ids.ToImmutable(), prompt.Text, warning.Text);
            latest = latest with { PersonDeathContinuation = (continuationFactory ?? throw Missing("continuation_capture"))(request) };
            return null;
        }

        internal (GameState State, int? NativeReturn) ResumeRulerSuccession(PersonDeathContinuation saved, int officerId)
        {
            try
            {
                int deceased = saved.PersonPhysical;
                if (Identity(deceased) != saved.OfficerId) throw Missing("continuation_deceased_identity");
                int successor = Known(current.ReadPersonLookupReference(officerId)?.PhysicalSlot, "continuation_successor_pointer");
                if (!saved.PendingSuccession.CandidateOfficerIds.Contains(officerId) ||
                    !Known(current.ReadPersonNativeValidity(successor), "continuation_successor_validity") ||
                    Status(successor) is < 1 or > 4 ||
                    Known(current.ReadPersonOldForceSlot(successor), "continuation_successor_Force") != saved.PendingSuccession.ForceId ||
                    (Known(current.ReadOwnedReturnPersonFlags(successor), "continuation_successor_flags") & 0x4000) != 0)
                    throw Missing("continuation_successor_no_longer_eligible");
                deathCause = saved.Cause; killer = new(saved.KillerPhysical);
                retireAssociatedArmy = saved.RetireAssociatedArmy; emitReports = saved.EmitReports;
                emitHistory = saved.EmitHistory; suppressAllReports = saved.SuppressReports;
                deathOriginalForce = new(saved.OriginalForcePhysical);
                deathOriginalCity = RestoreBase(saved.OriginalCity);
                latest = latest with { PersonDeathContinuation = null };
                CompleteRulerSuccessorSelection(deceased, successor, deathOriginalForce);
                if (!Valid(killer))
                    foreach (var item in PersonItems(deceased))
                    { ClearItemFlag3(item); WriteDirectHeldItem(item, (ushort)Math.Min((int)Identity(successor), 6000)); }
                return FinishOrdinaryDeath(deceased, RestoreBase(saved.OriginalBase), deathOriginalForce,
                    saved.OriginalStatus, new(saved.OriginalSwornPersonPhysical), saved.OriginalMeritStage,
                    new(saved.AssociatedCommandId));
            }
            finally { ReleaseRelationships(); }
        }
    }
}

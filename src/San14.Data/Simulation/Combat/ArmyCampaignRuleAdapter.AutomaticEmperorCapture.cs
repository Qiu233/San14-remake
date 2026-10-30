using San14.Core;

namespace San14.Data;

//Explicit invocation values override the real saved configuration.
//Absent initialization remains unknown; a virtual PE zero is not a default.
internal sealed record AutomaticEmperorCaptureInputs(int? NpcReceptionTendencyMinimum,
    int? AbolitionWitnessTendencyMinimum, StoryForcedEmperorCaptureInputs? Business);

internal sealed record AutomaticEmperorCapturePreparation(GameState State,
    AutomaticEmperorCaptureProgress? Progress);
internal sealed record AutomaticEmperorCaptureAdvance(GameState State,
    AutomaticEmperorCaptureProgress Progress, IReadOnlyList<CampaignEffectRequest> Effects,
    bool NeedsPresentationReceipt);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2F6890 mode0 at the actual post-transfer service call. The
    /// excluded Force is the caller's real optional argument, not an inferred
    /// previous owner. The caller has already tested oldOwner != newOwner and
    /// the new owner's native validity, as26DA10 does.</summary>
    internal AutomaticEmperorCapturePreparation PrepareAutomaticEmperorCaptureCurrent(
        GameState state, SourceWorldContext current, BaseReference city,
        ForceReference? excludedForce, ForceReference capturingForce,
        AutomaticEmperorCaptureInputs? inputs = null)
    {
        int raw = EmperorAutomaticNeed(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654");
        if (raw == 0) return new(state, null);
        int emperor = EmperorAutomaticNeed((raw <= 6000 ? current.ReadPersonLookupReference(raw) :
            current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Emperor_pointer");
        var position = PersonPositionRepair.DecodeSecondLocation(EmperorAutomaticNeed(
            current.ReadPersonSecondLocation(emperor), "Emperor_E4"));
        int cityPhysical = EmperorAutomaticNeed(city.PhysicalSlot, "City_identity");
        // There is intentionally no extra Emperor native-validity test here.
        if (position.Kind != 5 || position.Index != cityPhysical) return new(state, null);
        int owner = EmperorAutomaticNeed(capturingForce.PhysicalSlot, "new_Force_identity");
        int rulerRaw = EmperorAutomaticNeed(current.ReadForceRecord(owner)?.RawRuler10, "new_Force_ruler10");
        int ruler = EmperorAutomaticNeed((rulerRaw <= 6000 ? current.ReadPersonLookupReference(rulerRaw) :
            current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "ruler_pointer");
        int kingdom = EmperorAutomaticNeed(current.ReadForceKingdomNameIndex(owner), "new_Force48");
        int rank = EmperorAutomaticNeed(current.ReadMoraleWaveForceRank47(owner), "new_Force47");
        bool player = TalentForceIsPlayerCurrent(state, current, capturingForce);
        int emperorId = EmperorAutomaticNeed(current.ReadPersonIdentity(emperor), "Emperor10");
        int rulerId = EmperorAutomaticNeed(current.ReadPersonIdentity(ruler), "ruler10");
        int location = EmperorAutomaticNeed(current.ReadBaseObjectLink(city), "City4E");
        var context = new AutomaticEmperorCaptureContext(cityPhysical, location, emperor,
            emperorId, owner, ruler, rulerId, excludedForce?.PhysicalSlot, player, kingdom);
        inputs = EmperorCaptureConfiguration.Resolve(state, inputs);
        int? mode = kingdom == 6 || kingdom is >= 38 and <= 43 || rank == 1 ? 2 : null;
        string? question = null;
        if (mode is null && player)
        {
            // Format once, adopting the formatter's real application RNG state.
            // Presentation/receipt ownership belongs to the actual capture host.
            if (EmperorAutomaticNeed(ReadApplicationReturnToTitlePending(), "choice_return_to_title"))
                throw EmperorAutomaticMissing("choice_application_returning_to_title");
            var formatted = FormatNormalTurnReport(state, current, 0x2AEA,
                new(Base0: checked((ushort)location), Person0: emperorId));
            state = formatted.State;
            question = formatted.Text;
        }
        else if (mode is null)
        {
            int tendency = EmperorAutomaticNeed(RulerCaptureReleaseInputs.ReadCurrentWord(state, ruler, 0x17A), "new_ruler17A");
            mode = tendency < EmperorAutomaticNeed(inputs?.NpcReceptionTendencyMinimum,
                "configuration.npc_reception_tendency_minimum") ? 2 : 1;
        }
        return new(state, new(context, question is null ? AutomaticEmperorCapturePhase.Opening :
            AutomaticEmperorCapturePhase.AwaitingChoice, mode, question)
        {
            PendingDecision = question is null ? null : new(owner, location, emperorId, question)
        });
    }

    /// <summary>Continue only after the enclosing owner has consumed the
    /// preceding real presentation receipts. A nullable reply is meaningful
    /// only at AwaitingChoice; it never silently becomes No.</summary>
    internal AutomaticEmperorCaptureAdvance AdvanceAutomaticEmperorCaptureCurrent(
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        AutomaticEmperorCaptureProgress progress, AutomaticEmperorCaptureInputs? inputs = null,
        bool? playerReceptionReply = null)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw EmperorAutomaticMissing("current_pool");
        if (progress.NeedsPresentationReceipt) throw EmperorAutomaticMissing("presentation_receipt_pending");
        if (progress.Phase == AutomaticEmperorCapturePhase.AwaitingChoice)
        {
            if (playerReceptionReply is not bool reply)
                return new(state, progress, [], false);
            progress = progress with { SelectedMode = reply ? 1 : 2,
                Phase = AutomaticEmperorCapturePhase.Opening, ChoiceText = null, PendingDecision = null };
        }
        else if (playerReceptionReply.HasValue) throw EmperorAutomaticMissing("reply_outside_choice");
        int mode = progress.SelectedMode ?? throw EmperorAutomaticMissing("resolved_mode");
        if (mode is not (1 or 2)) throw EmperorAutomaticMissing("resolved_mode");
        var context = progress.Context;
        var effects = new List<CampaignEffectRequest>();
        inputs = EmperorCaptureConfiguration.Resolve(state, inputs);
        var source = new ForcedEmperorCaptureSource(this, state, current, pool,
            new(BaseKind.City, context.CityPhysicalSlot), context.ExcludedForcePhysicalSlot is int old ? new(old) : null,
            new(context.NewForcePhysicalSlot), mode, effects, inputs?.Business);
        return source.AdvanceAutomatic(progress, inputs);
    }

    private static T EmperorAutomaticNeed<T>(T? value, string field) where T : struct
        => value ?? throw EmperorAutomaticMissing(field);
    private static InvalidDataException EmperorAutomaticMissing(string field)
        => new("capture.automatic-emperor." + field);
}

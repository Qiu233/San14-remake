using San14.Core;

namespace San14.Data;

/// <summary>The saved portrait selected when the owned unit-action balloon
/// was constructed. ForceColor is the original palette identity, not a Force ID.</summary>
public sealed record BaseMapSpeechSpeakerPublication(OfficerPortraitRoute Portrait, byte ForceColor);

public sealed partial class ArmyCampaignRuleAdapter : IBaseAbnormalFeedbackRules
{
    /// <summary>Retains a legacy notification whose source-time view was not
    /// recorded. New writers admit the attempt before leaving their source
    /// operation; a later world is never substituted for that earlier source.</summary>
    public BaseAbnormalFeedbackPreparationResult PrepareBaseAbnormalFeedback(GameState state,
        BaseAbnormalFeedbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            if (request.Action is not (15 or 16) || request.BaseLocationId is < 1 or > 61 ||
                request.OfficerId is < 0 or > 6000 || request.HexId is < 0 or > 48400)
                throw MapSpeechMissing("Base_feedback_shape");
            //World180 notifications did not retain the portrait selected by
            //1AF720. A later world cannot reconstruct that source-time view.
            //New writers call the same-current admission below directly.
            throw MapSpeechMissing("Base_feedback_source_time_portrait");
        }
        catch (InvalidDataException error) { return new(state, false, error.Message); }
    }

    internal GameState QueueBaseAbnormalFeedbackCurrent(GameState state, SourceWorldContext current,
        BaseAbnormalFeedbackRequest request)
    {
        //The older notification's field name is BaseLocationId, but its
        //producer captured word4E, exactly the kind2 packet identity.
        //Keep that message operand separate from the canonical marker ID.
        var receiver = BaseLookup.Find(request.BaseLocationId, current);
        int physical = MapSpeechNeed(receiver.PhysicalSlot, "Base_feedback_pointer");
        int location = receiver.Kind == BaseKind.City ? physical : checked(physical + 51);
        if (location is < 1 or > 61 || current.ReadBaseObjectLink(receiver) != request.BaseLocationId)
            throw MapSpeechMissing("Base_feedback_identity");
        return QueueArmyMapSpeechCurrent(state, current, 0, request.OfficerId,
            request.Action, request.HexId, baseSpeakerLocationId: location,
            baseSpeakerObjectId: request.BaseLocationId);
    }

    /// <summary>Reads the named face resource for the appearance retained by
    /// the owned balloon, including a priority replacement's inherited view.</summary>
    public AreaAdministrationQuery<BaseMapSpeechSpeakerPublication> QueryBaseMapSpeechSpeaker(
        GameState state, ArmyMapSpeechEntry entry)
    {
        try
        {
            ushort appearance = entry.BaseSpeakerAppearanceIndex ?? throw MapSpeechMissing("owned_face_appearance");
            byte colour = entry.BaseSpeakerForceColor ?? throw MapSpeechMissing("owned_face_colour");
            var current = CapturePersonPresentationCurrent(state);
            var portrait = ReadOfficerPortraitAppearance(state, current, _definitions,
                entry.OfficerId, appearance, ArmyMapUiSources.Create());
            if (portrait.Kind != OfficerPortraitRouteKind.StockNamed)
                throw new InvalidDataException(portrait.UnresolvedInput ?? "army-map-speech.owned_face_route");
            return AreaAdministrationQuery<BaseMapSpeechSpeakerPublication>.Known(new(portrait, colour));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<BaseMapSpeechSpeakerPublication>.Unknown(error.Message); }
    }

    private sealed record MapSpeechSpeakerImage(ushort AppearanceIndex, byte ForceColor);

    //1AF720: native-invalid Person means no owned layout was created. This
    //is a known cancellation branch, not an invented default face or colour.
    private MapSpeechSpeakerImage? CaptureBaseMapSpeechSpeakerCurrent(GameState state,
        SourceWorldContext current, int officerId)
    {
        int physical = MapSpeechNeed(current.ReadPersonLookupReference(officerId <= 6000 ? officerId : 0)?.PhysicalSlot,
            "owned_speaker_pointer");
        if (!MapSpeechNeed(current.ReadPersonNativeValidity(physical), "owned_speaker_validity")) return null;
        ushort appearance = MapSpeechNeed(current.ReadPersonPortraitAppearance(physical), "owned_speaker_appearance");
        var force = current.ReadPersonForce(physical) ?? throw MapSpeechMissing("owned_speaker_Force_pointer");
        byte raw = MapSpeechNeed(current.ReadForceReportColor(force), "owned_speaker_Force49");
        //2AD450(mode0)/2AD340: the ordinary palette is selected below60;
        //special/invalid colours use the actual neutral palette61.
        return new(appearance, raw < 60 ? raw : (byte)61);
    }
}

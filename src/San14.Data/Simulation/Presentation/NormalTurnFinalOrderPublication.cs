using System.Globalization;
using San14.Core;

namespace San14.Data;

/// <summary>The event illustration layer, shared by final-order scenes and
/// the doctrine resource bonus page. This is a still, not a background.</summary>
public sealed record FinalOrderIllustrationRoute(int IllustrationId, string TexturePack, string SpriteName)
{
    public uint LayoutId => 0xC6CCCD9C;
    public int ArtNode => 1;
}

/// <summary>The single narration shown by CDestinyRandomBonusState. The
/// formatted message and its numerical operands belong to the queued source.</summary>
public sealed record NormalTurnForceResourcePublication(NormalTurnForceResourceNoticeRequest Request,
    string Text, IReadOnlyList<string> Pages, FinalOrderIllustrationRoute Illustration, int SoundId);

/// <summary>An illustration transition around the separately queued
/// narration and sound requests. Exit has no replacement illustration.</summary>
public sealed record NormalTurnFinalOrderScenePublication(MonthlyMaintenanceOperation Operation,
    FinalOrderIllustrationRoute? Illustration, string AnimationName);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Publishes the already formatted source message without
    /// rerunning narration against the state after the final orders settled.</summary>
    public AreaAdministrationQuery<NormalTurnForceResourcePublication> QueryNormalTurnForceResourceNotice(
        GameState state, NormalTurnForceResourceNoticeRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ForceId is < 1 or > 45 || request.BaseLocationId is < 1 or > 61 || request.Amount <= 0 ||
            (request.ResourceKind, request.MessageRoot) is not ((0x17, 0x5EE3) or (0x2A, 0x5EE4) or (0x21, 0x5EE5)))
            return AreaAdministrationQuery<NormalTurnForceResourcePublication>.Unknown(
                "normal-turn.final-order.resource_notice_request");
        if (request.TextAtSource is not { } text)
            return AreaAdministrationQuery<NormalTurnForceResourcePublication>.Unknown(
                "normal-turn.final-order.resource_notice_text_at_source");

        //6C5EB0 submits the seven-field packet to CDestinyRandomBonusState.
        //7239D0 opens its illustration;75C470 plays packet SE20 and presents
        //one narration. There is no officer actor or result card in this page.
        return AreaAdministrationQuery<NormalTurnForceResourcePublication>.Known(new(request, text,
            SplitStoryPages(text), FinalOrderIllustration(request.ResourceKind), 20));
    }

    /// <summary>Publishes the actual EventView still transition. Sounds and
    /// narration retain their own ordered requests and are not duplicated here.</summary>
    public AreaAdministrationQuery<NormalTurnFinalOrderScenePublication> QueryNormalTurnFinalOrderScene(
        GameState state, MonthlyMaintenanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Operation == MonthlyMaintenanceOperation.FinalOrderSceneEnter &&
            request.EventKind is 10 or 26 or 31)
        {
            //441480 selects the game EventView.453790 calls844200 with the
            //event kind: texture kindD and the stl_event_in transition.
            return AreaAdministrationQuery<NormalTurnFinalOrderScenePublication>.Known(new(
                request.Operation, FinalOrderIllustration(request.EventKind.Value), "stl_event_in"));
        }
        if (request.Operation == MonthlyMaintenanceOperation.FinalOrderSceneExit && request.EventKind is null)
        {
            //441790 ->836A00 retires the same still, clearing its identity.
            return AreaAdministrationQuery<NormalTurnFinalOrderScenePublication>.Known(new(
                request.Operation, null, "stl_event_out"));
        }
        return AreaAdministrationQuery<NormalTurnFinalOrderScenePublication>.Unknown(
            "normal-turn.final-order.scene_request");
    }

    private static FinalOrderIllustrationRoute FinalOrderIllustration(int identity)
    {
        string suffix = identity.ToString("D3", CultureInfo.InvariantCulture);
        return new(identity, "texture_stl_event_" + suffix, "stl_event_" + suffix);
    }
}

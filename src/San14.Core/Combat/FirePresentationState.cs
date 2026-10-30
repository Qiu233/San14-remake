namespace San14.Core;

/// <summary>The actual Army view point copied by the fire cast caller. Null
/// snapshot is unknown; Present=false is the source-known absence of a view.</summary>
public sealed record ArmyFireViewSnapshot(bool Present,float X,float Y,float Z,float W);
public sealed record ArmyFireCastSnapshot(ArmyReceiverId SourceReceiverId,ArmyFireViewSnapshot SourceView);
public sealed record ArmyFireAnnouncementSnapshot(ArmyReceiverId SourceReceiverId,int SpeakerHexId);

/// <summary>The original gross-number target and call operands. Exactly one
/// receiver is present; no later Unit/Base lookup may substitute that receiver.</summary>
public sealed record DamagePresentationSnapshot(ArmyReceiverId? ArmyReceiverId,
    MapObjectReceiverId? BaseReceiverId,int HexId,sbyte? DisplayModifier);

public sealed record FireAnnouncementPreparationResult(GameState State,bool Handled,
    string? UnresolvedOwner=null);
public interface IFireAnnouncementRules
{
    FireAnnouncementPreparationResult PrepareFireAnnouncement(GameState state,ArmyFireAnnouncement request);
}

internal static class FirePresentationValidation
{
    internal static bool AnnouncementSource(CampaignEffectSource source)=>source is
        CampaignEffectSource.ArmyFirePresentation or CampaignEffectSource.NormalCombatApplication or
        CampaignEffectSource.ArmyCommandMaintenance or CampaignEffectSource.FacilityTraps;
    internal static bool ValidCastSnapshot(ArmyFireCastSnapshot? source) => source is null ||
        source.SourceReceiverId.Value>0 && source.SourceView is { } view &&
        float.IsFinite(view.X)&&float.IsFinite(view.Y)&&float.IsFinite(view.Z)&&float.IsFinite(view.W);
    internal static bool ValidAnnouncementSnapshot(ArmyFireAnnouncementSnapshot? source) => source is null ||
        source.SourceReceiverId.Value>0&&source.SpeakerHexId is >=0 and <=ushort.MaxValue;

    internal static void ValidateRequest(CampaignEffectSource source,CampaignEffectRequest request)
    {
        if(source==CampaignEffectSource.ArmyFirePresentation&&request.ArmyFireCast is null&&request.ArmyFireAnnouncement is null)
            throw new InvalidDataException("Army fire presentation requires its source payload.");
        if(request.ArmyFireCast is { } cast &&
            (source!=CampaignEffectSource.ArmyFirePresentation||cast.UnitId<1||cast.OfficerId is <0 or >6000||
             cast.HexId is <0 or >=48400||!ValidCastSnapshot(cast.SourceSnapshot)))
            throw new InvalidDataException("Invalid Army fire cast presentation.");
        if(request.ArmyFireAnnouncement is { } announcement&&
            (!AnnouncementSource(source)||announcement.UnitId<1||announcement.OfficerId is <0 or >6000||
             announcement.PersonalityId is <0 or >355||!ValidAnnouncementSnapshot(announcement.SourceSnapshot)))
            throw new InvalidDataException("Invalid Army fire announcement presentation.");
        if(request.FireDamagePresentation is { SourceSnapshot: { } fire } fireRequest)
            ValidateDamage(fire,fireRequest.Operation==FireDamagePresentationOperation.BaseGrossDamage,
                fireRequest.Operation==FireDamagePresentationOperation.ArmyTargetViewRefresh);
        if(request.TerrainDamagePresentation is { SourceSnapshot: { } terrain } terrainRequest)
            ValidateDamage(terrain,false,terrainRequest.Operation==TerrainDamagePresentationOperation.ArmyTargetViewRefresh);
    }

    private static void ValidateDamage(DamagePresentationSnapshot source,bool isBase,bool refresh)
    {
        bool receiver=isBase ? source.ArmyReceiverId is null&&source.BaseReceiverId is {Value:>0}
            : source.ArmyReceiverId is {Value:>0}&&source.BaseReceiverId is null;
        if(!receiver||source.HexId is <0 or >ushort.MaxValue||
            (refresh?source.DisplayModifier is not null:source.DisplayModifier!=0))
            throw new InvalidDataException("Invalid source-time fire or terrain damage operands.");
    }
}

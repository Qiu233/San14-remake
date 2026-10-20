using San14.Core;
using San14.Data;
using San14.Data.Formats;

internal static class DeploymentSummaryContent
{
    internal static DeploymentSummaryRowContent Create(GameResourceRepository repository,PreviewText text,GameState world,
        DeploymentDraftRow row,LoadedSprite? portrait,DeploymentDraftFigures? figures)
    {
        string unknown=text.Get("detail_unknown");
        string format=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[0x0331].Text
            ??throw new InvalidDataException("Missing deployment army-name format.");
        var request=row.Request;
        if(request.TacticIds.Count!=3)throw new InvalidDataException("A source summary requires three tactic slots.");
        string name=world.Officers.GetValueOrDefault(request.OfficerId)?.Name??unknown;
        string Tactic(int id)=>id<0?unknown:id==0||id>=201?"":world.Tactics.GetValueOrDefault(id)?.Name??unknown;
        return new(request.OfficerId,format.Replace("%s",name,StringComparison.Ordinal),request.FormationId,request.Troops,
            request.TacticIds.Select(Tactic).ToArray(),portrait,figures);
    }
}

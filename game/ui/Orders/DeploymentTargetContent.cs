using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Formats one current target-card publication. Route and ETA queries
/// stay outside the view; an unavailable route never becomes zero days.</summary>
internal static class DeploymentTargetContent
{
    internal static DeploymentTargetRowContent Create(GameResourceRepository repository,PreviewText text,GameState world,
        DeploymentDraftRow row,DeploymentTargetCard current,OfficerPortraitRoute? portraitRoute,
        int? estimatedDays,bool hasResolvedTarget)
    {
        int officer=row.Request.OfficerId;
        if(current.OfficerId!=officer||portraitRoute is not null&&portraitRoute.OfficerId!=officer)
            throw new ArgumentException("A target card must use its own officer publication.",nameof(current));
        string unknown=text.Get("detail_unknown");
        string format=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1]
            .Records[0x0331].Text??throw new InvalidDataException("Missing target army-name format.");
        // Target's validity gate differs from portrait eligibility. The
        // invalid receiver's name text is not supplied by a portrait fallback.
        string name=current.IsPersonValid==true&&world.Officers.TryGetValue(officer,out var person)
            ?format.Replace("%s",person.Name,StringComparison.Ordinal):unknown;
        LoadedSprite? portrait=null;
        var sprites=new LocalizedUiSpriteSource(repository);
        if(current.IsPersonValid==false)
        {
            var placeholder=ArmyMapUiSources.Create().PortraitNames(2198);
            portrait=sprites.LoadTexturePackSprite(placeholder.ResourceName,placeholder.SpriteName);
        }
        else if(current.IsPersonValid==true&&portraitRoute is {Kind:OfficerPortraitRouteKind.StockNamed})
            portrait=sprites.LoadTexturePackSprite(portraitRoute.ResourceName!,portraitRoute.SpriteName!);
        return new(officer,name,row.Request.Troops,current.FormationIcon,current.GrainPalette,
            portrait,estimatedDays,hasResolvedTarget)
        {
            RetainInitialFormation=current.HasCurrentHex==true&&current.UsesNavalFormation.HasValue&&
                current.FormationIcon is null&&current.FormationUnresolvedInput is null
        };
    }
}

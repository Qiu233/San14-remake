using San14.Data;

/// <summary>Resolves only the card's published resource routes. The owning
/// page recreates this reader with its resource set; no load failure fallback.</summary>
internal sealed class AreaAdministrationOfficerContent(GameResourceRepository repository)
{
    private readonly LocalizedUiSpriteSource sprites=new(repository);
    private readonly Dictionary<(string Pack,string Name),LoadedSprite> portraits=[];
    internal AreaAdministrationOfficerValues Create(AreaAdministrationOfficerCard? card,
        Func<int,AreaAdministrationQuery<string?>>? traitTooltip=null)
    {
        if(card is null)return new(null,null,null,null,null);
        LoadedSprite? portrait=null;
        if(card.Portrait.Kind==OfficerPortraitRouteKind.StockNamed)
        {
            var key=(card.Portrait.ResourceName!,card.Portrait.SpriteName!);
            if(!portraits.TryGetValue(key,out portrait))
            {portrait=sprites.LoadTexturePackSprite(key.Item1,key.Item2);portraits.Add(key,portrait);}
        }
        var abilities=card.Abilities.Select(a=>new AreaAdministrationAbilityValue(a.Base.Available?a.Base.Value:null,
            a.Effective.Available?a.Effective.Value:null)).ToArray();
        DetailForceAppearance? flag=null;
        if(card.ForceFlag.Available&&card.ForceFlag.Value is { } value)
            flag=new(value.Letter.HasValue,value.Glyph,value.Fill,value.Letter??default,null);
        var traits=card.Traits.Available?card.Traits.Value!.Select(t=>new AreaAdministrationOfficerTraitValue(t.PhysicalTraitId,t.Name,t.Style,
            traitTooltip is null?null:()=>traitTooltip(t.LookupTraitId))).ToArray():null;
        return new(card.Name,abilities,portrait,flag,traits);
    }
}

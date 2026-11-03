using San14.Data;

/// <summary>Projects the published card without loading presentation assets.
/// Its view borrows the actual portrait from the application resource owner.</summary>
internal sealed class AreaAdministrationOfficerContent
{
    internal AreaAdministrationOfficerValues Create(AreaAdministrationOfficerCard? card,
        Func<int,AreaAdministrationQuery<string?>>? traitTooltip=null)
    {
        if(card is null)return new(null,null,null,null,null);
        var abilities=card.Abilities.Select(a=>new AreaAdministrationAbilityValue(a.Base.Available?a.Base.Value:null,
            a.Effective.Available?a.Effective.Value:null)).ToArray();
        DetailForceAppearance? flag=null;
        if(card.ForceFlag.Available&&card.ForceFlag.Value is { } value)
            flag=new(value.Letter.HasValue,value.Glyph,value.Fill,value.Letter??default,null);
        var traits=card.Traits.Available?card.Traits.Value!.Select(t=>new AreaAdministrationOfficerTraitValue(t.PhysicalTraitId,t.Name,t.Style,
            traitTooltip is null?null:()=>traitTooltip(t.LookupTraitId))).ToArray():null;
        return new(card.Name,abilities,card.Portrait,flag,traits);
    }
}

using System.Collections.Immutable;
using System.Text.Json.Serialization;
namespace San14.Core;

/// <summary>Source-time flag text and the actual Force49 palette selector.
/// Resource tables and portrait pack names remain presentation-layer concerns.</summary>
public sealed record CampaignDuelFlagSnapshot([property:JsonRequired] byte ColorIndex,
    [property:JsonRequired] string Glyph);
public sealed record CampaignDuelTraitSnapshot([property:JsonRequired] int SourceIndex,
    [property:JsonRequired] string Text,[property:JsonRequired] byte Style);
public sealed record CampaignDuelPresentationPersonSnapshot([property:JsonRequired] int OfficerId,
    [property:JsonRequired] string? Name,[property:JsonRequired] ushort? PortraitAppearanceIndex,
    [property:JsonRequired] ushort? PortraitAdmittedAppearanceIndex,[property:JsonRequired] CampaignDuelFlagSnapshot? Flag,
    [property:JsonRequired] int? Strength,[property:JsonRequired] ImmutableArray<CampaignDuelTraitSnapshot> Traits,
    [property:JsonRequired] ImmutableArray<string> UnresolvedFields);

/// <summary>Captured before duel aftermath/rewards. Actor/opponent retain
/// their business identities; PlayerSide chooses presentation left/right.</summary>
public sealed record CampaignDuelPresentationSnapshot([property:JsonRequired] CampaignDuelPresentationPersonSnapshot Actor,
    [property:JsonRequired] CampaignDuelPresentationPersonSnapshot Opponent)
{
    internal void Validate(CampaignDuelPair pair)
    {
        Person(Actor,pair.ActorOfficerId);Person(Opponent,pair.OpponentOfficerId);
        static void Person(CampaignDuelPresentationPersonSnapshot? person,int expected)
        {
            if(person is null||person.OfficerId!=expected||person.Name?.Contains('\0')==true||
                person.PortraitAdmittedAppearanceIndex.HasValue&&!person.PortraitAppearanceIndex.HasValue||
                person.Traits.IsDefault||person.Traits.Length>5||person.UnresolvedFields.IsDefault||
                person.UnresolvedFields.Any(text=>text is null||text.Contains('\0'))||
                person.Flag is {} flag&&(flag.Glyph is null||flag.Glyph.Contains('\0'))||
                person.Traits.Any(trait=>trait is null||trait.SourceIndex is <1 or >355||trait.Text is null||trait.Text.Contains('\0')))
                throw new InvalidDataException("Duel source-time presentation snapshot is invalid.");
        }
    }
}
public sealed partial record CampaignRegisteredDuel
{
    //Null retains unknown old presentation knowledge. Never reconstruct it
    //from the post-aftermath World while loading or displaying a saved duel.
    [JsonRequired] public CampaignDuelPresentationSnapshot? PresentationSnapshot {get;init;}
}

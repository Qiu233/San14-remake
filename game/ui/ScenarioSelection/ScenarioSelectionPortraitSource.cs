using San14.Data;
using San14.Data.Formats;

/// <summary>Resolves the scenario header's direct portrait index through the
/// existing stock resource owner. Application predicates must carry the full
/// current eligibility decision; absent knowledge is not an unavailable image.</summary>
internal sealed class ScenarioSelectionPortraitSource
{
    private readonly LocalizedUiSpriteSource sprites;
    private readonly ArmyMapUiSources names = ArmyMapUiSources.Create();
    private readonly Func<ushort, bool>? currentAppearanceEligible, specialAppearanceAvailable;

    internal ScenarioSelectionPortraitSource(GameResourceRepository repository,
        Func<ushort, bool>? currentAppearanceEligible = null,
        Func<ushort, bool>? specialAppearanceAvailable = null)
    {
        sprites = new(repository);
        this.currentAppearanceEligible = currentAppearanceEligible;
        this.specialAppearanceAvailable = specialAppearanceAvailable;
    }

    internal LoadedSprite Resolve(S14Entry scenario, short person)
    {
        var metadata = scenario.SelectionMetadata
            ?? throw new InvalidDataException("Scenario portrait metadata is unknown.");
        if (person is < 1 or > 5999)
            throw new ArgumentOutOfRangeException(nameof(person));
        // This caller uses the preview number itself, never a campaign Person's
        // current appearance. DLC/edit indices cannot pass its 1..5999 gate.
        ushort index = (ushort)person;
        if (index >= 5000 && index is not (>= 5106 and <= 5205))
        {
            var available = specialAppearanceAvailable
                ?? throw new InvalidDataException("Scenario portrait special availability is unknown.");
            if (!available(index)) index = 2000;
        }
        // Native scenario63 skips only this complete current-person predicate.
        // An index >=1300 is accepted by the original predicate without a scan.
        if (metadata.NativeId != 63 && index < 1300)
        {
            var eligible = currentAppearanceEligible
                ?? throw new InvalidDataException("Scenario portrait current eligibility is unknown.");
            if (!eligible(index)) index = 2000;
        }
        if (index is not (<= 1310 or >= 2000 and <= 2199 or >= 2500 and <= 2509 or
            >= 3000 and <= 3005 or >= 5000 and <= 5399))
            throw new InvalidDataException("Scenario portrait range is not registered: " + index);
        var route = names.PortraitNames(index);
        return sprites.LoadTexturePackSprite(route.ResourceName, route.SpriteName);
    }
}

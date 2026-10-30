using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record EditedEventTestPerson(int PhysicalSlot, ushort Identity,
    string Name, ushort Appearance, byte Status, ushort Flags, byte Behavior)
{
    public bool Valid => PhysicalSlot != 0 && ((Flags & 0x4000) != 0
        ? Behavior != 0 : Identity is >= 5001 and <= 5100 || Status != 0);
}

/// <summary>The actual title prototype and application-owned registered people.
/// This is a presentation lookup, without a campaign, random stream or command authority.</summary>
public sealed class EditedEventTestContext : IS14PlayerEventConditionQueries
{
    private readonly IReadOnlyDictionary<int, EditedEventTestPerson> byIdentity;
    private readonly IReadOnlyDictionary<int, EditedEventTestPerson> byPhysical;
    private readonly EditedEventTestPerson zero;
    private readonly GameResourceRepository repository;
    public string Locale { get; }
    public IReadOnlyList<SourceProvenance> Sources { get; }

    internal EditedEventTestContext(GameResourceRepository repository, string locale,
        IEnumerable<EditedEventTestPerson> persons, EditedEventTestPerson zero, IReadOnlyList<SourceProvenance> sources)
    {
        this.repository = repository; Locale = locale; this.zero = zero; Sources = sources;
        byIdentity = persons.Where(p => p.Identity != 0).ToDictionary(p => (int)p.Identity);
        byPhysical = byIdentity.Values.Append(zero).ToDictionary(p => p.PhysicalSlot);
    }

    public static EditedEventTestContext Load(GameResourceRepository repository, string locale,
        RegisteredOfficerLibrary registeredLibrary, CollectionOfficerAdmission? admission = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(registeredLibrary); registeredLibrary.Validate();
        return repository.LoadEditedEventTestContext(locale, registeredLibrary, admission);
    }

    internal TalentOfficerPresentationContext ReadSpeaker(int id)
    {
        if (!byIdentity.TryGetValue(id, out var person)) throw Missing("speaker_identity");
        return new(id, person.Name, person.Appearance);
    }

    internal StoryActorPublication PublishActor(int slot, TalentOfficerPresentationContext speaker)
    {
        if (speaker.PortraitAppearanceIndex is not ushort appearance)
            throw Missing("speaker_appearance");
        try
        {
            var portrait = repository.ResolveStandingPortrait(appearance);
            return new(slot, speaker, AreaAdministrationQuery<DeploymentNarrationPortraitRoute>.Known(portrait), null, null);
        }
        catch (ArgumentOutOfRangeException)
        {
            string owner = "event_editor.test.custom_standing_portrait:" + appearance;
            return new(slot, speaker, AreaAdministrationQuery<DeploymentNarrationPortraitRoute>.Unknown(owner), null, owner);
        }
        catch (InvalidDataException error)
        { return new(slot, speaker, AreaAdministrationQuery<DeploymentNarrationPortraitRoute>.Unknown(error.Message), null, error.Message); }
    }

    public AreaAdministrationQuery<PersonnelPresentationSpeaker> QueryPersonnelSpeaker(
        TalentOfficerPresentationContext speaker)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        if (speaker.OfficerId is < 1 or > 6000 || speaker.NameAtSource is null)
            return AreaAdministrationQuery<PersonnelPresentationSpeaker>.Unknown("event_editor.test.source_speaker");
        if (speaker.OfficerId == 5012)
        {
            if (speaker.PortraitAppearanceIndex is not ushort appearance)
                return AreaAdministrationQuery<PersonnelPresentationSpeaker>.Unknown("event_editor.test.source_appearance");
            try
            {
                var names = repository.ResolveFacePortrait(appearance);
                var face = new OfficerPortraitRoute(speaker.OfficerId, OfficerPortraitRouteKind.StockNamed,
                    appearance, names.ResolvedAppearanceIndex, names.ResourceName, names.SpriteName, null, null);
                return AreaAdministrationQuery<PersonnelPresentationSpeaker>.Known(new(speaker.OfficerId,
                    speaker.OfficerId, speaker.NameAtSource, face, null, null));
            }
            catch (Exception error) when (error is InvalidDataException or ArgumentOutOfRangeException)
            { return AreaAdministrationQuery<PersonnelPresentationSpeaker>.Unknown(error.Message); }
        }
        var actor = PublishActor(0, speaker);
        return AreaAdministrationQuery<PersonnelPresentationSpeaker>.Known(new(speaker.OfficerId,
            speaker.OfficerId, speaker.NameAtSource, null, actor.StandingPortrait.Value, actor.UnresolvedOwner));
    }

    public S14EventConditionPersonPointer? ReadConditionPersonTableSlot(int slot) => slot is < 0 or > 6000
        ? null : new(new(byIdentity.TryGetValue(slot, out var person) ? person.PhysicalSlot : zero.PhysicalSlot));
    public S14EventConditionPersonPointer? ReadConditionBasePerson() => null;
    private EditedEventTestPerson? Read(S14EventConditionPersonReference person) => byPhysical.GetValueOrDefault(person.PhysicalSlot);
    public byte? ReadConditionPersonStatus(S14EventConditionPersonReference person) => Read(person)?.Status;
    public bool? ReadConditionPersonValidity(S14EventConditionPersonReference person) => Read(person)?.Valid;
    public ushort? ReadConditionPersonCharacterId(S14EventConditionPersonReference person) => Read(person)?.Identity;

    // No world or strategy inputs are invented for the six-statement editor language.
    public IReadOnlyList<S14EventConditionForceReference>? ReadRegisteredConditionForcesInNativeOrder() => null;
    public ushort? ReadConditionForceRulerPersonId(S14EventConditionForceReference force) => null;
    public S14EventConditionForcePointer? ReadConditionForceTableSlot(int slot) => null;
    public S14EventConditionForcePointer? ReadConditionPersonForce(S14EventConditionPersonReference person) => null;
    public bool? ReadConditionForceValidity(S14EventConditionForceReference force) => null;
    public int? ReadConditionCurrentPlayerForcePhysicalSlot() => null;
    public bool? ReadConditionApplicationPresent() => null;
    public int? ReadConditionCurrentUserIndex() => null;
    public int? ReadConditionSelectedUserPersonId(int index) => null;
    private static InvalidDataException Missing(string field) => new("event_editor.test." + field);
}

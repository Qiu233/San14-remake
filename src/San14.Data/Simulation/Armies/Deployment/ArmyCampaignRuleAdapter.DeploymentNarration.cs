using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record DeploymentNarrationQuery(int? SpeakerOfficerId, string? SpeakerName,
    ushort? PortraitAppearanceCode, int? MessageId, int? VariantIndex, string? Text,
    IReadOnlyList<string>? Pages, string? UnresolvedInput)
{
    public DeploymentNarrationPortraitRoute? PortraitRoute { get; init; }
    public bool Available => UnresolvedInput is null;
    public int RequestedVoiceId { get; init; } = 37;
    public bool VoiceAvailabilityKnown { get; init; }
    public IReadOnlyList<NarrationVoiceRequest> Voices { get; init; } = [];
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Prepare once after Yes. The caller owns application presentation
    /// random choices and retains this result throughout actual page consumption.
    /// World random state and deployment creation are not changed by this query.</summary>
    public DeploymentNarrationQuery QueryDeploymentNarration(GameState state,
        IReadOnlyList<DeploymentDraftRow> rows, string locale, Func<int,int> appPresentationRandom)
        => QueryDeploymentNarrationCurrent(state,rows,locale,appPresentationRandom,false);

    private DeploymentNarrationQuery QueryDeploymentNarrationCurrent(GameState state,
        IReadOnlyList<DeploymentDraftRow> rows, string locale, Func<int,int> appPresentationRandom,
        bool generalUnitTarget)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(appPresentationRandom);
        try
        {
            if (rows.Count == 0) throw NarrationMissing("empty_rows");
            if (locale is not ("TC" or "SC") || state.NativeArmySimulation?.Parameters is not {} parameters ||
                parameters.Language != Seed.Language || parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                throw NarrationMissing("rule_profile");
            var current = CaptureOfficerCommandCapacityCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, parameters.RuleVersion);
            var sorted = rows.ToList();
            var statuses = new Dictionary<int, byte>();
            var offices = new Dictionary<int, int>();
            var effective = new Dictionary<int, OfficerAbilities>();
            for (int index = 1; index < sorted.Count; index++)
            {
                var row = sorted[index]; int at = index;
                while (at > 0 && Before(row.Request.OfficerId, sorted[at-1].Request.OfficerId))
                { sorted[at] = sorted[at-1]; at--; }
                sorted[at] = row;
            }
            int requested = sorted[0].Request.OfficerId;
            int physical = Physical(requested);
            if (!NarrationNeed(current.ReadPersonNativeValidity(physical), "speaker_validity")) throw NarrationMissing("invalid_speaker");
            int speaker = NarrationNeed(current.ReadPersonIdentity(physical), "speaker_identity");
            if (!state.Officers.TryGetValue(speaker, out var person)) throw NarrationMissing("speaker_owner");
            var force = current.ReadPersonForce(physical) ?? throw NarrationMissing("speaker_force");
            int forcePhysical = NarrationNeed(force.PhysicalSlot, "speaker_force_pointer");
            int ruler = current.ReadForceRecord(forcePhysical)?.RawRuler10 ?? throw NarrationMissing("current_force_ruler");
            ushort portrait = NarrationNeed(person.PortraitAppearanceIndex, "current_portrait");
            var portraitRoute = ReadNarrationPortrait(state, current, _definitions, speaker, portrait);
            //The existing repository owns resource caching and invalidation.
            //A preparation owns this finite decoded program only for its call.
            var messages = new DeploymentNarrationMessages(_repository.LoadMessages(locale, MessageKind.Game).Messages);
            var voices = new NarrationVoiceRequests(_personnelNarrationVoice ?? throw NarrationMissing("voice_settings"),
                appPresentationRandom);
            var people = new DeploymentNarrationCurrentPeople(state,current,abilities,speaker,ruler,voiceRequests:voices);
            var text = generalUnitTarget ? messages.ResolveGeneralUnitTarget(people,appPresentationRandom,true) :
                messages.Resolve(people,appPresentationRandom,true);
            return new(speaker, person.Name, portrait, text.MessageId, text.VariantIndex, text.Text, text.Pages, null)
                { PortraitRoute = portraitRoute, VoiceAvailabilityKnown = true, Voices = voices.Pending.ToArray() };

            int Physical(int id)
            {
                if (id < 0) throw NarrationMissing("negative_person_index");
                return current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot ?? throw NarrationMissing("person_pointer");
            }
            byte Status(int id) => statuses.TryGetValue(id, out byte value) ? value :
                statuses[id] = NarrationNeed(current.ReadPersonStatus(Physical(id)), "sort_status");
            int Office(int id)
            {
                if (offices.TryGetValue(id, out int value)) return value;
                int office = NarrationNeed(current.ReadMoraleWavePersonPostE7(Physical(id)), "sort_office");
                return offices[id] = NarrationNeed(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, office), "sort_office_capacity");
            }
            OfficerAbilities Effective(int id) => effective.TryGetValue(id, out var value) ? value :
                effective[id] = abilities.Resolve(state,id) ?? throw NarrationMissing("sort_effective_abilities");
            bool Before(int a, int b)
            {
                byte first = Status(a), second = Status(b);
                if ((first == 1) != (second == 1)) return first == 1;
                if ((first == 3) != (second == 3)) return first == 3;
                int firstOffice = Office(a), secondOffice = Office(b);
                if (firstOffice != secondOffice) return firstOffice > secondOffice;
                var left = Effective(a); var right = Effective(b);
                if (left.Leadership != right.Leadership) return left.Leadership > right.Leadership;
                if (left.Strength != right.Strength) return left.Strength > right.Strength;
                return a < b;
            }
        }
        catch(InvalidDataException error) { return new(null,null,null,null,null,null,null,error.Message); }
    }
    private static T NarrationNeed<T>(T? value,string field) where T:struct => value ?? throw NarrationMissing(field);
    private static InvalidDataException NarrationMissing(string field) => new("deployment.narration."+field);

    internal static DeploymentNarrationPortraitRoute ReadNarrationPortrait(GameState state,
        SourceWorldContext current, ArmyOperationContext definitions, int id, ushort appearance,
        bool requireCurrentAppearance = true)
    {
        //931830 bank9 and the existing bank10 query share the same current
        //availability gates. Only their resource and sprite naming differ.
        int physical = current.ReadPersonLookupReference(id)?.PhysicalSlot ?? throw NarrationMissing("current_person_identity");
        if (definitions.ResolveOfficerSource(id) != physical || current.ReadPersonIdentity(physical) != id ||
            current.ReadPersonNativeValidity(physical) != true) throw NarrationMissing("current_person_validity");
        if (requireCurrentAppearance &&
            (!state.Officers.TryGetValue(id, out var owner) || owner.PortraitAppearanceIndex != appearance ||
             current.ReadPersonPortraitAppearance(physical) != appearance)) throw NarrationMissing("current_appearance_binding");
        // This source dispatch precedes bank 9's stock/DLC/edit gates. Bank
        // 12 owns its own original frame resources, including the b aliases.
        if (DeploymentNarrationPortraitRoute.IsAnimatedAppearance(appearance))
            return DeploymentNarrationPortraitRoute.Animated(appearance);
        if (DlcPortraitDescriptor.ForAppearance(appearance) is not null)
        {
            var repository = definitions.PortraitResources;
            var available = repository.ReadDlcPortraitAvailability(appearance);
            if (!available.Available) throw new InvalidDataException(available.UnresolvedInput);
            return available.Value ? repository.ResolveDlcStandingPortrait(appearance)
                : RegisteredOfficerPortraitCatalog.StandingPortrait(2000) with { CurrentAppearanceIndex = appearance };
        }
        if (appearance is >= 7000 and <= 7999)
        {
            var repository = definitions.PortraitResources;
            if (repository.LoadUserPortraits().IsRegistered(appearance)
                && repository.LoadUserPortraits().HasResource(appearance, 9))
                return repository.ResolveUserStandingPortrait(appearance);
            return RegisteredOfficerPortraitCatalog.StandingPortrait(2000) with { CurrentAppearanceIndex = appearance };
        }
        var eligibility = ReadOfficerPortraitAppearance(state, current, definitions, id, appearance, ArmyMapUiSources.Create());
        if (eligibility.Kind != OfficerPortraitRouteKind.StockNamed || eligibility.ResolvedAppearanceIndex is not int resolved)
            throw new InvalidDataException(eligibility.UnresolvedInput ?? "deployment.narration.portrait_route");
        bool remapped = eligibility.SpriteName?.EndsWith("b_ctr", StringComparison.Ordinal) == true;
        string number = resolved.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + (remapped ? "b" : "");
        string stem = "face_" + number;
        return new(appearance,resolved,"texture_chara_anime_"+number,[stem+"_b",stem+"_a",stem+"_c"]);
    }
}

public enum StandingPortraitKind { ThreePart, AnimatedFrames }

public sealed record DeploymentNarrationPortraitRoute(ushort CurrentAppearanceIndex,
    int ResolvedAppearanceIndex, string ResourceName, IReadOnlyList<string> SpriteNames)
{
    public StandingPortraitKind Kind { get; init; }
    public static bool IsAnimatedAppearance(ushort appearance) => appearance is
        1 or 2 or 3 or 9 or 36 or 45 or 60 or 64 or 91 or 566 or
        2193 or 2194 or 2192 or 2196 or 2195 or 2190 or 2184 or 2182;

    public static DeploymentNarrationPortraitRoute Animated(ushort appearance)
    {
        if (!IsAnimatedAppearance(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        int resolved=appearance;
        foreach(var row in PersonPortraitTransitionCatalog.Rules)
            if(row.PortraitWhenTrue==appearance){resolved=row.PortraitWhenFalse;break;}
        string number=resolved.ToString("D4",System.Globalization.CultureInfo.InvariantCulture)+(resolved!=appearance?"b":"");
        // Original bank12 binds all thirty stand_NNN slots to this atlas.
        // Playback selects their real authored texture-pattern curve.
        var frames = Enumerable.Range(0, StandingPortraitFrameAnimation.FrameCount)
            .Select(index => number + "_" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new(appearance,resolved,"texture_chara_spine_"+number,Array.AsReadOnly(frames))
            {Kind=StandingPortraitKind.AnimatedFrames};
    }

    public LoadedStandingPortraitSprites LoadSprites(Func<string,string,LoadedSprite> load) =>
        new(Kind,SpriteNames.Select(name=>load(ResourceName,name)).ToArray(),CurrentAppearanceIndex);
}

/// <summary>Retains the resource publication's rendering kind through existing
/// read-only participant lists, without changing the caller's input lifetime.</summary>
public sealed class LoadedStandingPortraitSprites(StandingPortraitKind kind,IReadOnlyList<LoadedSprite> sprites,
    ushort? currentAppearanceIndex = null)
    : System.Collections.ObjectModel.ReadOnlyCollection<LoadedSprite>(sprites.ToArray())
{
    public StandingPortraitKind Kind { get; } = kind;
    public ushort? CurrentAppearanceIndex { get; } = currentAppearanceIndex;
}

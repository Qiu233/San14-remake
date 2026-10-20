using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record DeploymentNarrationQuery(int? SpeakerOfficerId, string? SpeakerName,
    ushort? PortraitAppearanceCode, int? MessageId, int? VariantIndex, string? Text,
    IReadOnlyList<string>? Pages, string? UnresolvedInput)
{
    public DeploymentNarrationPortraitRoute? PortraitRoute { get; init; }
    public bool Available => UnresolvedInput is null;
    public int RequestedVoiceId => 37;
    public bool VoiceAvailabilityKnown => false;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Prepare once after Yes. The caller owns application presentation
    /// random choices and retains this result throughout actual page consumption.
    /// World random state and deployment creation are not changed by this query.</summary>
    public DeploymentNarrationQuery QueryDeploymentNarration(GameState state,
        IReadOnlyList<DeploymentDraftRow> rows, string locale, Func<int,int> appPresentationRandom)
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
            var text = messages.Resolve(new DeploymentNarrationCurrentPeople(state, current, abilities, speaker, ruler), appPresentationRandom);
            return new(speaker, person.Name, portrait, text.MessageId, text.VariantIndex, text.Text, text.Pages, null)
                { PortraitRoute = portraitRoute };

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
        SourceWorldContext current, ArmyOperationContext definitions, int id, ushort appearance)
    {
        //The animation dispatch occurs before the common stock eligibility
        //and naming path. These are appearance identities, not officer IDs.
        ReadOnlySpan<int> animated = [1,2,3,9,36,45,60,64,91,566,2193,2194,2192,2196,2195,2190,2184,2182];
        if (animated.Contains(appearance)) throw NarrationMissing("animated_portrait");
        //931830 bank9 and the existing bank10 query share the same current
        //availability gates. Only their resource and sprite naming differ.
        int physical = current.ReadPersonLookupReference(id)?.PhysicalSlot ?? throw NarrationMissing("current_person_identity");
        if (definitions.ResolveOfficerSource(id) != physical || current.ReadPersonIdentity(physical) != id ||
            current.ReadPersonNativeValidity(physical) != true) throw NarrationMissing("current_person_validity");
        if (!state.Officers.TryGetValue(id, out var owner) || owner.PortraitAppearanceIndex != appearance ||
            current.ReadPersonPortraitAppearance(physical) != appearance) throw NarrationMissing("current_appearance_binding");
        var eligibility = ReadOfficerPortraitAppearance(state, current, definitions, id, appearance, ArmyMapUiSources.Create());
        if (eligibility.Kind != OfficerPortraitRouteKind.StockNamed || eligibility.ResolvedAppearanceIndex is not int resolved)
            throw new InvalidDataException(eligibility.UnresolvedInput ?? "deployment.narration.portrait_route");
        bool remapped = appearance != resolved;
        string number = resolved.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + (remapped ? "b" : "");
        string stem = "face_" + number;
        return new(appearance,resolved,"texture_chara_anime_"+number,stem+"_b",stem+"_a",stem+"_c");
    }
}

public sealed record DeploymentNarrationPortraitRoute(ushort CurrentAppearanceIndex,
    int ResolvedAppearanceIndex, string ResourceName, string SpriteB, string SpriteA, string SpriteC);

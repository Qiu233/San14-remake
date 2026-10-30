using System.Globalization;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record UniqueTacticLinkedArt(int OfficerId, int NodeIndex, OrdinaryTacticNameArt Art);

/// <summary>The separate Direction cutin and large bank21 title. LinkedArts
/// contains the first four images; LinkedOfficerCount retains the complete
/// source list count, including the no-linked-effect branch above four.</summary>
public sealed record UniqueTacticLayoutPublication(long Sequence, int TacticId,
    string LayoutName, OrdinaryTacticNameArt MainArt, int MainNode,
    IReadOnlyList<UniqueTacticLinkedArt> LinkedArts, int LinkedOfficerCount,
    int NameLength, string TitleLayoutName, OrdinaryTacticNameArt TitleArt,
    int EffectClass, int HiddenDecorationNode, OrdinaryTacticViewIdentity CameraFocusView,
    int SpeakerId, int OrdinaryVoiceEvent, bool EligibleForSpecialVoice32,
    int InitialOpcode, int InitialSoundId)
{
    /// <summary>All title nodes sharing the source placeholder's sprite table
    /// entry. Each retains its own UV slice when the bank21 image is bound.</summary>
    public IReadOnlyList<int> TitleImageNodes { get; init; } = [];
    public string AnimationName => "senpou_" + Math.Min(LinkedOfficerCount + 1, 5).ToString(CultureInfo.InvariantCulture);
    public string TitleAnimationName => "senpou_txt_" + NameLength.ToString(CultureInfo.InvariantCulture);
    public string DarkChipResourceName => "cmn_prt_chip_dark";
    public int DarkChipNode => 1;
    public int DarkChipChildNode => 1;
    public byte DarkChipChildAlpha => byte.MaxValue;
    public int TitleImageNode => 2;
    public int TitleImageVariant => 1;
    public int InitialEffectId => 98 + EffectClass;
    public int? LinkedEffectId => LinkedOfficerCount is >= 1 and <= 4 ? 102 + LinkedOfficerCount : null;
    public int FinalEffectId => 107 + EffectClass;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>36B470/36AB50/36B290's actual Direction declarations. No
    /// ordinary request is fabricated, no random stream is consumed, and
    /// camera focus remains a current-view identity rather than a Hex pose.</summary>
    public AreaAdministrationQuery<UniqueTacticLayoutPublication> QueryUniqueTacticLayout(
        GameState state, TacticPresentationRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            if (request.Kind != TacticPresentationKind.UniqueDirection)
                throw UniqueLayoutMissing("Direction_request");
            if (request.Actors.Length is < 1 or > 11)
                throw UniqueLayoutMissing("primary_and_ten_linked_actors");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var primary = request.Actors[0];
            if (primary.TacticId is < 1 or > 200) throw UniqueLayoutMissing("Tactic_identity_range");
            var tactic = current.ReadSkillTacticReference(primary.TacticId)
                ?? throw UniqueLayoutMissing("Tactic_pointer");
            if (!(current.ReadSkillTacticValidity(tactic) ?? throw UniqueLayoutMissing("Tactic_validity")))
                throw UniqueLayoutMissing("invalid_Tactic_layout");
            int tacticId = current.ReadSkillTacticPhysicalSlot(tactic) ?? throw UniqueLayoutMissing("Tactic_identity");
            if (tacticId is < 1 or > 200) throw UniqueLayoutMissing("Tactic_art_identity");
            //AC35B0(...,-1,1) counts UTF16 units until NUL, without skipping
            //format escapes. Read the actual current name, including overrides.
            var nameUnits = new List<char>(5);
            for (int offset = 0x10; offset < 0x1A; offset += 2)
            {
                ushort unit = (ushort)((current.ReadSkillTacticRawByte(tactic, offset)
                    ?? throw UniqueLayoutMissing("Tactic_name")) |
                    ((current.ReadSkillTacticRawByte(tactic, offset + 1)
                    ?? throw UniqueLayoutMissing("Tactic_name")) << 8));
                if (unit == 0) break;
                nameUnits.Add((char)unit);
            }
            int nameLength = nameUnits.Count;
            if (nameLength is < 2 or > 4) throw UniqueLayoutMissing("unsupported_Tactic_name_length");

            ArmyLifecycleSourceView? lifecycle = null;
            var naming = ArmyMapUiSources.Create();
            int speaker = Commander(primary) ?? 0;
            int personPhysical = Person(speaker);
            //The source evaluates2C3FF0 before its ID/range/flag expression.
            bool alternate = PersonPortraitTransitionPredicate.Read(state, current, personPhysical);
            bool fullPerson = !((speaker is >= 1 and <= 1400 or >= 5101 and <= 5200) && !alternate &&
                ((current.ReadArmyReturnPersonFlags196(personPhysical) ?? throw UniqueLayoutMissing("Person_flags")) & 0x800) == 0);
            if (speaker == 5137) fullPerson = true;
            int imageIndex = fullPerson
                ? current.ReadPersonPortraitAppearance(personPhysical) ?? throw UniqueLayoutMissing("Person_appearance")
                : tacticId;
            if (imageIndex == 106 && speaker == 5145) imageIndex = 1106;
            OrdinaryTacticNameArt mainArt = fullPerson ? PersonArt(speaker, imageIndex) : TacticArt(imageIndex);

            //Builder36B470 appends valid linked Army commanders in packet
            //order, without sorting or filtering by Person validity.
            var linkedOfficers = new List<int>(10);
            for (int index = 1; index < request.Actors.Length; index++)
            {
                if (request.Actors[index].BaseCasterLocationId is not null) continue;
                if (Commander(request.Actors[index]) is int commander) linkedOfficers.Add(commander);
            }
            int count = linkedOfficers.Count, variant = Math.Min(count, 4);
            int[] imageNodes = variant switch { 0 => [], 1 => [6], 2 => [6, 10], 3 => [6, 10, 14], _ => [8, 12, 16, 20] };
            var linkedArts = new List<UniqueTacticLinkedArt>(imageNodes.Length);
            for (int index = 0; index < imageNodes.Length; index++)
            {
                int id = linkedOfficers[index];
                int appearance = current.ReadPersonPortraitAppearance(Person(id)) ?? throw UniqueLayoutMissing("linked_Person_appearance");
                linkedArts.Add(new(id, imageNodes[index], PersonArt(id, appearance)));
            }
            string number = tacticId.ToString("D3", CultureInfo.InvariantCulture);
            string language = Seed.Language switch { "TC" => "tw", "SC" => "ch", _ => throw UniqueLayoutMissing("resource_language") };
            string titleSprite = tacticId is >= 1 and <= 30 or >= 131 and <= 139
                ? language + "_hanyo_senpo_" + tacticId.ToString("D2", CultureInfo.InvariantCulture)
                : language + "_texture_senpo_" + number;
            int opcode = current.ReadTacticEffectOpcode(tactic, 0);
            byte category = current.ReadSkillTacticRawByte(tactic, 0x40) ?? throw UniqueLayoutMissing("Tactic_voice_classification");
            bool specialVoiceCandidate = speaker is >= 1 and <= 1400 &&
                ((current.ReadArmyReturnPersonFlags196(personPhysical) ?? throw UniqueLayoutMissing("Voice_Person_flags")) & 0x800) == 0;
            int initialSound = opcode switch
            {
                1 or 18 => 39,
                >= 2 and <= 4 or >= 10 and <= 14 or 16 or 22 or 23 => 42,
                >= 5 and <= 9 or 15 or 21 => 41,
                17 => 40, 19 or 20 => 43, _ => 33
            };
            int effectClass = opcode switch
            {
                17 => 1, >= 5 and <= 9 or 21 => 2,
                >= 2 and <= 4 or >= 10 and <= 14 or 16 or 22 or 23 => 3,
                15 or 19 or 20 => 4, _ => 0
            };
            int mainNode = variant switch { 0 => 4, 1 => 8, 2 => 12, 3 => 16, _ => 22 };
            int hiddenNode = variant switch { 0 => 6, 1 => 10, 2 => 14, 3 => 18, _ => 24 };
            string titleLayoutName = "00_86_01_unique_offset_txt_" + nameLength.ToString(CultureInfo.InvariantCulture);
            var titleLayout = MapMarkerLayout.LoadNamed(_repository, titleLayoutName);
            var titleImageNodes = LcskNodeReader.Read(titleLayout.Layout).Where(node => node.Type is 1 or 3 &&
                LcskSpriteNodeReader.Read(titleLayout.Layout, node).Groups.Any(group =>
                    group.Parts.Any(part => part.SpriteName == "texture_senpo_dummy"))).Select(node => node.Index).ToArray();
            if (titleImageNodes.Length == 0) throw UniqueLayoutMissing("title_sprite_table_binding");
            return AreaAdministrationQuery<UniqueTacticLayoutPublication>.Known(new(request.Sequence, tacticId,
                "00_86_01_unique_" + (variant + 1).ToString("D2", CultureInfo.InvariantCulture) + (fullPerson ? "_otomo" : ""),
                mainArt, mainNode, linkedArts.AsReadOnly(), count, nameLength,
                titleLayoutName,
                new("texture_stl_senpo_text_" + language + "_" + number, titleSprite), effectClass, hiddenNode, View(primary),
                speaker, TacticPresentationSource.ReadArmyVoiceEvent(tacticId, category), specialVoiceCandidate, opcode, initialSound)
                { TitleImageNodes = Array.AsReadOnly(titleImageNodes) });

            int? Commander(TacticActorPresentation actor)
            {
                if (actor.BaseCasterLocationId is not null) return null;
                lifecycle ??= state.ReadArmyLifecycle() ?? throw UniqueLayoutMissing("Army_receiver_owner");
                int physical = lifecycle.Slots.FirstOrDefault(slot => slot.ReceiverId == actor.Actor)?.PhysicalSlot
                    ?? throw UniqueLayoutMissing("Army_receiver_slot");
                var army = new ArmyUnitReference(physical);
                if (!(current.ReadArmyUnitNativeValidity(army) ?? throw UniqueLayoutMissing("Army_validity"))) return null;
                return current.ReadArmyUnitCommanderId(army) ?? throw UniqueLayoutMissing("Army_commander");
            }
            int Person(int id) => current.ReadPersonLookupReference(id is >= 0 and <= 6000 ? id : 0)?.PhysicalSlot
                ?? throw UniqueLayoutMissing("Person_pointer");
            OrdinaryTacticNameArt PersonArt(int id, int appearance)
            {
                var route = ReadOfficerPortraitAppearance(state, current, _definitions, id,
                    checked((ushort)appearance), naming);
                if (route.Kind != OfficerPortraitRouteKind.StockNamed || route.ResolvedAppearanceIndex is not int resolved)
                    throw new InvalidDataException(route.UnresolvedInput ?? "tactic.unique-layout.Person_art_route");
                string index = resolved.ToString("D4", CultureInfo.InvariantCulture) + (resolved != appearance ? "b" : "");
                return new("texture_chara_unique_otomo_" + index, "face_" + index + "_unique");
            }
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<UniqueTacticLayoutPublication>.Unknown(error.Message); }

        static OrdinaryTacticNameArt TacticArt(int index)
        {
            string number = index == 1106 ? "106_2" : index.ToString("D3", CultureInfo.InvariantCulture);
            if (index is not (>= 31 and <= 130 or >= 140 and <= 200 or 1106))
                throw UniqueLayoutMissing("unregistered_main_Tactic_art");
            return new("texture_unique_senpo_" + number, "texture_chara_unique_" + number);
        }
        static OrdinaryTacticViewIdentity View(TacticActorPresentation actor)
        {
            if (actor.BaseCasterLocationId is int location)
            {
                if (location is < 1 or > 61 || actor.Actor.Value != 0) throw UniqueLayoutMissing("Base_view_identity");
                return location <= 51 ? new(OrdinaryTacticViewKind.City, location) : new(OrdinaryTacticViewKind.Gate, location - 51);
            }
            if (actor.Actor.Value < 1) throw UniqueLayoutMissing("Army_view_identity");
            return new(OrdinaryTacticViewKind.Army, actor.Actor.Value);
        }
    }

    private static InvalidDataException UniqueLayoutMissing(string field) => new("tactic.unique-layout." + field);
}

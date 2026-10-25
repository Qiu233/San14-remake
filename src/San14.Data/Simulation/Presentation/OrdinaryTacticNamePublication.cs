using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed record OrdinaryTacticNamePerson(int OfficerId, OfficerPortraitRoute Portrait);

/// <summary>Bank21's explicit Common texture declaration and sprite identity.
/// The normal resource owner loads this pair, retaining its override lifetime.</summary>
public sealed record OrdinaryTacticNameArt(string ResourceName, string SpriteName);

public sealed record OrdinaryTacticNamePublication(long Sequence, int TacticId,
    bool Visible, bool IsUnique, OrdinaryTacticNamePerson? Caster,
    IReadOnlyList<OrdinaryTacticNamePerson> LinkedPortraits, OrdinaryTacticNameArt? TacticArt)
{
    public string AnimationName => "senpo" + (LinkedPortraits.Count + 1).ToString(CultureInfo.InvariantCulture);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<OrdinaryTacticNamePublication> QueryOrdinaryTacticName(
        GameState state, OrdinaryTacticPresentationPublication presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        return QueryOrdinaryTacticNameCurrent(state, presentation.Sequence,
            presentation.Actors.Select(value => value.Actor).ToArray());
    }

    public AreaAdministrationQuery<OrdinaryTacticNamePublication> QueryOrdinaryTacticName(
        GameState state, TacticPresentationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Kind == TacticPresentationKind.OrdinaryEffects
            ? QueryOrdinaryTacticNameCurrent(state, request.Sequence, request.Actors)
            : AreaAdministrationQuery<OrdinaryTacticNamePublication>.Unknown("tactic.name.unique_direction_host");
    }

    private AreaAdministrationQuery<OrdinaryTacticNamePublication> QueryOrdinaryTacticNameCurrent(
        GameState state, long sequence, IReadOnlyList<TacticActorPresentation> actors)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            if (actors.Count is < 1 or > 11) throw TacticNameMissing("primary_and_ten_linked_actors");
            var primary = actors[0];
            //172A30's Base branch borrows the Base UnitAction text child. It
            //does not create an Army tactics layout or an invented commander.
            if (primary.BaseCasterLocationId is not null) return Hidden(primary.TacticId);
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var lifecycle = state.ReadArmyLifecycle() ?? throw TacticNameMissing("Army_receiver_owner");
            var physicalByReceiver = lifecycle.Slots.ToDictionary(slot => slot.ReceiverId, slot => slot.PhysicalSlot);
            var naming = ArmyMapUiSources.Create();
            var portraits = new Dictionary<int, OrdinaryTacticNamePerson?>();
            var army = Army(primary);
            if (!(current.ReadArmyUnitNativeValidity(army) ?? throw TacticNameMissing("primary_Army_validity")))
                return Hidden(primary.TacticId);
            OrdinaryTacticNamePerson? caster = Person(current.ReadArmyUnitCommanderId(army)
                ?? throw TacticNameMissing("primary_Army_commander"));
            var tactic = current.ReadSkillTacticReference(primary.TacticId) ?? throw TacticNameMissing("Tactic_pointer");
            if (!(current.ReadSkillTacticValidity(tactic) ?? throw TacticNameMissing("Tactic_validity")))
                return Hidden(primary.TacticId);
            int tacticId = current.ReadSkillTacticPhysicalSlot(tactic) ?? throw TacticNameMissing("Tactic_identity");
            if (tacticId is < 1 or > 200) throw TacticNameMissing("Tactic_art_identity");
            //233820, followed by the explicit131..139 override in1B5000.
            bool unique = tacticId is >= 31 and <= 130 or >= 140 and <= 200 or >= 131 and <= 139;
            var linked = new List<OrdinaryTacticNamePerson>(4);
            //172A30 appends valid Army commanders in block order.1B7C70
            //counts successful native-valid Person portraits, not blocks.
            for (int index = 1; index < actors.Count && linked.Count < 4; index++)
            {
                if (actors[index].BaseCasterLocationId is not null) continue;
                var other = Army(actors[index]);
                if (!(current.ReadArmyUnitNativeValidity(other) ?? throw TacticNameMissing("linked_Army_validity"))) continue;
                var person = Person(current.ReadArmyUnitCommanderId(other) ?? throw TacticNameMissing("linked_Army_commander"));
                if (person is not null) linked.Add(person);
            }
            return AreaAdministrationQuery<OrdinaryTacticNamePublication>.Known(new(sequence, tacticId,
                true, unique, caster, linked.AsReadOnly(), TacticArt(tacticId)));

            ArmyUnitReference Army(TacticActorPresentation actor)
            {
                if (!physicalByReceiver.TryGetValue(actor.Actor, out int physical))
                    throw TacticNameMissing("Army_receiver_slot");
                return new(physical);
            }

            OrdinaryTacticNamePerson? Person(int lookup)
            {
                if (portraits.TryGetValue(lookup, out var cached)) return cached;
                var reference = current.ReadPersonLookupReference(lookup <= 6000 ? lookup : 0)
                    ?? throw TacticNameMissing("Person_pointer");
                if (reference.PhysicalSlot is not int physical ||
                    !(current.ReadPersonNativeValidity(physical) ?? throw TacticNameMissing("Person_validity")))
                { portraits[lookup] = null; return null; }
                int id = current.ReadPersonIdentity(physical) ?? throw TacticNameMissing("Person_identity");
                if (!state.Officers.ContainsKey(id) || _definitions.ResolveOfficerSource(id) != physical)
                    throw TacticNameMissing("Person_owner_binding");
                ushort appearance = current.ReadPersonPortraitAppearance(physical) ?? throw TacticNameMissing("Person_appearance");
                var route = ReadOfficerPortraitAppearance(state, current, _definitions, id, appearance, naming);
                if (route.Kind != OfficerPortraitRouteKind.StockNamed)
                    throw new InvalidDataException(route.UnresolvedInput ?? "tactic.name.Person_portrait_route");
                var value = new OrdinaryTacticNamePerson(id, route);
                portraits.Add(lookup, value);
                return value;
            }

            AreaAdministrationQuery<OrdinaryTacticNamePublication> Hidden(int id) =>
                AreaAdministrationQuery<OrdinaryTacticNamePublication>.Known(new(sequence, id, false, false, null, [], null));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OrdinaryTacticNamePublication>.Unknown(error.Message); }
    }

    private OrdinaryTacticNameArt TacticArt(int tacticId)
    {
        string language = Seed.Language switch
        {
            "TC" => "tw", "SC" => "ch",
            _ => throw TacticNameMissing("resource_language")
        };
        string number = tacticId.ToString("D3", CultureInfo.InvariantCulture);
        //0042C0 registers four inclusive bank21 ranges.9317A0's actual
        //ordinary call reaches the sprite formatter with argument0 (_s).
        string sprite = tacticId is >= 1 and <= 30 or >= 131 and <= 139
            ? language + "_hanyo_senpo_" + tacticId.ToString("D2", CultureInfo.InvariantCulture)
            : language + "_texture_senpo_" + number + "_s";
        return new("texture_stl_senpo_text_" + language + "_" + number, sprite);
    }

    private static InvalidDataException TacticNameMissing(string owner) => new("tactic.name." + owner);
}

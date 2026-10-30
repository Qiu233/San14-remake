using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record KingdomNameChoices(IReadOnlyList<CampaignEndingKingdomChoice> Choices,
    int? AutomaticSelectionId);
internal sealed record EndingEmperorModalPreparation(GameState State,
    TalentOfficerPresentationContext Speaker, string TextAtSource);

public sealed partial class ArmyCampaignRuleAdapter
{
    //28B610's player branch is a real single-selection operation. In
    //particular a one-row list is not the NPC's automatic selection path.
    internal KingdomNameChoices ReadEndingKingdomNameChoicesCurrent(GameState state,
        SourceWorldContext current) => ReadKingdomNameChoicesCurrent(state, current, EndingNamePlayer(state, current));

    //The actual28B610 player kernel is shared with ordinary Story. The
    //caller owns what an empty/cancelled selection does after this operation.
    private KingdomNameChoices ReadKingdomNameChoicesCurrent(GameState state,
        SourceWorldContext current, ForceReference force)
    {
        if (!TalentForceIsPlayerCurrent(state, current, force)) throw EndingNameMissing("player_selection_scope");
        int forceId = EmpireNameNeed(current.ReadForcePhysicalSlot(force), "player_Force60");
        var definitions = ReadEmpireNameResourcesCurrent().CustomNames.Definitions;
        var list = Temporary();
        try
        {
            int imperialForce = ReadEmpireNameImperialForceCurrent(state, current);
            int scenario = current.ReadScenarioId() ?? throw EndingNameMissing("World_scenario_id");
            int faction = EmpireNameNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId)?.FactionOfficerId,
                "Force11E");
            int automatic = scenario == 19 ? faction switch { 5101 => 44, 5105 => 45, _ => 0 } : 0;
            if (automatic != 0 && !EmpireNameUsedCurrent(current, automatic))
                return new([Choice(definitions[automatic])], automatic);

            int ruler = Lookup(EmpireNameNeed(current.ReadForceRecord(forceId)?.RawRuler10, "Force_ruler10"));
            bool imperialValid = EmpireNameNeed(current.ReadForceNativeValidity(new(imperialForce)), "imperial_Force_validity");
            foreach (var country in definitions)
            {
                bool allowed = (country.Field8E & 1) != 0;
                if (!allowed && country.PhysicalSlot == 7)
                {
                    if (!imperialValid)
                    {
                        if (scenario == 63 && EmpireNameNeed(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654") == 0 &&
                            EmpireNameNeed(current.ReadPersonNativeValidity(ruler), "ruler_validity"))
                            allowed = faction == 2007 || OfficeWord(state, ruler, 0xB2) == 2007 || OfficeWord(state, ruler, 0xB4) == 2007;
                    }
                    else if (imperialForce == forceId && EmpireNameNeed(current.ReadPersonNativeValidity(ruler), "ruler_validity"))
                    {
                        //TC file-backed1A4A878 is the ordinary20-year
                        //threshold, shared with the existing name selector.
                        allowed = unchecked((short)EmpireNameNeed(RulerCaptureReleaseInputs.ReadCurrentWord(state, ruler, 0x17A),
                            "ruler17A")) >= 20;
                    }
                }
                if (!allowed || EmpireNameUsedCurrent(current, country.PhysicalSlot)) continue;
                Lock(true);
                try
                {
                    var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw EndingNameMissing("candidate_node_owner");
                    if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Kingdom, country.PhysicalSlot));
                }
                finally { Lock(false); }
            }
            var choices = new List<CampaignEndingKingdomChoice>();
            for (var node = current.ReadHexListHead(list) ?? throw EndingNameMissing("candidate_head"); node.Identity.HasValue;
                 node = current.ReadHexListNodeNext(node) ?? throw EndingNameMissing("candidate_next"))
            {
                var payload = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw EndingNameMissing("candidate_payload");
                if (payload is not { Kind: RegistryKind.Kingdom, PhysicalSlot: int country }) throw EndingNameMissing("candidate_kind");
                choices.Add(Choice(definitions[country]));
            }
            return new(choices.ToArray(), null);
        }
        finally
        {
            Lock(true);
            try { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
            finally { Lock(false); }
        }

        CampaignEndingKingdomChoice Choice(S14ForceNameDefinition country)
            => new(country.PhysicalSlot, country.ShortName) { Description = country.Text26 };
        int Lookup(int id) => EmpireNameNeed((id <= 6000 ? current.ReadPersonLookupReference(id)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "ruler_pointer");
        HexListHandle Temporary()
        {
            Lock(true);
            try { return current.CreateTemporaryHexList() ?? throw EndingNameMissing("candidate_descriptor"); }
            finally { Lock(false); }
        }
        void Lock(bool enter)
        {
            if (!EmpireNameNeed(current.ReadRegistryLockEnabled(), "registry_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
    }

    internal GameState ApplyEndingKingdomNameCurrent(GameState state, SourceWorldContext current,
        int selectedKingdomId)
    {
        var choices = ReadEndingKingdomNameChoicesCurrent(state, current);
        if (selectedKingdomId is < 1 or > 100 || !choices.Choices.Any(row => row.Id == selectedKingdomId) ||
            choices.AutomaticSelectionId is int automatic && selectedKingdomId != automatic)
            throw EndingNameMissing("selected_kingdom_not_eligible");
        var force = EndingNamePlayer(state, current);
        int forceId = EmpireNameNeed(current.ReadForcePhysicalSlot(force), "selected_Force60");
        //28C4FD stores only48. The rewrite's derived name projection reads
        //this current field, rather than keeping a second saved name bank.
        return StoreEmpireNameCurrent(state, current, forceId, selectedKingdomId);
    }

    //43EC30 ->2D2580(type1) prepares an actual modal event, not a monthly
    //announcement/history or a new rank. The Ending driver owns consumption.
    internal EndingEmperorModalPreparation PrepareEndingEmperorModalCurrent(GameState state,
        SourceWorldContext current)
    {
        var force = EndingNamePlayer(state, current);
        int forceId = EmpireNameNeed(current.ReadForcePhysicalSlot(force), "modal_Force60");
        int rawRuler = EmpireNameNeed(current.ReadForceRecord(forceId)?.RawRuler10, "modal_Force10");
        int physical = EmpireNameNeed((rawRuler <= 6000 ? current.ReadPersonLookupReference(rawRuler)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "modal_ruler_pointer");
        int ruler = EmpireNameNeed(current.ReadPersonIdentity(physical), "modal_ruler10");
        var text = FormatNormalTurnReport(state, current, 0x2C6B,
            new(Person0: ruler, Force0: forceId, Force1: forceId));
        if (text.Voices.Count != 0 || text.SpatialVoices.Count != 0)
            throw EndingNameMissing("modal_unexpected_voice");
        var person = text.State.Officers.GetValueOrDefault(ruler) ?? throw EndingNameMissing("modal_ruler_owner");
        string surname = person.CurrentSurname ?? throw EndingNameMissing("modal_ruler_surname");
        string given = person.CurrentGivenName ?? throw EndingNameMissing("modal_ruler_given_name");
        return new(text.State, new(ruler, surname + given, current.ReadPersonPortraitAppearance(physical)), text.Text);
    }

    private ForceReference EndingNamePlayer(GameState state, SourceWorldContext current)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw EndingNameMissing("current_player");
        if (!EmpireNameNeed(current.ReadForceNativeValidity(player), "player_validity") || !TalentForceIsPlayerCurrent(state, current, player))
            throw EndingNameMissing("player_selection_scope");
        return player;
    }

    private LoadedArmyFlagPaneNames ReadEmpireNameResourcesCurrent()
        => rankPromotionNames ??= ArmyFlagPaneNames.Load(_scenario);

    private static bool EmpireNameUsedCurrent(SourceWorldContext current, int country)
    {
        //2CA330 intentionally has no Force-native-validity or dedup gate.
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw EndingNameMissing("ForceC0"))
        {
            if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int physical }) throw EndingNameMissing("ForceC0_payload");
            if (EmpireNameNeed(current.ReadForceKingdomNameIndex(physical), "used_Force48") == country) return true;
        }
        return false;
    }

    private static int ReadEmpireNameImperialForceCurrent(GameState state, SourceWorldContext current)
    {
        int id = EmpireNameNeed(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654");
        int emperor = EmpireNameNeed((id <= 6000 ? current.ReadPersonLookupReference(id)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "emperor_pointer");
        int citySlot = 0;
        if (EmpireNameNeed(current.ReadPersonNativeValidity(emperor), "emperor_validity"))
        {
            var place = PersonPositionRepair.DecodeSecondLocation(EmpireNameNeed(current.ReadPersonSecondLocation(emperor), "emperor_E4"));
            if (place.Kind == 5) citySlot = place.Index < 52 ? place.Index : 0;
        }
        var city = current.ReadCityTableReference(citySlot) ?? throw EndingNameMissing("emperor_City_pointer");
        return EmpireNameNeed(current.ReadBaseNativeValidity(city), "emperor_City_validity")
            ? EmpireNameNeed(current.ReadBaseForceSlot(city), "emperor_City_Force")
            : EmpireNameNeed((current.ReadForceTableReference(0) ?? throw EndingNameMissing("Force0_pointer")).PhysicalSlot, "Force0");
    }

    private static GameState StoreEmpireNameCurrent(GameState state, SourceWorldContext current, int forceId, int country)
    {
        byte index = checked((byte)Math.Clamp(country, 0, 100));
        current.SetForceKingdomNameIndex(forceId, index);
        var owner = state.Forces.GetValueOrDefault(forceId) ?? throw EndingNameMissing("Force_owner");
        return state with { Forces = new(state.Forces) { [forceId] = owner with { KingdomNameIndex = index } } };
    }

    private static T EmpireNameNeed<T>(T? value, string field) where T : struct => value ?? throw EndingNameMissing(field);
    private static InvalidDataException EndingNameMissing(string field) => new("ending.kingdom-name." + field);
}

using San14.Core;

namespace San14.Data;

public sealed record IntelligencePersonSelection(OfficerInformationPageSet Pages,
    IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> AlteredHealth,
    IReadOnlyDictionary<int, IntelligenceTargetChoice> Eligibility, string GuideText);
public sealed record IntelligenceForceSelection(ForceInformationPageSet Pages,
    IReadOnlyDictionary<int, IntelligenceTargetChoice> Eligibility, string GuideText,
    IReadOnlyDictionary<int, string> FocusHints, DiplomacyTargetMapQuery MapSelection)
{
    public int MapInputType => 7;
    public int TerritoryMode => 1;
    public int MarkerMode => 11;
}
public sealed record IntelligenceCitySelection(CityInformationPageSet Pages,
    IReadOnlyDictionary<int, IntelligenceTargetChoice> Eligibility, string GuideText,
    DiplomacyTargetMapQuery MapSelection)
{
    public int MapInputType => 5;
    public int TerritoryMode => 5;
    public int MarkerMode => 9;
}

public sealed partial class IntelligenceHomeEditorDraft
{
    public AreaAdministrationQuery<IntelligencePersonSelection> ReadPersonSelection()
    {
        try { return AreaAdministrationQuery<IntelligencePersonSelection>.Known(
            source.PersonSelection(People, SelectedTargetId, 0)); }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IntelligencePersonSelection>.Unknown(error.Message); }
    }
}

public abstract partial class IntelligenceMissionDraft
{
    public bool CanChoosePerson => SelectedTargetId is int id && Targets.Any(row => row.Id == id && row.Enabled);
    public AreaAdministrationQuery<IntelligencePersonSelection> ReadPersonSelection()
    {
        if (!CanChoosePerson)
            return AreaAdministrationQuery<IntelligencePersonSelection>.Unknown("intelligence.selection.target_required");
        try { return AreaAdministrationQuery<IntelligencePersonSelection>.Known(
            source.PersonSelection(People, SelectedTargetId ?? 0, Option)); }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IntelligencePersonSelection>.Unknown(error.Message); }
    }
}

public sealed partial class IntelligenceAwayDraft
{
    public AreaAdministrationQuery<IntelligenceForceSelection> ReadTargetSelection()
    {
        try { return AreaAdministrationQuery<IntelligenceForceSelection>.Known(source.ForceSelection(Targets)); }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IntelligenceForceSelection>.Unknown(error.Message); }
    }
}

public sealed partial class IntelligenceInspectDraft
{
    public AreaAdministrationQuery<IntelligenceCitySelection> ReadTargetSelection()
    {
        try { return AreaAdministrationQuery<IntelligenceCitySelection>.Known(source.CitySelection(Targets)); }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IntelligenceCitySelection>.Unknown(error.Message); }
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private string IntelligenceSelectionGuide(int id) => new NarrationResourceMessages(
        _repository.LoadMessages(Seed.Language, Formats.MessageKind.Game).Messages)
        .ResolveIntelligenceSelectionGuide(id);

    private IntelligencePersonSelection ReadIntelligencePersonSelectionCurrent(GameState state,
        SourceWorldContext current, int forceId, int mode, IReadOnlyList<IntelligenceTargetChoice> choices,
        Func<int, int> travelDays)
    {
        var reader = new OfficerInformationCurrent(this, state, forceId,
            rowPaletteEnabled: mode != 0, retainedCurrent: current);
        return reader.IntelligencePages(mode, choices, travelDays,
            IntelligenceSelectionGuide(mode == 0 ? 0x5C24 : mode == 1 ? 0x5C41 : 0x5A33));
    }

    private IntelligenceForceSelection ReadIntelligenceForceSelectionCurrent(GameState state,
        SourceWorldContext current, int forceId, IReadOnlyList<IntelligenceTargetChoice> choices)
    {
        var reader = new ForceInformationCurrent(this, state, forceId, current);
        var hints = new Dictionary<int, string>();
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw IntelligenceMissing("selection.player_force");
        if (IntelligenceNeed(current.ReadForceNativeValidity(player), "selection.player_validity"))
        {
            int own = IntelligenceNeed(player.PhysicalSlot, "selection.player_identity");
            foreach (var row in choices)
            {
                var target = current.ReadForceTableReference(row.Id) ?? throw IntelligenceMissing("selection.target_force");
                if (!IntelligenceNeed(current.ReadForceNativeValidity(target), "selection.target_validity") ||
                    target.PhysicalSlot == player.PhysicalSlot) continue;
                byte relation = ReadReportForceRelationByte(state, target, own, IntelligenceMissing);
                byte ownFlags = IntelligenceNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(own)?.Flags12,
                    "selection.player_flags");
                byte targetFlags = IntelligenceNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(row.Id)?.Flags12,
                    "selection.target_flags");
                if ((relation & 127) != 0 || (ownFlags & 2) != 0 && (targetFlags & 2) != 0)
                    hints[row.Id] = IntelligenceSelectionGuide(0x5C40);
            }
        }
        return new(reader.IntelligencePages(choices.Select(row => row.Id).ToArray()),
            choices.ToDictionary(row => row.Id), IntelligenceSelectionGuide(0x5C3F), hints,
            CreateDiplomacyTargetMap(current, DiplomacyTargetSelectionKind.Force,
                choices.Select(row => row.Id).ToArray(), MapTargetPresentation.IntelligenceAwayForce,
                ReadIntelligenceSelectionSpyMarkersCurrent(state, current),
                choices.Where(row => !row.Enabled).Select(row => row.Id).ToHashSet()));
    }

    private IntelligenceCitySelection ReadIntelligenceCitySelectionCurrent(GameState state,
        SourceWorldContext current, int forceId, IReadOnlyList<IntelligenceTargetChoice> choices)
    {
        var reader = new CityInformationCurrent(this, state, forceId, current);
        return new(reader.IntelligencePages(choices.Select(row => row.Id).ToArray()),
            choices.ToDictionary(row => row.Id), IntelligenceSelectionGuide(0x5D14),
            CreateIntelligenceCitySelectionMap(current, choices.Where(row => row.Enabled).Select(row => row.Id).ToArray()));
    }

    // Inspect selects CCityData directly. Diplomacy's ordinary City selector
    // instead gives a representative Gate priority. This caller's type5
    // territory is exclusively the Area's City identity.
    private static DiplomacyTargetMapQuery CreateIntelligenceCitySelectionMap(SourceWorldContext current,
        IReadOnlyList<int> candidates)
    {
        var allowed = candidates.ToHashSet();
        return new(DiplomacyTargetSelectionKind.City, allowed, Read, Territory)
            { Presentation = MapTargetPresentation.IntelligenceInspectCity };
        DiplomacyTerritoryCell Territory(int index)
        {
            if ((uint)index >= 48400) throw new ArgumentOutOfRangeException(nameof(index));
            var hex = current.ReadHexTableReference(index) ?? throw IntelligenceMissing("selection.territory_hex");
            int areaId = IntelligenceNeed(current.ReadHexAreaSlot(hex), "selection.territory_area");
            var area = current.ReadAreaTableReference(areaId < 501 ? areaId : 0)
                ?? throw IntelligenceMissing("selection.territory_area_pointer");
            int cityId = IntelligenceNeed(current.ReadAreaCitySlot(area), "selection.territory_city");
            //181050 groups on the original Area35 byte, independently of
            //the clicked type5 input's City lookup and native-validity gate.
            ushort identity = checked((ushort)cityId);
            return new(0, 0, identity, 0, identity);
        }
        DiplomacyTargetMapPick Read(int index)
        {
            if ((uint)index >= 48400) return new(null, false);
            try
            {
                var hex = current.ReadHexTableReference(index) ?? throw IntelligenceMissing("selection.map_hex");
                int areaId = IntelligenceNeed(current.ReadHexAreaSlot(hex), "selection.map_area");
                var area = current.ReadAreaTableReference(areaId <= 500 ? areaId : 0)
                    ?? throw IntelligenceMissing("selection.map_area_pointer");
                if (!IntelligenceNeed(current.ReadAreaNativeValidity(area), "selection.map_area_validity"))
                    return new(null, false);
                int cityId = IntelligenceNeed(current.ReadAreaCitySlot(area), "selection.map_city");
                var city = current.ReadCityTableReference(cityId <= 51 ? cityId : 0)
                    ?? throw IntelligenceMissing("selection.map_city_pointer");
                if (!IntelligenceNeed(current.ReadBaseNativeValidity(city), "selection.map_city_validity"))
                    return new(null, false);
                int identity = IntelligenceNeed(city.PhysicalSlot, "selection.map_city_identity");
                return new(identity, allowed.Contains(identity));
            }
            catch (InvalidDataException error) { return new(null, null, error.Message); }
        }
    }
}

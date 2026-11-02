using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // Offline TC data extraction: .data RVA1A4AAD0/1A4AADC/1A4AAE8.
    // These are presentation category boundaries, not simulation overrides.
    private const int TerrainSlowMovementBoundary = 150;
    private const int TerrainFastMovementBoundary = 300;
    private const int TerrainSmallMoraleBoundary = 1;
    // Offline TC .data1A4AAF4(4) *1A4A004(1).
    private const int TerrainFireDurationUnitsPerDay = 4;

    /// <summary>The independent CInfoSummaryHex owner (929CD0). It consumes
    /// the actual accepted cursor Hex and never the selected city or ruler.</summary>
    public MapSummaryPage QueryMapTerrainStatus(GameState state, SourceWorldContext current, HexReference? hoveredHex)
    {
        const string layout = "00_82_12_sum_chikei_offset_00";
        var hidden = new MapSummaryPage(-1, hoveredHex?.PhysicalSlot ?? 0, layout, false, new Dictionary<int,string>());
        if (hoveredHex?.PhysicalSlot is not int hex || hex == 0) return hidden;
        var page = new OtherSummaryBuilder(-1, hex, layout, OtherSummaryText());
        try
        {
            if (OtherSummaryNeed(current.ReadHexAreaSlot(hoveredHex), "terrain.Hex_area") == 0) return hidden;
            int raw = OtherSummaryNeed(current.ReadHexSurfaceCode(hoveredHex), "terrain.Hex_surface");
            var surface = current.ReadSurfaceTypeTableReference(raw <= 30 ? raw : 0)
                ?? throw OtherSummaryMissing("terrain.surface_pointer");
            int physical = OtherSummaryNeed(surface.PhysicalSlot, "terrain.surface_identity");
            var terrain = _terrain.Catalog.Types[physical];
            int kind = terrain.RawField42;
            if (kind is < 1 or > 10) return hidden;
            int[] icons = [0x169,0x169,0x16A,0x16B,0x16D,0x16C,0x16E,0x16F,0x170,0x171,0x172];
            page.Images.Add(new(4, ArmyMapUiSources.Create().IconName(new(physical == 24 ? 0x3E9 : icons[kind],0)),
                physical == 24 ? "texture_pk_common" : null));
            page.Text[5] = terrain.Name;
            page.Presets[5] = 0;
            page.Read(9, () =>
            {
                if (terrain.RawField41 >= 4) { page.Visible[9] = false; return; }
                int[] levels = [0x339,0x338,0x4BE,0x4BD];
                page.Text[9] = page.Data(1,levels[terrain.RawField41]); page.Presets[9] = 0;
            });
            page.Read(13, () =>
            {
                int cost = ArmyRouteTerrainCost.Read(state,current,surface);
                int text = cost == 0 ? 0x339 : cost <= TerrainSlowMovementBoundary ? 0x4BE :
                    cost <= TerrainFastMovementBoundary ? 0x338 : 0x4BD;
                page.Text[13] = page.Data(1,text); page.Presets[13] = 0;
            });
            page.Text[14] = physical == 24 ? page.Data(1,0x6FF) : "";
            page.Presets[14] = 0;
            // Fire16 is an independent saved current owner. The regular terrain
            // status must still publish when only this optional formatter lacks input.
            page.Read(14, () =>
            {
                ushort fire = state.NativeBaseCaptureCurrent?.HexWords16 is {} fires && (uint)hex < (uint)fires.Length
                    ? OtherSummaryNeed(fires[hex], "terrain.Hex_fire_duration")
                    : throw OtherSummaryMissing("terrain.Hex_fire_owner");
                if (fire != 0) page.Text[14] = page.Data(1,0x40A) +
                    FormatSceneryPanelNumber(page.Data(1,0x324),fire / TerrainFireDurationUnitsPerDay);
                if (kind == 6) page.Text[14] = page.Data(1,0x4C1);
            });
            page.Read(15, () =>
            {
                int morale = terrain.RawField40;
                bool advanced = OtherSummaryNeed(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "terrain.environment_mode");
                if (advanced) morale = unchecked(morale * OtherSummaryNeed(BattleRemakeCurrentSettings.ReadAdvancedParameter(state,27), "terrain.morale_percent")) / 100;
                page.Text[15] = morale > 0 ? page.Data(1,morale <= TerrainSmallMoraleBoundary ? 0x4BF : 0x4C0) : "";
                page.Presets[15] = 0;
            });
            var palette = MapSpritePalettes.Create()[9];
            page.Colors[4] = page.Colors[8] = page.Colors[12] = palette;
            return page.Finish();
        }
        catch (InvalidDataException error) { return hidden with { ReceiverValid = null, UnresolvedOwner = error.Message }; }
    }
}

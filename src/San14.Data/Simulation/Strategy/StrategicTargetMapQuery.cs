using San14.Core;

namespace San14.Data;

public enum StrategicMapMarkerKind { City, Gate, Area }
public sealed record StrategicTargetMapPick(int? Id, bool? Allowed, string? UnresolvedInput = null);
public sealed record StrategicTargetMarkerState(int? TargetId, bool? Enabled, string? UnresolvedInput = null);
public readonly record struct StrategicTargetTerritoryCell(ushort Group, byte ForceColour = 0);

/// <summary>One strategic selector's original captured current and candidate
/// publication. Camera and pointer updates never create a World or rerun its
/// target, preview, advice or random producers.</summary>
public sealed class StrategicTargetMapQuery
{
    private readonly Func<int, StrategicTargetMapPick> pick;
    private readonly Func<StrategicMapMarkerKind, int, StrategicTargetMarkerState> marker;
    private readonly Func<int, StrategicTargetTerritoryCell> territory;
    private readonly IReadOnlyDictionary<int, StrategicCommandTargetChoice> choices;
    private StrategicTargetTerritoryCell[]? territoryCells;
    private readonly Dictionary<(StrategicMapMarkerKind, int), StrategicTargetMarkerState> markerStates = [];

    public int CommandKind { get; }
    public StrategicCommandTargetKind Kind { get; }
    public bool IsSecondary { get; }
    public int MapMode { get; }
    public int InputType => Kind == StrategicCommandTargetKind.Area ? 10 : Kind == StrategicCommandTargetKind.City ? 5 : 7;
    public int MarkerMode => Kind == StrategicCommandTargetKind.Area ? 8 : Kind == StrategicCommandTargetKind.City ? 9 : 7;
    public int TerritoryMode => Kind == StrategicCommandTargetKind.Area ? 4 : Kind == StrategicCommandTargetKind.City ? 5 : 1;
    public int TitleMessageId => Kind == StrategicCommandTargetKind.Area ? 0x5AF : Kind == StrategicCommandTargetKind.City ? 0x39A : 0x36D;
    public string GuideText { get; }
    public bool InitialMap { get; }
    public bool ShowFacilities => CommandKind == 20 && Kind == StrategicCommandTargetKind.Area;
    public bool Contains(int id) => choices.ContainsKey(id);
    public bool IsEnabled(int id) => choices.TryGetValue(id, out var row) && row.Enabled == true;
    public StrategicTargetMapPick QueryHex(int physicalHex) => pick(physicalHex);
    public StrategicTargetMarkerState QueryMarker(StrategicMapMarkerKind kind, int id)
    {
        if (!markerStates.TryGetValue((kind,id), out var state)) markerStates[(kind,id)] = state = marker(kind,id);
        return state;
    }
    public StrategicTargetTerritoryCell ReadTerritoryCell(int physicalHex)
    {
        if ((uint)physicalHex >= 48400) throw new ArgumentOutOfRangeException(nameof(physicalHex));
        if (territoryCells is not { } cells)
        {
            cells = new StrategicTargetTerritoryCell[48400];
            for (int index = 0; index < cells.Length; index++) cells[index] = territory(index);
            territoryCells = cells;
        }
        return cells[physicalHex];
    }

    internal StrategicTargetMapQuery(int commandKind, StrategicCommandTargetKind kind, bool secondary, int mapMode,
        string guide, bool initialMap, IReadOnlyList<StrategicCommandTargetChoice> candidates,
        Func<int, StrategicTargetMapPick> pick,
        Func<StrategicMapMarkerKind, int, StrategicTargetMarkerState> marker,
        Func<int, StrategicTargetTerritoryCell> territory)
    {
        CommandKind = commandKind; Kind = kind; IsSecondary = secondary; MapMode = mapMode;
        GuideText = guide; InitialMap = initialMap; choices = candidates.ToDictionary(row => row.Id);
        this.pick = pick; this.marker = marker; this.territory = territory;
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal StrategicTargetMapQuery CreateStrategicTargetMapQueryCurrent(GameState state, SourceWorldContext current,
        int forceId, int commandKind, bool secondary, IReadOnlyList<StrategicCommandTargetChoice> choices)
    {
        var kind = commandKind switch
        {
            10 => StrategicCommandTargetKind.Force,
            11 when !secondary => StrategicCommandTargetKind.City,
            13 or 14 or 20 when !secondary => StrategicCommandTargetKind.Area,
            _ => throw StrategicMapMissing("caller_kind")
        };
        int mode = commandKind == 10 ? secondary ? 10 : 9 : commandKind == 11 ? 11 : commandKind == 13 ? 13 : commandKind == 14 ? 14 : 15;
        var allowed = choices.ToDictionary(row => row.Id);
        bool Enabled(int id) => allowed.TryGetValue(id, out var row) && row.Enabled == true;
        var query = new StrategicTargetMapQuery(commandKind,kind,secondary,mode,
            StrategicSelectorGuide(secondary ? StrategicSelectionRole.SecondForce : StrategicSelectionRole.Target,commandKind),
            ReadStrategicMapInitialMode(state,commandKind),choices,Pick,Marker,Territory);
        return query;

        HexReference Hex(int index) => current.ReadHexTableReference(index) ?? throw StrategicMapMissing("Hex_pointer");
        AreaReference Area(HexReference hex)
        {
            int raw = StrategicMapNeed(current.ReadHexAreaSlot(hex), "Hex_Area12");
            return current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw StrategicMapMissing("Area_pointer");
        }
        StrategicTargetMapPick Pick(int index)
        {
            if ((uint)index >= 48400) return new(null,false);
            try
            {
                var hex = Hex(index);
                int id;
                if (kind == StrategicCommandTargetKind.Force)
                {
                    int raw = StrategicMapNeed(current.ReadHexForceSlot(hex), "Hex_Force14");
                    var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw StrategicMapMissing("Force_pointer");
                    if (!StrategicMapNeed(current.ReadForceNativeValidity(force), "Force_validity")) return new(null,false);
                    id = StrategicMapNeed(force.PhysicalSlot, "Force_identity");
                }
                else
                {
                    var area = Area(hex);
                    if (!StrategicMapNeed(current.ReadAreaNativeValidity(area), "Area_validity")) return new(null,false);
                    if (kind == StrategicCommandTargetKind.Area) id = StrategicMapNeed(area.PhysicalSlot, "Area_identity");
                    else
                    {
                        int raw = StrategicMapNeed(current.ReadAreaCitySlot(area), "Area_City35");
                        var city = current.ReadCityTableReference(raw < 52 ? raw : 0) ?? throw StrategicMapMissing("City_pointer");
                        if (!StrategicMapNeed(current.ReadBaseNativeValidity(city), "City_validity")) return new(null,false);
                        id = StrategicMapNeed(city.PhysicalSlot, "City_identity");
                    }
                }
                return new(id,Enabled(id));
            }
            catch (InvalidDataException error) { return new(null,null,error.Message); }
        }
        StrategicTargetMarkerState Marker(StrategicMapMarkerKind markerKind, int id)
        {
            try
            {
                if (kind == StrategicCommandTargetKind.Force) return new(null,false);
                if (markerKind == StrategicMapMarkerKind.Area)
                {
                    if (kind != StrategicCommandTargetKind.Area) return new(null,false);
                    var area = current.ReadAreaTableReference(id) ?? throw StrategicMapMissing("marker_Area_pointer");
                    if (!StrategicMapNeed(current.ReadAreaNativeValidity(area), "marker_Area_validity")) return new(null,false);
                    int actual = StrategicMapNeed(area.PhysicalSlot, "marker_Area_identity");
                    return new(actual,Enabled(actual));
                }
                var basis = BaseLookup.Find(id,current);
                if (!StrategicMapNeed(current.ReadBaseNativeValidity(basis), "marker_Base_validity")) return new(null,false);
                if (kind == StrategicCommandTargetKind.City)
                {
                    if (basis.Kind != BaseKind.City) return new(null,false);
                    int actual = StrategicMapNeed(basis.PhysicalSlot, "marker_City_identity");
                    return new(actual,Enabled(actual));
                }
                var owner = CurrentGateMapQueries.FindArea(basis,current);
                if (!StrategicMapNeed(current.ReadAreaNativeValidity(owner), "marker_Base_Area_validity")) return new(null,false);
                int identity = StrategicMapNeed(owner.PhysicalSlot, "marker_Base_Area_identity");
                return new(identity,Enabled(identity));
            }
            catch (InvalidDataException error) { return new(null,null,error.Message); }
        }
        StrategicTargetTerritoryCell Territory(int index)
        {
            var hex = Hex(index);
            if (kind == StrategicCommandTargetKind.Force)
            {
                byte raw = StrategicMapNeed(current.ReadHexForceSlot(hex), "territory_Hex14");
                var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw StrategicMapMissing("territory_Force_pointer");
                return new(raw,StrategicMapNeed(current.ReadForceReportColor(force), "territory_Force49"));
            }
            if (kind == StrategicCommandTargetKind.Area)
                return new(StrategicMapNeed(current.ReadHexAreaSlot(hex), "territory_Hex12"));
            return new(StrategicMapNeed(current.ReadAreaCitySlot(Area(hex)), "territory_Area35"));
        }
    }

    private bool ReadStrategicMapInitialMode(GameState state, int kind)
    {
        if (kind is not (13 or 14 or 20)) return false;
        if (kind != 13) return true;
        //750AF0 tests the presence of570850, then17A470(5C). This
        //does not infer that bitmap from the command's World or active flag.
        bool present = StrategicMapNeed(ReadApplicationGameplay()?.TutorialServicePresent, "initial_tutorial_service");
        return !present || !StrategicMapNeed(TacticRemakeCurrentBattleControl.Read(ReadTutorialControlState(state),0x5C),
            "initial_tutorial_5C");
    }

    private static T StrategicMapNeed<T>(T? value, string field) where T : struct => value ?? throw StrategicMapMissing(field);
    private static InvalidDataException StrategicMapMissing(string field) => new("strategic.target_map." + field);
}

using San14.Core;

namespace San14.Data;

public sealed record ArmyMapUiCurrentRow(int UnitId)
{
    public int? ArmyPhysicalSlot { get; init; }
    public bool? ArmyValid { get; init; }
    public ushort? Commander12 { get; init; }
    public ushort? Ready16 { get; init; }
    public byte? Morale1A { get; init; }
    public int? MoraleCapacity { get; init; }
    public int? EffectiveMorale { get; init; }
    public int? PersonPhysicalSlot { get; init; }
    public bool? PersonValid { get; init; }
    public ushort? AppearanceA6 { get; init; }
    public byte? PersonDistrictE0 { get; init; }
    public int? DistrictPhysicalSlot { get; init; }
    public bool? DistrictValid { get; init; }
    public byte? DistrictNumber11 { get; init; }
    public bool? DistrictPredicate233A90 { get; init; }
    public int? ForcePhysicalSlot { get; init; }
    /// <summary>Actual original Force definition style. Saved edited/current
    /// color49 mutation is not yet represented by the current World.</summary>
    public byte? SourceForceColor49 { get; init; }
    public bool? IsCurrentPlayerForce { get; init; }
    public byte? Army13C { get; init; }
    public byte? SelectedFormation { get; init; }
    public byte? FormationClass7E { get; init; }
    public ArmyMapIconSelection? Icon { get; init; }
}
public sealed record ArmyMapUiCurrentUnavailable(int UnitId, string Field);
public sealed record ArmyMapUiCurrentFrame(IReadOnlyList<ArmyMapUiCurrentRow> Rows,
    IReadOnlyList<ArmyMapUiCurrentUnavailable> Unavailable);

/// <summary>Reads one current World and its already captured Person/District/
/// Force context. It never imports the Army return arena, updates simulation,
/// or substitutes provisional morale/formation/portrait identities.</summary>
public sealed class ArmyMapUiCurrentSource
{
    private readonly Dictionary<int, int> _appearanceByPhysical;
    private readonly byte[] _terrain2C;

    public ArmyMapUiCurrentSource(ScenarioCityPreview scenario, LoadedTerrainTypes terrain)
    {
        _appearanceByPhysical = scenario.AffiliatedCharacters.ToDictionary(person => person.Slot,
            person => person.AppearanceCode);
        if (terrain.Catalog.Types.Count != 31) throw new InvalidDataException("Army UI needs the original terrain definitions.");
        _terrain2C = terrain.Catalog.Types.Select((row, index) => row.Slot == index
            ? row.RawField2C : throw new InvalidDataException("Army UI terrain physical row differs.")).ToArray();
    }

    public ArmyMapUiCurrentFrame Capture(GameState world, SourceWorldContext current)
    {
        var rows = new List<ArmyMapUiCurrentRow>();
        var missing = new List<ArmyMapUiCurrentUnavailable>();
        var player = current.ReadCurrentPlayerForce();
        int? playerPhysical = player?.PhysicalSlot;
        foreach (var unit in world.Units.Values)
        {
            int id = unit.Id;
            var row = new ArmyMapUiCurrentRow(id);
            if (unit.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
                world.NativeArmyLifecycle?.Slots is not { } slots || physical >= slots.Length ||
                slots[physical].PhysicalSlot != physical || slots[physical].ActiveUnitId != id)
            {
                missing.Add(new(id, "current.Army_saved_receiver")); rows.Add(row); continue;
            }
            var saved = slots[physical];
            bool? armyValid = saved.Commander12 is ushort commander && saved.Status10 is byte status
                ? commander != 0 && status != 0 : null;
            row = row with { ArmyPhysicalSlot = physical, ArmyValid = armyValid, Commander12 = saved.Commander12,
                Ready16 = unit.Troops is >= 0 and <= ushort.MaxValue ? (ushort)unit.Troops : null,
                Morale1A = unit.NativeMorale1A, Army13C = unit.NativeMapUiByte13C };
            if (armyValid != true) { missing.Add(new(id, "current.Army_validity")); rows.Add(row); continue; }
            if (row.Ready16 is null) missing.Add(new(id, "current.Army_ready16"));
            if (row.Morale1A is null) missing.Add(new(id, "current.Army_raw1A"));

            var person = current.ReadPersonLookupReference(row.Commander12!.Value <= 6000 ? row.Commander12.Value : 0);
            int? personPhysical = person?.PhysicalSlot;
            bool? personValid = personPhysical is int at ? current.ReadPersonNativeValidity(at) : person is null ? null : false;
            byte? personDistrictRaw = personPhysical is int personAt ? current.ReadPersonDistrictSlot(personAt) : null;
            byte? districtRaw = personValid == true ? personDistrictRaw :
                personValid == false ? (byte)0 : null;
            var district = districtRaw is byte raw ? current.ReadDistrictTableReference(raw <= 51 ? raw : 0) : null;
            int? districtPhysical = district?.PhysicalSlot;
            bool? districtValid = district is not null ? current.ReadDistrictNativeValidity(district) : null;
            byte? districtNumber = districtPhysical is int dp ? current.ReadDistrictNumber(dp) : null;
            byte? forceRaw = districtValid == true && districtPhysical is int fd ? current.ReadDistrictForceId(fd) :
                districtValid == false ? (byte)0 : null;
            var force = forceRaw is byte fr ? current.ReadForceTableReference(fr <= 51 ? fr : 0) : null;
            int? forcePhysical = force?.PhysicalSlot;
            ushort? appearance = personPhysical is int pp && _appearanceByPhysical.TryGetValue(pp, out int a6) &&
                a6 is >= 0 and <= ushort.MaxValue ? (ushort)a6 : null;
            row = row with { PersonPhysicalSlot = personPhysical, PersonValid = personValid, AppearanceA6 = appearance,
                PersonDistrictE0 = personDistrictRaw, DistrictPhysicalSlot = districtPhysical, DistrictValid = districtValid,
                DistrictNumber11 = districtNumber, ForcePhysicalSlot = forcePhysical,
                SourceForceColor49 = force?.PhysicalSlot is int ? current.ReadForceReportColor(force) : null,
                IsCurrentPlayerForce = forcePhysical is int f && playerPhysical is int p ? f == p : null };
            if (appearance is null) missing.Add(new(id, "current.Person_appearanceA6"));
            if (district is null || districtValid is null) missing.Add(new(id, "current.Commander_DistrictE0"));
            if (force is not null) try { row = row with { MoraleCapacity = BasePostLoad.ReadMoraleUpper(force, current) }; }
                catch (InvalidDataException) { missing.Add(new(id, "current.Force_morale_cap")); }
            else missing.Add(new(id, "current.Army_Force"));
            if (row.SourceForceColor49 is null) missing.Add(new(id, "source.Force_color49_style"));
            if (row.Morale1A is byte morale && row.MoraleCapacity is int cap)
                row = row with { EffectiveMorale = Math.Min((int)morale, cap) };

            ushort? hex = unit.NativeMarch?.CurrentHex2A ?? unit.NativeCurrentHex2A;
            var hexPointer = hex is ushort hx ? current.ReadHexTableReference(hx <= 48399 ? hx : 0) : null;
            byte? surface = hexPointer is not null ? current.ReadHexSurfaceCode(hexPointer) : null;
            byte? selected = surface is byte code ? ((_terrain2C[code <= 30 ? code : 0] & 2) != 0
                ? unit.NativeArmyConfiguration?.Formation1C : unit.NativeArmyConfiguration?.Formation1B) : null;
            byte? formationClass = selected is byte formation ? TacticRemakeCurrentFormations.ReadRawByte(world,
                formation <= 20 ? formation : 0, 0x7E) : null;
            bool? predicate = row.IsCurrentPlayerForce == true && districtValid == true && district is not null
                ? ReadDistrictPlayerLeader233A90(current, district) : null;
            // Only actual creator/reset receipts establish this saved byte.
            // Missing legacy fields block the reached player icon, not portraits.
            row = row with { SelectedFormation = selected, FormationClass7E = formationClass,
                DistrictPredicate233A90 = predicate };
            row = row with { Icon = SelectIcon(row.IsCurrentPlayerForce, row.Army13C, districtValid,
                predicate, districtNumber, selected, formationClass) };
            if (selected is null) missing.Add(new(id, "current.Army_selected_1B_or_1C"));
            if (formationClass is null) missing.Add(new(id, "current.Formation7E"));
            if (row.IsCurrentPlayerForce is null) missing.Add(new(id, "current.Player_Force"));
            else if (row.IsCurrentPlayerForce == true && row.Army13C is null) missing.Add(new(id, "current.Army13C"));
            if (row.IsCurrentPlayerForce == true && districtValid == true && predicate is null)
                missing.Add(new(id, "current.District_233A90"));
            rows.Add(row);
        }
        return new(rows.AsReadOnly(), missing.AsReadOnly());
    }

    public static ArmyMapIconSelection? SelectIcon(bool? player, byte? army13C, bool? districtValid,
        bool? predicate233A90, byte? districtNumber11, byte? selectedFormation, byte? formationClass7E)
    {
        if (player is null) return null;
        if (player == true)
        {
            if (army13C is null) return null;
            if (army13C != 0) return ArmyMapUiSources.SpecialPlayerIcon();
            if (districtValid is null) return null;
            if (districtValid == true)
            {
                if (predicate233A90 is null) return null;
                if (predicate233A90 == false) return districtNumber11 is byte number
                    ? ArmyMapUiSources.DistrictIcon(number) : null;
            }
        }
        return selectedFormation is byte formation && formationClass7E is byte kind
            ? ArmyMapUiSources.FormationIcon(formation, kind) : null;
    }

    // Source233A90: the actual current player Force, District10 and its leader's
    // current validity/E6. This is not District.Number==1 or Core officer Role.
    public static bool? ReadDistrictPlayerLeader233A90(SourceWorldContext current,
        DistrictReference district)
    {
        var player = current.ReadCurrentPlayerForce();
        if (player is null || current.ReadForceNativeValidity(player) is not bool valid) return null;
        if (!valid) return false;
        if (district.PhysicalSlot is not int physical || player.PhysicalSlot is not int playerPhysical ||
            current.ReadDistrictForceId(physical) is not byte force) return null;
        if (force != playerPhysical) return false;
        if (current.ReadDistrictLeader(physical) is not ushort leader) return null;
        var person = current.ReadPersonLookupReference(leader <= 6000 ? leader : 0);
        if (person is null) return null;
        if (person.PhysicalSlot is not int at) return false;
        if (current.ReadPersonNativeValidity(at) is not bool personValid) return null;
        if (!personValid) return false;
        return current.ReadPersonStatus(at) is byte status ? status == 1 : null;
    }
}

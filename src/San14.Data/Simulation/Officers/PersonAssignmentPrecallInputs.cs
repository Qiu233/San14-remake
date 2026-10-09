using San14.Core;

namespace San14.Data;

/// <summary>Owned29C430/2783B0 current fields after real constructor/static
/// loading. Force52 reuses the existing policy bank; runtime-only fields are
/// installed once and never reconstructed while loading a saved campaign.</summary>
public sealed class PersonAssignmentPrecallInputs
{
    private const string Prefix = "ce-person-assignment-precall-current:";
    public const string VersionKey = Prefix + "version";
    public static string CityFieldKey(int physical, int offset) => Prefix + $"city:{physical}:{offset:X}";
    public static string AreaFieldKey(int physical, int offset) => Prefix + $"area:{physical}:{offset:X}";
    public static string AssociationPersonKey(int physical, int index) => Prefix + $"force:{physical}:196:{index}";
    public static string AssociationTargetKey(int physical, int index) => Prefix + $"force:{physical}:19C:{index}";
    public static string PersonAreaLengthKey(int physical) => Prefix + $"person:{physical}:area-vector:length";
    public static string PersonAreaCount1E8Key(int physical) => Prefix + $"person:{physical}:1E8";
    public static string PersonAreaEntryKey(int physical, int index) => Prefix + $"person:{physical}:1D0:{index}";
    //SC semantic offsets; TC240DB0 stores these at1C7/1C6. Missing keys
    //remain unknown until an actual current source writer supplies values.
    public static string PersonResetWord1FFKey(int physical) => Prefix + $"person:{physical}:1FF";
    public static string PersonResetByte1FEKey(int physical) => Prefix + $"person:{physical}:1FE";
    private readonly LoadedScenario _source;
    private readonly string _ruleVersion;
    private PersonAssignmentPrecallInputs(LoadedScenario source, string language)
    {
        _source = source;
        _ruleVersion = SimulationRuleIdentity.ForLocale(language);
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != language ||
            source.Snapshot.Cities.Count != 52 || source.Snapshot.Areas.Count != 501 || source.Snapshot.Forces.Count != 52 || source.Snapshot.Characters.Count != 1851 ||
            !source.Snapshot.Cities.Select(x => x.Slot).SequenceEqual(Enumerable.Range(0, 52)) ||
            !source.Snapshot.Areas.Select(x => x.Slot).SequenceEqual(Enumerable.Range(0, 501)) ||
            !source.Snapshot.Forces.Select(x => x.Slot).SequenceEqual(Enumerable.Range(0, 52)))
            throw new InvalidDataException("Assignment precalls need complete physical CE City/Area/Force tables.");
    }
    public static PersonAssignmentPrecallInputs Load(GameResourceRepository repository, LoadedScenario source, string language)
    { ArgumentNullException.ThrowIfNull(repository); return new(source, language); }
    public GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != _source.Snapshot.Locale ||
            simulation.Parameters.RuleVersion != _ruleVersion || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri) != _source.ScenarioSource.Sha256 ||
            simulation.Parameters.Int32Values.Keys.Any(x => x.StartsWith(Prefix, StringComparison.Ordinal)))
            throw new InvalidDataException("Assignment precalls need their unused fresh current owner.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values) { [VersionKey] = 1 };
        //220B70/226CD0 set City qwordC8=10, so wordsCA/CC are0.
        //236E80's static archive8C!=0 branch omits those runtime words.
        for (int city = 0; city < 52; city++)
        { values.Add(CityFieldKey(city, 0xCA), 0); values.Add(CityFieldKey(city, 0xCC), 0); }
        //220900/226950 clear Area dword34, word6D and dword70.
        //235800 static loading omits officer36, bonus6D and focus72.
        for (int area = 0; area < 501; area++)
        {
            values.Add(AreaFieldKey(area, 0x36), 0);
            values.Add(AreaFieldKey(area, 0x6D), 0); values.Add(AreaFieldKey(area, 0x72), 0);
        }
        //227070 initializes all three current pairs;237CC0's static
        //branch omits them. This is unrelated to Force193/178.
        for (int force = 0; force < 52; force++)
            for (int i = 0; i < 3; i++)
            { values.Add(AssociationPersonKey(force, i), 0); values.Add(AssociationTargetKey(force, i), 0); }
        //2277B0 resets end=begin, then2425E0 really resizes to three
        //zero Int32 entries and writes activecount1E8=0.2391C0 static
        //loading omits both the runtime vector and its active count.
        for (int person = 0; person < 1851; person++)
        {
            values.Add(PersonAreaLengthKey(person), 3); values.Add(PersonAreaCount1E8Key(person), 0);
            for (int i = 0; i < 3; i++) values.Add(PersonAreaEntryKey(person, i), 0);
        }
        //Validate the already loaded owner instead of copying Force52 into
        //a second fact. Its source is CE329 row59 via237CC0.
        for (int force = 1; force < 52; force++)
            if (!values.TryGetValue(BattleRemakeCurrentForcePolicies.ForceFieldKey(force, 0x52), out int advisor) ||
                advisor is < 0 or > 65535)
                throw new InvalidDataException("The current original Force52 policy owner must precede fresh assignment inputs.");
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    public void BindCurrent(GameState state, SourceWorldContext current)
    {
        bool installed = state.NativeArmySimulation?.Parameters.Int32Values.GetValueOrDefault(VersionKey) == 1;
        for (int force = 0; force < 52; force++)
        {
            current.SetAssignmentForceAdvisor52(force, !installed ? null : force == 0 ? (ushort)0 :
                Word(state, BattleRemakeCurrentForcePolicies.ForceFieldKey(force, 0x52), false));
            for (int i = 0; i < 3; i++)
            {
                current.SetAssignmentForceAssociation(force, i, Word(state, AssociationPersonKey(force, i)),
                    Byte(state, AssociationTargetKey(force, i)));
            }
        }
        for (int city = 0; city < 52; city++)
        {
            var duties = state.Cities.GetValueOrDefault(city)?.Duties;
            current.SetAssignmentCityCA(city, duties is null ? Word(state, CityFieldKey(city, 0xCA)) : Id(duties.RecruiterId));
            current.SetAssignmentCityCC(city, duties is null ? Word(state, CityFieldKey(city, 0xCC)) : Id(duties.TrainerId));
        }
        for (int area = 0; area < 501; area++)
        {
            var administration = state.Areas.GetValueOrDefault(area)?.Administration;
            current.SetAssignmentArea36(area, administration is null ? Word(state, AreaFieldKey(area, 0x36)) : Id(administration.OverseerId));
            current.SetAssignmentArea72(area, administration is null ? Byte(state, AreaFieldKey(area, 0x72)) :
                administration.DevelopmentFocus switch
                { AreaDevelopmentFocus.None => 0, AreaDevelopmentFocus.Commerce => 1,
                  AreaDevelopmentFocus.Agriculture => 2, AreaDevelopmentFocus.Barracks => 3, _ => null });
            current.SetAssignmentArea6D(area, Byte(state, AreaFieldKey(area, 0x6D)));
        }
        for (int person = 0; person < 1851; person++)
        {
            if (Word(state, PersonResetWord1FFKey(person)) is ushort word) current.WritePersonResetWord1FF(person, word);
            if (Byte(state, PersonResetByte1FEKey(person)) is byte flag) current.WritePersonResetByte1FE(person, flag);
            int? length = Value(state, PersonAreaLengthKey(person), true), count = Value(state, PersonAreaCount1E8Key(person), true);
            int[]? vector = null;
            if (length is >= 0 && count.HasValue)
            {
                var entries = new int[length.Value]; bool known = true;
                for (int i = 0; i < entries.Length; i++)
                    if (Value(state, PersonAreaEntryKey(person, i), true) is int value) entries[i] = value;
                    else { known = false; break; }
                if (known) vector = entries;
            }
            current.SetAssignmentPersonAreaVector(person, vector is null || !count.HasValue ? null : new(vector, count.Value));
        }
        current.AssignmentAreaOfficerRemoval = (area, oldId) =>
        {
            var result = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, oldId);
            return result.NativeReturn.HasValue ? result.NativeReturn != 0 : throw new InvalidDataException(result.UnresolvedOwner);
        };
    }
    public static GameState PublishCurrent(GameState state, SourceWorldContext current)
    {
        if (state.NativeArmySimulation is not { } simulation || simulation.Parameters.Int32Values.GetValueOrDefault(VersionKey) != 1)
            throw new InvalidDataException("The saved assignment precall owner is unknown.");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        for (int force = 0; force < 52; force++)
        {
            //A later source module can update this same saved bank. Only
            //publish actual29C430 stores, never an old untouched bound view.
            if (force != 0 && current.WasAssignmentForceAdvisor52Written(force))
                values[BattleRemakeCurrentForcePolicies.ForceFieldKey(force, 0x52)] = Need(current.ReadAssignmentForceAdvisor52(force));
            for (int i = 0; i < 3; i++)
            {
                values[AssociationPersonKey(force, i)] = Need(current.ReadForceAssociationPersonId(new(force), i));
                values[AssociationTargetKey(force, i)] = Need(current.ReadForceAssociationTargetSlot(new(force), i));
            }
        }
        var cities = new Dictionary<int, CityState>(state.Cities);
        for (int city = 0; city < 52; city++)
        {
            ushort ca = Need(current.ReadAssignmentCityCA(city)), cc = Need(current.ReadAssignmentCityCC(city));
            values[CityFieldKey(city, 0xCA)] = ca; values[CityFieldKey(city, 0xCC)] = cc;
            if (cities.TryGetValue(city, out var row) && row.Duties is { } duties)
                cities[city] = row with { Duties = duties with { RecruiterId = ca == 0 ? null : ca, TrainerId = cc == 0 ? null : cc } };
        }
        var areas = new Dictionary<int, AreaState>(state.Areas);
        for (int area = 0; area < 501; area++)
        {
            ushort officer = Need(current.ReadAssignmentArea36(area));
            byte focus = Need(current.ReadAssignmentArea72(area)), bonus = Need(current.ReadAssignmentArea6D(area));
            values[AreaFieldKey(area, 0x36)] = officer; values[AreaFieldKey(area, 0x72)] = focus; values[AreaFieldKey(area, 0x6D)] = bonus;
            if (areas.TryGetValue(area, out var row) && row.Administration is { })
                areas[area] = row with { Administration = new(officer == 0 ? null : officer, AreaAdministration.DecodeFocus(focus)) };
        }
        for (int person = 0; person < 1851; person++)
        {
            if (current.ReadPersonResetWord1FF(person) is ushort word) values[PersonResetWord1FFKey(person)] = word;
            if (current.ReadPersonResetByte1FE(person) is byte flag) values[PersonResetByte1FEKey(person)] = flag;
            var fields = current.ReadAssignmentPersonAreaVector(person)
                ?? throw new InvalidDataException("Current Person1D0/1E8 assignment owner is unknown during publication.");
            values[PersonAreaLengthKey(person)] = fields.Values.Length; values[PersonAreaCount1E8Key(person)] = fields.ActiveCount1E8;
            for (int i = 0; i < fields.Values.Length; i++) values[PersonAreaEntryKey(person, i)] = fields.Values[i];
        }
        return state with { Cities = cities, Areas = areas, NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }
    private static ushort? Id(int? id) => id is null ? (ushort)0 : id is >= 0 and <= 65535 ? (ushort)id : null;
    private static ushort? Word(GameState state, string key, bool own = true) => Value(state, key, own) is int v && v is >= 0 and <= 65535 ? (ushort)v : null;
    private static byte? Byte(GameState state, string key) => Value(state, key, true) is int v && v is >= 0 and <= 255 ? (byte)v : null;
    private static int? Value(GameState state, string key, bool own) => state.NativeArmySimulation?.Parameters.Int32Values is { } values &&
        (!own || values.GetValueOrDefault(VersionKey) == 1) && values.TryGetValue(key, out int v) ? v : null;
    private static T Need<T>(T? value) where T : struct => value ?? throw new InvalidDataException("A current assignment precall field is unknown during publication.");
}

public sealed record PersonAreaAssignmentVector(int[] Values, int ActiveCount1E8);

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, ushort?> _assignmentAdvisors52 = [], _assignmentCityCA = [], _assignmentCityCC = [], _assignmentArea36 = [];
    private readonly Dictionary<int, byte?> _assignmentArea72 = [], _assignmentArea6D = [];
    private readonly Dictionary<int, PersonAreaAssignmentVector?> _assignmentPersonAreas = [];
    private readonly HashSet<int> _assignmentAdvisorsWritten52 = [];
    public Func<ForceReference, bool?>? AssignmentAdvisorResetCallbacks { get; set; }
    public Func<int, ushort, bool?>? AssignmentAreaOfficerRemoval { get; set; }
    public void SetAssignmentForceAdvisor52(int p, ushort? value) { Check(p, 52, "Force advisor52"); _assignmentAdvisors52[p] = value; }
    public void WriteAssignmentForceAdvisor52(int p, ushort value)
    { SetAssignmentForceAdvisor52(p, value); _assignmentAdvisorsWritten52.Add(p); }
    public bool WasAssignmentForceAdvisor52Written(int p)
    { Check(p, 52, "Force advisor52"); return _assignmentAdvisorsWritten52.Contains(p); }
    public ushort? ReadAssignmentForceAdvisor52(int p) { Check(p, 52, "Force advisor52"); return _assignmentAdvisors52.GetValueOrDefault(p); }
    public void SetAssignmentCityCA(int p, ushort? value) { Check(p, 52, "CityCA"); _assignmentCityCA[p] = value; }
    public void SetAssignmentCityCC(int p, ushort? value) { Check(p, 52, "CityCC"); _assignmentCityCC[p] = value; }
    public ushort? ReadAssignmentCityCA(int p) { Check(p, 52, "CityCA"); return _assignmentCityCA.GetValueOrDefault(p); }
    public ushort? ReadAssignmentCityCC(int p) { Check(p, 52, "CityCC"); return _assignmentCityCC.GetValueOrDefault(p); }
    public void SetAssignmentArea36(int p, ushort? value) { Check(p, 501, "Area36"); _assignmentArea36[p] = value; }
    public void SetAssignmentArea72(int p, byte? value) { Check(p, 501, "Area72"); _assignmentArea72[p] = value; }
    public void SetAssignmentArea6D(int p, byte? value) { Check(p, 501, "Area6D"); _assignmentArea6D[p] = value; }
    public ushort? ReadAssignmentArea36(int p) { Check(p, 501, "Area36"); return _assignmentArea36.GetValueOrDefault(p); }
    public byte? ReadAssignmentArea72(int p) { Check(p, 501, "Area72"); return _assignmentArea72.GetValueOrDefault(p); }
    public byte? ReadAssignmentArea6D(int p) { Check(p, 501, "Area6D"); return _assignmentArea6D.GetValueOrDefault(p); }
    public void SetAssignmentForceAssociation(int p, int i, ushort? person, byte? target)
    { Check(p, 52, "Force association"); Check(i, 3, "Force association ordinal"); _associationPersons[(p, i)] = person; _associationTargets[(p, i)] = target; }
    public void SetAssignmentPersonAreaVector(int p, PersonAreaAssignmentVector? fields)
    { Check(p, 1851, "Person area vector1D0"); _assignmentPersonAreas[p] = fields; }
    public PersonAreaAssignmentVector? ReadAssignmentPersonAreaVector(int p)
    { Check(p, 1851, "Person area vector1D0"); return _assignmentPersonAreas.GetValueOrDefault(p); }
}

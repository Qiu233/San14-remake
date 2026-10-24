using System.Text.Json;
using System.Text.Json.Serialization;
using San14.Core;
using San14.Data;

/// <summary>Application preferences have a lifetime independent of campaigns.
/// Only implemented current consumers are stored; missing files create the
/// source fresh choices, while malformed existing files are reported.</summary>
internal sealed class ClientPreferences
{
    private sealed record FileRecord
    {
        [JsonRequired] public int Version { get; init; }
        [JsonRequired] public MapEdgeScrollSpeed EdgeScroll { get; init; }
        [JsonRequired] public MapKeyboardScrollSpeed KeyboardScroll { get; init; }
        public AudioVolumeOptions? Audio { get; init; }
        public ListScrollSpeed? ListScroll { get; init; }
        public MapMusicPreferenceFile? MusicSelection { get; init; }
        public uint[]? ScenarioUnlocks { get; init; }
        public int[]? CompletedScenarios { get; init; }
        public bool? AutomaticSaving { get; init; }
        public VoicePreferences? Voice { get; init; }
        public AutomaticMessageAdvanceMode? AutomaticMessageAdvance { get; init; }
        public int[]? KeyboardBindings { get; init; }
    }
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string path;
    private readonly KeyboardBindingCatalog keyboardCatalog;
    internal MapCameraNavigationOptions Navigation { get; private set; }
    internal AudioVolumeOptions Audio { get; private set; }
    internal ListScrollSpeed ListScroll { get; private set; }
    internal MapMusicPreferences MusicSelection { get; private set; }
    internal bool AutomaticSaving { get; private set; } = true;
    internal VoicePreferences Voice { get; private set; } = new();
    internal AutomaticMessageAdvanceMode AutomaticMessageAdvance { get; private set; }
    internal IReadOnlyList<int> KeyboardBindings { get; private set; }
    private uint[] scenarioUnlocks = [0, 0, 0];
    private HashSet<int> completedScenarios = [];
    internal IReadOnlyList<uint> ScenarioUnlocks => Array.AsReadOnly(scenarioUnlocks);
    internal bool IsScenarioCompleted(int id) => completedScenarios.Contains(id);

    internal ClientPreferences(string path, KeyboardBindingCatalog keyboardCatalog)
    {
        this.path = Path.GetFullPath(path);
        this.keyboardCatalog = keyboardCatalog ?? throw new ArgumentNullException(nameof(keyboardCatalog));
        KeyboardBindings = CopyKeyboardBindings(this.keyboardCatalog.DefaultBindings);
        if (!File.Exists(this.path)) { Navigation = new(); Audio = new(); MusicSelection = new(); return; }
        var file = JsonSerializer.Deserialize<FileRecord>(File.ReadAllBytes(this.path), Json)
            ?? throw new InvalidDataException("Application preferences are null.");
        Audio = file.Version switch
        {
            //Version1's audio always used unity gain. Preserve that actual
            //previous behavior when adding independent mixer preferences.
            1 => new(),
            2 or 3 or 4 or 5 or 6 or 7 or 8 => file.Audio ?? throw new InvalidDataException("Application audio preferences are missing."),
            _ => throw new InvalidDataException($"Unsupported application preferences version {file.Version}.")
        };
        Navigation = new(file.EdgeScroll, file.KeyboardScroll);
        ListScroll = file.Version >= 3 ? file.ListScroll ?? throw new InvalidDataException("Application list scrolling preference is missing.") : ListScrollSpeed.Slow;
        MusicSelection = file.Version >= 4
            ? new(file.MusicSelection ?? throw new InvalidDataException("Application music selection preferences are missing."))
            : new();
        // Earlier project versions never wrote automatic campaign files.
        // Preserve that behavior on migration; fresh profiles use the source
        // default, and the settings page can enable it explicitly.
        AutomaticSaving = file.Version >= 6
            ? file.AutomaticSaving ?? throw new InvalidDataException("Application automatic saving preference is missing.")
            : false;
        // Voice requests are a new application feature. Earlier project
        // preferences have no historical voice choice to restore.
        Voice = file.Version >= 7 ? file.Voice ?? throw new InvalidDataException("Application voice preferences are missing.") : new();
        _ = Voice.Capture();
        AutomaticMessageAdvance = file.Version >= 7
            ? file.AutomaticMessageAdvance ?? throw new InvalidDataException("Application message advance preference is missing.")
            : AutomaticMessageAdvanceMode.Manual;
        if (!Enum.IsDefined(AutomaticMessageAdvance)) throw new InvalidDataException("Application message advance preference is invalid.");
        if (file.Version >= 8) KeyboardBindings = CopyKeyboardBindings(file.KeyboardBindings ??
            throw new InvalidDataException("Application keyboard bindings are missing."));
        // Earlier project preferences never recorded scenario history. Start
        // that project history empty; do not claim an original prdata restore.
        if (file.Version >= 5)
        {
            scenarioUnlocks = file.ScenarioUnlocks is { Length: 3 } unlocks ? unlocks
                : throw new InvalidDataException("Scenario unlock history is missing.");
            var completed = file.CompletedScenarios
                ?? throw new InvalidDataException("Scenario completion history is missing.");
            if (completed.Any(id => id is < 0 or >= 200))
                throw new InvalidDataException("Scenario completion identity is invalid.");
            completedScenarios = completed.ToHashSet();
        }
        if (!Enum.IsDefined(ListScroll)) throw new InvalidDataException("Application list scrolling preference is invalid.");
        Navigation.Validate(); Audio.Validate();
    }

    internal void Save(MapCameraNavigationOptions navigation, AudioVolumeOptions audio, ListScrollSpeed listScroll,
        bool automaticSaving, VoicePreferences? voice = null, AutomaticMessageAdvanceMode? automaticMessageAdvance = null,
        MapMusicPreferenceFile? musicSelection = null, IReadOnlyList<int>? keyboardBindings = null)
    {
        navigation.Validate(); audio.Validate();
        if (!Enum.IsDefined(listScroll)) throw new ArgumentOutOfRangeException(nameof(listScroll));
        voice ??= Voice;
        _ = voice.Capture();
        var advance = automaticMessageAdvance ?? AutomaticMessageAdvance;
        if (!Enum.IsDefined(advance)) throw new ArgumentOutOfRangeException(nameof(automaticMessageAdvance));
        var music = musicSelection is null ? MusicSelection : new MapMusicPreferences(musicSelection);
        var keys = CopyKeyboardBindings(keyboardBindings ?? KeyboardBindings);
        var file = new FileRecord { Version = 8, EdgeScroll = navigation.Edge, KeyboardScroll = navigation.Keyboard,
            Audio = audio, ListScroll = listScroll, MusicSelection = music.Capture(),
            ScenarioUnlocks = scenarioUnlocks, CompletedScenarios = completedScenarios.Order().ToArray(),
            AutomaticSaving = automaticSaving, Voice = voice, AutomaticMessageAdvance = advance, KeyboardBindings = keys.ToArray() };
        AtomicSaveFiles.Write(path, JsonSerializer.SerializeToUtf8Bytes(file, Json));
        Navigation = navigation; Audio = audio; ListScroll = listScroll; AutomaticSaving = automaticSaving;
        Voice = voice;
        AutomaticMessageAdvance = advance;
        MusicSelection = music;
        KeyboardBindings = keys;
    }

    internal void CommitMusic(MapMusicPreferenceFile music) =>
        Save(Navigation, Audio, ListScroll, AutomaticSaving, Voice, AutomaticMessageAdvance, music);

    private IReadOnlyList<int> CopyKeyboardBindings(IReadOnlyList<int> bindings)
    {
        if (bindings.Count != KeyboardBindingCatalog.BindingCount || bindings.Any(value => value < 0 ||
            (value & ~0x3FFF) != 0 || value != 0 && value != KeyboardBindingCatalog.ReservedBinding &&
            !keyboardCatalog.Keys.Any(key => key.Usage == (value & 0xFFF))))
            throw new InvalidDataException("Application keyboard bindings have an invalid layout or key code.");
        return Array.AsReadOnly(bindings.ToArray());
    }
}

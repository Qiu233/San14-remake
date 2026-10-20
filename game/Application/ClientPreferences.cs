using System.Text.Json;
using System.Text.Json.Serialization;
using San14.Core;

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
    }
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string path;
    internal MapCameraNavigationOptions Navigation { get; private set; }
    internal AudioVolumeOptions Audio { get; private set; }
    internal ListScrollSpeed ListScroll { get; private set; }

    internal ClientPreferences(string path)
    {
        this.path = Path.GetFullPath(path);
        if (!File.Exists(this.path)) { Navigation = new(); Audio = new(); return; }
        var file = JsonSerializer.Deserialize<FileRecord>(File.ReadAllBytes(this.path), Json)
            ?? throw new InvalidDataException("Application preferences are null.");
        Audio = file.Version switch
        {
            //Version1's audio always used unity gain. Preserve that actual
            //previous behavior when adding independent mixer preferences.
            1 => new(),
            2 or 3 => file.Audio ?? throw new InvalidDataException("Application audio preferences are missing."),
            _ => throw new InvalidDataException($"Unsupported application preferences version {file.Version}.")
        };
        Navigation = new(file.EdgeScroll, file.KeyboardScroll);
        ListScroll = file.Version == 3 ? file.ListScroll ?? throw new InvalidDataException("Application list scrolling preference is missing.") : ListScrollSpeed.Slow;
        if (!Enum.IsDefined(ListScroll)) throw new InvalidDataException("Application list scrolling preference is invalid.");
        Navigation.Validate(); Audio.Validate();
    }

    internal void Save(MapCameraNavigationOptions navigation, AudioVolumeOptions audio, ListScrollSpeed listScroll)
    {
        navigation.Validate(); audio.Validate();
        if (!Enum.IsDefined(listScroll)) throw new ArgumentOutOfRangeException(nameof(listScroll));
        var file = new FileRecord { Version = 3, EdgeScroll = navigation.Edge, KeyboardScroll = navigation.Keyboard, Audio = audio, ListScroll = listScroll };
        AtomicSaveFiles.Write(path, JsonSerializer.SerializeToUtf8Bytes(file, Json));
        Navigation = navigation; Audio = audio; ListScroll = listScroll;
    }
}

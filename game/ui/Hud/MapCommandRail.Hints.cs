using Godot;
using San14.Data;

internal partial class MapCommandRail
{
    //All written help identities from the real main/category producer and
    //all seven original submenu groups.
    //The application startup owner prepares this entire finite closure.
    internal static readonly int[] HintMessageIds =
        [0x2AD4,0x2AB5,0x2AB4,0x2AB2,0x2AB3,0x3408,0x2AD3,
         0x11171,0x2ACE,0x3142,0x4F17,0x4F19,0x4F1A,0x4F1B,
         0x3152,0x3153,0x5BDE,0x5BDF,0x5BE0,0x57C7,0x5BE2,
         0x5BE3,0x5BE4,0x5BE5,0x5BE6,0x5A1D,0x5A1E,0x5C17,
         0x5C19,0x5C1A,0x5C1C,0x5C1D,0x5C21,0x5C22,0x0000,
         0x2AD2,0x57D8,0x57DA,0x57DB,0x5829,0x582B,0x582C,
         0x582E,0x582F,0x5831,0x5832,0x5833,0x5834,0x5835,
         0x5836,0x5982,0x5983,0x5C6C,0x5C6D,0x5C6E,0x5C6F,
         0x5C70,0x5C71,0x5C72,0x5C73,0x5CE0,0x5CF7,0x5CF8,
         0x5CF9,0x2ACF,0x2AD0,0x5C7E,0x5908,0x5909,0x590A,
         0x3140,0x3141,0x590B,0x57E1,0x5C0A,0x5C0C,0x57E2,
         0x5C10,0x5C0B,0x5801,0x5802,0x5803,0x5804,0x5805,
         0x5A1F,0x5A20,0x5A21,0x5A22,0x5A23,0x5A24,0x5A25,
         0x5A26,0x5A27,0x5A28,0x5A29,0x5A2A,0x5C1F,0x5C20,
         0x5992,0x598E,0x5994,0x5993,0x5996,0x5995,0x5990,
         0x5998,0x5997,0x5999,0x599B,0x599A,0x5991,0x599D,
         0x599C,0x599F,0x599E,0x59A1,0x59A0];
    private static readonly int[] CategoryHintMessages =
        [0x2AD4,0x2AB5,0x2AB4,0x2AB2,0x2AB3,0x3408,0x2AD3];
    private static readonly (MapMenuCommand Command, int Message)[] FixedEntryHints =
        [(MapMenuCommand.Policy,0x2ACE),(MapMenuCommand.Events,0x3142)];
    private readonly Dictionary<int,string> _fixedHintTexts = [];
    private readonly Dictionary<Control,MapCommandCategory> _categoryHintButtons = [];
    private readonly Dictionary<Control,MapMenuCommand> _entryHintCommands = [];

    private void ConfigureKnownHints(GameResourceRepository repository)
    {
        foreach (int id in HintMessageIds) _fixedHintTexts.Add(id,id == 0x5991
            ? MainCommandHelpText.ReadRankRequirement(repository.LoadMessages(_locale, San14.Data.Formats.MessageKind.Game))
            : _entryMessages.Format(id));
        foreach (var (command,id) in FixedEntryHints) _entryHintTexts.Add(command,_fixedHintTexts[id]);
    }

    private void ProcessCommandHints()
    {
        if (_entryHint is null || _entryHintCanvas is null || !SourceControlReady || !Expanded)
        { HideEntryHint(); return; }
        var hovered=GetViewport().GuiGetHoveredControl();
        if (hovered is not null && _categoryHintButtons.TryGetValue(hovered,out var category))
        {
            _hoveredEntry=null;
            int? message=category is MapCommandCategory.Diplomacy or MapCommandCategory.Information
                ? _categoryEnabled.TryGetValue(category,out bool enabled)
                    ? enabled ? CategoryHintMessages[(int)category] : 0x11171 : null
                : CategoryHintMessages[(int)category];
            _entryHint.DisplayText(message is int id?_fixedHintTexts[id]:_entryUnknownText);
            _entryHint.SetMeta("source_game_message",message??-1);
        }
        else if (hovered is not null && OpenCategory is not null &&
            _entryHintCommands.TryGetValue(hovered,out var command) && _entryHintTexts.ContainsKey(command))
        {
            _hoveredEntry=command; ShowEntryHint(command);
            int? message;
            if (_entryFacts.TryGetValue(command, out var fact)) message = fact.GameMessageId;
            else
            {
                message = null;
                foreach (var hint in FixedEntryHints)
                    if (hint.Command == command) { message = hint.Message; break; }
            }
            //Several original denials explicitly write Game0; it is a real
            //message identity, distinct from unavailable or unwritten help.
            _entryHint.SetMeta("source_game_message", message ?? -1);
        }
        else { HideEntryHint(); return; }
        _entryHint.UpdatePointer(_entryHintCanvas.GetLocalMousePosition());
    }
}

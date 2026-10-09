namespace San14.Data;

/// <summary>World facts and independent event histories used by condition evaluation.</summary>
public sealed class EventConditionState
{
    private readonly EventFlagHistory _eventFlags = new();
    private readonly EventFlagHistory _resultFlags = new();

    public short? CurrentYear { get; set; }
    public int? ScenarioMode { get; set; }
    public int? PastGameTime { get; set; }
    public ushort? EmperorPersonId { get; set; }

    public bool? ReadEventFlag(int eventId) => _eventFlags.Read(eventId);
    public bool? ReadResultFlag(int eventId) => _resultFlags.Read(eventId);
    public void SetEventFlag(int eventId, bool? value) => _eventFlags.Set(eventId, value);
    public void SetResultFlag(int eventId, bool? value) => _resultFlags.Set(eventId, value);

    public void ResetFlags()
    {
        _eventFlags.Clear();
        _resultFlags.Clear();
    }
}

/// <summary>An unset history is unknown; an explicit reset establishes a cleared history.</summary>
internal sealed class EventFlagHistory
{
    private readonly Dictionary<int, bool?> _flags = [];
    private bool? _default;

    public bool? Read(int eventId) => _flags.TryGetValue(eventId, out bool? value) ? value : _default;
    public void Set(int eventId, bool? value) => _flags[eventId] = value;
    public void Clear()
    {
        _flags.Clear();
        _default = false;
    }
}

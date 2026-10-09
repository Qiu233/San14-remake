using San14.Core;
using San14.Data;

public sealed record CampaignEventDialogue(CampaignEventPersonMessage Source,
    string SpeakerName, string Text, string MessageResourceUri, string MessageResourceSha256,
    int MessageSourceOffset, string MessageRecordSha256);

public enum CampaignEventPresentationStatus { Staged, AwaitingAcknowledgement, Complete }

/// <summary>
/// Prepared, typed presentation for one committed original event. Advance pauses
/// at each source message. Back browses already shown messages and never repeats
/// source commands, audio calls, Core effects or the event eligibility scan.
/// This object owns no engine nodes and can be hidden/reopened without a restart.
/// </summary>
public sealed class CampaignEventPresentation
{
    private readonly CampaignEventExecutionPlan _plan;
    private readonly Action<CampaignEventCommand> _apply;
    private readonly IReadOnlyDictionary<int, CampaignEventDialogue> _dialogues;
    private readonly List<CampaignEventDialogue> _history = [];
    private int _commandIndex, _historyIndex = -1;
    private bool _committed;

    public int SourceEventId => _plan.Source.EventId;
    public CampaignEventExecutionPlan Plan => _plan;
    public IReadOnlyList<CampaignEventCommand> Commands => _plan.Commands;
    public IReadOnlyList<CampaignEventDialogue> History => _history.AsReadOnly();
    public CampaignEventPresentationStatus Status { get; private set; }
        = CampaignEventPresentationStatus.Staged;
    public CampaignEventDialogue? Current => _historyIndex < 0 ? null : _history[_historyIndex];
    public bool CanBack => _historyIndex > 0;
    public bool IsReviewingHistory => _historyIndex >= 0 && _historyIndex < _history.Count - 1;

    public CampaignEventPresentation(CampaignEventExecutionPlan plan,
        Func<CampaignEventPersonMessage, CampaignEventDialogue?> resolveDialogue,
        Action<CampaignEventCommand> applyCommand)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        ArgumentNullException.ThrowIfNull(resolveDialogue);
        _apply = applyCommand ?? throw new ArgumentNullException(nameof(applyCommand));
        var dialogues = new Dictionary<int, CampaignEventDialogue>();
        // Prepare every message before publication. A later unknown message must
        // not discover itself after an earlier source presentation/audio command.
        foreach (CampaignEventPersonMessage message in plan.Commands.OfType<CampaignEventPersonMessage>())
        {
            CampaignEventDialogue dialogue = resolveDialogue(message)
                ?? throw new InvalidDataException($"Original event message {message.MessageId} has no complete text binding.");
            if (dialogue.Source != message || string.IsNullOrWhiteSpace(dialogue.SpeakerName) ||
                dialogue.Text is null || dialogue.MessageSourceOffset < 0 ||
                !Uri.TryCreate(dialogue.MessageResourceUri, UriKind.Absolute, out _) ||
                !IsHash(dialogue.MessageResourceSha256) || !IsHash(dialogue.MessageRecordSha256))
                throw new InvalidDataException("Original event dialogue source or rendered text binding is invalid.");
            if (!dialogues.TryAdd(message.OperationIndex, dialogue))
                throw new InvalidDataException("Original event presentation has a repeated source message operation.");
        }
        _dialogues = new System.Collections.ObjectModel.ReadOnlyDictionary<int, CampaignEventDialogue>(dialogues);
    }

    /// <summary>Called only after the matching independent Core transaction succeeds.</summary>
    public void Commit(GameState committedState, string eventKey)
    {
        if (_committed) throw new InvalidOperationException("Original event presentation was already committed.");
        _plan.RequireCommitted(committedState, eventKey);
        _committed = true;
        Advance();
    }

    /// <summary>Continue the live sequence, or move forward through already displayed history.</summary>
    public CampaignEventDialogue? Advance()
    {
        if (!_committed) throw new InvalidOperationException("Original event presentation is still staged.");
        if (IsReviewingHistory)
        {
            _historyIndex++;
            return Current;
        }
        if (Status == CampaignEventPresentationStatus.Complete) return Current;
        while (_commandIndex < _plan.Commands.Count)
        {
            CampaignEventCommand command = _plan.Commands[_commandIndex];
            if (command is CampaignEventPersonMessage message)
            {
                CampaignEventDialogue dialogue = _dialogues[message.OperationIndex];
                _history.Add(dialogue);
                _historyIndex = _history.Count - 1;
                _commandIndex++;
                Status = CampaignEventPresentationStatus.AwaitingAcknowledgement;
                return dialogue;
            }
            _apply(command);
            _commandIndex++;
        }
        Status = CampaignEventPresentationStatus.Complete;
        return Current;
    }

    public bool Back()
    {
        if (!_committed) throw new InvalidOperationException("Original event presentation is still staged.");
        if (!CanBack) return false;
        _historyIndex--;
        return true;
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

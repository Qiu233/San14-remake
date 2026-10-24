using Godot;
using San14.Client.Gameplay;
using San14.Data;
using San14.Data.Formats;

public partial class PlayableForceHud
{
    private readonly UiSourceText[] _experienceAmounts = new UiSourceText[2];
    private ForceExperienceHeaderChange? _experienceTarget;
    private int? _displayedExperience, _displayedExperienceLevel;
    private int _experienceForce = -1, _experienceWithin, _experienceInitialWithin, _experienceRemaining;
    private int _experiencePreviousLevel;
    private bool _experienceDrawn, _experienceFinishing;
    private Func<bool>? _experienceAdmission;
    private Action<int>? _experienceCompleted;
    private string _experienceLevelFormat = "", _experienceGainFormat = "";
    internal bool IsExperienceTransitionActive => _experienceCompleted is not null;

    private void ConfigureExperienceHeader(UiSourceLayout source, IReadOnlyList<LcskNodeRecord> nodes, string locale,
        Func<string, string, int?, UiSourceText> addText)
    {
        var messages = _repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1];
        _experienceLevelFormat = messages.Records[0x0384].Text ?? throw new InvalidDataException("Missing doctrine level format.");
        _experienceGainFormat = messages.Records[0x038C].Text ?? throw new InvalidDataException("Missing experience gain format.");
        var action = UiSourceGaugeCurve.ReadAction(source.Source.Layout, "syugi");
        SetMeta("experience_source_action", "syugi");
        SetMeta("experience_source_action_start", action.Start);
        SetMeta("experience_source_action_end", action.End);
        for (int i = 0; i < _experienceAmounts.Length; i++)
        {
            _experienceAmounts[i] = addText("ExperienceGain" + i, nodes[23 + i].Name, null);
            _experienceAmounts[i].Hide();
        }
        _doctrineLevel.Draw += () => { if (IsExperienceTransitionActive) _experienceDrawn = true; };
    }

    private void RefreshExperienceHeader(ForceHudView view)
    {
        if (_experienceForce != view.ForceId)
        { AbandonExperienceTransition(); _experienceForce = view.ForceId; _displayedExperience = null; _displayedExperienceLevel = null; }
        _experienceTarget = view.PendingExperience;
        if (IsExperienceTransitionActive) return;
        if (_experienceTarget is { } pending)
        {
            if (_displayedExperience is null)
            {
                //A restored outbox supplies the still-unshown accumulation.
                //Its source old level must agree; unknown is not level zero.
                int start = unchecked(pending.CurrentExperience - pending.Amount);
                int level = pending.LevelAt(start);
                if (pending.SourcePreviousLevel != level) return;
                _displayedExperience = start; _displayedExperienceLevel = level;
            }
            ApplyExperienceDisplay(pending, _displayedExperience.Value);
            return;
        }
        _displayedExperience = view.ExperienceTotal;
        _displayedExperienceLevel = view.DoctrineProgress?.Level;
        _doctrineLevel.Text = view.DoctrineLevelText;
        _doctrineGauge.Apply(view.DoctrineProgress?.Percentage);
    }

    internal bool BeginExperienceTransition(Func<bool> admitted, Action<int> completed, Action playSound)
    {
        if (IsExperienceTransitionActive) return true;
        if (_experienceTarget is not { } target || _displayedExperience is not int total ||
            _displayedExperienceLevel is not int previous || total > target.CurrentExperience ||
            previous > target.LevelAt(target.CurrentExperience)) return false;
        _experiencePreviousLevel = previous;
        _experienceWithin = previous == 0 ? 0 : unchecked(total - target.Thresholds[previous]);
        _experienceInitialWithin = _experienceWithin; _experienceRemaining = target.Amount;
        _experienceAdmission = admitted; _experienceCompleted = completed;
        _experienceFinishing = false; _experienceDrawn = false;
        foreach (var text in _experienceAmounts)
        { text.Text = _experienceGainFormat.Replace("%d", target.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)); text.Show(); }
        _doctrineLevel.QueueRedraw();
        playSound();
        return true;
    }

    public override void _Process(double delta)
    {
        if (!IsExperienceTransitionActive || !IsVisibleInTree() || !(_experienceAdmission?.Invoke() ?? false) ||
            _experienceTarget is not { } target || !_experienceDrawn) return;
        if (_experienceFinishing)
        {
            var done = _experienceCompleted!; int previous = _experiencePreviousLevel;
            AbandonExperienceTransition(); done(previous); return;
        }
        int level = _displayedExperienceLevel!.Value, finalLevel = target.LevelAt(target.CurrentExperience);
        if (level < finalLevel)
        {
            int span = unchecked(target.Thresholds[level + 1] - target.Thresholds[level]);
            if (span <= 0) return;
            if (_experienceWithin < span)
                _experienceWithin = Math.Min(span, unchecked(_experienceWithin + Math.Max(10, (span - _experienceInitialWithin) / 30)));
            else
            {
                _experienceRemaining = unchecked(_experienceRemaining + _experienceInitialWithin - span);
                _displayedExperienceLevel = ++level; _experienceWithin = _experienceInitialWithin = 0;
            }
        }
        else
        {
            int finalWithin = unchecked(target.CurrentExperience - target.Thresholds[level]);
            if (_experienceWithin < finalWithin)
                _experienceWithin = Math.Min(finalWithin, unchecked(_experienceWithin + Math.Max(10, _experienceRemaining / 30)));
            else _experienceFinishing = true;
        }
        _displayedExperience = level == 0 ? 0 : unchecked(target.Thresholds[level] + _experienceWithin);
        ApplyExperienceDisplay(target, _displayedExperience.Value);
        _experienceDrawn = false;
        _doctrineLevel.QueueRedraw();
    }

    private void ApplyExperienceDisplay(ForceExperienceHeaderChange target, int total)
    {
        int level = _displayedExperienceLevel ?? target.LevelAt(total);
        _doctrineLevel.Text = _experienceLevelFormat.Replace("%d", level.ToString(System.Globalization.CultureInfo.InvariantCulture));
        int? percentage = null;
        if (level == 10) percentage = 100;
        else if (level > 0 && unchecked(target.Thresholds[level + 1] - target.Thresholds[level]) is int span && span != 0)
            percentage = unchecked((total - target.Thresholds[level]) * 100) / span;
        _doctrineGauge.Apply(percentage is int value ? Math.Clamp(value, 0, 100) : null);
        SetMeta("experience_display_level", level);
        SetMeta("experience_display_total", total);
    }

    internal void AbandonExperienceTransition()
    {
        _experienceCompleted = null; _experienceAdmission = null; _experienceFinishing = false;
        foreach (var text in _experienceAmounts) text?.Hide();
    }
}

using System.Collections.Immutable;
using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private EventEditorProgramView? _editedEventProgramPage;
    private EventEditorChoiceView? _editedEventProgramChoice;
    private EventEditorParameterView? _editedEventProgramParameters;
    private DeploymentCalculatorDialog? _editedEventProgramCalculator;
    private EditedEventDraft? _editedEventProgramOwner;
    private EditedEventAuthoringCatalog? _editedEventProgramCatalog;
    private ImmutableArray<EditedEventInstruction> _editedEventProgramOriginal;
    private ImmutableArray<EditedEventInstruction> _editedEventProgramWorking;
    private IReadOnlyList<EventEditorChoiceRow> _editedEventProgramRows = [];
    private bool _editedEventProgramResults;
    private int? _editedEventProgramSelection;
    private int _editedEventProgramCapacity;
    private EditedEventConditionActions? _editedEventConditionActions;
    private EditedEventAuthoringChoice? _editedEventParameterChoice;
    private EditedEventInstruction? _editedEventParameterOriginal;
    private EditedEventInstruction? _editedEventParameterWorking;
    private Action<EditedEventInstruction>? _editedEventParameterAccepted;

    private bool HasEditedEventProgramPage => _editedEventProgramPage is not null;
    private bool IsEditedEventProgramCurrent => IsEventEditorsCurrent &&
        _editedEventProgramPage is not null && ReferenceEquals(_editedEventDraft, _editedEventProgramOwner);
    private bool CanEditEditedEventProgram => IsEditedEventProgramCurrent &&
        _editedEventQuestion is null && _editedEventNotice is null &&
        _editedEventProgramChoice is null && _editedEventProgramParameters is null;
    private bool CanEditEditedEventParameters => IsEditedEventProgramCurrent &&
        _editedEventProgramParameters is not null && _editedEventProgramChoice is null &&
        _editedEventProgramCalculator is null && _editedEventQuestion is null && _editedEventNotice is null;

    private void OpenEditedEventProgram(bool results, int? selectedIndex = null)
    {
        if (!IsEventEditorsCurrent || _repository is null || _editedEventDraft is not { } draft ||
            HasEditedEventProgramPage) return;
        _editedEventProgramOwner = draft;
        _editedEventProgramResults = results;
        _editedEventProgramCatalog = ReadEditedEventAuthoringCatalog();
        _editedEventProgramOriginal = (results ? draft.Results : draft.ReadEditableConditions()).Instructions;
        _editedEventProgramWorking = _editedEventProgramOriginal;
        _editedEventProgramCapacity = results ? EditedEventLibrary.ResultCapacity : EditedEventLibrary.ConditionCapacity -
            draft.Conditions.Instructions.Count(instruction => instruction.Code == 64);
        _editedEventProgramSelection = selectedIndex is int index && index >= 0 && index < _editedEventProgramWorking.Length
            ? index : null;
        PublishEditedEventProgramRows();
        var view = new EventEditorProgramView(); _editedEventProgramPage = view; AddChild(view);
        try
        {
            view.Configure(_repository, _ui, results, ReadEditedEventProgram,
                () => CanEditEditedEventProgram && _editedEventProgramPage == view,
                () => RunUiAction(RequestEditedEventProgramBack),
                () => RunUiAction(AcceptEditedEventProgram),
                index => RunUiAction(() =>
                {
                    if (!CanEditEditedEventProgram || index < 0 || index >= _editedEventProgramWorking.Length) return;
                    _editedEventProgramSelection = index; PublishEditedEventProgramActions(); view.Refresh();
                }), action => RunUiAction(() => ApplyEditedEventProgramAction(action)), CurrentListScrollSpeed);
        }
        catch { CloseEditedEventProgram(); ResumeEditedEventDraft(); throw; }
    }

    private IReadOnlyList<EditedEventAuthoringChoice> EditedEventProgramChoices =>
        _editedEventProgramResults ? _editedEventProgramCatalog!.Results : _editedEventProgramCatalog!.Conditions;

    private EventEditorProgramValues ReadEditedEventProgram()
    {
        int count = _editedEventProgramWorking.Length;
        int? selected = _editedEventProgramSelection;
        var actions = new HashSet<EventEditorProgramAction>();
        if (!_editedEventProgramResults)
        {
            var available = _editedEventConditionActions!;
            if (available.CanInsert) actions.Add(EventEditorProgramAction.Insert);
            if (available.CanMoveUp) actions.Add(EventEditorProgramAction.MoveUp);
            if (available.CanMoveDown) actions.Add(EventEditorProgramAction.MoveDown);
            if (available.CanDelete) actions.Add(EventEditorProgramAction.Delete);
            if (available.CanCopy) actions.Add(EventEditorProgramAction.Copy);
            if (available.CanEdit && selected is int condition &&
                EditedEventProgramChoices.Any(choice => choice.Code == _editedEventProgramWorking[condition].Code &&
                    choice.Parameters.Count > 0)) actions.Add(EventEditorProgramAction.Edit);
            return new(_editedEventProgramRows, selected, actions, count <= _editedEventProgramCapacity, "");
        }
        if (count < _editedEventProgramCapacity) actions.Add(EventEditorProgramAction.Insert);
        if (selected is int index && index >= 0 && index < count)
        {
            if (index > 0) actions.Add(EventEditorProgramAction.MoveUp);
            if (index + 1 < count) actions.Add(EventEditorProgramAction.MoveDown);
            if (EditedEventProgramChoices.Any(choice => choice.Code == _editedEventProgramWorking[index].Code &&
                choice.Parameters.Count > 0))
                actions.Add(EventEditorProgramAction.Edit);
            actions.Add(EventEditorProgramAction.Delete);
            if (count < _editedEventProgramCapacity) actions.Add(EventEditorProgramAction.Copy);
        }
        return new(_editedEventProgramRows, selected, actions, count <= _editedEventProgramCapacity, "");
    }

    private void PublishEditedEventProgramRows()
    {
        _editedEventProgramRows = Array.AsReadOnly(_editedEventProgramWorking.Select((instruction, index) =>
            new EventEditorChoiceRow(index, _editedEventProgramCatalog!.DescribeInstruction(instruction))).ToArray());
        PublishEditedEventProgramActions();
    }

    private void PublishEditedEventProgramActions() => _editedEventConditionActions = _editedEventProgramResults
        ? null : EditedEventConditionEditing.ReadActions(new(_editedEventProgramWorking),
            _editedEventProgramSelection, _editedEventProgramCapacity);

    private void AdoptEditedEventConditionEdit(EditedEventConditionEditResult result)
    {
        _editedEventProgramWorking = result.Program.Instructions;
        _editedEventProgramSelection = result.SelectedIndex;
    }

    private void ApplyEditedEventProgramAction(EventEditorProgramAction action)
    {
        if (!CanEditEditedEventProgram || !ReadEditedEventProgram().EnabledActions.Contains(action)) return;
        int index = _editedEventProgramSelection ?? _editedEventProgramWorking.Length;
        if (!_editedEventProgramResults && action is not (EventEditorProgramAction.Insert or EventEditorProgramAction.Edit))
        {
            var program = new EditedEventProgram(_editedEventProgramWorking);
            var change = action switch
            {
                EventEditorProgramAction.MoveUp => EditedEventConditionEditing.Move(program,index,-1,_editedEventProgramCapacity),
                EventEditorProgramAction.MoveDown => EditedEventConditionEditing.Move(program,index,1,_editedEventProgramCapacity),
                EventEditorProgramAction.Delete => EditedEventConditionEditing.Delete(program,index),
                EventEditorProgramAction.Copy => EditedEventConditionEditing.Copy(program,index,_editedEventProgramCapacity),
                _ => throw new InvalidOperationException("Unknown condition edit action.")
            };
            AdoptEditedEventConditionEdit(change); RefreshEditedEventProgram(); return;
        }
        switch (action)
        {
            case EventEditorProgramAction.MoveUp:
            case EventEditorProgramAction.MoveDown:
                int destination = index + (action == EventEditorProgramAction.MoveUp ? -1 : 1);
                var moving = _editedEventProgramWorking[index];
                _editedEventProgramWorking = _editedEventProgramWorking.SetItem(index, _editedEventProgramWorking[destination])
                    .SetItem(destination, moving);
                _editedEventProgramSelection = destination; break;
            case EventEditorProgramAction.Delete:
                _editedEventProgramWorking = _editedEventProgramWorking.RemoveAt(index);
                _editedEventProgramSelection = _editedEventProgramWorking.IsEmpty ? null :
                    Math.Min(index, _editedEventProgramWorking.Length - 1); break;
            case EventEditorProgramAction.Copy:
                _editedEventProgramWorking = _editedEventProgramWorking.Insert(index + 1, _editedEventProgramWorking[index]);
                _editedEventProgramSelection = index + 1; break;
            case EventEditorProgramAction.Insert:
                ChooseEditedEventInstruction(null, instruction =>
                {
                    // The source's insert mode writes at the selected index;
                    // its separate no-row mode appends at the end.
                    if (_editedEventProgramResults)
                    {
                        _editedEventProgramWorking = _editedEventProgramWorking.Insert(index, instruction);
                        _editedEventProgramSelection = index;
                    }
                    else AdoptEditedEventConditionEdit(EditedEventConditionEditing.Insert(
                        new(_editedEventProgramWorking),index,instruction,_editedEventProgramCapacity));
                    RefreshEditedEventProgram();
                }); return;
            case EventEditorProgramAction.Edit:
                var original = _editedEventProgramWorking[index];
                var choice = EditedEventProgramChoices.First(item => item.Code == original.Code);
                if (choice.Parameters.Count == 0) return;
                OpenEditedEventParameters(choice, original, instruction =>
                {
                    _editedEventProgramWorking = _editedEventProgramWorking.SetItem(index, instruction);
                    RefreshEditedEventProgram();
                }); return;
        }
        RefreshEditedEventProgram();
    }

    private void RefreshEditedEventProgram()
    {
        PublishEditedEventProgramRows();
        _editedEventProgramPage?.SetBusy(false);
    }

    private void AcceptEditedEventProgram()
    {
        if (!CanEditEditedEventProgram || !ReadEditedEventProgram().CanConfirm || _editedEventProgramOwner is not { } owner) return;
        if (!EditedEventProgramsEqual(_editedEventProgramWorking, _editedEventProgramOriginal))
        {
            var program = new EditedEventProgram(_editedEventProgramWorking);
            if (_editedEventProgramResults) owner.SetResults(program); else owner.SetEditableConditions(program);
        }
        CloseEditedEventProgram(); ResumeEditedEventDraft();
    }

    private void ChooseEditedEventInstruction(EditedEventInstruction? original, Action<EditedEventInstruction> accepted)
    {
        var choices = EditedEventProgramChoices;
        bool Available(EditedEventAuthoringChoice choice) => choice.UnresolvedOwner is null &&
            (_editedEventProgramResults || choice.Code is not (5 or 7) ||
                _editedEventProgramWorking.Length + 2 <= _editedEventProgramCapacity);
        var rows = Array.AsReadOnly(choices.Select(choice => new EventEditorChoiceRow(choice.Code,
            choice.Caption, Available(choice), choice.UnresolvedOwner is null ? "" : EditedEventDataCaption(0x353)))
            .ToArray());
        OpenEditedEventProgramChoice(EditedEventDataCaption(_editedEventProgramResults ? 0x4A2 : 0x2F7), rows,
            original?.Code, code =>
            {
                if (code is not int selected) return;
                var choice = choices.First(item => item.Code == selected);
                // Changing the operation is the only path which resets operands
                // and controls. Same-kind edits keep every unexposed field.
                var instruction = original?.Code == choice.Code ? original! : choice.DefaultInstruction;
                if (choice.Parameters.Count == 0) { accepted(instruction); return; }
                OpenEditedEventParameters(choice, instruction, accepted);
            });
    }

    private void OpenEditedEventProgramChoice(string heading, IReadOnlyList<EventEditorChoiceRow> rows,
        int? selected, Action<int?> completed)
    {
        if (!IsEditedEventProgramCurrent || _repository is null || _editedEventProgramChoice is not null) return;
        _editedEventProgramPage!.SetBusy(true); _editedEventProgramParameters?.SetBusy(true);
        var view = new EventEditorChoiceView(); _editedEventProgramChoice = view; AddChild(view);
        try
        {
            view.Configure(_repository, _ui, heading, rows, selected,
                () => IsEditedEventProgramCurrent && _editedEventProgramChoice == view &&
                    _editedEventQuestion is null && _editedEventNotice is null,
                value => RunUiAction(() =>
                {
                    if (!IsEditedEventProgramCurrent || _editedEventProgramChoice != view) return;
                    _editedEventProgramChoice = null; view.Abandon(); view.QueueFree();
                    _editedEventProgramPage?.SetBusy(_editedEventProgramParameters is not null);
                    _editedEventProgramParameters?.SetBusy(false);
                    completed(value);
                }), CurrentListScrollSpeed);
            if (_editedEventProgramParameters is not null) view.ZIndex = 11;
        }
        catch
        {
            _editedEventProgramChoice = null; view.Abandon(); view.QueueFree();
            _editedEventProgramPage?.SetBusy(_editedEventProgramParameters is not null);
            _editedEventProgramParameters?.SetBusy(false); throw;
        }
    }

    private void OpenEditedEventParameters(EditedEventAuthoringChoice choice, EditedEventInstruction instruction,
        Action<EditedEventInstruction> accepted)
    {
        if (!IsEditedEventProgramCurrent || _repository is null || _editedEventProgramParameters is not null) return;
        _editedEventParameterChoice = choice; _editedEventParameterWorking = instruction;
        _editedEventParameterOriginal = instruction;
        _editedEventParameterAccepted = accepted;
        _editedEventProgramPage!.SetBusy(true);
        var view = new EventEditorParameterView(); _editedEventProgramParameters = view; AddChild(view);
        try
        {
            view.Configure(_repository, _ui, choice.Caption, choice.Parameters.Count, ReadEditedEventParameters,
                () => CanEditEditedEventParameters && _editedEventProgramParameters == view,
                () => RunUiAction(CancelEditedEventParameters),
                () => RunUiAction(AcceptEditedEventParameters),
                argument => RunUiAction(() => ChooseEditedEventParameter(argument)),
                (argument, direction) => RunUiAction(() => StepEditedEventParameter(argument, direction)));
        }
        catch { CloseEditedEventParameters(); _editedEventProgramPage?.SetBusy(false); throw; }
    }

    private static bool HasEditedEventParameterInput(EditedEventParameterSpec parameter) =>
        parameter.UnresolvedOwner is null && (parameter.Values.Count > 0 ||
            parameter.Kind == EditedEventParameterKind.Integer && parameter.Minimum is >= 0 &&
            parameter.Maximum is int maximum && maximum >= parameter.Minimum.Value);

    private static bool EditedEventParameterContains(EditedEventParameterSpec parameter, int value) =>
        HasEditedEventParameterInput(parameter) && (parameter.Values.Count > 0
            ? parameter.Values.Any(candidate => candidate.Value == value)
            : value >= parameter.Minimum!.Value && value <= parameter.Maximum!.Value);

    private EventEditorParameterValues ReadEditedEventParameters()
    {
        var choice = _editedEventParameterChoice!; var instruction = _editedEventParameterWorking!;
        var rows = choice.Parameters.Select(parameter =>
        {
            int value = instruction.Arguments[parameter.ArgumentIndex];
            bool enabled = HasEditedEventParameterInput(parameter);
            int position = parameter.Values.ToList().FindIndex(candidate => candidate.Value == value);
            bool previous = enabled && (parameter.Values.Count > 0 ? position > 0 :
                value > parameter.Minimum && value <= parameter.Maximum);
            bool next = enabled && (parameter.Values.Count > 0 ? position >= 0 && position + 1 < parameter.Values.Count :
                value >= parameter.Minimum && value < parameter.Maximum);
            return new EventEditorParameterRow(parameter.ArgumentIndex, parameter.Caption,
                _editedEventProgramCatalog!.DescribeParameterValue(parameter, value), enabled, previous, next,
                enabled ? "" : EditedEventDataCaption(0x353));
        }).ToArray();
        return new(Array.AsReadOnly(rows), choice.Parameters.All(parameter =>
            HasEditedEventParameterInput(parameter)
                ? EditedEventParameterContains(parameter, instruction.Arguments[parameter.ArgumentIndex])
                : instruction.Arguments[parameter.ArgumentIndex] ==
                    _editedEventParameterOriginal!.Arguments[parameter.ArgumentIndex]),
            choice.UnresolvedOwner is null ? "" : EditedEventDataCaption(0x353));
    }

    private void ChooseEditedEventParameter(int argument)
    {
        if (!CanEditEditedEventParameters || _repository is null || _editedEventParameterChoice is not { } choice ||
            _editedEventParameterWorking is not { } instruction) return;
        var parameter = choice.Parameters.FirstOrDefault(item => item.ArgumentIndex == argument);
        if (parameter is null || !HasEditedEventParameterInput(parameter)) return;
        int value = instruction.Arguments[argument];
        if (parameter.Values.Count > 0)
        {
            var rows = Array.AsReadOnly(parameter.Values.Select(item => new EventEditorChoiceRow(item.Value, item.Caption)).ToArray());
            OpenEditedEventProgramChoice(parameter.Caption, rows, value, selected =>
            { if (selected is int next) SetEditedEventParameter(argument, next); });
            return;
        }
        _editedEventProgramParameters!.SetBusy(true);
        var view = new DeploymentCalculatorDialog(); _editedEventProgramCalculator = view; AddChild(view);
        try
        {
            view.Configure(_repository, _ui, value, parameter.Minimum!.Value, parameter.Maximum!.Value,
                result => RunUiAction(() =>
                {
                    if (!IsEditedEventProgramCurrent || _editedEventProgramCalculator != view) return;
                    _editedEventProgramCalculator = null; view.Abandon(); view.QueueFree();
                    _editedEventProgramParameters?.SetBusy(false);
                    if (result is int next) SetEditedEventParameter(argument, next);
                }));
            view.Name = "EventEditorParameterCalculator"; view.ZIndex = 11;
        }
        catch
        {
            _editedEventProgramCalculator = null; view.Abandon(); view.QueueFree();
            _editedEventProgramParameters?.SetBusy(false); throw;
        }
    }

    private void StepEditedEventParameter(int argument, int direction)
    {
        if (!CanEditEditedEventParameters || direction is not (-1 or 1) ||
            _editedEventParameterWorking is not { } instruction || _editedEventParameterChoice is not { } choice) return;
        var parameter = choice.Parameters.FirstOrDefault(item => item.ArgumentIndex == argument);
        if (parameter is null || !HasEditedEventParameterInput(parameter)) return;
        int value = instruction.Arguments[argument];
        if (parameter.Values.Count > 0)
        {
            int index = parameter.Values.ToList().FindIndex(item => item.Value == value);
            if (index >= 0 && index + direction >= 0 && index + direction < parameter.Values.Count)
                SetEditedEventParameter(argument, parameter.Values[index + direction].Value);
        }
        else if (value >= parameter.Minimum && value <= parameter.Maximum)
            SetEditedEventParameter(argument, Math.Clamp(value + direction, parameter.Minimum!.Value, parameter.Maximum!.Value));
    }

    private void SetEditedEventParameter(int argument, int value)
    {
        if (!IsEditedEventProgramCurrent || _editedEventParameterWorking is not { } instruction ||
            _editedEventParameterChoice is not { } choice) return;
        var parameter = choice.Parameters.First(item => item.ArgumentIndex == argument);
        if (!EditedEventParameterContains(parameter, value)) return;
        _editedEventParameterWorking = instruction with { Arguments = instruction.Arguments.SetItem(argument, value) };
        _editedEventProgramParameters?.Refresh();
    }

    private void AcceptEditedEventParameters()
    {
        if (!CanEditEditedEventParameters || !ReadEditedEventParameters().CanConfirm) return;
        var instruction = _editedEventParameterWorking!; var accepted = _editedEventParameterAccepted!;
        CloseEditedEventParameters(); _editedEventProgramPage?.SetBusy(false); accepted(instruction);
    }

    private void CancelEditedEventParameters()
    {
        if (!CanEditEditedEventParameters) return;
        CloseEditedEventParameters(); _editedEventProgramPage?.SetBusy(false);
    }

    private void CloseEditedEventParameters()
    {
        var calculator = _editedEventProgramCalculator; _editedEventProgramCalculator = null;
        var parameters = _editedEventProgramParameters; _editedEventProgramParameters = null;
        _editedEventParameterChoice = null; _editedEventParameterWorking = null;
        _editedEventParameterOriginal = null; _editedEventParameterAccepted = null;
        if (calculator is not null) { calculator.Abandon(); calculator.QueueFree(); }
        if (parameters is not null) { parameters.Abandon(); parameters.QueueFree(); }
    }

    private void RequestEditedEventProgramBack()
    {
        if (_editedEventProgramCalculator is { } calculator) { calculator.RequestCancel(); return; }
        if (_editedEventProgramChoice is { } choice) { choice.RequestBack(); return; }
        if (_editedEventProgramParameters is { } parameters) { parameters.RequestBack(); return; }
        if (!CanEditEditedEventProgram) return;
        if (EditedEventProgramsEqual(_editedEventProgramWorking, _editedEventProgramOriginal))
        { CloseEditedEventProgram(); ResumeEditedEventDraft(); return; }
        _editedEventProgramPage!.SetBusy(true);
        AskEditedEventQuestion(0x2888,
            () => { CloseEditedEventProgram(); ResumeEditedEventDraft(); },
            () => _editedEventProgramPage?.SetBusy(false));
    }

    private static bool EditedEventProgramsEqual(ImmutableArray<EditedEventInstruction> first,
        ImmutableArray<EditedEventInstruction> second)
    {
        if (first == second) return true;
        if (first.Length != second.Length) return false;
        for (int index = 0; index < first.Length; index++)
        {
            var a = first[index]; var b = second[index];
            if (a.Code != b.Code || a.SettingCondition != b.SettingCondition || a.Suppression != b.Suppression ||
                !a.Arguments.SequenceEqual(b.Arguments)) return false;
        }
        return true;
    }

    private void CloseEditedEventProgram()
    {
        // Invalidate the captured parent before retiring any modal callbacks.
        _editedEventProgramOwner = null;
        var choice = _editedEventProgramChoice; _editedEventProgramChoice = null;
        if (choice is not null) { choice.Abandon(); choice.QueueFree(); }
        CloseEditedEventParameters();
        var page = _editedEventProgramPage; _editedEventProgramPage = null;
        _editedEventProgramCatalog = null; _editedEventProgramOriginal = _editedEventProgramWorking = default;
        _editedEventProgramRows = []; _editedEventProgramSelection = null; _editedEventConditionActions = null;
        if (page is not null) { page.Abandon(); page.QueueFree(); }
    }
}

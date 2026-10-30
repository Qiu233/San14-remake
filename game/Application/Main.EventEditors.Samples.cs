using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private void OpenEditedEventSamples()
    {
        if (!CanUseEditedEventMenu || _repository is null) return;
        var source = GetEditedEventStore().Current;
        var samples = _repository.LoadEditedEventSamples(_ui.SourceLocale);
        int maximum = Math.Min(samples.Catalog.Rows.Count,EditedEventLibrary.EventCapacity-source.Events.Length);
        if (maximum <= 0) return;
        var rows = samples.Catalog.Rows.Select(row => new CommandSelectionRow(row.Slot,
            [new(row.Name,SortUnavailable:true),new(row.Description,SortUnavailable:true)],true)).ToArray();
        var view = new CommandSelectionView(); _editedEventSelector = view; AddChild(view);
        _editedEventMenu!.Hide();
        bool Current() => IsEventEditorsCurrent && _editedEventSelector == view &&
            _editedEventQuestion is null && _editedEventNotice is null;
        void Retire()
        {
            _editedEventSelector = null; view.Abandon(); view.QueueFree(); ResumeEditedEventMenu();
        }
        try
        {
            view.Configure(_repository,_ui,0x8F,
                [new("name",EditedEventDataCaption(0x2EC),500),new("description",EditedEventDataCaption(0x400),1050)],
                rows,[],maximum,Current,result => RunUiAction(() =>
                {
                    if (!Current()) return;
                    if (result is not { Count: > 0 }) { Retire(); return; }
                    if (!ReferenceEquals(GetEditedEventStore().Current,source)) { Retire(); return; }
                    EditedEventSampleImport imported;
                    try { imported = samples.Catalog.Import(source,result); }
                    catch (InvalidDataException error) when (error.Message == "event_editor.samples.capacity")
                    {
                        GD.PushWarning(error.ToString());
                        ShowEditedEventNotice(0x2896,() =>
                        { if (IsEventEditorsCurrent && _editedEventSelector == view) view.Show(); });
                        return;
                    }
                    // The original mode3 copies after selection; it has no
                    // extra Yes/No prompt. Only the atomic store publishes.
                    if (SaveEditedEventLibrary(imported.Library)) Retire();
                    else view.Show();
                }),CurrentListScrollSpeed,
                guide:RegisteredOfficerExchangeMessages.Format(_repository,_ui.SourceLocale,0x289D),
                retainOnAccept:true,requireSelection:true);
            view.Name = "EditedEventSampleSelector";
            view.SetMeta("source_list_mode",3);
        }
        catch
        { _editedEventSelector=null;view.Abandon();view.QueueFree();ResumeEditedEventMenu();throw; }
    }
}

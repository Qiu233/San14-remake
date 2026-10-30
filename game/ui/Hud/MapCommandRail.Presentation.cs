using Godot;
using San14.Data;

internal partial class MapCommandRail
{
    private readonly Dictionary<MapCommandCategory,bool> _categoryEnabled = [];
    internal void RefreshPresentation(MapMainCommandPresentation presentation)
    {
        if (_retired) return;
        SetMeta("source_presentation_update", presentation.Update.ToString());
        SetMeta("source_presentation_issue", string.Join(";", presentation.UnresolvedOwners));
        //The original guard preserves its published cache. It does not reset
        //attention or first-use state when a world update is unavailable.
        foreach (var fact in presentation.Categories)
            if (fact.Enabled is bool enabled) _categoryEnabled[fact.Category] = enabled;
        if (presentation.Update == MapHeaderUpdate.Updated) _presentation = presentation;
        PublishPresentation();
    }

    private void PublishPresentation()
    {
        PublishEntryPresentation();
        foreach (var fact in _presentation?.Categories ?? [])
        {
            if (!_buttons.TryGetValue(fact.Category, out var button)) continue;
            if (fact.Enabled is bool enabled) button.Disabled = !enabled;
            if (fact.Attention is bool attention) _attention[fact.Category].Visible = attention;
            if (fact.Badge is bool badge) _badges[fact.Category].Visible = badge;
            button.SetMeta("source_category_enabled", fact.Enabled?.ToString() ?? "Unresolved");
            button.SetMeta("source_attention", fact.Attention?.ToString() ?? "Unresolved");
            button.SetMeta("source_badge", fact.Badge?.ToString() ?? "Unresolved");
            button.SetMeta("source_presentation_issue", string.Join(";", fact.UnresolvedOwners));
        }
        foreach (var (category,enabled) in _categoryEnabled)
            if (_buttons.TryGetValue(category,out var button))
            {
                button.Disabled = !enabled;
                button.SetMeta("source_category_enabled",enabled.ToString());
            }
        if (OpenCategory is { } openCategory && _buttons[openCategory].Disabled) CloseMenu();
    }

    internal void ApplyAutomaticHide(bool? hidden)
    {
        SetMeta("source_auto_hide", hidden?.ToString() ?? "Unresolved");
        if (_retired || hidden is not bool known || AutomaticallyHidden == known) return;
        AutomaticallyHidden = known;
        SetMeta("source_outer_clip", _layouts.Outer.Stable(known ? "cmd_hide_out" : "cmd_hide_in").Name);
        PublishAutomaticVisibility();
        PresentationChanged?.Invoke();
    }

    private void PublishAutomaticVisibility()
    {
        //Hide source layers, preserving the open submenu, selected command,
        //fold state and current callback generations for a genuine restore.
        _mainRoot.Visible = !AutomaticallyHidden;
        _submenuRoot.Visible = !AutomaticallyHidden && OpenCategory is not null;
        _linkRoot.Visible = !AutomaticallyHidden && OpenCategory is not null;
        if (AutomaticallyHidden)
        {
            foreach (var cursor in _cursors.Values) cursor.Hide();
            HideEntryHint();
        }
    }
}

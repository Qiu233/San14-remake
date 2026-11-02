using Godot;

// A page keeps its authored controls for the lifetime of the containing view.
// Business rows, selection and sorting remain owned by that view.
internal sealed class UiSourceTablePage<TRow>(Control headerHost, Control rowsHost)
{
    internal Control HeaderHost { get; } = headerHost;
    internal Control RowsHost { get; } = rowsHost;
    internal List<TRow> Items { get; } = [];
    internal Dictionary<int, UiSourceColumnButton> Headers { get; } = [];
    internal void SetActive(bool active)
    {
        HeaderHost.Visible = active;
        RowsHost.Visible = active;
    }
}

using Godot;
using San14.Data;

/// <summary>The two ordinary settings consumers share the original row/radio
/// mounts and tab shapes. These are source resource-node identities; their
/// coordinates and dimensions continue to come from the active layouts.</summary>
internal static class ScenarioSettingsSourceLayout
{
    // Offline extraction closes the caller's nine-row and four/five-radio
    // tables. Keep the authored gaps and button widths for shorter groups.
    private static readonly int[] RowMounts = [3, 4, 5, 11, 6, 7, 8, 9, 10];
    private static readonly int[] FourRadioMounts = [4, 7, 10, 13];
    private static readonly int[] FiveRadioMounts = [4, 7, 10, 20, 13];
    private static readonly int[][] HelpMessages =
    [
        [0x2791, 0x2794, 0x2795, 0x2796, 0x2798, 0x279A, 0x2799, 0x2941, 0x279D],
        [0x279E, 0x279F, 0x27A0, 0x27A1, 0x27A2, 0x27A3, 0x27A4],
        [0x27A6, 0x27A7, 0x27A8, 0x27A9, 0x27AA, 0x27AB, 0x27AC],
        [0x27AE, 0x27AF, 0x27B0, 0x27B1, 0x27B2, 0x27B3, 0x27B4],
        [0x27B5, 0x27B6, 0x27B7, 0x27B8, 0x27B9, 0x27BA, 0x27BB, 0x27BC],
    ];
    internal static Rect2 Row(UiSourceLayout layout, int row) => layout.Rectangle(RowMounts[row]);
    internal static int RowMount(int row) => RowMounts[row];
    internal static uint TabLayout(int index) => index switch { 0 => 0x172EDC24, 4 => 0xD45873AA, _ => 0x9AFB27B7 };
    internal static UiSourceLayout RadioRow(GameResourceRepository repository, int count) =>
        new(repository, count == 5 ? 0x7F1F1D88u : 0x28012CA9u);
    internal static Rect2 Radio(UiSourceLayout layout, int index, int count)
    {
        Rect2 box = layout.Rectangle((count == 5 ? FiveRadioMounts : FourRadioMounts)[index]);
        // The common factory truncates the mounted position and size before
        // resizing the joined radio's own source subtree.
        return new(new Vector2((int)box.Position.X, (int)box.Position.Y),
            new Vector2((int)box.Size.X, (int)box.Size.Y));
    }
    internal static int Help(int group, int row, int choice, bool top)
    {
        if (top && group == 0 && row == 3) return 0x2797;
        if (top && group == 2 && row == 0) return 0x2952;
        if (group == 0 && row == 0) return choice switch { 3 => 0x2792, 4 => 0x2939, _ => 0x2791 };
        return HelpMessages[group][row];
    }
}

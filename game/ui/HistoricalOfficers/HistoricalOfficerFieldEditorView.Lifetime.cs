using System.Globalization;
using Godot;
using San14.Data;

internal partial class HistoricalOfficerFieldEditorView
{
    private void BuildLifetime()
    {
        var source = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadWindow(repository, 7));
        var panel = Panel(source);
        foreach (int node in new[] { 2, 4, 5, 9, 11, 16, 18, 39, 40, 41, 44, 45, 48 })
            source.Art(panel, node, source.Rectangle(node), chrome!.Assets, chrome.SpriteShader);
        int birth = draft.Row.BirthYear;
        int lifespan = working.DeathYear - birth + 1;
        int minimum = draft.Row.AppearanceYear - birth + 1;
        source.Text(panel, 6, default, Message(0x4E7), chrome!.Font);
        source.Text(panel, 7, default, birth.ToString(CultureInfo.InvariantCulture), chrome.Font);
        source.Text(panel, 42, default, Message(0x4E9), chrome.Font);
        source.Text(panel, 43, default, draft.Row.AppearanceYear.ToString(CultureInfo.InvariantCulture), chrome.Font);
        source.Text(panel, 46, default, Message(0x4E8), chrome.Font);
        var death = source.Text(panel, 47, default, "", chrome.Font);
        var birthTrack = source.Rectangle(12);
        var disabledBirth = new UiSourceSlider { Name = "HistoricalOfficerBirthSlider", Position = birthTrack.Position, Size = birthTrack.Size };
        panel.AddChild(disabledBirth);
        disabledBirth.Configure(repository, chrome.Assets, chrome.SpriteShader, 135, 250, 1, birth, false, null);
        var birthBox = source.Rectangle(19);
        var birthButton = new UiSourceCommandButton { Name = "HistoricalOfficerBirthNumber", Position = birthBox.Position, Size = birthBox.Size };
        panel.AddChild(birthButton); buttons.Add(birthButton);
        birthButton.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "", null); birthButton.Disabled = true;
        NumericRow(panel, source, 7, 24, 25, 30, 37, [22, 23, 27, 29, 34, 36],
            [29, 27, 36, 34], 0x4C6, () => lifespan, minimum, () => 99, value =>
            {
                lifespan = value;
                // The original slider edits lifespan; its final death setter
                // saturates the candidate year rather than changing the birth.
                working = working with { DeathYear = (ushort)Math.Clamp(birth + lifespan - 1, 0, 350) };
            });

        var historyLayout = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_05_01_00_history_offset_00"));
        var host = new Control { Name = "HistoricalOfficerLifetimeHistory", Position = source.Rectangle(49).Position,
            Size = historyLayout.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(host);
        var scenarios = RegisteredOfficerBirthYears.LoadHistoryScenarios(repository, text.SourceLocale).Take(12).ToArray();
        var ages = new List<(RegisteredOfficerBirthYearScenario Scenario, UiSourceText Age)>();
        int left = (scenarios.Length + 1) / 2;
        for (int row = 0; row < scenarios.Length; row++)
        {
            int first = row < left ? 1 + row * 6 : 4 + (row - left) * 6;
            var scenario = scenarios[row];
            historyLayout.Text(host, first, default, Year(Message(0x569), scenario.StartYear), chrome.Font);
            historyLayout.Text(host, first + 1, default, scenario.Name, chrome.Font);
            ages.Add((scenario, historyLayout.Text(host, first + 2, default, "", chrome.Font)));
        }
        refreshers.Add(() =>
        {
            int year = birth + lifespan - 1;
            death.Text = year.ToString(CultureInfo.InvariantCulture);
            foreach (var (scenario, age) in ages)
                age.Text = scenario.StartYear < birth ? Message(0x55C) : scenario.StartYear > year
                    ? Message(0x55D) : Year(Message(0x55E), scenario.StartYear - birth + 1);
        });
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("history_source_layout", historyLayout.Source.Source.ResourceUri);
        SetMeta("birth_inputs_disabled", true); SetMeta("lifespan_minimum", minimum); SetMeta("lifespan_maximum", 99);
        static string Year(string format, int value) => format.Replace("%d", value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}

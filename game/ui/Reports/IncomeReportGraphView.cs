using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>The authored income graph, its four retained series and nine
/// period hit columns. Signed -1 is absence, never a fabricated zero.</summary>
internal partial class IncomeReportGraphView : Control
{
    private IncomeReportGraphPublication publication = null!;
    private UiSourceLayout source = null!, point = null!, bar = null!, balloonSource = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private MapFont font = null!;
    private Control curves = null!, balloon = null!;
    private readonly Control[] guides = new Control[9];
    private readonly bool[] enabled = [true, true, true, true];
    private readonly List<UiSourceCommandButton> buttons = [];
    private Func<bool> admitted = null!;
    private int[][] series = [];
    private readonly Texture2D[] icons = new Texture2D[4];
    private ImageTexture solid = null!;
    private int hovered = -1;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string locale, IncomeReportGraphPublication publication, Func<bool> admitted)
    {
        this.publication = publication; this.assets = assets; this.shader = shader; this.font = font; this.admitted = admitted;
        enabled[3] = publication.ActionPointsVisible;
        MouseFilter = MouseFilterEnum.Pass;
        var original = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "05_00_01_shushi_offset_01_graph"));
        var geometry = new Dictionary<int, UiSourceNodeGeometry>();
        for (int i = 0; i < 9; i++)
        {
            int positionNode = IncomeReportGraphNodeTables.PositionNodes[i], sizeNode = IncomeReportGraphNodeTables.SizeNodes[i];
            var guide = original.Nodes[positionNode]; var column = original.Nodes[sizeNode];
            geometry[positionNode] = new(Size: new((int)(guide.Value44 * .88f), guide.Value46));
            geometry[sizeNode] = new(Size: new((int)((column.Value44 - 23) * .88f), column.Value46));
        }
        foreach (int node in new[] { 20, 21, 22, 23, 24, 25 }) geometry[node] = new(Scale: new(.88f, 1));
        source = new(original.Source, geometry);
        point = new(MapMarkerLayout.LoadNamed(repository, "05_00_01_shushi_offset_01_point"));
        bar = new(MapMarkerLayout.LoadNamed(repository, "05_00_01_shushi_offset_01_bar"));
        balloonSource = new(MapMarkerLayout.LoadNamed(repository, "05_00_01_shushi_offset_02"));
        // The bar's cmn_poly_sq is a solid FreeShape (type8), not an atlas
        // sprite. Its caller replaces all four corners for every segment.
        using (var white = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8))
        { white.Fill(Colors.White); solid = ImageTexture.CreateFromImage(white); }
        var spriteSource = new LocalizedUiSpriteSource(repository);
        string[] iconNames = ["000_cmn_icon_kane", "000_cmn_icon_st_hyourou", "000_cmn_icon_heishi", "000_cmn_icon_gunjou"];
        // The military-intelligence icon is enum321 in bank24. The other
        // three belong to bank0; their names do not imply a shared atlas.
        for (int i = 0; i < icons.Length; i++)
            icons[i] = assets.SrgbSprite(iconNames[i], i == 3
                ? spriteSource.LoadTexturePackSprite("texture_ce_common", iconNames[i])
                : spriteSource.LoadCommonSprite(iconNames[i]));
        Size = source.Rectangle(0).Size;
        var messages = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records;
        foreach (var node in source.Nodes.Where(node => node.Type is 1 or 3))
        {
            if (IncomeReportGraphNodeTables.PositionNodes.IndexOf(node.Index) is int guideIndex && guideIndex >= 0)
            {
                guides[guideIndex] = source.Art(this, node.Index, source.Rectangle(node.Index), assets, shader);
                guides[guideIndex].Hide();
            }
            else if (node.Index is not (20 or 22 or 24)) source.Art(this, node.Index, source.Rectangle(node.Index), assets, shader);
        }
        int phase = publication.Date.Day < 11 ? 2 : publication.Date.Day < 21 ? 1 : 0;
        for (int month = 0; month < 3; month++)
        {
            int label = 21 + month * 2, tick = label - 1;
            float x = source.Rectangle(IncomeReportGraphNodeTables.PositionNodes[month * 3 + phase]).Position.X - 4;
            var box = source.Rectangle(label); box.Position = new(x, box.Position.Y); box.Size += new Vector2(8, 0);
            int value = (publication.Date.Month + 9 + month) % 12 + 1;
            string format = messages[value == 1 ? 0x56A : 0x611].Text ?? throw new InvalidDataException("income-report.graph_month_caption");
            source.Text(this, label, default, Format(format, value == 1
                ? [publication.Date.Year.ToString(CultureInfo.InvariantCulture), "", "1"]
                : [value.ToString(CultureInfo.InvariantCulture)]), font, rectangle: box);
            var tickBox = source.Rectangle(tick); tickBox.Position = new(x, box.Position.Y);
            if (source.Nodes[tick].Type is 1 or 3) source.Art(this, tick, tickBox, assets, shader);
        }
        curves = new() { Name = "IncomeHistoryCurves", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(curves);
        balloon = new() { Name = "IncomeHistoryBalloon", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 1 };
        AddChild(balloon); balloon.Hide();
        float pitch = source.Rectangle(IncomeReportGraphNodeTables.PositionNodes[1]).Position.X - source.Rectangle(IncomeReportGraphNodeTables.PositionNodes[0]).Position.X;
        for (int column = 0; column < 9; column++)
        {
            int index = column;
            var box = source.Rectangle(IncomeReportGraphNodeTables.PositionNodes[column]);
            var hit = new Control { Name = "IncomePeriod" + (column + 1), Position = new(box.Position.X - pitch / 2, 0),
                Size = new(pitch, box.Size.Y), MouseFilter = MouseFilterEnum.Pass };
            AddChild(hit);
            hit.MouseEntered += () => { if (admitted()) SelectPeriod(index); };
            hit.MouseExited += () => { if (hovered == index) SelectPeriod(-1); };
            hit.GuiInput += input =>
            {
                if (input is InputEventMouseMotion motion && hovered == index && admitted())
                    balloon.Position = hit.Position + motion.Position + new Vector2(20, -enabled.Count(e => e) * 50 - 10);
            };
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_line_width", 3);
    }

    internal void AddSeriesCommands(GameResourceRepository repository, Control parent, UiSourceLayout side, string locale)
    {
        var messages = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records;
        int[] captions = [0x20C, 0x20D, 0x20E, 0x835];
        for (int kind = 0; kind < 4; kind++)
        {
            int index = kind; var box = side.Rectangle(1 + kind * 2);
            var button = new UiSourceCommandButton { Name = "IncomeSeries" + kind, Position = box.Position, Size = box.Size,
                InputAdmission = admitted, Selected = enabled[kind], Disabled = kind == 3 && !publication.ActionPointsVisible };
            parent.AddChild(button); buttons.Add(button);
            button.Configure(repository, assets, shader, font, messages[captions[kind]].Text
                ?? throw new InvalidDataException("income-report.graph_series_caption"), () =>
            {
                if (!admitted() || button.Disabled) return;
                enabled[index] = !enabled[index]; button.Selected = enabled[index]; Refresh();
            });
            button.SetMeta("source_control_id", 11 + kind);
        }
    }

    internal void Apply(byte districtNumber)
    {
        NormalTurnDistrictResourceHistory row = publication.District(districtNumber);
        series = [row.Gold.ToArray(), row.Food.ToArray(), row.Troops.ToArray(), row.MilitaryIntelligence.ToArray()];
        Refresh();
    }

    internal void SetCommandsVisible(bool value)
    { foreach (var button in buttons) button.Visible = value; if (!value) SelectPeriod(-1); }

    private void Refresh()
    {
        foreach (Node child in curves.GetChildren()) { curves.RemoveChild(child); child.QueueFree(); }
        if (series.Length == 0) return;
        var palettes = MapSpritePalettes.Create();
        Rect2 plot = source.Rectangle(1);
        float width = (int)(plot.Size.X * .88f) - 3, height = plot.Size.Y;
        for (int kind = 3; kind >= 0; kind--)
        {
            if (!enabled[kind]) continue;
            Vector2? previous = null;
            for (int index = 0; index < 9; index++)
            {
                int value = series[kind][index + 1]; if (value == -1) continue;
                Vector2 coordinate = new(plot.Position.X + width * index / 8,
                    plot.Position.Y + height * (1 - value / (float)publication.UpperBound));
                if (previous is Vector2 start)
                {
                    float dx = coordinate.X - start.X, dy = coordinate.Y - start.Y;
                    float denominator = (dy * dy + dx * dx) * 4;
                    if (denominator > 0)
                    {
                        // 677F50 / 678E40: preserve the source's three-pixel
                        // solid quad, including its Y-side selection.
                        float a = MathF.Sqrt(((9 * dy) * dy) / denominator);
                        float b = MathF.Sqrt(((9 * dx) * dx) / denominator);
                        Vector2 p2 = new(start.X - a, start.Y - b), p3 = new(start.X + a, start.Y + b);
                        Vector2 p4 = new(coordinate.X - a, coordinate.Y - b), p5 = new(coordinate.X + a, coordinate.Y + b);
                        if (p3.Y < p4.Y)
                        { (p2.Y, p3.Y) = (p3.Y, p2.Y); (p4.Y, p5.Y) = (p5.Y, p4.Y); }
                        var segment = new Node2D { Name = "IncomeHistorySegment",
                            Transform = new(p5 - p3, p2 - p3, p3) };
                        curves.AddChild(segment);
                        var fill = new UiSourceSprite { Size = Vector2.One };
                        segment.AddChild(fill);
                        fill.Configure(shader, solid, UiSourceSprite.Blend.Ordinary, palettes[67 + kind]);
                        fill.SetMeta("source_layout", bar.Source.Source.ResourceUri);
                        fill.SetMeta("source_node_index", 1);
                    }
                }
                previous = coordinate;
                Rect2 anchor = source.Rectangle(IncomeReportGraphNodeTables.PositionNodes[index]), column = source.Rectangle(IncomeReportGraphNodeTables.SizeNodes[index]);
                var dot = new Control { Name = "IncomeHistoryPoint", Position = new(anchor.Position.X - (point.Rectangle(0).Size.X - 1) / 2,
                    column.Position.Y + (1 - value / (float)publication.UpperBound) * column.Size.Y - point.Rectangle(0).Size.Y / 2),
                    MouseFilter = MouseFilterEnum.Ignore };
                curves.AddChild(dot);
                foreach (var node in point.Nodes.Where(n => n.Type is 1 or 3))
                    point.Art(dot, node.Index, point.Rectangle(node.Index), assets, shader, palettes[67 + kind]);
            }
        }
        if (hovered >= 0) SelectPeriod(hovered);
    }

    private void SelectPeriod(int index)
    {
        hovered = index;
        for (int i = 0; i < guides.Length; i++) guides[i].Visible = i == index;
        foreach (Node child in balloon.GetChildren()) { balloon.RemoveChild(child); child.QueueFree(); }
        balloon.Visible = index >= 0 && series.Length != 0;
        if (!balloon.Visible) return;
        var palettes = MapSpritePalettes.Create(); var presets = MapTextPresets.Create();
        int row = 0;
        for (int kind = 0; kind < 4; kind++)
        {
            if (!enabled[kind]) continue;
            int value = series[kind][index + 1], previous = series[kind][index];
            int delta = previous == -1 || value == -1 ? 0 : unchecked(value - previous);
            if (delta == -1) delta = 0; //6831B0 also treats a computed -1 as its absence sentinel.
            //A disabled or absent row contributes no visible unit; the original
            //controller still reserves one unit slot for an enabled absent row.
            Vector2 mount = new(0, row++ * (balloonSource.Rectangle(0).Size.Y - 6));
            if (value == -1) continue;
            foreach (var node in balloonSource.Nodes.Where(n => n.Type is 1 or 3))
                if (node.Index != 4) balloonSource.Art(balloon, node.Index, balloonSource.Rectangle(node.Index, mount), assets, shader);
            var iconBox = balloonSource.Rectangle(4, mount);
            var icon = new UiSourceSprite { Position = iconBox.Position, Size = iconBox.Size };
            balloon.AddChild(icon); icon.Configure(shader, icons[kind], UiSourceSprite.Blend.Ordinary, palettes[67 + kind]);
            balloonSource.Text(balloon, 5, mount, value.ToString(CultureInfo.InvariantCulture), font);
            var change = balloonSource.Text(balloon, 6, mount, "", font);
            change.SetRuns(new("("), new(delta >= 0 ? "+" + delta : delta.ToString(CultureInfo.InvariantCulture),
                delta > 0 ? presets[38].LT.CpuPacked : delta < 0 ? presets[40].LT.CpuPacked : null), new(")"));
        }
        balloon.Position = GetLocalMousePosition() + new Vector2(20, -enabled.Count(e => e) * 50 - 10);
    }

    private static string Format(string format, string[] arguments)
    {
        int position = 0;
        return System.Text.RegularExpressions.Regex.Replace(format, "%[-+0-9.]*[ds]", _ =>
            position < arguments.Length ? arguments[position++] : throw new InvalidDataException("income-report.graph_month_format"));
    }

    public override void _ExitTree()
    { admitted = () => false; foreach (var button in buttons) button.Abandon(); solid?.Dispose(); }
}

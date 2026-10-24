namespace San14.Data;

public readonly record struct SceneryMapNameWidthRequests(int Name, int Panel, int HitArea, int Root);

/// <summary>The actual Scenery controller's county layouts. These resources
/// describe presentation only; a live Scenery Unit supplies the world anchor.</summary>
public sealed class SceneryMapMarkerSources
{
    public MapMarkerLayout Root { get; }
    public MapMarkerLayout Text => _county.Text;
    public MapMarkerLayout Flag => _county.Flag;
    private readonly AreaMapMarkerSources _county;

    public const int NameNode = 22;
    public const int NameTextPreset = 19;
    public const int TextMountNode = 2;
    public const int FlagMountNode = 9;

    private SceneryMapMarkerSources(MapMarkerLayout root, AreaMapMarkerSources county)
        => (Root, _county) = (root, county);

    public static SceneryMapMarkerSources Load(GameResourceRepository repository)
        => new(MapMarkerLayout.Load(repository, "00_81_07_county_flag", 0x855A1850, "LCSK4500"),
            AreaMapMarkerSources.LoadCurrentTc(repository));

    /// <summary>19EB50's seven camera rows and four MapInfo columns. Other
    /// MapInfo values use column zero; the separate override gate is a caller concern.</summary>
    public static int DisplayLevel(int cameraLevel, int mapInfoMode)
    {
        if ((uint)cameraLevel >= 7)
            throw new InvalidDataException("Scenery camera level is outside the display table.");
        ReadOnlySpan<int> levels =
        [
             0,  0,  0,  0,
             0,  0,  0,  0,
             0,  0,  0,  0,
             1,  1,  0,  1,
            -1, -1,  0, -1,
            -1, -1,  1, -1,
            -1, -1, -1, -1
        ];
        int column = mapInfoMode switch { 1 => 1, 8 => 2, 12 => 3, _ => 0 };
        return levels[cameraLevel * 4 + column];
    }

    /// <summary>19CCA0 treats the selected display value as unsigned.
    /// Zero displays both children, one only Flag, and all other values neither.</summary>
    public static (bool Text, bool Flag) VisibleChildren(int displayLevel)
        => (displayLevel == 0, unchecked((uint)displayLevel) < 2);

    /// <summary>1A5000 passes UTF16 code-unit count times two to 1A8820.
    /// These are writer requests: the three node widths are stored as signed
    /// shorts by the layout boundary, while the Control root width is int32.
    /// A native-invalid receiver only clears the text and skips this writer;
    /// a valid empty name still uses the zero-length calculation.</summary>
    public SceneryMapNameWidthRequests NameWidthRequests(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        int bytes = unchecked(name.Length * 2);
        int height = Text.Nodes[NameNode].Value46;
        int nameWidth = unchecked(height * bytes) / 2;
        int extra = (int)(((float)bytes * .5f - 1f) * height);
        int panelWidth = unchecked((byte)Text.Nodes[6].Value44 + extra);
        int hitWidth = unchecked((byte)Text.Nodes[1].Value44 + extra);
        int rootWidth = unchecked((int)Text.Nodes[1].HeaderValues[0] + hitWidth
            - (int)Text.Nodes[0].HeaderValues[0]);
        return new(nameWidth, panelWidth, hitWidth, rootWidth);
    }
}

using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The source's four research states, each with its authored pointer states.</summary>
internal partial class DoctrineResearchTileButton : UiSourceButton
{
    private static readonly int[][] Tokens = [[0, 1, 2], [3, 4, 4], [5, 6, 6], [7, 8, 8]];
    private UiSourceSprite picture = null!;
    private readonly Texture2D[] textures = new Texture2D[9];
    private int status, shown = -1;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, DoctrineResearchRow row)
    {
        Size = new(128, 128); FocusMode = FocusModeEnum.None; ActionMode = ActionModeEnum.Release;
        var source = new UiSourceLayout(repository, 0xE2E26093);
        var box = source.Rectangle(1);
        picture = new() { Position = box.Position, Size = box.Size };
        AddChild(picture);
        var uv = new LcskUvQuad(new(0, 0), new(1, 0), new(0, 1), new(1, 1));
        for (int token = 0; token < 9; token++)
        {
            string name = SpriteName(row.Category, row.FirstDoctrineId != 0 && row.RestrictToFirstDoctrine, token);
            textures[token] = assets.SrgbTexturePackSpritePart("texture_ce_06_shugi", name, uv);
        }
        picture.Configure(shader, textures[0], UiSourceSprite.Blend.Ordinary);
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        ButtonDown += QueueRedraw; ButtonUp += QueueRedraw;
        Apply(row);
    }

    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && new Rect2(21, 26, 86, 76).HasPoint(point);
    internal void Apply(DoctrineResearchRow row)
    {
        status = row.Researched ? row.Pending ? 3 : 2 : row.CanResearch == true ? 0 : 1;
        QueueRedraw();
    }
    public override void _Draw()
    {
        int pointer = !SourceInputAdmitted ? 0 : GetDrawMode() is DrawMode.Pressed or DrawMode.HoverPressed ? 2 : IsHovered() ? 1 : 0;
        int token = Tokens[status][pointer];
        if (token == shown) return;
        shown = token; picture.SetSource(textures[token]);
    }
    private static string SpriteName(int category, bool special, int token)
    {
        if (category == 0) return "06_str_btn_shugi_skill_06_0";
        if (category == 7) return "06_str_btn_shugi_skill_lock_" + Math.Min(token, 6 - (token & 1));
        string prefix = category switch
        { 1 => "05", 2 => "00", 3 => "04", 4 => "02", 5 => "03", 6 => "01", _ => throw new InvalidDataException("Unknown doctrine tile category.") };
        return "06_str_btn_shugi_skill_" + prefix + "_" + token + (special ? "_sp" : "");
    }
}

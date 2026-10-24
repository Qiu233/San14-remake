using Godot;
using San14.Data;

internal sealed record PersonnelRewardCardValues(int OfficerId, string Name, LoadedSprite? Portrait,
    string LoyaltyBefore, string LoyaltyAfter, bool ShowChange);

/// <summary>One original reward result row. Eighteen mounted instances are
/// reused when the user changes pages; this is a preview, never an order.</summary>
internal partial class PersonnelRewardCardView : Control
{
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private UiSourceLayout source = null!;
    private UiSourceText name = null!, before = null!, after = null!;
    private Control arrow = null!, portraitHost = null!;
    private UiSourcePortrait? portrait;
    private LoadedSprite? currentPortrait;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, bool dark)
    {
        this.repository = repository; this.assets = assets; this.shader = shader;
        source = new(repository, dark ? 0x31B048DCu : 0xBD71107Du);
        Size = source.Rectangle(0).Size; MouseFilter = MouseFilterEnum.Ignore;
        source.Art(this, 1, source.Rectangle(1), assets, shader);
        portraitHost = new() { Name = "RewardOfficerPortrait", Size = Size, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(portraitHost);
        source.Art(this, 4, source.Rectangle(4), assets, shader);
        var normal = MapTextPresets.Create()[0];
        name = source.Text(this, 5, default, "", font, normal);
        before = source.Text(this, 6, default, "", font, normal);
        after = source.Text(this, 7, default, "", font, normal);
        arrow = source.Art(this, 8, source.Rectangle(8), assets, shader);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Hide();
    }

    internal void Apply(PersonnelRewardCardValues? value)
    {
        if (value is null) { Hide(); return; }
        name.Text = value.Name; before.Text = value.LoyaltyBefore;
        after.Visible = arrow.Visible = value.ShowChange;
        after.SetKnownColourLine(value.LoyaltyAfter);
        if (!ReferenceEquals(currentPortrait, value.Portrait))
        {
            if (portrait is not null) { portrait.Hide(); portraitHost.RemoveChild(portrait); portrait.QueueFree(); portrait = null; }
            currentPortrait = value.Portrait;
            if (value.Portrait is { } sprite)
            {
                Rect2 box = source.Rectangle(2);
                portrait = new() { Position = box.Position, Size = box.Size };
                portraitHost.AddChild(portrait); portrait.Configure(repository, assets, shader, sprite);
            }
        }
        Show();
    }
}

using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary deployment draft has one source Move entry. Its
/// permission is supplied by the existing empty-Hex query.</summary>
internal partial class DeploymentMoveMenu : ArmyCommandMenu
{
    internal void Configure(GameResourceRepository repository, PreviewText text, Action released, Action<string?> hintChanged)
    {
        string caption = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[0x0261].Text
            ?? throw new InvalidDataException("Missing source Move caption.");
        string hint = repository.LoadMessages(text.SourceLocale, MessageKind.Game).Messages.Sections[2].Records[0x0881].Literals.Single().Text;
        base.Configure(repository, text, [new("Move", caption, "000_cmn_icon_bt_kidou", hint, true, released)], hintChanged);
        Name = "DeploymentMoveMenu";
    }
}

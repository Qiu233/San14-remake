using Godot;
using San14.Data;

/// <summary>The source numeric button. Its caller owns the calculator dialog.</summary>
internal partial class UiSourceNumberButton : UiSourceCommandButton
{
    internal int Value { set=>Caption=value.ToString(System.Globalization.CultureInfo.InvariantCulture); }

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        int value,Action? pressed)
        => base.Configure(repository,assets,shader,font,
            value.ToString(System.Globalization.CultureInfo.InvariantCulture),pressed);
}

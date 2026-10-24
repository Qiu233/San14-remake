using Godot;
using San14.Data;

/// <summary>The ordinary common header, borrowing its page's render resources.</summary>
internal partial class UiSourceHeader : Control
{
    private UiSourceHeaderStack? owner;
    private UiSourceText captionText = null!;
    private bool configured;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string caption)
    {
        var source = new UiSourceLayout(repository, 0xDC8AC8AA);
        Size = source.Rectangle(0).Size;
        MouseFilter = MouseFilterEnum.Ignore;
        source.Art(this, 1, source.Rectangle(1), assets, shader);
        captionText = source.Text(this, 2, default, caption, font, MapTextPresets.Create()[56]);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        configured = true;
    }

    internal void ApplyCaption(string caption) => captionText.Text = caption;

    internal void Publish(UiSourceHeaderStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        if (!configured || !IsInsideTree() || IsQueuedForDeletion())
            throw new InvalidOperationException("Only a configured, mounted header can be published.");
        if (ReferenceEquals(owner, stack)) return;
        if (owner is not null) throw new InvalidOperationException("The header already belongs to another presentation owner.");
        stack.Push(this);
        owner = stack;
    }

    public override void _ExitTree()
    {
        var previousOwner = owner;
        owner = null;
        previousOwner?.Remove(this);
    }
}

using Godot;
using San14.Data;

/// <summary>Resolved source artwork, without any mutable controls or materials.</summary>
internal sealed record UiSourceArtPart(Vector2 Position, Vector2 Size, Texture2D Texture,
    UiSourceSprite.Blend Blend, MapCornerColors? Colors, UiSourceStaticArtMaterial? Material,
    bool FlipU, bool FlipV, Rect2? SamplingRectangle, Rect2? AddressedRectangle,
    UiAssets.SourceSampling Addressing);

internal sealed record UiSourceArtPlan(string Name, int NodeIndex, string ResourceUri,
    Vector2 Position, Vector2 Size, Vector2 Scale, Transform2D? Projection,
    UiSourceArtPart[] Parts);

/// <summary>One current artwork tree. Authored states share resolved source
/// plans, while every control keeps its own sprites, materials and drawing RIDs.</summary>
internal sealed class UiSourceArtwork
{
    private readonly UiSourceArtPlan[] plans;
    private readonly UiSourceSprite[] parts;
    private readonly Node2D? projection;
    private int current;
    internal Control Root { get; }

    internal UiSourceArtwork(CanvasItem parent, Shader shader, UiSourceArtPlan[] states)
    {
        if (states.Length == 0) throw new ArgumentException("Artwork requires an authored state.", nameof(states));
        plans = states;
        var first = plans[0];
        Root = new Control { Name = first.Name, Position = first.Position, Size = first.Size,
            Scale = first.Scale, MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(Root);
        Root.SetMeta("source_node_index", first.NodeIndex);
        Root.SetMeta("source_layout", first.ResourceUri);
        Node canvas = Root;
        if (plans.Any(plan => plan.Projection is not null))
        {
            // A state may change projection. Keep one parent rather than
            // reparenting live sprites, which would retire their materials.
            projection = new Node2D { Name = "SourceProjection", Transform = first.Projection ?? Transform2D.Identity };
            Root.AddChild(projection); canvas = projection;
        }
        // This is the finite authored state's largest part count, not a
        // history of visited pages or dynamically accumulated artwork.
        int capacity = plans.Max(plan => plan.Parts.Length);
        parts = new UiSourceSprite[capacity];
        for (int index = 0; index < parts.Length; index++)
        {
            var seed = plans.First(plan => plan.Parts.Length > index).Parts[index];
            var sprite = new UiSourceSprite { Position = seed.Position, Size = seed.Size };
            if (index >= first.Parts.Length) sprite.Hide();
            canvas.AddChild(sprite);
            sprite.ConfigureArtwork(shader, seed);
            parts[index] = sprite;
        }
    }

    internal void SetEncodedPortraitSampling(LoadedUiSpriteSource source)
    {
        foreach (var part in parts) part.SetEncodedPortraitSampling(source.Image);
    }

    internal void Apply(int state)
    {
        if ((uint)state >= (uint)plans.Length) throw new ArgumentOutOfRangeException(nameof(state));
        if (current == state) return;
        var plan = plans[state];
        var previous = plans[current];
        if (ReferenceEquals(plan, previous)) { current = state; return; }
        Root.Position = plan.Position; Root.Size = plan.Size; Root.Scale = plan.Scale;
        Root.SetMeta("source_node_index", plan.NodeIndex);
        if (projection is not null) projection.Transform = plan.Projection ?? Transform2D.Identity;
        for (int index = 0; index < parts.Length; index++)
        {
            var sprite = parts[index];
            if (index >= plan.Parts.Length)
            {
                if (index < previous.Parts.Length) sprite.Hide();
                continue;
            }
            var part = plan.Parts[index];
            sprite.Position = part.Position; sprite.Size = part.Size;
            sprite.ApplyArtwork(part);
            if (index >= previous.Parts.Length) sprite.Show();
        }
        current = state;
    }
}

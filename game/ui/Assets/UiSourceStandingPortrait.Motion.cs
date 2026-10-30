using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal partial class UiSourceStandingPortrait
{
    private sealed class MotionLayer(LcskNodeRecord source, UiSourceSprite image)
    {
        internal readonly UiSourceSprite Image = image;
        internal readonly Vector2 InitialPosition = image.Position;
        internal readonly Vector2 InitialSourcePosition = new(source.HeaderValues[0], source.HeaderValues[1]);
        internal readonly Vector2 InitialScale = new(source.HeaderValues[7], source.HeaderValues[8]);
        internal Vector2 Position = new(source.HeaderValues[0], source.HeaderValues[1]);
        internal Vector2 Scale = new(source.HeaderValues[7], source.HeaderValues[8]);
        internal uint Color = BinaryPrimitives.ReadUInt32LittleEndian(source.RawBytes.AsSpan(120, 4));
        internal bool GeometryChanged, ColorChanged;
    }
    private StandingPortraitMotion? motion;
    private readonly Dictionary<int, MotionLayer> motionLayers = [];

    private void ConfigureMotion(IReadOnlyList<LoadedSprite> sprites)
    {
        var nodes = LcskNodeReader.Read(storyLayout!);
        foreach (var (id, image) in storyLayers)
        {
            var node = nodes.Single(node => node.Index == id);
            image.PivotOffset = (node.Value48 & 255) switch
            {
                0x11 => image.Size / 2,
                0x12 => new(image.Size.X / 2, image.Size.Y),
                _ => throw new InvalidDataException("standing.motion.source_anchor")
            };
            motionLayers.Add(id, new(node, image));
        }
        bool ordinary = frameAnimation is null;
        motion = StandingPortraitMotion.Read(storyLayout!, motionLayers.Keys.ToArray(), ordinary,
            (sprites as LoadedStandingPortraitSprites)?.CurrentAppearanceIndex);
        ApplyMotion(motion.SetAnimationEnabled(AnimateFrames));
        RefreshProcessing();
    }

    private void ApplyMotion(IReadOnlyList<StandingPortraitPropertyValue> values)
    {
        foreach (var value in values)
        {
            var layer = motionLayers[value.NodeId];
            switch (value.Channel)
            {
                case 0: layer.Position.X = (float)value.Value; layer.GeometryChanged = true; break;
                case 1: layer.Position.Y = (float)value.Value; layer.GeometryChanged = true; break;
                case 2:
                    if (value.Value != 0) throw new InvalidDataException("standing.motion.nonplanar_position");
                    break;
                case 6: layer.Scale.X = (float)value.Value; layer.GeometryChanged = true; break;
                case 7: layer.Scale.Y = (float)value.Value; layer.GeometryChanged = true; break;
                case >= 16 and <= 19:
                    int shift = (value.Channel - 16) * 8;
                    uint component = (byte)(int)Math.Clamp(value.Value * 255d, 0d, 255d);
                    layer.Color = (layer.Color & ~(255u << shift)) | component << shift;
                    layer.ColorChanged = true;
                    break;
            }
        }
        foreach (var layer in motionLayers.Values)
        {
            if (layer.GeometryChanged)
            {
                var position = layer.Position - layer.InitialSourcePosition;
                // Curve values replace original position/scale. The static
                // geometry already contains the original scale and pivot.
                layer.Image.Position = layer.InitialPosition + new Vector2(position.X, -position.Y);
                layer.Image.Scale = layer.Scale / layer.InitialScale;
                layer.GeometryChanged = false;
            }
            if (layer.ColorChanged)
            {
                // Native stores four bytes, while the original ordinary
                // shader consumes RGB only. Keep the existing shared route.
                layer.Image.SetMaterialColorScale(layer.Color);
                layer.ColorChanged = false;
            }
        }
    }

    private void RefreshProcessing() => SetProcess(
        AnimateFrames && frameAnimation is not null || motion?.NeedsUpdate == true);
}

using Godot;
using San14.Data;

/// <summary>One settled, resource-owned main-command presentation state.
/// Curves were sampled by Data at load; the HUD never samples them per frame.</summary>
internal sealed class MainCommandLayoutState
{
    internal UiSourceLayout Layout { get; }
    private readonly Dictionary<int, Dictionary<byte, double>> values = [];

    internal MainCommandLayoutState(MainCommandLayoutAsset source,
        MainCommandLayoutStableFrame frame, int? rotationNode = null, float sourceAngle = 0)
    {
        foreach (var value in frame.Values)
        {
            if (!values.TryGetValue(value.NodeId, out var channels)) values[value.NodeId] = channels = [];
            channels[value.Channel] = value.Value;
        }
        var geometry = new Dictionary<int, UiSourceNodeGeometry>();
        foreach (var node in source.Nodes)
        {
            values.TryGetValue(node.Index, out var channels);
            if (channels is null && rotationNode != node.Index) continue;
            var fields = node.HeaderValues;
            float Read(byte channel, float fallback)
            {
                if (channels is not null && channels.TryGetValue(channel, out var value)) return (float)value;
                return fallback;
            }
            if (Read(2, fields[2]) != 0 || Read(3, 0) != 0 || Read(4, 0) != 0)
                throw new InvalidDataException("main_command.layout.planar_geometry");
            float? angle = rotationNode == node.Index ? -Mathf.DegToRad(sourceAngle) :
                channels?.ContainsKey(5) == true ? -Mathf.DegToRad(Read(5, 0)) : null;
            geometry[node.Index] = new(Offset: new(Read(0, fields[0]), -Read(1, fields[1])),
                Scale: new(Read(6, fields[7]), Read(7, fields[8])), RotationRadians: angle);
        }
        //As for all mounted source layouts, the caller replaces the designer
        //root. Its absolute-viewport root coordinates are not added a second time.
        Layout = new(source.Source, geometry);
    }

    internal float Opacity(int node)
    {
        float opacity = 1;
        for (int? index = node; index is int current; index = Layout.Nodes[current].ParentIndex)
            if (values.TryGetValue(current, out var channels) && channels.TryGetValue(10, out double alpha))
                opacity *= (float)alpha;
        return opacity;
    }

    internal Control Art(Control parent, int node, UiAssets assets, Shader shader,
        UiSourceSprite.Blend blend = UiSourceSprite.Blend.Ordinary)
    {
        var art = Layout.Art(parent, node, Layout.Rectangle(node), assets, shader, blend: blend);
        art.Modulate = new(1, 1, 1, Opacity(node));
        return art;
    }
}

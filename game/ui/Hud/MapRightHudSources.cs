using Godot;
using San14.Data;

/// <summary>Read-only settled source poses shared by the right HUD's actual
/// controls. Layout descriptors remain in the application's existing assets.</summary>
internal static class MapRightHudSources
{
    internal static UiSourceLayout DispatchSlot(UiAssets assets)
    {
        var source=assets.SourceLayout("00_82_02_jump_offset_21");
        // 887A80 plays jump_otsukai_in during construction. Its node1 has
        // actual X/Y keys; the exported designer Y19.46393 is not its final
        // pose. Preserve the caller's mount and use the real keyed endpoint.
        var clip=LcskAnimationReader.Read(source.Source.Layout,new HashSet<string>{"jump_otsukai_in"}).Single();
        float End(int channel)
        {
            var track=clip.Tracks.Single(t=>t.NodeId==1&&t.Channel==channel&&t.Curve.Keys.Count>0);
            return (float)track.Curve.Sample(clip.EndFrame);
        }
        return new UiSourceLayout(source.Source,new Dictionary<int,UiSourceNodeGeometry>
        { [1]=new(Origin:new Vector2(End(0),-End(1))) });
    }
}

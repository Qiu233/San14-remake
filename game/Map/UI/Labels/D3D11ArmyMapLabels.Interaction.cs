using Godot;
using San14.Data;
using San14.Data.Formats;

internal partial class D3D11ArmyMapLabels
{
    private int? interactionHoveredArmy, interactionPressedArmy;
    private readonly Dictionary<(MapMarkerLayout Layout, int State), LcskAnimationClip> interactionClips = [];

    private LcskAnimationClip InteractionClip(MapMarkerLayout source, int state)
    {
        if (interactionClips.TryGetValue((source, state), out var retained)) return retained;
        // These resources retain different authored trailing spaces. They are
        // identities, not normalized display names.
        string name = state == 1
            ? ReferenceEquals(source, _source.SingleFlag) || ReferenceEquals(source, _source.GroupFlag) ? "1_focus" : "1_focus "
            : ReferenceEquals(source, _source.Face) ? "2_push" : "2_push ";
        var clip = LcskAnimationReader.Read(source.Layout, new HashSet<string> { name }).Single();
        interactionClips.Add((source, state), clip);
        return clip;
    }

    // Pointer admission belongs to the map surface. This consumes only its
    // accepted same-frame Army hit, never a world Hex or a retained selection.
    internal void UpdateInteractionPointer(Vector2 point, MainMapCameraFrame frame, bool? pressed)
    {
        int? hovered = TryPick(point, frame, out var hit) ? hit.UnitId : null;
        if (pressed == true) interactionPressedArmy = hovered;
        else if (pressed == false) interactionPressedArmy = null;
        if (interactionHoveredArmy == hovered && pressed is null) return;
        int? previous = interactionHoveredArmy;
        interactionHoveredArmy = hovered;
        if (previous is int old && _cards.TryGetValue(old, out var oldCard)) oldCard.RefreshInteractionInk();
        if (hovered is int current && _cards.TryGetValue(current, out var card)) card.RefreshInteractionInk();
    }

    internal void ClearInteractionPointer()
    {
        int? previous = interactionHoveredArmy;
        interactionHoveredArmy = interactionPressedArmy = null;
        if (previous is int old && _cards.TryGetValue(old, out var card)) card.RefreshInteractionInk();
    }

    internal partial class ArmyMapCard
    {
        private readonly Dictionary<(int Display, bool Group, int State), Control> interactionInk = [];
        private readonly Dictionary<Control, Control> interactionNumberInk = [];
        private (bool Outline, MapCornerColors Tints, float Multiplier)? simpleInkStyle;
        internal int InteractionState => _owner.interactionHoveredArmy == Fields.UnitId
            ? _owner.interactionPressedArmy == Fields.UnitId ? 2 : 1 : 0;

        internal void RefreshInteractionInk()
        {
            int state = InteractionState;
            if (IsSimple)
            {
                var current = (SimpleUsesOutline, SimpleTints, _simpleRgbMultiplier);
                if (simpleInkStyle is { } previous && previous != current)
                    foreach (var key in interactionInk.Keys.Where(key => key.Display == 2).ToArray())
                    { var stale = interactionInk[key]; interactionInk.Remove(key); RemoveChild(stale); stale.QueueFree(); }
                simpleInkStyle = current;
            }
            foreach (var ink in interactionInk.Values) ink.Hide();
            foreach (var ink in interactionNumberInk.Values) ink.Hide();
            if (state != 0)
            {
                var key = (Presentation.Display, IsGroup, state);
                if (!interactionInk.TryGetValue(key, out var ink))
                {
                    ink = CreateInteractionInk(state);
                    interactionInk.Add(key, ink);
                    AddChild(ink); MoveChild(ink, 0); // the ordinary portrait and numbers remain above their frame
                    if (interactionNumberInk.TryGetValue(ink, out var number))
                    { AddChild(number); MoveChild(number, _details.GetIndex()); }
                }
                ink.Show();
                // Runtime force colors can change without replacing the Army.
                foreach (var child in ink.GetChildren().OfType<Control>())
                {
                    byte alpha = child.HasMeta("army_details_ink") ? Presentation.DetailsAlpha :
                        IsSimple ? Presentation.SimpleAlpha : IsGroup ? Presentation.GroupAlpha : Presentation.PortraitAlpha;
                    Color tint = child.HasMeta("army_force_fill") ? ForceTint : IsSimple
                        ? new(_simpleRgbMultiplier, _simpleRgbMultiplier, _simpleRgbMultiplier, 1) : Colors.White;
                    child.SelfModulate = Alpha(tint, alpha);
                }
                if (interactionNumberInk.TryGetValue(ink, out var numberInk))
                {
                    numberInk.Modulate = Alpha(Colors.White, Presentation.DetailsAlpha);
                    numberInk.Show();
                }
            }
            QueueRedraw(); _details.QueueRedraw();
        }

        private Control CreateInteractionInk(int state)
        {
            var ink = new Control { Name = $"ArmySourceInteraction{(IsSimple ? "Simple" : IsGroup ? "Group" : "Normal")}{state}", MouseFilter = MouseFilterEnum.Ignore };
            if (IsSimple)
            {
                var source = IsGroup ? _owner._source.GroupFlag : _owner._source.SingleFlag;
                var layout = new UiSourceLayout(new LoadedLayout(source.Layout, source.Source));
                int first = state == 1 ? 6 : 11;
                string basis = IsGroup ? SimpleUsesOutline ? "000_cmn_prt_sign_gunzei_s_outline" : "000_cmn_prt_sign_gunzei_s_base"
                    : SimpleUsesOutline ? "000_cmn_prt_sign_unit_s_outline" : "000_cmn_prt_sign_unit_s_base";
                string fill = IsGroup ? "000_cmn_prt_sign_gunzei_s" : "000_cmn_prt_sign_unit_s";
                foreach (int node in new[] { first, first + 1, first + 2, first + 3 })
                {
                    bool force = node >= first + 2;
                    var art = layout.Art(ink, node, layout.Rectangle(node, new(-16, -16)), _owner._assets, _owner._speechShader,
                        force ? null : SimpleTints, force ? fill : basis,
                        blend: node % 5 is 2 or 4 ? UiSourceSprite.Blend.Additive : UiSourceSprite.Blend.Ordinary);
                    SetAlpha(art, source, node, _owner.InteractionClip(source, state));
                    if (force) art.SetMeta("army_force_fill", true);
                }
            }
            else if (IsGroup)
            {
                var source = _owner._source.Grouping;
                var layout = new UiSourceLayout(new LoadedLayout(source.Layout, source.Source));
                int first = state == 1 ? 7 : 12;
                foreach (int node in new[] { first, first + 1, first + 2, first + 3 })
                {
                    var art = layout.Art(ink, node, layout.Rectangle(node), _owner._assets, _owner._speechShader,
                        blend: node == first + 1 || node == first + 3 ? UiSourceSprite.Blend.Additive : UiSourceSprite.Blend.Ordinary);
                    SetAlpha(art, source, node, _owner.InteractionClip(source, state));
                    if (node >= first + 2) art.SetMeta("army_force_fill", true);
                }
            }
            else
            {
                var source = _owner._source.Face;
                var face = new UiSourceLayout(new LoadedLayout(source.Layout, source.Source));
                Vector2 offset = Rectangle(_owner._geometry.FaceBase).Position - face.Rectangle(4).Position;
                int first = state == 1 ? 11 : 19;
                foreach (int node in new[] { first, first + 1, first + 2, first + 3 })
                {
                    var art = face.Art(ink, node, face.Rectangle(node, offset), _owner._assets, _owner._speechShader,
                        blend: node == first + 1 || node == first + 3 ? UiSourceSprite.Blend.Additive : UiSourceSprite.Blend.Ordinary);
                    SetAlpha(art, source, node, _owner.InteractionClip(source, state));
                    if (node >= first + 2) art.SetMeta("army_force_fill", true);
                }
                if (Presentation.Display == 0)
                {
                    source = _owner._source.Normal;
                    var normal = new UiSourceLayout(new LoadedLayout(source.Layout, source.Source));
                    var number = new Control { Name = $"ArmySourceNumberInteraction{state}", MouseFilter = MouseFilterEnum.Ignore };
                    interactionNumberInk.Add(ink, number);
                    int panel = state == 1 ? 0x2B : 0x2E;
                    for (int node = panel; node <= panel + 1; node++)
                    {
                        var art = normal.Art(number, node, normal.Rectangle(node), _owner._assets, _owner._speechShader,
                            _owner._palettes[state + 4], blend: node == panel + 1 ? UiSourceSprite.Blend.Additive : UiSourceSprite.Blend.Ordinary);
                        SetAlpha(art, source, node, _owner.InteractionClip(source, state));
                    }
                }
            }
            ink.SetMeta("source_interaction_state", state);
            ink.SetMeta("source_static_settled_only", true);
            return ink;
        }

        private static void SetAlpha(Control art, MapMarkerLayout source, int node, LcskAnimationClip clip)
        {
            float alpha = 1;
            for (int? current = node; current is int id; current = source.Nodes[id].ParentIndex)
                foreach (var track in clip.Tracks)
                    if (track.NodeId == id && track.Channel == 10) alpha *= Math.Clamp((float)track.Curve.Sample(clip.EndFrame), 0, 1);
            art.Modulate = new(1, 1, 1, alpha);
        }
    }
}
